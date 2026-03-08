# Projections Guide for Event-Sourced Systems

This guide covers projection patterns for building read models from event streams, focusing on Marten for .NET, PostgreSQL for persistence, and DuckDB for analytics.

## What Are Projections?

Projections transform event streams into read-optimized views. They answer the question: "Given all these events, what is the current state?"

```
Events (Write Side)              Projections (Read Side)
─────────────────────            ─────────────────────────
OrderCreated ─────────┐
OrderItemAdded ───────┼────────► OrderSummaryView
OrderItemAdded ───────┤          { orderId, customerName,
OrderShipped ─────────┘            itemCount, total, status }
```

## Projection Types

### 1. Inline Projections (Synchronous)
Updated in the same transaction as events. Strong consistency, but slower writes.

### 2. Async Projections (Eventually Consistent)
Updated by a background daemon. Fast writes, but slight delay in read model updates.

### 3. Live Projections (On-Demand)
Computed at query time by replaying events. Always current, but expensive for long streams.

---

## Marten Projections

Marten provides first-class projection support with PostgreSQL as the backing store.

### Single Stream Projection

Aggregates events from a single stream into one document:

```csharp
// The read model
public record OrderSummary(
    Guid Id,
    string CustomerName,
    List<string> Items,
    decimal Total,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ShippedAt
);

// The projection
public class OrderSummaryProjection : SingleStreamProjection<OrderSummary>
{
    public OrderSummary Create(OrderCreated @event) =>
        new(
            Id: @event.OrderId,
            CustomerName: @event.CustomerName,
            Items: [],
            Total: 0m,
            Status: "Created",
            CreatedAt: @event.OccurredAt,
            ShippedAt: null
        );

    public OrderSummary Apply(OrderItemAdded @event, OrderSummary current) =>
        current with
        {
            Items = [..current.Items, @event.ProductName],
            Total = current.Total + @event.Price
        };

    public OrderSummary Apply(OrderShipped @event, OrderSummary current) =>
        current with
        {
            Status = "Shipped",
            ShippedAt = @event.ShippedAt
        };

    public OrderSummary Apply(OrderCancelled @event, OrderSummary current) =>
        current with { Status = "Cancelled" };
}
```

### Multi-Stream Projection

Aggregates events from multiple streams into one or more documents:

```csharp
// Read model spanning multiple aggregates
public record CustomerDashboard(
    Guid CustomerId,
    string Name,
    int TotalOrders,
    decimal TotalSpent,
    DateTimeOffset? LastOrderDate,
    List<Guid> RecentOrderIds
);

public class CustomerDashboardProjection : MultiStreamProjection<CustomerDashboard, Guid>
{
    public CustomerDashboardProjection()
    {
        // Identity comes from events, not stream ID
        Identity<CustomerRegistered>(e => e.CustomerId);
        Identity<OrderCreated>(e => e.CustomerId);
        Identity<OrderCompleted>(e => e.CustomerId);
    }

    public CustomerDashboard Create(CustomerRegistered @event) =>
        new(
            CustomerId: @event.CustomerId,
            Name: @event.Name,
            TotalOrders: 0,
            TotalSpent: 0m,
            LastOrderDate: null,
            RecentOrderIds: []
        );

    public CustomerDashboard Apply(OrderCreated @event, CustomerDashboard current) =>
        current with
        {
            TotalOrders = current.TotalOrders + 1,
            LastOrderDate = @event.OccurredAt,
            RecentOrderIds = [..current.RecentOrderIds.TakeLast(9), @event.OrderId]
        };

    public CustomerDashboard Apply(OrderCompleted @event, CustomerDashboard current) =>
        current with
        {
            TotalSpent = current.TotalSpent + @event.Total
        };
}
```

