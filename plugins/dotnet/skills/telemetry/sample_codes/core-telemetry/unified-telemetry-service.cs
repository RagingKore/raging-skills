// Unified Telemetry Service Pattern
// A single class per bounded context that encapsulates tracing, metrics, and logging.
// This is the recommended pattern for production .NET applications.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyApp.Telemetry;

// ============================================================================
// 1. INTERFACE (for testability and abstraction)
// ============================================================================

public interface IOrderTelemetry
{
    Activity? StartProcessOrder(string orderId, string customerType);
    Activity? StartPaymentProcessing(string orderId, string paymentMethod);
    void RecordOrderPlaced(string orderId, string customerType, double durationSecs);
    void RecordOrderFailed(string orderId, string reason);
    void RecordOrderValue(decimal amount, string currency);
    void RecordPaymentProcessed(string paymentMethod, double durationSecs, bool success);
}

// ============================================================================
// 2. IMPLEMENTATION (partial class for source-generated logging)
// ============================================================================

public sealed partial class OrderTelemetry : IOrderTelemetry
{
    // ---- Tracing: one ActivitySource per component ----
    // Static because ActivitySource is thread-safe and should be shared
    internal static readonly ActivitySource Source = new("MyApp.Orders", "1.0.0");

    // ---- Metrics: created via IMeterFactory for DI isolation ----
    private readonly Counter<long> _ordersPlaced;
    private readonly Counter<long> _ordersFailed;
    private readonly Histogram<double> _orderDuration;
    private readonly Histogram<double> _orderValue;
    private readonly Counter<long> _paymentsProcessed;
    private readonly Histogram<double> _paymentDuration;

    // ---- Logging: ILogger for structured logs ----
    private readonly ILogger _logger;

    public OrderTelemetry(IMeterFactory meterFactory, ILogger<OrderTelemetry> logger)
    {
        _logger = logger;

        // Create a Meter scoped to DI container (not a static Meter)
        var meter = meterFactory.Create("MyApp.Orders");

        // Follow OTel naming conventions: lowercase dotted, with units
        _ordersPlaced = meter.CreateCounter<long>(
            name: "myapp.orders.placed",
            unit: "{order}",
            description: "Total number of orders successfully placed");

        _ordersFailed = meter.CreateCounter<long>(
            name: "myapp.orders.failed",
            unit: "{order}",
            description: "Total number of orders that failed processing");

        _orderDuration = meter.CreateHistogram<double>(
            name: "myapp.orders.duration",
            unit: "s",
            description: "Time to process an order",
            // Custom bucket boundaries for better granularity
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30]
            });

        _orderValue = meter.CreateHistogram<double>(
            name: "myapp.orders.value",
            unit: "{currency_unit}",
            description: "Monetary value of orders");

        _paymentsProcessed = meter.CreateCounter<long>(
            name: "myapp.orders.payments_processed",
            unit: "{payment}",
            description: "Total payments processed");

        _paymentDuration = meter.CreateHistogram<double>(
            name: "myapp.orders.payment_duration",
            unit: "s",
            description: "Payment processing time");
    }

    // ---- Tracing Methods ----

    public Activity? StartProcessOrder(string orderId, string customerType)
    {
        // StartActivity returns null if no listener is subscribed (zero overhead)
        var activity = Source.StartActivity("ProcessOrder", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("order.id", orderId);
            activity.SetTag("order.customer_type", customerType);
        }
        LogOrderProcessingStarted(orderId, customerType);
        return activity;
    }

    public Activity? StartPaymentProcessing(string orderId, string paymentMethod)
    {
        var activity = Source.StartActivity("ProcessPayment", ActivityKind.Client);
        if (activity is not null)
        {
            activity.SetTag("order.id", orderId);
            activity.SetTag("payment.method", paymentMethod);
        }
        return activity;
    }

    // ---- Metrics Methods ----

    public void RecordOrderPlaced(string orderId, string customerType, double durationSecs)
    {
        // Use TagList for 4+ tags (avoids object[] allocation)
        var tags = new TagList
        {
            { "order.customer_type", customerType },
            { "order.status", "placed" }
        };
        _ordersPlaced.Add(1, tags);
        _orderDuration.Record(durationSecs, tags);

        // Also update the current Activity
        Activity.Current?.SetStatus(ActivityStatusCode.Ok);
        LogOrderPlaced(orderId, customerType, durationSecs);
    }

    public void RecordOrderFailed(string orderId, string reason)
    {
        _ordersFailed.Add(1,
            new KeyValuePair<string, object?>("order.failure_reason", reason));

        // Mark the current Activity as errored
        Activity.Current?.SetStatus(ActivityStatusCode.Error, reason);
        LogOrderFailed(orderId, reason);
    }

    public void RecordOrderValue(decimal amount, string currency)
    {
        _orderValue.Record((double)amount,
            new KeyValuePair<string, object?>("currency", currency));
    }

    public void RecordPaymentProcessed(string paymentMethod, double durationSecs, bool success)
    {
        var tags = new TagList
        {
            { "payment.method", paymentMethod },
            { "payment.success", success }
        };
        _paymentsProcessed.Add(1, tags);
        _paymentDuration.Record(durationSecs, tags);
    }

    // ---- Source-Generated Log Methods ----
    // Zero-allocation, compile-time template parsing, auto IsEnabled check

    [LoggerMessage(EventId = 1000, Level = LogLevel.Information,
        Message = "Processing order {OrderId} for customer type {CustomerType}")]
    private partial void LogOrderProcessingStarted(string orderId, string customerType);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Order {OrderId} placed for {CustomerType} in {DurationSecs:F3}s")]
    private partial void LogOrderPlaced(string orderId, string customerType, double durationSecs);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning,
        Message = "Order {OrderId} failed: {Reason}")]
    private partial void LogOrderFailed(string orderId, string reason);
}

