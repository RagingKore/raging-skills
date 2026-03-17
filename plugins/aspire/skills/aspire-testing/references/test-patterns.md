# Aspire Testing Advanced Patterns

Reference patterns for integration testing .NET Aspire applications with TUnit, Shouldly, and FakeItEasy.
All examples target Aspire 13.x / .NET 10.

## Complete TUnit Fixture Implementation

Full-featured fixture with health check waiting, logging, resilience, and cancellation support.

```csharp
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;

public class AspireFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromMinutes(3));

    public DistributedApplication App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.MyApp_AppHost>(cancellationToken: _cts.Token);

        // Add resilience to all HTTP clients created through the app
        appHost.Services.ConfigureHttpClientDefaults(http =>
            http.AddStandardResilienceHandler());

        // Configure test logging
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddFilter("Aspire", LogLevel.Warning);
            logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
        });

        App = await appHost.BuildAsync(_cts.Token);
        await App.StartAsync(_cts.Token);

        // Wait for all infrastructure resources
        await App.ResourceNotifications
            .WaitForResourceHealthyAsync("postgres", _cts.Token);
        await App.ResourceNotifications
            .WaitForResourceHealthyAsync("rabbitmq", _cts.Token);

        // Wait for application resources
        await App.ResourceNotifications
            .WaitForResourceHealthyAsync("apiservice", _cts.Token);
        await App.ResourceNotifications
            .WaitForResourceHealthyAsync("worker", _cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Dispose();
        await App.DisposeAsync();
    }
}
```

## Database Cleanup with Respawn

