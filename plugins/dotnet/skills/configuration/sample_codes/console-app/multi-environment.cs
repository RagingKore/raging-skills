// Console app: Multi-environment configuration with all providers
// Demonstrates full provider chain: JSON → env-JSON → user secrets → env vars → command-line

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Clear defaults and rebuild explicitly for full control
builder.Configuration.Sources.Clear();

IHostEnvironment env = builder.Environment;

builder.Configuration
    // Base settings (lowest priority)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    // Environment-specific overrides
    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
    // User secrets (development only)
    .AddUserSecrets<Program>(optional: true)
    // Environment variables with app-specific prefix
    .AddEnvironmentVariables(prefix: "MYAPP_")
    // Also load unprefixed env vars
    .AddEnvironmentVariables()
    // Command-line has highest priority
    .AddCommandLine(args);

// Bind and validate options
builder.Services
    .AddOptionsWithValidateOnStart<AppOptions>()
    .Bind(builder.Configuration.GetSection("App"))
    .ValidateDataAnnotations();

builder.Services.AddTransient<ConfigDumper>();

using IHost host = builder.Build();

// Dump all configuration for debugging
var dumper = host.Services.GetRequiredService<ConfigDumper>();
dumper.DumpProviders();
dumper.DumpValues();

await host.RunAsync();

// --- Types ---

public sealed class AppOptions {
    [System.ComponentModel.DataAnnotations.Required]
    public required string Name { get; set; }

    public required string Environment { get; set; }

    [System.ComponentModel.DataAnnotations.Range(1, 100)]
    public int WorkerCount { get; set; } = 4;
}

public sealed class ConfigDumper(IConfiguration config) {

    public void DumpProviders() {
        if (config is IConfigurationRoot root) {
            Console.WriteLine("=== Configuration Providers (execution order) ===");
            int i = 0;
            foreach (IConfigurationProvider provider in root.Providers) {
                Console.WriteLine($"  [{i++}] {provider}");
            }
            Console.WriteLine();
        }
    }

    public void DumpValues() {
        Console.WriteLine("=== Configuration Values ===");
        foreach ((string key, string? value) in config.AsEnumerable()
            .Where(kvp => kvp.Value is not null)
            .OrderBy(kvp => kvp.Key)) {
            // Mask potential secrets
            string display = key.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || key.Contains("password", StringComparison.OrdinalIgnoreCase)
                || key.Contains("key", StringComparison.OrdinalIgnoreCase)
                    ? "****"
                    : value ?? "(null)";
            Console.WriteLine($"  {key} = {display}");
        }
    }
}

// Run examples:
//   DOTNET_ENVIRONMENT=Development dotnet run
//   DOTNET_ENVIRONMENT=Production dotnet run -- --App:WorkerCount=8
//   MYAPP_App__Name="Override" dotnet run
