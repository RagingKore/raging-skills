---
name: dependency-injection
description: |
  .NET dependency injection expert for IServiceCollection, service lifetimes, and advanced DI patterns.
  Covers service registration (transient, scoped, singleton, keyed services), constructor injection,
  factory registrations, open generics, decorator pattern, composite pattern, service resolution,
  scope management, IServiceScopeFactory for background services, captive dependency prevention,
  async initialization, service descriptors, TryAdd semantics, ActivatorUtilities, and integration
  with IOptions/configuration. Use when registering services, configuring DI containers, resolving
  dependencies, fixing lifetime mismatches, implementing factory patterns, writing extension methods
  for service registration, setting up background services with scoped dependencies, debugging DI
  issues, or structuring service composition roots in .NET apps.
---

# .NET Dependency Injection Expert

Comprehensive guidance for `Microsoft.Extensions.DependencyInjection` in .NET 9+ console apps, ASP.NET Core, and worker services.

## Quick Decision Matrix

| Need                                       | Solution                                                    | Lifetime          |
|--------------------------------------------|-------------------------------------------------------------|-------------------|
| New instance every time                    | `AddTransient<T>()`                                         | None (always new) |
| One per HTTP request / unit of work        | `AddScoped<T>()`                                            | Scope             |
| Shared app-wide singleton                  | `AddSingleton<T>()`                                         | App lifetime      |
| Multiple implementations of same interface | `AddTransient<IFoo, FooA>()` + `AddTransient<IFoo, FooB>()` | Varies            |
| Resolve by key/name (.NET 8+)              | `AddKeyedSingleton<T>("key")`                               | Varies            |
| Instance requiring async init              | Factory + `IHostedService` warmup                           | Singleton         |
| Eagerly construct singleton at startup     | `AddActivatedSingleton<T>()` (AutoActivation package)       | Singleton         |
| Library services grouped together          | Extension method on `IServiceCollection`                    | N/A               |
| Avoid replacing existing registration      | `TryAddTransient<T>()`                                      | Varies            |
| Disposable per-request resources           | `AddScoped<T>()`                                            | Scope             |

## Service Lifetimes

### The Three Lifetimes

```
Singleton ──────────────────────────────────── app shutdown
  └─ One instance shared across ALL requests and scopes

Scoped     ├── request 1 ──┤  ├── request 2 ──┤
  └─ One instance per scope (per HTTP request in ASP.NET Core)

Transient  * * * * * * * * * * * * * * * * * *
  └─ New instance EVERY time it's requested from the container
```

### Lifetime Rules

| Consuming Service | Can Depend On                | CANNOT Depend On                        |
|-------------------|------------------------------|-----------------------------------------|
| **Transient**     | Transient, Scoped, Singleton | —                                       |
| **Scoped**        | Scoped, Singleton            | —                                       |
| **Singleton**     | Singleton only               | Scoped, Transient (captive dependency!) |

> **Captive dependency**: A shorter-lived service captured by a longer-lived one. The scoped/transient service is held alive beyond its intended lifetime, causing stale data, connection leaks, and thread-safety bugs.

### Enable Scope Validation (Development)

```csharp
var builder = WebApplication.CreateBuilder(args);
// Already enabled by default when ASPNETCORE_ENVIRONMENT=Development

// For generic host:
var builder = Host.CreateApplicationBuilder(args);
// Set ValidateScopes and ValidateOnBuild:
builder.Services.AddOptions<ServiceProviderOptions>()
    .Configure(opts => {
        opts.ValidateScopes = true;
        opts.ValidateOnBuild = true;
    });
```

`ValidateScopes` throws at runtime when a scoped service is resolved from the root provider.
`ValidateOnBuild` verifies all registrations can be constructed at startup.

## Registration Patterns

### Basic Registration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Interface → Implementation
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// Concrete type only (resolved by concrete type)
builder.Services.AddTransient<OrderValidator>();

// Instance registration (singleton only)
builder.Services.AddSingleton(new ApiSettings { BaseUrl = "https://api.example.com" });
builder.Services.AddSingleton(TimeProvider.System);
```

### Factory Registration

Use factories when construction requires logic, conditional setup, or dependencies not in the container.

```csharp
// Simple factory
builder.Services.AddScoped<IDbConnection>(sp => {
    var config = sp.GetRequiredService<IConfiguration>();
    var connection = new SqlConnection(config.GetConnectionString("Default"));
    return connection;
});

