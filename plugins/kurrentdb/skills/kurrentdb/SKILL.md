---
name: kurrentdb
description: |
  Master KurrentDB (formerly EventStore) event-native database development with expert-level guidance.
  Use when writing event sourcing code, working with KurrentDB streams, projections, subscriptions,
  connectors, indexes, or the .NET KurrentDB.Client API. Covers appending and reading events,
  optimistic concurrency, catch-up and persistent subscriptions, server-side JavaScript projections,
  secondary and user-defined indexes, connectors (Kafka, RabbitMQ, HTTP, SQL, MongoDB, Elasticsearch,
  Pulsar), stream operations, multi-stream atomic appends, the HTTP/gRPC API, server configuration,
  security, operations, and event-driven architecture patterns including outbox, time-travel, and
  polyglot persistence. Use when someone mentions KurrentDB, EventStore, EventStoreDB, event sourcing
  database, event store, event streams, or event-native database.
---

# KurrentDB Expert Skill

## Overview

KurrentDB is an **event-native database** purpose-built for Event Sourcing, Event-Driven Architecture, and real-time event streaming. Formerly known as EventStoreDB (rebranded in v25.0), it stores every state change as an immutable event in an append-only log, providing a complete audit trail and enabling temporal queries, event replay, and real-time subscriptions.

**Current LTS**: v26.0 (Jan 2026, .NET 10) | **Previous LTS**: v24.10 (Nov 2024)

**Key Capabilities**: Immutable event log, individually indexed streams (billions supported), server-side projections (JavaScript), persistent subscriptions with competing consumers, built-in connectors, secondary and user-defined indexes, ad-hoc SQL queries, archiving to S3/Azure/GCP, multi-stream atomic appends, encryption-at-rest.

**Client SDKs**: .NET, Java, Go, Python, Node.js, Rust | **APIs**: gRPC (primary), HTTP/AtomPub (legacy)

## .NET Client Quick Start

```bash
dotnet add package KurrentDB.Client --version "1.1.*"
```

```csharp
// Create singleton client
var client = new KurrentDBClient(
    KurrentDBClientSettings.Create("kurrentdb://admin:changeit@localhost:2113?tls=false")
);

// Create and append an event
var evt = new EventData(
    Uuid.NewUuid(),
    "OrderPlaced",
    JsonSerializer.SerializeToUtf8Bytes(new { OrderId = "order-123", Amount = 99.99 })
);
await client.AppendToStreamAsync("order-123", StreamState.NoStream, [evt]);

// Read events
var events = client.ReadStreamAsync(Direction.Forwards, "order-123", StreamPosition.Start);
await foreach (var e in events)
    Console.WriteLine(Encoding.UTF8.GetString(e.OriginalEvent.Data.ToArray()));

// Subscribe to stream (catch-up)
await using var sub = client.SubscribeToStream("order-123", FromStream.Start);
await foreach (var msg in sub.Messages) {
    if (msg is StreamMessage.Event(var evnt))
        Console.WriteLine($"{evnt.OriginalEventNumber}@{evnt.OriginalStreamId}");
}
```

### Three Client Classes

| Class                                    | Purpose                                  |
|------------------------------------------|------------------------------------------|
| `KurrentDBClient`                        | Streams: append, read, subscribe, delete |
| `KurrentDBPersistentSubscriptionsClient` | Persistent subscription management       |
| `KurrentDBProjectionManagementClient`    | Projection CRUD and state retrieval      |

### Connection Strings

| Schema                  | Use Case                                                            |
|-------------------------|---------------------------------------------------------------------|
| `kurrentdb://`          | Direct single-node connection                                       |
| `kurrentdb+discover://` | Cluster discovery via gossip (works with any topology since v22.10) |

Key parameters: `tls` (true), `nodePreference` (leader/follower/random/readOnlyReplica), `tlsVerifyCert`, `tlsCaFile`, `defaultDeadline`, `keepAliveInterval`, `userCertFile`/`userKeyFile` (X.509).

See [references/dotnet-client.md](references/dotnet-client.md) for complete API reference.

## Stream Naming Conventions

| Pattern          | Example             | Purpose                         |
|------------------|---------------------|---------------------------------|
| `{entity}-{id}`  | `order-bc2f3a4e`    | Per-entity stream (recommended) |
| `$ce-{category}` | `$ce-order`         | Category projection stream      |
| `$et-{type}`     | `$et-OrderPlaced`   | Event type projection stream    |
| `$$streamname`   | `$$order-123`       | Stream metadata                 |
| `$` prefix       | `$all`, `$settings` | System streams (admin access)   |

**Rules**: Stream IDs are case-sensitive strings. Use `-` separator for categories. System streams (`$` prefix) require `$admins` access by default.

See [references/streams-and-events.md](references/streams-and-events.md) for complete stream documentation.

## Core Operations Cheat Sheet

