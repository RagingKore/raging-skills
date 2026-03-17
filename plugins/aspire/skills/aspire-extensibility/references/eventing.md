# Aspire Eventing API Reference

Complete reference for the .NET Aspire eventing system targeting Aspire 13.x on .NET 10.

## Event Hierarchy

### AppHost Events (fire once for the entire application)

| Event                             | When                                        | Common Use                            |
|-----------------------------------|---------------------------------------------|---------------------------------------|
| `BeforeStartEvent`                | Before any resource starts                  | Validation, dynamic registration      |
| `ResourceEndpointsAllocatedEvent` | After all endpoints are allocated           | Endpoint inspection, late config      |
| `AfterResourcesCreatedEvent`      | After all resources are created and running | Post-startup tasks, integration tests |

### Resource Events (fire per resource)

| Event                             | When                                  | Common Use                      |
|-----------------------------------|---------------------------------------|---------------------------------|
| `InitializeResourceEvent`         | Resource is being initialized         | Custom init logic               |
| `ResourceEndpointsAllocatedEvent` | Endpoints allocated for this resource | Endpoint-aware configuration    |
| `ConnectionStringAvailableEvent`  | Connection string resolved            | Validate or log connection info |
| `BeforeResourceStartedEvent`      | Resource is about to start            | Last-chance configuration       |
| `ResourceReadyEvent`              | Resource started and healthy          | Post-ready actions, seeding     |

### Event Ordering

```
AppHost level:
  BeforeStartEvent
    -> ResourceEndpointsAllocatedEvent
      -> AfterResourcesCreatedEvent

Per resource (within the AppHost sequence):
  InitializeResourceEvent
    -> ResourceEndpointsAllocatedEvent
      -> ConnectionStringAvailableEvent
        -> BeforeResourceStartedEvent
          -> ResourceReadyEvent
```

## Subscribing to Events

### AppHost-Level Subscription

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Subscribe to an AppHost event
builder.Eventing.Subscribe<BeforeStartEvent>((@event, ct) =>
{
    var logger = @event.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Application is starting...");
    return Task.CompletedTask;
});

builder.Eventing.Subscribe<AfterResourcesCreatedEvent>((@event, ct) =>
{
    var logger = @event.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("All resources created successfully");
    return Task.CompletedTask;
});
```

### Resource-Level Subscription (Eventing API)

```csharp
var db = builder.AddPostgres("pg").AddDatabase("catalog");

// Subscribe to events for a specific resource
builder.Eventing.Subscribe<ResourceReadyEvent>(
    db.Resource,
    async (@event, ct) =>
    {
        var logger = @event.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Database {Name} is ready", @event.Resource.Name);
    });

builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(
    db.Resource,
    async (@event, ct) =>
    {
        // Connection string is now available for this resource
    });
```

### Chainable Convenience Methods

These methods are extensions on `IResourceBuilder<T>` and provide a fluent API for subscribing to resource events:

```csharp
var db = builder.AddPostgres("pg")
    .OnInitializeResource(async (@event, ct) =>
    {
        // Resource initialization
    })
    .OnResourceEndpointsAllocated(async (@event, ct) =>
    {
        // Endpoints allocated
    })
    .OnConnectionStringAvailable(async (@event, ct) =>
    {
        // Connection string resolved
        var connStr = await @event.Resource.GetConnectionStringAsync(ct);
    })
    .OnBeforeResourceStarted(async (@event, ct) =>
    {
        // About to start
    })
    .OnResourceReady(async (@event, ct) =>
    {
        // Running and healthy
    });
```

## Event Dispatch Behaviors

When publishing events, control how handlers execute:

```csharp
public enum EventDispatchBehavior
{
    /// <summary>
    /// Handlers execute one at a time in subscription order. Publisher awaits completion.
    /// This is the default behavior.
    /// </summary>
    BlockingSequential,

    /// <summary>
    /// All handlers execute concurrently. Publisher awaits all completions.
    /// </summary>
    BlockingConcurrent,

