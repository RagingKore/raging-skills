# Data Redaction in Logging

Prevent sensitive data from appearing in logs using the .NET compliance and redaction libraries.

## Packages Required

```xml
<PackageReference Include="Microsoft.Extensions.Telemetry" Version="9.*" />
<PackageReference Include="Microsoft.Extensions.Compliance.Redaction" Version="9.*" />
```

## How It Works

1. **Data classification** - Annotate sensitive parameters with classification attributes
2. **Redactor registration** - Configure which redactor handles each classification
3. **Pipeline integration** - Enable redaction in the logging pipeline
4. **Automatic redaction** - Classified data is redacted before being written to any sink

## Setup

### 1. Define Classifications

Use built-in attributes or create custom ones:

```csharp
using Microsoft.Extensions.Compliance.Classification;

// Built-in from Microsoft.Extensions.Compliance.Testing:
// [PrivateData], [SensitiveData]

// Or define custom classifications:
public sealed class PiiDataAttribute : DataClassificationAttribute
{
    public PiiDataAttribute() : base(new DataClassification("Taxonomy", "PII")) { }
}

public sealed class FinancialDataAttribute : DataClassificationAttribute
{
    public FinancialDataAttribute() : base(new DataClassification("Taxonomy", "Financial")) { }
}
```

### 2. Apply to Log Methods

```csharp
public static partial class AuditLog
{
    [LoggerMessage(LogLevel.Information, "User {Email} accessed account {AccountNumber}")]
    public static partial void UserAccess(
        this ILogger logger,
        [PiiData] string email,
        [FinancialData] string accountNumber);

    [LoggerMessage(LogLevel.Warning, "Failed login for {Username} from {IpAddress}")]
    public static partial void FailedLogin(
        this ILogger logger,
        [PiiData] string username,
        string ipAddress);  // Not classified - logged as-is
}
```

### 3. Register Redactors

```csharp
var builder = WebApplication.CreateBuilder(args);

// Enable redaction in the logging pipeline
builder.Logging.EnableRedaction();

// Configure redactors per classification
builder.Services.AddRedaction(redaction =>
{
    // Erase PII data completely
    redaction.SetRedactor<ErasingRedactor>(
        new PiiDataAttribute().Classification);

    // Mask financial data with stars
    redaction.SetRedactor<StarRedactor>(
        new FinancialDataAttribute().Classification);
});
```

## Built-In Redactors

| Redactor | Behavior | Example |
|----------|----------|---------|
| `ErasingRedactor` | Replaces with empty string | `""` |
| `StarRedactor` | Replaces with asterisks | `*****` |
| `HmacRedactor` | Produces consistent HMAC hash | `a1b2c3d4...` (same input = same hash) |

### HMAC Redactor (for correlation without exposure)

```csharp
redaction.SetHmacRedactor(options =>
{
    options.Key = Convert.ToBase64String(keyBytes); // 256-bit key
    options.KeyId = 1;
}, new PiiDataAttribute().Classification);
```

Output: Hashed value that's consistent for the same input, allowing correlation in logs without exposing the raw value.

## Output Examples

Given: `AuditLog.UserAccess(logger, "john@example.com", "1234-5678")`

| Configuration | Output |
|---------------|--------|
| No redaction | `User john@example.com accessed account 1234-5678` |
| Erasing PII, Star financial | `User  accessed account *****` |
| HMAC PII, Star financial | `User p3xKQ7... accessed account *****` |

## Custom Redactors

Implement `Redactor` for custom behavior:

```csharp
public sealed class PartialMaskRedactor : Redactor
{
    public override int GetRedactedLength(ReadOnlySpan<char> input) => input.Length;

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        // Show last 4 chars, mask the rest
        var maskLength = Math.Max(0, source.Length - 4);
        destination[..maskLength].Fill('*');
        source[maskLength..].CopyTo(destination[maskLength..]);
        return source.Length;
    }
}

// "john@example.com" -> "************.com"
// "1234-5678" -> "*****5678"
```

## Learn More

| Topic | How to Find |
|-------|-------------|
| Data classification | `microsoft_docs_search(query=".NET data classification compliance")` |
| Redaction API reference | `microsoft_docs_search(query="Microsoft.Extensions.Compliance.Redaction API")` |
| Telemetry enrichment | `microsoft_docs_search(query=".NET telemetry enrichment logging")` |
