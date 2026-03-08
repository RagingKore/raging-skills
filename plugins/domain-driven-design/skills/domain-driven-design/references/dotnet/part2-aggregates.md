# Part II: Aggregates

Core aggregate patterns that apply to both persistence strategies.

## Aggregates - OOP Style

Traditional rich domain model with behavior encapsulated in objects.

### OOP Aggregate (Record-based)

```csharp
namespace MyDomain.Domain.Aggregates.Order;

// Commands
public sealed record PlaceOrderCommand(
    CustomerId CustomerId,
    IReadOnlyList<OrderLineInput> Items,
    Address ShippingAddress);

public sealed record AddItemCommand(OrderId OrderId, ProductId ProductId, Quantity Quantity, Money UnitPrice);
public sealed record ShipOrderCommand(OrderId OrderId, ShipmentDetails Details);

public sealed record OrderLineInput(ProductId ProductId, Quantity Quantity, Money UnitPrice);

// Events
public sealed record OrderPlaced(
    OrderId OrderId,
    CustomerId CustomerId,
    IReadOnlyList<OrderLineSnapshot> Items,
    Address ShippingAddress,
    Money Total,
    DateTime PlacedAt) : DomainEvent;

public sealed record ItemAddedToOrder(
    OrderId OrderId,
    OrderLineId LineId,
    ProductId ProductId,
    Quantity Quantity,
    Money UnitPrice) : DomainEvent;

public sealed record OrderShipped(
    OrderId OrderId,
    ShipmentId ShipmentId,
    string Carrier,
    string TrackingNumber,
    DateTime ShippedAt) : DomainEvent;

public sealed record OrderLineSnapshot(ProductId ProductId, Quantity Quantity, Money UnitPrice);

// Aggregate Root
public record Order : AggregateRoot<OrderId>
{
    public CustomerId CustomerId { get; private init; }
    public OrderStatus Status { get; private set; }
    public Address ShippingAddress { get; private init; }
    public Money Total { get; private set; }
    public DateTime? PlacedAt { get; private init; }
    public DateTime? ShippedAt { get; private set; }

    private readonly List<OrderLine> _lines = [];
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    private Order() { } // For ORMs/serialization

    public static Order Place(PlaceOrderCommand command)
    {
        var orderId = OrderId.New();
        var order = new Order
        {
            Id = orderId,
            CustomerId = command.CustomerId,
            ShippingAddress = command.ShippingAddress,
            Status = OrderStatus.Placed,
            PlacedAt = DateTime.UtcNow
        };

        foreach (var item in command.Items)
        {
            order._lines.Add(OrderLine.Create(
                OrderLineId.New(),
                item.ProductId,
                item.Quantity,
                item.UnitPrice));
        }

        order.RecalculateTotal();

        order.AddDomainEvent(new OrderPlaced(
            orderId,
            command.CustomerId,
            command.Items.Select(i => new OrderLineSnapshot(i.ProductId, i.Quantity, i.UnitPrice)).ToList(),
            command.ShippingAddress,
            order.Total,
            order.PlacedAt!.Value));

        return order;
    }

    public void AddItem(ProductId productId, Quantity quantity, Money unitPrice)
    {
        EnsureModifiable();

        var existingLine = _lines.FirstOrDefault(l => l.ProductId == productId);
        if (existingLine is not null)
        {
            existingLine.IncreaseQuantity(quantity);
        }
        else
        {
            var line = OrderLine.Create(OrderLineId.New(), productId, quantity, unitPrice);
            _lines.Add(line);

            AddDomainEvent(new ItemAddedToOrder(Id, line.Id, productId, quantity, unitPrice));
        }

        RecalculateTotal();
    }

    public void Ship(ShipmentDetails details)
    {
        if (Status != OrderStatus.Placed && Status != OrderStatus.Paid)
            throw new InvalidOrderStateException(Id, Status, "ship");

        Status = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderShipped(
            Id,
            details.ShipmentId,
            details.Carrier,
            details.TrackingNumber,
            ShippedAt.Value));
    }

    private void EnsureModifiable()
    {
        if (Status != OrderStatus.Draft && Status != OrderStatus.Placed)
            throw new OrderNotModifiableException(Id, Status);
    }

    private void RecalculateTotal()
    {
        Total = _lines.Aggregate(
            Money.Zero(Currency.USD),
            (sum, line) => sum + line.LineTotal);
    }
}

public record OrderLine : Entity<OrderLineId>
{
    public ProductId ProductId { get; private init; }
    public Quantity Quantity { get; private set; }
    public Money UnitPrice { get; private init; }
    public Money LineTotal => UnitPrice * Quantity.Value;

    private OrderLine() { }

    public static OrderLine Create(OrderLineId id, ProductId productId, Quantity quantity, Money unitPrice)
    {
        return new OrderLine
        {
            Id = id,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice
        };
    }

    public void IncreaseQuantity(Quantity additional)
    {
        Quantity = Quantity + additional;
    }
}

public readonly record struct OrderLineId(Guid Value)
{
    public static OrderLineId New() => new(Guid.NewGuid());
}

public readonly record struct ShipmentId(Guid Value)
{
    public static ShipmentId New() => new(Guid.NewGuid());
}

public record ShipmentDetails(ShipmentId ShipmentId, string Carrier, string TrackingNumber);

public enum OrderStatus { Draft, Placed, Paid, Shipped, Delivered, Cancelled }
```

