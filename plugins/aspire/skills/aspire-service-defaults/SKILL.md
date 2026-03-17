---
name: aspire-service-defaults
description: |
  .NET Aspire ServiceDefaults shared project expert for cross-cutting concerns. Covers the
  AddServiceDefaults extension method, OpenTelemetry configuration (logging, metrics, tracing with
  OTLP exporter), default health checks (/health readiness and /alive liveness endpoints), HTTP
  client resilience with AddStandardResilienceHandler, service discovery configuration for
  HttpClient, the ServiceDefaults project file with IsAspireSharedProject, and customization
  patterns for non-ASP.NET Core projects. Use when setting up service defaults, calling
  AddServiceDefaults, configuring OpenTelemetry in Aspire, adding health checks, setting up
  resilience handlers, configuring service discovery, creating or modifying a ServiceDefaults
  project, mapping /health or /alive endpoints, adding custom health checks, adding custom trace
  sources, or building a ServiceDefaults library for worker services and non-web projects.
---

# Aspire Service Defaults

Patterns for the shared ServiceDefaults project that centralizes cross-cutting concerns across all services in
an Aspire application targeting .NET 10.

## Overview

The ServiceDefaults project is a shared library referenced by every service in an Aspire application. It
configures OpenTelemetry, health checks, HTTP client resilience, and service discovery in a single
`AddServiceDefaults()` call. This eliminates duplicated boilerplate across API services, worker services, and
background jobs.

## Quick Decision Matrix

| Concern             | What ServiceDefaults configures                           | Customization point               |
|---------------------|-----------------------------------------------------------|-----------------------------------|
| Distributed tracing | OTLP exporter, ASP.NET Core + HttpClient instrumentation  | Add custom `ActivitySource` names |
| Metrics             | ASP.NET Core, HttpClient, runtime metrics                 | Add custom `Meter` names          |
| Structured logging  | OpenTelemetry log provider with OTLP export               | Standard `ILogger` configuration  |
| Health checks       | `/health` (readiness) and `/alive` (liveness)             | Register additional checks        |
| HTTP resilience     | Standard resilience handler on all `HttpClient` instances | Per-client override               |
| Service discovery   | Automatic resolution of Aspire resource names             | None needed                       |

## Project File

Create the ServiceDefaults project with the `IsAspireSharedProject` property and required package references:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsAspireSharedProject>true</IsAspireSharedProject>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="9.*" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.*" />
  </ItemGroup>

