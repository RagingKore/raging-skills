---
name: aspire-extensibility
description: |
  .NET Aspire extensibility expert for the eventing system, custom resources, lifecycle hooks, dashboard
  configuration, and advanced customization. Covers the AppHost eventing API with
  builder.Eventing.Subscribe for BeforeStartEvent, ResourceEndpointsAllocatedEvent, and
  AfterResourcesCreatedEvent, resource-level events with chainable OnInitializeResource,
  OnResourceEndpointsAllocated, OnConnectionStringAvailable, OnBeforeResourceStarted, and OnResourceReady
  methods, custom event definitions implementing IDistributedApplicationEvent or
  IDistributedApplicationResourceEvent, event dispatch behaviors (BlockingSequential,
  BlockingConcurrent, NonBlockingSequential, NonBlockingConcurrent), IDistributedApplicationEventingSubscriber
  for library authors, custom resources extending IResource or ContainerResource, resource annotations,
  custom commands for the Aspire dashboard, and dashboard standalone configuration with OTLP endpoints
  and authentication modes (BrowserToken, ApiKey, Unsecured). Use when extending Aspire with custom
  resources, subscribing to Aspire lifecycle events, implementing IDistributedApplicationLifecycleHook,
  creating custom dashboard commands, configuring the Aspire dashboard standalone, adding resource
  annotations, defining custom Aspire events, handling BeforeStartEvent or AfterResourcesCreatedEvent,
  using resource eventing callbacks, or building Aspire extension libraries.
---

# .NET Aspire Extensibility

Eventing system, custom resources, resource annotations, dashboard configuration, and extension patterns
targeting Aspire 13.x on .NET 10.

## Quick Decision Matrix

| Need                                | API / Approach                                     | Notes                                        |
|-------------------------------------|----------------------------------------------------|----------------------------------------------|
| React to AppHost lifecycle          | `builder.Eventing.Subscribe<T>()`                  | BeforeStart, AfterResourcesCreated           |
| React to resource lifecycle         | `.OnResourceReady()`, `.OnBeforeResourceStarted()` | Chainable convenience methods                |
| Define custom events                | Implement `IDistributedApplicationEvent`           | Publish with `PublishAsync<T>()`             |
| Build extension library subscribers | `IDistributedApplicationEventingSubscriber`        | Register with `AddEventingSubscriber<T>()`   |
| Create custom resource types        | Extend `Resource` or `ContainerResource`           | Add builder extension methods                |
| Attach metadata to resources        | Resource annotations                               | Custom `IResourceAnnotation` implementations |
| Add dashboard commands              | Custom commands API                                | Appear in dashboard resource actions         |
| Run dashboard standalone            | Docker image with OTLP config                      | BrowserToken, ApiKey, or Unsecured auth      |

## Eventing System

The Aspire eventing system replaces the legacy `IDistributedApplicationLifecycleHook` interface. Subscribe to
strongly-typed events at the AppHost or resource level to hook into the application lifecycle.

### AppHost Events

AppHost-level events fire in this order:

1. **`BeforeStartEvent`** - Before any resource starts. Use for validation, dynamic resource registration, or
   environment setup.
2. **`ResourceEndpointsAllocatedEvent`** - After all endpoints are allocated but before resources start. Use for
   endpoint inspection or late configuration.
3. **`AfterResourcesCreatedEvent`** - After all resources are created and running. Use for post-startup tasks
   like health verification or integration testing triggers.

Subscribe to AppHost events on the builder:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.Eventing.Subscribe<BeforeStartEvent>((@event, ct) =>
{
    // Validate configuration, register dynamic resources
    return Task.CompletedTask;
});

builder.Eventing.Subscribe<AfterResourcesCreatedEvent>((@event, ct) =>
{
    // All resources running, perform post-startup actions
    return Task.CompletedTask;
});
```

### Resource Events

Resource-level events fire per resource in this order:

1. **`InitializeResourceEvent`** - Resource is being initialized
2. **`ResourceEndpointsAllocatedEvent`** - Endpoints allocated for this resource
3. **`ConnectionStringAvailableEvent`** - Connection string is resolved and available
4. **`BeforeResourceStartedEvent`** - Resource is about to start
5. **`ResourceReadyEvent`** - Resource is started and healthy

Use chainable convenience methods for concise resource-level subscriptions:

```csharp
var db = builder.AddPostgres("pg")
    .OnInitializeResource((@event, ct) =>
    {
        // Custom initialization logic
        return Task.CompletedTask;
    })
    .OnConnectionStringAvailable((@event, ct) =>
    {
        // Connection string resolved, log or validate
        return Task.CompletedTask;
    })
    .OnBeforeResourceStarted((@event, ct) =>
    {
        // Last chance before resource starts
        return Task.CompletedTask;
    })
    .OnResourceReady((@event, ct) =>
    {
        // Resource is running and healthy
        return Task.CompletedTask;
    });
