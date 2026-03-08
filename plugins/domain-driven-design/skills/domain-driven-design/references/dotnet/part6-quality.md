# Part VI: Quality & Reference

Testing patterns, libraries, and package references.

## Testing Patterns

TUnit is a modern, fast .NET testing framework with source-generated tests and async-first assertions.

### Domain Model Tests

```csharp
namespace MyDomain.Tests.Domain;

using TUnit;

public class OrderTests
{
    [Test]
    public async Task Place_WithValidItems_CreatesOrderInPlacedStatus()
    {
        // Arrange
        var command = new PlaceOrderCommand(
            CustomerId.New(),
            [
                new OrderLineInput(ProductId.FromSku("SKU-001"), new Quantity(2), new Money(10.00m, Currency.USD)),
                new OrderLineInput(ProductId.FromSku("SKU-002"), new Quantity(1), new Money(25.00m, Currency.USD))
            ],
            Address.Create("123 Main St", "City", "ST", "12345", "USA"));

        // Act
        var order = Order.Place(command);

        // Assert - TUnit async assertions
        await Assert.That(order.Status).IsEqualTo(OrderStatus.Placed);
        await Assert.That(order.Lines).HasCount(2);
        await Assert.That(order.Total.Amount).IsEqualTo(45.00m);
        await Assert.That(order.DomainEvents).HasCount(1);
        await Assert.That(order.DomainEvents[0]).IsTypeOf<OrderPlaced>();
    }

    [Test]
    public async Task Ship_WhenNotPaidOrPlaced_ThrowsException()
    {
        // Arrange
        var order = CreateShippedOrder();
        var shipmentDetails = new ShipmentDetails(ShipmentId.New(), "FedEx", "1234567890");

        // Act & Assert
        await Assert.That(() => order.Ship(shipmentDetails))
            .ThrowsException()
            .OfType<InvalidOrderStateException>();
    }

    [Test]
    [Arguments(OrderStatus.Draft)]
    [Arguments(OrderStatus.Shipped)]
    [Arguments(OrderStatus.Delivered)]
    public async Task AddItem_WhenNotModifiable_ThrowsException(OrderStatus status)
    {
        // Arrange
        var order = CreateOrderWithStatus(status);

        // Act & Assert
        await Assert.That(() => order.AddItem(
                ProductId.FromSku("NEW"),
                new Quantity(1),
                new Money(10m, Currency.USD)))
            .ThrowsException()
            .OfType<OrderNotModifiableException>();
    }

    private static Order CreateOrderWithStatus(OrderStatus status)
    {
        // Helper to create order in specific state for testing
        var order = Order.Place(new PlaceOrderCommand(
            CustomerId.New(),
            [new OrderLineInput(ProductId.FromSku("X"), new Quantity(1), new Money(10m, Currency.USD))],
            Address.Create("St", "City", "ST", "12345", "USA")));

        // Use reflection or internal methods to set status for testing
        // In real code, you'd have proper state transitions
        return order;
    }
}
```

### Decider Tests

```csharp
namespace MyDomain.Tests.Domain;

public class OrderDeciderTests
{
    [Test]
    public async Task Decide_PlaceOrder_ReturnsOrderPlacedEvent()
    {
        // Arrange
        var state = OrderState.Initial;
        var command = new PlaceOrder(
            OrderId.New(),
            CustomerId.New(),
            [new OrderLineInput(ProductId.FromSku("SKU-1"), new Quantity(2), new Money(10m, Currency.USD))],
            Address.Create("123 Main", "City", "ST", "12345", "USA"));

        // Act
        var events = OrderDecider.Decide(command, state);

        // Assert
        await Assert.That(events).HasCount(1);
        await Assert.That(events[0]).IsTypeOf<OrderPlaced>();

        var placed = (OrderPlaced)events[0];
        await Assert.That(placed.OrderId).IsEqualTo(command.OrderId);
        await Assert.That(placed.Total.Amount).IsEqualTo(20m);
    }

    [Test]
    public async Task Evolve_OrderPlaced_UpdatesStateCorrectly()
    {
        // Arrange
        var initial = OrderState.Initial;
        var @event = new OrderPlaced(
            OrderId.New(),
            CustomerId.New(),
            [new OrderLineSnapshot(ProductId.FromSku("SKU-1"), new Quantity(2), new Money(10m, Currency.USD))],
            Address.Create("123 Main", "City", "ST", "12345", "USA"),
            new Money(20m, Currency.USD),
            DateTime.UtcNow);

        // Act
        var newState = OrderDecider.Evolve(initial, @event);

        // Assert
        await Assert.That(newState.Status).IsEqualTo(OrderStatus.Placed);
        await Assert.That(newState.Lines).HasCount(1);
        await Assert.That(newState.Total.Amount).IsEqualTo(20m);
    }

    [Test]
    public async Task Decide_PlaceOrder_WhenAlreadyPlaced_Throws()
    {
        // Arrange
        var state = OrderState.Initial with { Status = OrderStatus.Placed };
        var command = new PlaceOrder(
            OrderId.New(),
            CustomerId.New(),
            [new OrderLineInput(ProductId.FromSku("SKU-1"), new Quantity(1), new Money(10m, Currency.USD))],
            Address.Create("123 Main", "City", "ST", "12345", "USA"));

        // Act & Assert
        await Assert.That(() => OrderDecider.Decide(command, state))
            .ThrowsException()
            .OfType<InvalidOrderStateException>();
    }
}

// Given-When-Then helper for readable tests
public static class DeciderTestHelpers
{
    public static DeciderScenario<TState, TEvent> Given<TState, TEvent>(TState initialState)
        where TEvent : DomainEvent
    {
        return new DeciderScenario<TState, TEvent>(initialState);
    }
}

public class DeciderScenario<TState, TEvent> where TEvent : DomainEvent
{
    private readonly TState _state;
    private IReadOnlyList<TEvent>? _producedEvents;
    private Exception? _exception;

    public DeciderScenario(TState state) => _state = state;

    public DeciderScenario<TState, TEvent> When(Func<TState, IReadOnlyList<TEvent>> decide)
    {
        try
        {
            _producedEvents = decide(_state);
        }
        catch (Exception ex)
        {
            _exception = ex;
        }
        return this;
    }

    public async Task ThenExpect(params TEvent[] expectedEvents)
    {
        await Assert.That(_exception).IsNull();
        await Assert.That(_producedEvents).IsNotNull();
        await Assert.That(_producedEvents!.Count).IsEqualTo(expectedEvents.Length);

        for (int i = 0; i < expectedEvents.Length; i++)
        {
            await Assert.That(_producedEvents[i].GetType())
                .IsEqualTo(expectedEvents[i].GetType());
        }
    }

    public async Task ThenThrows<TException>() where TException : Exception
    {
        await Assert.That(_exception).IsNotNull();
        await Assert.That(_exception).IsTypeOf<TException>();
    }
}
```

