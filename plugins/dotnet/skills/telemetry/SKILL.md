---
name: telemetry
description: |
  .NET telemetry expert covering the complete observability stack: distributed tracing (ActivitySource, Activity, DiagnosticSource),
  metrics (Meter, IMeterFactory, Counter, Histogram, Gauge, MeterListener), EventSource/EventCounter,
  high-performance logging integration, and OpenTelemetry configuration. Covers dual-write patterns
  (logs + diagnostic events), creating unified telemetry classes, hot-path optimization, sampling,
  enrichment, Activity propagation, and custom metric listeners. Use when implementing telemetry,
  adding tracing, creating metrics, setting up OpenTelemetry, building diagnostic listeners,
  optimizing hot-path instrumentation, creating a core telemetry service, or integrating
  with Prometheus/Grafana/Jaeger/Application Insights. Triggers on "add telemetry", "ActivitySource",
  "DiagnosticSource", "Meter", "metrics", "distributed tracing", "OpenTelemetry", "observability",
  "Activity", "spans", "MeterListener", "EventSource", "EventCounter", "telemetry class",
  "instrument code", "hot path performance".
---

# .NET Telemetry & Observability Expert

Comprehensive guidance for the three pillars of .NET observability: **distributed tracing**, **metrics**, and **diagnostic events** – with OpenTelemetry integration and high-performance patterns.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     YOUR APPLICATION CODE                       │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────────────┐ │
│  │ ILogger<T>   │  │ ActivitySource│  │ Meter (IMeterFactory) │ │
│  │ [LoggerMsg]  │  │ .StartActivity│  │ Counter/Histogram/etc │ │
│  └──────┬───────┘  └──────┬───────┘  └───────────┬───────────┘ │
│         │                 │                       │             │
│  ┌──────┴─────────────────┴───────────────────────┴───────────┐ │
│  │            Core Telemetry Service (unified)                │ │
│  │  - Logs (ILogger)                                          │ │
│  │  - Traces (ActivitySource → Activity / Span)               │ │
│  │  - Metrics (Meter → Counter, Histogram, Gauge)             │ │
│  │  - DiagnosticSource events (optional, legacy integration)  │ │
│  └────────────────────────┬───────────────────────────────────┘ │
└───────────────────────────┼─────────────────────────────────────┘
                            │
              ┌─────────────┴──────────────┐
              │      OpenTelemetry SDK     │
              │  TracerProvider            │
              │  MeterProvider             │
              │  LoggerProvider            │
              └─────────────┬──────────────┘
                            │
       ┌────────────┬───────┴───────┬────────────┐
       │ OTLP       │ Prometheus    │ Console    │ Azure Monitor
       │ Exporter   │ Exporter      │ Exporter   │ Exporter
       └────────────┴───────────────┴────────────┘
```

## .NET ↔ OpenTelemetry Terminology

| .NET Type            | OTel Concept | Purpose                                |
|----------------------|--------------|----------------------------------------|
| `ActivitySource`     | Tracer       | Creates spans (activities)             |
| `Activity`           | Span         | Unit of work with timing, tags, events |
| `Meter`              | Meter        | Groups related instruments             |
| `Counter<T>`         | Counter      | Monotonically increasing value         |
| `Histogram<T>`       | Histogram    | Distribution of measurements           |
| `Gauge<T>` (.NET 9+) | Gauge        | Point-in-time value                    |
| `ILogger`            | LogRecord    | Structured log entry                   |
| `DiagnosticSource`   | *(legacy)*   | Rich payload events (pre-OTel)         |

## Quick Decision Matrix

| Need                                    | Use                            | Why                                         |
|-----------------------------------------|--------------------------------|---------------------------------------------|
| Track request duration/flow             | `ActivitySource.StartActivity` | Distributed tracing with parent/child       |
| Count operations (monotonic)            | `Counter<T>`                   | Total count + rate of change                |
| Measure latency distributions           | `Histogram<T>`                 | Percentiles (p50, p95, p99)                 |
| Track queue depth / active connections  | `UpDownCounter<T>`             | Value that increases and decreases          |
| Snapshot current value on demand        | `ObservableGauge<T>`           | Callback-based, read on collection interval |
| Set exact current value                 | `Gauge<T>` (.NET 9+)           | Record point-in-time values                 |
| Rich diagnostic payload for subscribers | `DiagnosticSource.Write`       | When listeners need full object access      |
| ETW / EventPipe structured events       | `EventSource`                  | Low-level, cross-platform tracing           |

## Core Telemetry Service Pattern

The recommended approach is a **single telemetry class per bounded context** that encapsulates all three pillars:

```csharp
public sealed class OrderTelemetry {
    // --- Tracing ---
    private static readonly ActivitySource Source = new("MyApp.Orders", "1.0.0");

