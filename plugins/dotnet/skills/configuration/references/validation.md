# Configuration Validation

Complete reference for validating configuration in .NET console apps and ASP.NET Core.

## Table of Contents

- [DataAnnotations Validation](#dataannotations-validation)
- [Delegate Validation](#delegate-validation)
- [IValidateOptions for Complex Validation](#ivalidateoptions-for-complex-validation)
- [ValidateOnStart (Eager Validation)](#validateonstart-eager-validation)
- [Recursive Validation](#recursive-validation)
- [Compile-Time Validation Source Generator](#compile-time-validation-source-generator)
- [IValidatableObject (Self-Validating Options)](#ivalidatableobject-self-validating-options)

---

## DataAnnotations Validation

Use `System.ComponentModel.DataAnnotations` attributes on options classes.

**Required package:** `Microsoft.Extensions.Options.DataAnnotations`
(Included automatically in ASP.NET Core web SDK)

### Options Class

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class SmtpOptions {
    public const string SectionName = "Smtp";

    [Required(ErrorMessage = "SMTP host is required")]
    public required string Host { get; set; }

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = 587;

    [Required, EmailAddress]
    public required string FromAddress { get; set; }

    [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Username must be alphanumeric")]
    public string? Username { get; set; }

    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string? Password { get; set; }
}
```

### Registration

```csharp
builder.Services
    .AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateDataAnnotations();
```

### Common DataAnnotation Attributes

| Attribute                            | Purpose                          |
|--------------------------------------|----------------------------------|
| `[Required]`                         | Value must be non-null/non-empty |
| `[Range(min, max)]`                  | Numeric/comparable range         |
| `[MinLength(n)]` / `[MaxLength(n)]`  | String/collection length         |
| `[RegularExpression(pattern)]`       | Regex match                      |
| `[EmailAddress]`                     | Valid email format               |
| `[Url]`                              | Valid URL format                 |
| `[StringLength(max, MinimumLength)]` | String length bounds             |
| `[AllowedValues(...)]`               | .NET 8+ restrict to allowed set  |
| `[DeniedValues(...)]`                | .NET 8+ deny specific values     |
| `[Length(min, max)]`                 | .NET 8+ collection length range  |
| `[Base64String]`                     | .NET 8+ valid Base64             |

---

## Delegate Validation

Add custom validation logic with lambda expressions.

```csharp
builder.Services
    .AddOptions<RetryOptions>()
    .Bind(builder.Configuration.GetSection("Retry"))
    .ValidateDataAnnotations()
    .Validate(options => {
        if (options.MaxRetries > 0 && options.DelayMs <= 0) {
            return false;
        }
        return true;
    }, "DelayMs must be positive when MaxRetries > 0")
    .Validate(options => options.MaxRetries <= 10,
        "MaxRetries cannot exceed 10");
```

Multiple `.Validate()` calls are supported; all are evaluated.

---

## IValidateOptions for Complex Validation

For validation logic that needs DI services or is too complex for delegates.

### Validator Class

```csharp
using System.Text;
using Microsoft.Extensions.Options;

public sealed class DatabaseOptionsValidator(
    IHostEnvironment env) : IValidateOptions<DatabaseOptions> {

    public ValidateOptionsResult Validate(string? name, DatabaseOptions options) {
        StringBuilder? failures = null;

        if (string.IsNullOrWhiteSpace(options.ConnectionString)) {
            (failures ??= new()).AppendLine("ConnectionString is required.");
        }

        if (env.IsProduction() && options.ConnectionString.Contains("localhost")) {
            (failures ??= new()).AppendLine(
                "Production cannot use localhost connection string.");
        }

        if (options.MaxRetryCount is < 0 or > 100) {
            (failures ??= new()).AppendLine(
                "MaxRetryCount must be between 0 and 100.");
        }

        if (options.CommandTimeout < TimeSpan.FromSeconds(1)) {
            (failures ??= new()).AppendLine(
                "CommandTimeout must be at least 1 second.");
        }

        return failures is not null
            ? ValidateOptionsResult.Fail(failures.ToString())
            : ValidateOptionsResult.Success;
    }
}
```

### Registration

```csharp
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services.AddSingleton<
    IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
```

Or using `TryAddEnumerable` to avoid duplicate registrations:

```csharp
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<
        IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>());
```

---

## ValidateOnStart (Eager Validation)

By default, validation runs lazily (first time options are resolved). Use `ValidateOnStart()` to fail at application startup.

### Fluent API

```csharp
builder.Services
    .AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(o => !string.IsNullOrEmpty(o.Host), "Host cannot be empty")
    .ValidateOnStart();
```

### AddOptionsWithValidateOnStart (shorthand)

```csharp
builder.Services
    .AddOptionsWithValidateOnStart<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateDataAnnotations();
```

### Handling Validation Failures

When using lazy validation, catch `OptionsValidationException`:

```csharp
try {
    SmtpOptions opts = optionsAccessor.Value;
} catch (OptionsValidationException ex) {
    foreach (string failure in ex.Failures) {
        logger.LogError("Config validation: {Failure}", failure);
    }
}
```

With `ValidateOnStart`, the app throws during `host.Build()` or `app.Run()` and the process exits.

---

## Recursive Validation

By default, DataAnnotations only validates the top-level options class. Use attributes to recurse into nested objects and collections.

### ValidateObjectMembers

```csharp
public sealed class AppOptions {
    [Required]
    public required string Name { get; set; }

    [ValidateObjectMembers]
    public DatabaseOptions Database { get; set; } = new();
}

public sealed class DatabaseOptions {
    [Required, MinLength(1)]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(1, 100)]
    public int MaxRetries { get; set; } = 3;
}
```

### ValidateEnumeratedItems

```csharp
public sealed class ClusterOptions {
    [ValidateEnumeratedItems]
    public List<ServerOptions> Servers { get; set; } = [];
}

public sealed class ServerOptions {
    [Required, RegularExpression(@"^[a-zA-Z0-9\-\.]+$")]
    public string HostName { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; }
}
```

Both attributes work with the compile-time validation source generator.

---

## Compile-Time Validation Source Generator

Generates `IValidateOptions<T>` implementations from DataAnnotations at compile time. Faster than runtime reflection.

### Enable

```xml
<PropertyGroup>
    <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
</PropertyGroup>
```

The source generator automatically creates validation code for options classes that use `ValidateDataAnnotations()`. No code changes needed.

For details: `microsoft_docs_fetch(url="https://learn.microsoft.com/en-us/dotnet/core/extensions/options-validation-generator")`

---

## IValidatableObject (Self-Validating Options)

Options classes can implement `IValidatableObject` for class-level cross-property validation.

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class FeatureOptions : IValidatableObject {
    [Required]
    public required string Name { get; set; }

    public int MinUsers { get; set; }
    public int MaxUsers { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext) {
        if (MaxUsers > 0 && MinUsers > MaxUsers) {
            yield return new ValidationResult(
                "MinUsers cannot exceed MaxUsers",
                [nameof(MinUsers), nameof(MaxUsers)]);
        }
    }
}
```

Register with `ValidateDataAnnotations()` - it automatically calls `IValidatableObject.Validate`.

---

## Post-Configuration

Runs after all `Configure<T>` calls. Useful for overrides and computed defaults.

```csharp
// Post-configure default instance
builder.Services.PostConfigure<DatabaseOptions>(options => {
    if (string.IsNullOrEmpty(options.ConnectionString)) {
        options.ConnectionString = "Data Source=:memory:";
    }
});

// Post-configure named instance
builder.Services.PostConfigure<DatabaseOptions>("readonly", options => {
    options.MaxRetryCount = 1;
});

// Post-configure ALL instances
builder.Services.PostConfigureAll<DatabaseOptions>(options => {
    options.CommandTimeout = TimeSpan.FromSeconds(60);
});
```
