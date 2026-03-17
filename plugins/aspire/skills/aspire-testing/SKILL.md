---
name: aspire-testing
description: |
  .NET Aspire integration testing expert using Aspire.Hosting.Testing as a full replacement for
  TestContainers. Covers DistributedApplicationTestingBuilder, creating and starting test application
  hosts, CreateHttpClient for HTTP resources, GetConnectionStringAsync for database resources,
  ResourceNotifications with WaitForResourceHealthyAsync and WaitForResourceAsync, health check
  waiting with timeouts, TUnit test fixtures with IAsyncInitializer and IAsyncDisposable,
  ClassDataSource sharing patterns (SharedType.Globally, SharedType.PerClass), Shouldly assertions,
  FakeItEasy mocking, environment variable overrides, logging configuration in tests, parallel test
  isolation via automatic port randomization, testing APIs with real databases, testing messaging
  with real queues, and closed-box integration testing where services run as separate processes.
  Use when writing aspire tests, setting up integration tests for distributed applications,
  replacing testcontainers with aspire, creating aspire test fixtures, testing with real databases,
  testing with real dependencies, using DistributedApplicationTestingBuilder, calling CreateHttpClient
  or WaitForResourceHealthyAsync or GetConnectionStringAsync or ResourceNotifications, debugging
  aspire test timeouts or port conflicts, or testing distributed application orchestration.
---

# .NET Aspire Integration Testing Expert

Comprehensive guidance for integration testing .NET Aspire applications using `Aspire.Hosting.Testing` with
TUnit, Shouldly, and FakeItEasy. Aspire testing is a direct replacement for TestContainers -- reuse your actual
AppHost definition instead of manually configuring containers.

## Why Aspire Testing Replaces TestContainers

| Concern                     | TestContainers                          | Aspire Testing                                  |
|-----------------------------|-----------------------------------------|-------------------------------------------------|
| Container definition        | Manual per-container setup in test code | Reuse existing AppHost orchestration            |
| Port management             | Manual port mapping and randomization   | Automatic randomization built in                |
| Readiness waiting           | Custom wait strategies per container    | `WaitForResourceHealthyAsync` for all resources |
| Service URLs                | Build connection strings manually       | `CreateHttpClient` / `GetConnectionStringAsync` |
| Multi-service orchestration | Wire services together in tests         | Already wired in AppHost                        |
| Environment parity          | Test config diverges from dev/prod      | Same orchestration graph everywhere             |

Aspire testing runs your real AppHost, which means the integration graph in tests matches development and
production exactly. There is no drift between how containers are configured in tests versus how they run in the
real application.

## Project Setup

Add the testing package and reference the AppHost project.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.Testing" />
    <PackageReference Include="TUnit" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="FakeItEasy" />
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\MyApp.AppHost\MyApp.AppHost.csproj" />
  </ItemGroup>
</Project>
```

The `ProjectReference` to the AppHost is required so `DistributedApplicationTestingBuilder` can discover the
`Projects.MyApp_AppHost` type. Use underscores in the type name where the project name contains dots or hyphens.

## TUnit Test Fixture

Create a reusable fixture that starts the entire distributed application once and shares it across tests. TUnit
uses `IAsyncInitializer` for async setup and `IAsyncDisposable` for teardown.

```csharp
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

public class AspireFixture : IAsyncInitializer, IAsyncDisposable
{
    public DistributedApplication App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.MyApp_AppHost>();

        appHost.Services.ConfigureHttpClientDefaults(http =>
            http.AddStandardResilienceHandler());

        App = await appHost.BuildAsync();
        await App.StartAsync();

        // Wait for all critical resources before any test runs
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await App.ResourceNotifications
            .WaitForResourceHealthyAsync("apiservice", cts.Token);
        await App.ResourceNotifications
            .WaitForResourceHealthyAsync("postgres", cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
    }
}
```

Key points:
- `DistributedApplicationTestingBuilder.CreateAsync<T>()` creates a builder from your real AppHost
- The dashboard is disabled by default in tests
- Ports are randomized automatically for safe parallel execution
- `ConfigureHttpClientDefaults` with the standard resilience handler adds retries to all `HttpClient` instances,
  making tests more stable against startup timing
- Always wait for resources to be healthy before returning from `InitializeAsync`

## Using the Fixture in TUnit Tests

TUnit injects shared fixtures through `[ClassDataSource<T>]` on each test method. Use `SharedType.Globally` to
share a single fixture instance across all test classes, avoiding repeated AppHost startup.

```csharp
using System.Net;
using Aspire.Hosting.Testing;
using Shouldly;

