# Advanced DI Patterns

Advanced dependency injection patterns for .NET including decorators, composites, conditional registration, and service descriptor manipulation.

## Table of Contents

- [Decorator Pattern](#decorator-pattern)
- [Composite Pattern](#composite-pattern)
- [Conditional Registration](#conditional-registration)
- [Service Descriptor Manipulation](#service-descriptor-manipulation)
- [Lazy and Factory Injection](#lazy-and-factory-injection)
- [Options Integration with DI Services](#options-integration-with-di-services)
- [Typed/Named HTTP Clients](#typednamed-http-clients)
- [IServiceProviderIsService](#iserviceproviderisservice)
- [Complex Builder Patterns](#complex-builder-patterns)
- [Module Registration Pattern](#module-registration-pattern)

---

## Decorator Pattern

Wrap an existing service with additional behavior without modifying it.

### Manual Decoration

```csharp
// Register the inner service
builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();

// Decorate: replace registration with wrapper
builder.Services.Decorate<IOrderRepository, CachingOrderRepository>();
// NOTE: Decorate<> is from Scrutor. Without Scrutor, use manual approach below.

// Manual decoration without Scrutor:
builder.Services.AddScoped<IOrderRepository>(sp => {
    var inner = new SqlOrderRepository(
        sp.GetRequiredService<AppDbContext>());
    var cache = sp.GetRequiredService<IDistributedCache>();
    var logger = sp.GetRequiredService<ILogger<CachingOrderRepository>>();
    return new CachingOrderRepository(inner, cache, logger);
});
```

### Decorator Implementation

```csharp
public sealed class CachingOrderRepository(
    IOrderRepository inner,
    IDistributedCache cache,
    ILogger<CachingOrderRepository> logger) : IOrderRepository {

    public async Task<Order?> GetByIdAsync(int id, CancellationToken ct) {
        string key = $"order:{id}";
        var cached = await cache.GetStringAsync(key, ct);

        if (cached is not null) {
            logger.LogDebug("Cache hit for order {OrderId}", id);
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

    // Delegate all other methods to inner
    public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct)
        => inner.GetAllAsync(ct);

    public Task SaveAsync(Order order, CancellationToken ct) {
        // Invalidate cache on write
        cache.Remove($"order:{order.Id}");
        return inner.SaveAsync(order, ct);
    }
}
```

### Stacking Multiple Decorators

```csharp
// Execution order: Logging → Caching → Retry → SqlOrderRepository
builder.Services.AddScoped<SqlOrderRepository>();
builder.Services.AddScoped<IOrderRepository>(sp => {
    var sql = sp.GetRequiredService<SqlOrderRepository>();
    var retry = new RetryOrderRepository(sql, maxRetries: 3);
    var cache = new CachingOrderRepository(retry,
        sp.GetRequiredService<IDistributedCache>(),
        sp.GetRequiredService<ILogger<CachingOrderRepository>>());
    return new LoggingOrderRepository(cache,
        sp.GetRequiredService<ILogger<LoggingOrderRepository>>());
});
```

---

## Composite Pattern

Combine multiple implementations into one that dispatches to all.

```csharp
// Register individual implementations
builder.Services.AddTransient<INotifier, EmailNotifier>();
builder.Services.AddTransient<INotifier, SmsNotifier>();
builder.Services.AddTransient<INotifier, PushNotifier>();

// Register composite that wraps all implementations
builder.Services.AddTransient<INotifier>(sp => {
    var notifiers = sp.GetServices<INotifier>().ToList();
    // Remove composite to avoid recursion (if needed)
    return new CompositeNotifier(notifiers);
});

public sealed class CompositeNotifier(IEnumerable<INotifier> notifiers) : INotifier {
    public async Task NotifyAsync(string message, CancellationToken ct) {
        var tasks = notifiers.Select(n => n.NotifyAsync(message, ct));
        await Task.WhenAll(tasks);
    }
}
```

> **Warning**: If the composite itself is registered as `INotifier`, resolving `IEnumerable<INotifier>` inside the composite will include the composite itself → infinite recursion. Register concrete types separately and resolve them explicitly.

### Safe Composite Registration

```csharp
// Register concretes, not interface
builder.Services.AddTransient<EmailNotifier>();
builder.Services.AddTransient<SmsNotifier>();
builder.Services.AddTransient<PushNotifier>();

// Composite resolves concretes explicitly
builder.Services.AddTransient<INotifier>(sp => new CompositeNotifier([
    sp.GetRequiredService<EmailNotifier>(),
    sp.GetRequiredService<SmsNotifier>(),
    sp.GetRequiredService<PushNotifier>(),
]));
```

---

## Conditional Registration

### Environment-Based

```csharp
if (builder.Environment.IsDevelopment()) {
    builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
    builder.Services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
} else {
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
    builder.Services.AddSingleton<IPaymentGateway, StripePaymentGateway>();
}
```

### Configuration-Based

```csharp
var cacheProvider = builder.Configuration.GetValue<string>("Cache:Provider");

builder.Services.AddSingleton<ICache>(cacheProvider switch {
    "redis" => sp => new RedisCache(
        sp.GetRequiredService<IOptions<RedisOptions>>()),
    "memory" => sp => new MemoryCache(
        sp.GetRequiredService<IOptions<MemoryCacheOptions>>()),
    _ => throw new InvalidOperationException(
        $"Unknown cache provider: {cacheProvider}")
});
```

### Feature Flag-Based

```csharp
builder.Services.AddScoped<IOrderPipeline>(sp => {
    var features = sp.GetRequiredService<IFeatureManager>();

    var pipeline = new OrderPipeline();
    pipeline.AddStep(sp.GetRequiredService<ValidateOrderStep>());

    if (features.IsEnabledAsync("FraudDetection").GetAwaiter().GetResult()) {
        pipeline.AddStep(sp.GetRequiredService<FraudCheckStep>());
    }

    pipeline.AddStep(sp.GetRequiredService<ProcessPaymentStep>());
    return pipeline;
});
```

---

## Service Descriptor Manipulation

### Replace an Existing Registration

```csharp
// Replace ALL registrations of IEmailSender
builder.Services.Replace(
    ServiceDescriptor.Singleton<IEmailSender, MockEmailSender>());

// Remove a specific registration
var descriptor = builder.Services
    .FirstOrDefault(d => d.ServiceType == typeof(IEmailSender));
if (descriptor is not null) {
    builder.Services.Remove(descriptor);
}
```

### RemoveAll

```csharp
builder.Services.RemoveAll<IHealthCheck>(); // Remove all IHealthCheck registrations
builder.Services.RemoveAll(typeof(IValidator<>)); // Remove all open generic validators
```

### Inspect Registrations

```csharp
// List all registered services (useful for diagnostics)
foreach (var descriptor in builder.Services) {
    Console.WriteLine(
        $"{descriptor.ServiceType.Name} → " +
        $"{descriptor.ImplementationType?.Name ?? "factory"} " +
        $"({descriptor.Lifetime})");
}

// Check if a service is registered
bool hasCache = builder.Services.Any(
    d => d.ServiceType == typeof(ICache));
```

---

## Lazy and Factory Injection

### Lazy<T> (Not Built-In)

The default container does not support `Lazy<T>`. Register explicitly:

```csharp
builder.Services.AddTransient(typeof(Lazy<>), typeof(LazyService<>));

internal sealed class LazyService<T>(IServiceProvider sp) : Lazy<T>(
    sp.GetRequiredService<T>) where T : class;

// Usage:
public sealed class ExpensiveConsumer(Lazy<IExpensiveService> lazy) {
    public void DoWork() {
        if (needsExpensiveService) {
            var service = lazy.Value; // Created on first access
        }
    }
}
```

### Func<T> Factory

```csharp
// Register a factory delegate
builder.Services.AddTransient<Func<IWorker>>(
    sp => () => sp.GetRequiredService<IWorker>());

// Usage: create multiple instances
public sealed class WorkerPool(Func<IWorker> createWorker) {
    public void Scale(int count) {
        for (int i = 0; i < count; i++) {
            _workers.Add(createWorker());
        }
    }
}
```

---

## Options Integration with DI Services

Configure options using services from the container. Up to 5 service dependencies supported.

```csharp
// Single service dependency
builder.Services
    .AddOptions<FeatureOptions>()
    .Configure<IHostEnvironment>((options, env) => {
        options.EnableDetailedErrors = env.IsDevelopment();
    });

// Multiple service dependencies (up to 5)
builder.Services
    .AddOptions<CacheOptions>()
    .Configure<IConfiguration, IHostEnvironment>(
        (options, config, env) => {
            options.ConnectionString = config.GetConnectionString("Redis")!;
            options.SlidingExpiration = env.IsProduction()
                ? TimeSpan.FromMinutes(30)
                : TimeSpan.FromMinutes(5);
        });

// PostConfigure with DI services
builder.Services
    .AddOptions<LoggingOptions>()
    .PostConfigure<IHostEnvironment>((options, env) => {
        if (env.IsProduction()) {
            options.MinimumLevel = LogLevel.Warning;
        }
    });
```

### IConfigureOptions<T> / IPostConfigureOptions<T> (Class-Based)

For complex configuration logic that needs DI:

```csharp
public sealed class ConfigureJwtOptions(
    IConfiguration config,
    IHostEnvironment env) : IConfigureOptions<JwtBearerOptions> {

    public void Configure(JwtBearerOptions options) {
        var section = config.GetSection("Jwt");
        options.Authority = section["Authority"];
        options.Audience = section["Audience"];

        if (env.IsDevelopment()) {
            options.RequireHttpsMetadata = false;
        }

        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }
}

// Register
builder.Services.AddSingleton<
    IConfigureOptions<JwtBearerOptions>, ConfigureJwtOptions>();
```

---

## Typed/Named HTTP Clients

### Typed Client

```csharp
builder.Services.AddHttpClient<GitHubClient>(client => {
    client.BaseAddress = new Uri("https://api.github.com");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/1.0");
});

public sealed class GitHubClient(HttpClient client) {
    public async Task<GitHubUser?> GetUserAsync(string username) {
        return await client.GetFromJsonAsync<GitHubUser>($"users/{username}");
    }
}

// Inject directly:
public sealed class MyService(GitHubClient github) { }
```

### Named Client

```csharp
builder.Services.AddHttpClient("github", client => {
    client.BaseAddress = new Uri("https://api.github.com");
});

builder.Services.AddHttpClient("internal-api", client => {
    client.BaseAddress = new Uri("https://internal.example.com");
});

// Resolve via factory:
public sealed class ApiService(IHttpClientFactory factory) {
    public async Task CallGitHub() {
        var client = factory.CreateClient("github");
        // ...
    }
}
```

---

## IServiceProviderIsService

Probe whether a service is registered without resolving it.

```csharp
public sealed class SmartFactory(
    IServiceProvider sp,
    IServiceProviderIsService probe) {

    public IProcessor GetProcessor(string type) {
        var serviceType = Type.GetType($"MyApp.Processors.{type}Processor");

        if (serviceType is not null && probe.IsService(serviceType)) {
            return (IProcessor)sp.GetRequiredService(serviceType);
        }

        return sp.GetRequiredService<DefaultProcessor>();
    }
}
```

---

## Complex Builder Patterns

### Fluent Builder for Complex Service Setup

```csharp
public sealed class MessagingBuilder {
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;
    private string _provider = "rabbitmq";
    private bool _enableRetry = true;
    private int _maxRetries = 3;

    internal MessagingBuilder(
        IServiceCollection services,
        IConfiguration configuration) {
        _services = services;
        _configuration = configuration;
    }

    public MessagingBuilder UseProvider(string provider) {
        _provider = provider;
        return this;
    }

    public MessagingBuilder WithRetry(int maxRetries = 3) {
        _enableRetry = true;
        _maxRetries = maxRetries;
        return this;
    }

    public MessagingBuilder DisableRetry() {
        _enableRetry = false;
        return this;
    }

    public MessagingBuilder AddConsumer<TConsumer>()
        where TConsumer : class, IMessageConsumer {
        _services.AddScoped<IMessageConsumer, TConsumer>();
        return this;
    }

    internal void Build() {
        // Register core services based on accumulated configuration
        _services.AddOptionsWithValidateOnStart<MessagingOptions>()
            .Bind(_configuration.GetSection("Messaging"))
            .ValidateDataAnnotations()
            .Configure(opts => opts.Provider = _provider);

        switch (_provider) {
            case "rabbitmq":
                _services.AddSingleton<IMessageBus, RabbitMqMessageBus>();
                break;
            case "azure-servicebus":
                _services.AddSingleton<IMessageBus, AzureServiceBusMessageBus>();
                break;
            case "in-memory":
                _services.AddSingleton<IMessageBus, InMemoryMessageBus>();
                break;
        }

        if (_enableRetry) {
            // Decorate with retry wrapper
            _services.AddSingleton<IMessageBus>(sp => {
                var inner = sp.GetServices<IMessageBus>().First();
                return new RetryMessageBus(inner, _maxRetries);
            });
        }

        _services.AddHostedService<MessageConsumerHostedService>();
    }
}

public static class MessagingExtensions {
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<MessagingBuilder> configure) {

        var builder = new MessagingBuilder(services, configuration);
        configure(builder);
        builder.Build();
        return services;
    }
}

// Usage:
builder.Services.AddMessaging(builder.Configuration, messaging => {
    messaging
        .UseProvider("rabbitmq")
        .WithRetry(maxRetries: 5)
        .AddConsumer<OrderCreatedConsumer>()
        .AddConsumer<PaymentReceivedConsumer>();
});
```

### Multi-Tenant Builder

```csharp
public static class MultiTenantExtensions {
    public static IServiceCollection AddMultiTenancy(
        this IServiceCollection services,
        Action<TenantBuilder> configure) {

        var builder = new TenantBuilder(services);
        configure(builder);
        return services;
    }
}

public sealed class TenantBuilder {
    private readonly IServiceCollection _services;

    internal TenantBuilder(IServiceCollection services) {
        _services = services;
        // Core multi-tenancy infrastructure
        _services.AddScoped<ITenantAccessor, TenantAccessor>();
    }

    public TenantBuilder WithResolutionStrategy<T>()
        where T : class, ITenantResolutionStrategy {
        _services.AddScoped<ITenantResolutionStrategy, T>();
        return this;
    }

    public TenantBuilder WithStore<T>() where T : class, ITenantStore {
        _services.AddScoped<ITenantStore, T>();
        return this;
    }

    public TenantBuilder WithPerTenantOptions<TOptions>(
        Action<TOptions, Tenant> configure) where TOptions : class {
        _services.AddScoped<IConfigureOptions<TOptions>>(sp => {
            var accessor = sp.GetRequiredService<ITenantAccessor>();
            return new ConfigureNamedOptions<TOptions>(null, options => {
                if (accessor.Current is { } tenant) {
                    configure(options, tenant);
                }
            });
        });
        return this;
    }
}

// Usage:
builder.Services.AddMultiTenancy(tenant => {
    tenant
        .WithResolutionStrategy<HostHeaderTenantResolution>()
        .WithStore<DatabaseTenantStore>()
        .WithPerTenantOptions<DatabaseOptions>((opts, t) => {
            opts.ConnectionString = t.ConnectionString;
        });
});
```

---

## Module Registration Pattern

Organize registrations into self-contained modules for large applications.

```csharp
public interface IServiceModule {
    void Register(IServiceCollection services, IConfiguration configuration);
}

public sealed class OrderingModule : IServiceModule {
    public void Register(IServiceCollection services, IConfiguration configuration) {
        services.AddOptionsWithValidateOnStart<OrderOptions>()
            .Bind(configuration.GetSection("Ordering"))
            .ValidateDataAnnotations();

        services.AddScoped<IOrderRepository, SqlOrderRepository>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddTransient<IOrderValidator, OrderValidator>();
    }
}

public sealed class NotificationModule : IServiceModule {
    public void Register(IServiceCollection services, IConfiguration configuration) {
        services.AddOptionsWithValidateOnStart<SmtpOptions>()
            .Bind(configuration.GetSection("Smtp"))
            .ValidateDataAnnotations();

        services.AddTransient<INotifier, EmailNotifier>();
        services.AddTransient<INotifier, SmsNotifier>();
    }
}

// Extension to register all modules
public static class ModuleExtensions {
    public static IServiceCollection AddModules(
        this IServiceCollection services,
        IConfiguration configuration,
        params IServiceModule[] modules) {

        foreach (var module in modules) {
            module.Register(services, configuration);
        }
        return services;
    }
}

// Usage:
builder.Services.AddModules(builder.Configuration,
    new OrderingModule(),
    new NotificationModule());
```
