# Part IV: Event-Sourced Implementation

Persistence where events are the source of truth and state is derived.

## Event Store

Use event sourcing when events are the source of truth. The aggregate state is rebuilt by replaying events.

### KurrentDB Implementation

```csharp
namespace MyDomain.Infrastructure.Persistence;

using Kurrent.Client;
using Kurrent.Client.Streams;

public interface IEventStore<TState, TEvent>
{
    Task<TState> LoadAsync(string streamId, CancellationToken ct = default);
    Task AppendAsync(string streamId, TState currentState, IReadOnlyList<TEvent> events, CancellationToken ct = default);
}

public class KurrentDbOrderStore : IEventStore<OrderState, DomainEvent>
{
    private readonly KurrentDBClient _client;
    private readonly IEventSerializer _serializer;

    public KurrentDbOrderStore(KurrentDBClient client, IEventSerializer serializer)
    {
        _client = client;
        _serializer = serializer;
    }

    public async Task<OrderState> LoadAsync(string streamId, CancellationToken ct = default)
    {
        var events = new List<DomainEvent>();

        await foreach (var resolvedEvent in _client.ReadStreamAsync(
            Direction.Forwards,
            streamId,
            StreamPosition.Start,
            cancellationToken: ct))
        {
            var @event = _serializer.Deserialize(
                resolvedEvent.Event.EventType,
                resolvedEvent.Event.Data.Span);

            if (@event is DomainEvent domainEvent)
                events.Add(domainEvent);
        }

        return OrderDecider.Replay(events);
    }

    public async Task AppendAsync(
        string streamId,
        OrderState currentState,
        IReadOnlyList<DomainEvent> events,
        CancellationToken ct = default)
    {
        var eventData = events.Select(e => new EventData(
            Uuid.NewUuid(),
            e.GetType().Name,
            _serializer.Serialize(e),
            _serializer.SerializeMetadata(e)
        )).ToArray();

        // Optimistic concurrency using expected version
        await _client.AppendToStreamAsync(
            streamId,
            StreamState.Any, // Or use specific version for concurrency
            eventData,
            cancellationToken: ct);
    }
}
```

### Marten Implementation

```csharp
namespace MyDomain.Infrastructure.Persistence;

using Marten;
using Marten.Events;
using Marten.Events.Projections;

// Configure Marten
public static class MartenConfiguration
{
    public static void ConfigureEventSourcing(this StoreOptions options)
    {
        // Register event types
        options.Events.AddEventType<OrderPlaced>();
        options.Events.AddEventType<ItemAddedToOrder>();
        options.Events.AddEventType<OrderShipped>();

        // Inline projection - updated in same transaction
        options.Projections.Snapshot<OrderState>(SnapshotLifecycle.Inline);

        // Async projection - eventual consistency
        options.Projections.Add<OrderSummaryProjection>(ProjectionLifecycle.Async);
    }
}

// Event Store using Marten
public class MartenOrderStore : IEventStore<OrderState, DomainEvent>
{
    private readonly IDocumentStore _store;

    public MartenOrderStore(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<OrderState> LoadAsync(string streamId, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();

        // Marten can aggregate events automatically using Apply methods
        var state = await session.Events.AggregateStreamAsync<OrderState>(
            Guid.Parse(streamId),
            token: ct);

        return state ?? OrderState.Initial;
    }

    public async Task AppendAsync(
        string streamId,
        OrderState currentState,
        IReadOnlyList<DomainEvent> events,
        CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();

        var id = Guid.Parse(streamId);

        // Append events to stream
        session.Events.Append(id, events.Cast<object>().ToArray());

        await session.SaveChangesAsync(ct);
    }

    // Alternative: Use Decider pattern with Marten
    public async Task<OrderState> DecideAndAppend(
        string streamId,
        OrderCommand command,
        CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();

        var id = Guid.Parse(streamId);

        // Load current state
        var state = await session.Events.AggregateStreamAsync<OrderState>(id, token: ct)
            ?? OrderState.Initial;

        // Decide
        var events = OrderDecider.Decide(command, state);

        // Append
        session.Events.Append(id, events.Cast<object>().ToArray());

        await session.SaveChangesAsync(ct);

        // Return new state
        return events.Aggregate(state, OrderDecider.Evolve);
    }
}

// Marten needs Apply methods on state for automatic aggregation
public sealed record OrderState
{
    // ... existing properties ...

    // Apply methods for Marten
    public OrderState Apply(OrderPlaced @event) => this with
    {
        Id = @event.OrderId,
        CustomerId = @event.CustomerId,
        Status = OrderStatus.Placed,
        ShippingAddress = @event.ShippingAddress,
        Total = @event.Total,
        PlacedAt = @event.PlacedAt,
        Lines = @event.Items.Select((item, idx) => new OrderLineState(
            OrderLineId.New(),
            item.ProductId,
            item.Quantity,
            item.UnitPrice)).ToList()
    };

    public OrderState Apply(ItemAddedToOrder @event) => this with
    {
        Lines = Lines.Append(new OrderLineState(
            @event.LineId,
            @event.ProductId,
            @event.Quantity,
            @event.UnitPrice)).ToList(),
        Total = Total + (@event.UnitPrice * @event.Quantity.Value)
    };

    public OrderState Apply(OrderShipped @event) => this with
    {
        Status = OrderStatus.Shipped,
        ShippedAt = @event.ShippedAt
    };
}
```