// Factory with multiple dependencies
builder.Services.AddScoped<IOrderService>(sp => {
    var repo = sp.GetRequiredService<IOrderRepository>();
    var logger = sp.GetRequiredService<ILogger<OrderService>>();
    var options = sp.GetRequiredService<IOptions<OrderOptions>>();
    return new OrderService(repo, logger, options.Value.MaxItemsPerOrder);
});

// Conditional registration
builder.Services.AddSingleton<IEmailSender>(sp => {
    var env = sp.GetRequiredService<IHostEnvironment>();
    return env.IsDevelopment()
        ? new ConsoleEmailSender()
        : new SmtpEmailSender(sp.GetRequiredService<IOptions<SmtpOptions>>());
});
```

### Open Generic Registration

Register a generic interface/class without specifying the type argument. The container closes it at resolve time.

```csharp
// Basic open generic — must use typeof(), not angle brackets
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddTransient(typeof(IValidator<>), typeof(DataAnnotationsValidator<>));
builder.Services.AddSingleton(typeof(ICache<>), typeof(MemoryCache<>));

// Resolves correctly via constructor injection:
// IRepository<Order>    → EfRepository<Order>
// IRepository<Customer> → EfRepository<Customer>
// IValidator<Order>     → DataAnnotationsValidator<Order>
```

**Multiple type parameters:**

```csharp
// IHandler<TRequest, TResponse> with two type args
builder.Services.AddTransient(typeof(IHandler<,>), typeof(DefaultHandler<,>));

// Resolves: IHandler<CreateOrderCommand, OrderResult> → DefaultHandler<CreateOrderCommand, OrderResult>
```

**Multiple implementations of the same open generic (IEnumerable):**

```csharp
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));

// IEnumerable<IPipelineBehavior<TReq, TRes>> resolves all three
// Single injection resolves the LAST registered (PerformanceBehavior)
```

**Constrained generics — the container respects `where` constraints:**

```csharp
public class AuditableRepository<T> : IRepository<T>
    where T : class, IAuditable { /* ... */ }

public class StandardRepository<T> : IRepository<T>
    where T : class { /* ... */ }

// Register both — the container selects based on constraint match
builder.Services.AddScoped(typeof(IRepository<>), typeof(AuditableRepository<>));
builder.Services.AddScoped(typeof(IRepository<>), typeof(StandardRepository<>));

// IRepository<Order>       → StandardRepository<Order> (Order doesn't implement IAuditable)
// IRepository<AuditEntry>  → AuditableRepository<AuditEntry> (AuditEntry : IAuditable)
// If both match, last-wins for single resolution; both appear in IEnumerable
```

**Factory-based open generic registration:**

```csharp
// When you need factory logic for an open generic, register a closed version per type
// (the container doesn't support factory delegates with open generics directly)
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

// Override for a specific closed type
builder.Services.AddScoped<IRepository<Order>>(sp => {
    var inner = new EfRepository<Order>(sp.GetRequiredService<AppDbContext>());
    var cache = sp.GetRequiredService<IDistributedCache>();
    return new CachingRepository<Order>(inner, cache);
});
// IRepository<Order> → CachingRepository<Order> (closed wins over open)
// IRepository<Product> → EfRepository<Product> (open generic fallback)
```

**Open generics with keyed services (.NET 8+):**

```csharp
builder.Services.AddKeyedScoped(typeof(IRepository<>), "readonly", typeof(ReadOnlyRepository<>));
builder.Services.AddKeyedScoped(typeof(IRepository<>), "readwrite", typeof(EfRepository<>));

public sealed class OrderService(
    [FromKeyedServices("readonly")] IRepository<Order> readRepo,
    [FromKeyedServices("readwrite")] IRepository<Order> writeRepo) { }
```

**TryAdd with open generics:**

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

// Only registers if no IRepository<> is already registered
builder.Services.TryAdd(
    ServiceDescriptor.Scoped(typeof(IRepository<>), typeof(EfRepository<>)));
```

**Resolution — open generics resolve through normal constructor injection:**

```csharp
// Constructor injection (preferred)
public sealed class OrderService(
    IRepository<Order> orders,
    IValidator<Order> validator,
    IEnumerable<IPipelineBehavior<CreateOrderCommand, OrderResult>> behaviors) { }

// Manual resolution
var repo = sp.GetRequiredService<IRepository<Order>>();
```

