---
name: configuration
description: |
  .NET configuration expert for console apps and ASP.NET Core. Covers all configuration sources
  (JSON, XML, INI, environment variables, command-line, user secrets, Azure Key Vault, Azure App
  Configuration, in-memory, key-per-file, custom providers), the options pattern (IOptions,
  IOptionsSnapshot, IOptionsMonitor, named options), validation (DataAnnotations, IValidateOptions,
  ValidateOnStart, recursive validation), binding (hierarchical, arrays, source generator for AOT),
  and custom configuration providers. Use when adding configuration, setting up appsettings.json,
  binding options classes, validating configuration, implementing IOptions pattern, creating custom
  configuration providers, configuring environment-specific settings, working with user secrets,
  or debugging configuration issues in .NET apps.
---

# .NET Configuration Expert

Comprehensive guidance for `Microsoft.Extensions.Configuration` and the Options pattern in .NET 9+ console apps and ASP.NET Core.

## Quick Decision Matrix

| Need                                  | Solution              | Interface                             |
|---------------------------------------|-----------------------|---------------------------------------|
| Read-once settings at startup         | `IOptions<T>`         | Singleton, no reload                  |
| Settings that reload per-request      | `IOptionsSnapshot<T>` | Scoped, recomputed per request        |
| React to config changes in singletons | `IOptionsMonitor<T>`  | Singleton, change notifications       |
| Multiple configs of same type         | Named options         | `IOptionsSnapshot<T>.Get(name)`       |
| Fail-fast on bad config               | `ValidateOnStart()`   | Throws at startup                     |
| AOT/trim-safe binding                 | Source generator      | `EnableConfigurationBindingGenerator` |

## Configuration Sources (Priority Order)

Default priority in `Host.CreateApplicationBuilder` / `WebApplication.CreateBuilder` (highest to lowest):

1. **Command-line arguments** - `--Key=Value`
2. **Environment variables** (non-prefixed) - `Key=Value`
3. **User secrets** (Development only) - `dotnet user-secrets set Key Value`
4. **appsettings.{Environment}.json** - `appsettings.Development.json`
5. **appsettings.json** – Base settings
6. **ChainedConfigurationProvider** – Host config fallback

> Last provider added wins for duplicate keys.

## Console App Setup

### Minimal (no host)

```csharp
using Microsoft.Extensions.Configuration;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

string? value = config["Section:Key"];
```

**Required packages:** `Microsoft.Extensions.Configuration.Json`, `Microsoft.Extensions.Configuration.EnvironmentVariables`, `Microsoft.Extensions.Configuration.CommandLine`

### With Generic Host (recommended)

```csharp
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
// Configuration already loaded: appsettings.json, env vars, command-line, user secrets

builder.Services.Configure<MyOptions>(
    builder.Configuration.GetSection(nameof(MyOptions)));

using IHost host = builder.Build();
await host.RunAsync();
```

**Required package:** `Microsoft.Extensions.Hosting`

## ASP.NET Core Setup

```csharp
var builder = WebApplication.CreateBuilder(args);
// All default providers pre-configured

builder.Services
    .AddOptionsWithValidateOnStart<MyOptions>()
    .Bind(builder.Configuration.GetSection("MyOptions"))
    .ValidateDataAnnotations();

var app = builder.Build();
```

## Options Pattern

### Options Class Rules

- Must be non-abstract with public parameterless constructor
- Public read-write properties (fields are NOT bound)
- Use `required` modifier for mandatory properties

```csharp
public sealed class DatabaseOptions {
    public const string SectionName = "Database";

    public required string ConnectionString { get; set; }
    public int MaxRetryCount { get; set; } = 3;
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

### Registration Patterns

```csharp
// Pattern 1: Bind to section
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));

// Pattern 2: OptionsBuilder fluent API (supports validation)
builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Pattern 3: Configure with DI services
builder.Services
    .AddOptions<DatabaseOptions>()
    .Configure<IHostEnvironment>((options, env) => {
        if (env.IsDevelopment()) {
            options.MaxRetryCount = 1;
        }
    });
```

### Consuming Options

```csharp
// IOptions<T> - Singleton, never reloads
public sealed class MyService(IOptions<DatabaseOptions> options) {
    private readonly DatabaseOptions _db = options.Value;
}

// IOptionsSnapshot<T> - Scoped, recomputed per request
public sealed class ScopedService(IOptionsSnapshot<DatabaseOptions> options) {
    private readonly DatabaseOptions _db = options.Value;
}

// IOptionsMonitor<T> - Singleton, supports change notifications
public sealed class MonitorService(IOptionsMonitor<DatabaseOptions> monitor) {
    public void DoWork() {
        DatabaseOptions current = monitor.CurrentValue;  // Always latest
    }
}
```

### Interface Comparison

| Feature               | `IOptions<T>` | `IOptionsSnapshot<T>` | `IOptionsMonitor<T>` |
|-----------------------|---------------|-----------------------|----------------------|
| Lifetime              | Singleton     | Scoped                | Singleton            |
| Reads updates         | No            | Yes (per request)     | Yes (real-time)      |
| Named options         | No            | Yes                   | Yes                  |
| Change notifications  | No            | No                    | Yes (`OnChange`)     |
| Inject into Singleton | Yes           | **No**                | Yes                  |

## Validation

See [references/validation.md](references/validation.md) for complete validation patterns including DataAnnotations, IValidateOptions, recursive validation, and compile-time source generation.

## Configuration Providers

See [references/providers.md](references/providers.md) for detailed coverage of all built-in and custom configuration providers.

## Hierarchical Keys

- Use `:` as delimiter: `"Section:SubSection:Key"`
- Environment variables: use `__` (double underscore) instead of `:`
- Azure Key Vault: use `--` (double dash)
- Arrays: `"Array:0"`, `"Array:1"`, etc.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

Access: `config["Logging:LogLevel:Default"]` or env var `Logging__LogLevel__Default`

## AOT / Trim-Safe Configuration

Enable the configuration binding source generator for Native AOT and trimming:

```xml
<PropertyGroup>
    <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
</PropertyGroup>
```

This intercepts `Bind()`, `Get<T>()`, and `Configure<T>()` calls and replaces reflection with generated code. Enabled by default when `PublishAot=true`.

## Learn More

| Topic                           | How to Find                                                                                                    |
|---------------------------------|----------------------------------------------------------------------------------------------------------------|
| All configuration providers     | `microsoft_docs_search(query=".NET configuration providers JSON XML INI environment")`                         |
| Custom configuration provider   | `microsoft_docs_search(query=".NET implement custom configuration provider")`                                  |
| Azure App Configuration         | `microsoft_docs_search(query="Azure App Configuration .NET provider")`                                         |
| Azure Key Vault provider        | `microsoft_docs_search(query="Azure Key Vault configuration provider ASP.NET Core")`                           |
| User secrets                    | `microsoft_docs_search(query="ASP.NET Core user secrets Secret Manager")`                                      |
| Options pattern library authors | `microsoft_docs_search(query=".NET options pattern library authors guidance")`                                 |
| Configuration source generator  | `microsoft_docs_fetch(url="https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-generator")` |
