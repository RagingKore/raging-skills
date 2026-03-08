# DCB Implementation Patterns

Complete C#/.NET 9 patterns for implementing Dynamic Consistency Boundary.

## Table of Contents

1. [Core Abstractions](#core-abstractions)
2. [Event Definitions](#event-definitions)
3. [Projection Patterns](#projection-patterns)
4. [Decision Model Builder](#decision-model-builder)
5. [Command Handler Patterns](#command-handler-patterns)
6. [Retry Logic](#retry-logic)
7. [Testing Patterns](#testing-patterns)

## Core Abstractions

### Event Interface

```csharp
public interface IDcbEvent
{
    string Type { get; }
    IReadOnlySet<string> Tags { get; }
    DateTimeOffset Timestamp { get; }
}

public record DcbEventEnvelope(
    long SequencePosition,
    IDcbEvent Event
);
```

### Query Types

```csharp
public record QueryItem(
    IReadOnlySet<string>? Types = null,
    IReadOnlySet<string>? Tags = null
)
{
    public bool Matches(IDcbEvent @event)
    {
        var typeMatches = Types is null || Types.Contains(@event.Type);
        var tagsMatch = Tags is null || Tags.IsSubsetOf(@event.Tags);
        return typeMatches && tagsMatch;
    }
}

public record DcbQuery(params QueryItem[] Items)
{
    public bool Matches(IDcbEvent @event) => Items.Any(item => item.Matches(@event));
    
    public static DcbQuery Combine(params DcbQuery[] queries) =>
        new(queries.SelectMany(q => q.Items).ToArray());
}
```

### Append Condition

```csharp
public record AppendCondition(
    DcbQuery FailIfEventsMatch,
    long After
);

public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(long expectedPosition, long actualPosition)
        : base($"Expected position {expectedPosition}, but store is at {actualPosition}") { }
}
```

### Event Store Interface

```csharp
public interface IDcbEventStore
{
    IAsyncEnumerable<DcbEventEnvelope> ReadAsync(
        DcbQuery query,
        long afterPosition = 0,
        CancellationToken ct = default);
    
    Task<long> AppendAsync(
        IDcbEvent @event,
        AppendCondition? condition = null,
        CancellationToken ct = default);
    
    Task<long> AppendAsync(
        IEnumerable<IDcbEvent> events,
        AppendCondition? condition = null,
        CancellationToken ct = default);
}
```

## Event Definitions

### Record-Based Events with Tags

```csharp
// Course domain events
public record CourseDefined(
    string CourseId,
    string Title,
    int Capacity
) : IDcbEvent
{
    public string Type => nameof(CourseDefined);
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlySet<string> Tags => new HashSet<string> { $"course:{CourseId}" };
}

public record CourseCapacityChanged(
    string CourseId,
    int NewCapacity
) : IDcbEvent
{
    public string Type => nameof(CourseCapacityChanged);
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlySet<string> Tags => new HashSet<string> { $"course:{CourseId}" };
}

// Cross-entity event with multiple tags
public record StudentSubscribedToCourse(
    string StudentId,
    string CourseId
) : IDcbEvent
{
    public string Type => nameof(StudentSubscribedToCourse);
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlySet<string> Tags => new HashSet<string>
    {
        $"student:{StudentId}",
        $"course:{CourseId}",
        $"enrollment:{StudentId}-{CourseId}"  // Optional: compound tag for direct lookup
    };
}

public record StudentUnsubscribedFromCourse(
    string StudentId,
    string CourseId,
    string Reason
) : IDcbEvent
{
    public string Type => nameof(StudentUnsubscribedFromCourse);
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlySet<string> Tags => new HashSet<string>
    {
        $"student:{StudentId}",
        $"course:{CourseId}",
        $"enrollment:{StudentId}-{CourseId}"
    };
}
```

### Source Generator Pattern (Optional)

```csharp
// Attribute-based tag generation (conceptual - implement as source generator)
[DcbEvent]
public partial record StudentSubscribedToCourse(
    [Tag("student")] string StudentId,
    [Tag("course")] string CourseId
);

// Generated code would be:
public partial record StudentSubscribedToCourse : IDcbEvent
{
    public string Type => nameof(StudentSubscribedToCourse);
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlySet<string> Tags => new HashSet<string>
    {
        $"student:{StudentId}",
        $"course:{CourseId}"
    };
}
```

## Projection Patterns

### Base Projection Interface

```csharp
public interface IDcbProjection<TState>
{
    TState InitialState { get; }
    IReadOnlySet<string> TagFilter { get; }
    IReadOnlySet<string>? TypeFilter { get; }  // null = all types
    TState Apply(TState state, IDcbEvent @event);
    
    DcbQuery ToQuery() => new(new QueryItem(TypeFilter, TagFilter));
}
```

### Concrete Projections

```csharp
// Boolean existence check
public class CourseExistsProjection : IDcbProjection<bool>
{
    private readonly string _courseId;
    
    public CourseExistsProjection(string courseId) => _courseId = courseId;
    
    public bool InitialState => false;
    public IReadOnlySet<string> TagFilter => [$"course:{_courseId}"];
    public IReadOnlySet<string>? TypeFilter => [nameof(CourseDefined)];
    
    public bool Apply(bool state, IDcbEvent @event) => @event switch
    {
        CourseDefined => true,
        _ => state
    };
}

// Numeric counter
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

// Complex state
public record CourseState(
    bool Exists,
    string? Title,
    int Capacity,
    int SubscriptionCount
);

public class CourseProjection : IDcbProjection<CourseState>
{
    private readonly string _courseId;
    
    public CourseProjection(string courseId) => _courseId = courseId;
    
    public CourseState InitialState => new(false, null, 0, 0);
    public IReadOnlySet<string> TagFilter => [$"course:{_courseId}"];
    public IReadOnlySet<string>? TypeFilter => null;  // All types with this tag
    
    public CourseState Apply(CourseState state, IDcbEvent @event) => @event switch
    {
        CourseDefined e => state with { Exists = true, Title = e.Title, Capacity = e.Capacity },
        CourseCapacityChanged e => state with { Capacity = e.NewCapacity },
        StudentSubscribedToCourse => state with { SubscriptionCount = state.SubscriptionCount + 1 },
        StudentUnsubscribedFromCourse => state with { SubscriptionCount = state.SubscriptionCount - 1 },
        _ => state
    };
}

// Set-based state
public class StudentEnrollmentsProjection : IDcbProjection<ImmutableHashSet<string>>
{
    private readonly string _studentId;
    
    public StudentEnrollmentsProjection(string studentId) => _studentId = studentId;
    
    public ImmutableHashSet<string> InitialState => [];
    public IReadOnlySet<string> TagFilter => [$"student:{_studentId}"];
    public IReadOnlySet<string>? TypeFilter => 
        [nameof(StudentSubscribedToCourse), nameof(StudentUnsubscribedFromCourse)];
    
    public ImmutableHashSet<string> Apply(ImmutableHashSet<string> state, IDcbEvent @event) => @event switch
    {
        StudentSubscribedToCourse e => state.Add(e.CourseId),
        StudentUnsubscribedFromCourse e => state.Remove(e.CourseId),
        _ => state
    };
}
```

## Decision Model Builder

### Generic Decision Model

```csharp
public record DecisionModel<TState>(
    TState State,
    AppendCondition AppendCondition,
    long LastPosition
);

public static class DecisionModelBuilder
{
    public static async Task<DecisionModel<TState>> BuildAsync<TState>(
        IDcbEventStore eventStore,
        IDcbProjection<TState> projection,
        CancellationToken ct = default)
    {
        var state = projection.InitialState;
        long lastPosition = 0;
        
        await foreach (var envelope in eventStore.ReadAsync(projection.ToQuery(), ct: ct))
        {
            state = projection.Apply(state, envelope.Event);
            lastPosition = envelope.SequencePosition;
        }
        
        return new DecisionModel<TState>(
            state,
            new AppendCondition(projection.ToQuery(), lastPosition),
            lastPosition
        );
    }
}
```

### Multi-Projection Decision Model

```csharp
public static class MultiProjectionDecisionModel
{
    public static async Task<(TState1 S1, TState2 S2, AppendCondition Condition)> BuildAsync<TState1, TState2>(
        IDcbEventStore eventStore,
        IDcbProjection<TState1> p1,
        IDcbProjection<TState2> p2,
        CancellationToken ct = default)
    {
        var state1 = p1.InitialState;
        var state2 = p2.InitialState;
        long lastPosition = 0;
        
        var combinedQuery = DcbQuery.Combine(p1.ToQuery(), p2.ToQuery());
        
        await foreach (var envelope in eventStore.ReadAsync(combinedQuery, ct: ct))
        {
            if (p1.ToQuery().Matches(envelope.Event))
                state1 = p1.Apply(state1, envelope.Event);
            if (p2.ToQuery().Matches(envelope.Event))
                state2 = p2.Apply(state2, envelope.Event);
            lastPosition = envelope.SequencePosition;
        }
        
        return (state1, state2, new AppendCondition(combinedQuery, lastPosition));
    }
    
    // Extension for arbitrary number of projections using dynamic
    public static async Task<(Dictionary<string, object> States, AppendCondition Condition)> BuildAsync(
        IDcbEventStore eventStore,
        params (string Name, object Projection)[] projections)
    {
        var states = new Dictionary<string, object>();
        var queries = new List<DcbQuery>();
        long lastPosition = 0;
        
        // Initialize states and collect queries
        foreach (var (name, projection) in projections)
        {
            var projType = projection.GetType();
            var initialState = projType.GetProperty("InitialState")!.GetValue(projection);
            states[name] = initialState!;
            
            var toQuery = projType.GetMethod("ToQuery")!;
            queries.Add((DcbQuery)toQuery.Invoke(projection, null)!);
        }
        
        var combinedQuery = DcbQuery.Combine(queries.ToArray());
        
        await foreach (var envelope in eventStore.ReadAsync(combinedQuery))
        {
            for (int i = 0; i < projections.Length; i++)
            {
                var (name, projection) = projections[i];
                if (queries[i].Matches(envelope.Event))
                {
                    var applyMethod = projection.GetType().GetMethod("Apply")!;
                    states[name] = applyMethod.Invoke(projection, [states[name], envelope.Event])!;
                }
            }
            lastPosition = envelope.SequencePosition;
        }
        
        return (states, new AppendCondition(combinedQuery, lastPosition));
    }
}
```

## Command Handler Patterns

### Basic Command Handler

```csharp
public class CourseCommandHandler
{
    private readonly IDcbEventStore _eventStore;
    
    public CourseCommandHandler(IDcbEventStore eventStore) => _eventStore = eventStore;
    
    public async Task<string> DefineCourse(string title, int capacity, CancellationToken ct = default)
    {
        var courseId = Guid.NewGuid().ToString();
        
        await _eventStore.AppendAsync(
            new CourseDefined(courseId, title, capacity),
            ct: ct
        );
        
        return courseId;
    }
    
    public async Task ChangeCapacity(string courseId, int newCapacity, CancellationToken ct = default)
    {
        var model = await DecisionModelBuilder.BuildAsync(
            _eventStore,
            new CourseExistsProjection(courseId),
            ct
        );
        
        if (!model.State)
            throw new CourseNotFoundException(courseId);
        
        await _eventStore.AppendAsync(
            new CourseCapacityChanged(courseId, newCapacity),
            model.AppendCondition,
            ct
        );
    }
}
```

### Cross-Entity Command Handler

```csharp
public class EnrollmentCommandHandler
{
    private readonly IDcbEventStore _eventStore;
    private readonly IRetryPolicy _retryPolicy;
    
    public EnrollmentCommandHandler(IDcbEventStore eventStore, IRetryPolicy retryPolicy)
    {
        _eventStore = eventStore;
        _retryPolicy = retryPolicy;
    }
    
    public async Task SubscribeStudentToCourse(
        string studentId,
        string courseId,
        CancellationToken ct = default)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            // Build decision model with all relevant projections
            var (states, condition) = await MultiProjectionDecisionModel.BuildAsync(
                _eventStore,
                ("courseExists", new CourseExistsProjection(courseId)),
                ("courseCapacity", new CourseCapacityProjection(courseId)),
                ("courseSubscriptions", new CourseSubscriptionCountProjection(courseId)),
                ("studentSubscriptions", new StudentSubscriptionCountProjection(studentId)),
                ("studentEnrollments", new StudentEnrollmentsProjection(studentId))
            );
            
            // Enforce invariants
            if (!(bool)states["courseExists"])
                throw new CourseNotFoundException(courseId);
            
            var enrollments = (ImmutableHashSet<string>)states["studentEnrollments"];
            if (enrollments.Contains(courseId))
                throw new AlreadyEnrolledException(studentId, courseId);
            
            var capacity = (int)states["courseCapacity"];
            var courseSubscriptions = (int)states["courseSubscriptions"];
            if (courseSubscriptions >= capacity)
                throw new CourseFullException(courseId);
            
            var studentSubscriptions = (int)states["studentSubscriptions"];
            if (studentSubscriptions >= 10)
                throw new TooManyEnrollmentsException(studentId);
            
            // Append with condition
            await _eventStore.AppendAsync(
                new StudentSubscribedToCourse(studentId, courseId),
                condition,
                ct
            );
        }, ct);
    }
}
```

## Retry Logic

### Retry Policy for Concurrency Conflicts

```csharp
public interface IRetryPolicy
{
    Task ExecuteAsync(Func<Task> operation, CancellationToken ct = default);
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default);
}

public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;
    
    public ExponentialBackoffRetryPolicy(int maxRetries = 3, TimeSpan? baseDelay = null)
    {
        _maxRetries = maxRetries;
        _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(50);
    }
    
    public async Task ExecuteAsync(Func<Task> operation, CancellationToken ct = default)
    {
        var attempt = 0;
        
        while (true)
        {
            try
            {
                await operation();
                return;
            }
            catch (ConcurrencyConflictException) when (attempt < _maxRetries)
            {
                attempt++;
                var delay = _baseDelay * Math.Pow(2, attempt - 1);
                await Task.Delay(delay, ct);
            }
        }
    }
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
    {
        var attempt = 0;
        
        while (true)
        {
            try
            {
                return await operation();
            }
            catch (ConcurrencyConflictException) when (attempt < _maxRetries)
            {
                attempt++;
                var delay = _baseDelay * Math.Pow(2, attempt - 1);
                await Task.Delay(delay, ct);
            }
        }
    }
}
```

## Testing Patterns

### In-Memory Event Store for Testing

```csharp
public class InMemoryDcbEventStore : IDcbEventStore
{
    private readonly List<DcbEventEnvelope> _events = [];
    private readonly object _lock = new();
    
    public async IAsyncEnumerable<DcbEventEnvelope> ReadAsync(
        DcbQuery query,
        long afterPosition = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        List<DcbEventEnvelope> snapshot;
        lock (_lock)
        {
            snapshot = _events.ToList();
        }
        
        foreach (var envelope in snapshot.Where(e => 
            e.SequencePosition > afterPosition && query.Matches(e.Event)))
        {
            yield return envelope;
        }
    }
    
    public Task<long> AppendAsync(
        IDcbEvent @event,
        AppendCondition? condition = null,
        CancellationToken ct = default)
        => AppendAsync([@event], condition, ct);
    
    public Task<long> AppendAsync(
        IEnumerable<IDcbEvent> events,
        AppendCondition? condition = null,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (condition is not null)
            {
                var conflicting = _events
                    .Where(e => e.SequencePosition > condition.After)
                    .Any(e => condition.FailIfEventsMatch.Matches(e.Event));
                
                if (conflicting)
                    throw new ConcurrencyConflictException(condition.After, _events.Count);
            }
            
            foreach (var @event in events)
            {
                _events.Add(new DcbEventEnvelope(_events.Count + 1, @event));
            }
            
            return Task.FromResult((long)_events.Count);
        }
    }
}
```

### TUnit Test Examples

```csharp
public class EnrollmentTests
{
    [Test]
    public async Task SubscribeStudentToCourse_WhenCourseHasCapacity_Succeeds()
    {
        // Arrange
        var store = new InMemoryDcbEventStore();
        var handler = new EnrollmentCommandHandler(store, new ExponentialBackoffRetryPolicy());
        
        await store.AppendAsync(new CourseDefined("c1", "DDD 101", 30));
        
        // Act
        await handler.SubscribeStudentToCourse("s1", "c1");
        
        // Assert
        var events = await store.ReadAsync(
            new DcbQuery(new QueryItem(Tags: [$"enrollment:s1-c1"]))
        ).ToListAsync();
        
        await Assert.That(events).HasCount().EqualTo(1);
        await Assert.That(events[0].Event).IsTypeOf<StudentSubscribedToCourse>();
    }
    
    [Test]
    public async Task SubscribeStudentToCourse_WhenCourseFull_ThrowsCourseFullException()
    {
        // Arrange
        var store = new InMemoryDcbEventStore();
        var handler = new EnrollmentCommandHandler(store, new ExponentialBackoffRetryPolicy());
        
        await store.AppendAsync(new CourseDefined("c1", "DDD 101", 1));
        await store.AppendAsync(new StudentSubscribedToCourse("s1", "c1"));
        
        // Act & Assert
        await Assert.ThrowsAsync<CourseFullException>(
            async () => await handler.SubscribeStudentToCourse("s2", "c1")
        );
    }
    
    [Test]
    public async Task ConcurrentSubscriptions_WithConflict_RetriesSuccessfully()
    {
        // Arrange
        var store = new InMemoryDcbEventStore();
        var handler = new EnrollmentCommandHandler(store, new ExponentialBackoffRetryPolicy(maxRetries: 5));
        
        await store.AppendAsync(new CourseDefined("c1", "DDD 101", 100));
        
        // Act - concurrent enrollments
        var tasks = Enumerable.Range(1, 10)
            .Select(i => handler.SubscribeStudentToCourse($"s{i}", "c1"));
        
        await Task.WhenAll(tasks);
        
        // Assert
        var enrollments = await store.ReadAsync(
            new DcbQuery(new QueryItem(
                Types: [nameof(StudentSubscribedToCourse)],
                Tags: ["course:c1"]
            ))
        ).ToListAsync();
        
        await Assert.That(enrollments).HasCount().EqualTo(10);
    }
}
```
