# Dynamic Consistency Boundary (DCB)

Modern approach to consistency in event-driven systems that replaces rigid aggregate
boundaries with flexible, query-based consistency boundaries established at runtime.

## Overview

DCB shifts consistency enforcement from stream-based aggregate boundaries to dynamic,
query-based boundaries. Events carry multiple tags representing domain concepts, and
boundaries form based on what invariants each operation needs to enforce. It enables a
single event to affect multiple entities atomically without Sagas or compensating
transactions.

## Skills

### Auto-Loaded

**dcb**

Activates when you work on event sourcing, consistency boundaries, or cross-entity
business rules. Provides DCB patterns with C#/.NET code examples, decision model
composition, and guidance on when DCB fits versus traditional event sourcing.
