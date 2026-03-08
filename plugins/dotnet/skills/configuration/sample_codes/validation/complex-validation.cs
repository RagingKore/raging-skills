// Complex validation with IValidateOptions, recursive validation,
// and ValidateOnStart for fail-fast behavior

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Register with ValidateOnStart - app won't start with bad config
builder.Services
    .AddOptionsWithValidateOnStart<AppConfig>()
    .Bind(builder.Configuration.GetSection("App"))
    .ValidateDataAnnotations();

// Register custom validator that uses DI
builder.Services.AddSingleton<
    IValidateOptions<AppConfig>, AppConfigValidator>();

var app = builder.Build();
app.MapGet("/", (IOptions<AppConfig> opts) => opts.Value);
app.Run();

// --- Options with recursive validation ---

public sealed class AppConfig {
    [Required]
    public required string ApplicationName { get; set; }

    [Required, Url]
    public required string BaseUrl { get; set; }

    [ValidateObjectMembers]  // Recurse into nested object
    public required StorageOptions Storage { get; set; }

    [ValidateEnumeratedItems]  // Validate each item in the list
    public List<EndpointOptions> Endpoints { get; set; } = [];

    [Range(1, 3600)]
    public int SessionTimeoutSeconds { get; set; } = 900;
}

public sealed class StorageOptions {
    [Required, MinLength(1)]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string ContainerName { get; set; } = "default";

    [Range(1, 1024)]
    public int MaxFileSizeMb { get; set; } = 50;
}

public sealed class EndpointOptions {
    [Required, RegularExpression(@"^[a-zA-Z0-9\-\.]+$")]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; }

    public bool UseTls { get; set; } = true;
}

// --- Custom validator with DI ---

public sealed class AppConfigValidator(
    IWebHostEnvironment env) : IValidateOptions<AppConfig> {

    public ValidateOptionsResult Validate(string? name, AppConfig options) {
        List<string>? errors = null;

        if (env.IsProduction()) {
            if (!options.BaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                (errors ??= []).Add("BaseUrl must use HTTPS in production.");
            }

            if (options.Storage.ConnectionString.Contains("localhost",
                    StringComparison.OrdinalIgnoreCase)) {
                (errors ??= []).Add("Storage cannot use localhost in production.");
            }

            if (options.Endpoints.Any(e => !e.UseTls)) {
                (errors ??= []).Add("All endpoints must use TLS in production.");
            }
        }

        if (options.Endpoints is { Count: > 0 }) {
            var duplicateHosts = options.Endpoints
                .GroupBy(e => $"{e.Host}:{e.Port}")
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateHosts.Count > 0) {
                (errors ??= []).Add(
                    $"Duplicate endpoints: {string.Join(", ", duplicateHosts)}");
            }
        }

        return errors is { Count: > 0 }
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

// appsettings.json:
// {
//   "App": {
//     "ApplicationName": "MyWebApp",
//     "BaseUrl": "https://myapp.example.com",
//     "SessionTimeoutSeconds": 1800,
//     "Storage": {
//       "ConnectionString": "DefaultEndpointsProtocol=https;...",
//       "ContainerName": "uploads",
//       "MaxFileSizeMb": 100
//     },
//     "Endpoints": [
//       { "Host": "api.example.com", "Port": 443, "UseTls": true },
//       { "Host": "worker.example.com", "Port": 443, "UseTls": true }
//     ]
//   }
// }
