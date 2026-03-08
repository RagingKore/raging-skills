# Python DDD Implementation Guide

Complete reference for implementing DDD patterns in Python.

## Quick Navigation

| Part | Focus | When to Read |
|------|-------|--------------|
| [Part I: Foundations](part1-foundations.md) | Project structure, Value Objects, Entities, Domain Events | Starting a new project |
| [Part II: Aggregates](part2-aggregates.md) | OOP Style & Decider Pattern implementations | Designing domain model |
| [Part III: State-Based](part3-state-based.md) | Repositories, Simple CQRS | Using SQLAlchemy, simple persistence |
| [Part IV: Event-Sourced](part4-event-sourced.md) | Event Store, CQRS, Projections, Serialization | Using EventStoreDB, full CQRS |
| [Part V: Advanced](part5-advanced.md) | Actor Integration (Ray, Thespian) | High concurrency, distributed systems |
| [Part VI: Quality](part6-quality.md) | Testing Patterns, Libraries & Packages | Testing, project setup |

---

## Decision Guide: State-Based vs Event-Sourced

| Factor | State-Based | Event-Sourced |
|--------|-------------|---------------|
| **Audit requirements** | Low/Medium | High - full history |
| **Query patterns** | Current state only | Time-travel queries |
| **Complexity budget** | Conservative | Can invest in infra |
| **Team experience** | Typical ORM | Event-driven systems |
| **Read performance** | Direct queries | Projections required |

**Start with State-Based** (Part III) unless you have specific needs for Event Sourcing.

**Choose Event-Sourced** (Part IV) when:
- Regulatory audit trail required
- Need to reconstruct past states
- Complex event-driven integrations
- Building distributed systems

---

## Detailed Table of Contents

### Part I: Foundations
- Project Structure (package layout, dependency inversion)
- Value Objects (frozen dataclasses, validation)
- Strongly-Typed IDs
- Entities (base class, identity equality)
- Domain Events (protocol, base class, publisher)

### Part II: Aggregates
- OOP Style Aggregates (aggregate root base, Order example)
- Decider Pattern (infrastructure, OrderDecider implementation)

### Part III: State-Based Implementation
- Repository Protocol
- SQLAlchemy Repository
- Simple CQRS (when to use, query handlers)

### Part IV: Event-Sourced Implementation
- Event Store (EventStoreDB, in-memory for testing)
- CQRS & Projections (command handlers, read models, projectors)
- Simple Mediator
- Event Serialization (JSON, integration events)

### Part V: Advanced Patterns
- Actor Integration with Ray (distributed)
- Actor Integration with Thespian (local/distributed)
- Actor Supervision for Read Side

### Part VI: Quality & Reference
- Testing Deciders (pytest examples)
- Given-When-Then Test Helper
- Libraries & Packages by category
- Requirements.txt template
- Type checking setup
