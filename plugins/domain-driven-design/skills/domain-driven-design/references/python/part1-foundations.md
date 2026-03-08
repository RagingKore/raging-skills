# Part I: Foundations

Shared building blocks that apply regardless of persistence strategy.

## Project Structure

### Recommended Package Layout

```
src/
├── domain/                          # Pure domain logic, NO infrastructure
│   ├── __init__.py
│   ├── orders/
│   │   ├── __init__.py
│   │   ├── order.py                 # Aggregate root
│   │   ├── order_line.py            # Entity within aggregate
│   │   ├── order_id.py              # Strongly-typed ID
│   │   ├── events.py                # Domain events
│   │   ├── commands.py              # Commands (for Decider)
│   │   └── repository.py            # Repository protocol
│   ├── customers/
│   │   └── ...
│   └── shared_kernel/
│       ├── __init__.py
│       ├── money.py
│       ├── address.py
│       ├── entity.py                # Base classes
│       └── aggregate.py
│
├── application/                     # Use cases, orchestration
│   ├── __init__.py
│   ├── orders/
│   │   ├── commands.py              # Command handlers
│   │   └── queries.py               # Query handlers
│   └── common/
│       ├── unit_of_work.py
│       └── mediator.py
│
├── infrastructure/                  # External concerns
│   ├── __init__.py
│   ├── persistence/
│   │   ├── sqlalchemy/
│   │   │   ├── order_repository.py
│   │   │   └── models.py
│   │   └── event_store/
│   │       └── esdb_repository.py
│   └── messaging/
│       └── event_publisher.py
│
└── api/                             # Entry point
    ├── __init__.py
    ├── main.py
    └── routes/
```

### Key Principle: Dependency Inversion

```python
# domain/orders/repository.py - Protocol (interface)
from typing import Protocol, Optional
from .order import Order
from .order_id import OrderId

class OrderRepository(Protocol):
    async def get_by_id(self, order_id: OrderId) -> Optional[Order]: ...
    async def save(self, order: Order) -> None: ...

# infrastructure/persistence/sqlalchemy/order_repository.py - Implementation
class SqlAlchemyOrderRepository:
    def __init__(self, session: AsyncSession):
        self._session = session

    async def get_by_id(self, order_id: OrderId) -> Optional[Order]:
        ...
```

---

## Value Objects

### Using Frozen Dataclasses

```python
from dataclasses import dataclass, field
from decimal import Decimal
from typing import Self

@dataclass(frozen=True, slots=True)
class Money:
    amount: Decimal
    currency: str

    def __post_init__(self) -> None:
        if self.amount < 0:
            raise ValueError("Amount cannot be negative")
        if len(self.currency) != 3:
            raise ValueError("Currency must be 3-letter ISO code")
        # Normalize currency to uppercase
        object.__setattr__(self, 'currency', self.currency.upper())

    @classmethod
    def zero(cls, currency: str = "USD") -> Self:
        return cls(Decimal("0"), currency)

    @classmethod
    def usd(cls, amount: Decimal | float | str) -> Self:
        return cls(Decimal(str(amount)), "USD")

    @classmethod
    def eur(cls, amount: Decimal | float | str) -> Self:
        return cls(Decimal(str(amount)), "EUR")

    def add(self, other: "Money") -> "Money":
        if self.currency != other.currency:
            raise ValueError(f"Cannot add {self.currency} to {other.currency}")
        return Money(self.amount + other.amount, self.currency)

    def subtract(self, other: "Money") -> "Money":
        if self.currency != other.currency:
            raise ValueError(f"Cannot subtract {other.currency} from {self.currency}")
        return Money(self.amount - other.amount, self.currency)

    def multiply(self, factor: Decimal | int) -> "Money":
        return Money(self.amount * Decimal(str(factor)), self.currency)

    def __str__(self) -> str:
        return f"{self.amount:.2f} {self.currency}"
```

### Complex Value Object with Validation

