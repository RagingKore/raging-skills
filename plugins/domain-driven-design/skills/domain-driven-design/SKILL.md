---
name: domain-driven-design
description: |
  Comprehensive Domain-Driven Design (DDD) skill covering strategic and tactical patterns. Use when: (1) Designing complex business domains, (2) Implementing aggregates, entities, value objects, (3) Using Decider/Functional patterns vs OOP aggregates, (4) Event sourcing and CQRS architectures, (5) Actor-based DDD systems, (6) Bounded context mapping and integration. Supports .NET and Python implementations with multiple architectural styles from pure functional to traditional OOP.
---

# Domain-Driven Design (DDD) Skill

Expert guidance for modeling complex business domains using strategic and tactical DDD patterns.

## Table of Contents

- [Core Philosophy](#core-philosophy)
- [Ubiquitous Language](#ubiquitous-language)
- [Problem Space: Strategic DDD](#problem-space-strategic-ddd)
- [Solution Space: Tactical DDD](#solution-space-tactical-ddd)
- [Implementation Patterns](#implementation-patterns)
- [Pattern Selection Guide](#pattern-selection-guide)
- [Common Mistakes](#common-mistakes)
- [Reference Documents](#reference-documents)

---

## Core Philosophy

DDD addresses the **complexity gap** between business reality and software models. It's about *understanding*, not patterns.

**Core Insight**: Software fails when developers model *technology* instead of *business*. DDD inverts this: model the business first, let technology serve the model.

### When to Use DDD

| Use DDD When | Skip DDD When |
|--------------|---------------|
| Business logic is primary complexity | CRUD operations dominate |
| Domain experts are accessible | Domain is commodity (use frameworks) |
| System evolves over years | Time-to-market trumps maintainability |
| Multiple teams collaborate | No access to domain experts |
| Getting model wrong is costly | Simple, well-understood domain |

---

## Ubiquitous Language

**Ubiquitous Language is the cornerstone of DDD.** Without it, all tactical patterns are just code organization.

A **shared vocabulary** between developers and domain experts that is:
- Used consistently in conversations, documentation, AND code
- Specific to a bounded context (terms may differ across contexts)
- Evolved collaboratively as understanding deepens

### Code Reflects Language Exactly

```csharp
// ❌ BAD: Technical/generic terms
class DataProcessor {
    void HandleRequest(RequestDto dto) { ... }
}

// ✅ GOOD: Domain language
class LoanApplication {
    void Approve(ApprovalDecision decision) {
        Guard.Against(Status != LoanStatus.UnderReview,
            "Can only approve applications under review");
        Status = LoanStatus.Approved;
        AddDomainEvent(new LoanApplicationApproved(Id, decision));
    }
}
```

### Use Domain Terms

| Don't Use | Do Use |
|-----------|--------|
| `User` | `Customer`, `Borrower`, `Subscriber` |
| `Item` | `Product`, `LineItem`, `Asset` |
| `Process` | `Originate`, `Underwrite`, `Fulfill` |
| `Handler` | `Underwriter`, `ClaimsProcessor` |

### Language Across Bounded Contexts

The same word can mean different things in different contexts. **This is expected.**

| Term | Catalog Context | Order Context | Shipping Context |
|------|-----------------|---------------|------------------|
| **Product** | Full catalog info | SKU + price | Weight + dimensions |
| **Customer** | Demographics | Billing address | Delivery address |

---

## Problem Space: Strategic DDD

Strategic DDD happens *before* writing code. It's about understanding the business.

### Subdomains

| Type | Definition | Investment | Example |
|------|------------|------------|---------|
| **Core** | Competitive advantage | Maximum | Trading algorithms, recommendation engine |
| **Supporting** | Necessary for core | Moderate | User management, reporting |
| **Generic** | Solved problems | Minimum (buy/OSS) | Authentication, email |

**Rule**: Invest DDD effort proportionally to subdomain type.

### Bounded Contexts

A Bounded Context is a **semantic boundary** where terms have precise meaning.

**Example**: "Account" means different things in:
- **Banking Context**: Balance, transactions, interest rates
- **Identity Context**: Username, password, permissions
- **Marketing Context**: Segments, campaigns, preferences

**These are NOT the same entity.** They share a name but have different models.

```
┌──────────────────────────────────────────────────────────────┐
│                      E-COMMERCE SYSTEM                        │
├──────────────────┬──────────────────┬────────────────────────┤
│   CATALOG BC     │    ORDERS BC     │     SHIPPING BC        │
│  Product = full  │  Product = SKU + │  Product = weight +    │
│  catalog info    │  qty + price     │  dimensions            │
└──────────────────┴──────────────────┴────────────────────────┘
```

### Context Mapping Patterns

| Pattern | Relationship | Use When |
|---------|--------------|----------|
| **Partnership** | Mutual dependency, shared success | Teams coordinate closely |
| **Shared Kernel** | Small shared code | Few truly shared, stable concepts |
| **Customer-Supplier** | Upstream provides, downstream consumes | Clear asymmetric dependency |
| **Conformist** | Downstream adopts upstream model | No leverage (external API) |
| **Anti-Corruption Layer** | Translation layer protects your model | Integrating legacy/external systems |
| **Open Host Service** | Published API for multiple consumers | Multiple systems integrate with you |
| **Published Language** | Shared interchange format | Standard data format needed |
| **Separate Ways** | No integration | Contexts truly independent |

**Detailed patterns with code examples:** See [references/context-mapping.md](references/context-mapping.md)

### Event Storming

Collaborative workshop to explore domain:

1. **Domain Events** (orange): Past-tense facts - `OrderPlaced`, `PaymentReceived`
2. **Commands** (blue): Actions - `PlaceOrder`, `ProcessPayment`
3. **Aggregates** (yellow): Clusters handling commands, emitting events
4. **Policies** (purple): Reactive rules ("when X happens, do Y")
5. **Read Models** (green): Views needed by users

---

## Solution Space: Tactical DDD

Tactical patterns implement the model in code.

### Building Blocks

| Pattern | Identity | Mutability | Examples |
|---------|----------|------------|----------|
| **Entity** | Has unique ID | Mutable | Customer, Order, Account |
| **Value Object** | Defined by attributes | Immutable | Money, Address, DateRange |
| **Aggregate** | Consistency boundary | Transaction unit | Order (with OrderLines) |
| **Domain Event** | Facts that happened | Immutable | `OrderPlaced`, `PaymentFailed` |
| **Domain Service** | Stateless operations | N/A | FundsTransferService |
| **Repository** | Persistence abstraction | N/A | OrderRepository |

### Aggregate Rules

1. **Reference other aggregates by ID only** (never object reference)
2. **One aggregate per transaction**
3. **Keep aggregates small**
4. **Eventual consistency between aggregates** (use domain events)

---

## Implementation Patterns

### Pattern 1: OOP Aggregates (Traditional)

Rich domain model with behavior encapsulated in objects.

```
Flow: Load → Call method → Save
Pros: Familiar OOP, encapsulation, works with ORMs
Cons: Mutable state, harder testing
```

### Pattern 2: Functional Decider (Modern)

Pure functions that decide state transitions.

```
Three pure functions:
1. DECIDE: (Command, State) → Event[]
2. EVOLVE: (State, Event) → State
3. INITIAL_STATE: () → State

Pros: Pure functions, easy testing, natural event sourcing
Cons: Less familiar, different persistence
```

### Pattern 3: Event Sourcing

Events as source of truth, state derived by replay.

```
Instead of: Order { status: "shipped", total: 100 }

Store events:
- OrderCreated { id: 123 }
- ItemAdded { productId: "ABC", qty: 2 }
- OrderShipped { carrier: "FedEx" }

State = fold(events, initial_state, evolve)
```

### Pattern 4: CQRS

Separate models for writes (commands) and reads (queries).

```
Write Side: Aggregates, domain logic, consistency
Read Side: Denormalized views, optimized queries
Sync: Domain events → Projectors → Read models
```

---

## Pattern Selection Guide

```
Is domain logic complex?
├─ NO → Simple CRUD
└─ YES
    Need audit trail / temporal queries?
    ├─ YES → EVENT SOURCING + CQRS
    │   ├─ Functional team? → DECIDER PATTERN
    │   └─ OOP team? → OOP + Event capture
    └─ NO
        Concurrency/distribution critical?
        ├─ YES → ACTOR MODEL + DDD
        └─ NO
            ├─ Functional → DECIDER PATTERN
            └─ OOP → RICH DOMAIN MODEL
```

---

## Common Mistakes

| Mistake | Problem | Fix |
|---------|---------|-----|
| **Anemic Domain Model** | Entities are data bags, logic in services | Put behavior in entities |
| **Aggregate Too Large** | Contention, slow loading | Reference by ID, keep small |
| **Repository Per Entity** | Bypasses aggregate root | One repository per aggregate |
| **Exposing Internals** | `public List<T>` allows bypass | Return `IReadOnlyList<T>` |
| **CRUD Events** | `OrderUpdated` - meaningless | `OrderShipped` - domain meaning |
| **Missing BC Boundaries** | Shared `Account` across contexts | Separate models per context |
| **Sync Cross-Aggregate** | Direct calls between aggregates | Use domain events |
| **Domain = Integration Events** | Internal events published externally | Translate to stable contracts |
| **Over-Engineering** | Full DDD for user preferences | Match complexity to domain |
| **No Domain Experts** | Guessing business rules | Regular sessions, glossary |

**Detailed examples and code:** See [references/common-mistakes.md](references/common-mistakes.md)

---

## Reference Documents

### Implementation Guides

| Language | Guide | Content |
|----------|-------|---------|
| .NET/C# | [dotnet/](references/dotnet/) | Record entities, Decider, Event Sourcing, Akka.NET actors |
| Python | [python/](references/python/) | dataclasses, functional Decider, Ray/Thespian actors |

Each guide is split into 6 parts for focused reference:
- Part I: Foundations (Value Objects, Entities, Events)
- Part II: Aggregates (OOP and Decider patterns)
- Part III: State-Based (Repositories, simple CQRS)
- Part IV: Event-Sourced (Event Store, CQRS, Projections)
- Part V: Advanced (Actor integration)
- Part VI: Quality (Testing, Libraries)

### Additional References

| Document | Content |
|----------|---------|
| [actors-ddd-guide.md](references/actors-ddd-guide.md) | Actor Model + DDD: aggregate-per-actor, distributed consistency |
| [context-mapping.md](references/context-mapping.md) | Detailed context mapping patterns with code examples |
| [common-mistakes.md](references/common-mistakes.md) | Detailed mistake examples with good/bad code |
| [event-evolution.md](references/event-evolution.md) | Event versioning, schema evolution, upcasting |
| [projections-guide.md](references/projections-guide.md) | Building projections: Marten, PostgreSQL, DuckDB |
| [books-resources.md](references/books-resources.md) | Curated reading: foundational texts, articles, talks |
