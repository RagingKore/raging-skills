# DCB Event Store Adapters

Implementing DCB with various event stores. DCB requires tag-based querying and conditional append—traditional stream-based stores need adaptation layers.

## Table of Contents

1. [Native DCB Stores](#native-dcb-stores)
2. [PostgreSQL Adapter](#postgresql-adapter)
3. [KurrentDB Adapter](#kurrentdb-adapter)
4. [Marten Adapter](#marten-adapter)
5. [Adapter Selection Guide](#adapter-selection-guide)

---

## Native DCB Stores

These stores support DCB operations natively:

| Store                  | Type        | API  | Notes                                          |
|------------------------|-------------|------|------------------------------------------------|
| **Axon Server 2025.1** | Commercial  | gRPC | Enterprise support, Axon Framework integration |
| **EventSourcingDB**    | Commercial  | HTTP | Purpose-built for DCB                          |
| **UmaDB**              | Open Source | gRPC | Rust, MVCC design                              |

For native stores, use their official SDKs directly.

---

## PostgreSQL Adapter

Custom PostgreSQL implementation with GIN indexes for tag-based queries.

### Schema

```sql
CREATE TABLE dcb_events (
    sequence_position BIGSERIAL PRIMARY KEY,
    event_type TEXT NOT NULL,
    event_data JSONB NOT NULL,
    tags TEXT[] NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    metadata JSONB
);

-- GIN index for tag array containment queries
CREATE INDEX idx_dcb_events_tags ON dcb_events USING GIN (tags);

-- Composite index for type + tags queries
CREATE INDEX idx_dcb_events_type_tags ON dcb_events (event_type, tags);

-- Function for atomic conditional append
CREATE OR REPLACE FUNCTION dcb_append_if_no_conflict(
    p_event_type TEXT,
    p_event_data JSONB,
    p_tags TEXT[],
    p_fail_if_types TEXT[],
    p_fail_if_tags TEXT[],
    p_after_position BIGINT
) RETURNS BIGINT AS $$
DECLARE
    v_conflict_exists BOOLEAN;
    v_new_position BIGINT;
BEGIN
    -- Check for conflicting events
    SELECT EXISTS (
        SELECT 1 FROM dcb_events
        WHERE sequence_position > p_after_position
        AND (p_fail_if_types IS NULL OR event_type = ANY(p_fail_if_types))
        AND (p_fail_if_tags IS NULL OR tags @> p_fail_if_tags)
    ) INTO v_conflict_exists;
    
    IF v_conflict_exists THEN
        RAISE EXCEPTION 'Concurrency conflict: events matching condition found after position %', p_after_position;
    END IF;
    
    -- Insert the event
    INSERT INTO dcb_events (event_type, event_data, tags)
    VALUES (p_event_type, p_event_data, p_tags)
    RETURNING sequence_position INTO v_new_position;
    
    RETURN v_new_position;
END;
$$ LANGUAGE plpgsql;
```

### C# Adapter Implementation

```csharp
public class PostgresDcbEventStore : IDcbEventStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IEventSerializer _serializer;
    
    public PostgresDcbEventStore(NpgsqlDataSource dataSource, IEventSerializer serializer)
    {
        _dataSource = dataSource;
        _serializer = serializer;
    }
    
    public async IAsyncEnumerable<DcbEventEnvelope> ReadAsync(
        DcbQuery query,
        long afterPosition = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sql = BuildReadSql(query);
        
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        
        AddQueryParameters(cmd, query, afterPosition);
        
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        
        while (await reader.ReadAsync(ct))
        {
            var position = reader.GetInt64(0);
            var eventType = reader.GetString(1);
            var eventData = reader.GetString(2);
            var tags = (string[])reader.GetValue(3);
            var timestamp = reader.GetDateTime(4);
            
            var @event = _serializer.Deserialize(eventType, eventData, tags, timestamp);
            yield return new DcbEventEnvelope(position, @event);
        }
    }
    
    public async Task<long> AppendAsync(
        IDcbEvent @event,
        AppendCondition? condition = null,
        CancellationToken ct = default)
    {
        var eventData = _serializer.Serialize(@event);
        
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        
        if (condition is null)
        {
            // Simple append without condition
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO dcb_events (event_type, event_data, tags) VALUES (@type, @data::jsonb, @tags) RETURNING sequence_position",
                conn
            );
            cmd.Parameters.AddWithValue("type", @event.Type);
            cmd.Parameters.AddWithValue("data", eventData);
            cmd.Parameters.AddWithValue("tags", @event.Tags.ToArray());
            
            return (long)(await cmd.ExecuteScalarAsync(ct))!;
        }
        
        // Conditional append using stored function
        await using var cmd2 = new NpgsqlCommand(
            "SELECT dcb_append_if_no_conflict(@type, @data::jsonb, @tags, @fail_types, @fail_tags, @after)",
            conn
        );
        cmd2.Parameters.AddWithValue("type", @event.Type);
        cmd2.Parameters.AddWithValue("data", eventData);
        cmd2.Parameters.AddWithValue("tags", @event.Tags.ToArray());
        cmd2.Parameters.AddWithValue("fail_types", GetTypesFromQuery(condition.FailIfEventsMatch));
        cmd2.Parameters.AddWithValue("fail_tags", GetTagsFromQuery(condition.FailIfEventsMatch));
        cmd2.Parameters.AddWithValue("after", condition.After);
        
        try
        {
            return (long)(await cmd2.ExecuteScalarAsync(ct))!;
        }
        catch (PostgresException ex) when (ex.Message.Contains("Concurrency conflict"))
        {
            throw new ConcurrencyConflictException(condition.After, -1);
        }
    }
    
    private string BuildReadSql(DcbQuery query)
    {
        var conditions = new List<string>();
        
        for (int i = 0; i < query.Items.Length; i++)
        {
            var item = query.Items[i];
            var itemConditions = new List<string>();
            
            if (item.Types is not null)
                itemConditions.Add($"event_type = ANY(@types_{i})");
            if (item.Tags is not null)
                itemConditions.Add($"tags @> @tags_{i}");
            
            if (itemConditions.Count > 0)
                conditions.Add($"({string.Join(" AND ", itemConditions)})");
        }
        
        var whereClause = conditions.Count > 0
            ? $"AND ({string.Join(" OR ", conditions)})"
            : "";
        
        return $@"
            SELECT sequence_position, event_type, event_data::text, tags, timestamp
            FROM dcb_events
            WHERE sequence_position > @after_position
            {whereClause}
            ORDER BY sequence_position";
    }
    
    private void AddQueryParameters(NpgsqlCommand cmd, DcbQuery query, long afterPosition)
    {
        cmd.Parameters.AddWithValue("after_position", afterPosition);
        
        for (int i = 0; i < query.Items.Length; i++)
        {
            var item = query.Items[i];
            if (item.Types is not null)
                cmd.Parameters.AddWithValue($"types_{i}", item.Types.ToArray());
            if (item.Tags is not null)
                cmd.Parameters.AddWithValue($"tags_{i}", item.Tags.ToArray());
        }
    }
    
    private string[]? GetTypesFromQuery(DcbQuery query)
    {
        var types = query.Items
            .Where(i => i.Types is not null)
            .SelectMany(i => i.Types!)
            .Distinct()
            .ToArray();
        return types.Length > 0 ? types : null;
    }
    
    private string[]? GetTagsFromQuery(DcbQuery query)
    {
        // For conditional append, we need intersection of all tag requirements
        var allTags = query.Items
            .Where(i => i.Tags is not null)
            .SelectMany(i => i.Tags!)
            .Distinct()
            .ToArray();
        return allTags.Length > 0 ? allTags : null;
    }
}
```

---

## KurrentDB Adapter

KurrentDB (formerly EventStoreDB) uses streams with metadata. DCB adapter stores all events in a single stream with tags in metadata.

### Event Metadata Schema

```csharp
public record DcbEventMetadata(
    string[] Tags,
    DateTimeOffset Timestamp
);
```

### Adapter Implementation

```csharp
public class KurrentDbDcbEventStore : IDcbEventStore
{
    private readonly EventStoreClient _client;
    private readonly IEventSerializer _serializer;
    private readonly string _streamName;
    
    public KurrentDbDcbEventStore(
        EventStoreClient client,
        IEventSerializer serializer,
        string boundedContextName = "default")
    {
        _client = client;
        _serializer = serializer;
        _streamName = $"dcb-{boundedContextName}";  // Single stream per bounded context
    }
    
    public async IAsyncEnumerable<DcbEventEnvelope> ReadAsync(
        DcbQuery query,
        long afterPosition = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var startPosition = afterPosition > 0 
            ? StreamPosition.FromInt64(afterPosition) 
            : StreamPosition.Start;
        
        var events = _client.ReadStreamAsync(
            Direction.Forwards,
            _streamName,
            startPosition,
            cancellationToken: ct
        );
        
        await foreach (var resolvedEvent in events)
        {
            var position = resolvedEvent.Event.EventNumber.ToInt64();
            var metadata = JsonSerializer.Deserialize<DcbEventMetadata>(
                resolvedEvent.Event.Metadata.Span
            )!;
            
            var dcbEvent = _serializer.Deserialize(
                resolvedEvent.Event.EventType,
                Encoding.UTF8.GetString(resolvedEvent.Event.Data.Span),
                metadata.Tags.ToHashSet(),
                metadata.Timestamp
            );
            
            // Filter by query (KurrentDB doesn't support tag queries natively)
            if (query.Matches(dcbEvent))
            {
                yield return new DcbEventEnvelope(position, dcbEvent);
            }
        }
    }
    
    public async Task<long> AppendAsync(
        IDcbEvent @event,
        AppendCondition? condition = null,
        CancellationToken ct = default)
    {
        var eventData = _serializer.Serialize(@event);
        var metadata = new DcbEventMetadata(@event.Tags.ToArray(), @event.Timestamp);
        
        var esEvent = new EventData(
            Uuid.NewUuid(),
            @event.Type,
            Encoding.UTF8.GetBytes(eventData),
            JsonSerializer.SerializeToUtf8Bytes(metadata)
        );
        
        if (condition is null)
        {
            var result = await _client.AppendToStreamAsync(
                _streamName,
                StreamState.Any,
                [esEvent],
                cancellationToken: ct
            );
            return (long)result.NextExpectedStreamRevision;
        }
        
        // With condition: use optimistic concurrency
        // First verify no conflicting events exist
        await VerifyNoConflictsAsync(condition, ct);
        
        try
        {
            var result = await _client.AppendToStreamAsync(
                _streamName,
                StreamRevision.FromInt64(condition.After),
                [esEvent],
                cancellationToken: ct
            );
            return (long)result.NextExpectedStreamRevision;
        }
        catch (WrongExpectedVersionException)
        {
            throw new ConcurrencyConflictException(condition.After, -1);
        }
    }
    
    private async Task VerifyNoConflictsAsync(AppendCondition condition, CancellationToken ct)
    {
        // Read events after the expected position
        var events = _client.ReadStreamAsync(
            Direction.Forwards,
            _streamName,
            StreamPosition.FromInt64(condition.After + 1),
            cancellationToken: ct
        );
        
        await foreach (var resolvedEvent in events)
        {
            var metadata = JsonSerializer.Deserialize<DcbEventMetadata>(
                resolvedEvent.Event.Metadata.Span
            )!;
            
            // Check if this event matches the conflict query
            var dcbEvent = _serializer.Deserialize(
                resolvedEvent.Event.EventType,
                Encoding.UTF8.GetString(resolvedEvent.Event.Data.Span),
                metadata.Tags.ToHashSet(),
                metadata.Timestamp
            );
            
            if (condition.FailIfEventsMatch.Matches(dcbEvent))
            {
                throw new ConcurrencyConflictException(
                    condition.After,
                    resolvedEvent.Event.EventNumber.ToInt64()
                );
            }
        }
    }
}
```

### Performance Note

KurrentDB adapter performs client-side filtering. For high-volume scenarios, consider:
- Category projections for type-based filtering
- Custom projections for specific tag combinations
- Caching frequently-read projections

---

## Marten Adapter

Marten with PostgreSQL using its event sourcing capabilities plus custom DCB extensions.

### Schema Extensions

```csharp
public static class MartenDcbExtensions
{
    public static void AddDcbSupport(this StoreOptions options)
    {
        // Register custom event metadata
        options.Events.MetadataConfig.HeadersEnabled = true;
        
        // Add GIN index on metadata for tag queries
        options.Schema.For<IEvent>().Index(x => x.Headers, idx =>
        {
            idx.Method = IndexMethod.gin;
            idx.Name = "idx_events_dcb_tags";
        });
    }
}
```

### Adapter Implementation

```csharp
public class MartenDcbEventStore : IDcbEventStore
{
    private readonly IDocumentStore _store;
    private readonly string _streamId;
    
    public MartenDcbEventStore(IDocumentStore store, string boundedContextName = "dcb-default")
    {
        _store = store;
        _streamId = boundedContextName;
    }
    
    public async IAsyncEnumerable<DcbEventEnvelope> ReadAsync(
        DcbQuery query,
        long afterPosition = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var session = _store.QuerySession();
        
        // Use Marten's event query with custom filtering
        var events = await session.Events
            .QueryAllRawEvents()
            .Where(e => e.Sequence > afterPosition)
            .OrderBy(e => e.Sequence)
            .ToListAsync(ct);
        
        foreach (var @event in events)
        {
            var tags = GetTagsFromHeaders(@event.Headers);
            
            // Create DCB event wrapper
            var dcbEvent = new DcbEventWrapper(
                @event.EventTypeName,
                @event.Data,
                tags,
                @event.Timestamp
            );
            
            if (query.Matches(dcbEvent))
            {
                yield return new DcbEventEnvelope(@event.Sequence, dcbEvent);
            }
        }
    }
    
    public async Task<long> AppendAsync(
        IDcbEvent @event,
        AppendCondition? condition = null,
        CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        
        // Add tags to event headers
        var headers = new Dictionary<string, object>
        {
            ["dcb_tags"] = @event.Tags.ToArray()
        };
        
        if (condition is not null)
        {
            // Verify no conflicts first
            var conflictingEvents = await session.Events
                .QueryAllRawEvents()
                .Where(e => e.Sequence > condition.After)
                .ToListAsync(ct);
            
            foreach (var existing in conflictingEvents)
            {
                var existingTags = GetTagsFromHeaders(existing.Headers);
                var wrapper = new DcbEventWrapper(
                    existing.EventTypeName,
                    existing.Data,
                    existingTags,
                    existing.Timestamp
                );
                
                if (condition.FailIfEventsMatch.Matches(wrapper))
                {
                    throw new ConcurrencyConflictException(condition.After, existing.Sequence);
                }
            }
        }
        
        // Append to single DCB stream
        var action = session.Events.Append(
            _streamId,
            @event
        );
        
        // Set headers on the pending event
        action.SetHeader("dcb_tags", @event.Tags.ToArray());
        
        await session.SaveChangesAsync(ct);
        
        return action.Events.Last().Sequence;
    }
    
    private IReadOnlySet<string> GetTagsFromHeaders(IReadOnlyDictionary<string, object>? headers)
    {
        if (headers?.TryGetValue("dcb_tags", out var tagsObj) == true && tagsObj is string[] tags)
        {
            return tags.ToHashSet();
        }
        return new HashSet<string>();
    }
}

// Wrapper to implement IDcbEvent for queried events
internal record DcbEventWrapper(
    string Type,
    object Data,
    IReadOnlySet<string> Tags,
    DateTimeOffset Timestamp
) : IDcbEvent;
```

---

## Adapter Selection Guide

| Factor                      | PostgreSQL    | KurrentDB       | Marten       |
|-----------------------------|---------------|-----------------|--------------|
| **Native tag queries**      | ✅ GIN index   | ❌ Client filter | ⚠️ Headers   |
| **Throughput**              | ~1,000 evt/s  | ~10,000 evt/s   | ~2,000 evt/s |
| **Existing infra**          | Common        | Event sourcing  | .NET shops   |
| **Operational complexity**  | Low           | Medium          | Low          |
| **True conditional append** | ✅ Stored proc | ⚠️ Two-phase    | ⚠️ Two-phase |

### Recommendations

**Choose PostgreSQL adapter when:**
- Starting fresh with DCB
- Need native tag query performance
- Already have PostgreSQL infrastructure

**Choose KurrentDB adapter when:**
- Migrating existing EventStoreDB system
- High throughput requirements (accept client-side filtering)
- Need proven event store reliability

**Choose Marten adapter when:**
- Existing Marten/PostgreSQL investment
- Want Marten's projection and query features
- Gradual migration from document storage

### Performance Optimization Tips

1. **Index strategy**: For PostgreSQL, ensure GIN index on tags array
2. **Query batching**: Read events in batches rather than streaming for large datasets
3. **Projection caching**: Cache frequently-read decision model state
4. **Connection pooling**: Use connection pools for high-concurrency scenarios
5. **Partition by bounded context**: Use separate streams/tables per bounded context
