# Part III: State-Based Implementation

Traditional persistence where current aggregate state is stored directly.

## Repositories

Use repositories when persisting current aggregate state (not using event sourcing). For event-sourced persistence, see [Part IV](part4-event-sourced.md).

### Repository Interface

```csharp
namespace MyDomain.Domain.Aggregates.Order;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct = default);
    Task SaveAsync(Order order, CancellationToken ct = default);
    Task DeleteAsync(Order order, CancellationToken ct = default);
}
```

### EF Core Implementation

```csharp
namespace MyDomain.Infrastructure.Persistence.EFCore;

using Microsoft.EntityFrameworkCore;

public class OrderDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();

    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id)
                .HasConversion(id => id.Value, guid => new OrderId(guid));

            entity.Property(o => o.CustomerId)
                .HasConversion(id => id.Value, guid => new CustomerId(guid));

            entity.OwnsOne(o => o.Total, money =>
            {
                money.Property(m => m.Amount).HasColumnName("TotalAmount");
                money.Property(m => m.Currency).HasConversion(
                    c => c.Code,
                    code => Currency.FromCode(code));
            });

            entity.OwnsOne(o => o.ShippingAddress);

            entity.HasMany(o => o.Lines)
                .WithOne()
                .HasForeignKey("OrderId");
        });
    }
}

public class EfCoreOrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;
    private readonly IDomainEventPublisher _eventPublisher;

    public EfCoreOrderRepository(OrderDbContext context, IDomainEventPublisher eventPublisher)
    {
        _context = context;
        _eventPublisher = eventPublisher;
    }

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct = default)
    {
        return await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task SaveAsync(Order order, CancellationToken ct = default)
    {
        var events = order.DomainEvents.ToList();
        order.ClearDomainEvents();

        var entry = _context.Entry(order);
        if (entry.State == EntityState.Detached)
        {
            var existing = await _context.Orders.FindAsync([order.Id], ct);
            if (existing is null)
                _context.Orders.Add(order);
            else
                _context.Entry(existing).CurrentValues.SetValues(order);
        }

        await _context.SaveChangesAsync(ct);
        await _eventPublisher.PublishAsync(events, ct);
    }

    public async Task DeleteAsync(Order order, CancellationToken ct = default)
    {
        _context.Orders.Remove(order);
        await _context.SaveChangesAsync(ct);
    }
}
```

### Marten Document Store Implementation

```csharp
namespace MyDomain.Infrastructure.Persistence.Marten;

using global::Marten;

// Marten document store - stores aggregate as JSON document
public class MartenOrderRepository : IOrderRepository
{
    private readonly IDocumentStore _store;
    private readonly IDomainEventPublisher _eventPublisher;

    public MartenOrderRepository(IDocumentStore store, IDomainEventPublisher eventPublisher)
    {
        _store = store;
        _eventPublisher = eventPublisher;
    }

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct = default)
    {
        await using var session = _store.QuerySession();
        return await session.LoadAsync<Order>(id.Value, ct);
    }

    public async Task SaveAsync(Order order, CancellationToken ct = default)
    {
        var events = order.DomainEvents.ToList();
        order.ClearDomainEvents();

        await using var session = _store.LightweightSession();
        session.Store(order);
        await session.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(events, ct);
    }

    public async Task DeleteAsync(Order order, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        session.Delete(order);
        await session.SaveChangesAsync(ct);
    }
}

// Marten configuration for document storage
public static class MartenDocumentConfiguration
{
    public static void ConfigureDocumentStore(this StoreOptions options)
    {
        options.Schema.For<Order>()
            .Identity(x => x.Id.Value)
            .Index(x => x.CustomerId.Value)
            .Index(x => x.Status);

        // Use System.Text.Json for serialization
        options.UseSystemTextJsonForSerialization(
            EnumStorage.AsString,
            Casing.CamelCase);
    }
}
```

---

## Simple CQRS (Optional)

For state-based systems, CQRS is optional. When read performance becomes a bottleneck, separate read models can be introduced without event sourcing.

```csharp
namespace MyDomain.Application.ReadModels;

// Simple read DTO - query directly from database
public record OrderListItem(
    Guid Id,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    DateTime PlacedAt);

// Query handler using repository/EF Core
public class OrderQueryHandler
{
    private readonly OrderDbContext _context;

    public OrderQueryHandler(OrderDbContext context) => _context = context;

    public async Task<IReadOnlyList<OrderListItem>> GetRecentOrders(int count, CancellationToken ct)
    {
        return await _context.Orders
            .OrderByDescending(o => o.PlacedAt)
            .Take(count)
            .Select(o => new OrderListItem(
                o.Id.Value,
                o.CustomerId.Value.ToString(), // Would join with Customer in real app
                o.Status.ToString(),
                o.Total.Amount,
                o.PlacedAt ?? DateTime.MinValue))
            .ToListAsync(ct);
    }
}
```

**When to introduce Simple CQRS:**
- Read queries are slow due to aggregate loading
- Different read shapes needed (lists, summaries, reports)
- Read/write ratio heavily favors reads

**When NOT needed:**
- Simple CRUD operations
- Reads can use aggregate directly
- Early in development