---

## CQRS & Projections

### Read Model (View)

```csharp
namespace MyDomain.Application.ReadModels;

// Denormalized view optimized for queries
public record OrderSummary
{
    public Guid Id { get; init; }
    public string CustomerId { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string Status { get; init; } = "";
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "USD";
    public int ItemCount { get; init; }
    public DateTime PlacedAt { get; init; }
    public DateTime? ShippedAt { get; init; }
    public string? TrackingNumber { get; init; }
}
```

### Marten Projection

```csharp
namespace MyDomain.Infrastructure.Projections;

using Marten.Events.Aggregation;

// Single-stream projection for order summary
public class OrderSummaryProjection : SingleStreamProjection<OrderSummary>
{
    public OrderSummaryProjection()
    {
        // Delete document when order cancelled
        DeleteEvent<OrderCancelled>();
    }

    public OrderSummary Create(OrderPlaced @event)
    {
        return new OrderSummary
        {
            Id = @event.OrderId.Value,
            CustomerId = @event.CustomerId.Value.ToString(),
            Status = "Placed",
            TotalAmount = @event.Total.Amount,
            Currency = @event.Total.Currency.Code,
            ItemCount = @event.Items.Count,
            PlacedAt = @event.PlacedAt
        };
    }

    public OrderSummary Apply(ItemAddedToOrder @event, OrderSummary current)
    {
        return current with
        {
            ItemCount = current.ItemCount + 1,
            TotalAmount = current.TotalAmount + (@event.UnitPrice.Amount * @event.Quantity.Value)
        };
    }

    public OrderSummary Apply(OrderShipped @event, OrderSummary current)
    {
        return current with
        {
            Status = "Shipped",
            ShippedAt = @event.ShippedAt,
            TrackingNumber = @event.TrackingNumber
        };
    }

    public OrderSummary Apply(OrderPaid @event, OrderSummary current)
    {
        return current with { Status = "Paid" };
    }
}

// Multi-stream projection example - aggregate across streams
public class DailyOrderStatsProjection : MultiStreamProjection<DailyOrderStats, string>
{
    public DailyOrderStatsProjection()
    {
        // Group by date
        Identity<OrderPlaced>(e => e.PlacedAt.ToString("yyyy-MM-dd"));
    }

    public DailyOrderStats Create(OrderPlaced @event)
    {
        return new DailyOrderStats
        {
            Date = @event.PlacedAt.Date,
            OrderCount = 1,
            TotalRevenue = @event.Total.Amount
        };
    }

    public DailyOrderStats Apply(OrderPlaced @event, DailyOrderStats current)
    {
        return current with
        {
            OrderCount = current.OrderCount + 1,
            TotalRevenue = current.TotalRevenue + @event.Total.Amount
        };
    }
}

public record DailyOrderStats
{
    public string Id { get; init; } = "";
    public DateTime Date { get; init; }
    public int OrderCount { get; init; }
    public decimal TotalRevenue { get; init; }
}
```

### Query Handler

