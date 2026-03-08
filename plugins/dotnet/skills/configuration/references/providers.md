# Configuration Providers

Complete reference for all built-in and custom configuration providers in .NET.

## Table of Contents

- [Provider Priority](#provider-priority)
- [JSON Configuration Provider](#json-configuration-provider)
- [Environment Variables Provider](#environment-variables-provider)
- [Command-Line Provider](#command-line-provider)
- [XML Configuration Provider](#xml-configuration-provider)
- [INI Configuration Provider](#ini-configuration-provider)
- [User Secrets](#user-secrets)
- [Key-Per-File Provider](#key-per-file-provider)
- [In-Memory Provider](#in-memory-provider)
- [Custom Configuration Provider](#custom-configuration-provider)
- [Azure Providers](#azure-providers)

---

## Provider Priority

### Host.CreateApplicationBuilder / WebApplication.CreateBuilder defaults

From **highest** to **lowest** priority:

| Priority    | Source                               | Provider                                    |
|-------------|--------------------------------------|---------------------------------------------|
| 1 (highest) | Command-line args                    | `CommandLineConfigurationProvider`          |
| 2           | Environment variables (non-prefixed) | `EnvironmentVariablesConfigurationProvider` |
| 3           | User secrets (Development only)      | `JsonConfigurationProvider` (secrets.json)  |
| 4           | appsettings.{Environment}.json       | `JsonConfigurationProvider`                 |
| 5 (lowest)  | appsettings.json                     | `JsonConfigurationProvider`                 |

**Rule:** Last provider added wins for duplicate keys. Providers added *after* defaults override them.

### Host Configuration (separate from app config)

| Priority | Source                          |
|----------|---------------------------------|
| 1        | Command-line args               |
| 2        | `DOTNET_` prefixed env vars     |
| 3        | `ASPNETCORE_` prefixed env vars |

---

## JSON Configuration Provider

**Package:** `Microsoft.Extensions.Configuration.Json`

```csharp
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json",
        optional: true, reloadOnChange: true);
```

### Parameters

| Parameter        | Default    | Purpose                         |
|------------------|------------|---------------------------------|
| `path`           | (required) | JSON file path                  |
| `optional`       | `false`    | Don't throw if file missing     |
| `reloadOnChange` | `false`    | Reload config when file changes |

### Key Rules

- Hierarchical keys use `:` delimiter: `"Logging:LogLevel:Default"`
- Arrays use numeric indices: `"Servers:0:Host"`, `"Servers:1:Host"`
- Case-insensitive keys
- Supports comments (JavaScript/C# style) despite RFC 7159

### Environment-Specific Files

`appsettings.{Environment}.json` overrides `appsettings.json`:

```
appsettings.json                  ← Base settings
appsettings.Development.json      ← Development overrides
appsettings.Staging.json          ← Staging overrides
appsettings.Production.json       ← Production overrides
```

Set environment via `ASPNETCORE_ENVIRONMENT` or `DOTNET_ENVIRONMENT`.

---

## Environment Variables Provider

**Package:** `Microsoft.Extensions.Configuration.EnvironmentVariables`

```csharp
// All environment variables (default)
builder.Configuration.AddEnvironmentVariables();

// Only variables with prefix (prefix is stripped)
builder.Configuration.AddEnvironmentVariables(prefix: "MYAPP_");
```

### Hierarchical Key Mapping

| JSON key                    | Environment variable         |
|-----------------------------|------------------------------|
| `Logging:LogLevel:Default`  | `Logging__LogLevel__Default` |
| `ConnectionStrings:Default` | `ConnectionStrings__Default` |

Use `__` (double underscore) as delimiter — works on all platforms. `:` does not work on Linux/Bash.

### Connection String Prefixes

| Prefix                 | Mapped to                | Provider value           |
|------------------------|--------------------------|--------------------------|
| `CUSTOMCONNSTR_MyDb`   | `ConnectionStrings:MyDb` | (none)                   |
| `SQLCONNSTR_MyDb`      | `ConnectionStrings:MyDb` | `System.Data.SqlClient`  |
| `SQLAZURECONNSTR_MyDb` | `ConnectionStrings:MyDb` | `System.Data.SqlClient`  |
| `MYSQLCONNSTR_MyDb`    | `ConnectionStrings:MyDb` | `MySql.Data.MySqlClient` |

### launchSettings.json

Environment variables in `launchSettings.json` override system environment variables during `dotnet run` / Visual Studio debugging.

---

## Command-Line Provider

**Package:** `Microsoft.Extensions.Configuration.CommandLine`

```csharp
builder.Configuration.AddCommandLine(args);
```

### Argument Formats

```bash
# Equals sign
dotnet run --ConnectionString="Server=localhost"

# Space-separated (requires -- or / prefix)
dotnet run --ConnectionString "Server=localhost"
dotnet run /ConnectionString "Server=localhost"

# Equals sign (no prefix)
dotnet run ConnectionString="Server=localhost"
```

### Switch Mappings

Map short flags to configuration keys:

```csharp
var switchMappings = new Dictionary<string, string> {
    ["-c"] = "ConnectionString",
    ["-e"] = "Environment",
    ["--verbose"] = "Logging:LogLevel:Default"
};
builder.Configuration.AddCommandLine(args, switchMappings);
```

Usage: `dotnet run -c "Server=localhost" -e Development --verbose Debug`

---

## XML Configuration Provider

**Package:** `Microsoft.Extensions.Configuration.Xml`

```csharp
builder.Configuration.AddXmlFile("config.xml", optional: true, reloadOnChange: true);
```

### Example XML

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <ConnectionStrings>
    <DefaultConnection>Server=localhost</DefaultConnection>
  </ConnectionStrings>
  <Logging>
    <LogLevel>
      <Default>Information</Default>
    </LogLevel>
  </Logging>
</configuration>
```

Repeating elements: use `name` attribute in .NET 5 and earlier. In .NET 6+, repeated elements are auto-indexed.

---

## INI Configuration Provider

**Package:** `Microsoft.Extensions.Configuration.Ini`

```csharp
builder.Configuration.AddIniFile("config.ini", optional: true, reloadOnChange: true);
```

### Example INI

```ini
SecretKey="my-secret"

[ConnectionStrings]
DefaultConnection="Server=localhost;Database=mydb"

[Logging:LogLevel]
Default=Information
Microsoft=Warning
```

Section headers map to hierarchical keys: `[Logging:LogLevel]` + `Default` = `Logging:LogLevel:Default`.

---

## User Secrets

**Package:** `Microsoft.Extensions.Configuration.UserSecrets`

For storing sensitive data during development. NOT for production.

### Setup

```bash
# Initialize (adds UserSecretsId to .csproj)
dotnet user-secrets init

# Set a secret
dotnet user-secrets set "Database:Password" "my-secret-password"

# List secrets
dotnet user-secrets list

# Remove
dotnet user-secrets remove "Database:Password"

# Clear all
dotnet user-secrets clear
```

### How It Works

- Stored in `%APPDATA%\Microsoft\UserSecrets\{ID}\secrets.json` (Windows)
- Or `~/.microsoft/usersecrets/{ID}/secrets.json` (Linux/macOS)
- Loaded automatically in Development environment by `CreateApplicationBuilder`
- **Never committed to source control**

### Manual Registration

```csharp
if (builder.Environment.IsDevelopment()) {
    builder.Configuration.AddUserSecrets<Program>();
}
```

---

## Key-Per-File Provider

**Package:** `Microsoft.Extensions.Configuration.KeyPerFile`

Each file in a directory becomes a key-value pair (filename = key, content = value). Common in Docker/Kubernetes for mounted secrets.

```csharp
builder.Configuration.AddKeyPerFile(
    directoryPath: "/run/secrets", optional: true);
```

File `Logging__LogLevel__Default` with content `Warning` becomes key `Logging:LogLevel:Default` = `Warning`.

---

## In-Memory Provider

**Package:** `Microsoft.Extensions.Configuration` (base package)

```csharp
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
    ["Feature:Enabled"] = "true",
    ["Feature:MaxItems"] = "100",
    ["ConnectionStrings:Default"] = "Server=localhost"
});
```

Useful for testing and default values.

---

## Custom Configuration Provider

Implement `IConfigurationSource` + `ConfigurationProvider` for custom data sources (database, remote API, etc.).

### IConfigurationSource

```csharp
public sealed class DatabaseConfigurationSource(
    string connectionString) : IConfigurationSource {

    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new DatabaseConfigurationProvider(connectionString);
}
```

### ConfigurationProvider

```csharp
public sealed class DatabaseConfigurationProvider(
    string connectionString) : ConfigurationProvider {

    public override void Load() {
        // Data is a protected Dictionary<string, string?> from base class
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = new SqlCommand(
            "SELECT [Key], [Value] FROM AppSettings", connection);
        using var reader = command.ExecuteReader();

        var data = new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase);

        while (reader.Read()) {
            data[reader.GetString(0)] = reader.GetString(1);
        }

        Data = data;
    }
}
```

### Extension Method

```csharp
public static class DatabaseConfigurationExtensions {
    public static IConfigurationBuilder AddDatabase(
        this IConfigurationBuilder builder, string connectionString) =>
        builder.Add(new DatabaseConfigurationSource(connectionString));
}
```

### Usage

```csharp
builder.Configuration.AddDatabase(
    builder.Configuration.GetConnectionString("ConfigDb")!);
```

For full example with Entity Framework: `microsoft_docs_fetch(url="https://learn.microsoft.com/en-us/dotnet/core/extensions/custom-configuration-provider")`

---

## Azure Providers

### Azure App Configuration

**Package:** `Microsoft.Azure.AppConfiguration.AspNetCore`

```csharp
builder.Configuration.AddAzureAppConfiguration(options => {
    options.Connect(new Uri(endpoint), new DefaultAzureCredential())
           .Select(KeyFilter.Any)
           .Select(KeyFilter.Any, builder.Environment.EnvironmentName);
});
```

### Azure Key Vault

**Package:** `Azure.Extensions.AspNetCore.Configuration.Secrets`

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://my-vault.vault.azure.net/"),
    new DefaultAzureCredential());
```

Key Vault uses `--` as hierarchy separator (auto-converted to `:`).

For details: `microsoft_docs_search(query="Azure Key Vault configuration provider ASP.NET Core")`