### Registering Projections

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMarten(options =>
{
    options.Connection(connectionString);
    
    // Inline: same transaction as events
    options.Projections.Add<OrderSummaryProjection>(ProjectionLifecycle.Inline);
    
    // Async: background daemon updates
    options.Projections.Add<CustomerDashboardProjection>(ProjectionLifecycle.Async);
})
.AddAsyncDaemon(DaemonMode.HotCold); // Required for async projections
```

### Projection Lifecycle Options

| Lifecycle | Consistency | Write Speed | Use Case |
|-----------|-------------|-------------|----------|
| `Inline` | Strong | Slower | Critical read models, must be consistent |
| `Async` | Eventual | Fast | Dashboards, reports, search indexes |
| `Live` | Strong | N/A | Rarely queried, long streams acceptable |

### Async Daemon Configuration

```csharp
builder.Services.AddMarten(options =>
{
    options.Connection(connectionString);
    
    options.Projections.Add<CustomerDashboardProjection>(ProjectionLifecycle.Async);
    
    // Daemon configuration
    options.Projections.DaemonLockId = 12345; // Unique per app instance
    options.Projections.AsyncMode = DaemonMode.HotCold;
    
    // Performance tuning
    options.Projections.StaleSequenceThreshold = TimeSpan.FromSeconds(5);
})
.AddAsyncDaemon(DaemonMode.HotCold);
```

**Daemon Modes:**
- `HotCold`: One active instance, others on standby (recommended for production)
- `Solo`: Single instance only
- `Disabled`: No daemon (for testing)

### Rebuilding Projections

When projection logic changes, rebuild from scratch:

```csharp
// During deployment or maintenance
await using var store = DocumentStore.For(options);

// Rebuild specific projection
await store.Advanced.RebuildProjectionAsync<OrderSummaryProjection>(CancellationToken.None);

// Or rebuild by document type
await store.Advanced.RebuildProjectionAsync(
    typeof(CustomerDashboard).Name, 
    CancellationToken.None
);
```

### Live Aggregation

For on-demand computation without persisting:

```csharp
await using var session = store.LightweightSession();

// Aggregate events on the fly
var order = await session.Events
    .AggregateStreamAsync<Order>(orderId);

// Or with a custom projection
var summary = await session.Events
    .AggregateStreamAsync<OrderSummary>(
        orderId,
        timestamp: DateTimeOffset.UtcNow.AddDays(-30) // Point-in-time
    );
```

---

## Custom Projections with Raw Events

For complex scenarios requiring full control:

```csharp
public class InventoryLedgerProjection : IProjection
{
    public void Apply(
        IDocumentOperations operations,
        IReadOnlyList<StreamAction> streams)
    {
        foreach (var stream in streams)
        {
            foreach (var @event in stream.Events)
            {
                switch (@event.Data)
                {
                    case InventoryReceived received:
                        ApplyReceived(operations, received);
                        break;
                    case InventoryShipped shipped:
                        ApplyShipped(operations, shipped);
                        break;
                }
            }
        }
    }

    private void ApplyReceived(IDocumentOperations ops, InventoryReceived @event)
    {
        var ledger = ops.Load<InventoryLedger>(@event.Sku) 
            ?? new InventoryLedger(@event.Sku, 0, []);
        
        ops.Store(ledger with
        {
            QuantityOnHand = ledger.QuantityOnHand + @event.Quantity,
            Transactions = [..ledger.Transactions, new("Received", @event.Quantity, DateTimeOffset.UtcNow)]
        });
    }

    private void ApplyShipped(IDocumentOperations ops, InventoryShipped @event)
    {
        var ledger = ops.Load<InventoryLedger>(@event.Sku);
        if (ledger is null) return;
        
        ops.Store(ledger with
        {
            QuantityOnHand = ledger.QuantityOnHand - @event.Quantity,
            Transactions = [..ledger.Transactions, new("Shipped", -@event.Quantity, DateTimeOffset.UtcNow)]
        });
    }

    public Task ApplyAsync(
        IDocumentOperations operations,
        IReadOnlyList<StreamAction> streams,
        CancellationToken ct)
    {
        Apply(operations, streams);
        return Task.CompletedTask;
    }
}
```

---

## PostgreSQL Read Models

### Direct Table Projections

For high-performance queries, project directly to PostgreSQL tables:

```csharp
public class OrderSearchProjection : IProjection
{
    public async Task ApplyAsync(
        IDocumentOperations operations,
        IReadOnlyList<StreamAction> streams,
        CancellationToken ct)
    {
        // Access underlying Npgsql connection
        var connection = operations.As<IMartenSession>().Connection;
        
        foreach (var stream in streams)
        {
            foreach (var @event in stream.Events)
            {
                if (@event.Data is OrderCreated created)
                {
                    await connection.ExecuteAsync(
                        """
                        INSERT INTO order_search (order_id, customer_name, created_at, status)
                        VALUES (@orderId, @customerName, @createdAt, 'Created')
                        ON CONFLICT (order_id) DO UPDATE SET
                            customer_name = EXCLUDED.customer_name
                        """,
                        new
                        {
                            orderId = created.OrderId,
                            customerName = created.CustomerName,
                            createdAt = created.OccurredAt
                        });
                }
                else if (@event.Data is OrderShipped shipped)
                {
                    await connection.ExecuteAsync(
                        """
                        UPDATE order_search
                        SET status = 'Shipped', shipped_at = @shippedAt
                        WHERE order_id = @orderId
                        """,
                        new { orderId = shipped.OrderId, shippedAt = shipped.ShippedAt });
                }
            }
        }
    }

