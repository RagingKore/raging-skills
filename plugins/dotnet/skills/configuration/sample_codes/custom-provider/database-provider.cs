// Custom Configuration Provider: Reads settings from a database table
// Demonstrates IConfigurationSource, ConfigurationProvider, and extension method

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// --- ConfigurationSource ---

public sealed class DatabaseConfigurationSource(
    string connectionString,
    string tableName = "AppSettings",
    TimeSpan? refreshInterval = null) : IConfigurationSource {

    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new DatabaseConfigurationProvider(connectionString, tableName, refreshInterval);
}

// --- ConfigurationProvider ---

public sealed class DatabaseConfigurationProvider : ConfigurationProvider, IDisposable {
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly Timer? _refreshTimer;

    public DatabaseConfigurationProvider(
        string connectionString,
        string tableName,
        TimeSpan? refreshInterval) {
        _connectionString = connectionString;
        _tableName = tableName;

        if (refreshInterval is { } interval) {
            _refreshTimer = new Timer(
                _ => {
                    Load();
                    OnReload();  // Notifies IOptionsMonitor of changes
                },
                state: null,
                dueTime: interval,
                period: interval);
        }
    }

    public override void Load() {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = new SqlCommand(
            $"SELECT [Key], [Value] FROM [{_tableName}] WHERE [IsActive] = 1",
            connection);

        using var reader = command.ExecuteReader();

        var data = new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase);

        while (reader.Read()) {
            string key = reader.GetString(0);
            string? value = reader.IsDBNull(1) ? null : reader.GetString(1);
            data[key] = value;
        }

        Data = data;
    }

    public void Dispose() => _refreshTimer?.Dispose();
}

// --- Extension Method ---

public static class DatabaseConfigurationExtensions {
    public static IConfigurationBuilder AddDatabase(
        this IConfigurationBuilder builder,
        string connectionString,
        string tableName = "AppSettings",
        TimeSpan? refreshInterval = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.Add(new DatabaseConfigurationSource(
            connectionString, tableName, refreshInterval));
    }
}

// --- Usage in Program.cs ---
//
// HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
//
// // Add database config after JSON (so DB values override file values)
// builder.Configuration.AddDatabase(
//     connectionString: builder.Configuration.GetConnectionString("ConfigDb")!,
//     tableName: "AppSettings",
//     refreshInterval: TimeSpan.FromMinutes(5));
//
// using IHost host = builder.Build();
// await host.RunAsync();

// --- SQL Table Schema ---
//
// CREATE TABLE [AppSettings] (
//     [Key]      NVARCHAR(256) NOT NULL PRIMARY KEY,
//     [Value]    NVARCHAR(MAX) NULL,
//     [IsActive] BIT           NOT NULL DEFAULT 1
// );
//
// INSERT INTO [AppSettings] ([Key], [Value])
// VALUES
//     ('Feature:Flags:NewUI', 'true'),
//     ('Smtp:Host', 'smtp.example.com'),
//     ('Smtp:Port', '587');