### Optimistic Concurrency
```csharp
// Strict: fails if stream changed since last read
await client.AppendToStreamAsync("order-123", expectedRevision, [evt]);

// Relaxed: no concurrency check
await client.AppendToStreamAsync("order-123", StreamState.Any, [evt]);

// First write only: fails if stream exists
await client.AppendToStreamAsync("order-123", StreamState.NoStream, [evt]);
```

### Multi-Stream Atomic Append (v25.1+)
```csharp
await client.MultiStreamAppendAsync([
    new AppendStreamRequest("order-stream", StreamState.Any, [orderEvt]),
    new AppendStreamRequest("inventory-stream", StreamState.Any, [inventoryEvt])
]);
```

### Stream Deletion
```csharp
await client.DeleteAsync("order-123", StreamState.Any);      // Soft: can reopen
await client.TombstoneAsync("order-123", StreamState.Any);   // Hard: permanent, name unusable
```

### Stream Metadata
```json
{ "$maxAge": 86400, "$maxCount": 1000, "$cacheControl": 60,
  "$acl": { "$r": ["$admins", "reader-role"], "$w": "$admins" } }
```

## Subscriptions Overview

| Type           | Checkpoint     | Consumers            | Ordering       | Use Case                    |
|----------------|----------------|----------------------|----------------|-----------------------------|
| **Catch-up**   | Client-managed | Single               | Guaranteed     | Event handlers, projections |
| **Persistent** | Server-managed | Multiple (competing) | Not guaranteed | Scalable processors         |

### Catch-Up Subscription
```csharp
await using var sub = client.SubscribeToAll(FromAll.Start,
    filterOptions: new(EventTypeFilter.ExcludeSystemEvents()));

await foreach (var msg in sub.Messages) {
    switch (msg) {
        case StreamMessage.Event(var e): /* process */ break;
        case StreamMessage.AllStreamCheckpointReached(var p): /* save checkpoint */ break;
    }
}
```

### Persistent Subscription
```csharp
await using var psClient = new KurrentDBPersistentSubscriptionsClient(settings);
await psClient.CreateToStreamAsync("order-123", "processor-group", new PersistentSubscriptionSettings());
await using var sub = psClient.SubscribeToStream("order-123", "processor-group");
await foreach (var msg in sub.Messages) {
    if (msg is PersistentSubscriptionMessage.Event(var e, _)) {
        await ProcessEvent(e);
        await sub.Ack(e);  // or sub.Nack(PersistentSubscriptionNakEventAction.Park, "reason", e)
    }
}
```

**Consumer strategies**: RoundRobin (default), DispatchToSingle, Pinned, PinnedByCorrelation.

See [references/subscriptions.md](references/subscriptions.md) for full details.

## Projections Overview

Server-side projections process events using JavaScript. **Events must be JSON-serialized.**

```javascript
// Category aggregation
fromCategory("order")
  .foreachStream()
  .when({
    $init: () => ({ total: 0, items: [] }),
    OrderPlaced: (s, e) => { s.total += e.body.amount; s.items.push(e.body.itemId); },
    OrderCancelled: (s, e) => { s.total = 0; s.items = []; }
  })
  .outputState();

// Cross-stream correlation with emit
fromAll()
  .when({
    OrderPlaced: (s, e) => emit("invoice-" + e.body.customerId, "InvoiceRequested", e.body),
    PaymentReceived: (s, e) => linkTo("payments-all", e)
  });
```

**Selectors**: `fromAll()`, `fromStream(id)`, `fromStreams([...])`, `fromCategory(name)`
**Handlers**: `$init`, `$any`, `$deleted`, specific event types
**Actions**: `emit(stream, type, body, metadata)`, `linkTo(stream, event, metadata)`, `log(msg)`
**Partitioning**: `foreachStream()`, `partitionBy(fn)`, `transformBy(fn)`, `filterBy(fn)`

**System projections** (5 built-in): `$by_category`, `$by_event_type`, `$by_correlation_id`, `$stream_by_category`, `$streams`

**WARNING**: Projections cause write amplification. Enabling all system projections = 4x write operations per event. Only run on leader node.

See [references/projections.md](references/projections.md) for complete JavaScript API.

## Indexes

| Type             | Version | Description                                                                                             |
|------------------|---------|---------------------------------------------------------------------------------------------------------|
| **Default**      | All     | Stream name hash + event number -> log position. ~24 bytes/event                                        |
| **Secondary**    | v25.1+  | Category + event type indexes in DuckDB. Up to 50% less storage, 10x faster reads vs system projections |
| **User-Defined** | v26.0+  | Custom indexes from record content via JavaScript filter/field selectors                                |

```csharp
// Read from secondary index (instead of system projection + resolveLinkTos)
var events = client.ReadAllAsync(Direction.Forwards, Position.Start,
    StreamFilter.Prefix("$idx-et-OrderPlaced"));

// User-defined index: read by field value
var events = client.ReadAllAsync(Direction.Forwards, Position.Start,
    StreamFilter.Prefix("$idx-user-orders-by-country:Mauritius"));
```