```

Alternatively, subscribe to resource events through the eventing API with a resource filter:

```csharp
builder.Eventing.Subscribe<ResourceReadyEvent>(
    db.Resource,
    (@event, ct) =>
    {
        // Fires only for the "pg" resource
        return Task.CompletedTask;
    });
```

### Event Dispatch Behaviors

Control how event handlers execute when publishing events:

| Behavior                   | Execution        | Blocking | Use Case                              |
|----------------------------|------------------|----------|---------------------------------------|
| `BlockingSequential`       | One at a time    | Yes      | Default. Ordered, predictable         |
| `BlockingConcurrent`       | All at once      | Yes      | Parallel handlers, await completion   |
| `NonBlockingSequential`    | One at a time    | No       | Fire-and-forget, ordered              |
| `NonBlockingConcurrent`    | All at once      | No       | Fire-and-forget, parallel             |

Specify dispatch behavior when publishing custom events:

```csharp
await builder.Eventing.PublishAsync(
    new MyCustomEvent(),
    EventDispatchBehavior.BlockingConcurrent,
    cancellationToken);
```

## Custom Events

Define custom events by implementing the appropriate interface:

```csharp
// AppHost-level event (not tied to a specific resource)
public sealed class DatabaseSeededEvent : IDistributedApplicationEvent
{
    public required string DatabaseName { get; init; }
    public required int RecordCount { get; init; }
}

// Resource-level event (tied to a specific resource)
public sealed class ResourceMigratedEvent : IDistributedApplicationResourceEvent
{
    public required IResource Resource { get; init; }
    public required string MigrationVersion { get; init; }
}
```

Publish and subscribe to custom events:

```csharp
// Subscribe
builder.Eventing.Subscribe<DatabaseSeededEvent>((@event, ct) =>
{
    Console.WriteLine($"Database {event.DatabaseName} seeded with {event.RecordCount} records");
    return Task.CompletedTask;
});

// Publish (from an event handler or startup logic)
await builder.Eventing.PublishAsync(
    new DatabaseSeededEvent { DatabaseName = "catalog", RecordCount = 1000 },
    cancellationToken: ct);
```

## Event Subscribers for Libraries

Build reusable event subscribers for extension libraries using `IDistributedApplicationEventingSubscriber`:

```csharp
public sealed class DatabaseMigrationSubscriber : IDistributedApplicationEventingSubscriber
{
    public void Subscribe(
        IDistributedApplicationEventing eventing,
        CancellationToken cancellationToken = default)
    {
        eventing.Subscribe<BeforeStartEvent>((@event, ct) =>
        {
            // Run database migrations before any resource starts
            return Task.CompletedTask;
        });

        eventing.Subscribe<AfterResourcesCreatedEvent>((@event, ct) =>
        {
            // Verify migration state after resources are up
            return Task.CompletedTask;
        });
    }
}
```

Register the subscriber in the AppHost:

```csharp
builder.AddEventingSubscriber<DatabaseMigrationSubscriber>();
```

See [references/eventing.md](references/eventing.md) for the complete eventing API reference with all event
types, subscriber patterns, and full code examples.

## Custom Resources

Create custom resource types by extending the resource model. Define a resource class, then provide builder
extension methods:

```csharp
// Resource definition
public sealed class MailDevResource(string name) : ContainerResource(name)
{
    // Custom properties for your resource
    public EndpointReference SmtpEndpoint =>
        new(this, "smtp");

