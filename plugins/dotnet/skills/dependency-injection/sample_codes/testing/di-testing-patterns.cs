// Testing patterns for services that use dependency injection
// Demonstrates constructor injection testability, test containers, and WebApplicationFactory

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// ===== Unit Testing: Constructor injection makes testing trivial =====

// The service under test — all dependencies injected via constructor
public sealed class OrderService(
    IOrderRepository repository,
    IOrderValidator validator,
    IOptions<OrderOptions> options,
    ILogger<OrderService> logger) : IOrderService {

    public async Task<OrderResult> CreateAsync(
        CreateOrderRequest request, CancellationToken ct) {

        var order = Order.From(request);
        var validation = validator.Validate(order);

        if (!validation.IsValid) {
            return OrderResult.Failure(validation.Errors);
        }

        if (order.Items.Count > options.Value.MaxItemsPerOrder) {
            return OrderResult.Failure(
                [$"Cannot exceed {options.Value.MaxItemsPerOrder} items"]);
        }

        await repository.SaveAsync(order, ct);
        logger.LogInformation("Order {OrderId} created", order.Id);

        return OrderResult.Success(order.Id);
    }
}

// --- Unit test (using NSubstitute/Moq — framework agnostic) ---

// [Test]
// public async Task CreateAsync_ValidOrder_SavesAndReturnsSuccess() {
//     // Arrange — create fakes for each dependency
//     var repository = Substitute.For<IOrderRepository>();
//     var validator = Substitute.For<IOrderValidator>();
//     validator.Validate(Arg.Any<Order>())
//         .Returns(ValidationResult.Valid);
//
//     var options = Options.Create(new OrderOptions { MaxItemsPerOrder = 100 });
//     var logger = NullLogger<OrderService>.Instance;
//
//     var sut = new OrderService(repository, validator, options, logger);
//
//     // Act
//     var result = await sut.CreateAsync(
//         new CreateOrderRequest { Items = [new("Widget", 2)] },
//         CancellationToken.None);
//
//     // Assert
//     Assert.True(result.IsSuccess);
//     await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
// }

// ===== Integration Testing: Build a real DI container =====

// --- Test-specific service collection ---

// [Test]
// public async Task OrderService_IntegrationTest_WithRealDependencies() {
//     var services = new ServiceCollection();
//
//     // Register real services with test configuration
//     services.AddSingleton(Options.Create(new OrderOptions { MaxItemsPerOrder = 50 }));
//     services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
//     services.AddTransient<IOrderValidator, OrderValidator>();
//     services.AddScoped<IOrderService, OrderService>();
//     services.AddLogging();
//
//     await using var provider = services.BuildServiceProvider(
//         new ServiceProviderOptions {
//             ValidateScopes = true,
//             ValidateOnBuild = true
//         });
//
//     await using var scope = provider.CreateAsyncScope();
//     var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
//
//     var result = await orderService.CreateAsync(
//         new CreateOrderRequest { Items = [new("Widget", 1)] },
//         CancellationToken.None);
//
//     Assert.True(result.IsSuccess);
// }

// ===== WebApplicationFactory: Full ASP.NET Core integration test =====

// --- Replace services for testing ---

// public sealed class OrderApiTests : IClassFixture<WebApplicationFactory<Program>> {
//     private readonly WebApplicationFactory<Program> _factory;
//
//     public OrderApiTests(WebApplicationFactory<Program> factory) {
//         _factory = factory.WithWebHostBuilder(builder => {
//             builder.ConfigureServices(services => {
//                 // Remove real repository
//                 var descriptor = services.SingleOrDefault(
//                     d => d.ServiceType == typeof(IOrderRepository));
//                 if (descriptor is not null) services.Remove(descriptor);
//
//                 // Add test double
//                 services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
//             });
//         });
//     }
//
//     [Fact]
//     public async Task CreateOrder_ReturnsCreated() {
//         var client = _factory.CreateClient();
//
//         var response = await client.PostAsJsonAsync("/orders",
//             new CreateOrderRequest { Items = [new("Widget", 1)] });
//
//         Assert.Equal(HttpStatusCode.Created, response.StatusCode);
//     }
// }

// ===== Test doubles in DI =====

// For quick test replacements:
public sealed class InMemoryOrderRepository : IOrderRepository {
    private readonly List<Order> _orders = [];
    private int _nextId = 1;

    public Task<Order?> GetByIdAsync(int id, CancellationToken ct)
        => Task.FromResult(_orders.FirstOrDefault(o => o.Id == id));

    public Task SaveAsync(Order order, CancellationToken ct) {
        if (order.Id == 0) order.Id = _nextId++;
        _orders.RemoveAll(o => o.Id == order.Id);
        _orders.Add(order);
        return Task.CompletedTask;
    }
}