    /// <summary>
    /// Handlers execute one at a time in subscription order. Publisher does not wait.
    /// </summary>
    NonBlockingSequential,

    /// <summary>
    /// All handlers execute concurrently. Publisher does not wait.
    /// </summary>
    NonBlockingConcurrent
}
```

### Choosing a Dispatch Behavior

| Scenario                                 | Recommended Behavior      | Reason                                     |
|------------------------------------------|---------------------------|--------------------------------------------|
| Migrations that must complete first      | `BlockingSequential`      | Ordered, must finish before proceeding     |
| Independent validations                  | `BlockingConcurrent`      | Parallel checks, await all results         |
| Telemetry / logging notifications        | `NonBlockingConcurrent`   | Fire-and-forget, no ordering needed        |
| Audit log entries                        | `NonBlockingSequential`   | Fire-and-forget but maintain order         |

## Custom Events

### Defining AppHost-Level Events

```csharp
/// <summary>
/// Raised after database migrations complete successfully.
/// </summary>
public sealed class MigrationsCompletedEvent : IDistributedApplicationEvent
{
    public required string DatabaseName { get; init; }
    public required string TargetVersion { get; init; }
    public required TimeSpan Duration { get; init; }
}
```

### Defining Resource-Level Events

```csharp
/// <summary>
/// Raised when a resource completes its warm-up sequence.
/// </summary>
public sealed class ResourceWarmedUpEvent : IDistributedApplicationResourceEvent
{
    public required IResource Resource { get; init; }
    public required int CacheEntriesPreloaded { get; init; }
}
```

### Publishing Custom Events

```csharp
// Publish an AppHost-level event
await builder.Eventing.PublishAsync(
    new MigrationsCompletedEvent
    {
        DatabaseName = "catalog",
        TargetVersion = "20250301_AddProductIndex",
        Duration = TimeSpan.FromSeconds(12)
    },
    EventDispatchBehavior.BlockingSequential,
    cancellationToken);

// Publish a resource-level event
await builder.Eventing.PublishAsync(
    new ResourceWarmedUpEvent
    {
        Resource = cacheResource,
        CacheEntriesPreloaded = 5000
    },
    EventDispatchBehavior.NonBlockingConcurrent,
    cancellationToken);
```

### Subscribing to Custom Events

```csharp
// AppHost-level custom event
builder.Eventing.Subscribe<MigrationsCompletedEvent>((@event, ct) =>
{
    Console.WriteLine(
        $"Migrations for {event.DatabaseName} completed in {event.Duration.TotalSeconds}s");
    return Task.CompletedTask;
});

// Resource-level custom event (for a specific resource)
builder.Eventing.Subscribe<ResourceWarmedUpEvent>(
    cacheResource,
    (@event, ct) =>
    {
        Console.WriteLine($"Cache preloaded {event.CacheEntriesPreloaded} entries");
        return Task.CompletedTask;
    });
```

## Event Subscribers for Extension Libraries

### Implementing IDistributedApplicationEventingSubscriber

Use this interface when building reusable NuGet packages or extension libraries that need to hook into Aspire events:

```csharp
public sealed class AutoMigrationSubscriber : IDistributedApplicationEventingSubscriber
{
    public void Subscribe(
        IDistributedApplicationEventing eventing,
        CancellationToken cancellationToken = default)
    {
        // Subscribe to multiple events in one place
        eventing.Subscribe<BeforeStartEvent>(async (@event, ct) =>
        {
            var model = @event.Services
                .GetRequiredService<DistributedApplicationModel>();

            // Find all database resources and run migrations
            foreach (var resource in model.Resources)
            {
                if (resource is IResourceWithConnectionString dbResource)
                {
                    var connectionString = await dbResource
                        .GetConnectionStringAsync(ct);

                    if (connectionString is not null)
                    {
                        // Run EF Core migrations or other migration tool
                        await RunMigrationsAsync(connectionString, ct);
                    }
                }
            }
        });

        eventing.Subscribe<AfterResourcesCreatedEvent>((@event, ct) =>
        {
            // Verify migration state after all resources are up
            return Task.CompletedTask;
        });
    }

