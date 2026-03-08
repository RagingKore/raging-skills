---
name: dcb
description: Dynamic Consistency Boundary (DCB) - a modern approach to consistency in event-driven systems that replaces rigid aggregate-based boundaries with flexible, query-based consistency boundaries established at runtime. Use this skill when implementing event sourcing with cross-entity business rules, designing systems where multiple aggregates need atomic consistency, refactoring from traditional aggregate-based event sourcing, working with event stores that support DCB (KurrentDB, Axon Server, EventSourcingDB, UmaDB), modeling complex domains with many-to-many relationships, or when traditional aggregates feel forced or keep growing into "God Objects". DCB enables a single event to affect multiple entities atomically without Sagas or compensating transactions.
---

# Dynamic Consistency Boundary (DCB)

DCB shifts consistency enforcement from rigid stream-based aggregate boundaries to flexible query-based boundaries established at runtime. Created by Sara Pellegrini (AxonIQ, 2023).

## Core Concept

Traditional event sourcing: one aggregate = one stream = one consistency boundary. DCB decouples these: events carry **multiple tags** representing domain concepts, and consistency boundaries form dynamically based on **what invariants each operation needs to enforce**.

**The fundamental insight**: Consistency boundaries should be determined by the operation being performed, not by how events are stored.

## When to Use DCB

**DCB excels for:**
- Cross-entity business rules (subscriptions, inventory, reservations)
- Many-to-many relationships requiring atomic updates
- Domains with evolving boundaries (flexibility over raw throughput)
- When aggregates keep growing or feel artificial

**Prefer traditional event sourcing when:**
- Clear, stable aggregate boundaries exist
- Maximum write throughput is critical (global ordering constraint)
- Simple CRUD with well-defined entities

## DCB vs Traditional Event Sourcing

| Aspect             | Traditional              | DCB                               |
|--------------------|--------------------------|-----------------------------------|
| Consistency unit   | Stream/Aggregate         | Query-based boundary              |
| Event organization | One stream per aggregate | Single stream per bounded context |
| Conflict detection | Stream revision          | Query-based append condition      |
| Event ownership    | One stream only          | Multiple tags (entities)          |
| Cross-entity rules | Sagas required           | Single atomic transaction         |
| Boundary changes   | Data migration           | Code-only refactoring             |

## Three Core Operations

DCB requires only three operations from an event store:

### 1. Events with Tags

Events have type, data, and **tags** (set of strings representing domain concepts):

```csharp
public record StudentSubscribedToCourse(
    string StudentId,
    string CourseId
) : IDcbEvent
{
    public IReadOnlySet<string> Tags => new HashSet<string>
    {
        $"student:{StudentId}",
        $"course:{CourseId}"    // Multiple tags = multiple consistency boundaries
    };
}
```

### 2. Query Events by Tags

Query items combine types (OR) and tags (AND). Multiple items combine with OR:

```csharp
var query = new DcbQuery(
    new QueryItem(
        Types: ["CourseDefined", "CourseCapacityChanged"],
        Tags: ["course:c1"]
    ),
    new QueryItem(
        Types: ["StudentSubscribedToCourse"],
        Tags: ["student:s1"]
    )
);
```

### 3. Conditional Append

Append with condition: "fail if events matching query were added after position X":

```csharp
await eventStore.AppendAsync(
    @event,
    new AppendCondition(
        FailIfEventsMatch: query,
        After: lastKnownPosition
    )
);
```

## Decision Model Pattern

Decision models replace aggregates as the runtime consistency mechanism. They're **temporary constructs built for a single operation**, composed from small focused projections.

### Projections (State Builders)

Each projection calculates one piece of state:

```csharp
public class CourseCapacityProjection : IDcbProjection<int>
{
    private readonly string _courseId;
    
    public CourseCapacityProjection(string courseId) => _courseId = courseId;
    
    public int InitialState => 0;
    
    public IReadOnlySet<string> TagFilter => [$"course:{_courseId}"];
    
    public IReadOnlySet<string> TypeFilter => ["CourseDefined", "CourseCapacityChanged"];
    
    public int Apply(int state, IDcbEvent @event) => @event switch
    {
        CourseDefined e => e.Capacity,
        CourseCapacityChanged e => e.NewCapacity,
        _ => state
    };
}
```

### Composing Decision Models

Command handlers compose projections dynamically:

```csharp
public async Task SubscribeStudentToCourse(SubscribeCommand cmd)
{
    var decisionModel = await _eventStore.BuildDecisionModelAsync(
        ("courseExists", new CourseExistsProjection(cmd.CourseId)),
        ("capacity", new CourseCapacityProjection(cmd.CourseId)),
        ("courseSubscriptions", new CourseSubscriptionCountProjection(cmd.CourseId)),
        ("studentSubscriptions", new StudentSubscriptionCountProjection(cmd.StudentId))
    );
    
    // Enforce invariants
    if (!decisionModel.State.courseExists)
        throw new CourseNotFoundException();
    if (decisionModel.State.courseSubscriptions >= decisionModel.State.capacity)
        throw new CourseFullException();
    if (decisionModel.State.studentSubscriptions >= 10)
        throw new TooManyCoursesException();
    
    // Append with automatically-generated condition
    await _eventStore.AppendAsync(
        new StudentSubscribedToCourse(cmd.StudentId, cmd.CourseId),
        decisionModel.AppendCondition
    );
}
```

The `BuildDecisionModelAsync` function:
1. Reads events matching all projections' tag/type filters
2. Applies handlers to build state
3. Generates append condition covering the same query
4. Returns combined state and condition

## Reference Files

- **[references/implementation-patterns.md](references/implementation-patterns.md)**: Complete C#/.NET implementation patterns with KurrentDB/Marten integration
- **[references/anti-patterns.md](references/anti-patterns.md)**: Common mistakes and how to avoid them with bad/good code comparisons
- **[references/event-store-adapters.md](references/event-store-adapters.md)**: Adapting different event stores (KurrentDB, Marten, PostgreSQL) for DCB
- **[references/migration-guide.md](references/migration-guide.md)**: Migrating from traditional event sourcing to DCB
- **[references/research-report.md](references/research-report.md)**: Original research covering DCB theory, ecosystem, community, and resources

## Quick Reference

### Tag Conventions

```
{concept}:{id}        → student:s1, course:c1, order:o123
{concept}:{compound}  → enrollment:s1-c1
{type}:{value}        → type:premium, status:active
```

### Query Composition

```csharp
// Single entity query
new DcbQuery(new QueryItem(Tags: ["course:c1"]))

// Cross-entity query (OR of items)
new DcbQuery(
    new QueryItem(Tags: ["student:s1"]),
    new QueryItem(Tags: ["course:c1"])
)

// Type-filtered query
new QueryItem(
    Types: ["OrderPlaced", "OrderCancelled"],
    Tags: ["order:o1"]
)
```

### Append Condition Semantics

```csharp
// "Fail if ANY event matching this query was appended after position 42"
new AppendCondition(
    FailIfEventsMatch: query,
    After: 42
)
```

## Key Principles

1. **One fact, one event**: A `StudentSubscribedToCourse` event is tagged with both student and course, not duplicated across streams

2. **Boundaries in code, not storage**: Event stream structure never changes; refactor boundaries by changing projection composition

3. **Compose for the operation**: Each operation builds exactly the decision model it needs—no more, no less

4. **Fine-grained conflict detection**: Only operations affecting the same tags conflict; unrelated operations on the same entity proceed in parallel

## Trade-offs

**Requires global ordering**: DCB needs a single sequence position across the bounded context, making horizontal partitioning difficult

**Query performance**: Multi-dimensional tag queries require sophisticated indexing; expect ~1,000 events/second with PostgreSQL

**Mental model shift**: Teams accustomed to aggregate-centric design may find use-case-oriented organization unfamiliar initially
