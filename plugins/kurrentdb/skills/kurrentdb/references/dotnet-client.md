# KurrentDB .NET Client API Reference

## Table of Contents

- [Connection and Configuration](#connection-and-configuration)
- [Client Classes](#client-classes)
- [EventData](#eventdata)
- [Appending Events](#appending-events)
- [Reading Events](#reading-events)
- [Catch-Up Subscriptions](#catch-up-subscriptions)
- [Persistent Subscriptions](#persistent-subscriptions)
- [Projection Management](#projection-management)
- [Stream Deletion](#stream-deletion)
- [Authentication](#authentication)
- [Observability](#observability)
- [Gotchas and Common Mistakes](#gotchas-and-common-mistakes)

---

## Connection and Configuration

### Package

```
KurrentDB.Client v1.1.*
```

### Connection String Schemas

| Schema                    | Use Case                          |
|---------------------------|-----------------------------------|
| `kurrentdb://`            | Single node direct connection     |
| `kurrentdb+discover://`   | Cluster discovery via gossip      |

### Connection String Parameters

| Parameter             | Default  | Description                                               |
|-----------------------|----------|-----------------------------------------------------------|
| `tls`                 | `true`   | Enable TLS encryption                                     |
| `connectionName`      | (auto)   | Named identifier for the connection                       |
| `maxDiscoverAttempts` | `10`     | Max gossip discovery retries                              |
| `discoveryInterval`   | `100ms`  | Delay between discovery attempts                          |
| `gossipTimeout`       | `5s`     | Timeout for gossip requests                               |
| `nodePreference`      | `leader` | Node selection: leader, follower, random, readOnlyReplica |
| `tlsVerifyCert`       | `true`   | Verify server TLS certificate                             |
| `tlsCaFile`           | (none)   | Path to CA certificate file                               |
| `defaultDeadline`     | (none)   | Default gRPC deadline for operations                      |
| `keepAliveInterval`   | `10s`    | gRPC keep-alive ping interval                             |
| `keepAliveTimeout`    | `10s`    | gRPC keep-alive ping timeout                              |
| `userCertFile`        | (none)   | Path to X.509 user certificate (v25.0+)                   |
| `userKeyFile`         | (none)   | Path to X.509 user private key (v25.0+)                   |

### Creating a Client

The client is a **singleton** -- create once and reuse for the lifetime of the application. There is no open/close lifecycle.

```csharp
var settings = KurrentDBClientSettings.Create("kurrentdb://localhost:2113?tls=false");
var client = new KurrentDBClient(settings);

// Cluster with discovery
var clusterSettings = KurrentDBClientSettings.Create(
    "kurrentdb+discover://node1:2113,node2:2113,node3:2113?tls=true&tlsVerifyCert=false"
);
var clusterClient = new KurrentDBClient(clusterSettings);
```

---

## Client Classes

KurrentDB provides three separate client classes, each targeting a different area of functionality.

| Class                                      | Purpose                                    |
|--------------------------------------------|--------------------------------------------|
| `KurrentDBClient`                          | Streams: append, read, subscribe, delete   |
| `KurrentDBPersistentSubscriptionsClient`   | Persistent subscription management         |
| `KurrentDBProjectionManagementClient`      | Projection management                      |

All three accept `KurrentDBClientSettings` in their constructor.

```csharp
var settings = KurrentDBClientSettings.Create("kurrentdb://localhost:2113?tls=false");

var client = new KurrentDBClient(settings);
var persistentSubClient = new KurrentDBPersistentSubscriptionsClient(settings);
var projectionClient = new KurrentDBProjectionManagementClient(settings);
```

---

## EventData

`EventData` represents a single event to write to a stream.

```csharp
var eventData = new EventData(
    Uuid.NewUuid(),                                      // eventId - for idempotent writes
    "OrderPlaced",                                       // type - event type name
    JsonSerializer.SerializeToUtf8Bytes(orderPlaced),    // data - byte[] payload
    JsonSerializer.SerializeToUtf8Bytes(metadata)        // metadata - optional byte[]
);
```

| Parameter  | Type     | Description                                                        |
|------------|----------|--------------------------------------------------------------------|
| `eventId`  | `Uuid`   | Unique ID for idempotent writes. Use `Uuid.NewUuid()`.             |
| `type`     | `string` | Event type name (e.g., `"OrderPlaced"`).                           |
| `data`     | `byte[]` | Serialized event payload.                                          |
| `metadata` | `byte[]` | Optional. Must be valid JSON with string keys/values if provided.  |

---

## Appending Events

### Basic Append

```csharp
var eventData = new EventData(
    Uuid.NewUuid(),
    "OrderPlaced",
    JsonSerializer.SerializeToUtf8Bytes(new { OrderId = "123", Amount = 99.99 })
);

var result = await client.AppendToStreamAsync(
    "order-123",
    StreamState.Any,
    new[] { eventData }
);
```

### StreamState Options

| Value                      | Behavior                               |
|----------------------------|----------------------------------------|
| `StreamState.Any`          | No concurrency check -- always appends |
| `StreamState.NoStream`     | Fails if stream already exists         |
| `StreamState.StreamExists` | Fails if stream does not exist         |

### Optimistic Concurrency

Pass the `ulong` revision from the last read to detect conflicts. Throws `WrongExpectedVersionException` on mismatch.

```csharp
var result = await client.ReadStreamAsync(
    Direction.Forwards, "order-123", StreamPosition.Start
);

var events = await result.ToListAsync();
var lastRevision = events.Last().Event.EventNumber;

try
{
    await client.AppendToStreamAsync(
        "order-123",
        lastRevision,       // expected revision
        new[] { eventData }
    );
}
catch (WrongExpectedVersionException ex)
{
    // Another writer modified the stream -- handle conflict
}
```

### User Credentials Override

```csharp
await client.AppendToStreamAsync(
    "order-123",
    StreamState.Any,
    new[] { eventData },
    userCredentials: new UserCredentials("admin", "changeit")
);
```

### Multi-Stream Atomic Append (v25.1+)

All appends succeed or all fail atomically across multiple streams.

```csharp
var result = await client.MultiStreamAppendAsync(
    new AppendStreamRequest("order-123", StreamState.Any, new[] { orderEvent }),
    new AppendStreamRequest("inventory-abc", StreamState.Any, new[] { inventoryEvent })
);
```

---

## Reading Events

### Reading from a Stream

```csharp
var result = client.ReadStreamAsync(
    Direction.Forwards,
    "order-123",
    StreamPosition.Start,
    maxCount: 100,
    resolveLinkTos: false
);

if (await result.ReadState == ReadState.StreamNotFound)
{
    Console.WriteLine("Stream not found");
    return;
}

await foreach (var e in result)
{
    Console.WriteLine($"{e.Event.EventType} @ {e.Event.EventNumber}");
}
```

### Reading from $all

Requires **admin** credentials.

```csharp
var result = client.ReadAllAsync(
    Direction.Forwards,
    Position.Start,
    maxCount: 100,
    resolveLinkTos: false,
    userCredentials: new UserCredentials("admin", "changeit")
);

await foreach (var e in result)
{
    // Filter out system events
    if (e.Event.EventType.StartsWith("$")) continue;

    Console.WriteLine($"{e.Event.EventStreamId}: {e.Event.EventType}");
}
```

### Direction

| Value                  | Description             |
|------------------------|-------------------------|
| `Direction.Forwards`   | Oldest to newest        |
| `Direction.Backwards`  | Newest to oldest        |

### Position Types

| Type             | Values                                      | Used With    |
|------------------|---------------------------------------------|--------------|
| `StreamPosition` | `Start`, `End`, or a `ulong` value          | Stream reads |
| `Position`       | `Start`, `End` (commit + prepare positions) | `$all` reads |

### Read Result Properties

| Property               | Description                        |
|------------------------|------------------------------------|
| `ReadState`            | `Ok` or `StreamNotFound`           |
| `FirstStreamPosition`  | First event position in result     |
| `LastStreamPosition`   | Last event position in result      |

---

## Catch-Up Subscriptions

Catch-up subscriptions track position client-side, deliver to a single consumer, and guarantee ordering.

### Subscribe to a Stream

```csharp
var subscription = client.SubscribeToStream(
    "order-123",
    FromStream.Start,         // or FromStream.End, FromStream.After(position)
    resolveLinkTos: false
);

await foreach (var message in subscription.Messages)
{
    switch (message)
    {
        case StreamMessage.Event(var resolvedEvent):
            Console.WriteLine($"Received: {resolvedEvent.Event.EventType}");
            // Save resolvedEvent.Event.EventNumber as checkpoint
            break;

        case StreamMessage.CaughtUp:
            Console.WriteLine("Caught up to live");
            break;
    }
}
```

### Subscribe to $all

```csharp
var subscription = client.SubscribeToAll(
    FromAll.Start,            // or FromAll.End, FromAll.After(position)
    resolveLinkTos: false
);

await foreach (var message in subscription.Messages)
{
    switch (message)
    {
        case StreamMessage.Event(var resolvedEvent):
            Console.WriteLine($"Received: {resolvedEvent.Event.EventType}");
            // Save resolvedEvent.OriginalPosition as checkpoint
            break;

        case StreamMessage.AllStreamCheckpointReached(var position):
            // Save position as checkpoint for filtered subscriptions
            break;
    }
}
```

### FromStream / FromAll

| Type         | Values                                                                 |
|--------------|------------------------------------------------------------------------|
| `FromStream` | `Start`, `End`, `After(StreamPosition)` -- position is **exclusive**   |
| `FromAll`    | `Start`, `End`, `After(Position)` -- position is **exclusive**         |

Positions are **exclusive**: the subscription delivers the **next** event after the specified position.

### Message Types

| Message                                           | Description                                    |
|---------------------------------------------------|------------------------------------------------|
| `StreamMessage.Event(var resolvedEvent)`          | An event was received                          |
| `StreamMessage.CaughtUp`                          | Transitioned from historical to live (v23.10+) |
| `StreamMessage.AllStreamCheckpointReached(var p)` | Checkpoint for filtered $all subscriptions     |

### Server-Side Filtering ($all Only)

| Filter                                        | Description                         |
|-----------------------------------------------|-------------------------------------|
| `StreamFilter.Prefix("order-")`               | Streams starting with prefix        |
| `StreamFilter.RegularExpression("^order-.*")` | Streams matching regex              |
| `EventTypeFilter.Prefix("Order")`             | Event types starting with prefix    |
| `EventTypeFilter.RegularExpression("^Order")` | Event types matching regex          |
| `EventTypeFilter.ExcludeSystemEvents()`       | Exclude events starting with `$`    |

```csharp
var filterOptions = new SubscriptionFilterOptions(
    EventTypeFilter.ExcludeSystemEvents(),
    checkpointInterval: 32   // checkpoint every 32 * 32 = 1024 events
);

var subscription = client.SubscribeToAll(
    FromAll.Start,
    filterOptions: filterOptions
);
```

The `checkpointInterval` value is multiplied by 32 internally to determine how many events pass between checkpoint messages.

---

## Persistent Subscriptions

Persistent subscriptions are server-managed with competing consumers. Requires a separate client class.

### Creating a Persistent Subscription

```csharp
var persistentSubClient = new KurrentDBPersistentSubscriptionsClient(settings);

// To a stream
await persistentSubClient.CreateToStreamAsync(
    "order-123",
    "order-processing-group",
    new PersistentSubscriptionSettings(
        resolveLinkTos: true,
        startFrom: StreamPosition.Start
    )
);

// To $all (with filter)
await persistentSubClient.CreateToAllAsync(
    "all-orders-group",
    EventTypeFilter.Prefix("Order"),
    new PersistentSubscriptionSettings(
        startFrom: Position.Start
    )
);
```

### Subscribing

```csharp
var subscription = persistentSubClient.SubscribeToStream(
    "order-123",
    "order-processing-group"
);

await foreach (var message in subscription.Messages)
{
    switch (message)
    {
        case PersistentSubscriptionMessage.Event(var resolvedEvent, var retryCount):
            try
            {
                // Process event
                await subscription.Ack(resolvedEvent);
            }
            catch (Exception ex)
            {
                await subscription.Nack(
                    PersistentSubscriptionNakEventAction.Retry,
                    ex.Message,
                    resolvedEvent
                );
            }
            break;

        case PersistentSubscriptionMessage.SubscriptionConfirmation:
            Console.WriteLine("Subscription confirmed");
            break;
    }
}
```

### Nack Actions

| Action     | Behavior                                           |
|------------|----------------------------------------------------|
| `Unknown`  | Server decides (currently parks)                   |
| `Park`     | Move to parked message stream                      |
| `Retry`    | Redeliver to a consumer                            |
| `Skip`     | Discard the message                                |

### Consumer Strategies

| Strategy               | Description                                               |
|------------------------|-----------------------------------------------------------|
| `RoundRobin` (default) | Distributes events evenly across consumers                |
| `DispatchToSingle`     | Sends all events to one consumer until it disconnects     |
| `Pinned`               | Hashes stream ID to 1024 buckets, pins bucket to consumer |
| `PinnedByCorrelation`  | Hashes correlation ID to pin to consumer                  |

### PersistentSubscriptionSettings Defaults

| Property                | Default         | Description                               |
|-------------------------|-----------------|-------------------------------------------|
| `ResolveLinkTos`        | `false`         | Resolve link events to original events    |
| `StartFrom`             | `null` (end)    | Position to start reading from            |
| `ExtraStatistics`       | `false`         | Track latency percentile statistics       |
| `MessageTimeout`        | `30s`           | Time before unacked message is retried    |
| `MaxRetryCount`         | `10`            | Max retries before parking                |
| `LiveBufferSize`        | `500`           | Buffer for live events                    |
| `ReadBatchSize`         | `20`            | Batch size when reading historical events |
| `HistoryBufferSize`     | `500`           | Buffer for historical events              |
| `CheckPointAfter`       | `2s`            | Checkpoint write interval                 |
| `MinCheckPointCount`    | `10`            | Min events between checkpoints            |
| `MaxCheckPointCount`    | `1000`          | Max events between checkpoints            |
| `MaxSubscriberCount`    | `0` (unbounded) | Max concurrent consumers                  |
| `NamedConsumerStrategy` | `RoundRobin`    | Consumer dispatch strategy                |

### Update and Delete

```csharp
// Update settings
await persistentSubClient.UpdateToStreamAsync(
    "order-123",
    "order-processing-group",
    new PersistentSubscriptionSettings(messageTimeout: TimeSpan.FromSeconds(60))
);

// Delete
await persistentSubClient.DeleteToStreamAsync(
    "order-123",
    "order-processing-group"
);
// Throws PersistentSubscriptionNotFoundException if not found
```

---

## Projection Management

Requires a separate client class.

```csharp
var projectionClient = new KurrentDBProjectionManagementClient(settings);
```

### Creating a Projection

```csharp
var jsQuery = @"
    fromStream('order-123')
    .when({
        $init: function() { return { count: 0 }; },
        OrderPlaced: function(state, event) {
            state.count++;
            return state;
        }
    });
";

await projectionClient.CreateContinuousAsync("order-count", jsQuery);
```

### Lifecycle Operations

| Method                 | Description                                     |
|------------------------|-------------------------------------------------|
| `EnableAsync(name)`    | Start/resume projection                         |
| `DisableAsync(name)`   | Stop projection, saves checkpoint               |
| `AbortAsync(name)`     | Stop projection, does NOT save checkpoint       |
| `ResetAsync(name)`     | Deletes checkpoint, soft-deletes output streams |
| `UpdateAsync(name, q)` | Update projection query                         |

### Querying Projections

```csharp
// List projections
var allProjections = await projectionClient.ListAllAsync();
var continuousOnly = await projectionClient.ListContinuousAsync();

// Status
var status = await projectionClient.GetStatusAsync("order-count");

// State (current accumulated state)
var state = await projectionClient.GetStateAsync<OrderCountState>("order-count");

// Result (output of resultStream)
var result = await projectionClient.GetResultAsync<OrderCountResult>("order-count");
```

### Administrative Operations

```csharp
// Restart the entire projection subsystem (requires $ops or $admin)
await projectionClient.RestartSubsystemAsync();
```

### Error Handling

```csharp
try
{
    await projectionClient.CreateContinuousAsync("order-count", jsQuery);
}
catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
{
    // Projection already exists
}
catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
{
    // Projection not found
}
```

> **Note:** Projection delete is NOT currently supported by the client API.

---

## Stream Deletion

| Method           | Behavior                                                          |
|------------------|-------------------------------------------------------------------|
| `DeleteAsync`    | **Soft delete** -- stream can be reopened by appending new events |
| `TombstoneAsync` | **Hard delete** -- permanent, stream name can never be reused     |

```csharp
// Soft delete
await client.DeleteAsync("order-123", StreamState.Any);
// Stream can be recreated by appending to it again

// Hard delete (PERMANENT)
await client.TombstoneAsync("order-123", StreamState.Any);
// Stream name "order-123" is permanently unusable
```

Both methods accept a `StreamState` or `ulong` revision for optimistic concurrency.

---

## Authentication

### Basic Authentication

Username and password in the connection string:

```
kurrentdb://admin:changeit@localhost:2113?tls=false
```

Or override per-operation:

```csharp
var credentials = new UserCredentials("admin", "changeit");
await client.ReadAllAsync(Direction.Forwards, Position.Start, userCredentials: credentials);
```

### X.509 Certificate Authentication (v25.0+, License Required)

```
kurrentdb://localhost:2113?userCertFile=/path/to/user.crt&userKeyFile=/path/to/user.key
```

- Both `userCertFile` and `userKeyFile` must be provided (error if only one).
- If both certificate and username/password are provided, user credentials take priority.

---

## Observability

### Package

```
KurrentDB.Client.Extensions.OpenTelemetry
```

### Setup

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddKurrentDBClientInstrumentation()
        .AddConsoleExporter()
    );
```

### Activity Source

The activity source name is `"kurrentdb"`.

### Traced Operations

- Append to stream
- Catch-up subscription
- Persistent subscription operations

### Trace Attributes

| Attribute                              | Description                      |
|----------------------------------------|----------------------------------|
| `db.user`                              | Authenticated user               |
| `db.system`                            | Always `"eventstoredb"`          |
| `db.operation`                         | Operation name                   |
| `db.eventstoredb.stream`               | Stream name                      |
| `db.eventstoredb.subscription.id`      | Subscription identifier          |
| `db.eventstoredb.event.id`             | Event UUID                       |
| `db.eventstoredb.event.type`           | Event type name                  |
| `server.address`                       | Server hostname                  |
| `server.port`                          | Server port                      |

> **Note:** The `db.system` attribute value is still `"eventstoredb"` (not `"kurrentdb"`) in traces.

---

## Gotchas and Common Mistakes

1. **Class name**: The class is `KurrentDBClient` with **two r's**. Documentation or autocomplete may show a typo.
2. **Singleton client**: Create the client once. Do not create/dispose per operation.
3. **TLS required by default**: Use `tls=false` explicitly for insecure/development connections.
4. **JSON required for projections**: Events must be JSON-serialized for server-side projections to process them.
5. **Idempotent writes**: Idempotency via `eventId` only works with a specific stream revision, not with `StreamState.Any`.
6. **Subscription positions are exclusive**: `FromStream.After(5)` delivers event 6 onwards, not event 5.
7. **$all requires admin**: Reading from or subscribing to `$all` requires admin credentials.
8. **Hard delete is permanent**: `TombstoneAsync` makes the stream name permanently unusable.
9. **CaughtUp message**: Only available on server v23.10+ and only fires on transition from historical to live.
10. **Checkpoint interval math**: The `checkpointInterval` parameter is multiplied by 32 internally.
11. **Projection delete unsupported**: The client API does not support deleting projections.
12. **Trace system name**: `db.system` reports `"eventstoredb"`, not `"kurrentdb"`.
13. **Multi-stream append**: `MultiStreamAppendAsync` requires server v25.1 or later.
14. **Persistent subscription buffer**: Default buffer size is 10 -- tune for your workload to avoid slow consumers.
