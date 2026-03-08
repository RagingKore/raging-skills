// Compile-time options validation using source generator
// Generates IValidateOptions<T> from DataAnnotations at build time
// AOT and trim-safe — no runtime reflection for validation

// .csproj:
// <PropertyGroup>
//     <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
// </PropertyGroup>

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Source generator intercepts ValidateDataAnnotations() and generates code
builder.Services
    .AddOptionsWithValidateOnStart<CacheOptions>()
    .Bind(builder.Configuration.GetSection("Cache"))
    .ValidateDataAnnotations();

builder.Services
    .AddOptionsWithValidateOnStart<MessagingOptions>()
    .Bind(builder.Configuration.GetSection("Messaging"))
    .ValidateDataAnnotations();

var app = builder.Build();

app.MapGet("/", (IOptions<CacheOptions> cache, IOptions<MessagingOptions> msg) =>
    new { Cache = cache.Value, Messaging = msg.Value });

app.Run();

// --- Options classes with DataAnnotations ---
// The source generator reads these attributes and creates validation code

public sealed class CacheOptions {
    [Required(ErrorMessage = "Cache provider is required (Redis, Memory, etc.)")]
    public required string Provider { get; set; }

    [Required, MinLength(1)]
    public required string ConnectionString { get; set; }

    [Range(1, 3600, ErrorMessage = "DefaultTtlSeconds must be 1-3600")]
    public int DefaultTtlSeconds { get; set; } = 300;

    [Range(1, 10_000)]
    public int MaxItems { get; set; } = 1_000;

    [AllowedValues("LRU", "LFU", "FIFO", ErrorMessage = "Invalid eviction policy")]
    public string EvictionPolicy { get; set; } = "LRU";

    // Recursive validation into nested object (source-gen aware)
    [ValidateObjectMembers]
    public CacheSerializationOptions Serialization { get; set; } = new();
}

public sealed class CacheSerializationOptions {
    [AllowedValues("JSON", "MessagePack", "Protobuf")]
    public string Format { get; set; } = "JSON";

    public bool CompressValues { get; set; }

    [Range(0, 9, ErrorMessage = "CompressionLevel must be 0-9")]
    public int CompressionLevel { get; set; } = 6;
}

public sealed class MessagingOptions {
    [Required, Url]
    public required string BrokerUrl { get; set; }

    [Required, MinLength(1)]
    public required string TopicPrefix { get; set; }

    [Range(1, 100)]
    public int MaxConcurrentConsumers { get; set; } = 4;

    [Range(1, 300)]
    public int AckTimeoutSeconds { get; set; } = 30;

    // Validate each item in the list (source-gen aware)
    [ValidateEnumeratedItems]
    public List<SubscriptionOptions> Subscriptions { get; set; } = [];
}

public sealed class SubscriptionOptions {
    [Required, RegularExpression(@"^[a-z][a-z0-9\.\-]*$",
        ErrorMessage = "Topic must be lowercase alphanumeric with dots/hyphens")]
    public string Topic { get; set; } = string.Empty;

    [Range(1, 50)]
    public int Concurrency { get; set; } = 1;

    public bool DeadLetterEnabled { get; set; } = true;
}

// To see generated code, add to .csproj:
// <PropertyGroup>
//     <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
// </PropertyGroup>
// Then check: obj/Debug/net9.0/generated/