**Closed registrations take priority over open generic registrations.** If both `IRepository<>` → `EfRepository<>` and `IRepository<Order>` → `CustomOrderRepository` are registered, resolving `IRepository<Order>` returns `CustomOrderRepository`.

### TryAdd Semantics (Library Authors)

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

// Only registers if no IEmailSender is already registered
builder.Services.TryAddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.TryAddScoped<IOrderRepository, SqlOrderRepository>();
builder.Services.TryAddSingleton<ICacheService, MemoryCacheService>();

// TryAddEnumerable: only adds if exact implementation isn't registered
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Transient<IValidator, EmailValidator>());
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Transient<IValidator, PhoneValidator>());
// Both register because implementations differ
```

### Keyed Services (.NET 8+)

```csharp
// Register with string keys
builder.Services.AddKeyedSingleton<ICache, RedisCache>("distributed");
builder.Services.AddKeyedSingleton<ICache, MemoryCache>("local");

// Register with enum keys
builder.Services.AddKeyedScoped<INotifier, EmailNotifier>(Channel.Email);
builder.Services.AddKeyedScoped<INotifier, SmsNotifier>(Channel.Sms);

// Consume via attribute
public sealed class OrderService(
    [FromKeyedServices("distributed")] ICache cache,
    [FromKeyedServices(Channel.Email)] INotifier notifier) { }

// Resolve manually
var cache = sp.GetRequiredKeyedService<ICache>("distributed");

// AnyKey wildcard: factory that receives the key
builder.Services.AddKeyedTransient<ICache>(
    KeyedService.AnyKey,
    (sp, key) => new PrefixedCache(key?.ToString() ?? "default"));
```

### Multiple Implementations (IEnumerable)

```csharp
// Register multiple implementations
builder.Services.AddTransient<IHealthCheck, DatabaseHealthCheck>();
builder.Services.AddTransient<IHealthCheck, RedisHealthCheck>();
builder.Services.AddTransient<IHealthCheck, ExternalApiHealthCheck>();

// Inject all - order matches registration order
public sealed class HealthCheckRunner(IEnumerable<IHealthCheck> checks) {
    public async Task<bool> CheckAllAsync() {
        foreach (var check in checks) {
            if (!await check.IsHealthyAsync()) return false;
        }
        return true;
    }
}

// Last-wins resolution for single injection:
// IHealthCheck → ExternalApiHealthCheck (last registered)
```

## Constructor Injection

### Selection Rules

The container picks the constructor with the most parameters it can satisfy:

1. Evaluate all public constructors
2. For each, check if all parameters can be resolved
3. Pick the one with the most resolvable parameters
4. If ambiguous (two constructors with same max count), throw `InvalidOperationException`

```csharp
public class MyService {
    // This constructor is chosen if ILogger and ICache are both registered
    public MyService(ILogger<MyService> logger, ICache cache) { }

    // This constructor is chosen if only ILogger is registered
    public MyService(ILogger<MyService> logger) { }
}
```

### ActivatorUtilities

Create instances with mixed DI and explicit parameters:

```csharp
// Resolve ILogger from DI, pass orderId explicitly
var processor = ActivatorUtilities.CreateInstance<OrderProcessor>(
    serviceProvider, orderId);

// Pre-compiled factory (better performance for repeated creation)
var factory = ActivatorUtilities.CreateFactory<OrderProcessor>(
    [typeof(int)]); // explicit parameter types

var processor = factory(serviceProvider, [orderId]);
```

## Extension Methods for Grouped Registration

Bundle related registrations into a single extension method. This is the standard pattern for libraries and modules.

```csharp
public static class OrderingServiceCollectionExtensions {
    public static IServiceCollection AddOrderingModule(
        this IServiceCollection services,
        IConfiguration configuration) {

        // Options
        services.AddOptionsWithValidateOnStart<OrderOptions>()
            .Bind(configuration.GetSection(OrderOptions.SectionName))
            .ValidateDataAnnotations();

        // Core services
        services.AddScoped<IOrderRepository, SqlOrderRepository>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddTransient<IOrderValidator, OrderValidator>();

        // Domain events
        services.AddTransient<INotificationHandler<OrderPlaced>, OrderPlacedHandler>();

        return services;
    }
}

