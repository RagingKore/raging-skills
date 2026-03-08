# DCB Research Report

Original research compiled January 2026 covering Dynamic Consistency Boundary theory, ecosystem, and community.

## Table of Contents

1. [Overview](#overview)
2. [The Core Problem DCB Solves](#the-core-problem-dcb-solves)
3. [How DCB Works Technically](#how-dcb-works-technically)
4. [Building Decision Models](#building-decision-models)
5. [DCB vs Traditional Event Sourcing](#dcb-vs-traditional-event-sourcing)
6. [Trade-offs and Limitations](#trade-offs-and-limitations)
7. [When to Choose DCB](#when-to-choose-dcb)
8. [Implementation Ecosystem](#implementation-ecosystem)
9. [The People Behind DCB](#the-people-behind-dcb)
10. [Resources](#resources)

---

## Overview

DCB (Dynamic Consistency Boundary) represents a fundamental shift in how event-sourced systems enforce consistency—moving from rigid, stream-based aggregate boundaries to flexible, query-based boundaries established at runtime. Created by **Sara Pellegrini** at AxonIQ and first presented in her 2023 talk "Kill Aggregate!", DCB solves the notorious multi-aggregate consistency problem that has plagued traditional event sourcing implementations for years. The pattern enables a single event to affect multiple entities atomically, eliminating the need for Sagas or compensating transactions in many scenarios.

---

## The Core Problem DCB Solves

Traditional event sourcing maps aggregates to event streams in a one-to-one relationship. Each aggregate instance has its own dedicated stream, and optimistic concurrency is enforced per stream using sequence numbers. This pattern works well for single-aggregate operations but breaks down when business rules span multiple aggregates.

Consider a course subscription system with two constraints: "a course cannot exceed its capacity" and "a student cannot enroll in more than 10 courses." The `StudentSubscribedToCourse` event affects invariants of both entities. Traditional approaches offer three unsatisfying solutions: accept eventual consistency (soft constraints), create oversized aggregates (poor scalability), or coordinate via Sagas (complexity and temporary invalid states). As Bastian Waidelich observes: "Operations affecting multiple entities are not edge cases—they are actually the norm in complex domains."

**DCB eliminates this dilemma by allowing events to carry multiple tags**, associating them with different domain concepts simultaneously. A single `StudentSubscribedToCourse` event can be tagged with both `student:s1` and `course:c1`, allowing atomic enforcement of constraints across both entities without coordination mechanisms.

---

## How DCB Works Technically

At its core, DCB requires only two operations from an event store: **read** events matching a query, and **append** events with an optional condition that enforces consistency.

### Event Structure and Tagging

Events in DCB contain three essential components: a **type** (string identifier like `CourseDefined`), **data** (the event payload), and **tags** (a set of strings representing domain concepts). Tags follow a key:value convention like `product:p123` or `student:s1`, though the event store treats them as opaque strings. The critical innovation is that **a single event can have multiple tags**, enabling it to participate in different consistency boundaries for different operations.

```typescript
function StudentSubscribedToCourse({ studentId, courseId }) {
  return {
    type: "StudentSubscribedToCourse",
    data: { studentId, courseId },
    tags: [`student:${studentId}`, `course:${courseId}`],  // Multiple tags!
  }
}
```

### The Query System

DCB queries filter events by type and/or tags. A query contains one or more **Query Items** combined with OR logic, where each item specifies types (OR within item) and tags (AND within item). This enables sophisticated multi-dimensional filtering:

```json
{
  "items": [
    { "types": ["CourseDefined", "CourseCapacityChanged"], "tags": ["course:c1"] },
    { "types": ["StudentSubscribedToCourse"], "tags": ["student:s1"] }
  ]
}
```

### Conditional Appending—The Consistency Mechanism

The **Append Condition** is where DCB enforces consistency. It contains a `failIfEventsMatch` query and an optional `after` sequence position. The event store atomically checks: "Have any events matching this query been appended since position X?" If yes, the append fails and the client must retry with updated state.

```typescript
eventStore.append(
  StudentSubscribedToCourse({ studentId: "s1", courseId: "c1" }),
  {
    failIfEventsMatch: query,  // Same query used to build decision model
    after: lastKnownPosition   // Position of last event client saw
  }
)
```

This differs fundamentally from traditional event sourcing, which checks "has the stream's revision changed?" DCB instead asks "have any events relevant to *this specific operation* been added?" This fine-grained approach means operations on different aspects of the same entity can proceed in parallel.

---

## Building Decision Models

DCB introduces **Decision Models** as the runtime equivalent of aggregates—temporary constructs built for a single operation that guard only the relevant invariants. Decision models are composed from small, focused **projections** that each calculate a specific piece of state.

```typescript
function CourseCapacityProjection(courseId: string) {
  return createProjection({
    initialState: 0,
    handlers: {
      CourseDefined: (state, event) => event.data.capacity,
      CourseCapacityChanged: (state, event) => event.data.newCapacity,
    },
    tagFilter: [`course:${courseId}`],
  })
}

function NumberOfStudentSubscriptionsProjection(studentId: string) {
  return createProjection({
    initialState: 0,
    handlers: {
      StudentSubscribedToCourse: (state) => state + 1,
    },
    tagFilter: [`student:${studentId}`],
  })
}
```

A command handler **composes** these projections dynamically based on what invariants need checking:

```typescript
subscribeStudentToCourse(command) {
  const { state, appendCondition } = buildDecisionModel(this.eventStore, {
    courseExists: CourseExistsProjection(command.courseId),
    courseCapacity: CourseCapacityProjection(command.courseId),
    numberOfCourseSubscriptions: NumberOfCourseSubscriptionsProjection(command.courseId),
    numberOfStudentSubscriptions: NumberOfStudentSubscriptionsProjection(command.studentId),
  })
  
  if (state.numberOfCourseSubscriptions >= state.courseCapacity) {
    throw new Error("Course is fully booked")
  }
  if (state.numberOfStudentSubscriptions >= 10) {
    throw new Error("Student has too many courses")
  }
  
  this.eventStore.append(StudentSubscribedToCourse({...}), appendCondition)
}
```

The `buildDecisionModel` function reads events matching all projections' tag filters, applies the handlers, and generates an append condition that covers the same query—guaranteeing that if any relevant events were added between reading and writing, the operation fails safely.

---

## DCB vs Traditional Event Sourcing

| Aspect                 | Traditional Event Sourcing       | DCB                                  |
|------------------------|----------------------------------|--------------------------------------|
| **Consistency unit**   | Stream/Aggregate                 | Query-based boundary                 |
| **Event organization** | One stream per aggregate         | One stream per bounded context       |
| **Conflict detection** | Stream revision check            | Query-based append condition         |
| **Event ownership**    | Each event belongs to ONE stream | Events tagged with MULTIPLE concepts |
| **Cross-entity rules** | Requires Sagas                   | Single atomic transaction            |
| **Boundary changes**   | Requires data migration          | Code-only refactoring                |

### The "One Fact Magic"

Traditional approaches to subscribing a student to a course require publishing events to both the student's stream and the course's stream, then coordinating with compensating events if either fails. This produces **two events representing the same fact**. DCB produces a single `StudentSubscribedToCourse` event tagged with both identifiers—"one fact, one event."

### Flexibility in Refactoring

Perhaps DCB's most significant advantage is that consistency boundaries exist only in code. With traditional event sourcing, aggregate boundaries become "materialized in the structure of the Event Streams," making changes after deployment extremely difficult. With DCB, the event stream structure never changes—you can experiment with different boundary definitions by modifying projection compositions without data migration.

---

## Trade-offs and Limitations

DCB is not without costs. The pattern requires **global ordering** of events (a single sequence position across the bounded context), which makes horizontal partitioning difficult. As the dcb.events FAQ notes: "DCB guarantees consistency only inside the scope of the global Sequence Position. Thus, Events must be ordered to allow the conditional appending. As a result, it's not (easily) possible to partition Events."

**Query performance** presents technical challenges. Multi-dimensional and cross-cutting queries across millions of events require sophisticated indexing. Early PostgreSQL implementations experimented with GIN indexes, array operators, and full-text search before finding workable approaches. Benchmarks show approximately **1,000 events/second** with PostgreSQL implementations containing millions of events—adequate for many applications but potentially limiting for high-throughput scenarios.

DCB also requires a **mental model shift**. Teams accustomed to aggregate-centric design may find the use-case-oriented approach disorienting. Code organized around behaviors rather than nouns can feel unfamiliar initially.

---

## When to Choose DCB

**DCB excels when:**
- Cross-entity business rules are common (subscription systems, inventory, many-to-many relationships)
- Domain understanding is still evolving and boundaries may change
- Traditional aggregates feel forced or keep growing into "God Objects"
- Flexibility matters more than raw write throughput

**Traditional event sourcing excels when:**
- Clear, stable aggregate boundaries exist in a mature domain
- Maximum write performance and horizontal scaling are critical
- Teams have significant investment in stream-based tooling
- Simple CRUD-like applications with well-defined entities

---

## Implementation Ecosystem

The DCB ecosystem has matured rapidly since Sara Pellegrini's 2023 presentation.

### DCB-Compliant Event Stores

- **Axon Server 2025.1** (Commercial) - Full DCB support with Axon Framework 5.0
- **EventSourcingDB** (Commercial) - HTTP API, designed for DCB from the ground up
- **UmaDB** (Open Source, Rust) - gRPC API, copy-on-write MVCC design

### Libraries by Language

**PHP**: The most mature implementations, maintained by Bastian Waidelich
- `wwwision/dcb-eventstore` - Core library with adapters for Doctrine, Laravel, EventSourcingDB
- `bwaidelich/dcb-example-courses` - Reference implementation

**Java/Kotlin**:
- **Axon Framework 5.0** - Enterprise-grade DCB with "temporary consistency bubble" concept
- `m1l4n54v1c/event-store` - Educational implementation

**Python**:
- `eventsourcing` library (v9.5+) - DCB support with PostgreSQL and UmaDB adapters

**C#/.NET**:
- `Sekiban.Dcb` - NuGet package supporting Azure Cosmos DB, DynamoDB, PostgreSQL with Microsoft Orleans integration

**Rust**:
- `disintegrate` - Inspired by DCB with PostgreSQL implementation
- `umadb-dcb` - Core types and traits for UmaDB

**TypeScript/JavaScript**: Work in progress implementations documented at dcb.events

---

## The People Behind DCB

**Sara Pellegrini** created DCB while working at AxonIQ, introducing it in her April 2023 presentation "Kill Aggregate!" at an Avanscoperta meetup. She has since presented at DDD FR, JAX Mainz, Spring I/O Barcelona, KanDDDinsky (keynote), and AxonIQ Conference 2023.

**Milan Savic**, also at AxonIQ, co-presents the "Aggregate is Dead, Long Live the Aggregate" talk and has written extensively about DCB implementation details.

**Robert Baelde** is credited with proposing the name "Dynamic Consistency Boundary."

**Bastian Waidelich** maintains the PHP implementations and co-authored the DCB specification alongside Sara Pellegrini and Paul Grimshaw. He also maintains the dcb.events documentation website.

---

## Resources

### Official
- **Website**: https://dcb.events/
- **FAQ**: https://dcb.events/faq/
- **Libraries**: https://dcb.events/resources/libraries/

### Talks
- "Kill Aggregate!" - Sara Pellegrini (Avanscoperta, April 2023)
- "Aggregate is Dead, Long Live the Aggregate" - Sara Pellegrini & Milan Savic

### Articles
- "Dynamic Consistency Boundaries" - JAVAPRO International
- "DCB in Axon Framework 5" - AxonIQ Blog

### Implementations
- PHP: `wwwision/dcb-eventstore`
- Java: Axon Framework 5.0
- Python: `eventsourcing` v9.5+
- C#: `Sekiban.Dcb`
- Rust: `disintegrate`, `umadb-dcb`

---

## Conclusion

DCB doesn't reject aggregates—it evolves them. As the dcb.events documentation states: "The Aggregate is dead, long live the Aggregate." Consistency boundaries remain essential; DCB simply makes them dynamic rather than static.

The pattern separates two concerns that traditional event sourcing conflates: the **consistency boundary** (what invariants must hold together) and the **event stream structure** (where events are stored). By tagging events with multiple domain concepts and using query-based conditional appending, DCB allows the same events to participate in different consistency boundaries for different operations.

For teams struggling with cross-aggregate consistency, artificial "coordination aggregates," or Saga complexity, DCB offers a compelling alternative. The ecosystem is maturing rapidly, with enterprise support from AxonIQ and active open-source implementations across multiple languages. While not a silver bullet—global ordering constraints and query performance challenges remain—DCB represents a significant step forward in making event-sourced systems more flexible and maintainable.
