# Context Mapping Patterns

## Table of Contents

- [Partnership](#partnership)
- [Shared Kernel](#shared-kernel)
- [Customer-Supplier](#customer-supplier)
- [Conformist](#conformist)
- [Anti-Corruption Layer (ACL)](#anti-corruption-layer-acl)
- [Open Host Service](#open-host-service)
- [Published Language](#published-language)
- [Separate Ways](#separate-ways)

---

## Partnership

**Relationship**: Two teams with mutual dependency, shared success/failure.

**Use when**: Teams can coordinate closely, releases are synchronized, both benefit equally.

**Example**: Payment team and Order team jointly own the checkout flow.

```
┌─────────────────┐         ┌─────────────────┐
│   Order Team    │◄───────►│  Payment Team   │
│                 │ Partner │                 │
│ Checkout Flow   │   🤝    │ Payment Process │
└─────────────────┘         └─────────────────┘

- Joint planning sessions
- Shared integration tests
- Synchronized releases
- Both teams can modify shared interface
```

**Risks**: Coordination overhead, both teams blocked if one is busy.

---

## Shared Kernel

**Relationship**: Small, carefully managed shared code between contexts.

**Use when**: A few core concepts truly shared and stable.

**Example**: Money value object shared between Billing and Accounting.

```csharp
// Shared Kernel - owned by both teams, changes require agreement
namespace SharedKernel;

public readonly record struct Money(decimal Amount, Currency Currency)
{
    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new CurrencyMismatchException();
        return new Money(a.Amount + b.Amount, a.Currency);
    }
}
```

**Risks**:
- Kernel grows too large
- Changes require coordination across teams
- Tight coupling disguised as sharing

**Rules**:
- Keep it SMALL (< 5% of codebase)
- Changes require approval from ALL consuming teams
- Test coverage must be extensive

---

## Customer-Supplier

**Relationship**: Upstream (supplier) provides, downstream (customer) consumes.

**Use when**: Clear asymmetric dependency; upstream can accommodate downstream needs.

**Example**: Product Catalog (upstream) supplies data to Recommendation Engine (downstream).

```
┌─────────────────┐         ┌─────────────────┐
│   Product       │         │  Recommendation │
│   Catalog       │────────►│     Engine      │
│   (Upstream)    │ Supplier│   (Downstream)  │
└─────────────────┘         └─────────────────┘

- Downstream can request features
- Upstream prioritizes based on relationship
- Downstream has some negotiation power
- Clear API contract
```

---

## Conformist

**Relationship**: Downstream adopts upstream model without negotiation.

**Use when**: No leverage over upstream (external API, legacy system, platform).

**Example**: Your system conforms to Stripe's payment model.

```csharp
// You conform to Stripe's concepts - no negotiation
public class PaymentService
{
    public async Task<PaymentResult> ProcessPayment(Order order)
    {
        // We use Stripe's terminology and structure
        var paymentIntent = await _stripe.PaymentIntents.CreateAsync(
            new PaymentIntentCreateOptions
            {
                Amount = (long)(order.Total.Amount * 100), // Stripe wants cents
                Currency = order.Total.Currency.Code.ToLower(), // Stripe format
                PaymentMethod = order.PaymentMethodId,
            });

        // Translate back to our domain
        return MapToPaymentResult(paymentIntent);
    }
}
```

**Risks**: Your domain becomes polluted with external concepts.

---

## Anti-Corruption Layer (ACL)

**Relationship**: Translation layer protects your model from external/legacy systems.

**Use when**: Integrating with legacy, external APIs, or systems you don't want to conform to.

**Example**: Integrating legacy mainframe with modern domain model.

```csharp
// ❌ WITHOUT ACL: Legacy concepts leak into domain
public class OrderService
{
    public void CreateOrder(LegacyCustomerRecord custRec, LegacyItemList items)
    {
        // Legacy concepts everywhere
        var order = new Order
        {
            CustNum = custRec.CUST_NUM,  // Legacy naming
            OrdDt = custRec.ORD_DATE,    // Legacy format
        };
    }
}

// ✅ WITH ACL: Clean boundary
public class LegacyOrderAdapter : IOrderAdapter
{
    private readonly LegacySystemClient _legacy;

    public Order TranslateFromLegacy(string legacyOrderId)
    {
        var legacyOrder = _legacy.GetOrder(legacyOrderId);

        return new Order(
            id: OrderId.FromLegacy(legacyOrder.ORDNUM),
            customer: TranslateCustomer(legacyOrder.CUST_REC),
            items: legacyOrder.ITEMS.Select(TranslateItem).ToList(),
            placedAt: ParseLegacyDate(legacyOrder.ORD_DATE)
        );
    }

    private Customer TranslateCustomer(LegacyCustomerRecord rec)
    {
        return new Customer(
            CustomerId.New(),
            new PersonName(rec.FNAME, rec.LNAME),
            new EmailAddress(rec.EMAIL_ADDR)
        );
    }
}
```

**Structure**:
```
┌─────────────────────────────────────────────────────────────┐
│                    Your Domain Model                         │
│  (Clean, uses ubiquitous language)                          │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│              Anti-Corruption Layer                           │
│  - Translators / Adapters                                   │
│  - Facades                                                  │
│  - Mappers                                                  │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                  Legacy / External System                    │
│  (Whatever mess exists there)                               │
└─────────────────────────────────────────────────────────────┘
```

---

## Open Host Service

**Relationship**: Published API/protocol for multiple consumers.

**Use when**: Multiple downstream systems need to integrate with you.

**Example**: Order Service exposes REST/gRPC API for multiple consumers.

```protobuf
// Published service definition - stable contract
service OrderService {
    rpc GetOrder(GetOrderRequest) returns (OrderResponse);
    rpc PlaceOrder(PlaceOrderCommand) returns (OrderPlacedResponse);
    rpc CancelOrder(CancelOrderCommand) returns (OrderCancelledResponse);
}

message OrderResponse {
    string order_id = 1;
    OrderStatus status = 2;
    repeated OrderLineItem items = 3;
    Money total = 4;
}
```

**Rules**:
- Versioning strategy required
- Changes must be backward-compatible
- Clear deprecation policy
- Good documentation essential

---

## Published Language

**Relationship**: Shared data interchange format (often combined with Open Host).

**Use when**: Standard data format needed for integration.

**Example**: Integration events use protobuf with schema registry.

```protobuf
// Published language - schema in shared registry
message OrderPlaced {
    string order_id = 1;
    string customer_id = 2;
    google.protobuf.Timestamp placed_at = 3;
    repeated OrderItem items = 4;
    Money total_amount = 5;
}
```

---

## Separate Ways

**Relationship**: No integration—contexts are truly independent.

**Use when**: Cost of integration exceeds benefit; contexts genuinely independent.

**Example**: HR system and Product Catalog have no business reason to integrate.

**Signs this is the right choice**:
- No shared data needed
- No workflows cross boundaries
- Teams never need to coordinate
- Duplication is acceptable or minimal