Use [Respawn](https://github.com/jbogard/Respawn) to reset the database between tests without recreating the
schema. This avoids shared mutable state while keeping tests fast.

### Fixture with Respawn Support

```csharp
using Aspire.Hosting.Testing;
using Npgsql;
using Respawn;

public class AspireFixture : IAsyncInitializer, IAsyncDisposable
{
    public DistributedApplication App { get; private set; } = null!;
    public string PostgresConnectionString { get; private set; } = null!;

    private Respawner _respawner = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.MyApp_AppHost>();

        appHost.Services.ConfigureHttpClientDefaults(http =>
            http.AddStandardResilienceHandler());

        App = await appHost.BuildAsync();
        await App.StartAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await App.ResourceNotifications
            .WaitForResourceHealthyAsync("postgres", cts.Token);
        await App.ResourceNotifications
            .WaitForResourceHealthyAsync("apiservice", cts.Token);

        // Store connection string for direct database access
        PostgresConnectionString = await App.GetConnectionStringAsync("postgres")
            ?? throw new InvalidOperationException("postgres connection string not found");

        // Initialize Respawner after the app has started and migrations have run
        await using var connection = new NpgsqlConnection(PostgresConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    /// <summary>
    /// Reset the database to a clean state. Call this in test setup or between tests.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(PostgresConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
    }
}
```

### Using Respawn in Tests

```csharp
public class OrderTests
{
    [Test]
    [ClassDataSource<AspireFixture>(Shared = SharedType.Globally)]
    public async Task CreateOrder_PersistsToDatabase(AspireFixture fixture)
    {
        // Reset database before test for isolation
        await fixture.ResetDatabaseAsync();

        using var client = fixture.App.CreateHttpClient("apiservice");
        var order = new { ProductId = "prod-1", Quantity = 5 };

        var response = await client.PostAsJsonAsync("/orders", order);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Verify directly in the database
        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM orders WHERE product_id = 'prod-1'");
        count.ShouldBe(1);
    }
}
```

## Database Seeding in Tests

Seed reference data after Respawn cleanup for tests that depend on lookup tables or initial state.

```csharp
public class AspireFixture : IAsyncInitializer, IAsyncDisposable
{
    // ... (fields and InitializeAsync as above)

    public async Task ResetAndSeedAsync()
    {
        await ResetDatabaseAsync();
        await SeedReferenceDataAsync();
    }

    private async Task SeedReferenceDataAsync()
    {
        await using var connection = new NpgsqlConnection(PostgresConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync("""
            INSERT INTO product_categories (id, name) VALUES
                ('cat-1', 'Electronics'),
                ('cat-2', 'Books'),
                ('cat-3', 'Clothing')
            ON CONFLICT (id) DO NOTHING;
            """);
    }

    // ... DisposeAsync
}
```

## Testing with Real Message Queues (RabbitMQ)

Test asynchronous messaging through the actual AppHost RabbitMQ resource.

### Pattern: Publish and Verify Consumer Processed

```csharp
public class MessagingTests
{
    [Test]
    [ClassDataSource<AspireFixture>(Shared = SharedType.Globally)]
    public async Task OrderPlaced_ConsumerCreatesInvoice(AspireFixture fixture)
    {
        await fixture.ResetDatabaseAsync();

        using var orderClient = fixture.App.CreateHttpClient("orderservice");

        // Place an order (triggers message publish to RabbitMQ)
        var response = await orderClient.PostAsJsonAsync("/orders", new
        {
            CustomerId = "cust-1",
            ProductId = "prod-1",
            Quantity = 2
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        // Poll for the async side effect (invoice created by consumer)
        using var invoiceClient = fixture.App.CreateHttpClient("invoiceservice");
        var invoice = await PollForResultAsync(async () =>
        {
            var invoiceResponse = await invoiceClient
                .GetAsync($"/invoices?orderId={order!.Id}");
            if (!invoiceResponse.IsSuccessStatusCode) return null;

            var invoices = await invoiceResponse.Content
                .ReadFromJsonAsync<JsonElement[]>();
            return invoices?.Length > 0 ? invoices[0] : null;
        }, timeout: TimeSpan.FromSeconds(10));

        invoice.ShouldNotBeNull();
        invoice.Value.GetProperty("orderId").GetString().ShouldBe(order!.Id);
    }

    private static async Task<T?> PollForResultAsync<T>(
        Func<Task<T?>> action,
        TimeSpan timeout,
        TimeSpan? interval = null)
    {
        interval ??= TimeSpan.FromMilliseconds(500);
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.Token.IsCancellationRequested)
        {
            var result = await action();
            if (result is not null) return result;
            await Task.Delay(interval.Value, cts.Token);
        }

        return default;
    }
}
```

### Pattern: Verify Message Published to Queue

When you need to verify the message itself rather than the consumer side effect, connect directly to RabbitMQ.

```csharp
[Test]
[ClassDataSource<AspireFixture>(Shared = SharedType.Globally)]
public async Task OrderPlaced_PublishesEventToQueue(AspireFixture fixture)
{
    var rabbitConnectionString = await fixture.App
        .GetConnectionStringAsync("rabbitmq");

    var factory = new ConnectionFactory { Uri = new Uri(rabbitConnectionString!) };
    await using var connection = await factory.CreateConnectionAsync();
    await using var channel = await connection.CreateChannelAsync();

    // Declare a temporary queue bound to the exchange
    var queue = await channel.QueueDeclareAsync(exclusive: true);
    await channel.QueueBindAsync(queue.QueueName, "orders-exchange", "order.placed");

    // Trigger the publish
    using var client = fixture.App.CreateHttpClient("orderservice");
    await client.PostAsJsonAsync("/orders", new { ProductId = "p1", Quantity = 1 });

    // Consume the message
    var tcs = new TaskCompletionSource<byte[]>();
    var consumer = new AsyncEventingBasicConsumer(channel);
    consumer.ReceivedAsync += (_, ea) =>
    {
        tcs.TrySetResult(ea.Body.ToArray());
        return Task.CompletedTask;
    };
    await channel.BasicConsumeAsync(queue.QueueName, autoAck: true, consumer: consumer);

    var body = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    var message = JsonSerializer.Deserialize<JsonElement>(body);
    message.GetProperty("productId").GetString().ShouldBe("p1");
}
```

## Testing Email with Mailpit

When the AppHost includes a Mailpit resource for email testing, verify emails through the Mailpit API.

```csharp
[Test]
[ClassDataSource<AspireFixture>(Shared = SharedType.Globally)]
public async Task PasswordReset_SendsEmail(AspireFixture fixture)
{
    using var apiClient = fixture.App.CreateHttpClient("apiservice");
    using var mailpitClient = fixture.App.CreateHttpClient("mailpit");

    // Trigger password reset
    await apiClient.PostAsJsonAsync("/auth/password-reset", new
    {
        Email = "user@example.com"
    });

    // Allow async email delivery
    await Task.Delay(TimeSpan.FromSeconds(2));

    // Verify via Mailpit API
    var messages = await mailpitClient
        .GetFromJsonAsync<JsonElement>("/api/v1/messages");

    var messageCount = messages.GetProperty("messages_count").GetInt32();
    messageCount.ShouldBeGreaterThanOrEqualTo(1);

    var firstMessage = messages.GetProperty("messages")[0];
    firstMessage.GetProperty("To")[0]
        .GetProperty("Address").GetString()
        .ShouldBe("user@example.com");
    firstMessage.GetProperty("Subject").GetString()
        .ShouldContain("Password Reset");
}
```

## Environment Variable Override Patterns

### Override at Builder Level

Pass arguments to `CreateAsync` to override configuration values.

```csharp
var appHost = await DistributedApplicationTestingBuilder
    .CreateAsync<Projects.MyApp_AppHost>(args:
    [
        "DcpPublisher:RandomizePorts=false",       // Disable port randomization (debugging)
        "Parameters:DbPassword=test-password",      // Override a parameter
        "ASPNETCORE_ENVIRONMENT=Testing"             // Set environment
    ]);
```

### Override Service Configuration

Use the builder's `Services` to override configuration after creation.

```csharp
var appHost = await DistributedApplicationTestingBuilder
    .CreateAsync<Projects.MyApp_AppHost>();

// Add or override configuration
appHost.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["FeatureFlags:EnableNewCheckout"] = "true",
        ["RateLimiting:MaxRequests"] = "1000"
    })
    .Build());

var app = await appHost.BuildAsync();
```

## Parallel Test Isolation

Aspire testing provides built-in isolation through port randomization. Each test run gets unique ports, so
parallel test execution in CI is safe without additional configuration.

### When Tests Mutate Shared State

For tests that modify shared state (database rows, queue messages), use one of these strategies:

| Strategy             | Pros                       | Cons                                | When to Use                   |
|----------------------|----------------------------|-------------------------------------|-------------------------------|
| Respawn              | Fast, preserves schema     | Requires direct DB access           | Most database tests           |
| Transaction rollback | No cleanup needed          | Not always possible with HTTP calls | Unit-of-work tests            |
| Unique identifiers   | No cleanup, fully parallel | Queries must filter by test ID      | Read-heavy tests              |
| Per-class fixture    | Full isolation             | Slow (multiple AppHost starts)      | Conflicting test requirements |

### Unique Identifier Pattern

Generate unique keys per test to avoid collisions without database cleanup.

```csharp
[Test]
[ClassDataSource<AspireFixture>(Shared = SharedType.Globally)]
public async Task GetOrder_ReturnsCorrectOrder(AspireFixture fixture)
{
    var testId = Guid.NewGuid().ToString("N")[..8];
    using var client = fixture.App.CreateHttpClient("apiservice");

    // Create with unique identifier
    var response = await client.PostAsJsonAsync("/orders", new
    {
        ProductId = $"prod-{testId}",
        Quantity = 1
    });
    var created = await response.Content.ReadFromJsonAsync<OrderResponse>();

    // Retrieve and verify
    var getResponse = await client.GetAsync($"/orders/{created!.Id}");
    var order = await getResponse.Content.ReadFromJsonAsync<OrderResponse>();

    order.ShouldNotBeNull();
    order.ProductId.ShouldBe($"prod-{testId}");
}
```

## Custom Health Check Waiting

### Wait for Multiple Resources in Parallel

```csharp
public async Task InitializeAsync()
{
    // ... build and start app ...

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

    // Wait for all resources concurrently
    await Task.WhenAll(
        App.ResourceNotifications
            .WaitForResourceHealthyAsync("postgres", cts.Token),
        App.ResourceNotifications
            .WaitForResourceHealthyAsync("redis", cts.Token),
        App.ResourceNotifications
            .WaitForResourceHealthyAsync("rabbitmq", cts.Token),
        App.ResourceNotifications
            .WaitForResourceHealthyAsync("apiservice", cts.Token)
    );
}
```

### Wait for One-Shot Resources (Migrations, Seeders)

```csharp
// Wait for database migration container to finish
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
await App.ResourceNotifications
    .WaitForResourceAsync("db-migrations", KnownResourceStates.Finished, cts.Token);

// Then wait for the API that depends on it
await App.ResourceNotifications
    .WaitForResourceHealthyAsync("apiservice", cts.Token);
```

## Using FakeItEasy for Selective Mocking

Aspire integration tests are closed-box by design -- services run in separate processes. FakeItEasy is useful
for mocking external dependencies that are not part of the AppHost, such as third-party APIs or services you do
not want to call in tests.

### Mock an External API Client in the Test Process

```csharp
[Test]
[ClassDataSource<AspireFixture>(Shared = SharedType.Globally)]
public async Task ProcessPayment_WhenGatewayUnavailable_ReturnsServiceUnavailable(
    AspireFixture fixture)
{
    // FakeItEasy is used for test-process-level concerns, not for
    // replacing services inside the Aspire-hosted processes.
    // For example, mocking an HTTP response from an external gateway
    // that the test itself calls:

    var fakeHandler = A.Fake<HttpMessageHandler>();
    A.CallTo(fakeHandler)
        .Where(call => call.Method.Name == "SendAsync")
        .WithReturnType<Task<HttpResponseMessage>>()
        .Returns(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

    using var client = new HttpClient(fakeHandler)
    {
        BaseAddress = new Uri("https://payment-gateway.example.com")
    };

    var response = await client.PostAsJsonAsync("/charge", new { Amount = 100 });
    response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
}
```

## TestContainers vs Aspire Testing: Feature Comparison

| Feature                         | TestContainers                                               | Aspire Testing                           |
|---------------------------------|--------------------------------------------------------------|------------------------------------------|
| **Setup**                       | NuGet per container type (`Testcontainers.PostgreSql`, etc.) | Single package: `Aspire.Hosting.Testing` |
| **Container definition**        | Manual in test code                                          | Reuse AppHost definition                 |
| **Multi-service orchestration** | Wire manually (networks, env vars)                           | Built into AppHost graph                 |
| **Port management**             | `GetMappedPublicPort()` per container                        | Automatic randomization                  |
| **Readiness checks**            | `IWaitStrategy` per container                                | `WaitForResourceHealthyAsync`            |
| **HTTP client creation**        | Manual `new HttpClient { BaseAddress = ... }`                | `app.CreateHttpClient("name")`           |
| **Connection strings**          | Build from container properties                              | `app.GetConnectionStringAsync("name")`   |
| **Service discovery**           | Not supported                                                | Built-in (same as dev/prod)              |
| **Environment parity**          | Diverges from dev/prod config                                | Same orchestration everywhere            |
| **Dashboard**                   | Not available                                                | Available (disabled by default in tests) |
| **Startup time**                | Fast per container                                           | Slightly slower (full AppHost)           |
| **Granularity**                 | Individual containers                                        | Full application graph                   |
| **Framework support**           | xUnit, NUnit, MSTest, TUnit                                  | xUnit, NUnit, MSTest, TUnit              |
| **When to prefer**              | Testing a single container integration                       | Testing the full distributed application |

### Migration from TestContainers to Aspire Testing

```csharp
// BEFORE: xUnit + TestContainers
public class OrderTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("orders")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // Manually configure connection, run migrations, etc.
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task CreateOrder_Works()
    {
        var connectionString = _postgres.GetConnectionString();
        // ... set up DbContext, HTTP client manually ...
    }
}

// AFTER: Aspire Testing with TUnit
public class OrderTests
{
    [Test]
    [ClassDataSource<AspireFixture>(Shared = SharedType.Globally)]
    public async Task CreateOrder_Works(AspireFixture fixture)
    {
        // AppHost already has postgres + apiservice + migrations
        using var client = fixture.App.CreateHttpClient("apiservice");

        var response = await client.PostAsJsonAsync("/orders", new
        {
            ProductId = "prod-1",
            Quantity = 3
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
```

## Tips for CI/CD

- **Container runtime**: Ensure Docker or Podman is available in the CI runner
- **Image caching**: Cache container images between CI runs to avoid cold-pull delays
- **Timeouts**: Use longer timeouts in CI (2-3 minutes) than local development (30-60 seconds)
- **Parallel test runs**: Safe by default thanks to port randomization; no extra configuration needed
- **Resource limits**: Aspire tests start real containers; allocate sufficient CPU and memory to CI runners
- **Health check retries**: The standard resilience handler on `HttpClient` covers transient startup failures
