// ASP.NET Core DI setup: comprehensive real-world example
// Demonstrates modular registration, middleware, and endpoint injection

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// === Module-based registration ===

builder.Services.AddAuthenticationModule(builder.Configuration);
builder.Services.AddOrderingModule(builder.Configuration);
builder.Services.AddNotificationModule(builder.Configuration);

// === Scope validation in Development (already enabled by default) ===

if (builder.Environment.IsDevelopment()) {
    builder.Services.Configure<ServiceProviderOptions>(opts => {
        opts.ValidateScopes = true;
        opts.ValidateOnBuild = true;
    });
}

var app = builder.Build();

// === Middleware that uses DI ===

app.UseMiddleware<RequestTimingMiddleware>();

// === Endpoints with injected services ===

app.MapGet("/orders/{id:int}", async (
    int id,
    IOrderService orderService,
    CancellationToken ct) => {

    var order = await orderService.GetByIdAsync(id, ct);
    return order is not null ? Results.Ok(order) : Results.NotFound();
});

app.MapPost("/orders", async (
    CreateOrderRequest request,
    IOrderService orderService,
    CancellationToken ct) => {

    var result = await orderService.CreateAsync(request, ct);
    return Results.Created($"/orders/{result.Id}", result);
});

// Keyed service in endpoint
app.MapGet("/cache/{key}", async (
    string key,
    [FromKeyedServices("distributed")] ICache cache) => {

    var value = await cache.GetAsync<string>(key);
    return value is not null ? Results.Ok(value) : Results.NotFound();
});

app.Run();

// ===== Registration Modules =====

public static class AuthenticationModuleExtensions {
    public static IServiceCollection AddAuthenticationModule(
        this IServiceCollection services,
        IConfiguration configuration) {

        services.AddOptionsWithValidateOnStart<JwtOptions>()
            .Bind(configuration.GetSection("Jwt"))
            .ValidateDataAnnotations();

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IUserService, UserService>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}

public static class OrderingModuleExtensions {
    public static IServiceCollection AddOrderingModule(
        this IServiceCollection services,
        IConfiguration configuration) {

        services.AddOptionsWithValidateOnStart<OrderOptions>()
            .Bind(configuration.GetSection("Ordering"))
            .ValidateDataAnnotations();

        // Core services
        services.AddScoped<IOrderRepository, SqlOrderRepository>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddTransient<IOrderValidator, OrderValidator>();

        // Keyed caches
        services.AddKeyedSingleton<ICache, RedisCache>("distributed");
        services.AddKeyedSingleton<ICache, MemoryCache>("local");

        return services;
    }
}

public static class NotificationModuleExtensions {
    public static IServiceCollection AddNotificationModule(
        this IServiceCollection services,
        IConfiguration configuration) {

        services.AddOptionsWithValidateOnStart<NotificationOptions>()
            .Bind(configuration.GetSection("Notifications"))
            .ValidateDataAnnotations();

        // Multiple notifiers
        services.AddTransient<EmailNotifier>();
        services.AddTransient<SmsNotifier>();

        // Composite as the interface
        services.AddTransient<INotifier>(sp => new CompositeNotifier([
            sp.GetRequiredService<EmailNotifier>(),
            sp.GetRequiredService<SmsNotifier>(),
        ]));

        return services;
    }
}

// ===== Middleware with DI =====

public sealed class RequestTimingMiddleware(
    RequestDelegate next,
    ILogger<RequestTimingMiddleware> logger) {

    // Middleware is singleton — constructor dependencies must be singleton-safe
    // Scoped services must be resolved from HttpContext.RequestServices

    public async Task InvokeAsync(HttpContext context) {
        var sw = Stopwatch.StartNew();

        try {
            await next(context);
        }
        finally {
            sw.Stop();
            logger.LogInformation(
                "{Method} {Path} completed in {ElapsedMs}ms with {StatusCode}",
                context.Request.Method,
                context.Request.Path,
                sw.ElapsedMilliseconds,
                context.Response.StatusCode);
        }
    }
}

// ===== Options Classes =====

public sealed class JwtOptions {
    [Required]
    public required string SecretKey { get; set; }

    [Required, Url]
    public required string Issuer { get; set; }

    [Required]
    public required string Audience { get; set; }

    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; } = 60;
}

public sealed class OrderOptions {
    [Range(1, 1000)]
    public int MaxItemsPerOrder { get; set; } = 100;

    [Range(1, 365)]
    public int OrderRetentionDays { get; set; } = 90;
}

public sealed class NotificationOptions {
    public bool EnableEmail { get; set; } = true;
    public bool EnableSms { get; set; }
}