    public void Apply(IDocumentOperations operations, IReadOnlyList<StreamAction> streams)
        => ApplyAsync(operations, streams, CancellationToken.None).GetAwaiter().GetResult();
}
```

### Materialized Views for Reporting

```sql
-- Create materialized view for sales reports
CREATE MATERIALIZED VIEW daily_sales_summary AS
SELECT 
    date_trunc('day', (data->>'occurredAt')::timestamptz) AS sale_date,
    COUNT(*) AS order_count,
    SUM((data->>'total')::decimal) AS total_revenue,
    AVG((data->>'total')::decimal) AS average_order_value
FROM mt_events
WHERE type = 'OrderCompleted'
GROUP BY date_trunc('day', (data->>'occurredAt')::timestamptz)
ORDER BY sale_date DESC;

-- Create index for fast queries
CREATE UNIQUE INDEX idx_daily_sales_date ON daily_sales_summary(sale_date);

-- Refresh on schedule (via pg_cron or application)
REFRESH MATERIALIZED VIEW CONCURRENTLY daily_sales_summary;
```

### Refresh Strategies

```csharp
public class MaterializedViewRefresher : BackgroundService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<MaterializedViewRefresher> _logger;

    public MaterializedViewRefresher(
        NpgsqlDataSource dataSource,
        ILogger<MaterializedViewRefresher> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await _dataSource.OpenConnectionAsync(stoppingToken);
                await connection.ExecuteAsync(
                    "REFRESH MATERIALIZED VIEW CONCURRENTLY daily_sales_summary");
                
                _logger.LogInformation("Refreshed daily_sales_summary materialized view");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh materialized view");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

---

## DuckDB for Analytics

DuckDB excels at analytical queries over event data. Use it for complex aggregations, time-series analysis, and ad-hoc reporting.

### When to Use DuckDB

| Use Case | PostgreSQL | DuckDB |
|----------|------------|--------|
| OLTP read models | ✅ Primary | ❌ |
| Simple aggregations | ✅ | ✅ |
| Complex analytics | ⚠️ Slower | ✅ Primary |
| Time-series analysis | ⚠️ | ✅ |
| Ad-hoc exploration | ⚠️ | ✅ |
| Real-time dashboards | ✅ | ⚠️ Batch |

### Setting Up DuckDB Projection

```csharp
public class AnalyticsProjection : IAsyncDisposable
{
    private readonly DuckDBConnection _connection;

    public AnalyticsProjection(string databasePath)
    {
        _connection = new DuckDBConnection($"Data Source={databasePath}");
        _connection.Open();
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        _connection.Execute("""
            CREATE TABLE IF NOT EXISTS order_events (
                event_id UUID PRIMARY KEY,
                stream_id UUID NOT NULL,
                event_type VARCHAR NOT NULL,
                occurred_at TIMESTAMPTZ NOT NULL,
                customer_id UUID,
                product_sku VARCHAR,
                quantity INTEGER,
                unit_price DECIMAL(10,2),
                total DECIMAL(10,2),
                data JSON
            );
            
            CREATE INDEX IF NOT EXISTS idx_order_events_occurred 
            ON order_events(occurred_at);
            
            CREATE INDEX IF NOT EXISTS idx_order_events_customer 
            ON order_events(customer_id);
        """);
    }

    public async Task AppendEventsAsync(IEnumerable<IEvent> events)
    {
        using var appender = _connection.CreateAppender("order_events");
        
        foreach (var @event in events)
        {
            var row = appender.CreateRow();
            row.AppendValue(@event.Id);
            row.AppendValue(@event.StreamId);
            row.AppendValue(@event.Data.GetType().Name);
            row.AppendValue(@event.Timestamp);
            
            // Extract common fields based on event type
            switch (@event.Data)
            {
                case OrderCreated created:
                    row.AppendValue(created.CustomerId);
                    row.AppendNullValue(); // product_sku
                    row.AppendNullValue(); // quantity
                    row.AppendNullValue(); // unit_price
                    row.AppendNullValue(); // total
                    break;
                    
                case OrderItemAdded item:
                    row.AppendNullValue(); // customer_id
                    row.AppendValue(item.ProductSku);
                    row.AppendValue(item.Quantity);
                    row.AppendValue(item.UnitPrice);
                    row.AppendValue(item.Quantity * item.UnitPrice);
                    break;
                    
                default:
                    row.AppendNullValue();
                    row.AppendNullValue();
                    row.AppendNullValue();
                    row.AppendNullValue();
                    row.AppendNullValue();
                    break;
            }
            
            row.AppendValue(JsonSerializer.Serialize(@event.Data));
            row.EndRow();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
```

### Analytical Queries

```csharp
public class AnalyticsQueryService
{
    private readonly DuckDBConnection _connection;

    // Revenue by product over time
    public async Task<IReadOnlyList<ProductRevenue>> GetProductRevenueAsync(
        DateTimeOffset from,
        DateTimeOffset to)
    {
        return (await _connection.QueryAsync<ProductRevenue>("""
            SELECT 
                product_sku,
                date_trunc('day', occurred_at) AS day,
                SUM(quantity) AS units_sold,
                SUM(total) AS revenue
            FROM order_events
            WHERE event_type = 'OrderItemAdded'
              AND occurred_at BETWEEN @from AND @to
            GROUP BY product_sku, date_trunc('day', occurred_at)
            ORDER BY day DESC, revenue DESC
        """, new { from, to })).ToList();
    }

    // Customer cohort analysis
    public async Task<IReadOnlyList<CohortMetrics>> GetCohortAnalysisAsync()
    {
        return (await _connection.QueryAsync<CohortMetrics>("""
            WITH first_orders AS (
                SELECT 
                    customer_id,
                    date_trunc('month', MIN(occurred_at)) AS cohort_month
                FROM order_events
                WHERE event_type = 'OrderCreated'
                GROUP BY customer_id
            ),
            order_months AS (
                SELECT 
                    o.customer_id,
                    f.cohort_month,
                    date_trunc('month', o.occurred_at) AS order_month,
                    datediff('month', f.cohort_month, date_trunc('month', o.occurred_at)) AS months_since_first
                FROM order_events o
                JOIN first_orders f ON o.customer_id = f.customer_id
                WHERE o.event_type = 'OrderCreated'
            )
            SELECT 
                cohort_month,
                months_since_first,
                COUNT(DISTINCT customer_id) AS customers,
                COUNT(*) AS orders
            FROM order_months
            GROUP BY cohort_month, months_since_first
            ORDER BY cohort_month, months_since_first
        """)).ToList();
    }

    // Moving averages and trends
    public async Task<IReadOnlyList<SalesTrend>> GetSalesTrendsAsync(int windowDays = 7)
    {
        return (await _connection.QueryAsync<SalesTrend>($$"""
            WITH daily_sales AS (
                SELECT 
                    date_trunc('day', occurred_at)::DATE AS sale_date,
                    COUNT(*) AS orders,
                    SUM(total) AS revenue
                FROM order_events
                WHERE event_type = 'OrderItemAdded'
                GROUP BY date_trunc('day', occurred_at)::DATE
            )
            SELECT 
                sale_date,
                orders,
                revenue,
                AVG(revenue) OVER (
                    ORDER BY sale_date 
                    ROWS BETWEEN {{windowDays - 1}} PRECEDING AND CURRENT ROW
                ) AS moving_avg_revenue,
                SUM(revenue) OVER (
                    ORDER BY sale_date
                ) AS cumulative_revenue
            FROM daily_sales
            ORDER BY sale_date DESC
            LIMIT 90
        """)).ToList();
    }
}
```

### Syncing from PostgreSQL/Marten

```csharp
public class DuckDBSyncService : BackgroundService
{
    private readonly IDocumentStore _martenStore;
    private readonly AnalyticsProjection _analytics;
    private long _lastProcessedSequence;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncNewEventsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task SyncNewEventsAsync(CancellationToken ct)
    {
        await using var session = _martenStore.LightweightSession();
        
        // Fetch events after last processed sequence
        var newEvents = await session
            .Query<IEvent>()
            .Where(e => e.Sequence > _lastProcessedSequence)
            .OrderBy(e => e.Sequence)
            .Take(1000)
            .ToListAsync(ct);

        if (newEvents.Count == 0) return;

        await _analytics.AppendEventsAsync(newEvents);
        
        _lastProcessedSequence = newEvents.Max(e => e.Sequence);
    }
}
```

---

## Projection Patterns

### Event Enrichment

Add computed fields during projection:

```csharp
public class EnrichedOrderProjection : SingleStreamProjection<EnrichedOrder>
{
    public EnrichedOrder Create(OrderCreated @event) =>
        new(
            Id: @event.OrderId,
            CustomerName: @event.CustomerName,
            Items: [],
            Subtotal: 0m,
            Tax: 0m,
            Total: 0m,
            ItemCount: 0,
            AverageItemPrice: 0m
        );

    public EnrichedOrder Apply(OrderItemAdded @event, EnrichedOrder current)
    {
        var newItems = current.Items.Append(new OrderLineItem(
            @event.ProductSku,
            @event.ProductName,
            @event.Quantity,
            @event.UnitPrice
        )).ToList();
        
        var subtotal = newItems.Sum(i => i.Quantity * i.UnitPrice);
        var tax = subtotal * 0.08m; // 8% tax
        
        return current with
        {
            Items = newItems,
            Subtotal = subtotal,
            Tax = tax,
            Total = subtotal + tax,
            ItemCount = newItems.Sum(i => i.Quantity),
            AverageItemPrice = newItems.Count > 0 
                ? subtotal / newItems.Sum(i => i.Quantity) 
                : 0m
        };
    }
}
```

### Snapshot Optimization

For aggregates with many events, periodically snapshot:

```csharp
public class SnapshotOptimizedProjection : SingleStreamProjection<AccountBalance>
{
    // Marten automatically handles snapshots when configured
    public SnapshotOptimizedProjection()
    {
        // Create snapshot every 100 events
        Snapshot(SnapshotLifecycle.Inline, 100);
    }

    public AccountBalance Create(AccountOpened @event) =>
        new(@event.AccountId, @event.InitialBalance, 0);

    public AccountBalance Apply(MoneyDeposited @event, AccountBalance current) =>
        current with
        {
            Balance = current.Balance + @event.Amount,
            TransactionCount = current.TransactionCount + 1
        };

    public AccountBalance Apply(MoneyWithdrawn @event, AccountBalance current) =>
        current with
        {
            Balance = current.Balance - @event.Amount,
            TransactionCount = current.TransactionCount + 1
        };
}
```

### Partitioned Projections

For multi-tenant or high-volume scenarios:

```csharp
public class TenantPartitionedProjection : MultiStreamProjection<TenantMetrics, string>
{
    public TenantPartitionedProjection()
    {
        // Partition by tenant ID
        Identity<TenantEvent>(e => e.TenantId);
        
        // Store in tenant-specific documents
        CustomGrouping(new TenantGrouper());
    }

    private class TenantGrouper : IAggregateGrouper<string>
    {
        public async Task Group(
            IQuerySession session,
            IEnumerable<IEvent> events,
            ITenantSliceGroup<string> grouping)
        {
            foreach (var @event in events)
            {
                if (@event.Data is TenantEvent tenantEvent)
                {
                    grouping.AddEvent(tenantEvent.TenantId, @event);
                }
            }
        }
    }
}
```

---

## Testing Projections

```csharp
public class OrderSummaryProjectionTests
{
    [Test]
    public async Task Projects_OrderCreated_ToInitialSummary()
    {
        // Arrange
        await using var store = DocumentStore.For(opts =>
        {
            opts.Connection("Host=localhost;Database=test_db");
            opts.Projections.Add<OrderSummaryProjection>(ProjectionLifecycle.Inline);
        });
        
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        
        var orderId = Guid.NewGuid();
        var created = new OrderCreated(orderId, "John Doe", DateTimeOffset.UtcNow);

        // Act
        await using var session = store.LightweightSession();
        session.Events.StartStream<Order>(orderId, created);
        await session.SaveChangesAsync();

        // Assert
        var summary = await session.LoadAsync<OrderSummary>(orderId);
        
        await Assert.That(summary).IsNotNull();
        await Assert.That(summary!.CustomerName).IsEqualTo("John Doe");
        await Assert.That(summary.Items).IsEmpty();
        await Assert.That(summary.Status).IsEqualTo("Created");
    }

    [Test]
    public async Task Projects_MultipleItems_CalculatesTotal()
    {
        // Arrange
        await using var store = DocumentStore.For(opts =>
        {
            opts.Connection("Host=localhost;Database=test_db");
            opts.Projections.Add<OrderSummaryProjection>(ProjectionLifecycle.Inline);
        });
        
        var orderId = Guid.NewGuid();
        var events = new object[]
        {
            new OrderCreated(orderId, "Jane Doe", DateTimeOffset.UtcNow),
            new OrderItemAdded(orderId, "SKU-1", "Widget", 2, 10.00m),
            new OrderItemAdded(orderId, "SKU-2", "Gadget", 1, 25.00m)
        };

        // Act
        await using var session = store.LightweightSession();
        session.Events.StartStream<Order>(orderId, events);
        await session.SaveChangesAsync();

        // Assert
        var summary = await session.LoadAsync<OrderSummary>(orderId);
        
        await Assert.That(summary!.Total).IsEqualTo(45.00m); // 2*10 + 1*25
        await Assert.That(summary.Items).HasCount().EqualTo(2);
    }

    [Test]
    public async Task Rebuilds_Projection_FromScratch()
    {
        // Arrange
        await using var store = DocumentStore.For(opts =>
        {
            opts.Connection("Host=localhost;Database=test_db");
            opts.Projections.Add<OrderSummaryProjection>(ProjectionLifecycle.Inline);
        });
        
        // ... create some events ...

        // Act
        await store.Advanced.RebuildProjectionAsync<OrderSummaryProjection>(CancellationToken.None);

        // Assert
        // Verify projections are rebuilt correctly
    }
}
```

---

## Common Mistakes

### ❌ Querying Events Instead of Projections

```csharp
// BAD: Replays all events on every query
public async Task<decimal> GetOrderTotal(Guid orderId)
{
    var events = await session.Events.FetchStreamAsync(orderId);
    return events
        .Select(e => e.Data)
        .OfType<OrderItemAdded>()
        .Sum(e => e.Price);
}
```

### ✅ Query Pre-Built Projections

```csharp
// GOOD: Fast lookup of pre-computed state
public async Task<decimal> GetOrderTotal(Guid orderId)
{
    var summary = await session.LoadAsync<OrderSummary>(orderId);
    return summary?.Total ?? 0m;
}
```

### ❌ Blocking on Async Projections

```csharp
// BAD: Assumes immediate consistency
await session.SaveChangesAsync();
var dashboard = await session.LoadAsync<CustomerDashboard>(customerId);
// dashboard might not include the just-saved event!
```

### ✅ Account for Eventual Consistency

```csharp
// GOOD: Either use inline projections or handle eventual consistency
// Option 1: Use inline projection for critical reads
options.Projections.Add<OrderSummaryProjection>(ProjectionLifecycle.Inline);

// Option 2: Return command result, let UI poll/subscribe for updates
return new OrderCreatedResponse(orderId, expectedTotal);
```

### ❌ Fat Projections with Everything

```csharp
// BAD: One projection trying to serve all use cases
public record MegaOrderView(
    Guid Id,
    string CustomerName,
    List<OrderLineItem> Items,
    List<OrderStatusChange> StatusHistory,
    List<PaymentAttempt> Payments,
    ShippingDetails Shipping,
    CustomerDetails Customer,
    List<RelatedOrder> RelatedOrders,
    // ... 50 more fields
);
```

### ✅ Purpose-Built Projections

```csharp
// GOOD: Separate projections for separate use cases
public record OrderListItem(Guid Id, string CustomerName, decimal Total, string Status);
public record OrderDetails(Guid Id, string CustomerName, List<OrderLineItem> Items, decimal Total);
public record OrderShipmentView(Guid Id, ShippingDetails Shipping, string Status);
public record OrderPaymentView(Guid Id, List<PaymentAttempt> Payments, decimal AmountDue);
```

---

## Summary

| Pattern | Technology | Consistency | Best For |
|---------|------------|-------------|----------|
| SingleStreamProjection | Marten | Inline or Async | Per-aggregate read models |
| MultiStreamProjection | Marten | Inline or Async | Cross-aggregate views |
| Custom Table | PostgreSQL | Inline | High-performance queries |
| Materialized View | PostgreSQL | Scheduled | Reports, dashboards |
| Analytical Store | DuckDB | Batch | Complex analytics, exploration |

**Key Principles:**
1. Build projections for specific query needs, not generic views
2. Choose consistency level based on business requirements
3. Use inline for critical reads, async for scalability
4. Consider DuckDB for complex analytics over PostgreSQL
5. Test projection logic with real event sequences
6. Plan for rebuilds when projection logic changes