public class ApiServiceTests
{
    [Test]
    [ClassDataSource<AspireFixture>(Shared = SharedType.Globally)]
    public async Task GetWeatherForecast_ReturnsOk(AspireFixture fixture)
    {
        using var client = fixture.App.CreateHttpClient("apiservice");

        var response = await client.GetAsync("/weatherforecast");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    [ClassDataSource<AspireFixture>(Shared = SharedType.Globally)]
    public async Task GetWeatherForecast_ReturnsJsonArray(AspireFixture fixture)
    {
        using var client = fixture.App.CreateHttpClient("apiservice");

        var forecasts = await client.GetFromJsonAsync<JsonElement[]>("/weatherforecast");

        forecasts.ShouldNotBeNull();
        forecasts.Length.ShouldBeGreaterThan(0);
    }
}
```

### Sharing Strategies

| Strategy         | Attribute             | Behavior                                        |
|------------------|-----------------------|-------------------------------------------------|
| Global singleton | `SharedType.Globally` | One fixture for the entire test run             |
| Per-class        | `SharedType.PerClass` | One fixture per test class                      |
| Per-test         | `SharedType.None`     | New fixture per test (expensive, rarely needed) |

Use `SharedType.Globally` for Aspire fixtures. Starting a distributed application is expensive and the
randomized ports ensure test isolation without needing separate instances.

## Accessing HTTP Resources

Use `CreateHttpClient` to get an `HttpClient` preconfigured with the correct base address for any HTTP resource
in the AppHost.

```csharp
// Default endpoint
using var client = fixture.App.CreateHttpClient("apiservice");

// Named endpoint (when a resource exposes multiple endpoints)
using var client = fixture.App.CreateHttpClient("apiservice", "internal");
```

The returned `HttpClient` has its `BaseAddress` set to the randomized URL assigned to that resource. Use relative
paths for all requests.

## Accessing Database Resources

For non-HTTP resources like databases, use `GetConnectionStringAsync` to retrieve the connection string with the
randomized port.

```csharp
var connectionString = await fixture.App
    .GetConnectionStringAsync("postgres");

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

var result = await connection.ExecuteScalarAsync<int>(
    "SELECT COUNT(*) FROM orders");
result.ShouldBeGreaterThan(0);
```

This works for any resource that exposes a connection string: PostgreSQL, SQL Server, Redis, MongoDB, RabbitMQ,
and others.

## Health Check Waiting

### WaitForResourceHealthyAsync

Wait for a resource to pass its health checks. Always use a cancellation token with a timeout to avoid hanging
tests.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
await app.ResourceNotifications
    .WaitForResourceHealthyAsync("postgres", cts.Token);
```

This waits until the resource reports a healthy state through the Aspire health check system. Databases, message
brokers, and other infrastructure resources all report health through this mechanism.

### WaitForResourceAsync

Wait for a resource to reach a specific state. Use this for finer-grained control.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

// Wait for running state (not necessarily healthy)
await app.ResourceNotifications
    .WaitForResourceAsync("worker", KnownResourceStates.Running, cts.Token);

// Wait for finished state (one-shot resources like database migrations)
await app.ResourceNotifications
    .WaitForResourceAsync("db-migrations", KnownResourceStates.Finished, cts.Token);
```

### Recommended Timeouts

| Resource Type           | Suggested Timeout | Reason                   |
|-------------------------|-------------------|--------------------------|
| API service             | 30 seconds        | Fast startup             |
| PostgreSQL / SQL Server | 60 seconds        | Container pull + init    |
| RabbitMQ / Kafka        | 90 seconds        | Cluster initialization   |
| First run (cold pull)   | 120+ seconds      | Container image download |

## Environment Variable Overrides

Override environment variables on resources during test setup to control behavior.

```csharp
var appHost = await DistributedApplicationTestingBuilder
    .CreateAsync<Projects.MyApp_AppHost>();

// Add or override configuration values before building
appHost.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["FeatureFlags:EnableNewCheckout"] = "true",
        ["RateLimiting:MaxRequests"] = "1000"
    })
    .Build());

var app = await appHost.BuildAsync();
```

For disabling port randomization when debugging:

```csharp
var appHost = await DistributedApplicationTestingBuilder
    .CreateAsync<Projects.MyApp_AppHost>(args: ["DcpPublisher:RandomizePorts=false"]);
```

## Logging Configuration

Configure test logging to capture output from the distributed application.

```csharp
var appHost = await DistributedApplicationTestingBuilder
    .CreateAsync<Projects.MyApp_AppHost>();

