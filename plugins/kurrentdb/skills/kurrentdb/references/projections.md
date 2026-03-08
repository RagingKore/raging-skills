# Projections Reference

## Table of Contents

- [Overview](#overview)
- [Write Amplification Warning](#write-amplification-warning)
- [System Projections](#system-projections)
- [Custom Projections](#custom-projections)
- [JavaScript API](#javascript-api)
- [Configuration](#configuration)
- [Server Settings](#server-settings)
- [Projection Management API](#projection-management-api)
- [.NET Client Management](#net-client-management)
- [Common Patterns](#common-patterns)
- [Gotchas](#gotchas)

---

## Overview

Projections are a **server-side subsystem** that processes events from streams and emits or links events to new streams. They are JavaScript-based and run in continuous mode, processing events as they arrive.

### Key Constraints

- Runs **only on the leader node** in a cluster
- **Requires JSON-serialized event bodies** — binary events are ignored
- Best suited for "temporal correlation queries" — grouping events across streams by time-based relationships
- Continuous mode keeps projections running into the future, processing new events as they are appended

---

## Write Amplification Warning

Projections create additional write operations for every event they process. This has direct impact on storage and throughput.

| Scenario                         | Write Amplification                                 |
|----------------------------------|-----------------------------------------------------|
| No projections                   | 1x (baseline)                                       |
| All 5 system projections enabled | Significant multiplication per event                |
| Custom projections with emit     | Highest amplification — each emit/linkTo is a write |
| `trackemittedstreams` enabled    | Adds a separate tracking event per emit             |

**Plan storage and throughput capacity accordingly.** Only enable the projections you actually need.

---

## System Projections

KurrentDB includes 5 built-in system projections. All are disabled by default.

### 1. $by_category

Links events to `$ce-{category}` streams based on stream name prefix.

| Setting            | Default | Description                               |
|--------------------|---------|-------------------------------------------|
| `separator`        | `-`     | Character that separates category from ID |
| `categoryPosition` | `first` | Use `first` or `last` segment as category |

Example: stream `order-bc2f3a4e` produces a link event in `$ce-order`.

### 2. $by_event_type

Links events to `$et-{eventType}` streams based on the event type name.

- **Not configurable**
- Example: an `OrderPlaced` event produces a link in `$et-OrderPlaced`

### 3. $by_correlation_id

Links events to `$bc-{correlationId}` streams based on metadata.

| Setting                  | Default            | Description                      |
|--------------------------|--------------------|----------------------------------|
| `correlationIdProperty`  | `$correlationId`   | Metadata property to read        |

### 4. $stream_by_category

Links events to `$category-{category}` streams. Uses the same configuration as `$by_category` (separator and categoryPosition).

### 5. $streams

Links events to the `$streams` stream.

- **Not configurable**
- Contains references to all streams that receive events

### Enabling System Projections

```http
POST /projection/$by_category/command/enable
```

---

## Custom Projections

Custom projections are written in JavaScript and created via the HTTP API.

### Creating a Projection

```http
POST /projections/continuous?name=my-projection&type=js&enabled=true&emit=true&trackemittedstreams=true
Content-Type: application/json

fromCategory("order")
  .foreachStream()
  .when({
    $init: function() { return { total: 0 }; },
    OrderPlaced: function(s, e) { s.total = e.body.amount; }
  })
  .outputState();
```

---

## JavaScript API

### Selectors

Selectors define which events the projection processes.

| Selector                 | Source           | Available Methods                                     |
|--------------------------|------------------|-------------------------------------------------------|
| `fromAll()`              | `$all` stream    | `partitionBy`, `when`, `foreachStream`, `outputState` |
| `fromCategory(category)` | `$ce-{category}` | `partitionBy`, `when`, `foreachStream`, `outputState` |
| `fromStream(streamId)`   | Single stream    | `partitionBy`, `when`, `outputState`                  |
| `fromStreams([array])`   | Multiple streams | `partitionBy`, `when`, `outputState`                  |

### Event Handlers

Handlers are defined inside the `when({})` block.

| Handler           | Signature                 | Description                                                      |
|-------------------|---------------------------|------------------------------------------------------------------|
| `$init`           | `() => initialState`      | Returns initial state object (required for stateful projections) |
| `$initShared`     | `() => sharedState`       | Shared initialization for partitioned projections                |
| `$any`            | `(state, event) => state` | Matches any event type                                           |
| `$deleted`        | `(state, event) => state` | Stream deletion handler (`foreachStream` only)                   |
| `"EventTypeName"` | `(state, event) => state` | Matches a specific event type by name                            |

### Event Object Properties

| Property          | Description                       |
|-------------------|-----------------------------------|
| `isJson`          | Whether the event body is JSON    |
| `data`            | Parsed JSON body (alias: `body`)  |
| `body`            | Parsed JSON body (alias: `data`)  |
| `bodyRaw`         | Raw event body as string          |
| `sequenceNumber`  | Event number within the stream    |
| `metadataRaw`     | Raw metadata as string            |
| `linkMetadataRaw` | Raw link event metadata as string |
| `partition`       | Partition key for this event      |
| `eventType`       | Event type name                   |
| `streamId`        | Source stream ID                  |

### Actions

| Action                                           | Description                                           |
|--------------------------------------------------|-------------------------------------------------------|
| `emit(streamId, eventType, eventBody, metadata)` | Appends a **new** event to a stream                   |
| `linkTo(streamId, event, metadata)`              | Writes a **link event** referencing an existing event |
| `log(message)`                                   | Debug logging output                                  |

**emit vs linkTo**: Use `linkTo` when you want to reference an existing event without duplicating data. Use `emit` when you need to create a new derived event with different data.

### Chaining Methods

| Method                          | Description                                        |
|---------------------------------|----------------------------------------------------|
| `foreachStream()`               | Partition state per stream automatically           |
| `partitionBy(function(event))`  | Custom partitioning by return value                |
| `outputState()`                 | Produces a result stream with current state        |
| `transformBy(function(state))`  | Transform state before outputting                  |
| `filterBy(function(state))`     | Return `null` to filter out a partition's state    |

### Options

| Option             | Default | Description                                                     |
|--------------------|---------|-----------------------------------------------------------------|
| `resultStreamName` | auto    | Override the default result stream name                         |
| `$includeLinks`    | `false` | Include or exclude link events from processing                  |
| `processingLag`    | `500`   | Buffer threshold in ms for `reorderEvents` (only `fromStreams`) |
| `reorderEvents`    | `false` | Buffer events by prepare position (only `fromStreams`)          |

---

## Configuration

Configuration changes can **only** be applied to **STOPPED** projections.

### Emit Options

| Setting                    | Default   | Description                                                     |
|----------------------------|-----------|-----------------------------------------------------------------|
| `emit`                     | `false`   | Allow `emit()` and `linkTo()` calls                             |
| `trackemittedstreams`      | `false`   | Track emitted streams for deletion (causes write amplification) |
| `MaxAllowedWritesInFlight` | unbounded | Concurrent write limit                                          |
| `MaxWriteBatchLength`      | `500`     | Maximum events per write batch                                  |

### Checkpoint Options

| Setting                             | Default   | Description                                   |
|-------------------------------------|-----------|-----------------------------------------------|
| `CheckpointAfterMs`                 | `0`       | Minimum time between checkpoints (ms)         |
| `CheckpointHandledThreshold`        | `4000`    | Events processed before checkpoint attempt    |
| `CheckpointUnhandledBytesThreshold` | 10 MiB    | Bytes processed before checkpoint             |

### Processing Options

| Setting                      | Default  | Description                                   |
|------------------------------|----------|-----------------------------------------------|
| `PendingEventsThreshold`     | `5000`   | Maximum pending events before pausing         |
| `ProjectionExecutionTimeout` | `250`    | Per-event execution timeout (ms)              |

---

## Server Settings

These are server-level settings configured at startup.

| Setting                      | Default       | Description                                 |
|------------------------------|---------------|---------------------------------------------|
| `RunProjections`             | `None`        | `None`, `System`, or `All`                  |
| `ProjectionThreads`          | `3`           | Number of threads for projection processing |
| `FaultOutOfOrderProjections` | `false`       | Fault projections on out-of-order events    |
| `StartStandardProjections`   | `false`       | Auto-start system projections on boot       |

---

## Projection Management API

### HTTP Endpoints

| Operation | Method | Endpoint                                                                                            |
|-----------|--------|-----------------------------------------------------------------------------------------------------|
| Create    | POST   | `/projections/continuous?name={name}&type=js&enabled={bool}&emit={bool}&trackemittedstreams={bool}` |
| Enable    | POST   | `/projection/{name}/command/enable`                                                                 |
| Disable   | POST   | `/projection/{name}/command/disable`                                                                |
| Abort     | POST   | `/projection/{name}/command/abort`                                                                  |
| Reset     | POST   | `/projection/{name}/command/reset`                                                                  |
| Status    | GET    | `/projection/{name}`                                                                                |
| State     | GET    | `/projection/{name}/state`                                                                          |
| Result    | GET    | `/projection/{name}/result`                                                                         |
| Update    | PUT    | `/projection/{name}/query?emit={bool}`                                                              |
| List All  | GET    | `/projections/any`                                                                                  |

---

## .NET Client Management

The `KurrentDBProjectionManagementClient` provides full projection lifecycle management.

### Available Methods

| Method                     | Description                                |
|----------------------------|--------------------------------------------|
| `CreateContinuousAsync`    | Create a new continuous projection         |
| `EnableAsync`              | Enable a stopped projection                |
| `DisableAsync`             | Disable a running projection               |
| `AbortAsync`               | Abort a running projection                 |
| `ResetAsync`               | Reset projection checkpoint and state      |
| `UpdateAsync`              | Update projection query or settings        |
| `ListAllAsync`             | List all projections                       |
| `ListContinuousAsync`      | List only continuous projections           |
| `GetStatusAsync`           | Get projection status                      |
| `GetStateAsync<T>`         | Get typed projection state                 |
| `GetResultAsync<T>`        | Get typed projection result                |
| `RestartSubsystemAsync`    | Restart the projection subsystem           |

**Note**: Delete is **not supported** for projections.

---

## Common Patterns

### Aggregate State Per Entity

```javascript
fromCategory("order")
  .foreachStream()
  .when({
    $init: function() {
      return { total: 0, status: "pending" };
    },
    OrderPlaced: function(s, e) {
      s.total = e.body.amount;
      s.status = "placed";
    },
    OrderShipped: function(s, e) {
      s.status = "shipped";
    }
  })
  .outputState();
```

### Cross-Stream Linking and Derived Events

```javascript
fromAll().when({
  OrderPlaced: function(s, e) {
    linkTo("orders-" + e.body.region, e);
  },
  PaymentReceived: function(s, e) {
    emit("invoices", "InvoiceCreated", {
      orderId: e.body.orderId,
      amount: e.body.amount
    });
  }
});
```

### Global Event Counting

```javascript
fromAll()
  .when({
    $init: function() {
      return { count: 0 };
    },
    $any: function(s, e) {
      s.count++;
    }
  })
  .outputState();
```

### Custom Partitioning by Region

```javascript
fromCategory("order")
  .partitionBy(function(e) {
    return e.body.region;
  })
  .when({
    $init: function() {
      return { orderCount: 0, totalAmount: 0 };
    },
    OrderPlaced: function(s, e) {
      s.orderCount++;
      s.totalAmount += e.body.amount;
    }
  })
  .outputState();
```

---

## Gotchas

1. **JSON bodies required** — projections silently skip non-JSON events
2. **Leader-only execution** — creates IO/CPU imbalance in clusters; follower nodes remain idle for projection work
3. **Never append to projection output streams** — manually writing to `$ce-*`, `$et-*`, or other projection streams will fault the projection
4. **Resetting a projection** soft-deletes its output streams and resets the checkpoint — all output is reprocessed from scratch
5. **Write amplification is real** — plan storage and throughput capacity with projections enabled
6. **`trackemittedstreams` adds significant overhead** — creates an additional tracking event for every emitted event
7. **Configuration changes require STOPPED state** — you cannot modify projection settings while it is running
8. **`processingLag` and `reorderEvents`** are only valid for `fromStreams()` — they have no effect on other selectors
