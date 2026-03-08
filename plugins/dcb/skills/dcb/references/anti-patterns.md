# DCB Anti-Patterns

Common mistakes when implementing DCB with bad/good code comparisons.

## Table of Contents

1. [Overly Broad Consistency Boundaries](#overly-broad-consistency-boundaries)
2. [Ignoring Tag Granularity](#ignoring-tag-granularity)
3. [Missing Retry Logic](#missing-retry-logic)
4. [Duplicating Events Across Entities](#duplicating-events-across-entities)
5. [Treating DCB Like Traditional Aggregates](#treating-dcb-like-traditional-aggregates)
6. [Ignoring Eventual Consistency Where Appropriate](#ignoring-eventual-consistency-where-appropriate)
7. [Leaking Tags into Business Logic](#leaking-tags-into-business-logic)
8. [Monolithic Projections](#monolithic-projections)

---

## Overly Broad Consistency Boundaries

### ❌ Bad: Query Too Broad

```csharp
public class OverlyBroadEnrollmentHandler
{
    public async Task SubscribeStudentToCourse(string studentId, string courseId)
    {
        // BAD: Queries ALL course events and ALL student events
        var (states, condition) = await MultiProjectionDecisionModel.BuildAsync(
            _eventStore,
            ("allCourses", new AllCoursesProjection()),      // Every course event!
            ("allStudents", new AllStudentsProjection())     // Every student event!
        );
        
        // This will conflict with ANY operation on ANY course or student
        await _eventStore.AppendAsync(
            new StudentSubscribedToCourse(studentId, courseId),
            condition
        );
    }
}
```

### ✅ Good: Precise Boundaries

```csharp
public class PreciseEnrollmentHandler
{
    public async Task SubscribeStudentToCourse(string studentId, string courseId)
    {
        // GOOD: Only query events for THIS specific student and course
        var (states, condition) = await MultiProjectionDecisionModel.BuildAsync(
            _eventStore,
            ("courseExists", new CourseExistsProjection(courseId)),       // Only course:c1
            ("courseCapacity", new CourseCapacityProjection(courseId)),   // Only course:c1
            ("studentCount", new StudentSubscriptionCountProjection(studentId))  // Only student:s1
        );
        
        // Only conflicts with operations on this specific student or course
        await _eventStore.AppendAsync(
            new StudentSubscribedToCourse(studentId, courseId),
            condition
        );
    }
}
```

**Why it matters**: Broad boundaries cause false conflicts. Operations on unrelated students/courses will fail unnecessarily, destroying throughput.

---

## Ignoring Tag Granularity

### ❌ Bad: Single Generic Tag

```csharp
public record StudentSubscribedToCourse(
    string StudentId,
    string CourseId
) : IDcbEvent
{
    public string Type => nameof(StudentSubscribedToCourse);
    
    // BAD: Single tag means you can't query by student OR course independently
    public IReadOnlySet<string> Tags => new HashSet<string>
    {
        $"enrollment:{StudentId}-{CourseId}"
    };
}
```

### ✅ Good: Multiple Granular Tags

```csharp
public record StudentSubscribedToCourse(
    string StudentId,
    string CourseId
) : IDcbEvent
{
    public string Type => nameof(StudentSubscribedToCourse);
    
    // GOOD: Multiple tags enable flexible querying
    public IReadOnlySet<string> Tags => new HashSet<string>
    {
        $"student:{StudentId}",           // Query by student
        $"course:{CourseId}",             // Query by course
        $"enrollment:{StudentId}-{CourseId}"  // Query specific enrollment
    };
}
```

**Why it matters**: Tags determine queryability. Without `student:s1` tag, you can't build a decision model for "all of student s1's enrollments."

---

## Missing Retry Logic

### ❌ Bad: No Retry on Conflict

```csharp
public class NoRetryHandler
{
    public async Task SubscribeStudentToCourse(string studentId, string courseId)
    {
        var model = await BuildDecisionModel(studentId, courseId);
        
        // BAD: Concurrent operations will fail and stay failed
        await _eventStore.AppendAsync(
            new StudentSubscribedToCourse(studentId, courseId),
            model.AppendCondition
        );
    }
}
```

### ✅ Good: Retry with Exponential Backoff

```csharp
public class RetryingHandler
{
    private readonly IRetryPolicy _retry;
    
    public async Task SubscribeStudentToCourse(string studentId, string courseId)
    {
        // GOOD: Retry rebuilds decision model with fresh state
        await _retry.ExecuteAsync(async () =>
        {
            var model = await BuildDecisionModel(studentId, courseId);
            
            ValidateInvariants(model.State);
            
            await _eventStore.AppendAsync(
                new StudentSubscribedToCourse(studentId, courseId),
                model.AppendCondition
            );
        });
    }
}
```

**Why it matters**: Concurrent operations on overlapping tags WILL conflict. Retry is not optional—it's fundamental to DCB's optimistic concurrency model.

---

## Duplicating Events Across Entities

### ❌ Bad: Traditional Two-Event Pattern

```csharp
public class TwoEventHandler
{
    public async Task SubscribeStudentToCourse(string studentId, string courseId)
    {
        // BAD: Two events for one fact = coordination nightmare
        await _eventStore.AppendAsync(new StudentEnrolled(studentId, courseId));
        await _eventStore.AppendAsync(new CourseEnrollmentAdded(courseId, studentId));
        
        // What if the second append fails? Inconsistent state!
    }
}

// BAD: Separate events for the same fact
public record StudentEnrolled(string StudentId, string CourseId);
public record CourseEnrollmentAdded(string CourseId, string StudentId);
```

### ✅ Good: One Fact, One Event

```csharp
public class OneEventHandler
{
    public async Task SubscribeStudentToCourse(string studentId, string courseId)
    {
        // GOOD: Single event with multiple tags
        await _eventStore.AppendAsync(
            new StudentSubscribedToCourse(studentId, courseId)  // Tags both entities
        );
    }
}

public record StudentSubscribedToCourse(
    string StudentId,
    string CourseId
) : IDcbEvent
{
    public IReadOnlySet<string> Tags => new HashSet<string>
    {
        $"student:{StudentId}",
        $"course:{CourseId}"
    };
}
```

**Why it matters**: DCB's core value proposition is "one fact, one event." Duplicating events defeats the purpose and reintroduces coordination problems.

---

## Treating DCB Like Traditional Aggregates

### ❌ Bad: Stateful Aggregate Objects

```csharp
// BAD: Traditional aggregate-style class with internal state
public class CourseAggregate
{
    private readonly List<IDcbEvent> _changes = [];
    private int _subscriptionCount;
    private int _capacity;
    
    public void SubscribeStudent(string studentId)
    {
        if (_subscriptionCount >= _capacity)
            throw new CourseFullException();
        
        _changes.Add(new StudentSubscribedToCourse(studentId, _courseId));
        _subscriptionCount++;  // Mutating internal state
    }
    
    public async Task SaveAsync(IDcbEventStore store)
    {
        // BAD: Where does the append condition come from?
        await store.AppendAsync(_changes);
    }
}
```

### ✅ Good: Stateless Decision Models

```csharp
// GOOD: Stateless handler with temporary decision model
public class CourseCommandHandler
{
    public async Task SubscribeStudent(string studentId, string courseId)
    {
        // Decision model is temporary, built fresh for this operation
        var model = await DecisionModelBuilder.BuildAsync(
            _eventStore,
            new CourseProjection(courseId)
        );
        
        if (model.State.SubscriptionCount >= model.State.Capacity)
            throw new CourseFullException(courseId);
        
        // No internal state mutation - just append with condition
        await _eventStore.AppendAsync(
            new StudentSubscribedToCourse(studentId, courseId),
            model.AppendCondition
        );
    }
}
```

**Why it matters**: DCB decision models are ephemeral. Building stateful aggregate objects with cached state defeats the retry mechanism and introduces stale data bugs.

---

## Ignoring Eventual Consistency Where Appropriate

### ❌ Bad: Strong Consistency for Everything

```csharp
public class OverlyStrictHandler
{
    public async Task UpdateStudentProfile(string studentId, string newEmail)
    {
        // BAD: Why does updating email need to check enrollments?
        var (states, condition) = await MultiProjectionDecisionModel.BuildAsync(
            _eventStore,
            ("profile", new StudentProfileProjection(studentId)),
            ("enrollments", new StudentEnrollmentsProjection(studentId)),  // Unnecessary!
            ("payments", new StudentPaymentsProjection(studentId))         // Unnecessary!
        );
        
        await _eventStore.AppendAsync(
            new StudentEmailUpdated(studentId, newEmail),
            condition
        );
    }
}
```

### ✅ Good: Right Consistency for the Operation

```csharp
public class RightConsistencyHandler
{
    public async Task UpdateStudentProfile(string studentId, string newEmail)
    {
        // GOOD: Only check what this operation actually needs
        var model = await DecisionModelBuilder.BuildAsync(
            _eventStore,
            new StudentExistsProjection(studentId)
        );
        
        if (!model.State)
            throw new StudentNotFoundException(studentId);
        
        await _eventStore.AppendAsync(
            new StudentEmailUpdated(studentId, newEmail),
            model.AppendCondition
        );
    }
}
```

**Why it matters**: DCB makes strong consistency easier, but strong consistency isn't always needed. Compose only the projections that guard invariants relevant to the operation.

---

## Leaking Tags into Business Logic

### ❌ Bad: Business Logic Knows About Tags

```csharp
public class TagLeakingHandler
{
    public async Task SubscribeStudentToCourse(string studentId, string courseId)
    {
        // BAD: Business logic constructs queries manually
        var query = new DcbQuery(
            new QueryItem(Tags: [$"student:{studentId}"]),
            new QueryItem(Tags: [$"course:{courseId}"])
        );
        
        var events = await _eventStore.ReadAsync(query).ToListAsync();
        
        // BAD: Manual state calculation
        var subscriptionCount = events.Count(e => e.Event is StudentSubscribedToCourse);
        
        // Business rules mixed with infrastructure concerns
        if (subscriptionCount >= 10)
            throw new TooManyEnrollmentsException();
    }
}
```

### ✅ Good: Projections Encapsulate Tag Logic

```csharp
public class CleanHandler
{
    public async Task SubscribeStudentToCourse(string studentId, string courseId)
    {
        // GOOD: Projections encapsulate tag and type filtering
        var model = await DecisionModelBuilder.BuildAsync(
            _eventStore,
            new StudentSubscriptionCountProjection(studentId)
        );
        
        // Business logic only sees domain state
        if (model.State >= 10)
            throw new TooManyEnrollmentsException(studentId);
    }
}

// Tag logic is encapsulated in the projection
public class StudentSubscriptionCountProjection : IDcbProjection<int>
{
    private readonly string _studentId;
    
    public StudentSubscriptionCountProjection(string studentId) => _studentId = studentId;
    
    public IReadOnlySet<string> TagFilter => [$"student:{_studentId}"];
    // ... rest of projection
}
```

**Why it matters**: Tag construction is infrastructure. Business logic should work with domain concepts, not tag strings.

---

## Monolithic Projections

### ❌ Bad: One Projection Does Everything

```csharp
// BAD: God projection that tracks all possible state
public class EverythingProjection : IDcbProjection<EverythingState>
{
    public IReadOnlySet<string> TagFilter => [];  // All tags!
    public IReadOnlySet<string>? TypeFilter => null;  // All types!
    
    public EverythingState Apply(EverythingState state, IDcbEvent @event)
    {
        // Handles every event type in the system
        return @event switch
        {
            CourseDefined e => HandleCourseDefined(state, e),
            StudentRegistered e => HandleStudentRegistered(state, e),
            PaymentReceived e => HandlePaymentReceived(state, e),
            // ... 50 more cases
        };
    }
}
```

### ✅ Good: Small, Composable Projections

```csharp
// GOOD: Single-purpose projections
public class CourseExistsProjection : IDcbProjection<bool> { /* ... */ }
public class CourseCapacityProjection : IDcbProjection<int> { /* ... */ }
public class CourseSubscriptionCountProjection : IDcbProjection<int> { /* ... */ }
public class StudentSubscriptionCountProjection : IDcbProjection<int> { /* ... */ }

// Compose only what you need
var (states, condition) = await MultiProjectionDecisionModel.BuildAsync(
    _eventStore,
    ("exists", new CourseExistsProjection(courseId)),
    ("capacity", new CourseCapacityProjection(courseId))
);
```

**Why it matters**: Small projections enable precise consistency boundaries. Monolithic projections force broad queries that cause unnecessary conflicts.

---

## Summary: DCB Success Checklist

✅ **Tags**: Multiple granular tags per event  
✅ **Queries**: Precisely scoped to needed invariants  
✅ **Retry**: Always wrap appends in retry logic  
✅ **Events**: One fact = one event (no duplication)  
✅ **Models**: Stateless, temporary decision models  
✅ **Consistency**: Match consistency level to actual requirements  
✅ **Encapsulation**: Tags stay in projections, not business logic  
✅ **Projections**: Small, focused, composable  
