# Common DDD Mistakes (Detailed)

## Table of Contents

- [1. Anemic Domain Model](#1-anemic-domain-model)
- [2. Aggregate Too Large](#2-aggregate-too-large)
- [3. Repository Per Entity](#3-repository-per-entity)
- [4. Exposing Domain Internals](#4-exposing-domain-internals)
- [5. CRUD Events Instead of Domain Events](#5-crud-events-instead-of-domain-events)
- [6. Missing Bounded Context Boundaries](#6-missing-bounded-context-boundaries)
- [7. Synchronous Cross-Aggregate Communication](#7-synchronous-cross-aggregate-communication)
- [8. Treating Domain Events as Integration Events](#8-treating-domain-events-as-integration-events)
- [9. Over-Engineering Simple Domains](#9-over-engineering-simple-domains)
- [10. Not Including Domain Experts](#10-not-including-domain-experts)

---

## 1. Anemic Domain Model

**What happens**: Entities become data containers with getters/setters. Business logic lives in "services" that manipulate entities externally.

**Why it's wrong**:
- Violates encapsulation—anyone can put entity in invalid state
- Logic scattered across services—hard to find, easy to duplicate
- No single source of truth for business rules
- Tests must set up complex service interactions

**❌ BAD: Anemic model**
```csharp
// Entity is just data
public class Order
{
    public Guid Id { get; set; }
    public OrderStatus Status { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
    public decimal Total { get; set; }
}

// Logic in services
public class OrderService
{
    public void AddItem(Order order, Product product, int quantity)
    {
        if (order.Status != OrderStatus.Draft)
            throw new InvalidOperationException("Cannot modify");

        order.Lines.Add(new OrderLine { ProductId = product.Id, Quantity = quantity });
        order.Total = order.Lines.Sum(l => l.Quantity * GetPrice(l.ProductId));
    }
}
```

**✅ GOOD: Rich domain model**
```csharp
public record Order
{
    public OrderId Id { get; init; }
    public OrderStatus Status { get; private set; }
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public void AddItem(Product product, Quantity quantity)
    {
        EnsureDraft();
        var line = OrderLine.Create(product.Id, quantity, product.Price);
        _lines.Add(line);
        RecalculateTotal();
        AddDomainEvent(new ItemAddedToOrder(Id, product.Id, quantity));
    }

    private void EnsureDraft()
    {
        if (Status != OrderStatus.Draft)
            throw new OrderNotModifiableException(Id, Status);
    }
}
```

---

## 2. Aggregate Too Large

**What happens**: Everything related gets stuffed into one aggregate.

**Why it's wrong**: Creates contention, performance issues, complex transactions.

**❌ BAD**: Order contains Customer contains Address contains Country...
```csharp
public record Order
{
    public Customer Customer { get; set; }  // ❌ Full entity
    public record OrderLine { public Product Product { get; set; } }  // ❌ Full entity
}
```

**✅ GOOD**: Reference by ID, copy needed data as value objects
```csharp
public record Order
{
    public CustomerId CustomerId { get; init; }  // ✅ Just ID
    public ShippingAddress Address { get; init; }  // ✅ Value object snapshot
}
```

**Signs your aggregate is too large**:
- Loading it requires multiple queries/joins
- Concurrent updates cause conflicts
- Transaction timeouts on save
- Aggregate has > 10 entities inside

---

## 3. Repository Per Entity

**What happens**: Each entity gets its own repository, bypassing aggregate root.

**Why it's wrong**: Breaks aggregate boundaries, allows invalid state.

**❌ BAD**:
```csharp
var line = _orderLineRepository.GetById(lineId);  // ❌ Direct access
line.Discount = discount;  // No validation!
```

**✅ GOOD**:
```csharp
var order = await _orderRepository.GetById(orderId);
order.ApplyDiscountToLine(lineId, discount);  // Aggregate enforces rules
await _orderRepository.Save(order);
```

---

## 4. Exposing Domain Internals

**What happens**: Collections and internal state exposed via public setters.

**❌ BAD**:
```csharp
public List<OrderLine> Lines { get; set; }
// Anyone can do: order.Lines.Clear() - bypassing all rules
```

**✅ GOOD**:
```csharp
private readonly List<OrderLine> _lines = new();
public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

public void AddLine(OrderLine line) { /* validation */ _lines.Add(line); }
public void RemoveLine(OrderLineId id) { /* validation */ ... }
```

---

## 5. CRUD Events Instead of Domain Events

**What happens**: Events just describe data changes, not business meaning.

**❌ BAD**:
```csharp
new OrderUpdated(Order newState)  // What happened? Who knows!
new OrderStatusChanged(oldStatus, newStatus)  // Still unclear
```

**✅ GOOD**:
```csharp
new OrderShipped(OrderId, ShipmentId, CarrierId, DateTime)  // Clear business meaning
new OrderCancelled(OrderId, Reason, CancelledBy, DateTime)
new PaymentReceived(OrderId, PaymentId, Amount, Method)
```

**Rule**: Event names should be understandable to domain experts.

---

## 6. Missing Bounded Context Boundaries

**What happens**: One shared model used across different business areas.

**❌ BAD**: One `Account` class used by Banking, Identity, and Marketing.
```csharp
public class Account  // Used everywhere - means different things
{
    public string Username { get; set; }  // Identity needs this
    public decimal Balance { get; set; }  // Banking needs this
    public string[] Segments { get; set; }  // Marketing needs this
}
```

**✅ GOOD**: Separate models per context.
```csharp
// Identity context
public class UserAccount { public string Username; public string PasswordHash; }

// Banking context
public class BankAccount { public decimal Balance; public AccountType Type; }

// Marketing context
public class CustomerProfile { public string[] Segments; public DateTime LastContact; }
```

---

## 7. Synchronous Cross-Aggregate Communication

**What happens**: One aggregate directly calls another during its operation.

**❌ BAD**:
```csharp
public class Order
{
    public void Place()
    {
        _inventory.Reserve(Items);  // ❌ Synchronous call to another aggregate
        _payment.Charge(Total);     // ❌ What if this fails?
        Status = OrderStatus.Placed;
    }
}
```

**✅ GOOD**: Use domain events for eventual consistency.
```csharp
public class Order
{
    public void Place()
    {
        Status = OrderStatus.Placed;
        AddDomainEvent(new OrderPlaced(Id, Items, Total));
    }
}

// Separate handler
public class InventoryReservationHandler : IHandle<OrderPlaced>
{
    public async Task Handle(OrderPlaced @event)
    {
        await _inventory.Reserve(@event.OrderId, @event.Items);
    }
}
```

---

## 8. Treating Domain Events as Integration Events

**What happens**: Internal domain events published directly to message bus.

**❌ BAD**:
```csharp
// Internal domain event with implementation details
public record OrderPlaced(OrderId Id, List<OrderLine> Lines, Customer Customer);

// Published directly to Kafka - now external systems depend on internals
_kafka.Publish("orders", orderPlaced);
```

**✅ GOOD**: Translate to stable integration events.
```csharp
// Domain event (internal)
public record OrderPlaced(OrderId Id, List<OrderLine> Lines, CustomerId Customer);

// Integration event (external contract)
public record OrderPlacedIntegrationEvent(
    string OrderId,
    string CustomerId,
    decimal TotalAmount,
    DateTime PlacedAt
);

// Handler translates
public class PublishOrderPlacedHandler : IHandle<OrderPlaced>
{
    public async Task Handle(OrderPlaced @event)
    {
        var integrationEvent = new OrderPlacedIntegrationEvent(
            @event.Id.ToString(),
            @event.Customer.ToString(),
            CalculateTotal(@event.Lines),
            DateTime.UtcNow
        );
        await _bus.Publish(integrationEvent);
    }
}
```

---

## 9. Over-Engineering Simple Domains

**What happens**: Full DDD tactical patterns applied to simple CRUD operations.

**❌ BAD**: User preferences with aggregates, events, repositories...
```csharp
public class UserPreferencesAggregate
{
    public void UpdateTheme(Theme theme)
    {
        EnsureValid(theme);
        Theme = theme;
        AddDomainEvent(new ThemeUpdated(UserId, theme));
    }
}
```

**✅ GOOD**: Simple data access for simple domains.
```csharp
public class UserPreferencesService
{
    public async Task UpdateTheme(UserId userId, Theme theme)
    {
        await _db.Execute(
            "UPDATE user_preferences SET theme = @theme WHERE user_id = @userId",
            new { theme, userId });
    }
}
```

**Rule**: Apply DDD complexity proportional to domain complexity.

---

## 10. Not Including Domain Experts

**Signs of the problem**:
- "I think the business means..."
- Model based on UI mockups
- No glossary or ubiquitous language
- Developers guess at business rules
- Terms change between conversations

**Fix**:
- Schedule regular sessions with domain experts
- Build and maintain a collaborative glossary
- Have domain experts review model (pseudo-code is fine)
- When confused, ask—don't assume
- Treat mismatches as learning opportunities, not bugs
