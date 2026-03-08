# Part IV: Event-Sourced Implementation

Persistence where events are the source of truth and state is derived.

## Event Store

Use event sourcing when events are the source of truth. The aggregate state is rebuilt by replaying events.

### With EventStoreDB

```python
from esdbclient import EventStoreDBClient, StreamState
from dataclasses import asdict
import json
from typing import List, Tuple, Optional

class EventSourcedRepository:
    def __init__(
        self,
        client: EventStoreDBClient,
        decider: OrderDecider
    ):
        self._client = client
        self._decider = decider

    def _stream_name(self, order_id: OrderId) -> str:
        return f"order-{order_id.value}"

    async def load(self, order_id: OrderId) -> Tuple[OrderState, int]:
        """Load aggregate state and version from event stream."""
        stream_name = self._stream_name(order_id)
        state = self._decider.initial_state
        version = -1

        try:
            events = self._client.get_stream(stream_name)
            for recorded in events:
                event = self._deserialize(recorded)
                if event:
                    state = self._decider.evolve(state, event)
                    version = recorded.stream_position
        except Exception:
            # Stream doesn't exist yet
            pass

        return state, version

    async def save(
        self,
        command: OrderCommand,
        expected_version: int
    ) -> int:
        """Execute command and append resulting events."""
        state, _ = await self.load(command.order_id)
        events = list(self._decider.decide(command, state))

        if not events:
            return expected_version

        stream_name = self._stream_name(command.order_id)

        # Prepare event data
        event_data = [
            self._client.new_event(
                type=type(e).__name__,
                data=json.dumps(asdict(e), default=str).encode()
            )
            for e in events
        ]

        # Append with optimistic concurrency
        if expected_version == -1:
            result = self._client.append_to_stream(
                stream_name,
                current_version=StreamState.NO_STREAM,
                events=event_data
            )
        else:
            result = self._client.append_to_stream(
                stream_name,
                current_version=expected_version,
                events=event_data
            )

        return result.commit_position

    def _deserialize(self, recorded) -> Optional[OrderEvent]:
        """Deserialize event from storage."""
        event_type = recorded.type
        data = json.loads(recorded.data)

        # Map event type to class
        event_classes = {
            "OrderCreated": OrderCreated,
            "OrderLineAdded": OrderLineAdded,
            "OrderConfirmed": OrderConfirmed,
            "OrderShipped": OrderShipped,
            # ... etc
        }

        event_class = event_classes.get(event_type)
        if event_class:
            # Reconstruct dataclass from dict
            return event_class(**data)
        return None
```

### Simple In-Memory Event Store (Testing)

```python
from dataclasses import dataclass
from typing import Dict, List

@dataclass
class StoredEvent:
    stream_name: str
    position: int
    event: OrderEvent


class InMemoryEventStore:
    def __init__(self):
        self._streams: Dict[str, List[StoredEvent]] = {}
        self._decider = OrderDecider()

    def load(self, stream_name: str) -> Tuple[OrderState, int]:
        events = self._streams.get(stream_name, [])
        state = self._decider.fold(e.event for e in events)
        version = len(events) - 1
        return state, version

    def append(
        self,
        stream_name: str,
        events: List[OrderEvent],
        expected_version: int
    ) -> int:
        current = self._streams.get(stream_name, [])
        current_version = len(current) - 1

        if expected_version != current_version:
            raise ConcurrencyError(
                f"Expected version {expected_version}, but was {current_version}"
            )

        if stream_name not in self._streams:
            self._streams[stream_name] = []

        for event in events:
            position = len(self._streams[stream_name])
            self._streams[stream_name].append(
                StoredEvent(stream_name, position, event)
            )

        return len(self._streams[stream_name]) - 1
```

---

## CQRS & Projections

### Command Side

```python
from dataclasses import dataclass
from typing import Protocol

# Command
@dataclass
class CreateOrderCommand:
    customer_id: CustomerId


# Handler Protocol
class CommandHandler(Protocol[TCommand, TResult]):
    async def handle(self, command: TCommand) -> TResult: ...


# Handler Implementation
class CreateOrderHandler:
    def __init__(self, repository: OrderRepository):
        self._repository = repository

    async def handle(self, command: CreateOrderCommand) -> OrderId:
        order = Order.create(command.customer_id)
        await self._repository.save(order)
        return order.id
```

### Query Side with Read Models

```python
from dataclasses import dataclass
from typing import Optional, List
from datetime import datetime

# Read Model
@dataclass
class OrderSummary:
    id: str
    customer_name: str
    status: str
    total: float
    line_count: int
    created_at: datetime


# Query
@dataclass
class GetOrderSummaryQuery:
    order_id: OrderId


# Query Handler
class GetOrderSummaryHandler:
    def __init__(self, read_db: ReadDatabase):
        self._read_db = read_db

    async def handle(self, query: GetOrderSummaryQuery) -> Optional[OrderSummary]:
        return await self._read_db.order_summaries.get(str(query.order_id.value))


# Projector (updates read model from events)
class OrderSummaryProjector:
    def __init__(self, read_db: ReadDatabase):
        self._read_db = read_db

    async def on_order_created(self, event: OrderCreated) -> None:
        summary = OrderSummary(
            id=str(event.order_id.value),
            customer_name="",  # Would fetch from customer service
            status="Draft",
            total=0.0,
            line_count=0,
            created_at=event.occurred_at,
        )
        await self._read_db.order_summaries.save(summary)

    async def on_line_added(self, event: OrderLineAdded) -> None:
        summary = await self._read_db.order_summaries.get(str(event.order_id.value))
        if summary:
            summary.line_count += 1
            summary.total += float(event.unit_price.amount * event.quantity)
            await self._read_db.order_summaries.save(summary)

    async def on_order_confirmed(self, event: OrderConfirmed) -> None:
        summary = await self._read_db.order_summaries.get(str(event.order_id.value))
        if summary:
            summary.status = "Confirmed"
            await self._read_db.order_summaries.save(summary)
```