    // --- Metrics ---
    private readonly Counter<long> _ordersPlaced;
    private readonly Counter<long> _ordersFailed;
    private readonly Histogram<double> _orderDuration;

    // --- Logging (via source-gen in partial class or injected ILogger) ---
    private readonly ILogger _logger;

    public OrderTelemetry(IMeterFactory meterFactory, ILogger<OrderTelemetry> logger) {
        var meter = meterFactory.Create("MyApp.Orders");
        _ordersPlaced = meter.CreateCounter<long>("myapp.orders.placed", "{order}", "Orders placed");
        _ordersFailed = meter.CreateCounter<long>("myapp.orders.failed", "{order}", "Orders that failed");
        _orderDuration = meter.CreateHistogram<double>("myapp.orders.duration", "s", "Order processing time",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.01, 0.05, 0.1, 0.5, 1, 5, 10] });
        _logger = logger;
    }

    public Activity? StartProcessOrder(string orderId) {
        var activity = Source.StartActivity("ProcessOrder", ActivityKind.Internal);
        activity?.SetTag("order.id", orderId);
        return activity;
    }

    public void RecordOrderPlaced(string orderId, double durationSecs) {
        _ordersPlaced.Add(1, new KeyValuePair<string, object?>("order.type", "standard"));
        _orderDuration.Record(durationSecs);
    }

    public void RecordOrderFailed(string orderId, Exception ex) {
        _ordersFailed.Add(1);
        Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
        Activity.Current?.RecordException(ex);
    }
}
```

**Registration:**
```csharp
builder.Services.AddSingleton<OrderTelemetry>();
```

See [sample_codes/core-telemetry/unified-telemetry-service.cs](sample_codes/core-telemetry/unified-telemetry-service.cs) for the complete pattern with all three pillars.

## Distributed Tracing (ActivitySource / Activity)

### Creating Activities

```csharp
// Static source - one per library/component
private static readonly ActivitySource Source = new("MyApp.Component", "1.0.0");

// Start an activity (returns null if no listener)
using var activity = Source.StartActivity("OperationName", ActivityKind.Client);