appHost.Services.AddLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Debug);
    logging.AddFilter("Aspire", LogLevel.Trace);
    logging.AddFilter("Microsoft.Hosting", LogLevel.Debug);
});
```

Add `logging.AddConsole()` if running locally. In CI, the default providers are usually sufficient.

## Common Test Patterns

### Test API Endpoint Returns Expected Data

```csharp
[Test]
[ClassDataSource<AspireFixture>(Shared = SharedType.Globally)]
public async Task CreateOrder_ReturnsCreated(AspireFixture fixture)
{
    using var client = fixture.App.CreateHttpClient("apiservice");
    var order = new { ProductId = "prod-1", Quantity = 5 };

    var response = await client.PostAsJsonAsync("/orders", order);

    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    var created = await response.Content.ReadFromJsonAsync<OrderResponse>();
    created.ShouldNotBeNull();
    created.ProductId.ShouldBe("prod-1");
}
```

### Test Database Operations Through the API

```csharp
[Test]
[ClassDataSource<AspireFixture>(Shared = SharedType.Globally)]
public async Task CreateAndRetrieveOrder_RoundTrips(AspireFixture fixture)
{
    using var client = fixture.App.CreateHttpClient("apiservice");
    var order = new { ProductId = "prod-2", Quantity = 3 };

    var createResponse = await client.PostAsJsonAsync("/orders", order);
    var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

    var getResponse = await client.GetAsync($"/orders/{created!.Id}");
    var retrieved = await getResponse.Content.ReadFromJsonAsync<OrderResponse>();

    retrieved.ShouldNotBeNull();
    retrieved.Id.ShouldBe(created.Id);
    retrieved.ProductId.ShouldBe("prod-2");
}
```

### Test Multiple Services Communicating

```csharp
[Test]
[ClassDataSource<AspireFixture>(Shared = SharedType.Globally)]
public async Task OrderCreated_NotificationServiceProcesses(AspireFixture fixture)
{
    using var orderClient = fixture.App.CreateHttpClient("orderservice");
    using var notificationClient = fixture.App.CreateHttpClient("notificationservice");

    await orderClient.PostAsJsonAsync("/orders", new { ProductId = "p1", Quantity = 1 });

    // Allow async processing time
    await Task.Delay(TimeSpan.FromSeconds(2));

    var notifications = await notificationClient
        .GetFromJsonAsync<JsonElement[]>("/notifications?type=order-created");
    notifications.ShouldNotBeNull();
    notifications.Length.ShouldBeGreaterThanOrEqualTo(1);
}
```

## Troubleshooting

| Symptom                                             | Cause                                | Fix                                                                                                                |
|-----------------------------------------------------|--------------------------------------|--------------------------------------------------------------------------------------------------------------------|
| `TimeoutException` on `WaitForResourceHealthyAsync` | Container slow to start or unhealthy | Increase timeout, check container logs, verify health check endpoint                                               |
| `SocketException` / connection refused              | Test ran before resource was ready   | Add `WaitForResourceHealthyAsync` before test execution                                                            |
| Port conflicts in CI                                | Multiple test runs on same machine   | Ensure `DcpPublisher:RandomizePorts` is not set to `false`                                                         |
| `InvalidOperationException` on `CreateAsync`        | AppHost project not referenced       | Add `ProjectReference` to the AppHost project                                                                      |
| Container not found                                 | Container runtime not running        | Start Docker Desktop or Podman before running tests                                                                |
| Flaky tests with real databases                     | Shared mutable state between tests   | Use Respawn or transaction rollback between tests (see [references/test-patterns.md](references/test-patterns.md)) |

## Advanced Patterns

See [references/test-patterns.md](references/test-patterns.md) for:
- Complete TUnit fixture with all features
- Database cleanup with Respawn between tests
- Testing with real message queues (RabbitMQ)
- Testing email with Mailpit
- Parallel test isolation strategies
- Environment variable override patterns
- TestContainers vs Aspire Testing comparison table

## Learn More

| Topic                      | How to Find                                                                                               |
|----------------------------|-----------------------------------------------------------------------------------------------------------|
| Aspire testing overview    | `microsoft_docs_search(query=".NET Aspire integration testing DistributedApplicationTestingBuilder")`     |
| Aspire health checks       | `microsoft_docs_search(query=".NET Aspire resource health checks WaitForResourceHealthyAsync")`           |
| TUnit documentation        | `resolve-library-id(libraryName="TUnit")` then `query-docs`                                               |
| Shouldly assertions        | `resolve-library-id(libraryName="Shouldly")` then `query-docs`                                            |
| Aspire.Hosting.Testing API | `microsoft_docs_fetch(url="https://learn.microsoft.com/en-us/dotnet/aspire/testing/integration-testing")` |