### Simple Mediator

```python
from typing import Dict, Type, Callable, Any, TypeVar
import asyncio

TCommand = TypeVar("TCommand")
TResult = TypeVar("TResult")

class Mediator:
    def __init__(self):
        self._handlers: Dict[Type, Callable] = {}

    def register(self, command_type: Type, handler: Callable) -> None:
        self._handlers[command_type] = handler

    async def send(self, command: Any) -> Any:
        handler = self._handlers.get(type(command))
        if handler is None:
            raise ValueError(f"No handler for {type(command).__name__}")
        return await handler(command)


# Usage
mediator = Mediator()
mediator.register(CreateOrderCommand, CreateOrderHandler(repository).handle)
mediator.register(GetOrderSummaryQuery, GetOrderSummaryHandler(read_db).handle)

# Execute
order_id = await mediator.send(CreateOrderCommand(customer_id))
summary = await mediator.send(GetOrderSummaryQuery(order_id))
```

---

## Event Serialization & Versioning

Serialization strategy is critical for DDD applications, especially event-sourced systems. Key concerns:

- **Internal events**: JSON for flexibility and Python ecosystem compatibility
- **Integration events**: Protobuf or Avro for schema evolution and cross-platform compatibility
- **Domain → Integration translation**: Never expose internal domain events directly

### JSON Serialization with dataclasses

```python
import json
from dataclasses import asdict, fields
from datetime import datetime
from typing import Type, TypeVar, Any
from uuid import UUID

T = TypeVar("T")

class DomainEventSerializer:
    """Serializes domain events to/from JSON."""

    def serialize(self, event: Any) -> bytes:
        """Convert domain event to JSON bytes."""
        data = asdict(event)
        # Handle special types
        data = self._convert_types(data)
        data["__type__"] = type(event).__name__
        return json.dumps(data).encode("utf-8")

    def deserialize(self, data: bytes, event_types: dict[str, Type]) -> Any:
        """Convert JSON bytes back to domain event."""
        parsed = json.loads(data.decode("utf-8"))
        event_type_name = parsed.pop("__type__")
        event_class = event_types.get(event_type_name)
        if not event_class:
            raise ValueError(f"Unknown event type: {event_type_name}")
        # Convert back to domain types
        parsed = self._restore_types(parsed, event_class)
        return event_class(**parsed)

    def _convert_types(self, data: dict) -> dict:
        """Convert domain types to JSON-serializable types."""
        result = {}
        for key, value in data.items():
            if isinstance(value, UUID):
                result[key] = str(value)
            elif isinstance(value, datetime):
                result[key] = value.isoformat()
            elif hasattr(value, "value"):  # Strongly-typed IDs
                result[key] = str(value.value)
            elif isinstance(value, dict):
                result[key] = self._convert_types(value)
            else:
                result[key] = value
        return result

    def _restore_types(self, data: dict, event_class: Type) -> dict:
        """Restore domain types from JSON data."""
        # Implementation depends on your type hints
        # Use typing.get_type_hints() to introspect
        return data
```

### Integration Events (External Contracts)

```python
from dataclasses import dataclass
from datetime import datetime

# Integration event - stable external contract
@dataclass(frozen=True)
class OrderPlacedIntegrationEvent:
    """Published to message bus. Stable schema."""
    event_id: str
    order_id: str
    customer_id: str
    total_amount: float
    currency: str
    placed_at: str  # ISO format string
    version: int = 1  # Schema version


class IntegrationEventTranslator:
    """Translates domain events to integration events."""

    def translate_order_placed(self, event: OrderPlaced) -> OrderPlacedIntegrationEvent:
        return OrderPlacedIntegrationEvent(
            event_id=str(event.event_id),
            order_id=str(event.order_id.value),
            customer_id=str(event.customer_id.value),
            total_amount=float(event.total.amount),
            currency=event.total.currency,
            placed_at=event.occurred_at.isoformat(),
        )
```

### Serialization Pitfalls

| Pitfall | Problem | Solution |
|---------|---------|----------|
| **No versioning** | Cannot evolve schema | Add version field to events |
| **Internal types exposed** | Breaking changes break consumers | Translate to stable integration events |
| **UUID as string** | Lost type safety | Use custom decoder to restore types |
| **Datetime timezone** | Inconsistent parsing | Always use UTC, ISO format |
| **Nested dataclasses** | asdict() loses type info | Custom encoder/decoder |
