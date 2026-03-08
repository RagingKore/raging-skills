---
name: logging
description: Add and review logging in .NET classes using [LoggerMessage] source generation for zero-allocation, high-performance logs. Covers ILogger patterns (instance, extension, static methods), BeginScope for contextual metadata, structured logging, EventId/LogLevel configuration, exception handling, data redaction, and migration from LoggerExtensions. Use for "add logging", "LoggerMessage attribute", "ILogger setup", "logging performance", "structured logs", "BeginScope", "review logging code", "fix logging", or any .NET 6+ logging task.
---

# .NET High-Performance Logging Expert

Expert-level guidance for `[LoggerMessage]` source-generated logging in .NET 6+.

## Quick Decision Matrix

| Scenario              | Pattern              | Why                                         |
|-----------------------|----------------------|---------------------------------------------|
| Class-specific logs   | **Instance method**  | Private, clean call sites, no ILogger param |
| Shared across classes | **Extension method** | Reusable, but pollutes IntelliSense         |
| Utility/static class  | **Static method**    | Explicit ILogger param required             |

## The Three Patterns

### 1. Instance Methods (Recommended for most cases)

```csharp
public sealed partial class MyService {
    private readonly ILogger _logger;  // Source gen finds this automatically

    public MyService(ILogger<MyService> logger) => _logger = logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Processing {ItemId}")]
    partial void LogProcessing(string itemId);

    public void Process(string id)
    {
        LogProcessing(id);  // Clean call site, no _logger param
    }
}
```

**.NET 9+ Primary Constructor:**
```csharp
public sealed partial class MyService(ILogger<MyService> logger) {
    [LoggerMessage(Level = LogLevel.Debug, Message = "Processing {ItemId}")]
    partial void LogProcessing(string itemId);
}
```

**Benefits:**
- Private to the class (no IntelliSense pollution)
- Cleaner call sites
- Logger found automatically from `_logger` field or primary constructor
- If both field and primary constructor exist, field takes precedence

### 2. Extension Methods

```csharp
public static partial class MyServiceLogMessages {
    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection lost to {Host}")]
    public static partial void ConnectionLost(this ILogger logger, string host);
}

// Usage: _logger.ConnectionLost("server1");
```

**Requirements:**
- Must be in a **non-nested**, **non-generic**, **static** class
- Add `this` before `ILogger` parameter

**When to use:**
- Shared logging patterns across multiple classes
- Extension method style preferred by team

**Drawbacks:**
- Pollutes IntelliSense on every `ILogger`
- Requires separate static class

### 3. Static Methods

```csharp
public static partial class Log {
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed: {Error}")]
    public static partial void Failed(ILogger logger, string error);
}

// Usage: Log.Failed(_logger, "timeout");
```

**When to use:**
- Explicit control over which logger is used
- Static utility classes

## Attribute Properties

| Property           | Type     | Description                             |
|--------------------|----------|-----------------------------------------|
| `EventId`          | int      | Unique ID for filtering/correlation     |
| `EventName`        | string   | Human-readable event name               |
| `Level`            | LogLevel | Static log level (omit for dynamic)     |
| `Message`          | string   | Template with `{Placeholders}`          |
| `SkipEnabledCheck` | bool     | Skip auto-generated `IsEnabled()` guard |

### Constructor Overloads

```csharp
[LoggerMessage(Level = LogLevel.Debug, Message = "...")]           // Most common
[LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "...")]
[LoggerMessage(Message = "...")]                                    // Dynamic level
[LoggerMessage(Level = LogLevel.Debug)]                            // No message (params become state)
```

## Dynamic Log Level

Omit `Level` from attribute, add `LogLevel` parameter:

```csharp
[LoggerMessage(EventId = 1, Message = "Operation {Op} completed")]
partial void LogOperation(string op, LogLevel level);

// Usage:
LogOperation("save", LogLevel.Debug);
LogOperation("critical-save", LogLevel.Warning);
```

## Exception Handling

First `Exception` parameter is treated specially (attached to log, not in message):

```csharp
[LoggerMessage(Level = LogLevel.Error, Message = "Failed to process {ItemId}")]
partial void LogFailed(Exception ex, string itemId);

// Usage:
catch (Exception ex)
{
    LogFailed(ex, itemId);  // Exception attached automatically
}
```

**Warning:** Don't include the exception in the message template:
```csharp
// BAD - causes SYSLIB0025 warning
[LoggerMessage(Message = "Error: {Ex}")]
partial void Bad(Exception ex);

// GOOD
[LoggerMessage(Message = "Processing failed")]
partial void Good(Exception ex);
```

## Extra Structured Data

Parameters not in the message template are still captured as structured properties:

```csharp
[LoggerMessage(Level = LogLevel.Debug, Message = "Request completed")]
partial void LogRequest(
    string correlationId,  // Not in message, but captured
    int statusCode,        // Not in message, but captured
    string endpoint);      // Not in message, but captured
```

JSON output includes all parameters:
```json
{
  "Message": "Request completed",
  "State": {
    "correlationId": "abc-123",
    "statusCode": 200,
    "endpoint": "/api/users"
  }
}
```

## Log-Level Guarded Optimizations

Source-generated methods include automatic `IsEnabled()` checks. When a level is disabled, the method returns immediately with zero allocations.