    public EndpointReference WebEndpoint =>
        new(this, "web");
}
```

Create builder extension methods for ergonomic AppHost usage:

```csharp
public static class MailDevResourceBuilderExtensions
{
    public static IResourceBuilder<MailDevResource> AddMailDev(
        this IDistributedApplicationBuilder builder,
        string name,
        int? smtpPort = null,
        int? webPort = null)
    {
        return builder.AddResource(new MailDevResource(name))
            .WithImage("maildev/maildev", "latest")
            .WithEndpoint(
                targetPort: 1025,
                port: smtpPort,
                scheme: "tcp",
                name: "smtp")
            .WithHttpEndpoint(
                targetPort: 1080,
                port: webPort,
                name: "web");
    }
}
```

Use the custom resource in the AppHost:

```csharp
var mail = builder.AddMailDev("mail");

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(mail.Resource.SmtpEndpoint);
```

## Resource Annotations

Attach metadata to resources using annotations. Implement `IResourceAnnotation` for custom metadata:

```csharp
public sealed class HealthCheckAnnotation(string healthCheckName) : IResourceAnnotation
{
    public string HealthCheckName { get; } = healthCheckName;
}
```

Add annotations to resources:

```csharp
var db = builder.AddPostgres("pg");
db.WithAnnotation(new HealthCheckAnnotation("pg-health"));
```

Read annotations from resources:

```csharp
builder.Eventing.Subscribe<BeforeStartEvent>((@event, ct) =>
{
    var model = @event.Services.GetRequiredService<DistributedApplicationModel>();

    foreach (var resource in model.Resources)
    {
        var annotations = resource.Annotations.OfType<HealthCheckAnnotation>();
        foreach (var annotation in annotations)
        {
            // Process health check annotations
        }
    }

    return Task.CompletedTask;
});
```

## Custom Commands

Add custom commands to resources that appear in the Aspire dashboard. Commands enable operators to trigger
actions directly from the dashboard UI:

```csharp
var db = builder.AddPostgres("pg");

db.WithCommand("seed", "Seed Database", async context =>
{
    // Execute seeding logic
    // context provides resource information and services
    return CommandResults.Success();
});
```

Commands appear as action buttons in the dashboard for the associated resource. Use commands for operational
tasks like database seeding, cache clearing, or diagnostic data collection.

## Dashboard Configuration

### Standalone Dashboard

Run the Aspire dashboard as a standalone container for monitoring applications not orchestrated by Aspire:

```bash
docker run --rm -it -p 18888:18888 -p 4317:18889 \
    -d --name aspire-dashboard \
    mcr.microsoft.com/dotnet/aspire-dashboard:9.0
```

### Authentication Modes

Configure dashboard authentication using the `Dashboard__Frontend__AuthMode` environment variable:

| Mode            | Value          | Notes                                                    |
|-----------------|----------------|----------------------------------------------------------|
| Browser token   | `BrowserToken` | Default. Token displayed in container logs               |
| API key         | `ApiKey`       | Set with `Dashboard__Frontend__ApiKey`                   |
| Unsecured       | `Unsecured`    | No authentication. Development only                      |

```bash
# Unsecured mode for local development
docker run --rm -it -p 18888:18888 -p 4317:18889 \
    -e DASHBOARD__FRONTEND__AUTHMODE=Unsecured \
    mcr.microsoft.com/dotnet/aspire-dashboard:9.0
```

### OTLP Configuration

Configure OTLP endpoints for telemetry ingestion:

| Variable                          | Purpose                    | Default                |
|-----------------------------------|----------------------------|------------------------|
| `DASHBOARD__OTLP__GRPC__ENDPOINT` | gRPC OTLP endpoint         | `http://0.0.0.0:18889` |
| `DASHBOARD__OTLP__HTTP__ENDPOINT` | HTTP OTLP endpoint         | Not set                |
| `DASHBOARD__OTLP__AUTHMODE`       | OTLP auth mode             | `Unsecured`            |
| `DASHBOARD__OTLP__PRIMARYAPIKEY`  | API key for OTLP ingestion | Not set                |

Point application OTLP exporters at the dashboard endpoint to send traces, metrics, and logs:

```csharp
// In app configuration
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddOtlpExporter(o =>
        o.Endpoint = new Uri("http://localhost:4317")))
    .WithMetrics(metrics => metrics.AddOtlpExporter(o =>
        o.Endpoint = new Uri("http://localhost:4317")));
```

## Learn More

| Topic                            | How to Find                                                                                           |
|----------------------------------|-------------------------------------------------------------------------------------------------------|
| Aspire eventing system           | `microsoft_docs_search(query=".NET Aspire eventing lifecycle hooks")`                                 |
| Custom resource development      | `microsoft_docs_search(query=".NET Aspire custom resource implementation")`                           |
| Dashboard standalone             | `microsoft_docs_search(query=".NET Aspire dashboard standalone container")`                           |
| Dashboard configuration          | `microsoft_docs_search(query=".NET Aspire dashboard configuration authentication")`                   |
| Resource annotations             | `microsoft_docs_search(query=".NET Aspire resource annotations metadata")`                            |
| Custom commands                  | `microsoft_docs_search(query=".NET Aspire custom commands dashboard")`                                |
