# Part II: Aggregates

Core aggregate patterns that apply to both persistence strategies.

## Aggregates - OOP Style

### Aggregate Root Base Class

```python
from abc import ABC
from dataclasses import dataclass, field
from typing import Generic, TypeVar, List

from .events import DomainEvent

TId = TypeVar("TId")

@dataclass
class AggregateRoot(ABC, Generic[TId]):
    id: TId
    version: int = 0
    _domain_events: List[DomainEvent] = field(default_factory=list, repr=False)

    @property
    def domain_events(self) -> List[DomainEvent]:
        return list(self._domain_events)

    def add_domain_event(self, event: DomainEvent) -> None:
        self._domain_events.append(event)

    def clear_domain_events(self) -> List[DomainEvent]:
        events = self._domain_events.copy()
        self._domain_events.clear()
        return events
```

### Complete Order Aggregate (OOP Style)

```python
from dataclasses import dataclass, field
from datetime import datetime, UTC
from enum import Enum, auto
from typing import List, Optional

from .order_id import OrderId
from .order_line import OrderLine, OrderLineId
from .events import (
    OrderCreated, OrderLineAdded, OrderLineRemoved,
    ShippingAddressSet, OrderConfirmed, OrderShipped, OrderCancelled
)
from ..shared_kernel import Money, Address
from ..customers import CustomerId

class OrderStatus(Enum):
    DRAFT = auto()
    CONFIRMED = auto()
    SHIPPED = auto()
    CANCELLED = auto()


@dataclass
class Order(AggregateRoot[OrderId]):
    customer_id: CustomerId
    status: OrderStatus = OrderStatus.DRAFT
    lines: List[OrderLine] = field(default_factory=list)
    total: Money = field(default_factory=lambda: Money.zero())
    created_at: datetime = field(default_factory=lambda: datetime.now(UTC))
    shipped_at: Optional[datetime] = None
    shipping_address: Optional[Address] = None

    @classmethod
    def create(cls, customer_id: CustomerId, currency: str = "USD") -> "Order":
        """Factory method - the only way to create an order."""
        order = cls(
            id=OrderId.new(),
            customer_id=customer_id,
            total=Money.zero(currency),
        )
        order.add_domain_event(OrderCreated(
            order_id=order.id,
            customer_id=customer_id,
            occurred_at=order.created_at,
        ))
        return order

    def add_line(
        self,
        product_id: ProductId,
        product_name: str,
        quantity: int,
        unit_price: Money
    ) -> None:
        self._ensure_not_shipped()

        # Check if product already in order
        existing = next(
            (line for line in self.lines if line.product_id == product_id),
            None
        )

        if existing:
            existing.update_quantity(existing.quantity + quantity)
        else:
            line = OrderLine(
                id=OrderLineId(),
                product_id=product_id,
                product_name=product_name,
                quantity=quantity,
                unit_price=unit_price,
            )
            self.lines.append(line)

        self._recalculate_total()
        self.add_domain_event(OrderLineAdded(
            order_id=self.id,
            product_id=product_id,
            quantity=quantity,
            unit_price=unit_price,
            occurred_at=datetime.now(UTC),
        ))

    def remove_line(self, product_id: ProductId) -> None:
        self._ensure_not_shipped()

        line = next(
            (l for l in self.lines if l.product_id == product_id),
            None
        )
        if line is None:
            raise ValueError(f"Product {product_id} not in order")

        self.lines.remove(line)
        self._recalculate_total()
        self.add_domain_event(OrderLineRemoved(
            order_id=self.id,
            product_id=product_id,
            occurred_at=datetime.now(UTC),
        ))

    def set_shipping_address(self, address: Address) -> None:
        self._ensure_not_shipped()
        self.shipping_address = address
        self.add_domain_event(ShippingAddressSet(
            order_id=self.id,
            address=address,
            occurred_at=datetime.now(UTC),
        ))

    def confirm(self) -> None:
        if self.status != OrderStatus.DRAFT:
            raise InvalidOperationError(f"Cannot confirm order in status {self.status}")
        if not self.lines:
            raise InvalidOperationError("Cannot confirm empty order")
        if self.shipping_address is None:
            raise InvalidOperationError("Shipping address required")

        self.status = OrderStatus.CONFIRMED
        self.add_domain_event(OrderConfirmed(
            order_id=self.id,
            occurred_at=datetime.now(UTC),
        ))

    def ship(self, tracking_number: str) -> None:
        if self.status != OrderStatus.CONFIRMED:
            raise InvalidOperationError(f"Cannot ship order in status {self.status}")

        self.status = OrderStatus.SHIPPED
        self.shipped_at = datetime.now(UTC)
        self.add_domain_event(OrderShipped(
            order_id=self.id,
            tracking_number=tracking_number,
            occurred_at=self.shipped_at,
        ))

    def cancel(self, reason: str) -> None:
        if self.status == OrderStatus.SHIPPED:
            raise InvalidOperationError("Cannot cancel shipped order")

        self.status = OrderStatus.CANCELLED
        self.add_domain_event(OrderCancelled(
            order_id=self.id,
            reason=reason,
            occurred_at=datetime.now(UTC),
        ))

    def _recalculate_total(self) -> None:
        self.total = Money.zero(self.total.currency)
        for line in self.lines:
            self.total = self.total.add(line.line_total)

    def _ensure_not_shipped(self) -> None:
        if self.status == OrderStatus.SHIPPED:
            raise InvalidOperationError("Cannot modify shipped order")


class InvalidOperationError(Exception):
    """Domain operation cannot be performed in current state."""
    pass
```

