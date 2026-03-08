# DI Anti-Patterns and Pitfalls

Common dependency injection mistakes in .NET, how to detect them, and how to fix them.

## Table of Contents

- [Captive Dependency](#captive-dependency)
- [Service Locator Anti-Pattern](#service-locator-anti-pattern)
- [Disposable Transient Leaks](#disposable-transient-leaks)
- [Async Factory Deadlock](#async-factory-deadlock)
- [Constructor Over-Injection](#constructor-over-injection)
- [Scope Mismanagement](#scope-mismanagement)
- [Ambient Context / Static Service Locator](#ambient-context--static-service-locator)

---

## Captive Dependency

A shorter-lived service captured by a longer-lived service. The scoped/transient becomes a de-facto singleton, causing stale data, thread-safety bugs, and connection leaks.

### The Problem

```csharp
// WRONG: Singleton captures scoped DbContext
builder.Services.AddDbContext<AppDbContext>(); // Scoped by default
builder.Services.AddSingleton<IOrderCache, OrderCache>();

public sealed class OrderCache(AppDbContext db) : IOrderCache {
    // db is a SINGLE instance shared across ALL requests
    // DbContext is NOT thread-safe → data corruption, connection leaks
}
```

### Detection

Enable `ValidateScopes` (on by default in Development):

```csharp
// Throws InvalidOperationException at runtime:
// "Cannot consume scoped service 'AppDbContext' from singleton 'OrderCache'"
```

Enable `ValidateOnBuild` to catch at startup:

```csharp
builder.Host.UseDefaultServiceProvider(options => {
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;  // Catches at Build(), not first resolve
});
```

### Fixes

**Option 1: Match lifetimes** — Make the consumer scoped too:

```csharp
builder.Services.AddScoped<IOrderCache, OrderCache>();
```

**Option 2: IServiceScopeFactory** — Create a scope per operation:

```csharp
public sealed class OrderCache(IServiceScopeFactory scopeFactory) : IOrderCache {
    public async Task<Order?> GetAsync(int id) {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.FindAsync(id);
    }
}
```

**Option 3: Factory delegate** — Inject a factory:

```csharp
builder.Services.AddSingleton<IOrderCache>(sp => {
    return new OrderCache(() => sp.CreateScope()
        .ServiceProvider.GetRequiredService<AppDbContext>());
});

public sealed class OrderCache(Func<AppDbContext> dbFactory) : IOrderCache {
    public async Task<Order?> GetAsync(int id) {
        using var db = dbFactory();
        return await db.Orders.FindAsync(id);
    }
}
```

---

## Service Locator Anti-Pattern

Injecting `IServiceProvider` to resolve dependencies manually instead of declaring them in the constructor.

### The Problem

```csharp
// WRONG: Service locator - hides dependencies
public sealed class OrderService(IServiceProvider sp) {
    public void Process(Order order) {
        // Dependencies are invisible to callers and the container
        var repo = sp.GetRequiredService<IOrderRepository>();
        var logger = sp.GetRequiredService<ILogger<OrderService>>();
        var validator = sp.GetRequiredService<IOrderValidator>();
        // ...
    }
}
```

Problems:
- Dependencies are hidden — not visible from constructor signature
- Harder to test — must set up full `IServiceProvider`
- `ValidateOnBuild` can't verify these dependencies
- Violates explicit dependency principle

### Fix

Declare dependencies explicitly:

```csharp
public sealed class OrderService(
    IOrderRepository repo,
    ILogger<OrderService> logger,
    IOrderValidator validator) {

    public void Process(Order order) {
        // All dependencies visible and testable
    }
}
```

### Exceptions Where IServiceProvider Is Acceptable

- **Middleware factories** — `IMiddlewareFactory` needs `IServiceProvider`
- **Generic host** — Framework internals resolve from root
- **Plugin systems** — Dynamic resolution of unknown types at compile time
- **ActivatorUtilities** — Creating objects with mixed DI and explicit parameters

---

## Disposable Transient Leaks

The container tracks `IDisposable`/`IAsyncDisposable` transient services it creates. They are NOT disposed until the scope (or root provider) is disposed.

### The Problem

```csharp
builder.Services.AddTransient<IFileProcessor, FileProcessor>();

// FileProcessor implements IDisposable
public sealed class FileProcessor : IFileProcessor, IDisposable {
    private readonly FileStream _stream = File.OpenRead("data.bin");
    public void Dispose() => _stream.Dispose();
}

// Every resolution creates a NEW instance
// ALL instances held in memory until the scope ends
// In a long-lived scope or singleton → memory leak
```

### Detection

Large memory growth over time. Object tracking in diagnostics shows accumulated transient disposable instances.

### Fixes

**Option 1: Make it scoped** — Disposed at end of HTTP request:

```csharp
builder.Services.AddScoped<IFileProcessor, FileProcessor>();
```

**Option 2: Factory that caller disposes** — Remove container tracking:

```csharp
builder.Services.AddSingleton<Func<IFileProcessor>>(
    sp => () => new FileProcessor());

// Caller is responsible for disposal
public sealed class BatchJob(Func<IFileProcessor> createProcessor) {
    public async Task RunAsync() {
        using var processor = createProcessor();
        await processor.ProcessAsync();
    }
}
```

**Option 3: `ObjectPool<T>`** — Reuse expensive resources:

```csharp
builder.Services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
builder.Services.AddSingleton(sp => {
    var provider = sp.GetRequiredService<ObjectPoolProvider>();
    return provider.Create(new FileProcessorPoolPolicy());
});
```

---

## Async Factory Deadlock

Using `Task.Result` or `Task.GetAwaiter().GetResult()` in a DI factory causes deadlocks in ASP.NET Core.

### The Problem

```csharp
// WRONG: Sync-over-async deadlock
builder.Services.AddSingleton<ISecretStore>(sp => {
    var client = sp.GetRequiredService<SecretClient>();
    // DEADLOCK in ASP.NET Core — blocks the thread pool
    var secret = client.GetSecretAsync("api-key").Result;
    return new SecretStore(secret.Value.Value);
});
```

### Fixes

**Option 1: Warmup via IHostedService** (recommended):

```csharp
builder.Services.AddSingleton<SecretStore>();
builder.Services.AddHostedService<SecretStoreWarmup>();

public sealed class SecretStoreWarmup(SecretStore store) : IHostedService {
    public async Task StartAsync(CancellationToken ct) {
        await store.InitializeAsync(ct);  // Runs before request processing
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

public sealed class SecretStore : ISecretStore {
    private string? _apiKey;

    public async Task InitializeAsync(CancellationToken ct) {
        // Safe async initialization
        var client = new SecretClient(/* ... */);
        _apiKey = (await client.GetSecretAsync("api-key", cancellationToken: ct)).Value.Value;
    }

    public string ApiKey => _apiKey ?? throw new InvalidOperationException("Not initialized");
}
```

**Option 2: Lazy async initialization**:

```csharp
builder.Services.AddSingleton<ISecretStore>(sp => {
    var client = sp.GetRequiredService<SecretClient>();
    return new LazySecretStore(async () => {
        var secret = await client.GetSecretAsync("api-key");
        return secret.Value.Value;
    });
});

public sealed class LazySecretStore(Func<Task<string>> factory) : ISecretStore {
    private readonly Lazy<Task<string>> _lazy = new(factory);
    public async Task<string> GetApiKeyAsync() => await _lazy.Value;
}
```

---

## Constructor Over-Injection

A class with too many constructor parameters (typically > 5) signals it has too many responsibilities.

### The Problem

```csharp
// Code smell: 8 dependencies = too many responsibilities
public sealed class OrderService(
    IOrderRepository orders,
    ICustomerRepository customers,
    IProductRepository products,
    IInventoryService inventory,
    IPaymentService payments,
    IShippingService shipping,
    INotificationService notifications,
    ILogger<OrderService> logger) { }
```

### Fixes

**Option 1: Facade service** — Group related dependencies:

```csharp
public sealed class OrderFulfillmentService(
    IPaymentService payments,
    IShippingService shipping,
    IInventoryService inventory) {

    public async Task FulfillAsync(Order order) { /* ... */ }
}

public sealed class OrderService(
    IOrderRepository orders,
    OrderFulfillmentService fulfillment,
    INotificationService notifications,
    ILogger<OrderService> logger) {

    // 4 dependencies instead of 8
}
```

**Option 2: Mediator/CQRS** — Commands handle single operations:

```csharp
public sealed class PlaceOrderHandler(
    IOrderRepository orders,
    IPaymentService payments) : IRequestHandler<PlaceOrderCommand, OrderResult> {

    // Each handler has minimal dependencies
}
```

---

## Scope Mismanagement

### Creating Scopes Without Disposing

```csharp
// WRONG: scope never disposed → services leak
public void DoWork() {
    var scope = _scopeFactory.CreateScope();
    var service = scope.ServiceProvider.GetRequiredService<IScopedService>();
    service.Execute();
    // scope is never disposed!
}

// CORRECT: always use using/await using
public async Task DoWorkAsync() {
    await using var scope = _scopeFactory.CreateAsyncScope();
    var service = scope.ServiceProvider.GetRequiredService<IScopedService>();
    await service.ExecuteAsync();
}
```

### Resolving Scoped from Root

```csharp
// WRONG: resolving scoped service from root provider
var app = builder.Build();
var dbContext = app.Services.GetRequiredService<AppDbContext>();
// This is a "root scope" resolution — the DbContext lives forever

// CORRECT: create a scope
await using var scope = app.Services.CreateAsyncScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
```

---

## Ambient Context / Static Service Locator

### The Problem

```csharp
// WRONG: global static service locator
public static class ServiceLocator {
    public static IServiceProvider Provider { get; set; } = null!;
}

// In startup:
ServiceLocator.Provider = app.Services;

// Anywhere in code:
var logger = ServiceLocator.Provider.GetRequiredService<ILogger<Foo>>();
```

Problems:
- Hides all dependencies
- Makes testing extremely difficult
- Creates implicit coupling to global state
- Race conditions in parallel tests

### Fix

Always use constructor injection. If you need DI in a place where constructor injection isn't available (e.g., static methods, entity classes), consider:

1. Pass the dependency as a method parameter
2. Use a domain event dispatcher pattern
3. Restructure to move logic into a service class that CAN use constructor injection