For **expensive parameter evaluation**, use `SkipEnabledCheck` and guard manually:

```csharp
[LoggerMessage(Level = LogLevel.Debug, Message = "Details: {Data}", SkipEnabledCheck = true)]
partial void LogDetails(string data);

// Manual guard at call site:
if (_logger.IsEnabled(LogLevel.Debug))
{
    var data = ExpensiveComputation();  // Only called if Debug enabled
    LogDetails(data);
}
```

## Format Specifiers

Use standard .NET format specifiers in templates:

```csharp
[LoggerMessage(Level = LogLevel.Information, Message = "Value: {Value:E}")]        // Scientific
[LoggerMessage(Level = LogLevel.Information, Message = "Price: {Price:C}")]        // Currency
[LoggerMessage(Level = LogLevel.Information, Message = "Date: {Date:yyyy-MM-dd}")] // Custom date
[LoggerMessage(Level = LogLevel.Information, Message = "Percent: {Rate:P2}")]      // Percentage
```

## Constraints

1. Methods must be `partial` and return `void`
2. Method names must NOT start with underscore
3. Parameter names must NOT start with underscore
4. Methods cannot be generic
5. Static methods require `ILogger` parameter
6. Requires C# 9+ compiler (.NET 5+ SDK)
7. Containing class must also be `partial`

## Performance Comparison

| Approach             | Time (enabled) | Time (disabled) | Allocations |
|----------------------|----------------|-----------------|-------------|
| Source generator     | ~49 ns         | ~7 ns           | **0 bytes** |
| LoggerExtensions     | ~98 ns         | ~30 ns          | 64 bytes    |
| String interpolation | ~553 ns        | ~553 ns         | 664 bytes   |

**Key insight:** Source generator skips ALL work when log level is disabled.

## BeginScope Integration

Use `BeginScope` alongside source-generated methods for contextual data:

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["CorrelationId"] = correlationId,
    ["UserId"] = userId
}))
{
    LogProcessingStarted();   // Scope data attached to all logs in block
    LogProcessingCompleted();
}
```

Enable scopes in `appsettings.json`:
```json
{
  "Logging": {
    "Console": {
      "IncludeScopes": true
    }
  }
}
```

## Data Redaction (.NET 8+)

Prevent sensitive data leaks using `Microsoft.Extensions.Telemetry` and `Microsoft.Extensions.Compliance.Redaction`:

```csharp
// 1. Define data classification
[LoggerMessage(LogLevel.Information, "User SSN: {Ssn}")]
public static partial void LogUserInfo(
    this ILogger logger,
    [PrivateData] string ssn);  // Attribute marks sensitive data

// 2. Register redactors in DI
services.AddLogging(builder => builder.EnableRedaction());
services.AddRedaction(builder =>
{
    builder.SetRedactor<StarRedactor>(new PrivateDataAttribute().Classification);
});
```

Output: `User SSN: *****`

For more details, see [references/redaction.md](references/redaction.md).

## Code Review Checklist

When reviewing existing logging code, check for these issues:

| Issue                                                      | Severity   | Fix                                                                         |
|------------------------------------------------------------|------------|-----------------------------------------------------------------------------|
| `_logger.LogInformation($"...")` with string interpolation | **High**   | Always allocates, even when disabled. Replace with source-generated method  |
| `_logger.LogDebug("...", param)` extension methods         | **Medium** | Boxes value types, parses template every call. Migrate to `[LoggerMessage]` |
| Missing `IsEnabled()` guard before expensive computation   | **Medium** | Add manual guard with `SkipEnabledCheck = true`                             |
| Exception in message template `{Ex}`                       | **Low**    | Remove from template; first `Exception` param auto-attached                 |
| Inconsistent EventIds across the project                   | **Low**    | Assign unique EventIds; consider per-class ranges                           |
| Missing structured data (string concatenation in messages) | **Medium** | Use `{Placeholder}` template params instead                                 |

See [references/review-patterns.md](references/review-patterns.md) for detailed review and migration guidance.

## Common Gotchas

| Issue                                     | Cause                  | Fix                                                 |
|-------------------------------------------|------------------------|-----------------------------------------------------|
| "Partial method must have implementation" | Source gen not running | Rebuild; check C# 9+ and `partial class`            |
| Logger not found                          | Field named wrong      | Use `_logger`, `logger`, or `ILogger` parameter     |
| SYSLIB1006: Invalid parameter type        | Unsupported type       | Use primitives, strings, or types with `ToString()` |
| SYSLIB0025: Template includes exception   | `{Ex}` in message      | Remove; exception auto-attached from first param    |

## Learn More

| Topic                       | How to Find                                                                                                             |
|-----------------------------|-------------------------------------------------------------------------------------------------------------------------|
| Source generation deep dive | `microsoft_docs_fetch(url="https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/source-generation")`        |
| High-performance logging    | `microsoft_docs_fetch(url="https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/high-performance-logging")` |
| Data redaction in .NET      | `microsoft_docs_search(query=".NET data redaction compliance logging")`                                                 |
| Custom logging providers    | `microsoft_docs_search(query=".NET custom logging provider ILoggerProvider")`                                           |
| Logging for library authors | `microsoft_docs_search(query=".NET logging guidance library authors")`                                                  |
