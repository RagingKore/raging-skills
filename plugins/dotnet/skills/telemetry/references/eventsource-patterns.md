# EventSource & EventCounter Patterns

## Table of Contents

- [EventSource Basics](#eventsource-basics)
- [EventCounter for Aggregate Metrics](#eventcounter-for-aggregate-metrics)
- [In-Process Listening (EventListener)](#in-process-listening-eventlistener)
- [DiagnosticSource vs EventSource](#diagnosticsource-vs-eventsource)
- [Dual-Write Pattern](#dual-write-pattern)
- [Performance Considerations](#performance-considerations)
- [Migration to System.Diagnostics.Metrics](#migration-to-systemdiagnosticsmetrics)

---

## EventSource Basics

`EventSource` is a low-level, high-performance structured logging system built into the .NET runtime. Events are consumable via ETW (Windows), EventPipe (cross-platform), and `EventListener` (in-process).

```csharp
[EventSource(Name = "MyCompany.MyApp")]
public sealed class AppEventSource : EventSource
{
    public static readonly AppEventSource Log = new();

    [Event(1, Level = EventLevel.Informational, Keywords = Keywords.Requests,
           Message = "Request started: {0} {1}")]
    public void RequestStarted(string method, string path)
    {
        if (IsEnabled()) WriteEvent(1, method, path);
    }

    [Event(2, Level = EventLevel.Informational, Keywords = Keywords.Requests,
           Message = "Request completed: {0} in {1}ms")]
    public void RequestCompleted(string path, long durationMs)
    {
        if (IsEnabled()) WriteEvent(2, path, durationMs);
    }

    [Event(3, Level = EventLevel.Error, Keywords = Keywords.Errors,
           Message = "Request failed: {0}")]
    public void RequestFailed(string error)
    {
        if (IsEnabled()) WriteEvent(3, error);
    }

    // High-performance overload avoiding params object[]
    [NonEvent]
    public unsafe void RequestCompletedFast(string path, long durationMs)
    {
        if (IsEnabled(EventLevel.Informational, Keywords.Requests))
        {
            fixed (char* pathPtr = path)
            {
                EventData* data = stackalloc EventData[2];
                data[0] = new EventData { DataPointer = (IntPtr)pathPtr, Size = (path.Length + 1) * sizeof(char) };
                data[1] = new EventData { DataPointer = (IntPtr)(&durationMs), Size = sizeof(long) };
                WriteEventCore(2, 2, data);
            }
        }
    }

    public static class Keywords
    {
        public const EventKeywords Requests = (EventKeywords)0x0001;
        public const EventKeywords Errors   = (EventKeywords)0x0002;
        public const EventKeywords Database = (EventKeywords)0x0004;
    }
}
```

### Best Practices

- **Check `IsEnabled()` before `WriteEvent()`** for events with parameters - avoids boxing
- **Use `WriteEventCore` for hot paths** - eliminates params array allocation
- **Event IDs must be unique** within an EventSource and assigned sequentially
- **Name the EventSource** using reverse-DNS: `MyCompany.MyApp.Component`
- **Use Keywords** to allow consumers to filter which events they receive
- **Keep it singleton** - one static instance per EventSource type

## EventCounter for Aggregate Metrics

EventCounters aggregate data in-process and report statistics periodically. Newer code should prefer `System.Diagnostics.Metrics`, but EventCounters are still used by the runtime and many libraries.

```csharp
[EventSource(Name = "MyApp.Metrics")]
public sealed class AppMetricsEventSource : EventSource
{
    public static readonly AppMetricsEventSource Log = new();

    private EventCounter? _requestDuration;
    private IncrementingEventCounter? _requestCount;
    private PollingCounter? _activeConnections;
    private IncrementingPollingCounter? _totalBytesReceived;
    private long _activeConnectionCount;
    private long _bytesReceived;

    protected override void OnEventCommand(EventCommandEventArgs command)
    {
        if (command.Command == EventCommand.Enable)
        {
            _requestDuration ??= new EventCounter("request-duration", this)
            {
                DisplayName = "Request Duration",
                DisplayUnits = "ms"
            };

            _requestCount ??= new IncrementingEventCounter("request-count", this)
            {
                DisplayName = "Request Count",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _activeConnections ??= new PollingCounter("active-connections", this,
                () => Volatile.Read(ref _activeConnectionCount))
            {
                DisplayName = "Active Connections"
            };

            _totalBytesReceived ??= new IncrementingPollingCounter("bytes-received", this,
                () => Volatile.Read(ref _bytesReceived))
            {
                DisplayName = "Bytes Received",
                DisplayUnits = "B",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };
        }
    }

    public void RecordRequestDuration(double ms) => _requestDuration?.WriteMetric(ms);
    public void IncrementRequestCount() => _requestCount?.Increment();
    public void ConnectionOpened() => Interlocked.Increment(ref _activeConnectionCount);
    public void ConnectionClosed() => Interlocked.Decrement(ref _activeConnectionCount);
    public void BytesReceived(long count) => Interlocked.Add(ref _bytesReceived, count);

    protected override void Dispose(bool disposing)
    {
        _requestDuration?.Dispose();
        _requestCount?.Dispose();
        _activeConnections?.Dispose();
        _totalBytesReceived?.Dispose();
        base.Dispose(disposing);
    }
}
```

### Counter Types

| Type                         | Caller Reports    | Tool Shows         | Use Case             |
|------------------------------|-------------------|--------------------|----------------------|
| `EventCounter`               | Individual values | Mean, Min, Max, SD | Latency, size        |
| `IncrementingEventCounter`   | Increments        | Rate per interval  | Request count        |
| `PollingCounter`             | Callback          | Current value      | Queue depth, CPU %   |
| `IncrementingPollingCounter` | Callback (total)  | Rate of change     | Bytes sent, GC count |

## In-Process Listening (EventListener)

```csharp
public sealed class MetricsEventListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name == "MyApp.Metrics" || source.Name == "System.Runtime")
        {
            EnableEvents(source, EventLevel.Informational, EventKeywords.All,
                new Dictionary<string, string> { ["EventCounterIntervalSec"] = "5" });
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventName != "EventCounters") return;

        for (int i = 0; i < eventData.Payload?.Count; i++)
        {
            if (eventData.Payload[i] is IDictionary<string, object> payload)
            {
                var name = payload.TryGetValue("DisplayName", out var d) ? d.ToString() : "?";
                var value = payload.TryGetValue("Mean", out var m) ? m
                          : payload.TryGetValue("Increment", out var inc) ? inc
                          : "N/A";
                Console.WriteLine($"  {name}: {value}");
            }
        }
    }
}
```

### Wildcard Listening (.NET 9+)

```csharp
// Listen to all meters with prefix
EnableEvents(source, EventLevel.Informational, (EventKeywords)0x3,
    new Dictionary<string, string?> { { "Metrics", "MyCompany*" } });

// Listen to ALL meters
EnableEvents(source, EventLevel.Informational, (EventKeywords)0x3,
    new Dictionary<string, string?> { { "Metrics", "*" } });
```

## DiagnosticSource vs EventSource

| Feature                 | DiagnosticSource                       | EventSource                      |
|-------------------------|----------------------------------------|----------------------------------|
| **Payload**             | Rich typed objects (anonymous types)   | Primitives only                  |
| **Consumers**           | In-process only                        | In-proc + out-of-proc (ETW/Pipe) |
| **Overhead (disabled)** | ~2 ns (`IsEnabled` check)              | ~2 ns (`IsEnabled` check)        |
| **Use case**            | Library instrumentation for APMs       | System-level tracing, ETW        |
| **Filtering**           | Per-event name, with payload predicate | By EventLevel and Keywords       |

### When to Use DiagnosticSource

- You need to pass complex objects to listeners (HttpContext, DbCommand)
- You're building library instrumentation that APMs will consume
- Backward compatibility with pre-OTel instrumentation

### When to Use EventSource

- You need out-of-process collection via ETW or EventPipe
- You need `dotnet-trace` / `dotnet-counters` / PerfView compatibility
- You're instrumenting runtime-level or system-level code

## Dual-Write Pattern

Log via `ILogger` AND raise `DiagnosticSource` events for APM integration:

```csharp
public sealed class OrderService
{
    private static readonly DiagnosticListener _diagnostics = new("MyApp.Orders");
    private readonly ILogger<OrderService> _logger;

    public OrderService(ILogger<OrderService> logger) => _logger = logger;

    public async Task<Order> PlaceOrderAsync(OrderRequest request)
    {
        // Start activity for distributed tracing
        using var activity = OrderTelemetry.Source.StartActivity("PlaceOrder");
        activity?.SetTag("order.customer_id", request.CustomerId);

        // Raise DiagnosticSource event (rich payload for APM tools)
        if (_diagnostics.IsEnabled("OrderPlacing"))
            _diagnostics.Write("OrderPlacing", new { Request = request, Activity = activity });

        try
        {
            var sw = Stopwatch.StartNew();
            var order = await ProcessOrderAsync(request);
            sw.Stop();

            // Log (for structured log storage)
            LogOrderPlaced(request.CustomerId, order.Id, sw.Elapsed.TotalMilliseconds);

            // Metric
            OrderTelemetry.Instance.RecordOrderPlaced(order.Id, sw.Elapsed.TotalSeconds);

            // DiagnosticSource completion event
            if (_diagnostics.IsEnabled("OrderPlaced"))
                _diagnostics.Write("OrderPlaced", new { Order = order, Duration = sw.Elapsed });

            return order;
        }
        catch (Exception ex)
        {
            LogOrderFailed(request.CustomerId, ex);
            OrderTelemetry.Instance.RecordOrderFailed(request.CustomerId, ex);

            if (_diagnostics.IsEnabled("OrderFailed"))
                _diagnostics.Write("OrderFailed", new { Request = request, Exception = ex });

            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order placed for customer {CustomerId}: {OrderId} in {DurationMs}ms")]
    private partial void LogOrderPlaced(string customerId, string orderId, double durationMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Order failed for customer {CustomerId}")]
    private partial void LogOrderFailed(string customerId, Exception ex);
}
```

## Performance Considerations

### EventSource Hot-Path Optimization

```csharp
// BAD: WriteEvent with params creates object[] allocation
[Event(1)]
public void FastEvent(int a, int b, long c) => WriteEvent(1, a, b, c);

// GOOD: WriteEventCore avoids allocation
[Event(1)]
public unsafe void FastEvent(int a, int b, long c)
{
    if (IsEnabled())
    {
        EventData* data = stackalloc EventData[3];
        data[0] = new EventData { DataPointer = (IntPtr)(&a), Size = 4 };
        data[1] = new EventData { DataPointer = (IntPtr)(&b), Size = 4 };
        data[2] = new EventData { DataPointer = (IntPtr)(&c), Size = 8 };
        WriteEventCore(1, 3, data);
    }
}
```

### DiagnosticSource Hot-Path Pattern

```csharp
// Always guard with IsEnabled - Write allocates the anonymous type
if (_diagnostics.IsEnabled("EventName"))
{
    _diagnostics.Write("EventName", new { Data = payload });
}

// Even better: use IsEnabled with payload hint for advanced filtering
if (_diagnostics.IsEnabled("EventName", payload))
{
    _diagnostics.Write("EventName", new { Data = payload });
}
```

## Migration to System.Diagnostics.Metrics

For new code, prefer `System.Diagnostics.Metrics` over EventCounters:

| EventCounter Type            | Metrics Equivalent     |
|------------------------------|------------------------|
| `EventCounter` (mean)        | `Histogram<T>`         |
| `IncrementingEventCounter`   | `Counter<T>`           |
| `PollingCounter`             | `ObservableGauge<T>`   |
| `IncrementingPollingCounter` | `ObservableCounter<T>` |

Benefits of migration:
- Standardized with OpenTelemetry
- Better DI integration via `IMeterFactory`
- Richer tag support (multi-dimensional)
- `MeterListener` for in-process, OTel exporters for out-of-process
- `MetricCollector<T>` for testing
