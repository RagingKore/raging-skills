# Part V: Advanced Patterns

Patterns that build on either persistence strategy.

## Actor Integration

Using Akka.NET with the Decider pattern. The actor handles persistence; the Decider handles business logic.

### Aggregate Actor with Decider

```csharp
namespace MyDomain.Infrastructure.Actors;

using Akka.Actor;
using Akka.Persistence;

// Messages
public record ExecuteCommand(OrderCommand Command);
public record GetState;
public record CommandResult(bool Success, IReadOnlyList<DomainEvent> Events, string? Error = null);

/// <summary>
/// Persistent actor wrapping Decider pattern.
/// Actor handles persistence; Decider handles business logic.
/// </summary>
public class OrderActor : ReceivePersistentActor
{
    private OrderState _state = OrderState.Initial;

    public override string PersistenceId { get; }

    public OrderActor(string orderId)
    {
        PersistenceId = $"order-{orderId}";

        // Recovery: replay events to rebuild state
        Recover<DomainEvent>(@event =>
        {
            _state = OrderDecider.Evolve(_state, @event);
        });

        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is OrderState state)
                _state = state;
        });

        // Commands
        Command<ExecuteCommand>(cmd => HandleCommand(cmd.Command));
        Command<GetState>(_ => Sender.Tell(_state));
        Command<SaveSnapshotSuccess>(_ => { });
    }

    private void HandleCommand(OrderCommand command)
    {
        try
        {
            // Pure business logic - no side effects
            var events = OrderDecider.Decide(command, _state);

            // Persist events
            PersistAll(events.Cast<object>(), @event =>
            {
                _state = OrderDecider.Evolve(_state, (DomainEvent)@event);

                // Snapshot every N events for faster recovery
                if (LastSequenceNr % 100 == 0)
                    SaveSnapshot(_state);
            });

            Sender.Tell(new CommandResult(true, events));
        }
        catch (Exception ex)
        {
            Sender.Tell(new CommandResult(false, [], ex.Message));
        }
    }

    public static Props Props(string orderId) =>
        Akka.Actor.Props.Create(() => new OrderActor(orderId));
}

// Actor system setup
public class OrderActorSystem
{
    private readonly ActorSystem _system;
    private readonly IActorRef _orderSupervisor;

    public OrderActorSystem()
    {
        _system = ActorSystem.Create("orders");
        _orderSupervisor = _system.ActorOf(
            Props.Create(() => new OrderSupervisor()),
            "order-supervisor");
    }

    public async Task<CommandResult> SendCommand(OrderId orderId, OrderCommand command)
    {
        var actor = await _orderSupervisor.Ask<IActorRef>(
            new GetOrCreateOrder(orderId));

        return await actor.Ask<CommandResult>(
            new ExecuteCommand(command),
            TimeSpan.FromSeconds(5));
    }
}

public class OrderSupervisor : ReceiveActor
{
    private readonly Dictionary<OrderId, IActorRef> _orders = new();

    public OrderSupervisor()
    {
        Receive<GetOrCreateOrder>(msg =>
        {
            if (!_orders.TryGetValue(msg.OrderId, out var actor))
            {
                actor = Context.ActorOf(
                    OrderActor.Props(msg.OrderId.Value.ToString()),
                    $"order-{msg.OrderId}");
                _orders[msg.OrderId] = actor;
            }

            Sender.Tell(actor);
        });
    }
}

public record GetOrCreateOrder(OrderId OrderId);
```

### When to Use Actors

| Use Case | Actors Add Value |
|----------|------------------|
| High concurrency per aggregate | Yes - serializes access |
| Distributed systems | Yes - location transparency |
| Complex lifecycle management | Yes - supervision trees |
| Simple CRUD | No - unnecessary complexity |
| Low traffic | No - overhead not justified |

See [actors-ddd-guide.md](../actors-ddd-guide.md) for comprehensive Actor + DDD patterns.