</Project>
```

The `IsAspireSharedProject` property tells the Aspire tooling this is a shared project, not an independently
runnable service. The `FrameworkReference` to `Microsoft.AspNetCore.App` provides access to ASP.NET Core APIs
(health check endpoints, Kestrel types) without making this an executable web project.

## AddServiceDefaults Extension Method

The single entry point that wires all cross-cutting concerns:

```csharp
public static IHostApplicationBuilder AddServiceDefaults(
    this IHostApplicationBuilder builder)
{
    builder.ConfigureOpenTelemetry();
    builder.AddDefaultHealthChecks();
    builder.Services.AddServiceDiscovery();

    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.AddStandardResilienceHandler();
        http.AddServiceDiscovery();
    });

    return builder;
}
```

This method:

1. Configures OpenTelemetry logging, metrics, and tracing with OTLP export
2. Registers default health check endpoints
3. Enables service discovery for all `HttpClient` instances
4. Adds the standard resilience handler (retry, circuit breaker, timeout) to all `HttpClient` instances

## OpenTelemetry Configuration

Configure the three telemetry pillars in a single helper:

```csharp
public static IHostApplicationBuilder ConfigureOpenTelemetry(
    this IHostApplicationBuilder builder)
{
    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
    });

    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();
        })
        .WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation(options =>
                options.Filter = httpContext =>
                    !httpContext.Request.Path.StartsWithSegments("/health")
                    && !httpContext.Request.Path.StartsWithSegments("/alive"))
                .AddHttpClientInstrumentation();
        });

    AddOpenTelemetryExporters(builder);

    return builder;
}
```

### OTLP Exporter

Conditionally add the OTLP exporter when the endpoint is configured (Aspire sets this automatically):

```csharp
private static void AddOpenTelemetryExporters(
    IHostApplicationBuilder builder)
{
    var useOtlp = !string.IsNullOrWhiteSpace(
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

    if (useOtlp)
    {
        builder.Services.AddOpenTelemetry()
            .UseOtlpExporter();
    }
}
```

### Key Design Decisions

| Decision                         | Rationale                                                       |
|----------------------------------|-----------------------------------------------------------------|
| Filter `/health` from traces     | Health probes generate noise; exclude them from tracing         |
| Conditional OTLP export          | Works outside Aspire (no exporter) and inside (auto-configured) |
| `IncludeFormattedMessage = true` | Ensures human-readable messages appear in the dashboard         |
| `IncludeScopes = true`           | Preserves logging scope context in telemetry                    |

## Health Checks

Register default health checks and map the endpoints:

```csharp
public static IHostApplicationBuilder AddDefaultHealthChecks(
    this IHostApplicationBuilder builder)
{
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

    return builder;
}
```

Map the health check endpoints in the request pipeline:

```csharp
public static WebApplication MapDefaultEndpoints(this WebApplication app)
{
    app.MapHealthChecks("/health");

    app.MapHealthChecks("/alive", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live")
    });

    return app;
}
```

| Endpoint   | Purpose    | What it checks                          |
|------------|------------|-----------------------------------------|
| `/health`  | Readiness  | All registered health checks            |
| `/alive`   | Liveness   | Only checks tagged `"live"`             |

Kubernetes and container orchestrators use `/alive` for liveness probes (restart if unhealthy) and `/health` for
readiness probes (remove from load balancer if not ready).

## Usage in Services

### API / Web Services

Call both `AddServiceDefaults()` and `MapDefaultEndpoints()`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add application services...
builder.Services.AddControllers();

var app = builder.Build();

app.MapDefaultEndpoints();

// Map application endpoints...
app.MapControllers();

app.Run();
```

### Worker Services

Call `AddServiceDefaults()` only. Worker services have no HTTP pipeline, so `MapDefaultEndpoints()` is not
applicable:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHostedService<OrderProcessor>();

var host = builder.Build();
await host.RunAsync();
```

> Worker services still get OpenTelemetry, resilience, and service discovery. Health check endpoints require a
> web host; add `WebApplication` hosting if health probes are needed on workers.

## Custom Health Checks

Add application-specific health checks alongside the defaults:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add a database connectivity check
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("catalog")!,
        name: "catalog-db",
        tags: ["ready"])
    .AddRedis(builder.Configuration.GetConnectionString("cache")!,
        name: "cache",
        tags: ["ready"]);
```

Custom checks registered after `AddServiceDefaults()` are merged with the default "self" check. Tag checks with
`"ready"` for readiness-only or `"live"` to include them in liveness probes.

## HTTP Client Resilience

`AddStandardResilienceHandler()` applies a layered resilience pipeline to every `HttpClient` created through
`IHttpClientFactory`. The pipeline includes five strategies executed in order:

| Strategy        | Default behavior                                       |
|-----------------|--------------------------------------------------------|
| Rate limiter    | Limits concurrent requests to avoid overwhelming hosts |
| Total timeout   | 30-second overall timeout for the entire pipeline      |
| Retry           | Up to 3 retries with exponential backoff + jitter      |
| Circuit breaker | Opens after consecutive failures, resets after 30s     |
| Attempt timeout | 10-second timeout per individual attempt               |

Override defaults for a specific named client:

```csharp
builder.Services.AddHttpClient("catalog-api")
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 5;
        options.Retry.Delay = TimeSpan.FromMilliseconds(500);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
    });
```

Disable resilience for a specific client when retries are inappropriate (e.g., non-idempotent writes):

