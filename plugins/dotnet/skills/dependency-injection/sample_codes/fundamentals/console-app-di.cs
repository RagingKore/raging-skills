// Console application with dependency injection using Generic Host
// Demonstrates basic registration, constructor injection, and lifetime management

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Options with validation
builder.Services
    .AddOptionsWithValidateOnStart<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .ValidateDataAnnotations();

// Service registrations
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();
builder.Services.AddTransient<IOrderValidator, OrderValidator>();
builder.Services.AddScoped<IOrderService, OrderService>();

// Background worker
builder.Services.AddHostedService<OrderProcessingWorker>();

using IHost host = builder.Build();
await host.RunAsync();

// --- Options ---

public sealed class AppOptions {
    public const string SectionName = "App";

    [Required]
    public required string Name { get; set; }

    [Range(1, 100)]
    public int MaxConcurrency { get; set; } = 10;
}

// --- Interfaces ---

public interface ICacheService {
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan expiry);
}

public interface IOrderRepository {
    Task<Order?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Order>> GetPendingAsync(CancellationToken ct);
    Task SaveAsync(Order order, CancellationToken ct);
}

public interface IOrderValidator {
    ValidationResult Validate(Order order);
}

public interface IOrderService {
    Task ProcessOrderAsync(int orderId, CancellationToken ct);
}

// --- Implementations ---

public sealed class OrderService(
    IOrderRepository repository,
    IOrderValidator validator,
    ICacheService cache,
    IOptions<AppOptions> options,
    ILogger<OrderService> logger) : IOrderService {

    public async Task ProcessOrderAsync(int orderId, CancellationToken ct) {
        logger.LogInformation("Processing order {OrderId} for {AppName}",
            orderId, options.Value.Name);

        var cached = await cache.GetAsync<Order>($"order:{orderId}");
        var order = cached ?? await repository.GetByIdAsync(orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found");

        var result = validator.Validate(order);
        if (!result.IsValid) {
            logger.LogWarning("Order {OrderId} failed validation: {Errors}",
                orderId, string.Join(", ", result.Errors));
            return;
        }

        order.Status = OrderStatus.Processed;
        await repository.SaveAsync(order, ct);
        await cache.SetAsync($"order:{orderId}", order, TimeSpan.FromMinutes(5));

        logger.LogInformation("Order {OrderId} processed successfully", orderId);
    }
}

// --- Background Service using IServiceScopeFactory ---

public sealed class OrderProcessingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OrderProcessingWorker> logger) : BackgroundService {

    protected override async Task ExecuteAsync(CancellationToken ct) {
        logger.LogInformation("Order processing worker started");

        while (!ct.IsCancellationRequested) {
            try {
                // Create a scope for each iteration — scoped services are fresh
                await using var scope = scopeFactory.CreateAsyncScope();
                var orderService = scope.ServiceProvider
                    .GetRequiredService<IOrderService>();
                var repository = scope.ServiceProvider
                    .GetRequiredService<IOrderRepository>();

                var pending = await repository.GetPendingAsync(ct);
                foreach (var order in pending) {
                    await orderService.ProcessOrderAsync(order.Id, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                logger.LogError(ex, "Error processing orders");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }
}

// appsettings.json:
// {
//   "App": {
//     "Name": "OrderProcessor",
//     "MaxConcurrency": 5
//   },
//   "ConnectionStrings": {
//     "Default": "Server=.;Database=Orders;Trusted_Connection=true"
//   }
// }
