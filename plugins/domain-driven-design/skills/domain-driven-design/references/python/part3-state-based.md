# Part III: State-Based Implementation

Traditional persistence where current aggregate state is stored directly.

## Repositories

Use repositories when persisting current aggregate state (not using event sourcing). For event-sourced persistence, see [Part IV](part4-event-sourced.md).

### Repository Protocol

```python
from typing import Protocol, Optional, TypeVar, Generic, List

TAggregate = TypeVar("TAggregate")
TId = TypeVar("TId")


class Repository(Protocol[TAggregate, TId]):
    async def get_by_id(self, id: TId) -> Optional[TAggregate]: ...
    async def save(self, aggregate: TAggregate) -> None: ...
    async def delete(self, aggregate: TAggregate) -> None: ...


class OrderRepository(Protocol):
    async def get_by_id(self, order_id: OrderId) -> Optional[Order]: ...
    async def save(self, order: Order) -> None: ...
    async def get_by_customer_id(self, customer_id: CustomerId) -> List[Order]: ...
```

### SQLAlchemy Repository

```python
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import select
from sqlalchemy.orm import selectinload

class SqlAlchemyOrderRepository:
    def __init__(
        self,
        session: AsyncSession,
        event_publisher: EventPublisher
    ):
        self._session = session
        self._event_publisher = event_publisher

    async def get_by_id(self, order_id: OrderId) -> Optional[Order]:
        stmt = (
            select(OrderModel)
            .options(selectinload(OrderModel.lines))
            .where(OrderModel.id == order_id.value)
        )
        result = await self._session.execute(stmt)
        model = result.scalar_one_or_none()
        return self._to_domain(model) if model else None

    async def save(self, order: Order) -> None:
        # Get pending events before clearing
        events = order.clear_domain_events()

        # Upsert logic
        existing = await self._session.get(OrderModel, order.id.value)
        if existing:
            self._update_model(existing, order)
        else:
            model = self._to_model(order)
            self._session.add(model)

        await self._session.flush()

        # Publish events after successful save
        await self._event_publisher.publish(events)

    async def get_by_customer_id(self, customer_id: CustomerId) -> List[Order]:
        stmt = (
            select(OrderModel)
            .options(selectinload(OrderModel.lines))
            .where(OrderModel.customer_id == customer_id.value)
        )
        result = await self._session.execute(stmt)
        return [self._to_domain(m) for m in result.scalars()]

    def _to_domain(self, model: OrderModel) -> Order:
        # Map SQLAlchemy model to domain aggregate
        ...

    def _to_model(self, order: Order) -> OrderModel:
        # Map domain aggregate to SQLAlchemy model
        ...
```

---

## Simple CQRS (Optional)

For state-based systems, CQRS is optional. When read performance becomes a bottleneck, separate read models can be introduced without event sourcing.

```python
from dataclasses import dataclass
from datetime import datetime
from typing import List, Optional

# Simple read DTO - query directly from database
@dataclass
class OrderListItem:
    id: str
    customer_name: str
    status: str
    total_amount: float
    placed_at: datetime


# Query handler using repository/SQLAlchemy
class OrderQueryHandler:
    def __init__(self, session: AsyncSession):
        self._session = session

    async def get_recent_orders(self, count: int = 10) -> List[OrderListItem]:
        stmt = (
            select(OrderModel)
            .order_by(OrderModel.placed_at.desc())
            .limit(count)
        )
        result = await self._session.execute(stmt)
        return [
            OrderListItem(
                id=str(m.id),
                customer_name=str(m.customer_id),  # Would join with Customer
                status=m.status.name,
                total_amount=float(m.total_amount),
                placed_at=m.placed_at,
            )
            for m in result.scalars()
        ]
```

**When to introduce Simple CQRS:**
- Read queries are slow due to aggregate loading
- Different read shapes needed (lists, summaries, reports)
- Read/write ratio heavily favors reads

**When NOT needed:**
- Simple CRUD operations
- Reads can use aggregate directly
- Early in development
