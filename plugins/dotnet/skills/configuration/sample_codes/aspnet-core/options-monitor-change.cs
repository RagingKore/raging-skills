// IOptionsMonitor: React to configuration changes at runtime
// Useful for feature flags, rate limits, and dynamic settings

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptionsWithValidateOnStart<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection("RateLimit"))
    .ValidateDataAnnotations();

builder.Services.AddSingleton<RateLimitService>();

var app = builder.Build();

app.MapGet("/rate-limit", (RateLimitService svc) => new {
    svc.CurrentLimit,
    svc.WindowSeconds
});

app.Run();

// --- Options ---

public sealed class RateLimitOptions {
    [Range(1, 10000)]
    public int RequestsPerWindow { get; set; } = 100;

    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;

    public bool Enabled { get; set; } = true;
}

// --- Service using IOptionsMonitor ---

public sealed class RateLimitService : IDisposable {
    private readonly IOptionsMonitor<RateLimitOptions> _monitor;
    private readonly IDisposable? _changeListener;

    public int CurrentLimit { get; private set; }
    public int WindowSeconds { get; private set; }

    public RateLimitService(IOptionsMonitor<RateLimitOptions> monitor) {
        _monitor = monitor;
        ApplyOptions(monitor.CurrentValue);

        // Subscribe to changes - fires when appsettings.json is modified
        _changeListener = monitor.OnChange((options, name) => {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Rate limit config changed" +
                $" (name: {name ?? "default"})");
            ApplyOptions(options);
        });
    }

    private void ApplyOptions(RateLimitOptions options) {
        CurrentLimit = options.Enabled ? options.RequestsPerWindow : int.MaxValue;
        WindowSeconds = options.WindowSeconds;
        Console.WriteLine($"Rate limit: {CurrentLimit} requests / {WindowSeconds}s");
    }

    public void Dispose() => _changeListener?.Dispose();
}

// appsettings.json:
// {
//   "RateLimit": {
//     "RequestsPerWindow": 200,
//     "WindowSeconds": 60,
//     "Enabled": true
//   }
// }
//
// Change appsettings.json while app is running to see OnChange fire.
