# Event Evolution & Schema Versioning Guide

How to evolve event schemas over time without breaking your system.

## Table of Contents

1. [The Problem](#the-problem)
2. [Versioning Strategies](#versioning-strategies)
3. [Upcasting](#upcasting)
4. [Protobuf for Contracts](#protobuf-for-contracts)
5. [JSON Alternatives](#json-alternatives)
6. [Common Pitfalls](#common-pitfalls)

---

## The Problem

In event-sourced systems, **events are immutable and stored forever**. When you need to change an event's structure:

1. **Old events exist** - Can't modify stored data
2. **Replay depends on schema** - Must deserialize all historical events
3. **Multiple versions coexist** - Old events + new events in same stream
4. **Consumers vary** - Different services at different versions

### Types of Schema Changes

| Change Type | Breaking? | Examples |
|-------------|-----------|----------|
| **Add optional field** | No | Add `reason` to `OrderCancelled` |
| **Remove unused field** | Usually no | Drop deprecated field |
| **Rename field** | Yes | `customerId` → `clientId` |
| **Change field type** | Yes | `string` → `int` |
| **Change semantic meaning** | Yes | `amount` (gross → net) |

---

## Versioning Strategies

### Strategy 1: Weak Schema (Recommended)

Events are "bags of data." Unknown fields ignored; missing fields get defaults.

```csharp
// Version 1
public record OrderPlaced(string OrderId, string CustomerId, decimal Total);

// Version 2 - Added field (backward compatible)
public record OrderPlaced(
    string OrderId,
    string CustomerId,
    decimal Total,
    string? CouponCode = null);  // Optional, defaults to null
```

**Rules**: Never rename fields, new fields must have defaults, use nullable types.

---

### Strategy 2: Explicit Versioning

Each event type has explicit version; transformers convert between versions.

```csharp
public record OrderPlacedV1(string OrderId, string CustomerId, decimal Total);
public record OrderPlacedV2(string OrderId, string CustomerId, Money Total, string? CouponCode);
```

---

## Upcasting

Transform old event versions to new versions during deserialization.

```csharp
public class OrderPlacedUpcaster : IEventUpcaster
{
    public object Upcast(object @event, int fromVersion)
    {
        if (fromVersion < 2 && @event is OrderPlacedV1 v1)
        {
            return new OrderPlacedV2(
                v1.OrderId,
                v1.CustomerId,
                new Money(v1.Total, "USD"),
                null);
        }
        return @event;
    }
}

// Marten built-in upcasting
public class OrderPlacedTransformation : EventUpcaster<OrderPlacedV1, OrderPlacedV2>
{
    protected override OrderPlacedV2 Upcast(OrderPlacedV1 old) => new(
        old.OrderId, old.CustomerId, new Money(old.Total, "USD"), null);
}

options.Events.Upcast<OrderPlacedTransformation>();
```

---

## Protobuf for Contracts

**Protobuf excels** for event schemas:
- Binary format = smaller, faster
- Built-in schema evolution rules
- Cross-language support
- Field numbering prevents collisions

### Schema Design

```protobuf
syntax = "proto3";
package mycompany.orders.v1;

message OrderPlaced {
    string order_id = 1;
    string customer_id = 2;
    google.protobuf.Timestamp placed_at = 3;
    repeated OrderItem items = 4;
    Money total = 5;
    
    // Added in v2 - optional for backward compatibility
    optional string coupon_code = 6;
    
    // Reserved from removed fields
    reserved 10, 15;
    reserved "legacy_field";
}

message Money {
    int64 amount_minor_units = 1;  // Store as cents
    string currency_code = 2;
}
```

### Evolution Rules

**Safe changes**:
- Add new optional fields with new numbers
- Remove optional fields (keep number reserved)
- Add enum values

**Unsafe changes**:
- Change field numbers
- Change field types
- Rename fields

### Code-First with protobuf-net

```csharp
using ProtoBuf;

[ProtoContract]
public record OrderPlaced
{
    [ProtoMember(1)] public string OrderId { get; init; } = "";
    [ProtoMember(2)] public string CustomerId { get; init; } = "";
    [ProtoMember(3)] public DateTime PlacedAt { get; init; }
    [ProtoMember(4)] public List<OrderItem> Items { get; init; } = [];
    [ProtoMember(5)] public Money Total { get; init; } = new();
    [ProtoMember(6)] public string? CouponCode { get; init; }
}
```

---

## JSON Alternatives

JSON works for human-readable logs, debugging, simpler deployments.

### Weak Schema with System.Text.Json

```csharp
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

// Unknown fields ignored by default
```

### Capture Unknown Fields

```csharp
public record OrderPlaced
{
    public string OrderId { get; init; } = "";
    
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
```

---

## Common Pitfalls

### 1. Changing Semantic Meaning

```csharp
// BAD: Amount means different thing
// V1: amount is gross, V2: amount is net

// GOOD: Add new field
public record OrderPlaced(
    [Obsolete] decimal? Amount,  // Keep for old events
    decimal? NetAmount,
    decimal? TaxAmount);
```

### 2. Non-Nullable New Fields

```csharp
// BAD: Old events can't deserialize
public record OrderPlaced(string OrderId, string WarehouseId);  // Required

// GOOD: Nullable with default
public record OrderPlaced(string OrderId, string? WarehouseId = null);
```

### 3. Renaming Fields

```csharp
// BAD: Breaking change
// V1: customerId, V2: clientId

// GOOD: Use aliases
[JsonPropertyName("customerId")]
[JsonAlias("clientId")]
public string CustomerId { get; init; }
```

### 4. Type Changes

```csharp
// BAD: decimal → Money breaks
// GOOD: Add new field, upcast old to new
public record OrderPlaced
{
    [Obsolete] public decimal? Amount { get; init; }
    public Money? TotalAmount { get; init; }
}
```

---

## Best Practices

1. **Default to weak schema** - simplest, handles most cases
2. **Never rename fields** - add new, deprecate old
3. **Never change semantic meaning** - add new field
4. **Use protobuf for integration events** - built-in evolution
5. **Test with old events** - ensure backward compatibility
6. **Reserve removed field numbers** - prevent collisions
