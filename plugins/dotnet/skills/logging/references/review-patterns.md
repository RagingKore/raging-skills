# Logging Code Review & Migration Guide

Patterns for reviewing existing logging code and migrating to high-performance `[LoggerMessage]` source generation.

## Review Priority Order

When reviewing a codebase, fix issues in this order:

### Priority 1: String Interpolation in Log Calls (Critical)

String interpolation **always** allocates, even when the log level is disabled.

```csharp
// BAD - allocates every time, regardless of log level
_logger.LogDebug($"Processing order {orderId} for customer {customerId}");
_logger.LogInformation($"Elapsed: {stopwatch.ElapsedMilliseconds}ms");

// GOOD - zero allocation when Debug is disabled
[LoggerMessage(Level = LogLevel.Debug, Message = "Processing order {OrderId} for customer {CustomerId}")]
partial void LogProcessingOrder(string orderId, string customerId);

[LoggerMessage(Level = LogLevel.Information, Message = "Elapsed: {ElapsedMs}ms")]
partial void LogElapsed(long elapsedMs);
```

**Detection pattern:** Search for `Log(Debug|Information|Warning|Error|Critical)\(\$"` in the codebase.

### Priority 2: High-Frequency Extension Method Calls

Extension methods like `LogInformation("...", args)` box value types and parse the template on every call.

```csharp
// BEFORE - boxes 'count' (int -> object), parses template every call
_logger.LogInformation("Processed {Count} items in {Duration}ms", count, duration);

// AFTER - strongly typed, template pre-parsed at compile time
[LoggerMessage(Level = LogLevel.Information, Message = "Processed {Count} items in {Duration}ms")]
partial void LogProcessed(int count, long duration);
```

**When to prioritize:** Focus on log calls in hot paths (request pipelines, loops, event handlers).

### Priority 3: Missing Level Guards for Expensive Operations

```csharp
// BAD - serializes payload even when Debug is disabled
_logger.LogDebug("Request payload: {Payload}", JsonSerializer.Serialize(request));

// GOOD - guard expensive operations
[LoggerMessage(Level = LogLevel.Debug, Message = "Request payload: {Payload}", SkipEnabledCheck = true)]
partial void LogPayload(string payload);

if (_logger.IsEnabled(LogLevel.Debug))
{
    LogPayload(JsonSerializer.Serialize(request));
}
```

### Priority 4: Unstructured String Concatenation

```csharp
// BAD - loses structure, prevents filtering
_logger.LogInformation("User " + userId + " performed " + action + " on " + resource);

// GOOD - structured, filterable
[LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} performed {Action} on {Resource}")]
partial void LogUserAction(string userId, string action, string resource);
```

## Migration Steps

### Step 1: Make the class partial

```csharp
// Before
public sealed class OrderService
{
    // ...
}

// After
public sealed partial class OrderService
{
    // ...
}
```

### Step 2: Add logging methods

Group related log messages in the class that uses them:

```csharp
public sealed partial class OrderService
{
    private readonly ILogger<OrderService> _logger;

    // Group logging methods together
    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Order {OrderId} created for customer {CustomerId}")]
    partial void LogOrderCreated(string orderId, string customerId);

    [LoggerMessage(EventId = 101, Level = LogLevel.Warning, Message = "Order {OrderId} payment failed: {Reason}")]
    partial void LogPaymentFailed(string orderId, string reason);

    [LoggerMessage(EventId = 102, Level = LogLevel.Error, Message = "Order {OrderId} processing failed")]
    partial void LogProcessingFailed(Exception ex, string orderId);
}
```

### Step 3: Replace call sites

```csharp
// Before
_logger.LogInformation("Order {OrderId} created for customer {CustomerId}", orderId, customerId);

// After
LogOrderCreated(orderId, customerId);
```

### Step 4: Verify structured output

Run the app with JSON console formatter to verify structured properties are captured:

```json
{
  "EventId": 100,
  "LogLevel": "Information",
  "Category": "MyApp.OrderService",
  "Message": "Order ORD-123 created for customer CUST-456",
  "State": {
    "OrderId": "ORD-123",
    "CustomerId": "CUST-456"
  }
}
```

## EventId Strategy

Assign EventId ranges per domain area to avoid conflicts:

| Range   | Domain         |
|---------|----------------|
| 100-199 | Orders         |
| 200-299 | Authentication |
| 300-399 | Payments       |
| 400-499 | Notifications  |
| 1000+   | Infrastructure |

## Template Naming Conventions

Use PascalCase for template placeholders (they become property names in structured output):

```csharp
// BAD - inconsistent casing in structured output
[LoggerMessage(Message = "Processing {order_id} for {customer}")]

// GOOD - PascalCase placeholders
[LoggerMessage(Message = "Processing {OrderId} for {CustomerId}")]
```

## Bulk Migration with Analyzers

The .NET SDK includes analyzer `SYSLIB1054` that suggests migration from `LoggerMessage.Define` to `[LoggerMessage]`. Enable it in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.SYSLIB1054.severity = suggestion
```

For migrating from `LoggerExtensions` (e.g., `LogInformation`), there's no built-in analyzer, but the pattern is mechanical: search and replace call sites, adding `partial` methods as you go.