---

## Aggregates - Decider Pattern

### Decider Infrastructure

```python
from abc import ABC, abstractmethod
from dataclasses import dataclass
from datetime import datetime
from typing import Generic, TypeVar, Iterable, Callable
from functools import reduce

TState = TypeVar("TState")
TCommand = TypeVar("TCommand")
TEvent = TypeVar("TEvent")

class Decider(ABC, Generic[TState, TCommand, TEvent]):
    """
    Pure functional aggregate implementation.

    Three functions define behavior:
    - initial_state: Starting state for new aggregates
    - decide: Validate command and produce events
    - evolve: Apply event to update state
    """

    @property
    @abstractmethod
    def initial_state(self) -> TState:
        """Return the initial state for a new aggregate."""
        ...

    @abstractmethod
    def decide(self, command: TCommand, state: TState) -> Iterable[TEvent]:
        """
        Validate command against state and return events.
        Raise exception if command is invalid.
        """
        ...

    @abstractmethod
    def evolve(self, state: TState, event: TEvent) -> TState:
        """Apply event to state and return new state."""
        ...

    def fold(self, events: Iterable[TEvent]) -> TState:
        """Rebuild state from events."""
        return reduce(self.evolve, events, self.initial_state)
```

### Order as a Decider

```python
from dataclasses import dataclass, field
from datetime import datetime, UTC
from decimal import Decimal
from enum import Enum, auto
from typing import Iterable, List, Optional
from functools import reduce

# === State (Immutable) ===

class OrderStatus(Enum):
    NOT_CREATED = auto()
    DRAFT = auto()
    CONFIRMED = auto()
    SHIPPED = auto()
    CANCELLED = auto()


@dataclass(frozen=True)
class OrderLineState:
    product_id: ProductId
    product_name: str
    quantity: int
    unit_price: Money


@dataclass(frozen=True)
class OrderState:
    id: Optional[OrderId] = None
    customer_id: Optional[CustomerId] = None
    status: OrderStatus = OrderStatus.NOT_CREATED
    lines: tuple[OrderLineState, ...] = ()
    total: Optional[Money] = None
    shipping_address: Optional[Address] = None
    created_at: Optional[datetime] = None
    shipped_at: Optional[datetime] = None

    @property
    def exists(self) -> bool:
        return self.status != OrderStatus.NOT_CREATED

    def with_line_added(self, line: OrderLineState) -> "OrderState":
        new_lines = self.lines + (line,)
        return dataclass_replace(self, lines=new_lines, total=self._calc_total(new_lines))

    def with_line_removed(self, product_id: ProductId) -> "OrderState":
        new_lines = tuple(l for l in self.lines if l.product_id != product_id)
        return dataclass_replace(self, lines=new_lines, total=self._calc_total(new_lines))

    def _calc_total(self, lines: tuple[OrderLineState, ...]) -> Money:
        currency = self.total.currency if self.total else "USD"
        return reduce(
            lambda acc, line: acc.add(line.unit_price.multiply(line.quantity)),
            lines,
            Money.zero(currency)
        )


def dataclass_replace(obj, **changes):
    """Helper to create new dataclass instance with changes."""
    from dataclasses import replace
    return replace(obj, **changes)


# === Commands ===

@dataclass(frozen=True)
class OrderCommand:
    order_id: OrderId


@dataclass(frozen=True)
class CreateOrder(OrderCommand):
    customer_id: CustomerId


@dataclass(frozen=True)
class AddOrderLine(OrderCommand):
    product_id: ProductId
    product_name: str
    quantity: int
    unit_price: Money


@dataclass(frozen=True)
class RemoveOrderLine(OrderCommand):
    product_id: ProductId


@dataclass(frozen=True)
class SetShippingAddress(OrderCommand):
    address: Address


@dataclass(frozen=True)
class ConfirmOrder(OrderCommand):
    pass


@dataclass(frozen=True)
class ShipOrder(OrderCommand):
    tracking_number: str


@dataclass(frozen=True)
class CancelOrder(OrderCommand):
    reason: str


# === Events ===

@dataclass(frozen=True)
class OrderEvent:
    order_id: OrderId
    occurred_at: datetime


@dataclass(frozen=True)
class OrderCreated(OrderEvent):
    customer_id: CustomerId


@dataclass(frozen=True)
class OrderLineAdded(OrderEvent):
    product_id: ProductId
    product_name: str
    quantity: int
    unit_price: Money


@dataclass(frozen=True)
class OrderLineRemoved(OrderEvent):
    product_id: ProductId


@dataclass(frozen=True)
class ShippingAddressSet(OrderEvent):
    address: Address


@dataclass(frozen=True)
class OrderConfirmed(OrderEvent):
    pass


@dataclass(frozen=True)
class OrderShipped(OrderEvent):
    tracking_number: str


@dataclass(frozen=True)
class OrderCancelled(OrderEvent):
    reason: str


# === The Decider ===

class OrderDecider(Decider[OrderState, OrderCommand, OrderEvent]):

    @property
    def initial_state(self) -> OrderState:
        return OrderState()

    def decide(self, command: OrderCommand, state: OrderState) -> Iterable[OrderEvent]:
        now = datetime.now(UTC)

        match command:
            case CreateOrder(order_id, customer_id):
                yield from self._decide_create(order_id, customer_id, state, now)

            case AddOrderLine(order_id, product_id, product_name, quantity, unit_price):
                yield from self._decide_add_line(
                    order_id, product_id, product_name, quantity, unit_price, state, now
                )

            case RemoveOrderLine(order_id, product_id):
                yield from self._decide_remove_line(order_id, product_id, state, now)

            case SetShippingAddress(order_id, address):
                yield from self._decide_set_address(order_id, address, state, now)

            case ConfirmOrder(order_id):
                yield from self._decide_confirm(order_id, state, now)

            case ShipOrder(order_id, tracking_number):
                yield from self._decide_ship(order_id, tracking_number, state, now)

            case CancelOrder(order_id, reason):
                yield from self._decide_cancel(order_id, reason, state, now)

            case _:
                raise ValueError(f"Unknown command: {type(command).__name__}")

    def _decide_create(
        self, order_id: OrderId, customer_id: CustomerId,
        state: OrderState, now: datetime
    ) -> Iterable[OrderEvent]:
        if state.exists:
            raise InvalidOperationError("Order already exists")
        yield OrderCreated(order_id, now, customer_id)

    def _decide_add_line(
        self, order_id: OrderId, product_id: ProductId, product_name: str,
        quantity: int, unit_price: Money, state: OrderState, now: datetime
    ) -> Iterable[OrderEvent]:
        self._ensure_exists(state)
        self._ensure_not_shipped(state)
        if quantity <= 0:
            raise ValueError("Quantity must be positive")
        yield OrderLineAdded(order_id, now, product_id, product_name, quantity, unit_price)

    def _decide_remove_line(
        self, order_id: OrderId, product_id: ProductId,
        state: OrderState, now: datetime
    ) -> Iterable[OrderEvent]:
        self._ensure_exists(state)
        self._ensure_not_shipped(state)
        if not any(l.product_id == product_id for l in state.lines):
            raise InvalidOperationError(f"Product {product_id} not in order")
        yield OrderLineRemoved(order_id, now, product_id)

    def _decide_set_address(
        self, order_id: OrderId, address: Address,
        state: OrderState, now: datetime
    ) -> Iterable[OrderEvent]:
        self._ensure_exists(state)
        self._ensure_not_shipped(state)
        yield ShippingAddressSet(order_id, now, address)

    def _decide_confirm(
        self, order_id: OrderId, state: OrderState, now: datetime
    ) -> Iterable[OrderEvent]:
        self._ensure_exists(state)
        if state.status != OrderStatus.DRAFT:
            raise InvalidOperationError(f"Cannot confirm order in status {state.status}")
        if not state.lines:
            raise InvalidOperationError("Cannot confirm empty order")
        if state.shipping_address is None:
            raise InvalidOperationError("Shipping address required")
        yield OrderConfirmed(order_id, now)

    def _decide_ship(
        self, order_id: OrderId, tracking_number: str,
        state: OrderState, now: datetime
    ) -> Iterable[OrderEvent]:
        self._ensure_exists(state)
        if state.status != OrderStatus.CONFIRMED:
            raise InvalidOperationError(f"Cannot ship order in status {state.status}")
        yield OrderShipped(order_id, now, tracking_number)

    def _decide_cancel(
        self, order_id: OrderId, reason: str,
        state: OrderState, now: datetime
    ) -> Iterable[OrderEvent]:
        self._ensure_exists(state)
        if state.status == OrderStatus.SHIPPED:
            raise InvalidOperationError("Cannot cancel shipped order")
        yield OrderCancelled(order_id, now, reason)

    def evolve(self, state: OrderState, event: OrderEvent) -> OrderState:
        match event:
            case OrderCreated(order_id, occurred_at, customer_id):
                return OrderState(
                    id=order_id,
                    customer_id=customer_id,
                    status=OrderStatus.DRAFT,
                    created_at=occurred_at,
                    total=Money.zero(),
                )

            case OrderLineAdded(_, _, product_id, product_name, quantity, unit_price):
                return state.with_line_added(
                    OrderLineState(product_id, product_name, quantity, unit_price)
                )

            case OrderLineRemoved(_, _, product_id):
                return state.with_line_removed(product_id)

            case ShippingAddressSet(_, _, address):
                return dataclass_replace(state, shipping_address=address)

            case OrderConfirmed():
                return dataclass_replace(state, status=OrderStatus.CONFIRMED)

            case OrderShipped(_, occurred_at, _):
                return dataclass_replace(
                    state,
                    status=OrderStatus.SHIPPED,
                    shipped_at=occurred_at
                )

            case OrderCancelled():
                return dataclass_replace(state, status=OrderStatus.CANCELLED)

            case _:
                return state

    @staticmethod
    def _ensure_exists(state: OrderState) -> None:
        if not state.exists:
            raise InvalidOperationError("Order does not exist")

    @staticmethod
    def _ensure_not_shipped(state: OrderState) -> None:
        if state.status == OrderStatus.SHIPPED:
            raise InvalidOperationError("Cannot modify shipped order")
```