```csharp
namespace MyDomain.Application.Queries;

public record GetOrderSummary(Guid OrderId);
public record GetRecentOrders(int Count = 10);

public class OrderQueryHandler
{
    private readonly IDocumentStore _store;

    public OrderQueryHandler(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<OrderSummary?> Handle(GetOrderSummary query, CancellationToken ct)
    {
        await using var session = _store.QuerySession();
        return await session.LoadAsync<OrderSummary>(query.OrderId, ct);
    }

    public async Task<IReadOnlyList<OrderSummary>> Handle(GetRecentOrders query, CancellationToken ct)
    {
        await using var session = _store.QuerySession();

        return await session.Query<OrderSummary>()
            .OrderByDescending(x => x.PlacedAt)
            .Take(query.Count)
            .ToListAsync(ct);
    }
}
```

---

## Event Serialization & Versioning

Serialization strategy is critical for DDD applications, especially event-sourced systems. Key concerns:

- **Internal events**: JSON (System.Text.Json) for flexibility and readability
- **Integration events**: Protobuf for schema evolution and cross-platform compatibility
- **Domain → Integration translation**: Never expose internal domain events directly

### Protobuf Contracts

```protobuf
// contracts/order_events.proto
syntax = "proto3";

package mycompany.orders.v1;

import "google/protobuf/timestamp.proto";

message OrderPlacedEvent {
    string order_id = 1;
    string customer_id = 2;
    google.protobuf.Timestamp placed_at = 3;
    repeated OrderItem items = 4;
    Money total = 5;
}

message OrderItem {
    string product_id = 1;
    int32 quantity = 2;
    Money unit_price = 3;
}

message Money {
    int64 amount_minor_units = 1;  // cents/pence
    string currency_code = 2;
}

message OrderShippedEvent {
    string order_id = 1;
    string shipment_id = 2;
    string carrier = 3;
    string tracking_number = 4;
    google.protobuf.Timestamp shipped_at = 5;
}
```

### Code-First with protobuf-net

```csharp
namespace MyDomain.Infrastructure.Serialization;

using ProtoBuf;

// Code-first approach with attributes
[ProtoContract]
public record OrderPlacedIntegrationEvent
{
    [ProtoMember(1)] public string OrderId { get; init; } = "";
    [ProtoMember(2)] public string CustomerId { get; init; } = "";
    [ProtoMember(3)] public DateTime PlacedAt { get; init; }
    [ProtoMember(4)] public List<OrderItemDto> Items { get; init; } = [];
    [ProtoMember(5)] public MoneyDto Total { get; init; } = new();
}

[ProtoContract]
public record OrderItemDto
{
    [ProtoMember(1)] public string ProductId { get; init; } = "";
    [ProtoMember(2)] public int Quantity { get; init; }
    [ProtoMember(3)] public MoneyDto UnitPrice { get; init; } = new();
}

[ProtoContract]
public record MoneyDto
{
    [ProtoMember(1)] public long AmountMinorUnits { get; init; }  // Store as cents
    [ProtoMember(2)] public string CurrencyCode { get; init; } = "USD";

    public static MoneyDto FromMoney(Money money) => new()
    {
        AmountMinorUnits = (long)(money.Amount * 100),
        CurrencyCode = money.Currency.Code
    };

    public Money ToMoney() => new(
        AmountMinorUnits / 100m,
        Currency.FromCode(CurrencyCode));
}

// Serializer
public class ProtobufEventSerializer : IEventSerializer
{
    public byte[] Serialize<T>(T @event)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, @event);
        return stream.ToArray();
    }

    public T Deserialize<T>(ReadOnlySpan<byte> data)
    {
        return Serializer.Deserialize<T>(data);
    }
}

// Translator: Domain Event -> Integration Event
public class OrderEventTranslator
{
    public OrderPlacedIntegrationEvent Translate(OrderPlaced domainEvent)
    {
        return new OrderPlacedIntegrationEvent
        {
            OrderId = domainEvent.OrderId.Value.ToString(),
            CustomerId = domainEvent.CustomerId.Value.ToString(),
            PlacedAt = domainEvent.PlacedAt,
            Items = domainEvent.Items.Select(i => new OrderItemDto
            {
                ProductId = i.ProductId.Value,
                Quantity = i.Quantity.Value,
                UnitPrice = MoneyDto.FromMoney(i.UnitPrice)
            }).ToList(),
            Total = MoneyDto.FromMoney(domainEvent.Total)
        };
    }
}
```
