# Streams and Events Reference

## Table of Contents

- [Events](#events)
- [Streams](#streams)
- [Stream Metadata](#stream-metadata)
- [Optimistic Concurrency](#optimistic-concurrency)
- [Deleting Streams and Events](#deleting-streams-and-events)
- [System Streams](#system-streams)
- [Projection Output Streams](#projection-output-streams)

---

## Events

Events are **immutable state changes** — the fundamental unit of data in KurrentDB. Once written, an event cannot be modified or removed from the middle of a stream.

### EventData Structure

| Field      | Type         | Required | Description                                  |
|------------|--------------|----------|----------------------------------------------|
| `eventId`  | UUID         | Yes      | Unique identifier; enables idempotent writes |
| `type`     | string       | Yes      | Event type name (e.g., `OrderPlaced`)        |
| `data`     | bytes (JSON) | Yes      | Event body, typically JSON-serialized        |
| `metadata` | bytes (JSON) | No       | Arbitrary metadata attached to the event     |

### Positions

Each event carries two position values:

| Position          | Scope            | Description                                                  |
|-------------------|------------------|--------------------------------------------------------------|
| `StreamPosition`  | Within a stream  | Zero-based index of the event within its stream              |
| `Position`        | Within `$all`    | Global position expressed as commit + prepare positions      |

### Metadata Conventions

The following metadata properties are honored by the projection subsystem:

- `$correlationId` — groups related events across streams; used by `$by_correlation_id` projection
- `$causationId` — identifies the event that caused this event

```csharp
var eventData = new EventData(
    Uuid.NewUuid(),
    "OrderPlaced",
    JsonSerializer.SerializeToUtf8Bytes(new { OrderId = "abc-123", Amount = 99.95 }),
    JsonSerializer.SerializeToUtf8Bytes(new { ["$correlationId"] = correlationId })
);
```

---

## Streams

A stream is a **logical grouping of events** identified by a case-sensitive stream ID string. KurrentDB supports billions of streams natively.

### Naming Convention

Use the pattern `{entity-type}-{unique-id}`:

```
order-bc2f3a4e
customer-7f21d903
invoice-2024-00451
```

### Design Guideline

**One stream per entity instance** is the recommended approach. Each aggregate root or entity in your domain should have its own stream containing all events for that instance.

---

## Stream Metadata

Every stream has an associated metadata stream named `$$<streamname>`. Metadata controls retention, caching, and access.

### Retention Settings

| Property        | Type    | Description                                                    |
|-----------------|---------|----------------------------------------------------------------|
| `$maxAge`       | seconds | Sliding window by time — events older than this auto-disappear |
| `$maxCount`     | integer | Sliding window by count — only the last N events are retained  |
| `$cacheControl` | seconds | Cache TTL hint for HTTP caching                                |

When **both** `$maxAge` and `$maxCount` are set, events become eligible for removal when **either** condition is met.

Expired events auto-disappear from reads immediately. Physical removal happens during scavenging.

### Access Control Lists

ACL properties are set under the `$acl` object in stream metadata:

| Property | Description       |
|----------|-------------------|
| `$r`     | Read permission   |
| `$w`     | Write permission  |
| `$d`     | Delete permission |
| `$mr`    | Metadata read     |
| `$mw`    | Metadata write    |

---

## Optimistic Concurrency

KurrentDB uses a **lock-free** optimistic concurrency mechanism with no performance penalty.

### Expected State Options

| State                          | Behavior                                                    |
|--------------------------------|-------------------------------------------------------------|
| `StreamState.Any`              | No concurrency check — always appends                       |
| `StreamState.NoStream`         | Fails if the stream already exists                          |
| `StreamState.StreamExists`     | Fails if the stream does not exist                          |
| Specific revision (`ulong`)    | Fails if the stream's current revision doesn't match        |

### Usage Pattern

```csharp
// Read the stream to get the current revision
var result = client.ReadStreamAsync(Direction.Backwards, "order-123", StreamPosition.End, maxCount: 1);
var lastEvent = await result.FirstAsync();
var expectedRevision = lastEvent.Event.EventNumber;

// Append with expected revision
try
{
    await client.AppendToStreamAsync("order-123", expectedRevision, new[] { eventData });
}
catch (WrongExpectedVersionException ex)
{
    // Handle conflict — re-read and retry or reject
}
```

---

## Deleting Streams and Events

### Soft Delete

- Sets `TruncateBefore` to `Int64.MaxValue`
- Stream returns **404 / StreamNotFound**
- Stream **can be reopened** by appending new events (continues from last version + 1)
- `$all` bypasses the index — deleted events remain readable in `$all` until scavenged

```csharp
await client.SoftDeleteAsync("order-123", StreamState.Any);
```

### Hard Delete

- Writes a **tombstone event** (`$streamDeleted`)
- Stream returns **410 / StreamDeleted**
- Stream **cannot** be recreated or appended to ever again
- Tombstone persists even after scavenge

```csharp
await client.DeleteAsync("order-123", StreamState.Any);
```

### TruncateBefore

Set via stream metadata as `$tb`. Events with a stream position less than this value are treated as deleted.

### Important Constraints

- You **cannot** selectively delete individual events from the middle of a stream
- **Best practice for sensitive data**: append a `StreamDeleted` event and set `$maxCount` to `1`

### Comparison

| Aspect              | Soft Delete              | Hard Delete                    |
|---------------------|--------------------------|--------------------------------|
| HTTP status         | 404 StreamNotFound       | 410 StreamDeleted              |
| Can reopen          | Yes                      | No — permanent                 |
| Events in `$all`    | Visible until scavenged  | Tombstone persists forever     |
| Reversible          | Yes (append to reopen)   | No                             |

---

## System Streams

System streams use a `$` prefix and serve internal purposes.

| Stream                           | Description                                                 |
|----------------------------------|-------------------------------------------------------------|
| `$all`                           | Contains all events, paged reading, requires admin          |
| `$settings`                      | Default ACLs for streams without explicit ACLs              |
| `$persistentSubscriptionConfig`  | Persistent subscription configuration events                |
| `$stats`                         | Debugging and statistical information                       |
| `$scavenges`                     | Scavenge lifecycle events (initialized, started, completed) |
| `$authorization-policy-settings` | Stream policy configuration                                 |
| `$policies`                      | Stream policy definitions                                   |

### Default ACLs in `$settings`

The `$settings` stream controls default permissions via two properties:

- `$userStreamAcl` — default ACL for user streams (no `$` prefix)
- `$systemStreamAcl` — default ACL for system streams (`$` prefix)

### Metadata Streams for System Streams

Metadata streams for system streams use a `$$$` prefix (e.g., `$$$$settings`).

---

## Projection Output Streams

These streams are created and managed by the projection subsystem. **Never append to projection output streams** — doing so will fault the projection.

| Stream Pattern         | Source Projection     | Contents                                 |
|------------------------|-----------------------|------------------------------------------|
| `$ce-{category}`       | `$by_category`        | Link events by category                  |
| `$et-{eventType}`      | `$by_event_type`      | Link events by event type                |
| `$bc-{correlationId}`  | `$by_correlation_id`  | Link events by correlation ID            |
| `$category-{category}` | `$stream_by_category` | Link events by category (stream variant) |
| `$streams`             | `$streams`            | All stream references                    |

### Internal Projection Streams

Each projection maintains several internal streams:

| Stream Pattern                        | Purpose                    |
|---------------------------------------|----------------------------|
| `$projections-{name}`                 | Projection definition      |
| `$projections-{name}-checkpoint`      | Processing checkpoint      |
| `$projections-{name}-result`          | Projection result/state    |
| `$projections-{name}-emittedstreams`  | Tracked emitted streams    |
| `$projections-{name}-order`           | Ordering information       |
| `$projections-{name}-partitions`      | Partition tracking         |