### Integration Tests with Marten

```csharp
namespace MyDomain.Tests.Integration;

using Marten;
using Testcontainers.PostgreSql;

public class MartenOrderStoreTests : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;
    private IDocumentStore _store = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .Build();

        await _postgres.StartAsync();

        _store = DocumentStore.For(opts =>
        {
            opts.Connection(_postgres.GetConnectionString());
            opts.ConfigureEventSourcing();
        });
    }

    public async Task DisposeAsync()
    {
        _store.Dispose();
        await _postgres.DisposeAsync();
    }

    [Test]
    public async Task RoundTrip_SaveAndLoad_ReturnsCorrectState()
    {
        // Arrange
        var orderId = OrderId.New();
        var store = new MartenOrderStore(_store);

        var placeCommand = new PlaceOrder(
            orderId,
            CustomerId.New(),
            [new OrderLineInput(ProductId.FromSku("SKU-1"), new Quantity(2), new Money(10m, Currency.USD))],
            Address.Create("123 Main", "City", "ST", "12345", "USA"));

        // Act - Save
        var stateAfterPlace = await store.DecideAndAppend(
            orderId.Value.ToString(),
            placeCommand);

        // Assert intermediate state
        await Assert.That(stateAfterPlace.Status).IsEqualTo(OrderStatus.Placed);

        // Act - Ship
        var shipCommand = new ShipOrder(orderId, ShipmentId.New(), "FedEx", "123456");
        var stateAfterShip = await store.DecideAndAppend(
            orderId.Value.ToString(),
            shipCommand);

        // Assert final state
        await Assert.That(stateAfterShip.Status).IsEqualTo(OrderStatus.Shipped);

        // Act - Reload from scratch
        var reloadedState = await store.LoadAsync(orderId.Value.ToString());

        // Assert reloaded state matches
        await Assert.That(reloadedState.Status).IsEqualTo(OrderStatus.Shipped);
        await Assert.That(reloadedState.Total).IsEqualTo(stateAfterShip.Total);
    }
}
```

---

## Libraries & Packages

### By Category

| Category | Package | Use Case |
|----------|---------|----------|
| **Domain** | Ardalis.GuardClauses | Guard clauses, validation |
| **ORM** | Microsoft.EntityFrameworkCore | State-based persistence |
| **Document DB** | Marten | Document store + Event sourcing |
| **Event Store** | KurrentDB.Client | Dedicated event store |
| **CQRS** | MediatR | Command/Query dispatching |
| **Actors** | Akka.Persistence | Actor model with persistence |
| **Serialization** | protobuf-net | Binary serialization |
| **Serialization** | System.Text.Json | JSON (built-in) |
| **Testing** | TUnit | Modern test framework |
| **Testing** | Testcontainers | Integration test containers |

### Domain Project

```xml
<PackageReference Include="Ardalis.GuardClauses" Version="5.*" />
```

### Application Project

```xml
<PackageReference Include="MediatR" Version="12.*" />
```

### Infrastructure Project

```xml
<!-- State-Based Persistence (choose one or both) -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.*" />
<PackageReference Include="Marten" Version="7.*" />

<!-- Event-Based Persistence -->
<PackageReference Include="KurrentDB.Client" Version="1.*" />

<!-- Actors -->
<PackageReference Include="Akka.Persistence" Version="1.5.*" />
<PackageReference Include="Akka.Persistence.PostgreSql" Version="1.5.*" />

<!-- Serialization -->
<PackageReference Include="protobuf-net" Version="3.*" />
```

### Test Project

```xml
<PackageReference Include="TUnit" Version="0.6.*" />
<PackageReference Include="Testcontainers.PostgreSql" Version="4.*" />
<PackageReference Include="Testcontainers.MsSql" Version="4.*" />
```
