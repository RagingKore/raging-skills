# Migrating to DCB from Traditional Event Sourcing

Step-by-step guide for migrating from aggregate-per-stream event sourcing to Dynamic Consistency Boundary.

## Table of Contents

1. [Migration Strategy Overview](#migration-strategy-overview)
2. [Phase 1: Event Transformation](#phase-1-event-transformation)
3. [Phase 2: Projection Extraction](#phase-2-projection-extraction)
4. [Phase 3: Command Handler Refactoring](#phase-3-command-handler-refactoring)
5. [Phase 4: Running Dual-Write](#phase-4-running-dual-write)
6. [Phase 5: Cutover](#phase-5-cutover)
7. [Rollback Strategy](#rollback-strategy)

---

## Migration Strategy Overview

### Key Insight

DCB migration is primarily a **code change**, not a data migration. The fundamental shift is from "which stream does this event belong to?" to "which tags does this event carry?"

### Migration Approaches

| Approach          | Risk   | Complexity | Downtime |
|-------------------|--------|------------|----------|
| **Big Bang**      | High   | Low        | Yes      |
| **Strangler Fig** | Low    | Medium     | No       |
| **Dual-Write**    | Medium | High       | No       |

**Recommended: Strangler Fig** - Migrate operation by operation, with old and new systems coexisting.

---

## Phase 1: Event Transformation

Transform existing events to carry tags. This can be done at read time (no data migration) or write time (new events only).

### Before: Traditional Event

```csharp
// Traditional: Event belongs to one stream
public record StudentSubscribedToCourse(
    string StudentId,
    string CourseId
);

// Stored in stream: "Student-{StudentId}"
// OR stored in stream: "Course-{CourseId}"
// But not both!
```

### After: DCB Event with Tags

```csharp
public record StudentSubscribedToCourse(
    string StudentId,
    string CourseId
) : IDcbEvent
{
    public string Type => nameof(StudentSubscribedToCourse);
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    
    // Now tagged with BOTH entities
    public IReadOnlySet<string> Tags => new HashSet<string>
    {
        $"student:{StudentId}",
        $"course:{CourseId}"
    };
}
```

### Migration Adapter Pattern

Wrap existing events to add tags at read time:

```csharp
public class LegacyEventAdapter
{
    public IDcbEvent AdaptEvent(object legacyEvent, string streamId)
    {
        return legacyEvent switch
        {
            // Derive tags from stream ID and event properties
            LegacyStudentSubscribed e when streamId.StartsWith("Student-") =>
                new StudentSubscribedToCourse(
                    ExtractIdFromStream(streamId, "Student-"),
                    e.CourseId
                ),
            
            LegacyStudentSubscribed e when streamId.StartsWith("Course-") =>
                new StudentSubscribedToCourse(
                    e.StudentId,
                    ExtractIdFromStream(streamId, "Course-")
                ),
            
            // Add cases for all event types
            _ => throw new UnknownEventTypeException(legacyEvent.GetType())
        };
    }
    
    private string ExtractIdFromStream(string streamId, string prefix) =>
        streamId.Substring(prefix.Length);
}
```

---

## Phase 2: Projection Extraction

Convert aggregate methods to standalone projections.

### Before: Aggregate with State

```csharp
public class CourseAggregate
{
    public string Id { get; private set; }
    public int Capacity { get; private set; }
    public int SubscriptionCount { get; private set; }
    public List<string> SubscribedStudents { get; } = new();
    
    public void Apply(CourseDefined @event)
    {
        Id = @event.CourseId;
        Capacity = @event.Capacity;
    }
    
    public void Apply(StudentSubscribedToCourse @event)
    {
        SubscriptionCount++;
        SubscribedStudents.Add(@event.StudentId);
    }
    
    public void SubscribeStudent(string studentId)
    {
        if (SubscriptionCount >= Capacity)
            throw new CourseFullException();
        if (SubscribedStudents.Contains(studentId))
            throw new AlreadySubscribedException();
        
        // Raise event
    }
}
```

### After: Extracted Projections

```csharp
// Each piece of state becomes its own projection
public class CourseCapacityProjection : IDcbProjection<int>
{
    private readonly string _courseId;
    public CourseCapacityProjection(string courseId) => _courseId = courseId;
    
    public int InitialState => 0;
    public IReadOnlySet<string> TagFilter => [$"course:{_courseId}"];
    public IReadOnlySet<string>? TypeFilter => [nameof(CourseDefined), nameof(CourseCapacityChanged)];
    
    public int Apply(int state, IDcbEvent @event) => @event switch
    {
        CourseDefined e => e.Capacity,
        CourseCapacityChanged e => e.NewCapacity,
        _ => state
    };
}

public class CourseSubscriptionCountProjection : IDcbProjection<int>
{
    private readonly string _courseId;
    public CourseSubscriptionCountProjection(string courseId) => _courseId = courseId;
    
    public int InitialState => 0;
    public IReadOnlySet<string> TagFilter => [$"course:{_courseId}"];
    public IReadOnlySet<string>? TypeFilter => 
        [nameof(StudentSubscribedToCourse), nameof(StudentUnsubscribedFromCourse)];
    
    public int Apply(int state, IDcbEvent @event) => @event switch
    {
        StudentSubscribedToCourse => state + 1,
        StudentUnsubscribedFromCourse => state - 1,
        _ => state
    };
}

public class CourseSubscribedStudentsProjection : IDcbProjection<ImmutableHashSet<string>>
{
    private readonly string _courseId;
    public CourseSubscribedStudentsProjection(string courseId) => _courseId = courseId;
    
    public ImmutableHashSet<string> InitialState => [];
    public IReadOnlySet<string> TagFilter => [$"course:{_courseId}"];
    public IReadOnlySet<string>? TypeFilter => 
        [nameof(StudentSubscribedToCourse), nameof(StudentUnsubscribedFromCourse)];
    
    public ImmutableHashSet<string> Apply(ImmutableHashSet<string> state, IDcbEvent @event) => @event switch
    {
        StudentSubscribedToCourse e => state.Add(e.StudentId),
        StudentUnsubscribedFromCourse e => state.Remove(e.StudentId),
        _ => state
    };
}
```

### Extraction Checklist

For each aggregate property:

1. ✅ Create a projection class
2. ✅ Define tag filter (usually `{aggregateType}:{aggregateId}`)
3. ✅ Define type filter (which events affect this property)
4. ✅ Implement `Apply` with same logic as aggregate
5. ✅ Test projection produces same state as aggregate

---

## Phase 3: Command Handler Refactoring

Convert aggregate-based command handlers to decision model composition.

### Before: Aggregate Repository Pattern

```csharp
public class SubscriptionCommandHandler
{
    private readonly IAggregateRepository<CourseAggregate> _courseRepo;
    private readonly IAggregateRepository<StudentAggregate> _studentRepo;
    
    public async Task SubscribeStudentToCourse(string studentId, string courseId)
    {
        // Load both aggregates (two streams)
        var course = await _courseRepo.LoadAsync(courseId);
        var student = await _studentRepo.LoadAsync(studentId);
        
        // Check invariants across both
        course.SubscribeStudent(studentId);  // Checks course capacity
        student.EnrollInCourse(courseId);     // Checks student enrollment limit
        
        // Save both (two appends, potential inconsistency!)
        await _courseRepo.SaveAsync(course);
        await _studentRepo.SaveAsync(student);
    }
}
```

### After: Decision Model Composition

```csharp
public class SubscriptionCommandHandler
{
    private readonly IDcbEventStore _eventStore;
    private readonly IRetryPolicy _retry;
    
    public async Task SubscribeStudentToCourse(string studentId, string courseId)
    {
        await _retry.ExecuteAsync(async () =>
        {
            // Build single decision model from multiple projections
            var (states, condition) = await MultiProjectionDecisionModel.BuildAsync(
                _eventStore,
                ("courseCapacity", new CourseCapacityProjection(courseId)),
                ("courseSubscriptions", new CourseSubscriptionCountProjection(courseId)),
                ("courseStudents", new CourseSubscribedStudentsProjection(courseId)),
                ("studentEnrollments", new StudentEnrollmentCountProjection(studentId))
            );
            
            // Same invariant checks, now atomic
            var capacity = (int)states["courseCapacity"];
            var subscriptions = (int)states["courseSubscriptions"];
            var students = (ImmutableHashSet<string>)states["courseStudents"];
            var enrollments = (int)states["studentEnrollments"];
            
            if (subscriptions >= capacity)
                throw new CourseFullException(courseId);
            if (students.Contains(studentId))
                throw new AlreadySubscribedException(studentId, courseId);
            if (enrollments >= 10)
                throw new TooManyEnrollmentsException(studentId);
            
            // Single atomic append with multi-entity tags
            await _eventStore.AppendAsync(
                new StudentSubscribedToCourse(studentId, courseId),
                condition
            );
        });
    }
}
```

---

## Phase 4: Running Dual-Write

During transition, write to both old and new systems.

```csharp
public class DualWriteEventStore : IDcbEventStore
{
    private readonly ILegacyEventStore _legacy;
    private readonly IDcbEventStore _dcb;
    private readonly LegacyEventAdapter _adapter;
    
    public async Task<long> AppendAsync(
        IDcbEvent @event,
        AppendCondition? condition = null,
        CancellationToken ct = default)
    {
        // Write to DCB store (primary)
        var position = await _dcb.AppendAsync(@event, condition, ct);
        
        try
        {
            // Write to legacy store (for existing consumers)
            var legacyEvent = ConvertToLegacy(@event);
            var streamId = DeriveStreamId(@event);
            await _legacy.AppendAsync(streamId, legacyEvent);
        }
        catch (Exception ex)
        {
            // Log but don't fail - DCB is source of truth
            _logger.LogWarning(ex, "Legacy write failed for event {Type}", @event.Type);
        }
        
        return position;
    }
    
    public async IAsyncEnumerable<DcbEventEnvelope> ReadAsync(
        DcbQuery query,
        long afterPosition = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Read from DCB store first
        var dcbHasData = false;
        await foreach (var envelope in _dcb.ReadAsync(query, afterPosition, ct))
        {
            dcbHasData = true;
            yield return envelope;
        }
        
        // If no DCB data, fall back to legacy
        if (!dcbHasData && afterPosition == 0)
        {
            var legacyEvents = await ReadFromLegacyAsync(query, ct);
            foreach (var envelope in legacyEvents)
            {
                yield return envelope;
            }
        }
    }
}
```

---

## Phase 5: Cutover

### Pre-Cutover Checklist

- [ ] All command handlers migrated to DCB
- [ ] All projections tested against legacy data
- [ ] Dual-write running without errors for 1+ week
- [ ] Read models rebuilt from DCB events
- [ ] Performance benchmarks acceptable
- [ ] Rollback procedure tested

### Cutover Steps

1. **Announce maintenance window**
2. **Stop all writes** to legacy system
3. **Verify DCB is caught up** (no lag in dual-write)
4. **Switch read traffic** to DCB-based projections
5. **Remove dual-write**, DCB becomes sole source
6. **Monitor** for 24-48 hours
7. **Archive** legacy event store (don't delete yet)

### Post-Cutover

- Remove legacy event store code
- Remove adapter/transformation code
- Update documentation
- Delete legacy store after 30-90 day retention period

---

## Rollback Strategy

### Immediate Rollback (< 1 hour)

If issues discovered within first hour:

1. Re-enable dual-write
2. Switch reads back to legacy
3. Investigate DCB issues
4. No data loss (legacy has all events)

### Extended Rollback (> 1 hour)

If DCB was sole writer for extended period:

1. Export events from DCB since cutover
2. Transform back to legacy format
3. Append to legacy streams
4. Switch back to legacy handlers
5. Replay read models from legacy

### Rollback Prevention

Best way to avoid rollback:

1. **Long dual-write period** - Run both systems for weeks
2. **Shadow reads** - Compare DCB vs legacy results
3. **Gradual traffic shift** - Start with 1%, increase slowly
4. **Feature flags** - Enable DCB per-operation, not globally
5. **Comprehensive monitoring** - Alert on any discrepancy

---

## Common Migration Pitfalls

### Pitfall 1: Incorrect Tag Derivation

```csharp
// WRONG: Losing entity associations
public IDcbEvent AdaptEvent(LegacyStudentSubscribed e, string streamId)
{
    // Stream was "Course-123", we only have courseId
    var courseId = ExtractId(streamId);
    
    // BUG: Where does studentId come from if not in event?
    return new StudentSubscribedToCourse(???, courseId);
}
```

**Solution**: Ensure legacy events contain all entity IDs, or enrich from external source.

### Pitfall 2: Missing Projections for Cross-Entity Rules

```csharp
// Legacy had separate aggregates, easy to forget cross-entity rules
public async Task SubscribeStudentToCourse(...)
{
    // WRONG: Only checking course-side invariants
    var model = await BuildDecisionModel(_eventStore,
        new CourseCapacityProjection(courseId),
        new CourseSubscriptionCountProjection(courseId)
    );
    
    // BUG: Student enrollment limit not checked!
}
```

**Solution**: Review ALL business rules during migration, not just per-aggregate rules.

### Pitfall 3: Position Mismatch During Dual-Write

```csharp
// DCB position and legacy stream version are different numbers!
public async Task<long> AppendAsync(IDcbEvent @event, AppendCondition? condition, ...)
{
    var dcbPosition = await _dcb.AppendAsync(@event, condition);
    
    // BUG: Using DCB position as legacy expected version
    await _legacy.AppendAsync(streamId, legacyEvent, expectedVersion: dcbPosition);
}
```

**Solution**: Track positions independently for each store.
