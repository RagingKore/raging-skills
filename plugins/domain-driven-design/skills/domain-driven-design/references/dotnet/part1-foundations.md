# Part I: Foundations

Shared building blocks that apply regardless of persistence strategy.

## Project Structure

```
src/
├── MyDomain.Domain/           # Core domain (no dependencies)
│   ├── Aggregates/
│   │   └── Order/
│   │       ├── Order.cs       # Aggregate root (record)
│   │       ├── OrderLine.cs   # Child entity
│   │       ├── OrderId.cs     # Strongly-typed ID
│   │       ├── Commands.cs    # Command records
│   │       ├── Events.cs      # Event records
│   │       └── OrderDecider.cs # Decider (if functional)
│   ├── ValueObjects/
│   │   ├── Money.cs
│   │   ├── Address.cs
│   │   └── EmailAddress.cs
│   └── Services/
│       └── PricingService.cs  # Domain services
│
├── MyDomain.Application/      # Use cases, handlers
│   ├── Commands/
│   │   └── PlaceOrderHandler.cs
│   ├── Queries/
│   │   └── GetOrderHandler.cs
│   └── Services/
│       └── IEventStore.cs     # Port interfaces
│
├── MyDomain.Infrastructure/   # External concerns
│   ├── Persistence/
│   │   ├── MartenEventStore.cs
│   │   └── KurrentDbEventStore.cs
│   ├── Serialization/
│   │   └── ProtobufSerializer.cs
│   └── Messaging/
│       └── EventPublisher.cs
│
└── MyDomain.Api/              # Entry point
    └── Program.cs
```

---

## Value Objects

Use `readonly record struct` for value objects. Records provide:
- Automatic equality by value
- Immutability (with `init` properties)
- Built-in `ToString()`, `GetHashCode()`
- Deconstruction support

### Basic Value Object

```csharp
namespace MyDomain.Domain.ValueObjects;

/// <summary>
/// Money value object - immutable, equality by value
/// </summary>
public readonly record struct Money(decimal Amount, Currency Currency)
{
    public static Money Zero(Currency currency) => new(0, currency);

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a with { Amount = a.Amount + b.Amount };
    }

    public static Money operator -(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a with { Amount = a.Amount - b.Amount };
    }

    public static Money operator *(Money money, decimal multiplier) =>
        money with { Amount = money.Amount * multiplier };

    public static bool operator >(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a.Amount > b.Amount;
    }

    public static bool operator <(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a.Amount < b.Amount;
    }

    public Money Round(int decimals = 2) =>
        this with { Amount = Math.Round(Amount, decimals, MidpointRounding.AwayFromZero) };

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new CurrencyMismatchException(a.Currency, b.Currency);
    }
}

public readonly record struct Currency(string Code)
{
    public static Currency USD => new("USD");
    public static Currency EUR => new("EUR");
    public static Currency GBP => new("GBP");

    public static Currency FromCode(string code) =>
        new(code.ToUpperInvariant());
}
```

### Strongly-Typed IDs

```csharp
namespace MyDomain.Domain.Aggregates.Order;

/// <summary>
/// Strongly-typed Order ID - prevents mixing up different ID types
/// </summary>
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public static OrderId FromGuid(Guid guid) => new(guid);
    public static OrderId Parse(string s) => new(Guid.Parse(s));

    public override string ToString() => Value.ToString();

    // Implicit conversion for convenience in some scenarios
    public static implicit operator Guid(OrderId id) => id.Value;
}

public readonly record struct CustomerId(Guid Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct ProductId(string Value)
{
    public static ProductId FromSku(string sku) => new(sku);
    public override string ToString() => Value;
}
```

### Complex Value Object with Validation

```csharp
namespace MyDomain.Domain.ValueObjects;

public readonly record struct EmailAddress
{
    public string Value { get; }

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty", nameof(value));

        if (!IsValidEmail(value))
            throw new InvalidEmailException(value);

        Value = value.ToLowerInvariant().Trim();
    }

    private static bool IsValidEmail(string email) =>
        email.Contains('@') &&
        email.Contains('.') &&
        email.IndexOf('@') < email.LastIndexOf('.');

    public override string ToString() => Value;
}

public readonly record struct Address(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country)
{
    public string FullAddress => $"{Street}, {City}, {State} {PostalCode}, {Country}";

    public static Address Create(
        string street, string city, string state,
        string postalCode, string country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);

        return new Address(street, city, state, postalCode, country);
    }
}

public readonly record struct Quantity
{
    public int Value { get; }

    public Quantity(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Quantity cannot be negative");
        Value = value;
    }

    public static Quantity Zero => new(0);
    public static Quantity One => new(1);

    public static Quantity operator +(Quantity a, Quantity b) => new(a.Value + b.Value);
    public static Quantity operator -(Quantity a, Quantity b) => new(a.Value - b.Value);
    public static bool operator >(Quantity a, Quantity b) => a.Value > b.Value;
    public static bool operator <(Quantity a, Quantity b) => a.Value < b.Value;

    public static implicit operator int(Quantity q) => q.Value;
}
```

---

## Entities

Use `record` (not `record struct`) for entities. The key difference from value objects:
- Entities have identity (ID)
- Equality is based on ID only (override if needed)
- Can have mutable state (use `private set`)

### Entity Base Record

```csharp
namespace MyDomain.Domain;

/// <summary>
/// Base record for all entities. Equality based on ID only.
/// </summary>
public abstract record Entity<TId> where TId : struct
{
    public TId Id { get; init; }

    // Records use value equality by default - override for identity equality
    public virtual bool Equals(Entity<TId>? other) =>
        other is not null && Id.Equals(other.Id);

    public override int GetHashCode() => Id.GetHashCode();
}

/// <summary>
/// Base record for aggregate roots with domain events
/// </summary>
public abstract record AggregateRoot<TId> : Entity<TId> where TId : struct
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
```

---

## Domain Events

Domain events are immutable facts that record what happened in the domain. The base types (`IDomainEvent`, `DomainEvent`) are defined in the Entities section above.

### Event Naming Conventions

| Convention | Example | Notes |
|------------|---------|-------|
| Past tense verb | `OrderPlaced`, `ItemAdded` | Indicates something happened |
| Specific, not generic | `OrderShipped` not `OrderUpdated` | Business meaning |
| Include relevant data | IDs, values at time of event | Events are immutable history |

### Concrete Event Examples

```csharp
namespace MyDomain.Domain.Aggregates.Order;

// Events capture what happened with all relevant data
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

public sealed record OrderCancelled(
    OrderId OrderId,
    string Reason,
    DateTime CancelledAt) : DomainEvent;
```

### Event Publisher

```csharp
namespace MyDomain.Application;

public interface IDomainEventPublisher
{
    Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
}

public class MediatREventPublisher : IDomainEventPublisher
{
    private readonly IMediator _mediator;

    public MediatREventPublisher(IMediator mediator) => _mediator = mediator;

    public async Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var @event in events)
        {
            await _mediator.Publish(@event, ct);
        }
    }
}

// In-memory publisher for testing
public class InMemoryEventPublisher : IDomainEventPublisher
{
    private readonly List<IDomainEvent> _publishedEvents = [];

    public IReadOnlyList<IDomainEvent> PublishedEvents => _publishedEvents.AsReadOnly();

    public Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        _publishedEvents.AddRange(events);
        return Task.CompletedTask;
    }

    public void Clear() => _publishedEvents.Clear();
}
```
