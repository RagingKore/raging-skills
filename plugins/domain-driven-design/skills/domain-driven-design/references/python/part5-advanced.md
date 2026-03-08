# Part V: Advanced Patterns

Patterns that build on either persistence strategy.

## Actor Integration

See [actors-ddd-guide.md](../actors-ddd-guide.md) for comprehensive Actor + DDD patterns.

### With Ray (Distributed Actors)

```python
import ray
from typing import List

@ray.remote
class OrderActor:
    def __init__(self, order_id: str):
        self._order_id = OrderId.from_str(order_id)
        self._decider = OrderDecider()
        self._events: List[OrderEvent] = []
        self._state = self._decider.initial_state

    async def handle(self, command: OrderCommand) -> List[OrderEvent]:
        """Process command and return new events."""
        new_events = list(self._decider.decide(command, self._state))

        # Apply events to state
        for event in new_events:
            self._state = self._decider.evolve(self._state, event)
            self._events.append(event)

        return new_events

    async def get_state(self) -> OrderState:
        return self._state

    async def get_events(self) -> List[OrderEvent]:
        return self._events.copy()


# Usage
ray.init()

# Create actor (one per aggregate)
order_actor = OrderActor.remote("order-123")

# Send command
events = await order_actor.handle.remote(
    CreateOrder(OrderId.from_str("order-123"), CustomerId.new())
)

# Query state
state = await order_actor.get_state.remote()
```

### With Thespian (Local/Distributed)

```python
from thespian.actors import Actor, ActorSystem

class OrderActorMessage:
    pass

class HandleCommand(OrderActorMessage):
    def __init__(self, command: OrderCommand):
        self.command = command

class GetState(OrderActorMessage):
    pass

class CommandResult(OrderActorMessage):
    def __init__(self, events: List[OrderEvent]):
        self.events = events

class StateResult(OrderActorMessage):
    def __init__(self, state: OrderState):
        self.state = state


class OrderActor(Actor):
    def __init__(self):
        self._decider = OrderDecider()
        self._state = self._decider.initial_state
        self._events = []

    def receiveMessage(self, msg, sender):
        if isinstance(msg, HandleCommand):
            try:
                new_events = list(self._decider.decide(msg.command, self._state))
                for event in new_events:
                    self._state = self._decider.evolve(self._state, event)
                    self._events.append(event)
                self.send(sender, CommandResult(new_events))
            except Exception as e:
                self.send(sender, CommandResult([]))

        elif isinstance(msg, GetState):
            self.send(sender, StateResult(self._state))


# Usage
system = ActorSystem()
order_actor = system.createActor(OrderActor)

# Send command
result = system.ask(
    order_actor,
    HandleCommand(CreateOrder(OrderId.new(), CustomerId.new())),
    timeout=timedelta(seconds=5)
)
```

### Actor Supervision for Read Side

```python
@ray.remote
class OrderProjectionActor:
    """
    Subscribes to order events and maintains read model.
    """
    def __init__(self, read_db: ReadDatabase):
        self._read_db = read_db
        self._projector = OrderSummaryProjector(read_db)

    async def handle_event(self, event: OrderEvent) -> None:
        match event:
            case OrderCreated():
                await self._projector.on_order_created(event)
            case OrderLineAdded():
                await self._projector.on_line_added(event)
            case OrderConfirmed():
                await self._projector.on_order_confirmed(event)
            case OrderShipped():
                await self._projector.on_order_shipped(event)
            # ... etc


# Event dispatcher routes events to projection actors
class EventDispatcher:
    def __init__(self):
        self._projection_actor = OrderProjectionActor.remote(read_db)

    async def dispatch(self, events: List[OrderEvent]) -> None:
        for event in events:
            await self._projection_actor.handle_event.remote(event)
```

### When to Use Actors

| Use Case | Actors Add Value |
|----------|------------------|
| High concurrency per aggregate | Yes - serializes access |
| Distributed systems | Yes - location transparency |
| Complex lifecycle management | Yes - supervision trees |
| Simple CRUD | No - unnecessary complexity |
| Low traffic | No - overhead not justified |
