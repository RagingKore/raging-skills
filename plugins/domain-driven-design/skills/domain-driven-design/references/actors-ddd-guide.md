# Actors and Domain-Driven Design: A Perfect Match

Deep dive into why the Actor Model is the ideal runtime for DDD systems. Covers aggregate-per-actor patterns, distributed consistency, read-side actors, and practical implementation across frameworks.

## Table of Contents

1. [Why Actors Are Perfect for DDD](#why-actors-are-perfect-for-ddd)
2. [Core Concepts Alignment](#core-concepts-alignment)
3. [Write Side: Aggregates as Actors](#write-side-aggregates-as-actors)
4. [Read Side: Projections as Actors](#read-side-projections-as-actors)
5. [Event Sourcing with Actors](#event-sourcing-with-actors)
6. [Distributed Patterns](#distributed-patterns)
7. [Framework Comparison](#framework-comparison)
8. [Implementation Patterns](#implementation-patterns)
9. [Anti-Patterns to Avoid](#anti-patterns-to-avoid)

---

## Why Actors Are Perfect for DDD

### The Natural Alignment

The Actor Model and DDD share fundamental principles that make them naturally complementary:

```
┌─────────────────────────────────────────────────────────────────────┐
│                    CONCEPTUAL ALIGNMENT                              │
├─────────────────────────────────┬───────────────────────────────────┤
│         DDD CONCEPT             │        ACTOR MODEL                │
├─────────────────────────────────┼───────────────────────────────────┤
│ Aggregate                       │ Actor                             │
│ Aggregate boundary              │ Actor encapsulation               │
│ Single transaction per aggregate│ Actor processes one msg at a time │
│ Aggregate ID                    │ Actor address/reference           │
│ Domain events                   │ Messages                          │
│ Eventual consistency            │ Async message passing             │
│ Bounded context                 │ Actor system / cluster partition  │
└─────────────────────────────────┴───────────────────────────────────┘
```

### The Aggregate-Actor Equivalence

**DDD Rule**: One aggregate instance should be modified by one transaction at a time.

**Actor Property**: An actor processes one message at a time (sequential mailbox).

This is the same constraint expressed differently. Actors **enforce** what DDD **prescribes**.

```
┌─────────────────────────────────────────────────────────────────────┐
│                    AGGREGATE AS ACTOR                                │
│                                                                     │
│    ┌─────────────────────────────────────────────────────────┐     │
│    │                    Order Actor                           │     │
│    │                                                         │     │
│    │  ┌─────────┐    ┌─────────────┐    ┌─────────────┐     │     │
│    │  │ Mailbox │───▶│   State     │───▶│   Events    │     │     │
│    │  │(Commands)│   │ (Aggregate) │    │  (Output)   │     │     │
│    │  └─────────┘    └─────────────┘    └─────────────┘     │     │
│    │       │                                   │             │     │
│    │       │         Sequential               │             │     │
│    │       └──────── Processing ──────────────┘             │     │
│    │                                                         │     │
│    └─────────────────────────────────────────────────────────┘     │
│                                                                     │
│    Invariants GUARANTEED by actor model:                            │
│    • One command at a time (no concurrent mutations)                │
│    • State is never shared (encapsulation)                          │
│    • Communication only via messages (events)                       │
└─────────────────────────────────────────────────────────────────────┘
```

### Benefits Over Traditional Approaches

| Challenge | Traditional Approach | Actor Approach |
|-----------|---------------------|----------------|
| **Concurrency** | Locks, transactions, retries | Built-in single-threaded processing |
| **Distribution** | Complex coordination | Location transparency |
| **Scaling** | Database bottleneck | Actors distribute across nodes |
| **Recovery** | Manual state reconstruction | Actor supervision, persistence |
| **Isolation** | Hope nothing leaks | Physical memory isolation |

### The Key Insight

> "Every sufficiently complex distributed system eventually reinvents the actor model."

DDD's aggregates are essentially actors without a runtime. When you add an actor runtime, you get:

1. **Automatic concurrency control** - No more optimistic locking retries
2. **Location transparency** - Aggregate can live anywhere in cluster
3. **Lifecycle management** - Actors activate/passivate automatically
4. **Supervision** - Failed aggregates restart cleanly
5. **Natural event flow** - Events are just messages to other actors

---

## Core Concepts Alignment

### Aggregate Identity = Actor Address

```
DDD:    OrderId("order-12345")
Actor:  ActorRef<OrderActor>("order-12345")

Both uniquely identify an instance.
Both are used to route commands/messages.
Both are stable across time.
```

### Aggregate Boundary = Actor Boundary

```
┌────────────────────────────────────────────────────────────────────┐
│                        BOUNDARY ENFORCEMENT                         │
│                                                                    │
│  DDD Says:                          Actor Guarantees:              │
│  ─────────                          ──────────────────             │
│  "Don't reference other             Messages are the ONLY way      │
│   aggregates directly"              actors communicate             │
│                                                                    │
│  "Modify only one aggregate         Actor processes ONE message    │
│   per transaction"                  at a time                      │
│                                                                    │
│  "Keep aggregates small"            Small actors = fast messages   │
│                                     = better throughput            │
│                                                                    │
│  "Use eventual consistency          Async messaging IS eventual    │
│   between aggregates"               consistency                    │
└────────────────────────────────────────────────────────────────────┘
```

### Domain Events = Actor Messages

In DDD, aggregates communicate via domain events. In actors, everything is messages. These are the same thing:

```
                  DDD View                    Actor View
                  ────────                    ──────────

    ┌─────────┐              ┌─────────┐
    │  Order  │              │ Order   │
    │Aggregate│              │  Actor  │
    └────┬────┘              └────┬────┘
         │                        │
         │ OrderShipped           │ OrderShipped (message)
         │ (domain event)         │
         ▼                        ▼
    ┌─────────┐              ┌─────────┐
    │Shipping │              │Shipping │
    │Aggregate│              │  Actor  │
    └─────────┘              └─────────┘

    Same concept, different vocabulary
```

---

## Write Side: Aggregates as Actors

### Pattern 1: Actor Wrapping OOP Aggregate

Keep your existing aggregate, wrap it in an actor:

```
┌─────────────────────────────────────────────────────────────────────┐
│                    OOP AGGREGATE IN ACTOR                           │
│                                                                     │
│   ┌────────────────────────────────────────────────────────────┐   │
│   │                     OrderActor                              │   │
│   │                                                            │   │
│   │   ┌──────────────────────────────────────────────────┐    │   │
│   │   │           Order (OOP Aggregate)                   │    │   │
│   │   │                                                  │    │   │
│   │   │  - state fields                                  │    │   │
│   │   │  - behavior methods                              │    │   │
│   │   │  - domain events list                            │    │   │
│   │   └──────────────────────────────────────────────────┘    │   │
│   │                                                            │   │
│   │   Actor Responsibilities:                                  │   │
│   │   1. Receive commands                                      │   │
│   │   2. Delegate to aggregate methods                         │   │
│   │   3. Persist events (if event sourced)                     │   │
│   │   4. Publish events to other actors                        │   │
│   │   5. Reply to sender                                       │   │
│   │                                                            │   │
│   └────────────────────────────────────────────────────────────┘   │
│                                                                     │
│   Benefits:                                                         │
│   • Reuse existing domain model                                     │
│   • Gradual migration path                                          │
│   • Familiar patterns                                               │
│                                                                     │
│   Drawbacks:                                                        │
│   • Two layers of abstraction                                       │
│   • Mutable state inside actor                                      │
└─────────────────────────────────────────────────────────────────────┘
```

### Pattern 2: Actor with Decider (Recommended)

Combine Actor + Decider for cleanest architecture:

```
┌─────────────────────────────────────────────────────────────────────┐
│                    DECIDER PATTERN IN ACTOR                         │
│                                                                     │
│   ┌────────────────────────────────────────────────────────────┐   │
│   │                     OrderActor                              │   │
│   │                                                            │   │
│   │   State: OrderState (immutable)                            │   │
│   │   Decider: OrderDecider (stateless, pure functions)        │   │
│   │                                                            │   │
│   │   on_command(cmd):                                         │   │
│   │       events = decider.decide(cmd, state)                  │   │
│   │       for event in events:                                 │   │
│   │           persist(event)                                   │   │
│   │           state = decider.evolve(state, event)             │   │
│   │           publish(event)                                   │   │
│   │       reply(Success)                                       │   │
│   │                                                            │   │
│   │   on_recovery(event):                                      │   │
│   │       state = decider.evolve(state, event)                 │   │
│   │                                                            │   │
│   └────────────────────────────────────────────────────────────┘   │
│                                                                     │
│   Benefits:                                                         │
│   • Pure business logic in Decider (testable)                      │
│   • Actor handles infrastructure (persistence, messaging)          │
│   • Clear separation of concerns                                    │
│   • Natural fit for event sourcing                                  │
│   • State always consistent (fold over events)                      │
└─────────────────────────────────────────────────────────────────────┘
```

### Pattern 3: Virtual Actors (Cluster Sharding)

Actors that auto-activate on first message:

```
┌─────────────────────────────────────────────────────────────────────┐
│                    VIRTUAL ACTOR PATTERN                            │
│                                                                     │
│   Traditional Actors:                                               │
│   1. Explicitly create actor                                        │
│   2. Get reference                                                  │
│   3. Send message                                                   │
│                                                                     │
│   Virtual Actors:                                                   │
│   1. Get reference by ID (actor may not exist yet)                 │
│   2. Send message                                                   │
│   3. Runtime activates actor if needed                              │
│   4. Runtime passivates idle actors                                 │
│                                                                     │
│   ┌─────────────────────────────────────────────────────────────┐  │
│   │                                                             │  │
│   │    Client                     Actor Runtime                 │  │
│   │    ──────                     ─────────────                 │  │
│   │                                                             │  │
│   │    GetGrain<IOrder>("123")    Check: Is order-123 active?  │  │
│   │           │                          │                      │  │
│   │           │                   No ───▶│ Activate             │  │
│   │           │                          │ Load state           │  │
│   │           │                          │ Ready                │  │
│   │           │                          │                      │  │
│   │           └──── AddLine() ──────────▶│ Process              │  │
│   │                                      │                      │  │
│   │    (Later, after idle timeout)       │                      │  │
│   │                                      │ Passivate            │  │
│   │                                      │ Save state           │  │
│   │                                      │ Free memory          │  │
│   │                                                             │  │
│   └─────────────────────────────────────────────────────────────┘  │
│                                                                     │
│   Perfect for DDD because:                                          │
│   • Aggregate ID = Actor ID (natural mapping)                       │
│   • Millions of aggregates without millions of active actors        │
│   • State automatically persisted                                   │
│   • Distribution handled by runtime                                 │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Read Side: Projections as Actors

### Why Actors for Read Models?

Read models (projections) in CQRS consume events and update denormalized views. Actors excel here:

```
┌─────────────────────────────────────────────────────────────────────┐
│                    READ SIDE ACTOR ARCHITECTURE                     │
│                                                                     │
│   Event Stream                                                      │
│       │                                                             │
│       ▼                                                             │
│   ┌────────────────────────────────────────────────────────────┐   │
│   │              Event Dispatcher Actor                         │   │
│   │                                                            │   │
│   │   Routes events to appropriate projection actors           │   │
│   └───────────────────────┬────────────────────────────────────┘   │
│                           │                                         │
│           ┌───────────────┼───────────────┐                        │
│           │               │               │                        │
│           ▼               ▼               ▼                        │
│   ┌──────────────┐ ┌──────────────┐ ┌──────────────┐              │
│   │   Orders     │ │  Customer    │ │   Reports    │              │
│   │   Summary    │ │   Orders     │ │   Dashboard  │              │
│   │   Projector  │ │   Projector  │ │   Projector  │              │
│   │   Actor      │ │   Actor      │ │   Actor      │              │
│   └──────┬───────┘ └──────┬───────┘ └──────┬───────┘              │
│          │                │                │                        │
│          ▼                ▼                ▼                        │
│   ┌──────────────┐ ┌──────────────┐ ┌──────────────┐              │
│   │   Postgres   │ │    Redis     │ │ Elasticsearch│              │
│   │   Table      │ │    Cache     │ │    Index     │              │
│   └──────────────┘ └──────────────┘ └──────────────┘              │
│                                                                     │
│   Benefits:                                                         │
│   • Each projector processes events sequentially (ordering)        │
│   • Failure isolation (one projector crash doesn't affect others)  │
│   • Easy to add new projections                                    │
│   • Backpressure handled by mailbox                                │
│   • Can replay from event store                                    │
└─────────────────────────────────────────────────────────────────────┘
```

### Projection Actor Patterns

#### Single Projector per Read Model

```
┌────────────────────────────────────────────────────────────────────┐
│                    SINGLE PROJECTOR PATTERN                         │
│                                                                    │
│   One actor instance handles all events for one read model         │
│                                                                    │
│   Pros:                                                            │
│   • Simple ordering (events processed in sequence)                 │
│   • Single writer to database (no conflicts)                       │
│   • Easy to reason about                                           │
│                                                                    │
│   Cons:                                                            │
│   • Can become bottleneck                                          │
│   • No parallelism                                                 │
│                                                                    │
│   Best for:                                                        │
│   • Small/medium event volumes                                     │
│   • Read models that need strict ordering                          │
└────────────────────────────────────────────────────────────────────┘
```

#### Partitioned Projectors

```
┌────────────────────────────────────────────────────────────────────┐
│                    PARTITIONED PROJECTOR PATTERN                    │
│                                                                    │
│   Multiple actors, each handling a partition of events             │
│                                                                    │
│   Event Stream                                                     │
│       │                                                            │
│       ▼                                                            │
│   ┌────────────────────────────────────────────────┐              │
│   │            Router (by aggregate ID)            │              │
│   └───────┬───────────────┬───────────────┬───────┘              │
│           │               │               │                        │
│           ▼               ▼               ▼                        │
│   ┌──────────────┐ ┌──────────────┐ ┌──────────────┐              │
│   │ Projector    │ │ Projector    │ │ Projector    │              │
│   │ (A-H)        │ │ (I-P)        │ │ (Q-Z)        │              │
│   └──────────────┘ └──────────────┘ └──────────────┘              │
│                                                                    │
│   Pros:                                                            │
│   • Parallel processing                                            │
│   • Scales with load                                               │
│   • Maintains per-aggregate ordering                               │
│                                                                    │
│   Cons:                                                            │
│   • More complex                                                   │
│   • Cross-aggregate queries need coordination                      │
│                                                                    │
│   Best for:                                                        │
│   • High event volumes                                             │
│   • When ordering only matters within aggregate                    │
└────────────────────────────────────────────────────────────────────┘
```

---

## Event Sourcing with Actors

### Actor Persistence

Most actor frameworks support persistence. Combined with event sourcing:

```
┌─────────────────────────────────────────────────────────────────────┐
│                    EVENT SOURCED ACTOR                              │
│                                                                     │
│   ┌────────────────────────────────────────────────────────────┐   │
│   │                     OrderActor                              │   │
│   │                                                            │   │
│   │   State: OrderState                                        │   │
│   │   Events: List<OrderEvent>                                 │   │
│   │   LastSequenceNr: long                                     │   │
│   │                                                            │   │
│   │   ┌────────────────────────────────────────────────────┐  │   │
│   │   │              Command Processing                     │  │   │
│   │   │                                                    │  │   │
│   │   │   1. Receive command                               │  │   │
│   │   │   2. Validate against state (decide)               │  │   │
│   │   │   3. Generate events                               │  │   │
│   │   │   4. Persist events to journal                     │  │   │
│   │   │   5. Update state (evolve)                         │  │   │
│   │   │   6. Publish events                                │  │   │
│   │   │   7. Reply success                                 │  │   │
│   │   └────────────────────────────────────────────────────┘  │   │
│   │                                                            │   │
│   │   ┌────────────────────────────────────────────────────┐  │   │
│   │   │              Recovery (on actor start)              │  │   │
│   │   │                                                    │  │   │
│   │   │   1. Read events from journal                      │  │   │
│   │   │   2. Replay through evolve function                │  │   │
│   │   │   3. State = fold(events, initial)                 │  │   │
│   │   │   4. Ready to process new commands                 │  │   │
│   │   └────────────────────────────────────────────────────┘  │   │
│   │                                                            │   │
│   └────────────────────────────────────────────────────────────┘   │
│                                                                     │
│   Journal (Event Store):                                            │
│   ┌────────────────────────────────────────────────────────────┐   │
│   │ order-123 │ seq │ event                                    │   │
│   │───────────┼─────┼──────────────────────────────────────────│   │
│   │ order-123 │  1  │ OrderCreated { customerId: "c1" }        │   │
│   │ order-123 │  2  │ LineAdded { productId: "p1", qty: 2 }    │   │
│   │ order-123 │  3  │ OrderConfirmed { }                       │   │
│   └────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

### Snapshotting

For long-lived aggregates, avoid replaying all events:

```
┌─────────────────────────────────────────────────────────────────────┐
│                    SNAPSHOTTING STRATEGY                            │
│                                                                     │
│   Journal:                                                          │
│   [E1][E2][E3][E4][E5][E6][E7][E8][E9][E10][E11][E12][E13]...      │
│                          ↑                    ↑                     │
│                     Snapshot @5          Snapshot @10               │
│                                                                     │
│   Recovery without snapshots:                                       │
│   • Replay E1→E13 (slow for long streams)                          │
│                                                                     │
│   Recovery with snapshots:                                          │
│   • Load snapshot @10                                               │
│   • Replay E11→E13 only                                            │
│                                                                     │
│   Snapshot triggers:                                                │
│   • Every N events (e.g., every 100)                               │
│   • After certain commands (e.g., OrderShipped)                    │
│   • Periodic (e.g., every hour if changed)                         │
│                                                                     │
│   Implementation:                                                   │
│   • Snapshot is serialized state at a point in time                │
│   • Stored separately from events                                  │
│   • Old snapshots can be deleted (events are source of truth)      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Distributed Patterns

### Cluster Sharding (Aggregate Distribution)

```
┌─────────────────────────────────────────────────────────────────────┐
│                    CLUSTER SHARDING                                 │
│                                                                     │
│   Problem: Millions of aggregates, limited nodes                    │
│   Solution: Distribute aggregates across cluster by ID              │
│                                                                     │
│   ┌─────────────────────────────────────────────────────────────┐  │
│   │                     Shard Coordinator                        │  │
│   │                                                             │  │
│   │   OrderId → Shard → Node                                    │  │
│   │   hash(order-123) % 100 = Shard-45 → Node-2                 │  │
│   │                                                             │  │
│   └─────────────────────────────────────────────────────────────┘  │
│                                                                     │
│   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐            │
│   │    Node 1    │  │    Node 2    │  │    Node 3    │            │
│   │              │  │              │  │              │            │
│   │ Shards 0-33  │  │ Shards 34-66 │  │ Shards 67-99 │            │
│   │              │  │              │  │              │            │
│   │ order-456    │  │ order-123    │  │ order-789    │            │
│   │ order-111    │  │ order-222    │  │ order-333    │            │
│   │ ...          │  │ ...          │  │ ...          │            │
│   └──────────────┘  └──────────────┘  └──────────────┘            │
│                                                                     │
│   When Node 2 fails:                                                │
│   • Coordinator detects failure                                     │
│   • Redistributes Shards 34-66 to remaining nodes                  │
│   • Actors recover from persistence                                 │
│   • Clients automatically routed to new locations                  │
│                                                                     │
│   Benefits:                                                         │
│   • Location transparency (clients don't know where actors are)    │
│   • Automatic rebalancing                                          │
│   • Fault tolerance                                                │
│   • Horizontal scaling                                             │
└─────────────────────────────────────────────────────────────────────┘
```

### Saga Pattern with Actors

Coordinate multi-aggregate operations:

```
┌─────────────────────────────────────────────────────────────────────┐
│                    SAGA AS ACTOR                                    │
│                                                                     │
│   Use Case: Order fulfillment (Order + Inventory + Payment)        │
│                                                                     │
│   ┌────────────────────────────────────────────────────────────┐   │
│   │                  OrderFulfillmentSaga                        │   │
│   │                                                            │   │
│   │   State: { step: "created", orderId, paymentId, etc }      │   │
│   │                                                            │   │
│   │   Steps:                                                   │   │
│   │   1. OrderCreated → Reserve Inventory                      │   │
│   │   2. InventoryReserved → Process Payment                   │   │
│   │   3. PaymentProcessed → Confirm Order                      │   │
│   │   4. OrderConfirmed → Saga Complete                        │   │
│   │                                                            │   │
│   │   Compensations (on failure):                              │   │
│   │   - PaymentFailed → Release Inventory                      │   │
│   │   - InventoryUnavailable → Cancel Order                    │   │
│   │                                                            │   │
│   └────────────────────────────────────────────────────────────┘   │
│                                                                     │
│            OrderCreated                                             │
│                 │                                                   │
│                 ▼                                                   │
│   ┌──────────────────────┐                                         │
│   │   Inventory Actor    │──── Reserved ────┐                      │
│   └──────────────────────┘                  │                      │
│                                              ▼                      │
│                                   ┌──────────────────────┐         │
│                                   │   Payment Actor      │         │
│                                   └──────────┬───────────┘         │
│                                              │                      │
│                    Processed ───────────────┘                      │
│                        │                                            │
│                        ▼                                            │
│   ┌──────────────────────┐                                         │
│   │    Order Actor       │──── Confirmed                           │
│   └──────────────────────┘                                         │
│                                                                     │
│   Saga actor maintains state and orchestrates the flow             │
│   Each step is an actor message, failures trigger compensations    │
└─────────────────────────────────────────────────────────────────────┘
```

### Cross-Context Communication

```
┌─────────────────────────────────────────────────────────────────────┐
│                    BOUNDED CONTEXT INTEGRATION                      │
│                                                                     │
│   ┌────────────────────────┐      ┌────────────────────────┐      │
│   │    ORDERS CONTEXT      │      │   SHIPPING CONTEXT     │      │
│   │                        │      │                        │      │
│   │  ┌──────────────────┐ │      │ ┌──────────────────┐   │      │
│   │  │   Order Actors   │ │      │ │  Shipment Actors │   │      │
│   │  └────────┬─────────┘ │      │ └────────┬─────────┘   │      │
│   │           │           │      │          │             │      │
│   │           ▼           │      │          │             │      │
│   │  ┌──────────────────┐ │      │          │             │      │
│   │  │ Event Publisher  │─┼──────┼─────────▶│             │      │
│   │  └──────────────────┘ │      │          │             │      │
│   │                        │      │ ┌────────▼─────────┐   │      │
│   │                        │      │ │Anti-Corruption   │   │      │
│   │                        │      │ │Layer Actor       │   │      │
│   │                        │      │ │                  │   │      │
│   │                        │      │ │ Translates:      │   │      │
│   │                        │      │ │ OrderShipped →   │   │      │
│   │                        │      │ │ CreateShipment   │   │      │
│   │                        │      │ └──────────────────┘   │      │
│   │                        │      │                        │      │
│   └────────────────────────┘      └────────────────────────┘      │
│                                                                     │
│   Benefits:                                                         │
│   • Contexts remain independent                                    │
│   • ACL handles translation                                        │
│   • Event-driven integration                                       │
│   • Each context has its own actor system (optionally)             │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Framework Comparison

### .NET Frameworks

| Framework | Type | Best For | Event Sourcing | Clustering |
|-----------|------|----------|----------------|------------|
| **Akka.NET** | Traditional + Cluster Sharding | Complex distributed systems | Akka.Persistence | Yes |
| **Proto.Actor** | Traditional | High performance, cross-platform | Plugin | Yes |

### Python Frameworks

| Framework | Type | Best For | Persistence | Distribution |
|-----------|------|----------|-------------|--------------|
| **Ray** | Task-based | ML/Data, distributed compute | Checkpointing | Yes (Ray Cluster) |
| **Thespian** | Traditional | General purpose | Manual | Yes |
| **Pykka** | Threading | Simple local actors | Manual | No |
| **Dramatiq** | Task queue | Background jobs | Redis/RabbitMQ | Yes |

### Selection Guide

```
Need ML/data processing + DDD?
└── Ray (Python) - Distributed compute with actors

Need cloud-native, auto-scaling with virtual actors?
└── Akka.NET with Cluster Sharding

Need fine-grained control, complex patterns?
└── Akka.NET (.NET) or Akka (JVM)

Need maximum performance?
└── Proto.Actor

Need simplicity, local-only?
└── Pykka (Python) or in-memory actors
```

---

## Implementation Patterns

### Aggregate Actor Template (Akka.NET)

```csharp
public class AggregateActor<TState, TCommand, TEvent> : ReceivePersistentActor
    where TEvent : class
{
    private readonly IDecider<TState, TCommand, TEvent> _decider;
    private TState _state;

    public override string PersistenceId { get; }

    public AggregateActor(string id, IDecider<TState, TCommand, TEvent> decider)
    {
        PersistenceId = id;
        _decider = decider;
        _state = decider.InitialState;

        Command<TCommand>(HandleCommand);
        Command<GetState>(_ => Sender.Tell(_state));

        Recover<TEvent>(evt => _state = _decider.Evolve(_state, evt));
        Recover<SnapshotOffer>(offer => {
            if (offer.Snapshot is TState snapshot)
                _state = snapshot;
        });
    }

    private void HandleCommand(TCommand cmd)
    {
        try
        {
            var events = _decider.Decide(cmd, _state).ToList();

            if (!events.Any())
            {
                Sender.Tell(CommandResult.Success());
                return;
            }

            PersistAll(events, evt =>
            {
                _state = _decider.Evolve(_state, evt);
                Context.System.EventStream.Publish(evt);

                if (LastSequenceNr % 100 == 0)
                    SaveSnapshot(_state);
            });

            DeferAsync("complete", _ => Sender.Tell(CommandResult.Success(events)));
        }
        catch (Exception ex)
        {
            Sender.Tell(CommandResult.Failure(ex.Message));
        }
    }
}
```

### Cluster Sharding Template (Akka.NET)

Cluster Sharding provides virtual actor semantics in Akka.NET:

```csharp
// Message envelope for routing to sharded actors
public sealed record OrderEnvelope(string OrderId, OrderCommand Command);

// Entity actor (one per aggregate)
public class OrderShardEntity : ReceivePersistentActor
{
    public static Props Props(string entityId) => 
        Akka.Actor.Props.Create(() => new OrderShardEntity(entityId));

    private readonly string _entityId;
    private readonly OrderDecider _decider = new();
    private OrderState _state = OrderDecider.InitialState;

    public override string PersistenceId => $"order-{_entityId}";

    public OrderShardEntity(string entityId)
    {
        _entityId = entityId;
        
        Recover<OrderEvent>(evt => _state = _decider.Evolve(_state, evt));
        Recover<SnapshotOffer>(offer => _state = (OrderState)offer.Snapshot);
        
        Command<OrderCommand>(HandleCommand);
        Command<GetState>(_ => Sender.Tell(_state));
    }

    private void HandleCommand(OrderCommand command)
    {
        try
        {
            var events = _decider.Decide(command, _state).ToList();
            
            PersistAll(events, evt =>
            {
                _state = _decider.Evolve(_state, evt);
                
                if (LastSequenceNr % 100 == 0)
                    SaveSnapshot(_state);
            });
            
            Sender.Tell(CommandResult.Success(events));
        }
        catch (Exception ex)
        {
            Sender.Tell(CommandResult.Failure(ex.Message));
        }
    }
}

// Configure cluster sharding
public static class OrderSharding
{
    public static void Start(ActorSystem system)
    {
        var sharding = ClusterSharding.Get(system);
        
        sharding.Start(
            typeName: "Order",
            entityPropsFactory: entityId => OrderShardEntity.Props(entityId),
            settings: ClusterShardingSettings.Create(system),
            messageExtractor: new OrderMessageExtractor()
        );
    }
    
    public static IActorRef ShardRegion(ActorSystem system) =>
        ClusterSharding.Get(system).ShardRegion("Order");
}

// Message extractor for routing
public class OrderMessageExtractor : HashCodeMessageExtractor
{
    public OrderMessageExtractor() : base(maxNumberOfShards: 100) { }

    public override string EntityId(object message) => message switch
    {
        OrderEnvelope env => env.OrderId,
        _ => null!
    };

    public override object EntityMessage(object message) => message switch
    {
        OrderEnvelope env => env.Command,
        _ => message
    };
}

// Usage
var shardRegion = OrderSharding.ShardRegion(system);
var result = await shardRegion.Ask<CommandResult>(
    new OrderEnvelope("order-123", new CreateOrder("customer-1"))
);
```

---

## Anti-Patterns to Avoid

### 1. Actor-per-Entity (Instead of per-Aggregate)

```
❌ WRONG: Actor for each entity
   OrderActor, OrderLineActor, CustomerActor all separate

   Problems:
   • No consistency boundary
   • Complex coordination
   • Violates aggregate rules

✅ RIGHT: Actor for aggregate root only
   OrderActor contains OrderLines internally

   Benefits:
   • Consistency guaranteed
   • Simpler model
   • Matches DDD
```

### 2. Synchronous Request-Response Everywhere

```
❌ WRONG: Always ask and wait for response
   var result = await orderActor.Ask<Result>(command);
   var state = await orderActor.Ask<State>(GetState);

   Problems:
   • Creates hidden coupling
   • Defeats async benefits
   • Can cause deadlocks

✅ RIGHT: Fire-and-forget + events
   orderActor.Tell(command);
   // Read side updated via events

   Benefits:
   • True async
   • Decoupled
   • Scalable
```

### 3. Fat Actors (Too Much Responsibility)

```
❌ WRONG: Actor that does everything
   OrderActor that handles:
   - Business logic
   - Database access
   - Email sending
   - PDF generation

   Problems:
   • Violates SRP
   • Hard to test
   • Slow message processing

✅ RIGHT: Focused actors + delegation
   OrderActor: business logic only
   EmailActor: sends emails
   PdfActor: generates PDFs

   OrderActor → emits OrderConfirmed →
   EmailActor (subscribed) → sends email
```

### 4. Sharing Mutable State Between Actors

```
❌ WRONG: Passing mutable objects in messages
   actor.Tell(new ProcessOrder(mutableOrderObject));

   Problems:
   • Breaks actor isolation
   • Race conditions
   • Hard to debug

✅ RIGHT: Immutable messages only
   actor.Tell(new ProcessOrder(orderId, items.ToImmutableList()));

   Benefits:
   • True isolation
   • Safe concurrency
   • Predictable behavior
```

### 5. Blocking Inside Actors

```
❌ WRONG: Blocking calls in message handler
   public void Handle(ProcessPayment cmd)
   {
       var result = httpClient.Post(...).Result;  // BLOCKS!
       // ...
   }

   Problems:
   • Blocks thread pool
   • Reduces throughput
   • Can deadlock

✅ RIGHT: Async all the way or pipe to self
   public async Task Handle(ProcessPayment cmd)
   {
       var result = await httpClient.PostAsync(...);
       // ...
   }
   // OR
   public void Handle(ProcessPayment cmd)
   {
       httpClient.PostAsync(...).PipeTo(Self);
   }
```

---

## Summary: When to Use Actors for DDD

### Use Actors When:

- Building distributed systems
- High concurrency on aggregates
- Event sourcing is desired
- Auto-scaling needed
- Complex saga/process managers
- Cross-service coordination

### Consider Alternatives When:

- Simple CRUD application
- Single database, single server
- Team unfamiliar with actors
- Very low throughput requirements

### The Golden Rule

> If you're doing DDD with event sourcing in a distributed system,
> actors should be your default choice for the runtime.
> They enforce the constraints DDD prescribes.