```csharp
builder.Services.AddHttpClient("payment-api")
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 0;
    });
```

## Service Discovery Details

Service discovery resolves logical resource names (`https+http://api`) to physical endpoints at runtime.
The resolution chain:

1. **Configuration** - Check `services:{name}` in `appsettings.json` or environment variables
2. **Aspire environment variables** - Read endpoint URLs injected by the AppHost
3. **DNS** - Fall back to DNS resolution

Configure resolution schemes in `appsettings.json`:

```json
{
  "Services": {
    "api": {
      "https": ["https://api.prod.internal:5001"],
      "http": ["http://api.prod.internal:5000"]
    }
  }
}
```

The `https+http://` scheme prefix means "prefer HTTPS, fall back to HTTP." Use `http://` for plain HTTP only.
Service discovery integrates automatically with `HttpClient` through the `AddServiceDiscovery()` call in
`ConfigureHttpClientDefaults`.

## Custom Trace Sources

Register additional `ActivitySource` names for application-specific tracing:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("MyApp.Orders")
            .AddSource("MyApp.Payments");
    });
```

Then create activities in application code:

```csharp
private static readonly ActivitySource s_source = new("MyApp.Orders");

public async Task ProcessOrderAsync(Order order)
{
    using var activity = s_source.StartActivity("ProcessOrder");
    activity?.SetTag("order.id", order.Id);
    // ...
}
```

## Non-ASP.NET Core Projects

For class libraries or console apps that cannot take a `FrameworkReference` on `Microsoft.AspNetCore.App`,
create a variant without the health check endpoint mapping:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsAspireSharedProject>true</IsAspireSharedProject>
  </PropertyGroup>

  <!-- No FrameworkReference to Microsoft.AspNetCore.App -->

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="9.*" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.*" />
  </ItemGroup>

</Project>
```

Omit `OpenTelemetry.Instrumentation.AspNetCore` and the `MapDefaultEndpoints()` method. The
`AddServiceDefaults()` method works the same but skips ASP.NET Core-specific instrumentation.

## Custom Metrics

Register application-specific `Meter` names alongside the default instrumentation:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("MyApp.Orders")
            .AddMeter("MyApp.Payments");
    });
```

Create counters, histograms, and gauges in application code using `IMeterFactory`:

```csharp
public sealed class OrderMetrics(IMeterFactory meterFactory)
{
    private readonly Counter<long> _ordersPlaced =
        meterFactory.Create("MyApp.Orders")
            .CreateCounter<long>("orders.placed", "orders");

    public void RecordOrderPlaced() => _ordersPlaced.Add(1);
}
```

Register `OrderMetrics` as a singleton and inject it where needed. Metrics appear automatically in the Aspire
dashboard when the OTLP exporter is active.

## Extending ServiceDefaults

Keep the `Extensions.cs` file focused on shared concerns. Add project-specific configuration in each service's
`Program.cs` after calling `AddServiceDefaults()`. Avoid adding application-specific logic to ServiceDefaults --
it should remain generic and reusable across all services in the application.

When multiple applications share the same ServiceDefaults project, version the project alongside the AppHost.
Bump the version when adding new cross-cutting concerns that all services should adopt.

## Complete Implementation

See [references/extensions-implementation.md](references/extensions-implementation.md) for the full
`Extensions.cs` source code with all helper methods and the complete project file.

## Learn More

| Topic                     | How to Find                                                                            |
|---------------------------|----------------------------------------------------------------------------------------|
| Service defaults overview | `microsoft_docs_search(query=".NET Aspire service defaults project")`                  |
| OpenTelemetry in Aspire   | `microsoft_docs_search(query=".NET Aspire telemetry OpenTelemetry")`                   |
| Health checks             | `microsoft_docs_search(query="ASP.NET Core health checks")`                            |
| HTTP resilience           | `microsoft_docs_search(query="Microsoft.Extensions.Http.Resilience standard handler")` |
| Service discovery         | `microsoft_docs_search(query=".NET Aspire service discovery configuration")`           |
