# Part VI: Quality & Reference

Testing patterns, libraries, and package references.

## Testing Patterns

### Testing Deciders

```python
import pytest
from datetime import datetime, UTC

class TestOrderDecider:
    def setup_method(self):
        self.decider = OrderDecider()
        self.order_id = OrderId.new()
        self.customer_id = CustomerId.new()

    def test_create_order_when_new_should_emit_created(self):
        # Arrange
        state = self.decider.initial_state
        command = CreateOrder(self.order_id, self.customer_id)

        # Act
        events = list(self.decider.decide(command, state))

        # Assert
        assert len(events) == 1
        assert isinstance(events[0], OrderCreated)
        assert events[0].customer_id == self.customer_id

    def test_add_line_to_existing_order_should_emit_line_added(self):
        # Arrange
        state = self.decider.evolve(
            self.decider.initial_state,
            OrderCreated(self.order_id, datetime.now(UTC), self.customer_id)
        )
        command = AddOrderLine(
            self.order_id,
            ProductId("ABC"),
            "Widget",
            2,
            Money.usd(10)
        )

        # Act
        events = list(self.decider.decide(command, state))

        # Assert
        assert len(events) == 1
        assert isinstance(events[0], OrderLineAdded)
        assert events[0].quantity == 2

    def test_confirm_empty_order_should_raise(self):
        # Arrange
        state = self.decider.evolve(
            self.decider.initial_state,
            OrderCreated(self.order_id, datetime.now(UTC), self.customer_id)
        )
        command = ConfirmOrder(self.order_id)

        # Act & Assert
        with pytest.raises(InvalidOperationError, match="empty order"):
            list(self.decider.decide(command, state))

    def test_evolve_with_line_added_should_update_total(self):
        # Arrange
        state = self.decider.evolve(
            self.decider.initial_state,
            OrderCreated(self.order_id, datetime.now(UTC), self.customer_id)
        )

        # Act
        state = self.decider.evolve(
            state,
            OrderLineAdded(
                self.order_id,
                datetime.now(UTC),
                ProductId("ABC"),
                "Widget",
                2,
                Money.usd(10)
            )
        )

        # Assert
        assert state.total.amount == 20
```

### Given-When-Then Test Helper

```python
from typing import TypeVar, Generic, List
from dataclasses import dataclass

TState = TypeVar("TState")
TCommand = TypeVar("TCommand")
TEvent = TypeVar("TEvent")

class DeciderTestHarness(Generic[TState, TCommand, TEvent]):
    def __init__(self, decider: Decider[TState, TCommand, TEvent]):
        self._decider = decider
        self._state = decider.initial_state

    def given(self, *events: TEvent) -> "DeciderTestHarness":
        for event in events:
            self._state = self._decider.evolve(self._state, event)
        return self

    def when(self, command: TCommand) -> List[TEvent]:
        return list(self._decider.decide(command, self._state))

    def when_raises(self, command: TCommand, exception_type: type) -> Exception:
        with pytest.raises(exception_type) as exc_info:
            list(self._decider.decide(command, self._state))
        return exc_info.value

    @property
    def state(self) -> TState:
        return self._state


# Usage
def test_complete_order_flow():
    harness = DeciderTestHarness(OrderDecider())
    order_id = OrderId.new()
    customer_id = CustomerId.new()
    now = datetime.now(UTC)

    events = (
        harness
        .given(OrderCreated(order_id, now, customer_id))
        .given(OrderLineAdded(order_id, now, ProductId("ABC"), "Widget", 2, Money.usd(10)))
        .given(ShippingAddressSet(order_id, now, Address("123 Main", "NYC", "NY", "10001")))
        .when(ConfirmOrder(order_id))
    )

    assert len(events) == 1
    assert isinstance(events[0], OrderConfirmed)
```

---

## Libraries & Packages

### Event Sourcing / Event Stores

| Library | Use Case | Notes |
|---------|----------|-------|
| **esdbclient** | EventStoreDB client | Official Python client |
| **eventsourcing** | Complete ES framework | Batteries included |
| **sqlalchemy** | SQL-based event store | DIY with good ORM |

### CQRS / Mediator

| Library | Use Case |
|---------|----------|
| **mediatr-py** | Port of MediatR |
| **python-mediator** | Simple mediator |
| **injector** | Dependency injection |

### Actors

| Framework | Best For |
|-----------|----------|
| **Ray** | Distributed computing, ML |
| **Thespian** | General actor model |
| **Pykka** | Simple threading-based |
| **dramatiq** | Background tasks |

### Persistence

| Library | Use Case |
|---------|----------|
| **SQLAlchemy** | Full ORM |
| **databases** | Async SQL |
| **motor** | Async MongoDB |
| **redis-py** | Cache/simple store |

### Requirements

```txt
# Core
pydantic>=2.0
python-dateutil

# Event Sourcing
esdbclient>=1.0
# or
eventsourcing>=9.0

# Persistence
sqlalchemy[asyncio]>=2.0
asyncpg  # PostgreSQL async
aiosqlite  # SQLite async

# Actors
ray>=2.0
# or
thespian>=3.10

# Testing
pytest>=7.0
pytest-asyncio
hypothesis  # Property-based testing

# API
fastapi>=0.100
uvicorn[standard]
```

### Type Checking Setup

```toml
# pyproject.toml
[tool.mypy]
python_version = "3.11"
strict = true
plugins = ["pydantic.mypy"]

[tool.pytest.ini_options]
asyncio_mode = "auto"
```