// Always use null-conditional - activity may be null
activity?.SetTag("http.method", "GET");
activity?.SetTag("http.url", url);
activity?.AddEvent(new ActivityEvent("checkpoint-reached"));
activity?.SetStatus(ActivityStatusCode.Ok);
```

### ActivityKind

| Kind       | When                                          |
|------------|-----------------------------------------------|
| `Internal` | Default; internal operation                   |
| `Server`   | Handling incoming request                     |
| `Client`   | Making outgoing request                       |
| `Producer` | Enqueuing a message                           |
| `Consumer` | Dequeuing/processing a message                |

### Performance: IsAllDataRequested

```csharp
using var activity = Source.StartActivity("HotPath");
if (activity?.IsAllDataRequested == true) {
    // Only compute expensive tags when someone is listening for full data
    activity.SetTag("request.body.hash", ComputeHash(body));
}
```

### Sampling

When no `ActivityListener` subscribes, `StartActivity()` returns `null` — **zero overhead**. This is the primary performance optimization for hot paths.

```csharp
// Custom sampling via ActivityListener
ActivitySource.AddActivityListener(new ActivityListener {
    ShouldListenTo = source => source.Name == "MyApp.Orders",
    Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
        options.Parent.TraceFlags.HasFlag(ActivityTraceFlags.Recorded)
            ? ActivitySamplingResult.AllDataAndRecorded
            : ActivitySamplingResult.PropagationData,
    ActivityStarted = activity => { /* collect */ },
    ActivityStopped = activity => { /* export */ }
});
```

### Activity Links (Batch Processing)

```csharp
void ProcessBatch(ActivityContext[] requestContexts) {
    using var activity = Source.StartActivity(
        name: "ProcessBatch",
        kind: ActivityKind.Consumer,
        parentContext: default,
        links: requestContexts.Select(ctx => new ActivityLink(ctx)));
    // Process batch...
}
```

## Metrics (System.Diagnostics.Metrics)

### Instrument Types

| Type                    | API                                | Use Case                 | Aggregation |
|-------------------------|------------------------------------|--------------------------|-------------|
| Counter                 | `CreateCounter<T>`                 | Monotonic counts         | Sum / Rate  |
| UpDownCounter           | `CreateUpDownCounter<T>`           | Bidirectional counts     | Sum         |
| Histogram               | `CreateHistogram<T>`               | Distributions (latency)  | Percentiles |
| Gauge (.NET 9+)         | `CreateGauge<T>`                   | Point-in-time value      | Last value  |
| ObservableCounter       | `CreateObservableCounter<T>`       | Callback-based monotonic | Sum / Rate  |
| ObservableUpDownCounter | `CreateObservableUpDownCounter<T>` | Callback bidirectional   | Sum         |
| ObservableGauge         | `CreateObservableGauge<T>`         | Callback current value   | Last value  |

### DI-Based Metrics (Recommended)

```csharp
public class AppMetrics {
    private readonly Counter<long> _requestCount;
    private readonly Histogram<double> _requestDuration;

    public AppMetrics(IMeterFactory meterFactory) {
        var meter = meterFactory.Create("MyApp.Api");
        _requestCount = meter.CreateCounter<long>("myapp.api.requests", "{request}");
        _requestDuration = meter.CreateHistogram<double>("myapp.api.request_duration", "s",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10] });
    }

    public void RecordRequest(string method, string route, int statusCode, double durationSecs) {
        var tags = new TagList {
            { "http.request.method", method },
            { "http.route", route },
            { "http.response.status_code", statusCode }
        };
        _requestCount.Add(1, tags);
        _requestDuration.Record(durationSecs, tags);
    }
}
```

### Multi-Dimensional Metrics with Tags

```csharp
// Up to 3 tags: zero-allocation
counter.Add(1,
    new KeyValuePair<string, object?>("region", "us-east"),
    new KeyValuePair<string, object?>("env", "prod"));

// 4+ tags: use TagList to avoid allocation
var tags = new TagList {
    { "region", "us-east" },
    { "env", "prod" },
    { "service", "orders" },
    { "version", "2.1" }
};
counter.Add(1, tags);
```

### Custom MeterListener (In-Process Collection)

```csharp
using var listener = new MeterListener();
listener.InstrumentPublished = (instrument, meterListener) => {
    if (instrument.Meter.Name == "MyApp.Api")
        meterListener.EnableMeasurementEvents(instrument);
};
listener.SetMeasurementEventCallback<long>(OnMeasurement);
listener.SetMeasurementEventCallback<double>(OnMeasurement);
listener.Start();

static void OnMeasurement<T>(Instrument instrument, T value,
    ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state) {
    Console.WriteLine($"{instrument.Name}: {value}");
}
```

## DiagnosticSource (Legacy + Advanced Scenarios)

Use `DiagnosticSource` when you need to pass **rich, typed payloads** to in-process listeners. Many .NET libraries (HttpClient, ASP.NET Core, EF Core) emit diagnostic events.

```csharp
// Listening to library diagnostic events
IDisposable subscription = DiagnosticListener.AllListeners.Subscribe(
    new Observer<DiagnosticListener>(listener => {
        if (listener.Name == "Microsoft.AspNetCore") {
            listener.Subscribe(new Observer<KeyValuePair<string, object>>(pair => {
                if (pair.Key == "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop") {
                    // Access rich payload
                    var httpContext = pair.Value.GetType().GetProperty("HttpContext")?.GetValue(pair.Value);
                }
            }));
        }
    }));