// Usage in Program.cs
builder.Services.AddOrderingModule(builder.Configuration);
```

## Scope Management

### IServiceScopeFactory in Singletons

Singletons that need scoped services must create their own scope:

```csharp
public sealed class OrderProcessingJob(
    IServiceScopeFactory scopeFactory,
    ILogger<OrderProcessingJob> logger) : BackgroundService {

    protected override async Task ExecuteAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

            var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var pending = await repository.GetPendingAsync(ct);
            foreach (var order in pending) {
                order.Process();
            }
            await unitOfWork.SaveChangesAsync(ct);

            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }
}
```

### Manual Scope Creation

```csharp
await using var scope = serviceProvider.CreateAsyncScope();
var service = scope.ServiceProvider.GetRequiredService<IScopedService>();
await service.DoWorkAsync();
// scope disposed → all scoped services disposed
```

## Auto-Activation (Eager Singletons)

**Package:** `Microsoft.Extensions.DependencyInjection.AutoActivation`

By default, singletons are lazy — not instantiated until first resolved. Auto-activation forces singleton construction **at host startup**, eliminating the need for boilerplate `IHostedService` warmup classes.

```csharp
using Microsoft.Extensions.DependencyInjection;

// Option 1: Register + activate in one call
builder.Services.AddActivatedSingleton<ICacheWarmer, CacheWarmer>();
builder.Services.AddActivatedSingleton<IConnectionPool>(
    sp => new ConnectionPool(sp.GetRequiredService<IOptions<DbOptions>>()));

// Option 2: Mark an already-registered singleton for activation
builder.Services.AddSingleton<IMetricsCollector, PrometheusCollector>();
builder.Services.ActivateSingleton<IMetricsCollector>();

// Works with keyed services too
builder.Services.AddActivatedKeyedSingleton<ICache, RedisCache>("primary");
builder.Services.ActivateKeyedSingleton<ICache>("secondary");

// TryAdd variants (safe for library authors)
builder.Services.TryAddActivatedSingleton<ICacheWarmer, CacheWarmer>();
```

Internally it registers an `IHostedService` that resolves all marked singletons during `StartAsync`, before the app begins processing requests. This replaces the manual pattern:

```csharp
// BEFORE: boilerplate hosted service just to trigger construction
builder.Services.AddSingleton<ICacheWarmer, CacheWarmer>();
builder.Services.AddHostedService<CacheWarmerInitializer>();

public class CacheWarmerInitializer(ICacheWarmer warmer) : IHostedService {
    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

// AFTER: one line
builder.Services.AddActivatedSingleton<ICacheWarmer, CacheWarmer>();
```

**Use when:** A singleton must be alive at startup — cache warming, connection pooling, metrics collectors, subscription listeners, or any service with side effects in its constructor.

## Anti-Patterns and Pitfalls

See [references/anti-patterns.md](references/anti-patterns.md) for detailed coverage of:
- Captive dependency (scoped/transient in singleton)
- Service locator anti-pattern
- Disposable transient memory leaks
- Async factory deadlock (`Task.Result` in factory)
- Scope over-creation and under-disposal
- Injecting `IServiceProvider` everywhere

## Advanced Patterns

See [references/advanced-patterns.md](references/advanced-patterns.md) for:
- Decorator pattern with DI
- Composite pattern with DI
- Named/typed HTTP clients
- Options integration with DI services
- Lazy initialization
- Conditional/environment-based registration
- Service descriptors and Replace/Remove operations
- IServiceProviderIsService for probing

## Learn More

| Topic               | How to Find                                                                                                    |
|---------------------|----------------------------------------------------------------------------------------------------------------|
| DI fundamentals     | `microsoft_docs_search(query=".NET dependency injection fundamentals overview")`                               |
| Service lifetimes   | `microsoft_docs_search(query=".NET dependency injection service lifetimes scoped transient singleton")`        |
| Keyed services      | `microsoft_docs_search(query=".NET 8 keyed services dependency injection")`                                    |
| DI guidelines       | `microsoft_docs_search(query=".NET dependency injection guidelines recommendations")`                          |
| Background services | `microsoft_docs_search(query=".NET BackgroundService IHostedService worker")`                                  |
| Options pattern     | See **configuration** skill or `microsoft_docs_search(query=".NET options pattern IOptions IOptionsSnapshot")` |
| DI in ASP.NET Core  | `microsoft_docs_search(query="ASP.NET Core dependency injection middleware services")`                         |
| Make HTTP requests  | `microsoft_docs_search(query=".NET IHttpClientFactory dependency injection")`                                  |
| Auto-activation     | `microsoft_docs_search(query="Microsoft.Extensions.DependencyInjection.AutoActivation singleton startup")`     |
