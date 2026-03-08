// Keyed services (.NET 8+): register and resolve by key
// Useful for strategy pattern, multi-implementation dispatch

using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// --- String-keyed registrations ---

builder.Services.AddKeyedSingleton<ICache, RedisCache>("distributed");
builder.Services.AddKeyedSingleton<ICache, MemoryCache>("local");

// --- Enum-keyed registrations ---

builder.Services.AddKeyedScoped<INotifier, EmailNotifier>(NotificationChannel.Email);
builder.Services.AddKeyedScoped<INotifier, SmsNotifier>(NotificationChannel.Sms);
builder.Services.AddKeyedScoped<INotifier, PushNotifier>(NotificationChannel.Push);

// --- AnyKey wildcard: factory that receives the key ---

builder.Services.AddKeyedTransient<IFeatureFlag>(
    KeyedService.AnyKey,
    (sp, key) => new LaunchDarklyFeatureFlag(key?.ToString() ?? "default"));

// --- Services that consume keyed services ---

builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<NotificationDispatcher>();

var app = builder.Build();

app.MapPost("/orders", async (OrderService orders) => {
    await orders.PlaceOrderAsync(new Order { Id = 1, CustomerEmail = "test@example.com" });
    return Results.Ok();
});

app.Run();

// --- Consumer using [FromKeyedServices] attribute ---

public sealed class OrderService(
    [FromKeyedServices("distributed")] ICache cache,
    [FromKeyedServices(NotificationChannel.Email)] INotifier emailNotifier,
    ILogger<OrderService> logger) {

    public async Task PlaceOrderAsync(Order order) {
        // Use distributed cache
        await cache.SetAsync($"order:{order.Id}", order);
        logger.LogInformation("Order {OrderId} cached", order.Id);

        // Send email notification
        await emailNotifier.SendAsync(order.CustomerEmail!,
            $"Order {order.Id} confirmed");
    }
}

// --- Dispatching to keyed services dynamically ---

public sealed class NotificationDispatcher(IServiceProvider sp) {
    public async Task DispatchAsync(
        NotificationChannel channel, string recipient, string message) {

        var notifier = sp.GetRequiredKeyedService<INotifier>(channel);
        await notifier.SendAsync(recipient, message);
    }

    public async Task BroadcastAsync(string message) {
        foreach (var channel in Enum.GetValues<NotificationChannel>()) {
            var notifier = sp.GetKeyedService<INotifier>(channel);
            if (notifier is not null) {
                await notifier.SendAsync("all", message);
            }
        }
    }
}

// --- Types ---

public enum NotificationChannel { Email, Sms, Push }

public interface ICache {
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value);
}

public interface INotifier {
    Task SendAsync(string recipient, string message);
}

public interface IFeatureFlag {
    bool IsEnabled(string feature);
}
