# HTTP API Reference

## Table of Contents

- [Stream Endpoints](#stream-endpoints)
- [Subscription Endpoints](#subscription-endpoints)
- [Projection Endpoints](#projection-endpoints)
- [Connector Endpoints](#connector-endpoints)
- [Index Endpoints](#index-endpoints)
- [Admin Endpoints](#admin-endpoints)
- [Custom Headers](#custom-headers)
- [Content Types](#content-types)

---

## Stream Endpoints

| Method | Endpoint                                     | Description                     |
|--------|----------------------------------------------|---------------------------------|
| GET    | `/streams/{stream}`                          | Read stream (AtomFeed)          |
| POST   | `/streams/{stream}`                          | Append events                   |
| DELETE | `/streams/{stream}`                          | Delete stream                   |
| GET    | `/streams/{stream}/{event}`                  | Read single event               |
| GET    | `/streams/{stream}/{event}/{count}`          | Paginate backward               |
| GET    | `/streams/{stream}/{event}/backward/{count}` | Paginate backward (explicit)    |
| GET    | `/streams/{stream}/{event}/forward/{count}`  | Paginate forward                |
| GET    | `/streams/{stream}/metadata`                 | Read stream metadata            |
| POST   | `/streams/{stream}/metadata`                 | Update stream metadata          |
| GET    | `/streams/$all`                              | Read all events (auth required) |

### Feed Embed Modes

When reading streams via AtomFeed, use the `embed` query parameter:

| Mode         | Description                      |
|--------------|----------------------------------|
| `None`       | No event data embedded           |
| `Content`    | Event data as-is                 |
| `Rich`       | Event data with metadata         |
| `Body`       | Full event body                  |
| `PrettyBody` | Pretty-printed event body        |
| `TryHarder`  | Best effort at embedding content |

---

## Subscription Endpoints

Persistent subscriptions for competing consumers.

| Method | Endpoint                                | Description                   |
|--------|-----------------------------------------|-------------------------------|
| PUT    | `/subscriptions/{stream}/{sub}`         | Create subscription (admin)   |
| POST   | `/subscriptions/{stream}/{sub}`         | Update subscription           |
| DELETE | `/subscriptions/{stream}/{sub}`         | Delete subscription           |
| GET    | `/subscriptions`                        | List all subscriptions        |
| GET    | `/subscriptions/{stream}`               | List subscriptions for stream |
| GET    | `/subscriptions/{stream}/{sub}/info`    | Get subscription info         |
| GET    | `/subscriptions/{stream}/{sub}/{count}` | Read N events                 |

### Acknowledgment and Negative Acknowledgment

| Method | Endpoint                                                             | Description                 |
|--------|----------------------------------------------------------------------|-----------------------------|
| POST   | `/subscriptions/{stream}/{sub}/ack/{msgid}`                          | Acknowledge single event    |
| POST   | `/subscriptions/{stream}/{sub}/ack?ids={id1},{id2}`                  | Acknowledge multiple events |
| POST   | `/subscriptions/{stream}/{sub}/nack/{msgid}?action={action}`         | Nack single event           |
| POST   | `/subscriptions/{stream}/{sub}/nack?ids={id1},{id2}&action={action}` | Nack multiple events        |
| POST   | `/subscriptions/{stream}/{sub}/replayParked`                         | Replay parked messages      |

### Nack Actions

| Action  | Description                   |
|---------|-------------------------------|
| `Park`  | Move to parked messages queue |
| `Retry` | Retry delivery                |
| `Skip`  | Skip the message              |
| `Stop`  | Stop the subscription         |

---

## Projection Endpoints

| Method | Endpoint                   | Description                     |
|--------|----------------------------|---------------------------------|
| GET    | `/projections/any`         | List all projections            |
| GET    | `/projections/continuous`  | List continuous projections     |
| POST   | `/projections/continuous`  | Create continuous projection    |
| POST   | `/projections/onetime`     | Create one-time projection      |
| POST   | `/projections/transient`   | Create transient projection     |
| GET    | `/projection/{name}/query` | Get projection JS definition    |
| PUT    | `/projection/{name}/query` | Update projection JS definition |

---

## Connector Endpoints

| Method | Endpoint                    | Description             |
|--------|-----------------------------|-------------------------|
| POST   | `/connectors/{id}`          | Create connector        |
| POST   | `/connectors/{id}/start`    | Start connector         |
| POST   | `/connectors/{id}/stop`     | Stop connector          |
| PUT    | `/connectors/{id}/settings` | Reconfigure connector   |
| DELETE | `/connectors/{id}`          | Delete connector        |
| GET    | `/connectors`               | List all connectors     |
| GET    | `/connectors/{id}/settings` | View connector settings |

---

## Index Endpoints

**Available since v26.0**

| Method | Endpoint                   | Description       |
|--------|----------------------------|-------------------|
| POST   | `/v2/indexes/{name}`       | Create index      |
| POST   | `/v2/indexes/{name}/start` | Start index       |
| POST   | `/v2/indexes/{name}/stop`  | Stop index        |
| DELETE | `/v2/indexes/{name}`       | Delete index      |
| GET    | `/v2/indexes`              | List all indexes  |
| GET    | `/v2/indexes/{name}`       | Get index details |

---

## Admin Endpoints

| Method | Endpoint               | Description         |
|--------|------------------------|---------------------|
| POST   | `/admin/scavenge`      | Start scavenge      |
| DELETE | `/admin/scavenge/{id}` | Stop scavenge by ID |
| POST   | `/admin/mergeindexes`  | Manual index merge  |
| POST   | `/admin/shutdown`      | Shutdown the node   |

---

## Custom Headers

### Request Headers

| Header                    | Description                          | Values                                                                         |
|---------------------------|--------------------------------------|--------------------------------------------------------------------------------|
| `Kurrent-ExpectedVersion` | Optimistic concurrency control       | `-2` = any, `-1` = no stream exists, `-4` = stream exists, `N` = exact version |
| `Kurrent-ResolveLinkTo`   | Follow link events                   | `true` / `false`                                                               |
| `Kurrent-RequireLeader`   | Route request to the leader node     | `true` / `false`                                                               |
| `Kurrent-TrustedAuth`     | Externalized authentication username | Username string                                                                |
| `Kurrent-LongPoll`        | Long polling timeout in seconds      | Integer                                                                        |
| `Kurrent-HardDelete`      | Permanently delete (no tombstone)    | `true` / `false`                                                               |
| `Kurrent-EventType`       | Event type for single-event writes   | Type string                                                                    |
| `Kurrent-EventId`         | Idempotency identifier               | GUID                                                                           |

### Expected Version Values

| Value | Meaning                                   |
|-------|-------------------------------------------|
| `-2`  | Any — no concurrency check                |
| `-1`  | No stream — stream must not exist         |
| `-4`  | Stream exists — stream must already exist |
| `N`   | Exact — stream must be at version N       |

---

## Content Types

### Primary Content Types

| Content Type                          | Description                    |
|---------------------------------------|--------------------------------|
| `application/vnd.kurrent.events+json` | Batch of events (array format) |
| `application/json`                    | Single data-only event         |

### Batch Event Format

```json
[
  {
    "eventId": "fbf4a1a1-b4a3-4dfe-a01f-ec52c34e16e4",
    "eventType": "OrderPlaced",
    "data": {
      "orderId": "12345",
      "amount": 99.99
    },
    "metadata": {
      "correlationId": "abc-123"
    }
  }
]
```

Set `Content-Type: application/vnd.kurrent.events+json`.

### Legacy Content Types

The old `vnd.eventstore` prefix is still accepted but **deprecated**. Use `vnd.kurrent` for new integrations.