```python
import re
from dataclasses import dataclass

@dataclass(frozen=True, slots=True)
class EmailAddress:
    value: str

    _EMAIL_PATTERN = re.compile(
        r"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"
    )

    def __post_init__(self) -> None:
        if not self.value:
            raise ValueError("Email cannot be empty")
        if not self._EMAIL_PATTERN.match(self.value):
            raise ValueError(f"Invalid email format: {self.value}")
        # Normalize to lowercase
        object.__setattr__(self, 'value', self.value.lower())

    def __str__(self) -> str:
        return self.value


@dataclass(frozen=True, slots=True)
class Address:
    street: str
    city: str
    state: str
    postal_code: str
    country: str = "US"

    def __post_init__(self) -> None:
        if not self.street:
            raise ValueError("Street cannot be empty")
        if not self.city:
            raise ValueError("City cannot be empty")
        if not self.postal_code:
            raise ValueError("Postal code cannot be empty")

    def one_line(self) -> str:
        return f"{self.street}, {self.city}, {self.state} {self.postal_code}"
```

### Strongly-Typed IDs

```python
from dataclasses import dataclass
from uuid import UUID, uuid4
from typing import Self

@dataclass(frozen=True, slots=True)
class OrderId:
    value: UUID

    @classmethod
    def new(cls) -> Self:
        return cls(uuid4())

    @classmethod
    def from_str(cls, value: str) -> Self:
        return cls(UUID(value))

    def __str__(self) -> str:
        return str(self.value)


@dataclass(frozen=True, slots=True)
class CustomerId:
    value: UUID

    @classmethod
    def new(cls) -> Self:
        return cls(uuid4())


@dataclass(frozen=True, slots=True)
class ProductId:
    value: str

    def __post_init__(self) -> None:
        if not self.value or not self.value.strip():
            raise ValueError("ProductId cannot be empty")

    def __str__(self) -> str:
        return self.value
```

---

## Entities

### Entity Base Class

```python
from abc import ABC
from dataclasses import dataclass, field
from typing import Generic, TypeVar

TId = TypeVar("TId")

@dataclass
class Entity(ABC, Generic[TId]):
    id: TId

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Entity):
            return False
        if type(self) != type(other):
            return False
        return self.id == other.id

    def __hash__(self) -> int:
        return hash(self.id)
```

### Entity Within Aggregate

```python
from dataclasses import dataclass, field
from uuid import UUID, uuid4

@dataclass
class OrderLineId:
    value: UUID = field(default_factory=uuid4)

@dataclass
class OrderLine:
    id: OrderLineId
    product_id: ProductId
    product_name: str
    quantity: int
    unit_price: Money

    def __post_init__(self) -> None:
        if self.quantity <= 0:
            raise ValueError("Quantity must be positive")

    @property
    def line_total(self) -> Money:
        return self.unit_price.multiply(self.quantity)

    def update_quantity(self, new_quantity: int) -> None:
        if new_quantity <= 0:
            raise ValueError("Quantity must be positive")
        self.quantity = new_quantity
```

---

## Domain Events

### Event Protocol and Base

```python
from abc import ABC
from dataclasses import dataclass, field
from datetime import datetime, UTC
from uuid import UUID, uuid4
from typing import Protocol, runtime_checkable

@runtime_checkable
class DomainEvent(Protocol):
    event_id: UUID
    occurred_at: datetime

    @property
    def event_type(self) -> str: ...


@dataclass(frozen=True)
class DomainEventBase:
    event_id: UUID = field(default_factory=uuid4)
    occurred_at: datetime = field(default_factory=lambda: datetime.now(UTC))

    @property
    def event_type(self) -> str:
        return type(self).__name__
```

### Event Publisher

```python
from typing import Protocol, Callable, List, Dict, Type
import asyncio

class EventPublisher(Protocol):
    async def publish(self, events: List[DomainEvent]) -> None: ...


class InMemoryEventPublisher:
    def __init__(self):
        self._handlers: Dict[Type, List[Callable]] = {}

    def subscribe(self, event_type: Type, handler: Callable) -> None:
        if event_type not in self._handlers:
            self._handlers[event_type] = []
        self._handlers[event_type].append(handler)

    async def publish(self, events: List[DomainEvent]) -> None:
        for event in events:
            handlers = self._handlers.get(type(event), [])
            await asyncio.gather(*(h(event) for h in handlers))
```
