# ServiceDefaults Extensions Implementation

Complete implementation of the ServiceDefaults shared project for .NET Aspire applications targeting .NET 10.

## Project File (ServiceDefaults.csproj)

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

## Extensions.cs

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    /// <summary>
    /// Adds cross-cutting concerns: OpenTelemetry, health checks,
    /// service discovery, and HTTP client resilience.
    /// </summary>
    public static IHostApplicationBuilder AddServiceDefaults(
        this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Add standard resilience handler (retry, circuit breaker,
            // hedging, rate limiter, total timeout).
            http.AddStandardResilienceHandler();

            // Enable service discovery for all HttpClient instances so
            // URIs like "https+http://api" resolve automatically.
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Configures OpenTelemetry logging, metrics, and tracing with
    /// conditional OTLP export.
    /// </summary>
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
                // Filter out health check endpoints to reduce trace noise.
                tracing.AddAspNetCoreInstrumentation(options =>
                    options.Filter = httpContext =>
                        !httpContext.Request.Path.StartsWithSegments("/health")
                        && !httpContext.Request.Path.StartsWithSegments("/alive"))
                    .AddHttpClientInstrumentation();
            });

        AddOpenTelemetryExporters(builder);

        return builder;
    }

    /// <summary>
    /// Registers default health checks with a "self" check tagged for
    /// liveness probes.
    /// </summary>
    public static IHostApplicationBuilder AddDefaultHealthChecks(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            // A basic self-check that always returns healthy.
            // Tagged "live" so it is included in liveness probes.
            .AddCheck("self", () => HealthCheckResult.Healthy(),
                tags: ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps health check endpoints:
    ///   /health  - readiness (all checks)
    ///   /alive   - liveness (only "live"-tagged checks)
    /// </summary>
    public static WebApplication MapDefaultEndpoints(
        this WebApplication app)
    {
        // Readiness probe: runs ALL registered health checks.
        // Returns 200 when all checks pass, 503 otherwise.
        app.MapHealthChecks("/health");

        // Liveness probe: runs only checks tagged "live".
        // Returns 200 when the process is alive, even if
        // dependencies are unhealthy.
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = registration =>
                registration.Tags.Contains("live")
        });

        return app;
    }

    /// <summary>
    /// Conditionally adds the OTLP exporter when the endpoint is
    /// configured. Aspire sets OTEL_EXPORTER_OTLP_ENDPOINT automatically;
    /// outside Aspire, no exporter is added.
    /// </summary>
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
}
```

## Usage in an API Project (Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Wire all cross-cutting concerns from ServiceDefaults.
builder.AddServiceDefaults();

// Add application-specific services.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add application-specific health checks.
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("catalog")!,
        name: "catalog-db",
        tags: ["ready"]);

var app = builder.Build();

// Map health check endpoints before application routes.
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
```

## Usage in a Worker Service (Program.cs)

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Wire cross-cutting concerns. No MapDefaultEndpoints needed
// because workers have no HTTP pipeline.
builder.AddServiceDefaults();

builder.Services.AddHostedService<OrderProcessor>();

var host = builder.Build();
await host.RunAsync();
```

## Usage with Minimal APIs (Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/api/orders", async (CatalogContext db) =>
    await db.Orders.ToListAsync());

app.MapPost("/api/orders", async (Order order, CatalogContext db) =>
{
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    return Results.Created($"/api/orders/{order.Id}", order);
});

app.Run();
```

## Adding Custom Trace Sources

Register application-specific `ActivitySource` names to capture domain tracing:

```csharp
// In Extensions.cs or in Program.cs after AddServiceDefaults()
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("MyApp.Orders")
            .AddSource("MyApp.Payments")
            .AddSource("MyApp.Shipping");
    });
```

Create and use activities in application code:

```csharp
using System.Diagnostics;

public sealed class OrderService(ILogger<OrderService> logger)
{
    private static readonly ActivitySource s_source = new("MyApp.Orders");

    public async Task<Order> PlaceOrderAsync(OrderRequest request)
    {
        using var activity = s_source.StartActivity("PlaceOrder");
        activity?.SetTag("order.customer_id", request.CustomerId);
        activity?.SetTag("order.item_count", request.Items.Count);

        logger.LogInformation("Placing order for customer {CustomerId}",
            request.CustomerId);

        // Process order...

        activity?.SetTag("order.id", order.Id);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return order;
    }
}
```

## Adding Custom Metrics

Register application-specific `Meter` names:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("MyApp.Orders");
    });
```

Create and record metrics in application code:

```csharp
using System.Diagnostics.Metrics;

public sealed class OrderMetrics
{
    private readonly Counter<long> _ordersPlaced;
    private readonly Histogram<double> _orderTotal;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("MyApp.Orders");
        _ordersPlaced = meter.CreateCounter<long>(
            "orders.placed", "orders", "Number of orders placed");
        _orderTotal = meter.CreateHistogram<double>(
            "orders.total", "USD", "Order total amount");
    }

    public void RecordOrderPlaced(double total)
    {
        _ordersPlaced.Add(1);
        _orderTotal.Record(total);
    }
}
```

## Non-ASP.NET Core Variant

For projects that cannot reference `Microsoft.AspNetCore.App` (class libraries, pure console apps):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
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
                // No ASP.NET Core instrumentation in this variant.
                metrics.AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                // No ASP.NET Core instrumentation in this variant.
                tracing.AddHttpClientInstrumentation();
            });

        var useOtlp = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlp)
        {
            builder.Services.AddOpenTelemetry()
                .UseOtlpExporter();
        }

        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(),
                tags: ["live"]);

        return builder;
    }

    // No MapDefaultEndpoints in this variant - no web host available.
}
```