See [references/indexes.md](references/indexes.md) for configuration and SQL query support.

## Connectors Overview

Built-in server-side plugins that stream events to external systems. At-least-once delivery, in-order.

| Sink          | License  | Key Feature                                       |
|---------------|----------|---------------------------------------------------|
| HTTP          | Free     | Push to any HTTP endpoint                         |
| Kafka         | Required | Idempotent producer, compression                  |
| RabbitMQ      | Required | Exchange/routing key routing                      |
| SQL           | Required | SQL Server + PostgreSQL, custom statement mapping |
| MongoDB       | Required | BSON documents, batching                          |
| Elasticsearch | Required | JSON indexing, batching                           |
| Pulsar        | Required | JWT auth, partition key routing                   |
| Serilog       | Free     | Console/File/Seq logging                          |

| Source | License  | Key Feature                           |
|--------|----------|---------------------------------------|
| Kafka  | Required | Consume topics into KurrentDB streams |

**Management via REST**: `POST /connectors/{id}`, `POST /connectors/{id}/start`, `POST /connectors/{id}/stop`, `DELETE /connectors/{id}`

See [references/connectors.md](references/connectors.md) for configuration and all sink/source details.

## What's New

### v26.0 LTS (Jan 2026)
- **User-defined indexes**: Custom secondary indexes from record content (JavaScript)
- **Archiving**: GCP and Azure support (previously S3 only)
- **Kafka source connector**: Consume Kafka topics into KurrentDB streams
- **SQL sink connector**: Write events to SQL Server/PostgreSQL
- **Improved request processing**: Single virtual queue for reads, WorkerThreads deprecated
- **.NET 10** runtime

### v25.1 (Oct 2025)
- **Secondary indexes**: Category + event type indexes (DuckDB-backed, replace system projections)
- **Multi-stream appends**: Atomic writes across multiple streams
- **Log record properties**: Structured key-value metadata without serialization overhead
- **Ad-hoc SQL queries**: DuckDB-powered query UI (License Required)
- **Database stats page** in Web UI (License Required)

### v25.0 (Mar 2025)
- **EventStore rebranded to KurrentDB** (EventStoreDB -> KurrentDB)
- **Archiving**: Upload chunks to S3, transparent reads from archive
- **Connectors**: Elasticsearch sink, data protection (envelope encryption)
- **New Web UI**: Dashboard, stats, logs, configuration viewer

See [references/migration-guide.md](references/migration-guide.md) for EventStore → KurrentDB migration.

## Key Architecture Decisions

- **Projections only run on leader** - plan for IO/CPU imbalance
- **Persistent subscriptions only run on leader** - same consideration
- **Prefer catch-up subscriptions** when ordering matters
- **Prefer secondary indexes over system projections** (v25.1+) for massive storage and read performance gains
- **Events must be JSON** for server-side projections to work
- **Client is a singleton** – create once, reuse across application lifetime
- **Idempotent writes** require specifying expected stream revision (not `StreamState.Any`)

## Reference Files

| File                                                              | Coverage                                                                |
|-------------------------------------------------------------------|-------------------------------------------------------------------------|
| [dotnet-client.md](references/dotnet-client.md)                   | Complete .NET KurrentDB.Client API reference                            |
| [streams-and-events.md](references/streams-and-events.md)         | Events, streams, metadata, naming, system streams                       |
| [projections.md](references/projections.md)                       | System projections, custom JavaScript API, configuration                |
| [subscriptions.md](references/subscriptions.md)                   | Catch-up subscriptions, persistent subscriptions, filtering             |
| [connectors.md](references/connectors.md)                         | All sink/source connectors, configuration, data protection              |
| [indexes.md](references/indexes.md)                               | Default, secondary, user-defined indexes, SQL queries                   |
| [queries-and-web-ui.md](references/queries-and-web-ui.md)         | Embedded Web UI, SQL query engine, virtual tables, JSON access          |
| [server-configuration.md](references/server-configuration.md)     | Config precedence, networking, clustering, database settings            |
| [security.md](references/security.md)                             | Authentication, authorization, TLS, encryption-at-rest, stream policies |
| [operations.md](references/operations.md)                         | Backup/restore, scavenge, archiving, redaction                          |
| [http-api.md](references/http-api.md)                             | REST API endpoints, headers, content types                              |
| [patterns-and-use-cases.md](references/patterns-and-use-cases.md) | Outbox, time-travel, polyglot persistence patterns                      |
| [migration-guide.md](references/migration-guide.md)               | EventStore to KurrentDB migration reference                             |
| [diagnostics.md](references/diagnostics.md)                       | Metrics, logging, OpenTelemetry, monitoring                             |
