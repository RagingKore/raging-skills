# KurrentDB Subscriptions Reference

## Table of Contents

- [Subscription Types Overview](#subscription-types-overview)
- [Catch-Up Subscriptions](#catch-up-subscriptions)
  - [Stream Subscriptions](#stream-subscriptions)
  - [$all Subscriptions](#all-subscriptions)
  - [Message Pattern Matching](#message-pattern-matching)
  - [Checkpoint Recovery](#checkpoint-recovery)
  - [Resolving Link Events](#resolving-link-events)
  - [Server-Side Filtering](#server-side-filtering)
- [Persistent Subscriptions](#persistent-subscriptions)
  - [Creating Subscription Groups](#creating-subscription-groups)
  - [Connecting Consumers](#connecting-consumers)
  - [Ack/Nack Protocol](#acknack-protocol)
  - [Consumer Strategies](#consumer-strategies)
  - [Settings Reference](#settings-reference)
  - [Parked Messages](#parked-messages)
  - [Checkpointing Behavior](#checkpointing-behavior)
  - [Updating and Deleting Groups](#updating-and-deleting-groups)
- [When to Use Which](#when-to-use-which)
- [Key Warnings](#key-warnings)

---

## Subscription Types Overview

| Feature            | Catch-Up                        | Persistent                               |
|--------------------|---------------------------------|------------------------------------------|
| Position tracking  | Client-managed                  | Server-managed                           |
| Consumer count     | Single                          | Multiple (competing)                     |
| Ordering guarantee | Yes                             | No                                       |
| Delivery guarantee | At-least-once (with checkpoint) | At-least-once                            |
| Acknowledgment     | Not required                    | Required (Ack/Nack)                      |
| Client class       | `KurrentDBClient`               | `KurrentDBPersistentSubscriptionsClient` |

---

## Catch-Up Subscriptions

Catch-up subscriptions read all historical events from a position and then continue receiving live events. The client is responsible for tracking position (checkpointing).

### Stream Subscriptions

```csharp
var subscription = client.SubscribeToStream(
    "order-123",
    FromStream.Start,          // Start from the beginning
    resolveLinkTos: false
);

await foreach (var message in subscription.Messages)
{
    switch (message)
    {
        case StreamMessage.Event(var resolvedEvent):
            await HandleEvent(resolvedEvent);
            await SaveCheckpoint(resolvedEvent.Event.EventNumber);
            break;

        case StreamMessage.CaughtUp:
            Console.WriteLine("Caught up with live events");
            break;
    }
}
```

#### FromStream Options

| Value                         | Behavior                                              |
|-------------------------------|-------------------------------------------------------|
| `FromStream.Start`            | Read from the beginning of the stream                 |
| `FromStream.End`              | Start from live events only                           |
| `FromStream.After(position)`  | Start after the given position (**exclusive**)        |

### $all Subscriptions

```csharp
var subscription = client.SubscribeToAll(
    FromAll.Start,
    resolveLinkTos: false,
    userCredentials: new UserCredentials("admin", "changeit")
);

await foreach (var message in subscription.Messages)
{
    switch (message)
    {
        case StreamMessage.Event(var resolvedEvent):
            if (resolvedEvent.Event.EventType.StartsWith("$")) continue;
            await HandleEvent(resolvedEvent);
            await SaveCheckpoint(resolvedEvent.OriginalPosition!.Value);
            break;

        case StreamMessage.AllStreamCheckpointReached(var position):
            await SaveCheckpoint(position);
            break;
    }
}
```

#### FromAll Options

| Value                      | Behavior                                            |
|----------------------------|-----------------------------------------------------|
| `FromAll.Start`            | Read from the beginning of the event store          |
| `FromAll.End`              | Start from live events only                         |
| `FromAll.After(position)`  | Start after the given position (**exclusive**)      |

> **Important:** `$all` requires admin credentials.

### Message Pattern Matching

| Message Type                                      | When It Fires                                                                 |
|---------------------------------------------------|-------------------------------------------------------------------------------|
| `StreamMessage.Event(var resolvedEvent)`          | An event was delivered                                                        |
| `StreamMessage.CaughtUp`                          | Transitioned from reading history to live (v23.10+, fires once on transition) |
| `StreamMessage.AllStreamCheckpointReached(var p)` | Periodic checkpoint for filtered `$all` subscriptions                         |

### Checkpoint Recovery

To resume a subscription after restart, save the position of each processed event and use it as the start point.

#### Stream Checkpoint Pattern

```csharp
// On startup: load last checkpoint
var lastPosition = await LoadStreamCheckpoint(); // returns StreamPosition?

var startFrom = lastPosition.HasValue
    ? FromStream.After(lastPosition.Value)   // resume after last processed
    : FromStream.Start;                       // first run: start from beginning

var subscription = client.SubscribeToStream("order-123", startFrom);

await foreach (var message in subscription.Messages)
{
    if (message is StreamMessage.Event(var resolvedEvent))
    {
        await HandleEvent(resolvedEvent);
        await SaveStreamCheckpoint(resolvedEvent.Event.EventNumber);
    }
}
```

#### $all Checkpoint Pattern

```csharp
// On startup: load last checkpoint
var lastPosition = await LoadAllCheckpoint(); // returns Position?

var startFrom = lastPosition.HasValue
    ? FromAll.After(lastPosition.Value)
    : FromAll.Start;

var subscription = client.SubscribeToAll(startFrom, filterOptions: filterOptions);

await foreach (var message in subscription.Messages)
{
    switch (message)
    {
        case StreamMessage.Event(var resolvedEvent):
            await HandleEvent(resolvedEvent);
            await SaveAllCheckpoint(resolvedEvent.OriginalPosition!.Value);
            break;

        case StreamMessage.AllStreamCheckpointReached(var position):
            // Save checkpoint even when no matching events pass the filter
            await SaveAllCheckpoint(position);
            break;
    }
}
```

### Resolving Link Events

System projection streams (like `$ce-order` or `$et-OrderPlaced`) contain **link events** that point to the original events. Set `resolveLinkTos: true` to automatically resolve them.

```csharp
// Subscribe to a category projection stream
var subscription = client.SubscribeToStream(
    "$ce-order",
    FromStream.Start,
    resolveLinkTos: true   // Resolve links to original events
);
```

| `resolveLinkTos`  | Behavior                                              |
|-------------------|-------------------------------------------------------|
| `false` (default) | Returns link events as-is (metadata contains pointer) |
| `true`            | Resolves link to original event data                  |

### Server-Side Filtering

Server-side filtering is only available on `$all` subscriptions. Use it to reduce network traffic and processing overhead.

#### Available Filters

| Filter                                          | Matches                                   |
|-------------------------------------------------|-------------------------------------------|
| `StreamFilter.Prefix("order-")`                 | Streams with names starting with `order-` |
| `StreamFilter.RegularExpression("^order-\\d+")` | Streams matching the regex pattern        |
| `EventTypeFilter.Prefix("Order")`               | Event types starting with `Order`         |
| `EventTypeFilter.RegularExpression("^Order")`   | Event types matching the regex pattern    |
| `EventTypeFilter.ExcludeSystemEvents()`         | All events except those starting with `$` |

#### Filtered Subscription with Checkpoint Interval

```csharp
var filterOptions = new SubscriptionFilterOptions(
    EventTypeFilter.ExcludeSystemEvents(),
    checkpointInterval: 10    // checkpoint every 10 * 32 = 320 events
);

var subscription = client.SubscribeToAll(
    FromAll.Start,
    filterOptions: filterOptions
);

await foreach (var message in subscription.Messages)
{
    switch (message)
    {
        case StreamMessage.Event(var resolvedEvent):
            await HandleEvent(resolvedEvent);
            await SaveCheckpoint(resolvedEvent.OriginalPosition!.Value);
            break;

        case StreamMessage.AllStreamCheckpointReached(var position):
            // Fires periodically even when events are filtered out
            await SaveCheckpoint(position);
            break;
    }
}
```

> **Checkpoint interval math:** The `checkpointInterval` value N means the server emits a checkpoint every N * 32 events processed (regardless of how many pass the filter).

---

## Persistent Subscriptions

Persistent subscriptions are managed by the server. The server tracks the checkpoint, distributes events to competing consumers, and handles retries. They provide at-least-once delivery with multiple consumers.

### Creating Subscription Groups

Requires `KurrentDBPersistentSubscriptionsClient`.

```csharp
var persistentSubClient = new KurrentDBPersistentSubscriptionsClient(settings);
```

#### To a Stream

```csharp
await persistentSubClient.CreateToStreamAsync(
    "order-123",                       // stream name
    "order-processing-group",          // group name
    new PersistentSubscriptionSettings(
        resolveLinkTos: true,
        startFrom: StreamPosition.Start,
        maxRetryCount: 5,
        messageTimeout: TimeSpan.FromSeconds(30)
    )
);
```

#### To $all (with Filter)

```csharp
await persistentSubClient.CreateToAllAsync(
    "all-orders-group",
    EventTypeFilter.Prefix("Order"),
    new PersistentSubscriptionSettings(
        startFrom: Position.Start
    )
);
```

### Connecting Consumers

Multiple consumer instances can connect to the same group. The server distributes events according to the configured consumer strategy.

```csharp
var subscription = persistentSubClient.SubscribeToStream(
    "order-123",
    "order-processing-group"
);

await foreach (var message in subscription.Messages)
{
    switch (message)
    {
        case PersistentSubscriptionMessage.SubscriptionConfirmation:
            Console.WriteLine("Connected to persistent subscription");
            break;

        case PersistentSubscriptionMessage.Event(var resolvedEvent, var retryCount):
            try
            {
                await ProcessEvent(resolvedEvent);
                await subscription.Ack(resolvedEvent);
            }
            catch (TransientException)
            {
                await subscription.Nack(
                    PersistentSubscriptionNakEventAction.Retry,
                    "Transient failure, will retry",
                    resolvedEvent
                );
            }
            catch (PoisonMessageException ex)
            {
                await subscription.Nack(
                    PersistentSubscriptionNakEventAction.Park,
                    $"Poison message: {ex.Message}",
                    resolvedEvent
                );
            }
            break;
    }
}
```

For `$all`:

```csharp
var subscription = persistentSubClient.SubscribeToAll("all-orders-group");
```

### Ack/Nack Protocol

Every message delivered by a persistent subscription **must** be acknowledged. Unacknowledged messages will timeout (default 30s) and be retried.

| Method | Description                            |
|--------|----------------------------------------|
| `Ack`  | Confirm successful processing          |
| `Nack` | Report failure with a specified action |

#### Nack Actions

| Action    | Behavior                                                |
|-----------|---------------------------------------------------------|
| `Unknown` | Server decides (currently equivalent to Park)           |
| `Park`    | Move to the parked message stream for manual inspection |
| `Retry`   | Redeliver to a consumer (increments retry count)        |
| `Skip`    | Discard the message permanently                         |

### Consumer Strategies

| Strategy               | Behavior                                                                                                                |
|------------------------|-------------------------------------------------------------------------------------------------------------------------|
| `RoundRobin` (default) | Events distributed evenly across all connected consumers in round-robin order                                           |
| `DispatchToSingle`     | All events go to one consumer; fails over to another on disconnect                                                      |
| `Pinned`               | Stream ID hashed to 1024 buckets; each bucket pinned to one consumer. All events from a stream go to the same consumer. |
| `PinnedByCorrelation`  | Like Pinned but hashes correlation ID instead of stream ID                                                              |

**Pinned strategy note:** The Pinned strategy behaves differently depending on `ResolveLinkTos`. When resolving links, the resolved stream ID is used for hashing. When not resolving, the link stream ID is used. This can change which consumer receives events.

### Settings Reference

| Setting                 | Type       | Default         | Description                                                  |
|-------------------------|------------|-----------------|--------------------------------------------------------------|
| `ResolveLinkTos`        | `bool`     | `false`         | Resolve link events to original events                       |
| `StartFrom`             | varies     | `null` (end)    | `StreamPosition` for stream subs, `Position` for `$all` subs |
| `ExtraStatistics`       | `bool`     | `false`         | Enable latency percentile tracking                           |
| `MessageTimeout`        | `TimeSpan` | `30s`           | Time before unacked message is retried                       |
| `MaxRetryCount`         | `int`      | `10`            | Max retries before message is parked                         |
| `LiveBufferSize`        | `int`      | `500`           | Buffer size for live events waiting to be served             |
| `ReadBatchSize`         | `int`      | `20`            | Batch size when reading historical events                    |
| `HistoryBufferSize`     | `int`      | `500`           | Buffer size for historical events                            |
| `CheckPointAfter`       | `TimeSpan` | `2s`            | Time interval between checkpoint writes                      |
| `MinCheckPointCount`    | `int`      | `10`            | Minimum events processed between checkpoints                 |
| `MaxCheckPointCount`    | `int`      | `1000`          | Maximum events before checkpoint is forced                   |
| `MaxSubscriberCount`    | `int`      | `0` (unbounded) | Max concurrent consumers (0 = unlimited)                     |
| `NamedConsumerStrategy` | `string`   | `RoundRobin`    | Consumer dispatch strategy                                   |

### Parked Messages

When a message is Nacked with `Park`, or exceeds `MaxRetryCount`, it is moved to a parked message stream:

```
$persistentsubscription-{stream}::{group}-parked
```

#### Replaying Parked Messages

Use the HTTP API to replay parked messages back into the subscription:

```http
POST /subscriptions/{stream}/{group}/replayParked?stopAt=100
```

- Omit `stopAt` to replay all parked messages.
- `stopAt=N` replays only the first N parked messages.

### Checkpointing Behavior

Persistent subscriptions write checkpoints periodically to a checkpoint stream. The checkpoint tracks the last event that was acknowledged by all consumers.

- Checkpoints are written based on `CheckPointAfter` interval and `MinCheckPointCount`/`MaxCheckPointCount` thresholds.
- On leader node change, the subscription reloads from the last checkpoint. This means **some events may be redelivered** (duplicates are possible).
- Persistent subscriptions **only run on the leader node**.

### Updating and Deleting Groups

```csharp
// Update settings (e.g., increase message timeout)
await persistentSubClient.UpdateToStreamAsync(
    "order-123",
    "order-processing-group",
    new PersistentSubscriptionSettings(
        messageTimeout: TimeSpan.FromSeconds(60),
        maxRetryCount: 20
    )
);

// Delete a subscription group
await persistentSubClient.DeleteToStreamAsync(
    "order-123",
    "order-processing-group"
);
// Throws PersistentSubscriptionNotFoundException if group does not exist
```

---

## When to Use Which

### Use Catch-Up Subscriptions When

- **Ordering matters** -- events must be processed in strict order
- **Single processor** -- only one consumer processes the stream
- **Building read models** -- projecting events into query-optimized views
- **Event handlers** -- reacting to events in a deterministic pipeline
- **Replaying from scratch** -- need to rebuild state from the beginning

### Use Persistent Subscriptions When

- **Scalable processing** -- multiple consumers need to share the workload
- **At-least-once is acceptable** -- processing is idempotent or tolerates duplicates
- **Load balancing** -- distribute work across consumer instances
- **Server-managed checkpointing** -- prefer not to manage checkpoint storage
- **Independent processing** -- event order across streams does not matter

### Use Connectors When

- **External system integration** -- pushing events to Kafka, RabbitMQ, HTTP endpoints
- **Cross-system replication** -- forwarding events outside KurrentDB
- **Managed delivery** -- server handles retries and delivery tracking

### Decision Table

| Requirement                     | Catch-Up | Persistent | Connector |
|---------------------------------|----------|------------|-----------|
| Strict ordering                 | Yes      | No         | Varies    |
| Multiple competing consumers    | No       | Yes        | No        |
| Server-managed checkpoint       | No       | Yes        | Yes       |
| Build read models               | Yes      | Possible   | No        |
| Push to external systems        | No       | No         | Yes       |
| Replay from beginning           | Yes      | Yes        | Varies    |
| Idempotency not required        | Yes      | No         | No        |

---

## Key Warnings

1. **Persistent subscriptions do NOT guarantee ordering.** Events may be delivered out of order across consumers. If ordering matters, use catch-up subscriptions.

2. **Pinned strategy + ResolveLinkTos interaction.** When `ResolveLinkTos` is true, Pinned uses the resolved (original) stream ID for hashing. When false, it uses the link stream ID. This changes consumer assignment.

3. **Slow consumers can be dropped.** If a consumer falls too far behind or does not acknowledge messages in time, the subscription may drop the connection. Implement reconnection logic.

4. **Duplicate delivery after leader change.** Persistent subscriptions reload from the last checkpoint when the leader node changes. Events processed after the last checkpoint but before the failover will be redelivered. Design consumers to handle duplicates.

5. **Persistent subscriptions run on leader only.** If the leader node changes, there will be a brief interruption while the subscription migrates.

6. **Subscription positions are exclusive.** `FromStream.After(5)` starts delivering from event 6. `FromAll.After(position)` starts from the next event after that position.

7. **CaughtUp fires once on transition.** The `StreamMessage.CaughtUp` message only fires when transitioning from reading historical events to live. It requires server v23.10 or later. It does not fire repeatedly.

8. **Filtered $all checkpoint interval.** The checkpoint interval is multiplied by 32. A `checkpointInterval` of 10 means a checkpoint every 320 events (10 * 32), not every 10 events.

9. **Buffer sizing for persistent subscriptions.** The default in-flight message buffer is 10. If your consumers are fast, increase the buffer to avoid starvation. If they are slow, decrease to avoid message timeouts.

10. **Always Ack or Nack.** Not acknowledging a persistent subscription message causes it to timeout and retry, consuming retry budget. Always explicitly Ack on success or Nack with an appropriate action on failure.
