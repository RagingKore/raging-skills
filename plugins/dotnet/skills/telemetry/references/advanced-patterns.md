# Advanced Telemetry Patterns

## Table of Contents

- [Unified Telemetry Service (Full Pattern)](#unified-telemetry-service-full-pattern)
- [ActivityListener for Custom Collection](#activitylistener-for-custom-collection)
- [Custom MeterListener with Aggregation](#custom-meterlistener-with-aggregation)
- [Enriching Traces with Middleware](#enriching-traces-with-middleware)
- [Correlation Between Logs and Traces](#correlation-between-logs-and-traces)
- [Testing Telemetry](#testing-telemetry)
- [Conditional Telemetry (Feature Flags)](#conditional-telemetry-feature-flags)
- [Tag Cardinality Management](#tag-cardinality-management)

---

## Unified Telemetry Service (Full Pattern)

A production-ready telemetry service that unifies tracing, metrics, and logging for a domain:

```csharp
// ITelemetry interface for testability
public interface IOrderTelemetry
{
    Activity? StartProcessOrder(string orderId, string customerType);
    void RecordOrderPlaced(string orderId, string customerType, double durationSecs);
    void RecordOrderFailed(string orderId, string reason);
    void RecordOrderValue(decimal amount, string currency);
}

public sealed partial class OrderTelemetry : IOrderTelemetry
{
    // -- Tracing --
    internal static readonly ActivitySource Source = new("MyApp.Orders", "1.0.0");

    // -- Metrics --
    private readonly Counter<long> _ordersPlaced;
    private readonly Counter<long> _ordersFailed;
    private readonly Histogram<double> _orderDuration;
    private readonly Histogram<double> _orderValue;

    // -- Logging --
    private readonly ILogger _logger;

    public OrderTelemetry(IMeterFactory meterFactory, ILogger<OrderTelemetry> logger)
    {
        _logger = logger;
        var meter = meterFactory.Create("MyApp.Orders");

        _ordersPlaced = meter.CreateCounter<long>(
            "myapp.orders.placed", "{order}", "Total orders successfully placed");

        _ordersFailed = meter.CreateCounter<long>(
            "myapp.orders.failed", "{order}", "Total orders that failed");

        _orderDuration = meter.CreateHistogram<double>(
            "myapp.orders.duration", "s", "Order processing time",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30]
            });

        _orderValue = meter.CreateHistogram<double>(
            "myapp.orders.value", "{currency_unit}", "Order monetary value",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [1, 10, 50, 100, 500, 1000, 5000, 10000]
            });
    }

    public Activity? StartProcessOrder(string orderId, string customerType)
    {
        var activity = Source.StartActivity("ProcessOrder", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("order.id", orderId);
            activity.SetTag("order.customer_type", customerType);
        }
        LogOrderProcessingStarted(orderId, customerType);
        return activity;
    }

    public void RecordOrderPlaced(string orderId, string customerType, double durationSecs)
    {
        var tags = new TagList
        {
            { "order.customer_type", customerType },
            { "order.status", "placed" }
        };
        _ordersPlaced.Add(1, tags);
        _orderDuration.Record(durationSecs, tags);

        Activity.Current?.SetStatus(ActivityStatusCode.Ok);
        LogOrderPlaced(orderId, durationSecs);
    }

    public void RecordOrderFailed(string orderId, string reason)
    {
        _ordersFailed.Add(1, new KeyValuePair<string, object?>("order.failure_reason", reason));

        Activity.Current?.SetStatus(ActivityStatusCode.Error, reason);
        LogOrderFailed(orderId, reason);
    }

    public void RecordOrderValue(decimal amount, string currency)
    {
        _orderValue.Record((double)amount, new KeyValuePair<string, object?>("currency", currency));
    }

    // Source-generated log methods
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information,
        Message = "Processing order {OrderId} for customer type {CustomerType}")]
    private partial void LogOrderProcessingStarted(string orderId, string customerType);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Order {OrderId} placed in {DurationSecs}s")]
    private partial void LogOrderPlaced(string orderId, double durationSecs);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning,
        Message = "Order {OrderId} failed: {Reason}")]
    private partial void LogOrderFailed(string orderId, string reason);
}

// Registration
services.AddSingleton<IOrderTelemetry, OrderTelemetry>();
```

### Usage Pattern

```csharp
public class OrderHandler
{
    private readonly IOrderTelemetry _telemetry;

    public OrderHandler(IOrderTelemetry telemetry) => _telemetry = telemetry;

    public async Task<OrderResult> HandleAsync(PlaceOrderCommand command)
    {
        using var activity = _telemetry.StartProcessOrder(command.OrderId, command.CustomerType);
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await PlaceOrderCoreAsync(command);
            sw.Stop();

            _telemetry.RecordOrderPlaced(command.OrderId, command.CustomerType, sw.Elapsed.TotalSeconds);
            _telemetry.RecordOrderValue(result.TotalAmount, result.Currency);

            return result;
        }
        catch (InsufficientInventoryException ex)
        {
            _telemetry.RecordOrderFailed(command.OrderId, "insufficient_inventory");
            throw;
        }
        catch (PaymentDeclinedException ex)
        {
            _telemetry.RecordOrderFailed(command.OrderId, "payment_declined");
            throw;
        }
    }
}
```

## ActivityListener for Custom Collection

Build a custom trace collector without OpenTelemetry:

```csharp
public sealed class CustomTraceCollector : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<ActivitySnapshot> _completedActivities = new();

    public CustomTraceCollector(params string[] sourceNames)
    {
        var sourceSet = sourceNames.ToHashSet();

        _listener = new ActivityListener
        {
            ShouldListenTo = source => sourceSet.Contains(source.Name) || sourceNames.Length == 0,

            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                ActivitySamplingResult.AllDataAndRecorded,

            ActivityStopped = activity =>
            {
                _completedActivities.Enqueue(new ActivitySnapshot
                {
                    TraceId = activity.TraceId.ToString(),
                    SpanId = activity.SpanId.ToString(),
                    ParentSpanId = activity.ParentSpanId.ToString(),
                    OperationName = activity.OperationName,
                    Source = activity.Source.Name,
                    Duration = activity.Duration,
                    Status = activity.Status,
                    Tags = activity.TagObjects.ToDictionary(t => t.Key, t => t.Value?.ToString())
                });
            }
        };

        ActivitySource.AddActivityListener(_listener);
    }

    public IEnumerable<ActivitySnapshot> GetCompletedActivities()
    {
        while (_completedActivities.TryDequeue(out var snapshot))
            yield return snapshot;
    }

    public void Dispose() => _listener.Dispose();
}

public record ActivitySnapshot
{
    public string TraceId { get; init; } = "";
    public string SpanId { get; init; } = "";
    public string ParentSpanId { get; init; } = "";
    public string OperationName { get; init; } = "";
    public string Source { get; init; } = "";
    public TimeSpan Duration { get; init; }
    public ActivityStatusCode Status { get; init; }
    public Dictionary<string, string?> Tags { get; init; } = new();
}
```

## Custom MeterListener with Aggregation

Build a custom in-process metric aggregator:

```csharp
public sealed class InProcessMetricAggregator : IDisposable
{
    private readonly MeterListener _listener;
    private readonly ConcurrentDictionary<string, MetricBucket> _buckets = new();

    public InProcessMetricAggregator(params string[] meterNames)
    {
        var nameSet = meterNames.ToHashSet();
        _listener = new MeterListener();

        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (nameSet.Contains(instrument.Meter.Name))
                listener.EnableMeasurementEvents(instrument);
        };

        _listener.SetMeasurementEventCallback<int>(OnMeasurement);
        _listener.SetMeasurementEventCallback<long>(OnMeasurement);
        _listener.SetMeasurementEventCallback<double>(OnMeasurement);
        _listener.SetMeasurementEventCallback<float>(OnMeasurement);
        _listener.Start();
    }

    private void OnMeasurement<T>(Instrument instrument, T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state) where T : struct
    {
        var key = instrument.Name;
        var bucket = _buckets.GetOrAdd(key, _ => new MetricBucket());
        bucket.Record(Convert.ToDouble(value));
    }

    public IReadOnlyDictionary<string, MetricSnapshot> GetSnapshots()
    {
        return _buckets.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.TakeSnapshot());
    }

    public void Dispose() => _listener.Dispose();
}

public class MetricBucket
{
    private long _count;
    private double _sum;
    private double _min = double.MaxValue;
    private double _max = double.MinValue;

    public void Record(double value)
    {
        Interlocked.Increment(ref _count);
        // Note: for production, use more sophisticated thread-safe accumulation
        InterlockedAdd(ref _sum, value);
        InterlockedMin(ref _min, value);
        InterlockedMax(ref _max, value);
    }

    public MetricSnapshot TakeSnapshot() => new()
    {
        Count = Volatile.Read(ref _count),
        Sum = Volatile.Read(ref _sum),
        Min = Volatile.Read(ref _min),
        Max = Volatile.Read(ref _max)
    };

    private static void InterlockedAdd(ref double target, double value)
    {
        double current, newValue;
        do { current = target; newValue = current + value; }
        while (Interlocked.CompareExchange(ref target, newValue, current) != current);
    }

    private static void InterlockedMin(ref double target, double value)
    {
        double current;
        do { current = target; if (current <= value) return; }
        while (Interlocked.CompareExchange(ref target, value, current) != current);
    }

    private static void InterlockedMax(ref double target, double value)
    {
        double current;
        do { current = target; if (current >= value) return; }
        while (Interlocked.CompareExchange(ref target, value, current) != current);
    }
}

public record MetricSnapshot
{
    public long Count { get; init; }
    public double Sum { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Average => Count > 0 ? Sum / Count : 0;
}
```

## Correlation Between Logs and Traces

ILogger automatically captures the active `Activity`'s TraceId and SpanId when OTel logging bridge is configured:

```csharp
// In appsettings.json - enable scopes for console
{
  "Logging": {
    "Console": {
      "IncludeScopes": true,
      "FormatterName": "json"
    }
  }
}

// Logs automatically include:
// - TraceId (from Activity.Current.TraceId)
// - SpanId (from Activity.Current.SpanId)
// - TraceFlags

// Manual correlation when needed:
_logger.LogInformation("Processing order {OrderId}, trace: {TraceId}",
    orderId, Activity.Current?.TraceId.ToString());
```

## Testing Telemetry

### Testing Metrics with MetricCollector

```csharp
[Fact]
public void OrderPlaced_IncrementsCounter()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddMetrics();
    services.AddSingleton<OrderTelemetry>();
    var sp = services.BuildServiceProvider();

    var metrics = sp.GetRequiredService<OrderTelemetry>();
    var meterFactory = sp.GetRequiredService<IMeterFactory>();
    var collector = new MetricCollector<long>(meterFactory, "MyApp.Orders", "myapp.orders.placed");

    // Act
    metrics.RecordOrderPlaced("order-1", "premium", 1.5);

    // Assert
    var measurements = collector.GetMeasurementSnapshot();
    Assert.Single(measurements);
    Assert.Equal(1, measurements[0].Value);
    Assert.Equal("premium", measurements[0].Tags["order.customer_type"]);
}
```

### Testing Activities

```csharp
[Fact]
public void StartProcessOrder_CreatesActivity()
{
    // Arrange
    var activities = new List<Activity>();
    using var listener = new ActivityListener
    {
        ShouldListenTo = source => source.Name == "MyApp.Orders",
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        ActivityStarted = a => activities.Add(a)
    };
    ActivitySource.AddActivityListener(listener);

    var services = new ServiceCollection();
    services.AddMetrics();
    services.AddLogging();
    services.AddSingleton<OrderTelemetry>();
    var sp = services.BuildServiceProvider();
    var telemetry = sp.GetRequiredService<OrderTelemetry>();

    // Act
    using var activity = telemetry.StartProcessOrder("order-1", "standard");

    // Assert
    Assert.NotNull(activity);
    Assert.Equal("ProcessOrder", activity.OperationName);
    Assert.Equal("order-1", activity.GetTagItem("order.id")?.ToString());
}
```

## Tag Cardinality Management

High-cardinality tags cause memory and cost explosion. Normalize tags:

```csharp
public static class TagNormalizers
{
    // Normalize HTTP status codes to groups
    public static string NormalizeStatusCode(int statusCode) => statusCode switch
    {
        >= 200 and < 300 => "2xx",
        >= 300 and < 400 => "3xx",
        >= 400 and < 500 => "4xx",
        >= 500 => "5xx",
        _ => "unknown"
    };

    // Normalize URL paths (remove IDs)
    public static string NormalizeRoute(string path)
    {
        // /api/orders/abc-123 → /api/orders/{id}
        // Use route templates instead of actual URLs
        return path; // Use http.route from ASP.NET Core routing instead
    }

    // Cap string tag values
    public static string Truncate(string value, int maxLength = 64)
        => value.Length <= maxLength ? value : value[..maxLength];
}
```

**Rules:**
- Never use user IDs, email addresses, or session IDs as tag values
- Never use full URLs with query strings - use route templates
- Keep unique combinations under 1000 per instrument
- Histograms use more memory per combination - keep under 100