    private static async Task RunMigrationsAsync(
        string connectionString,
        CancellationToken ct)
    {
        // Migration logic here
    }
}
```

### Registering Subscribers

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Register the subscriber - its Subscribe method is called automatically
builder.AddEventingSubscriber<AutoMigrationSubscriber>();

// Multiple subscribers can be registered
builder.AddEventingSubscriber<TelemetryInitSubscriber>();
builder.AddEventingSubscriber<HealthCheckSubscriber>();
```

### Subscriber with Dependency Injection

```csharp
public sealed class NotificationSubscriber(
    ILogger<NotificationSubscriber> logger,
    INotificationService notifications)
    : IDistributedApplicationEventingSubscriber
{
    public void Subscribe(
        IDistributedApplicationEventing eventing,
        CancellationToken cancellationToken = default)
    {
        eventing.Subscribe<AfterResourcesCreatedEvent>(async (@event, ct) =>
        {
            logger.LogInformation("All resources created, sending notification");
            await notifications.SendAsync("Aspire app started successfully", ct);
        });
    }
}
```

## Complete Example: Database Seeding Pipeline

This example demonstrates combining multiple event types to create a database seeding pipeline
that runs after migrations:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("pg")
    .AddDatabase("catalog");

var migrator = builder.AddProject<Projects.DbMigrator>("migrator")
    .WithReference(db)
    .WaitFor(db);

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WaitForCompletion(migrator);

// Seed the database after the migrator completes and API is ready
builder.Eventing.Subscribe<ResourceReadyEvent>(
    api.Resource,
    async (@event, ct) =>
    {
        var logger = @event.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("API is ready, checking if seeding is needed...");

        // Use the API's health endpoint or database to check seed state
        // Then publish a custom event when seeding completes
        await @event.Services
            .GetRequiredService<IDistributedApplicationEventing>()
            .PublishAsync(
                new DatabaseSeededEvent { DatabaseName = "catalog", RecordCount = 500 },
                cancellationToken: ct);
    });

builder.Eventing.Subscribe<DatabaseSeededEvent>((@event, ct) =>
{
    var logger = @event.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation(
        "Database {Db} seeded with {Count} records",
        @event.DatabaseName,
        @event.RecordCount);
    return Task.CompletedTask;
});

builder.Build().Run();

// Custom event definition
public sealed class DatabaseSeededEvent : IDistributedApplicationEvent
{
    public required string DatabaseName { get; init; }
    public required int RecordCount { get; init; }
}
```

## Migration from IDistributedApplicationLifecycleHook

The legacy `IDistributedApplicationLifecycleHook` interface is replaced by the eventing system. Migrate as follows:

| Legacy Hook Method                  | Eventing Equivalent                                          |
|-------------------------------------|--------------------------------------------------------------|
| `BeforeStartAsync`                  | `Subscribe<BeforeStartEvent>`                                |
| `AfterEndpointsAllocatedAsync`      | `Subscribe<ResourceEndpointsAllocatedEvent>`                 |
| `AfterResourcesCreatedAsync`        | `Subscribe<AfterResourcesCreatedEvent>`                      |

Before (legacy):

```csharp
public class MyHook : IDistributedApplicationLifecycleHook
{
    public Task BeforeStartAsync(
        DistributedApplicationModel model,
        CancellationToken ct) => Task.CompletedTask;
}

builder.Services.AddSingleton<IDistributedApplicationLifecycleHook, MyHook>();
```

After (eventing):

```csharp
builder.Eventing.Subscribe<BeforeStartEvent>((@event, ct) =>
{
    var model = @event.Services.GetRequiredService<DistributedApplicationModel>();
    // Same logic as BeforeStartAsync
    return Task.CompletedTask;
});
```