---

## Aggregates - Decider Pattern

Pure functional approach with immutable state.

```csharp
namespace MyDomain.Domain.Aggregates.Order;

// Immutable State
public sealed record OrderState
{
    public OrderId Id { get; init; }
    public CustomerId CustomerId { get; init; }
    public OrderStatus Status { get; init; }
    public IReadOnlyList<OrderLineState> Lines { get; init; } = [];
    public Money Total { get; init; }
    public Address? ShippingAddress { get; init; }
    public DateTime? PlacedAt { get; init; }
    public DateTime? ShippedAt { get; init; }

    public static OrderState Initial => new()
    {
        Status = OrderStatus.Draft,
        Lines = [],
        Total = Money.Zero(Currency.USD)
    };
}

public sealed record OrderLineState(
    OrderLineId Id,
    ProductId ProductId,
    Quantity Quantity,
    Money UnitPrice)
{
    public Money LineTotal => UnitPrice * Quantity.Value;
}

// Commands
public abstract record OrderCommand;
public sealed record PlaceOrder(
    OrderId OrderId,
    CustomerId CustomerId,
    IReadOnlyList<OrderLineInput> Items,
    Address ShippingAddress) : OrderCommand;

public sealed record AddItem(
    OrderId OrderId,
    ProductId ProductId,
    Quantity Quantity,
    Money UnitPrice) : OrderCommand;

public sealed record ShipOrder(
    OrderId OrderId,
    ShipmentId ShipmentId,
    string Carrier,
    string TrackingNumber) : OrderCommand;

// Events (reuse from OOP section)

/// <summary>
/// Pure functional Decider - no side effects, easy to test
/// </summary>
public static class OrderDecider
{
    /// <summary>
    /// DECIDE: Given current state and command, produce events (or throw)
    /// </summary>
    public static IReadOnlyList<DomainEvent> Decide(OrderCommand command, OrderState state) =>
        command switch
        {
            PlaceOrder cmd => DecidePlaceOrder(cmd, state),
            AddItem cmd => DecideAddItem(cmd, state),
            ShipOrder cmd => DecideShipOrder(cmd, state),
            _ => throw new UnknownCommandException(command.GetType().Name)
        };

    private static IReadOnlyList<DomainEvent> DecidePlaceOrder(PlaceOrder cmd, OrderState state)
    {
        if (state.Status != OrderStatus.Draft)
            throw new InvalidOrderStateException(cmd.OrderId, state.Status, "place");

        if (cmd.Items.Count == 0)
            throw new EmptyOrderException(cmd.OrderId);

        var total = cmd.Items.Aggregate(
            Money.Zero(Currency.USD),
            (sum, item) => sum + (item.UnitPrice * item.Quantity.Value));

        return
        [
            new OrderPlaced(
                cmd.OrderId,
                cmd.CustomerId,
                cmd.Items.Select(i => new OrderLineSnapshot(i.ProductId, i.Quantity, i.UnitPrice)).ToList(),
                cmd.ShippingAddress,
                total,
                DateTime.UtcNow)
        ];
    }

    private static IReadOnlyList<DomainEvent> DecideAddItem(AddItem cmd, OrderState state)
    {
        if (state.Status != OrderStatus.Draft && state.Status != OrderStatus.Placed)
            throw new OrderNotModifiableException(cmd.OrderId, state.Status);

        return
        [
            new ItemAddedToOrder(
                cmd.OrderId,
                OrderLineId.New(),
                cmd.ProductId,
                cmd.Quantity,
                cmd.UnitPrice)
        ];
    }

    private static IReadOnlyList<DomainEvent> DecideShipOrder(ShipOrder cmd, OrderState state)
    {
        if (state.Status != OrderStatus.Placed && state.Status != OrderStatus.Paid)
            throw new InvalidOrderStateException(cmd.OrderId, state.Status, "ship");

        return
        [
            new OrderShipped(
                cmd.OrderId,
                cmd.ShipmentId,
                cmd.Carrier,
                cmd.TrackingNumber,
                DateTime.UtcNow)
        ];
    }

    /// <summary>
    /// EVOLVE: Apply event to state, producing new state
    /// </summary>
    public static OrderState Evolve(OrderState state, DomainEvent @event) =>
        @event switch
        {
            OrderPlaced e => state with
            {
                Id = e.OrderId,
                CustomerId = e.CustomerId,
                Status = OrderStatus.Placed,
                ShippingAddress = e.ShippingAddress,
                Total = e.Total,
                PlacedAt = e.PlacedAt,
                Lines = e.Items.Select((item, index) => new OrderLineState(
                    new OrderLineId(Guid.NewGuid()),
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice)).ToList()
            },

            ItemAddedToOrder e => state with
            {
                Lines = state.Lines.Append(new OrderLineState(
                    e.LineId, e.ProductId, e.Quantity, e.UnitPrice)).ToList(),
                Total = state.Total + (e.UnitPrice * e.Quantity.Value)
            },

            OrderShipped e => state with
            {
                Status = OrderStatus.Shipped,
                ShippedAt = e.ShippedAt
            },

            _ => state // Unknown events don't change state
        };

    /// <summary>
    /// Replay events to rebuild state
    /// </summary>
    public static OrderState Replay(IEnumerable<DomainEvent> events) =>
        events.Aggregate(OrderState.Initial, Evolve);
}
```
