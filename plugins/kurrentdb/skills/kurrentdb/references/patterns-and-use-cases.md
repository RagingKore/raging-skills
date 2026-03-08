# KurrentDB Patterns and Use Cases

Practical patterns for building event-sourced systems with KurrentDB, including the outbox pattern, polyglot persistence, time travel, and connector integration.

## Table of Contents

- [Event Sourcing Fundamentals](#event-sourcing-fundamentals)
- [Event Design Guidelines](#event-design-guidelines)
- [The Outbox Pattern](#the-outbox-pattern)
- [Polyglot Persistence](#polyglot-persistence)
- [Time Travel](#time-travel)
- [Connector Integration Patterns](#connector-integration-patterns)

---

## Event Sourcing Fundamentals

Event sourcing stores every state change as an immutable event rather than overwriting current state. The event log is the single source of truth.

### Core Concepts

| Concept        | Description                                            |
|----------------|--------------------------------------------------------|
| **Event**      | An immutable fact that something happened (past tense) |
| **Stream**     | An ordered sequence of events grouped by entity        |
| **Read model** | A projection of events into a queryable form           |
| **Projection** | The process of transforming events into read models    |

### Key Benefits

- **Complete audit trail** – Every change is recorded, nothing is lost
- **Temporal queries** – Reconstruct state at any point in time
- **Loose coupling** – Producers and consumers are decoupled through events
- **Time travel** – Replay events to debug, analyze, or rebuild state
- **Expendable read models** – Read models are caches that can be rebuilt from events at any time

### How It Works

```
Command → Validate → Append Event to Stream → Stream is Source of Truth
                                                    ↓
                                            Subscriptions
                                           ↓           ↓
                                     Read Model A   Read Model B
                                     (PostgreSQL)   (Redis Cache)
```

1. A command arrives and is validated against current state
2. If valid, one or more events are appended to the entity's stream
3. The stream is the authoritative source of truth
4. Subscriptions propagate events to read models asynchronously
5. Read models are expendable - delete and rebuild from events anytime

---

## Event Design Guidelines

Well-designed events are critical to a healthy event-sourced system.

### Naming Conventions

Use **past-tense verb phrases** that describe what happened:

| Good                 | Bad               |
|----------------------|-------------------|
| `OrderPlaced`        | `CreateOrder`     |
| `PaymentReceived`    | `ProcessPayment`  |
| `ItemShipped`        | `ShipItem`        |
| `CustomerRegistered` | `NewCustomer`     |
| `InventoryAdjusted`  | `UpdateInventory` |

### Stream Naming

Use **per-entity streams** with a category prefix and entity ID:

```
order-{orderId}        → order-f47ac10b
customer-{customerId}  → customer-550e8400
invoice-{invoiceId}    → invoice-6ba7b810
```

Category projections (using `$ce-` prefix) aggregate all streams of a type:
- `$ce-order` contains all events from all `order-*` streams
- `$ce-customer` contains all events from all `customer-*` streams

### Event Content Guidelines

- Include **enough context** in each event to be independently useful
- Avoid events that require reading other events to be meaningful
- Use `$correlationId` to link events in a workflow chain
- Use `$causationId` to track which event caused another event

```json
{
  "eventType": "OrderPlaced",
  "data": {
    "orderId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    "customerId": "550e8400-e29b-41d4-a716-446655440000",
    "items": [
      { "productId": "prod-001", "name": "Widget", "quantity": 2, "unitPrice": 29.99 }
    ],
    "totalAmount": 59.98,
    "currency": "USD",
    "placedAt": "2026-01-15T10:30:00Z"
  },
  "metadata": {
    "$correlationId": "req-abc-123",
    "$causationId": "cart-checkout-event-id"
  }
}
```

---

## The Outbox Pattern

### The Problem: Dual Writes

In traditional architectures, recording a business change requires writing to both a database and a message queue. This creates the **dual write problem**: if one write succeeds and the other fails, the system is left in an inconsistent state. Two-phase commits are fragile and slow.

```
Traditional (BROKEN):
  Command → Write to DB ──────✓
          → Publish to Queue ──✗  ← System is now inconsistent
```

### KurrentDB Solution: "The Stream IS the Outbox"

KurrentDB solves this by combining the database and message queue into a single construct: the **event stream**. Appending an event to a stream is a single atomic, durable, immediately consistent write. Subscribers then propagate changes asynchronously.

```
KurrentDB (CORRECT):
  Command → Append to Stream ──✓  ← Single atomic write
                ↓
          Subscriptions (async)
          ↓           ↓
    Send Email    Update Cache
```

### Implementation Steps

1. **Record the business change** as an event appended to a stream (single source of truth)
2. **Do NOT update other systems** during the append operation
3. **Set up persistent subscriptions** for asynchronous processing
4. **Process events** in subscription handlers to trigger external actions

### Persistent Subscription with Consumer Groups

Persistent subscriptions provide **competing consumers** – multiple instances process events from the same subscription, with the server distributing events across consumers.

```csharp
// Subscribe to the category projection for all order streams
await using var sub = psClient.SubscribeToStream(
    "$ce-order",      // Category projection stream
    "fulfillment"     // Consumer group name
);

await foreach (var msg in sub.Messages) {
    if (msg is PersistentSubscriptionMessage.Event(var resolvedEvent, _)) {
        try {
            await ProcessOrder(resolvedEvent);
            await sub.Ack(resolvedEvent);
        } catch (TransientException) {
            // Transient error: retry later
            await sub.Nack(
                PersistentSubscriptionNakEventAction.Retry,
                "transient failure",
                resolvedEvent
            );
        } catch (PermanentException ex) {
            // Permanent error: skip and log
            await sub.Nack(
                PersistentSubscriptionNakEventAction.Skip,
                ex.Message,
                resolvedEvent
            );
        }
    }
}
```

### Key Patterns

| Pattern                       | Description                                                                   |
|-------------------------------|-------------------------------------------------------------------------------|
| **Competing consumers**       | Multiple instances share a persistent subscription; server distributes events |
| **Idempotent handlers**       | Handlers must tolerate duplicate delivery (at-least-once guarantee)           |
| **Checkpoint-based recovery** | Server tracks checkpoints; consumers resume from last acknowledged position   |
| **Nack with Retry**           | Return event to the subscription for redelivery (transient errors)            |
| **Nack with Skip**            | Permanently skip a problematic event (permanent/poison errors)                |

### Idempotency Strategies

Since KurrentDB provides **at-least-once delivery**, handlers must be idempotent:

```csharp
// Strategy 1: Check if already processed (using event ID as dedup key)
async Task ProcessOrder(ResolvedEvent resolvedEvent) {
    var eventId = resolvedEvent.Event.EventId;

    if (await _processedEvents.ContainsAsync(eventId))
        return; // Already processed, skip

    await _fulfillmentService.FulfillOrder(resolvedEvent);
    await _processedEvents.MarkProcessedAsync(eventId);
}

// Strategy 2: Use upsert/ON CONFLICT operations (naturally idempotent)
// INSERT INTO orders (...) VALUES (...) ON CONFLICT (id) DO UPDATE SET ...
```

---

## Polyglot Persistence

### The Problem: One Database Cannot Do Everything

A single database cannot optimally serve all access patterns. Relational databases handle joins well but struggle with full-text search. Document stores handle flexible schemas but lack relational integrity. Key-value stores are fast but limited in query capability.

### The Solution: KurrentDB as Central Event Hub

Use KurrentDB as the authoritative event store and project events into purpose-built read stores via catch-up subscriptions.

```
KurrentDB (Source of Truth)
    ↓ Catch-up Subscriptions
    ├──→ PostgreSQL  (relational queries, reporting)
    ├──→ Redis       (fast lookups, sorted sets, caching)
    ├──→ MongoDB     (document queries, web frontends)
    └──→ Elasticsearch (full-text search, analytics)
```

### Implementation Pattern

1. **Capture events** in KurrentDB (single source of truth)
2. **Build read models** via catch-up subscriptions to optimized databases
3. **Real-time synchronization** through continuous subscription processing
4. **Error recovery** by replaying events from the beginning

### Exactly-Once Processing

Achieve **exactly-once semantics** by atomically committing the read model update and the checkpoint in the same database transaction:

```csharp
// PostgreSQL example: atomic update + checkpoint in one transaction
await using var transaction = await connection.BeginTransactionAsync();

// Update the read model
await connection.ExecuteAsync(
    "INSERT INTO order_summary (id, status, total) VALUES (@id, @status, @total) " +
    "ON CONFLICT (id) DO UPDATE SET status=@status, total=@total",
    new { id = evt.OrderId, status = evt.Status, total = evt.Total },
    transaction
);

// Update the checkpoint in the SAME transaction
await connection.ExecuteAsync(
    "INSERT INTO checkpoints (read_model_name, checkpoint) VALUES (@name, @pos) " +
    "ON CONFLICT (read_model_name) DO UPDATE SET checkpoint=@pos",
    new { name = "order_summary", pos = resolvedEvent.OriginalEventNumber },
    transaction
);

await transaction.CommitAsync();
```

### Checkpoint Storage by Database

| Database       | Storage Approach                                                                               |
|----------------|------------------------------------------------------------------------------------------------|
| **PostgreSQL** | `checkpoints` table with `read_model_name` + `checkpoint` columns, updated in same transaction |
| **Redis**      | `StringGet("checkpoint:{model}")` / `StringSet("checkpoint:{model}", position)`                |
| **MongoDB**    | Dedicated checkpoints collection, updated in same session/transaction                          |

### Example: Multi-Store Projection

```csharp
// Subscribe to all order events from the beginning
var subscription = client.SubscribeToStream(
    "$ce-order",
    FromStream.Start  // Or resume from last checkpoint
);

await foreach (var resolvedEvent in subscription.Messages) {
    var @event = Deserialize(resolvedEvent);

    // Project to PostgreSQL (relational queries)
    await _postgresProjection.Apply(@event, resolvedEvent.OriginalEventNumber);

    // Project to Redis (fast cache)
    await _redisProjection.Apply(@event, resolvedEvent.OriginalEventNumber);

    // Project to Elasticsearch (search)
    await _searchProjection.Apply(@event, resolvedEvent.OriginalEventNumber);
}
```

---

## Time Travel

### The Problem: Traditional Databases Only Store Current State

Traditional databases overwrite previous values on every update. Once data is changed, the previous state is lost. This makes historical analysis, debugging, and auditing difficult or impossible.

### The Solution: Immutable Event History

Since KurrentDB stores every change as an immutable event, you can reconstruct the state of any entity at any point in time.

### Approach 1: Pre-Computed Snapshots

Project events into **denormalized snapshot tables** at regular intervals (daily, monthly, etc.). Fast queries with configurable granularity.

```csharp
// Build daily snapshots from events
async Task BuildDailySnapshot(DateTime date) {
    var events = await ReadEventsUpTo(date.AddDays(1).AddTicks(-1));

    var snapshot = new DailySnapshot {
        Date = date,
        TotalOrders = events.Count(e => e.Type == "OrderPlaced"),
        TotalRevenue = events
            .Where(e => e.Type == "PaymentReceived")
            .Sum(e => e.Data.Amount),
        ActiveCustomers = events
            .Where(e => e.Type == "OrderPlaced")
            .Select(e => e.Data.CustomerId)
            .Distinct()
            .Count()
    };

    await _snapshotStore.SaveAsync(snapshot);
}
```

**Advantages:** Fast queries, pre-computed aggregations, configurable time granularity.

**Trade-offs:** Storage overhead, granularity limited to snapshot intervals, must rebuild if schema changes.

### Approach 2: On-Demand Replay

Read events up to a specific position or timestamp and construct the read model on-the-fly.

```csharp
// Reconstruct order state at a specific point in time
async Task<OrderState> GetOrderStateAt(string orderId, DateTime pointInTime) {
    var events = client.ReadStreamAsync(
        Direction.Forwards,
        $"order-{orderId}",
        StreamPosition.Start
    );

    var state = new OrderState();

    await foreach (var resolvedEvent in events) {
        // Stop when we've passed the target time
        if (resolvedEvent.Event.Created > pointInTime)
            break;

        state.Apply(Deserialize(resolvedEvent));
    }

    return state;
}

// Usage: "What did order X look like on January 15th?"
var historicState = await GetOrderStateAt(
    "f47ac10b",
    new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
);
```

**Advantages:** Always current, simpler architecture, no additional storage.

**Trade-offs:** Slower for large streams (must replay all events), not suitable for high-frequency queries.

### Choosing an Approach

| Factor      | Pre-Computed Snapshots      | On-Demand Replay                    |
|-------------|-----------------------------|-------------------------------------|
| Query speed | Fast (pre-computed)         | Slower (replay required)            |
| Storage     | Additional snapshot storage | No extra storage                    |
| Granularity | Fixed intervals             | Any point in time                   |
| Freshness   | Depends on build frequency  | Always current                      |
| Best for    | Dashboards, reporting       | Debugging, auditing, ad-hoc queries |

---

## Connector Integration Patterns

KurrentDB connectors enable event-driven integration with external systems without writing custom subscription code.

### Webhook Integration (HTTP Sink)

Push events to external HTTP APIs as webhooks:

```json
{
  "instanceTypeName": "http-sink",
  "settings": {
    "url": "https://api.partner.com/webhooks/orders/{event-type}",
    "authentication:scheme": "Bearer",
    "authentication:token": "webhook-secret",
    "subscription:filter:scope": "stream",
    "subscription:filter:filterType": "prefix",
    "subscription:filter:expression": "order-",
    "transformer:enabled": true,
    "transformer:function": "<base64: strip internal fields, add webhook metadata>"
  }
}
```

### Event Bus Bridging (Kafka Sink)

Bridge KurrentDB events to a Kafka event bus for broader consumption:

```json
{
  "instanceTypeName": "kafka-sink",
  "settings": {
    "topic": "domain-events",
    "bootstrapServers": "kafka:9092",
    "partitionKeyStrategy": "stream",
    "compression": "Zstd",
    "subscription:filter:scope": "stream",
    "subscription:filter:filterType": "prefix",
    "subscription:filter:expression": "order-,payment-,shipping-"
  }
}
```

### Read Model Projection (MongoDB/SQL Sinks)

Project events directly into read store databases:

```json
{
  "instanceTypeName": "mongo-db-sink",
  "settings": {
    "database": "read-models",
    "collection": "order-summaries",
    "connectionString": "mongodb://localhost:27017",
    "documentIdStrategy": "streamSuffix",
    "subscription:filter:scope": "stream",
    "subscription:filter:filterType": "prefix",
    "subscription:filter:expression": "order-",
    "transformer:enabled": true,
    "transformer:function": "<base64: reshape event into read model document>"
  }
}
```

### Structured Logging and Debugging (Serilog Sink)

Capture events for structured logging and debugging during development:

```json
{
  "instanceTypeName": "serilog-sink",
  "settings": {
    "includeRecordData": true,
    "subscription:filter:scope": "stream",
    "subscription:filter:filterType": "prefix",
    "subscription:filter:expression": "order-"
  }
}
```

### Pattern Selection Guide

| Use Case                     | Recommended Connector | Notes                                     |
|------------------------------|-----------------------|-------------------------------------------|
| Notify external services     | HTTP Sink             | Simple webhooks, no infrastructure needed |
| Distribute to many consumers | Kafka Sink            | When multiple teams consume events        |
| Build read models            | MongoDB/SQL Sink      | Direct projection, no custom code         |
| Full-text search             | Elasticsearch Sink    | Index events for search queries           |
| Message queuing              | RabbitMQ Sink         | When consumers need queue semantics       |
| Debugging / monitoring       | Serilog Sink          | Free, zero-config event inspection        |
| Ingest external events       | Kafka Source          | Bring Kafka events into KurrentDB         |
