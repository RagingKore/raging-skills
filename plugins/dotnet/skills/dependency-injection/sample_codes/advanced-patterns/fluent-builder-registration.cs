// Complex fluent builder pattern for service registration
// Demonstrates how libraries expose configurable DI setup

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Fluent builder usage — clean, discoverable API
builder.Services.AddMessaging(builder.Configuration, messaging => {
    messaging
        .UseRabbitMq()
        .WithRetry(maxRetries: 5, delay: TimeSpan.FromSeconds(2))
        .WithDeadLetterQueue()
        .AddConsumer<OrderCreatedConsumer>()
        .AddConsumer<PaymentReceivedConsumer>()
        .AddConsumer<InventoryUpdatedConsumer>();
});

builder.Services.AddCaching(builder.Configuration, caching => {
    caching
        .UseRedis()
        .WithKeyPrefix("myapp:")
        .WithDefaultExpiry(TimeSpan.FromMinutes(10))
        .EnableCompression();
});

var app = builder.Build();
app.Run();

// ===== Messaging Builder =====

public sealed class MessagingBuilder {
    internal readonly IServiceCollection Services;
    internal readonly IConfiguration Configuration;
    internal string Provider = "in-memory";
    internal bool RetryEnabled;
    internal int MaxRetries = 3;
    internal TimeSpan RetryDelay = TimeSpan.FromSeconds(1);
    internal bool DeadLetterEnabled;
    internal readonly List<Type> ConsumerTypes = [];

    internal MessagingBuilder(IServiceCollection services, IConfiguration config) {
        Services = services;
        Configuration = config;
    }

    public MessagingBuilder UseRabbitMq() { Provider = "rabbitmq"; return this; }
    public MessagingBuilder UseAzureServiceBus() { Provider = "azure"; return this; }
    public MessagingBuilder UseInMemory() { Provider = "in-memory"; return this; }

    public MessagingBuilder WithRetry(int maxRetries = 3, TimeSpan? delay = null) {
        RetryEnabled = true;
        MaxRetries = maxRetries;
        RetryDelay = delay ?? TimeSpan.FromSeconds(1);
        return this;
    }

    public MessagingBuilder WithDeadLetterQueue() {
        DeadLetterEnabled = true;
        return this;
    }

    public MessagingBuilder AddConsumer<TConsumer>()
        where TConsumer : class, IMessageConsumer {
        ConsumerTypes.Add(typeof(TConsumer));
        Services.AddScoped<TConsumer>();
        Services.AddScoped<IMessageConsumer>(sp =>
            sp.GetRequiredService<TConsumer>());
        return this;
    }

    internal void Build() {
        // Register options
        Services.AddOptionsWithValidateOnStart<MessagingOptions>()
            .Bind(Configuration.GetSection("Messaging"))
            .ValidateDataAnnotations()
            .Configure(opts => {
                opts.Provider = Provider;
                opts.MaxRetries = RetryEnabled ? MaxRetries : 0;
                opts.RetryDelay = RetryDelay;
                opts.EnableDeadLetterQueue = DeadLetterEnabled;
            });

        // Register transport
        Services.AddSingleton<IMessageBus>(Provider switch {
            "rabbitmq" => sp => ActivatorUtilities
                .CreateInstance<RabbitMqMessageBus>(sp),
            "azure" => sp => ActivatorUtilities
                .CreateInstance<AzureServiceBusMessageBus>(sp),
            _ => sp => new InMemoryMessageBus()
        });

        // Wrap with retry decorator if enabled
        if (RetryEnabled) {
            var original = Services.Last(d =>
                d.ServiceType == typeof(IMessageBus));
            Services.Remove(original);

            Services.AddSingleton<IMessageBus>(sp => {
                var inner = original.ImplementationFactory!(sp) as IMessageBus
                    ?? throw new InvalidOperationException("Failed to create inner bus");
                return new RetryMessageBusDecorator(inner, MaxRetries, RetryDelay,
                    sp.GetRequiredService<ILogger<RetryMessageBusDecorator>>());
            });
        }

        // Register consumer host
        if (ConsumerTypes.Count > 0) {
            Services.AddHostedService<MessageConsumerHostedService>();
        }
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

// ===== Caching Builder =====

public sealed class CachingBuilder {
    internal readonly IServiceCollection Services;
    internal readonly IConfiguration Configuration;
    internal string Provider = "memory";
    internal string KeyPrefix = "";
    internal TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);
    internal bool CompressionEnabled;

    internal CachingBuilder(IServiceCollection services, IConfiguration config) {
        Services = services;
        Configuration = config;
    }

    public CachingBuilder UseRedis() { Provider = "redis"; return this; }
    public CachingBuilder UseMemory() { Provider = "memory"; return this; }

    public CachingBuilder WithKeyPrefix(string prefix) {
        KeyPrefix = prefix;
        return this;
    }

    public CachingBuilder WithDefaultExpiry(TimeSpan expiry) {
        DefaultExpiry = expiry;
        return this;
    }

    public CachingBuilder EnableCompression() {
        CompressionEnabled = true;
        return this;
    }

    internal void Build() {
        Services.AddOptionsWithValidateOnStart<CacheOptions>()
            .Bind(Configuration.GetSection("Cache"))
            .ValidateDataAnnotations()
            .Configure(opts => {
                opts.Provider = Provider;
                opts.KeyPrefix = KeyPrefix;
                opts.DefaultExpiry = DefaultExpiry;
                opts.EnableCompression = CompressionEnabled;
            });

        switch (Provider) {
            case "redis":
                Services.AddStackExchangeRedisCache(options => {
                    options.Configuration = Configuration
                        .GetConnectionString("Redis");
                    options.InstanceName = KeyPrefix;
                });
                Services.AddSingleton<ICacheService, RedisCacheService>();
                break;

            default:
                Services.AddDistributedMemoryCache();
                Services.AddSingleton<ICacheService, MemoryCacheService>();
                break;
        }

        if (CompressionEnabled) {
            // Decorate with compression
            var original = Services.Last(d =>
                d.ServiceType == typeof(ICacheService));
            Services.Remove(original);

            Services.AddSingleton<ICacheService>(sp => {
                var inner = (ICacheService)original.ImplementationFactory!(sp);
                return new CompressingCacheDecorator(inner);
            });
        }
    }
}

public static class CachingExtensions {
    public static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<CachingBuilder> configure) {

        var builder = new CachingBuilder(services, configuration);
        configure(builder);
        builder.Build();
        return services;
    }
}

// --- Options ---

public sealed class MessagingOptions {
    [Required]
    public string Provider { get; set; } = "in-memory";
    public int MaxRetries { get; set; }
    public TimeSpan RetryDelay { get; set; }
    public bool EnableDeadLetterQueue { get; set; }
}

public sealed class CacheOptions {
    [Required]
    public string Provider { get; set; } = "memory";
    public string KeyPrefix { get; set; } = "";
    public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableCompression { get; set; }
}
