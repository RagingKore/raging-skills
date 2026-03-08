// Decorator and Composite patterns with dependency injection
// Demonstrates wrapping services with cross-cutting concerns

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// --- Decorator: Logging → Caching → Retry → SQL ---

// Register the concrete inner implementation
builder.Services.AddScoped<SqlOrderRepository>();

// Build the decoration chain via factory
builder.Services.AddScoped<IOrderRepository>(sp => {
    var sql = sp.GetRequiredService<SqlOrderRepository>();

    var retry = new RetryOrderRepository(sql, maxRetries: 3,
        sp.GetRequiredService<ILogger<RetryOrderRepository>>());

    var caching = new CachingOrderRepository(retry,
        sp.GetRequiredService<IDistributedCache>(),
        sp.GetRequiredService<ILogger<CachingOrderRepository>>());

    return new LoggingOrderRepository(caching,
        sp.GetRequiredService<ILogger<LoggingOrderRepository>>());
});

// --- Composite: notify via all channels ---

// Register concrete notifiers (not via interface)
builder.Services.AddTransient<EmailNotifier>();
builder.Services.AddTransient<SmsNotifier>();
builder.Services.AddTransient<SlackNotifier>();

// Register composite as the interface
builder.Services.AddTransient<INotifier>(sp => new CompositeNotifier([
    sp.GetRequiredService<EmailNotifier>(),
    sp.GetRequiredService<SmsNotifier>(),
    sp.GetRequiredService<SlackNotifier>(),
]));

var app = builder.Build();
app.Run();

// --- Decorator implementations ---

public sealed class LoggingOrderRepository(
    IOrderRepository inner,
    ILogger<LoggingOrderRepository> logger) : IOrderRepository {

    public async Task<Order?> GetByIdAsync(int id, CancellationToken ct) {
        logger.LogDebug("Getting order {OrderId}", id);
        var order = await inner.GetByIdAsync(id, ct);
        logger.LogDebug("Order {OrderId} {Result}",
            id, order is not null ? "found" : "not found");
        return order;
    }

    public Task SaveAsync(Order order, CancellationToken ct) {
        logger.LogInformation("Saving order {OrderId}", order.Id);
        return inner.SaveAsync(order, ct);
    }
}

public sealed class CachingOrderRepository(
    IOrderRepository inner,
    IDistributedCache cache,
    ILogger<CachingOrderRepository> logger) : IOrderRepository {

    public async Task<Order?> GetByIdAsync(int id, CancellationToken ct) {
        var key = $"order:{id}";
        var cached = await cache.GetStringAsync(key, ct);

        if (cached is not null) {
            logger.LogDebug("Cache hit: {Key}", key);
            return JsonSerializer.Deserialize<Order>(cached);
        }

        var order = await inner.GetByIdAsync(id, ct);
        if (order is not null) {
            await cache.SetStringAsync(key,
                JsonSerializer.Serialize(order),
                new DistributedCacheEntryOptions {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                }, ct);
        }
        return order;
    }

    public async Task SaveAsync(Order order, CancellationToken ct) {
        await inner.SaveAsync(order, ct);
        await cache.RemoveAsync($"order:{order.Id}", ct);
    }
}

public sealed class RetryOrderRepository(
    IOrderRepository inner,
    int maxRetries,
    ILogger<RetryOrderRepository> logger) : IOrderRepository {

    public async Task<Order?> GetByIdAsync(int id, CancellationToken ct) {
        for (int attempt = 1; ; attempt++) {
            try {
                return await inner.GetByIdAsync(id, ct);
            }
            catch (Exception ex) when (attempt < maxRetries) {
                logger.LogWarning(ex,
                    "Retry {Attempt}/{Max} for GetByIdAsync({OrderId})",
                    attempt, maxRetries, id);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
            }
        }
    }

    public Task SaveAsync(Order order, CancellationToken ct)
        => inner.SaveAsync(order, ct);
}

// --- Composite implementation ---

public sealed class CompositeNotifier(IReadOnlyList<INotifier> notifiers) : INotifier {
    public async Task NotifyAsync(Notification notification, CancellationToken ct) {
        var tasks = notifiers.Select(n => n.NotifyAsync(notification, ct));
        var results = await Task.WhenAll(tasks);

        var failures = results.Where(r => !r.Success).ToList();
        if (failures.Count > 0) {
            // Log failures but don't throw — partial success is acceptable
        }
    }
}
