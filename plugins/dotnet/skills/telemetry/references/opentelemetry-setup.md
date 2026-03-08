# OpenTelemetry Setup Patterns

## Table of Contents

- [ASP.NET Core (Full Setup)](#aspnet-core-full-setup)
- [Console / Worker Service](#console--worker-service)
- [Aspire Service Defaults](#aspire-service-defaults)
- [Exporter Configuration](#exporter-configuration)
- [Azure Monitor Integration](#azure-monitor-integration)
- [Sampling Configuration](#sampling-configuration)
- [Custom Processors and Enrichment](#custom-processors-and-enrichment)
- [Environment Variables](#environment-variables)
- [Common Pitfalls](#common-pitfalls)

---

## ASP.NET Core (Full Setup)

### Packages

```xml
<PackageReference Include="OpenTelemetry.Extensions.Hosting" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
<!-- Optional -->
<PackageReference Include="OpenTelemetry.Instrumentation.SqlClient" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" />
```

### Program.cs

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Register your telemetry class
builder.Services.AddSingleton<OrderTelemetry>();

var otel = builder.Services.AddOpenTelemetry();

// Resource: identifies your service
otel.ConfigureResource(resource => resource
    .AddService(
        serviceName: builder.Environment.ApplicationName,
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0")
    .AddAttributes(new Dictionary<string, object>
    {
        ["deployment.environment"] = builder.Environment.EnvironmentName
    }));

// Tracing
otel.WithTracing(tracing =>
{
    tracing
        .AddAspNetCoreInstrumentation(opts =>
        {
            opts.RecordException = true;
            opts.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health");
        })
        .AddHttpClientInstrumentation(opts =>
        {
            opts.RecordException = true;
            opts.FilterHttpRequestMessage = req => req.RequestUri?.Host != "localhost";
        })
        .AddSource("MyApp.Orders")      // Register your ActivitySources
        .AddSource("MyApp.Payments");

    if (builder.Environment.IsDevelopment())
        tracing.AddConsoleExporter();

    tracing.AddOtlpExporter();
});

// Metrics
otel.WithMetrics(metrics =>
{
    metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("MyApp.Orders")        // Register your Meters
        .AddMeter("MyApp.Api")
        .AddMeter("Microsoft.AspNetCore.Hosting")
        .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
        .AddMeter("System.Net.Http")
        .AddMeter("System.Net.NameResolution");

    metrics.AddOtlpExporter();

    // Optional: also expose Prometheus endpoint
    // metrics.AddPrometheusExporter();
});

// Logging (bridge ILogger to OTel)
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter();
});

var app = builder.Build();
// app.MapPrometheusScrapingEndpoint(); // if using Prometheus
app.Run();
```

## Console / Worker Service

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

// For hosted services
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("MyWorker"))
    .WithTracing(t => t
        .AddSource("MyApp.Worker")
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddMeter("MyApp.Worker")
        .AddOtlpExporter());

// For standalone console apps (no DI host)
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyConsole"))
    .AddSource("MyApp.Console")
    .AddConsoleExporter()
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("MyApp.Console")
    .AddConsoleExporter()
    .Build();
```

## Aspire Service Defaults

The easiest way to configure OTel for ASP.NET Core:

```csharp
// In your app:
var builder = WebApplication.CreateBuilder(args);
builder.ConfigureOpenTelemetry();
// OR for full Aspire defaults:
builder.AddServiceDefaults();

var app = builder.Build();
app.Run();
```

The `ConfigureOpenTelemetry()` from Aspire ServiceDefaults:
- Registers OTLP exporter
- Adds ASP.NET Core, HttpClient, and Runtime instrumentation
- Configures resource with service name
- Sets up logging bridge

Use OTLP environment variables to configure the endpoint:
```
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
OTEL_SERVICE_NAME=MyApp
```

## Exporter Configuration

### OTLP (OpenTelemetry Protocol)

```csharp
// gRPC (default, port 4317)
.AddOtlpExporter(opts =>
{
    opts.Endpoint = new Uri("http://otel-collector:4317");
    opts.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
})

// HTTP/protobuf (port 4318)
.AddOtlpExporter(opts =>
{
    opts.Endpoint = new Uri("http://otel-collector:4318");
    opts.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
})
```

### Prometheus (Metrics Only)

```csharp
// ASP.NET Core middleware
metrics.AddPrometheusExporter();
// ...
app.MapPrometheusScrapingEndpoint(); // exposes /metrics

// Standalone HTTP listener (non-ASP.NET)
metrics.AddPrometheusHttpListener(opts =>
    opts.UriPrefixes = new[] { "http://localhost:9184/" });
```

### Console (Development)

```csharp
.AddConsoleExporter()  // Both tracing and metrics
```

## Azure Monitor Integration

```xml
<PackageReference Include="Azure.Monitor.OpenTelemetry.AspNetCore" />
```

```csharp
// Simplest setup - auto-configures tracing, metrics, logging
builder.Services.AddOpenTelemetry().UseAzureMonitor(opts =>
{
    opts.ConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
});

// Add your custom sources/meters alongside Azure Monitor
builder.Services.ConfigureOpenTelemetryTracerProvider((sp, tracing) =>
    tracing.AddSource("MyApp.Orders"));

builder.Services.ConfigureOpenTelemetryMeterProvider((sp, metrics) =>
    metrics.AddMeter("MyApp.Orders"));
```

## Sampling Configuration

### Head-Based Sampling (TracerProvider)

```csharp
.WithTracing(tracing =>
{
    // Sample 10% of traces
    tracing.SetSampler(new TraceIdRatioBasedSampler(0.1));

    // Or parent-based: respect parent's sampling decision, sample 50% of roots
    tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(0.5)));
})
```

### Custom Sampler

```csharp
public class PrioritySampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters parameters)
    {
        // Always sample errors and high-priority operations
        if (parameters.Tags?.Any(t => t.Key == "priority" && t.Value?.ToString() == "high") == true)
            return new SamplingResult(SamplingDecision.RecordAndSample);

        // Sample 10% of everything else
        return Random.Shared.NextDouble() < 0.1
            ? new SamplingResult(SamplingDecision.RecordAndSample)
            : new SamplingResult(SamplingDecision.Drop);
    }
}
```

### Rate-Limiting Sampling (.NET 10+)

Out-of-proc via EventSource provider:
```
[AS]*/-ParentRateLimitingSampler(100)
```
Limits to 100 root activities per second.

## Custom Processors and Enrichment

### Trace Enrichment

```csharp
.WithTracing(tracing =>
{
    tracing.AddProcessor(new ActivityEnrichingProcessor());
})

public class ActivityEnrichingProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        // Add deployment info to every span
        activity.SetTag("deployment.version", Assembly.GetEntryAssembly()?.GetName().Version?.ToString());
        activity.SetTag("host.name", Environment.MachineName);
    }
}
```

### HTTP Metrics Enrichment

```csharp
public class MetricsEnrichmentHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpMetricsEnrichmentContext.AddCallback(request, static context =>
        {
            context.AddCustomTag("api.version", context.Request.Headers.GetValues("Api-Version").FirstOrDefault());
        });
        return base.SendAsync(request, cancellationToken);
    }
}

// Register with IHttpClientFactory
services.AddHttpClient("MyApi")
    .AddHttpMessageHandler<MetricsEnrichmentHandler>();
```

## Environment Variables

| Variable                      | Purpose             | Example                          |
|-------------------------------|---------------------|----------------------------------|
| `OTEL_SERVICE_NAME`           | Service name        | `my-api`                         |
| `OTEL_RESOURCE_ATTRIBUTES`    | Resource attributes | `deployment.environment=staging` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP endpoint       | `http://localhost:4317`          |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | OTLP protocol       | `grpc` or `http/protobuf`        |
| `OTEL_EXPORTER_OTLP_HEADERS`  | Auth headers        | `api-key=secret`                 |
| `OTEL_TRACES_SAMPLER`         | Sampler             | `parentbased_traceidratio`       |
| `OTEL_TRACES_SAMPLER_ARG`     | Sampler argument    | `0.1` (10% sampling)             |

## Common Pitfalls

| Pitfall                                                   | Impact                                 | Fix                                                |
|-----------------------------------------------------------|----------------------------------------|----------------------------------------------------|
| Forgetting `AddSource("Name")`                            | Custom activities not collected        | Register every `ActivitySource` name               |
| Forgetting `AddMeter("Name")`                             | Custom metrics not collected           | Register every `Meter` name                        |
| Using `new Meter()` instead of `IMeterFactory`            | DI isolation broken, testing difficult | Always use `IMeterFactory.Create()` in DI contexts |
| High-cardinality tags (user IDs, URLs with query strings) | Memory explosion, cost explosion       | Limit to <1000 unique tag combinations             |
| Missing `RecordException = true`                          | Exception details lost in traces       | Enable on ASP.NET Core and HTTP instrumentation    |
| Not filtering health checks                               | Noisy traces from /health, /ready      | Use `Filter` option to exclude                     |
| Calling `.Build()` on provider without `using`/disposal   | Memory leak, data loss                 | Always dispose TracerProvider/MeterProvider        |
