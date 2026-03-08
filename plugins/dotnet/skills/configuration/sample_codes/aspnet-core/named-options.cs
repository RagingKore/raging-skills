// Named Options: Multiple configurations of the same type
// Use case: Multiple external APIs with similar config shape

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Register named options from different config sections
builder.Services.Configure<ApiClientOptions>(
    ApiClientOptions.PaymentApi,
    builder.Configuration.GetSection("Apis:Payment"));

builder.Services.Configure<ApiClientOptions>(
    ApiClientOptions.NotificationApi,
    builder.Configuration.GetSection("Apis:Notification"));

builder.Services.Configure<ApiClientOptions>(
    ApiClientOptions.InventoryApi,
    builder.Configuration.GetSection("Apis:Inventory"));

builder.Services.AddScoped<OrderService>();

var app = builder.Build();

app.MapPost("/orders", async (OrderService svc) =>
    await svc.ProcessOrderAsync());

app.Run();

// --- Types ---

public sealed class ApiClientOptions {
    public const string PaymentApi = nameof(PaymentApi);
    public const string NotificationApi = nameof(NotificationApi);
    public const string InventoryApi = nameof(InventoryApi);

    [Required, Url]
    public required string BaseUrl { get; set; }

    [Required]
    public required string ApiKey { get; set; }

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;

    [Range(0, 5)]
    public int MaxRetries { get; set; } = 2;
}

public sealed class OrderService(IOptionsSnapshot<ApiClientOptions> optionsAccessor) {

    public Task ProcessOrderAsync() {
        ApiClientOptions payment = optionsAccessor.Get(ApiClientOptions.PaymentApi);
        ApiClientOptions notification = optionsAccessor.Get(ApiClientOptions.NotificationApi);
        ApiClientOptions inventory = optionsAccessor.Get(ApiClientOptions.InventoryApi);

        Console.WriteLine($"Payment API: {payment.BaseUrl} (timeout: {payment.TimeoutSeconds}s)");
        Console.WriteLine($"Notification API: {notification.BaseUrl}");
        Console.WriteLine($"Inventory API: {inventory.BaseUrl}");

        return Task.CompletedTask;
    }
}

// appsettings.json:
// {
//   "Apis": {
//     "Payment": {
//       "BaseUrl": "https://pay.example.com",
//       "ApiKey": "pk_live_xxx",
//       "TimeoutSeconds": 15,
//       "MaxRetries": 3
//     },
//     "Notification": {
//       "BaseUrl": "https://notify.example.com",
//       "ApiKey": "nk_live_xxx",
//       "TimeoutSeconds": 5
//     },
//     "Inventory": {
//       "BaseUrl": "https://inventory.example.com",
//       "ApiKey": "ik_live_xxx"
//     }
//   }
// }
