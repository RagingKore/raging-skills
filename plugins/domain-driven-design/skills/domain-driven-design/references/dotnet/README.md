# .NET DDD Implementation Guide

Modern .NET 9 patterns for Domain-Driven Design using records, KurrentDB, Marten, and TUnit.

## Quick Navigation

| Part | Focus | File |
|------|-------|------|
| **I. Foundations** | Building blocks: Value Objects, Entities, Domain Events | [part1-foundations.md](part1-foundations.md) |
| **II. Aggregates** | OOP Style and Decider Pattern implementations | [part2-aggregates.md](part2-aggregates.md) |
| **III. State-Based** | Repositories (EF Core, Marten), Simple CQRS | [part3-state-based.md](part3-state-based.md) |
| **IV. Event-Sourced** | Event Store, CQRS & Projections, Serialization | [part4-event-sourced.md](part4-event-sourced.md) |
| **V. Advanced** | Actor Integration (Akka.NET) | [part5-advanced.md](part5-advanced.md) |
| **VI. Quality** | Testing Patterns, Libraries & Packages | [part6-quality.md](part6-quality.md) |

---

## Decision Guide: State-Based vs Event-Sourced

Choose your persistence strategy based on your requirements:

| Factor | State-Based (Part III) | Event-Sourced (Part IV) |
|--------|------------------------|-------------------------|
| **Audit requirements** | External audit log | Built-in complete history |
| **Temporal queries** | Difficult/impossible | Native capability |
| **Complexity** | Lower | Higher |
| **Team experience** | DDD beginners OK | Requires ES experience |
| **Debugging** | Current state visible | Must replay to see state |
| **Storage** | Grows with entities | Grows with events (unbounded) |
| **CQRS** | Optional | Nearly mandatory |
| **Schema evolution** | Standard migrations | Event versioning required |

### Decision Flow

```
Is domain logic complex?
├─ NO → Simple CRUD (skip DDD)
└─ YES
    Need audit trail / temporal queries?
    ├─ YES → EVENT SOURCING (Part IV) + CQRS
    │   ├─ Functional team? → Decider Pattern (Part II)
    │   └─ OOP team? → OOP Aggregates + Event capture
    └─ NO
        Concurrency/distribution critical?
        ├─ YES → ACTOR MODEL (Part V) + DDD
        └─ NO
            ├─ Functional → Decider Pattern
            └─ OOP → Rich Domain Model
```

---

## Detailed Table of Contents

### Part I: Foundations
- [Project Structure](part1-foundations.md#project-structure) - Recommended package layout
- [Value Objects](part1-foundations.md#value-objects) - Money, strongly-typed IDs, validation
- [Entities](part1-foundations.md#entities) - Base classes, identity equality
- [Domain Events](part1-foundations.md#domain-events) - Event base, naming, publisher

### Part II: Aggregates
- [Aggregates - OOP Style](part2-aggregates.md#aggregates---oop-style) - Rich domain model with records
- [Aggregates - Decider Pattern](part2-aggregates.md#aggregates---decider-pattern) - Pure functional approach

### Part III: State-Based Implementation
- [Repositories](part3-state-based.md#repositories) - EF Core, Marten document store
- [Simple CQRS](part3-state-based.md#simple-cqrs-optional) - Optional read optimization

### Part IV: Event-Sourced Implementation
- [Event Store](part4-event-sourced.md#event-store) - KurrentDB, Marten ES
- [CQRS & Projections](part4-event-sourced.md#cqrs--projections) - Read models, projectors
- [Event Serialization & Versioning](part4-event-sourced.md#event-serialization--versioning) - Protobuf, schema evolution

### Part V: Advanced Patterns
- [Actor Integration](part5-advanced.md#actor-integration) - Akka.NET with Decider pattern

### Part VI: Quality & Reference
- [Testing Patterns](part6-quality.md#testing-patterns) - TUnit, Given-When-Then helpers
- [Libraries & Packages](part6-quality.md#libraries--packages) - Package references by category