// ============================================================================
// 3. REGISTRATION
// ============================================================================

public static class TelemetryServiceExtensions
{
    public static IServiceCollection AddOrderTelemetry(this IServiceCollection services)
    {
        // IMeterFactory is auto-registered in .NET 8+ hosts
        // ILogger<T> is auto-registered by AddLogging()
        services.AddSingleton<IOrderTelemetry, OrderTelemetry>();
        return services;
    }
}

// ============================================================================
// 4. USAGE IN A HANDLER/SERVICE
// ============================================================================

public class PlaceOrderHandler
{
    private readonly IOrderTelemetry _telemetry;
    private readonly IOrderRepository _repository;

    public PlaceOrderHandler(IOrderTelemetry telemetry, IOrderRepository repository)
    {
        _telemetry = telemetry;
        _repository = repository;
    }

    public async Task<OrderResult> HandleAsync(PlaceOrderCommand command, CancellationToken ct)
    {
        // Start a traced span (auto-disposed at end of scope)
        using var activity = _telemetry.StartProcessOrder(command.OrderId, command.CustomerType);
        var sw = Stopwatch.StartNew();

        try
        {
            // Nested span for payment
            using (var paymentActivity = _telemetry.StartPaymentProcessing(
                command.OrderId, command.PaymentMethod))
            {
                var paymentSw = Stopwatch.StartNew();
                await ProcessPaymentAsync(command);
                paymentSw.Stop();

                _telemetry.RecordPaymentProcessed(
                    command.PaymentMethod, paymentSw.Elapsed.TotalSeconds, success: true);
            }

            var result = await _repository.SaveOrderAsync(command, ct);
            sw.Stop();

            _telemetry.RecordOrderPlaced(command.OrderId, command.CustomerType, sw.Elapsed.TotalSeconds);
            _telemetry.RecordOrderValue(result.TotalAmount, result.Currency);

            return result;
        }
        catch (PaymentDeclinedException)
        {
            _telemetry.RecordOrderFailed(command.OrderId, "payment_declined");
            throw;
        }
        catch (InsufficientInventoryException)
        {
            _telemetry.RecordOrderFailed(command.OrderId, "insufficient_inventory");
            throw;
        }
    }

    private Task ProcessPaymentAsync(PlaceOrderCommand command) => Task.CompletedTask; // placeholder
}

// Placeholder types for compilation
public record PlaceOrderCommand(string OrderId, string CustomerType, string PaymentMethod);
public record OrderResult(string OrderId, decimal TotalAmount, string Currency);
public interface IOrderRepository { Task<OrderResult> SaveOrderAsync(PlaceOrderCommand cmd, CancellationToken ct); }
public class PaymentDeclinedException : Exception;
public class InsufficientInventoryException : Exception;
