// ASP.NET Core Minimal API with Options Pattern
// All configuration providers are pre-registered by WebApplication.CreateBuilder

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Register options with validation
builder.Services
    .AddOptionsWithValidateOnStart<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services
    .AddOptionsWithValidateOnStart<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .ValidateDataAnnotations();

var app = builder.Build();

// Access options in endpoints
app.MapGet("/config/jwt", (IOptions<JwtOptions> opts) => new {
    opts.Value.Issuer,
    opts.Value.Audience,
    ExpiryMinutes = opts.Value.ExpiryMinutes
});

// Use IOptionsMonitor for live-reloading config in singletons
app.MapGet("/config/db", (IOptionsMonitor<DatabaseOptions> monitor) => new {
    monitor.CurrentValue.MaxRetryCount,
    monitor.CurrentValue.CommandTimeout
});

app.Run();

// --- Options classes ---

public sealed class JwtOptions {
    public const string SectionName = "Jwt";

    [Required]
    public required string SecretKey { get; set; }

    [Required, Url]
    public required string Issuer { get; set; }

    [Required]
    public required string Audience { get; set; }

    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; } = 60;
}

public sealed class DatabaseOptions {
    public const string SectionName = "Database";

    [Required, MinLength(1)]
    public required string ConnectionString { get; set; }

    [Range(0, 10)]
    public int MaxRetryCount { get; set; } = 3;

    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public bool EnableSensitiveDataLogging { get; set; }
}
