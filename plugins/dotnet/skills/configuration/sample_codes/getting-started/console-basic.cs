// Console app: Basic configuration without generic host
// Packages: Microsoft.Extensions.Configuration.Json,
//           Microsoft.Extensions.Configuration.EnvironmentVariables,
//           Microsoft.Extensions.Configuration.Binder

using Microsoft.Extensions.Configuration;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
        optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Read individual values
string? connectionString = config.GetConnectionString("Default");
int maxRetries = config.GetValue("App:MaxRetries", defaultValue: 3);
bool featureEnabled = config.GetValue<bool>("Features:NewDashboard");

Console.WriteLine($"Connection: {connectionString}");
Console.WriteLine($"Max retries: {maxRetries}");
Console.WriteLine($"Feature enabled: {featureEnabled}");

// Bind to a strongly-typed object
AppSettings settings = config.GetSection("App").Get<AppSettings>()
    ?? throw new InvalidOperationException("App section not found");

Console.WriteLine($"App name: {settings.Name}, Version: {settings.Version}");

public sealed class AppSettings {
    public required string Name { get; set; }
    public required string Version { get; set; }
    public int MaxRetries { get; set; } = 3;
}