```

## Hot-Path Performance Guidelines

| Technique                            | Overhead When Disabled | Overhead When Enabled |
|--------------------------------------|------------------------|-----------------------|
| `ActivitySource.StartActivity`       | ~0 ns (returns null)   | ~1 μs                 |
| `Counter<T>.Add()`                   | ~10 ns (no listener)   | ~10-100 ns            |
| `Histogram<T>.Record()`              | ~10 ns (no listener)   | ~50-200 ns            |
| `DiagnosticSource.IsEnabled + Write` | ~2 ns (no listener)    | Depends on payload    |
| `[LoggerMessage]` source-gen         | ~7 ns (level disabled) | ~49 ns                |

### Rules for Hot Paths

1. **Always check for null**: `activity?.SetTag(...)` — `StartActivity` returns null if no listener
2. **Use `IsAllDataRequested`**: Skip expensive tag computation when not needed
3. **Prefer `TagList`** for 4+ tags to avoid `object[]` allocation
4. **Use `ObservableCounter`** over `Counter` in ultra-hot paths (>1M calls/sec/thread)
5. **Keep tag cardinality low**: <1000 unique combinations per instrument
6. **Use smaller numeric types**: `Counter<int>` uses 4 bytes vs `Counter<double>` 8 bytes per tag combo

## OpenTelemetry Setup

See [references/opentelemetry-setup.md](references/opentelemetry-setup.md) for complete setup patterns including ASP.NET Core, console apps, Aspire integration, and exporter configuration.

Quick ASP.NET Core setup:

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("MyApp"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("MyApp.Orders")         // your ActivitySources
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter("MyApp.Orders")          // your Meters
        .AddMeter("MyApp.Api")
        .AddOtlpExporter());
```

## EventSource / EventCounter (Low-Level)

For ETW/EventPipe scenarios. See [references/eventsource-patterns.md](references/eventsource-patterns.md).

## Naming Conventions (OTel Semantic Conventions)

| Element          | Convention                | Example                      |
|------------------|---------------------------|------------------------------|
| Meter name       | Reverse-DNS, PascalCase   | `MyCompany.MyApp.Orders`     |
| Instrument name  | Lowercase dotted          | `myapp.orders.placed`        |
| Instrument unit  | UCUM in braces            | `{request}`, `s`, `By`       |
| Tag name         | Lowercase dotted          | `http.request.method`        |
| ActivitySource   | Reverse-DNS               | `MyCompany.MyApp.Orders`     |
| Activity name    | PascalCase verb           | `ProcessOrder`, `SendEmail`  |

## Learn More

| Topic                  | How to Find                                                                                                                 |
|------------------------|-----------------------------------------------------------------------------------------------------------------------------|
| Activity API reference | `microsoft_docs_search(query=".NET Activity class distributed tracing API")`                                                |
| Built-in .NET metrics  | `microsoft_docs_fetch(url="https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics")`                    |
| Metric enrichment      | `microsoft_docs_search(query=".NET HTTP metrics enrichment HttpMetricsEnrichmentContext")`                                  |
| Compare metric APIs    | `microsoft_docs_fetch(url="https://learn.microsoft.com/en-us/dotnet/core/diagnostics/compare-metric-apis")`                 |
| Strongly-typed metrics | `microsoft_docs_search(query=".NET strongly typed metrics source generator")`                                               |
| DiagnosticSource guide | `microsoft_docs_fetch(url="https://learn.microsoft.com/en-us/dotnet/core/diagnostics/diagnosticsource-diagnosticlistener")` |
| .NET 10 diagnostics    | `microsoft_docs_search(query=".NET 10 diagnostics telemetry schema ActivitySourceOptions")`                                 |
