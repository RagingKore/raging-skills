// Console app: Configuration with generic host and options pattern
// Package: Microsoft.Extensions.Hosting

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
// Automatically loads: appsettings.json, appsettings.{env}.json,
// user secrets (dev), env vars, command-line args

builder.Services
    .AddOptionsWithValidateOnStart<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddHostedService<Worker>();

using IHost host = builder.Build();
await host.RunAsync();

// --- Types ---

public sealed class AppOptions {
    public const string SectionName = "App";

    [System.ComponentModel.DataAnnotations.Required]
    public required string Name { get; set; }

    [System.ComponentModel.DataAnnotations.Range(1, 100)]
    public int MaxConcurrency { get; set; } = 4;

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed class Worker(
    IOptions<AppOptions> options,
    IHostApplicationLifetime lifetime) : BackgroundService {

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        AppOptions config = options.Value;
        Console.WriteLine($"Starting {config.Name} with concurrency {config.MaxConcurrency}");
        Console.WriteLine($"Polling every {config.PollingInterval}");

        using var timer = new PeriodicTimer(config.PollingInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Tick");
        }
    }
}
