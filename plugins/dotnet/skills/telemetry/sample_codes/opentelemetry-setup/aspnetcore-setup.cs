// Complete OpenTelemetry setup for ASP.NET Core
// Configures tracing, metrics, and logging with OTLP export

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// --- Register your telemetry services ---
builder.Services.AddSingleton<OrderTelemetry>();

// --- Configure OpenTelemetry ---
var otel = builder.Services.AddOpenTelemetry();

// Resource: identifies your service in the telemetry backend
otel.ConfigureResource(resource => resource
    .AddService(
        serviceName: builder.Environment.ApplicationName,
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0")
    .AddAttributes(new Dictionary<string, object>
    {
        ["deployment.environment"] = builder.Environment.EnvironmentName,
        ["host.name"] = Environment.MachineName
    }));

// --- Distributed Tracing ---
otel.WithTracing(tracing =>
{
    // Built-in instrumentation
    tracing.AddAspNetCoreInstrumentation(opts =>
    {
        opts.RecordException = true;
        // Filter out health checks and static files
        opts.Filter = httpContext =>
            !httpContext.Request.Path.StartsWithSegments("/health") &&
            !httpContext.Request.Path.StartsWithSegments("/ready") &&
            !httpContext.Request.Path.StartsWithSegments("/_framework");
    });

    tracing.AddHttpClientInstrumentation(opts =>
    {
        opts.RecordException = true;
    });

    // Register YOUR custom ActivitySources
    tracing.AddSource("MyApp.Orders");
    tracing.AddSource("MyApp.Payments");

    // .NET 9+: built-in HTTP client tracing (no separate instrumentation needed)
    tracing.AddSource("System.Net.Http");

    // Sampling: only export 25% of traces in production
    if (!builder.Environment.IsDevelopment())
    {
        tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(0.25)));
    }

    // Exporters
    if (builder.Environment.IsDevelopment())
        tracing.AddConsoleExporter();

    tracing.AddOtlpExporter(); // uses OTEL_EXPORTER_OTLP_ENDPOINT env var
});

// --- Metrics ---
otel.WithMetrics(metrics =>
{
    // Built-in instrumentation
    metrics.AddAspNetCoreInstrumentation();
    metrics.AddHttpClientInstrumentation();

    // Register YOUR custom Meters
    metrics.AddMeter("MyApp.Orders");
    metrics.AddMeter("MyApp.Api");

    // Built-in .NET metrics
    metrics.AddMeter("Microsoft.AspNetCore.Hosting");
    metrics.AddMeter("Microsoft.AspNetCore.Server.Kestrel");
    metrics.AddMeter("System.Net.Http");
    metrics.AddMeter("System.Net.NameResolution");

    // Exporters
    metrics.AddOtlpExporter();

    // Optional: Prometheus scraping endpoint
    // metrics.AddPrometheusExporter();
});

// --- Logging (bridge ILogger → OTel) ---
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    // Only export logs at Information level and above
    logging.AddOtlpExporter();
});

var app = builder.Build();

// Optional: Prometheus scraping endpoint
// app.MapPrometheusScrapingEndpoint();

app.MapControllers();
app.Run();
