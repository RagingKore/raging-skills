// Captive dependency: problem and three different fixes
// A scoped DbContext captured by a singleton service

using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// === THE PROBLEM ===
// Uncommenting this causes a captive dependency:
// builder.Services.AddDbContext<AppDbContext>();          // Scoped
// builder.Services.AddSingleton<IReportService, ReportService>();  // Singleton!
// ReportService constructor takes AppDbContext → CAPTURED as singleton
// Result: same DbContext for ALL requests → stale data, thread-safety bugs

// === FIX 1: IServiceScopeFactory (recommended for background/singleton services) ===

builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddSingleton<IReportService, ReportServiceWithScopeFactory>();

// === FIX 2: Match lifetimes ===

// builder.Services.AddDbContext<AppDbContext>();
// builder.Services.AddScoped<IReportService, ReportService>();

// === FIX 3: Factory delegate ===

// builder.Services.AddDbContext<AppDbContext>();
// builder.Services.AddSingleton<IReportService>(sp => {
//     return new ReportServiceWithFactory(
//         () => sp.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>());
// });

var app = builder.Build();
app.Run();

// --- Fix 1: IServiceScopeFactory ---

public sealed class ReportServiceWithScopeFactory(
    IServiceScopeFactory scopeFactory,
    ILogger<ReportServiceWithScopeFactory> logger) : IReportService {

    public async Task<Report> GenerateAsync(ReportRequest request, CancellationToken ct) {
        // Each call gets its own scope → fresh DbContext
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var data = await db.Orders
            .Where(o => o.CreatedAt >= request.From && o.CreatedAt <= request.To)
            .ToListAsync(ct);

        logger.LogInformation("Generated report with {Count} orders", data.Count);

        return new Report {
            GeneratedAt = DateTimeOffset.UtcNow,
            OrderCount = data.Count,
            TotalRevenue = data.Sum(o => o.Total)
        };
    }
}

// --- Fix 3: Factory delegate ---

public sealed class ReportServiceWithFactory(
    Func<AppDbContext> dbFactory) : IReportService {

    public async Task<Report> GenerateAsync(ReportRequest request, CancellationToken ct) {
        // Caller disposes the context
        using var db = dbFactory();

        var data = await db.Orders
            .Where(o => o.CreatedAt >= request.From && o.CreatedAt <= request.To)
            .ToListAsync(ct);

        return new Report {
            GeneratedAt = DateTimeOffset.UtcNow,
            OrderCount = data.Count,
            TotalRevenue = data.Sum(o => o.Total)
        };
    }
}

// --- Scope validation catches this at runtime in Development ---
// InvalidOperationException:
// "Cannot consume scoped service 'AppDbContext' from singleton 'ReportService'"
//
// Enable ValidateOnBuild to catch at startup:
// builder.Host.UseDefaultServiceProvider(options => {
//     options.ValidateScopes = true;
//     options.ValidateOnBuild = true;
// });
