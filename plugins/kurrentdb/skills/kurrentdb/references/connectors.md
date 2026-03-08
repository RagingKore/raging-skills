# KurrentDB Connectors Reference

Server-side plugins that use catch-up subscriptions to filter, transform, and push events to external systems. Connectors are pre-installed and enabled by default in KurrentDB.

## Table of Contents

- [Overview](#overview)
- [Management REST API](#management-rest-api)
- [Common Settings](#common-settings)
- [KurrentDB Record Structure](#kurrentdb-record-structure)
- [Subscription Filters](#subscription-filters)
- [Transformations](#transformations)
- [Resilience and Backoff](#resilience-and-backoff)
- [Auto-Commit](#auto-commit)
- [Headers](#headers)
- [Data Protection](#data-protection)
- [Sink Connectors](#sink-connectors)
  - [HTTP Sink (Free)](#http-sink-free)
  - [Kafka Sink (License Required)](#kafka-sink-license-required)
  - [RabbitMQ Sink (License Required)](#rabbitmq-sink-license-required)
  - [SQL Sink (License Required)](#sql-sink-license-required)
  - [MongoDB Sink (License Required)](#mongodb-sink-license-required)
  - [Elasticsearch Sink (License Required)](#elasticsearch-sink-license-required)
  - [Pulsar Sink (License Required)](#pulsar-sink-license-required)
  - [Serilog Sink (Free)](#serilog-sink-free)
- [Source Connectors](#source-connectors)
  - [Kafka Source (License Required)](#kafka-source-license-required)
- [Metrics](#metrics)

---

## Overview

- Connectors use **catch-up subscriptions** internally to consume events
- Delivery guarantee: **at-least-once**, events delivered **in order**
- Pre-installed and enabled by default
- Disable globally: set `Connectors:Enabled: false` in server configuration

**Available instance types:**

| Instance Type        | License  | Direction |
|----------------------|----------|-----------|
| `http-sink`          | Free     | Sink      |
| `serilog-sink`       | Free     | Sink      |
| `kafka-sink`         | Required | Sink      |
| `rabbit-mq-sink`     | Required | Sink      |
| `sql-sink`           | Required | Sink      |
| `mongo-db-sink`      | Required | Sink      |
| `elasticsearch-sink` | Required | Sink      |
| `pulsar-sink`        | Required | Sink      |
| `kafka-source`       | Required | Source    |

---

## Management REST API

All connectors are managed through the REST API.

| Operation    | Method   | Endpoint                                                            |
|--------------|----------|---------------------------------------------------------------------|
| Create       | `POST`   | `/connectors/{id}`                                                  |
| Start        | `POST`   | `/connectors/{id}/start` or `/connectors/{id}/start/{log_position}` |
| List         | `GET`    | `/connectors?state=&instanceTypeName=&page=&pageSize=`              |
| Get Settings | `GET`    | `/connectors/{id}/settings`                                         |
| Reset        | `POST`   | `/connectors/{id}/reset` or `/connectors/{id}/reset/{log_position}` |
| Stop         | `POST`   | `/connectors/{id}/stop`                                             |
| Reconfigure  | `PUT`    | `/connectors/{id}/settings` (connector must be **stopped**)         |
| Delete       | `DELETE` | `/connectors/{id}`                                                  |
| Rename       | `PUT`    | `/connectors/{id}/rename`                                           |

**Connector states:** `UNKNOWN`, `ACTIVATING`, `RUNNING`, `DEACTIVATING`, `STOPPED`

### Create Example

```bash
curl -X POST http://localhost:2113/connectors/my-http-sink \
  -H "Content-Type: application/json" \
  -d '{
    "instanceTypeName": "http-sink",
    "settings": {
      "url": "https://api.example.com/events",
      "subscription:filter:scope": "stream",
      "subscription:filter:filterType": "prefix",
      "subscription:filter:expression": "order-"
    }
  }'
```

### Start / Stop / Reset

```bash
# Start from current position
curl -X POST http://localhost:2113/connectors/my-http-sink/start

# Start from specific log position
curl -X POST http://localhost:2113/connectors/my-http-sink/start/12345

# Stop
curl -X POST http://localhost:2113/connectors/my-http-sink/stop

# Reset to beginning
curl -X POST http://localhost:2113/connectors/my-http-sink/reset
```

---

## Common Settings

These settings apply to all connector types.

### Subscription Filter

| Setting                          | Description            | Values                                    |
|----------------------------------|------------------------|-------------------------------------------|
| `subscription:filter:scope`      | What to filter on      | `stream`, `record`                        |
| `subscription:filter:filterType` | Type of filter         | `streamId`, `regex`, `prefix`, `jsonPath` |
| `subscription:filter:expression` | Filter value/pattern   | Depends on filter type                    |
| `subscription:initialPosition`   | Where to start reading | `latest`, `earliest`                      |

### Transformer

| Setting                | Description                      | Default |
|------------------------|----------------------------------|---------|
| `transformer:enabled`  | Enable JavaScript transformation | `false` |
| `transformer:function` | Base64-encoded JS function       | -       |

### Resilience

| Setting                       | Description       | Default |
|-------------------------------|-------------------|---------|
| `resilience:firstDelayBound`  | Phase 1 max delay | 1 min   |
| `resilience:secondDelayBound` | Phase 2 max delay | 1 hour  |
| `resilience:thirdDelayBound`  | Phase 3 max delay | Forever |

### Auto-Commit

| Setting                       | Description           | Default |
|-------------------------------|-----------------------|---------|
| `autocommit:enabled`          | Enable auto-commit    | `true`  |
| `autocommit:interval`         | Commit interval (ms)  | `5000`  |
| `autocommit:recordsThreshold` | Records before commit | `1000`  |

---

## KurrentDB Record Structure

Every event flowing through a connector has this structure:

```json
{
  "recordId": "unique-record-identifier",
  "position": {
    "streamId": "order-12345",
    "logPosition": 42
  },
  "isTransformed": false,
  "headers": {
    "esdb-connector-id": "my-http-sink",
    "esdb-record-id": "unique-record-identifier",
    "esdb-record-timestamp": "2026-01-15T10:30:00Z",
    "esdb-record-stream-id": "order-12345",
    "esdb-record-log-position": "42"
  },
  "value": {
    "orderId": "12345",
    "amount": 99.99
  }
}
```

---

## Subscription Filters

### Filter Types

| Filter Type | Scope              | Description                   | Example Expression |
|-------------|--------------------|-------------------------------|--------------------|
| `streamId`  | `stream`           | Exact stream name match       | `order-12345`      |
| `regex`     | `stream`, `record` | Regular expression pattern    | `^order-.*`        |
| `prefix`    | `stream`, `record` | Comma-separated prefixes      | `order-,payment-`  |
| `jsonPath`  | `record` only      | RFC 9535 JSON path expression | `$.value.type`     |

**Important:** `jsonPath` filters only work with JSON content and require `scope: record`.

### Filter Examples

```json
// Exact stream match
{
  "subscription:filter:scope": "stream",
  "subscription:filter:filterType": "streamId",
  "subscription:filter:expression": "order-12345"
}

// All streams starting with "order-" or "payment-"
{
  "subscription:filter:scope": "stream",
  "subscription:filter:filterType": "prefix",
  "subscription:filter:expression": "order-,payment-"
}

// Regex on stream name
{
  "subscription:filter:scope": "stream",
  "subscription:filter:filterType": "regex",
  "subscription:filter:expression": "^(order|payment)-.*"
}

// JsonPath on record content
{
  "subscription:filter:scope": "record",
  "subscription:filter:filterType": "jsonPath",
  "subscription:filter:expression": "$.value.eventType"
}
```

---

## Transformations

Transformations use JavaScript functions to modify records before they reach the sink. The function must be named `transform` and must **mutate the record in-place** (do NOT return a new object).

### Writing a Transform Function

```javascript
// This function MUTATES the record - it must NOT return a new object
function transform(record) {
  // Add a computed field
  record.value.processedAt = new Date().toISOString();

  // Remove sensitive data
  delete record.value.creditCard;

  // Modify headers
  record.headers["x-custom-header"] = "processed";
}
```

### Encoding and Applying

The function must be **base64-encoded** before setting it in the connector configuration:

```bash
# Encode the function
FUNC=$(echo -n 'function transform(record) { record.value.processedAt = new Date().toISOString(); }' | base64)

# Apply to connector settings
curl -X PUT http://localhost:2113/connectors/my-sink/settings \
  -H "Content-Type: application/json" \
  -d "{
    \"transformer:enabled\": true,
    \"transformer:function\": \"$FUNC\"
  }"
```

**Critical rules:**
- Function MUST be named `transform`
- Function MUST mutate the `record` parameter directly
- Function MUST NOT return a new object
- Function is base64-encoded in settings

---

## Resilience and Backoff

Connectors use a **3-phase exponential backoff** strategy for error recovery:

| Phase   | Initial Delay          | Max Delay  | Duration                            |
|---------|------------------------|------------|-------------------------------------|
| Phase 1 | Exponential from small | 5 seconds  | Up to 1 minute                      |
| Phase 2 | Exponential from 5s    | 10 minutes | Up to 1 hour                        |
| Phase 3 | Exponential from 10m   | 1 hour     | Forever (until manual intervention) |

All phase boundaries are configurable via `resilience:firstDelayBound`, `resilience:secondDelayBound`, and `resilience:thirdDelayBound`.

**Note:** Kafka Sink, RabbitMQ Sink, and Pulsar Sink use their **own internal retry mechanisms** instead of the standard resilience backoff.

---

## Auto-Commit

Auto-commit controls how frequently the connector checkpoints its position in the event stream.

- **Enabled by default** (`autocommit:enabled: true`)
- Commits when **either** threshold is reached:
  - Time interval: every 5000ms (configurable via `autocommit:interval`)
  - Record count: every 1000 records (configurable via `autocommit:recordsThreshold`)

---

## Headers

### Internal Headers

All internal headers use the `esdb-` prefix:

| Header                     | Description           |
|----------------------------|-----------------------|
| `esdb-connector-id`        | Connector instance ID |
| `esdb-request-id`          | Unique request ID     |
| `esdb-record-id`           | Record identifier     |
| `esdb-record-timestamp`    | Event timestamp       |
| `esdb-record-stream-id`    | Source stream ID      |
| `esdb-record-log-position` | Position in the log   |

### User Headers

User-defined event metadata headers are forwarded with the prefix `esdb-record-headers-{key}`.

### System Headers

System metadata headers (prefixed with `$`) are **excluded by default**. Control with:
- `headers:ignoreSystem`: `true` (default) to exclude system headers

---

## Data Protection

Sensitive connector settings (passwords, tokens, connection strings) are protected with **envelope encryption**.

| Setting                               | Description                   |
|---------------------------------------|-------------------------------|
| `Connectors:DataProtection:TokenFile` | Path to encryption token file |
| `Connectors:DataProtection:Token`     | Inline encryption token       |

**Critical:** The data protection token is **permanent**. Once set, it must never be changed or all encrypted settings will become unreadable. KurrentDB uses the Surge key vault internally for encryption.

---

## Sink Connectors

### HTTP Sink (Free)

Sends individual JSON POST requests to an HTTP endpoint. No batching.

| Setting                    | Description              | Default |
|----------------------------|--------------------------|---------|
| `url`                      | **Required.** Target URL | -       |
| `method`                   | HTTP method              | `POST`  |
| `pooledConnectionLifetime` | Connection pool lifetime | 5 min   |

**Authentication options:**

| Auth Type | Settings                                                                             |
|-----------|--------------------------------------------------------------------------------------|
| None      | No additional settings                                                               |
| Basic     | `authentication:scheme: Basic`, `authentication:username`, `authentication:password` |
| Bearer    | `authentication:scheme: Bearer`, `authentication:token`                              |

**URL templates** support dynamic segments:

| Template Variable  | Description         |
|--------------------|---------------------|
| `{schema-subject}` | Schema subject name |
| `{event-type}`     | Event type name     |
| `{stream}`         | Source stream ID    |

**Custom headers:** Use `defaultHeaders:` prefix to add static headers to every request.

```json
{
  "instanceTypeName": "http-sink",
  "settings": {
    "url": "https://api.example.com/events/{event-type}",
    "authentication:scheme": "Bearer",
    "authentication:token": "my-secret-token",
    "defaultHeaders:X-Source": "kurrentdb",
    "subscription:filter:scope": "stream",
    "subscription:filter:filterType": "prefix",
    "subscription:filter:expression": "order-"
  }
}
```

---

### Kafka Sink (License Required)

Writes events to a Kafka topic using an idempotent producer.

| Setting            | Description                     | Default          |
|--------------------|---------------------------------|------------------|
| `topic`            | **Required.** Kafka topic name  | -                |
| `bootstrapServers` | Kafka broker addresses          | `localhost:9092` |
| `waitForBrokerAck` | Block until broker acknowledges | `true`           |

**Authentication:**

| Auth Type      | Settings                                                                                                                                                                |
|----------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Plaintext      | `authentication:securityProtocol: plaintext`                                                                                                                            |
| SASL Plaintext | `authentication:securityProtocol: saslPlaintext`, `authentication:saslMechanism: plain\|scramSha256\|scramSha512`, `authentication:username`, `authentication:password` |
| SASL SSL       | `authentication:securityProtocol: saslSsl`, plus SASL settings above                                                                                                    |

**Partition key strategies:**

| Strategy       | Description                   |
|----------------|-------------------------------|
| `partitionKey` | Use the event's partition key |
| `stream`       | Use the source stream ID      |
| `streamSuffix` | Use suffix of the stream ID   |
| `headers`      | Use a specific header value   |

**Compression:** `None`, `Gzip`, `Lz4`, `Zstd` (default), `Snappy`

**Note:** Kafka Sink uses its **own retry mechanism**, not the standard resilience backoff.

```json
{
  "instanceTypeName": "kafka-sink",
  "settings": {
    "topic": "events",
    "bootstrapServers": "kafka1:9092,kafka2:9092",
    "authentication:securityProtocol": "saslSsl",
    "authentication:saslMechanism": "scramSha256",
    "authentication:username": "producer",
    "authentication:password": "secret",
    "compression": "Zstd",
    "partitionKeyStrategy": "stream"
  }
}
```

---

### RabbitMQ Sink (License Required)

Publishes events to a RabbitMQ exchange with a routing key.

| Setting            | Description                     | Default     |
|--------------------|---------------------------------|-------------|
| `exchange:name`    | **Required.** Exchange name     | -           |
| `exchange:type`    | Exchange type                   | `fanout`    |
| `routingKey`       | Routing key                     | -           |
| `host`             | RabbitMQ host                   | `localhost` |
| `port`             | RabbitMQ port                   | `5672`      |
| `virtualHost`      | Virtual host                    | `/`         |
| `durable`          | Durable exchange                | `true`      |
| `autoDelete`       | Auto-delete exchange            | `false`     |
| `waitForBrokerAck` | Block until broker acknowledges | `false`     |

**Authentication:** `authentication:username` / `authentication:password` (default: `guest`/`guest`)

**Note:** RabbitMQ Sink uses its **own retry mechanism**, not the standard resilience backoff.

```json
{
  "instanceTypeName": "rabbit-mq-sink",
  "settings": {
    "exchange:name": "events",
    "exchange:type": "topic",
    "routingKey": "order.placed",
    "host": "rabbitmq.local",
    "authentication:username": "app",
    "authentication:password": "secret",
    "durable": true,
    "waitForBrokerAck": true
  }
}
```

---

### SQL Sink (License Required)

Writes events to SQL Server or PostgreSQL using SQL statement mappings.

| Setting            | Description                                   | Default                     |
|--------------------|-----------------------------------------------|-----------------------------|
| `type`             | **Required.** Database type                   | `SqlServer` or `PostgreSql` |
| `connectionString` | **Required.** Database connection string      | -                           |
| `reducer:mappings` | **Required.** SQL statement + extractor pairs | -                           |

**Important:** The SQL Sink requires **JSON content type** events.

**Statement mappings** consist of:
1. A SQL template with positional parameters
2. A JavaScript extractor function (base64-encoded) that returns an array of values

**Helper functions** available in extractors:
- `Guid(string)` - Parse GUID/UUID
- `DateTime(string)` - Parse date/time
- `TimeSpan(string)` - Parse time span

**Authentication:**
- SQL Server: username/password in connection string
- PostgreSQL: username/password or client certificate

```json
{
  "instanceTypeName": "sql-sink",
  "settings": {
    "type": "PostgreSql",
    "connectionString": "Host=localhost;Database=orders;Username=app;Password=secret",
    "reducer:mappings:0:statement": "INSERT INTO orders (id, customer, amount) VALUES ($1, $2, $3) ON CONFLICT (id) DO UPDATE SET customer=$2, amount=$3",
    "reducer:mappings:0:extractor": "<base64-encoded JS function>"
  }
}
```

Extractor function example (before base64 encoding):

```javascript
function extract(record) {
  return [
    Guid(record.value.orderId),
    record.value.customerName,
    record.value.amount
  ];
}
```

---

### MongoDB Sink (License Required)

Writes events as BSON documents to a MongoDB collection.

| Setting            | Description                             | Default |
|--------------------|-----------------------------------------|---------|
| `database`         | **Required.** Database name             | -       |
| `collection`       | **Required.** Collection name           | -       |
| `connectionString` | **Required.** MongoDB connection string | -       |
| `batchSize`        | Documents per batch                     | `1000`  |
| `batchTimeoutMs`   | Batch flush timeout (ms)                | `250`   |

**Authentication:**
- SCRAM (SHA-1 or SHA-256): username/password in connection string
- X.509: client certificate authentication

**Document ID strategies:**

| Strategy             | Description                           |
|----------------------|---------------------------------------|
| `recordId` (default) | Use the event record ID               |
| `stream`             | Use stream ID (with regex extraction) |
| `streamSuffix`       | Use suffix of the stream ID           |
| `headers`            | Use a specific header value           |

Event metadata is stored in the `_metadata` field of each document.

```json
{
  "instanceTypeName": "mongo-db-sink",
  "settings": {
    "database": "events",
    "collection": "orders",
    "connectionString": "mongodb://user:pass@localhost:27017",
    "batchSize": 500,
    "documentIdStrategy": "stream"
  }
}
```

---

### Elasticsearch Sink (License Required)

Indexes events as JSON documents in an Elasticsearch index.

| Setting            | Description                     | Default |
|--------------------|---------------------------------|---------|
| `connectionString` | **Required.** Elasticsearch URL | -       |
| `index`            | **Required.** Target index name | -       |
| `batchSize`        | Documents per batch             | `1000`  |
| `batchTimeoutMs`   | Batch flush timeout (ms)        | `250`   |
| `refresh`          | Index refresh behavior          | `true`  |

**Refresh options:** `true` (immediate, default), `wait_for` (wait for next refresh), `false` (no refresh)

**Authentication:**

| Auth Type          | Settings                                                                             |
|--------------------|--------------------------------------------------------------------------------------|
| Basic (default)    | `authentication:scheme: basic`, `authentication:username`, `authentication:password` |
| Token              | `authentication:scheme: token`, `authentication:token`                               |
| API Key            | `authentication:scheme: apiKey`, `authentication:apiKey`                             |
| Client Certificate | `authentication:clientCertificate`                                                   |
| Root Certificate   | `authentication:rootCertificate`                                                     |

**Document ID strategies:** Same as MongoDB (`recordId`, `stream`, `streamSuffix`, `headers`).

```json
{
  "instanceTypeName": "elasticsearch-sink",
  "settings": {
    "connectionString": "https://elasticsearch:9200",
    "index": "order-events",
    "authentication:scheme": "apiKey",
    "authentication:apiKey": "my-api-key",
    "batchSize": 500,
    "refresh": "wait_for"
  }
}
```

---

### Pulsar Sink (License Required)

Writes events to an Apache Pulsar topic.

| Setting                    | Description                     | Default                   |
|----------------------------|---------------------------------|---------------------------|
| `topic`                    | **Required.** Pulsar topic name | -                         |
| `url`                      | Pulsar service URL              | `pulsar://localhost:6650` |
| `resilience:retryInterval` | Retry interval                  | `3s`                      |

**Authentication:** JWT token via `authentication:token`

**Partition key strategies:** `stream`, `streamSuffix`, `headers`

**Note:** Pulsar Sink uses its **own retry mechanism** with a configurable `resilience:retryInterval`.

```json
{
  "instanceTypeName": "pulsar-sink",
  "settings": {
    "topic": "persistent://public/default/events",
    "url": "pulsar://pulsar.local:6650",
    "authentication:token": "eyJhbGci..."
  }
}
```

---

### Serilog Sink (Free)

Outputs events to Console, File, or Seq using Serilog structured logging.

| Setting             | Description                               | Default |
|---------------------|-------------------------------------------|---------|
| `configuration`     | Base64-encoded Serilog JSON configuration | -       |
| `includeRecordData` | Include full record data in log output    | `true`  |

The `configuration` setting accepts a standard Serilog JSON configuration, base64-encoded.

```json
{
  "instanceTypeName": "serilog-sink",
  "settings": {
    "configuration": "<base64-encoded Serilog JSON config>",
    "includeRecordData": true,
    "subscription:filter:scope": "stream",
    "subscription:filter:filterType": "prefix",
    "subscription:filter:expression": "order-"
  }
}
```

---

## Source Connectors

### Kafka Source (License Required)

Consumes messages from a Kafka topic and writes them into KurrentDB streams.

| Setting             | Description                          | Default        |
|---------------------|--------------------------------------|----------------|
| `topic`             | **Required.** Kafka topic to consume | -              |
| `schemaName`        | **Required.** Schema name for events | -              |
| `partition`         | Specific partition to consume        | All partitions |
| `offset`            | Starting offset                      | -              |
| `preserveMagicByte` | Preserve Kafka magic byte            | -              |
| `channelCapacity`   | Internal channel buffer size         | `10000`        |
| `tasks`             | Number of concurrent consumer tasks  | `1`            |

**Stream routing strategies:**

| Strategy          | Description                             |
|-------------------|-----------------------------------------|
| `topic` (default) | Write to a stream named after the topic |
| `partitionKey`    | Route by Kafka partition key            |
| `fixed`           | Write to a fixed stream name            |
| `header`          | Route by a specific Kafka header value  |

**Authentication:** `plaintext`, `ssl`, `saslPlaintext` (same options as Kafka Sink).

```json
{
  "instanceTypeName": "kafka-source",
  "settings": {
    "topic": "external-events",
    "schemaName": "ExternalEvent",
    "bootstrapServers": "kafka:9092",
    "streamRouting": "partitionKey",
    "tasks": 4,
    "channelCapacity": 50000
  }
}
```

---

## Metrics

Connectors expose Prometheus-compatible metrics for monitoring.

| Metric                                                     | Type      | Description                 |
|------------------------------------------------------------|-----------|-----------------------------|
| `kurrent_connector_active_total`                           | Gauge     | Number of active connectors |
| `kurrent_sink_written_total_records`                       | Histogram | Records written by sinks    |
| `kurrent_sink_errors_total`                                | Counter   | Total sink errors           |
| `messaging_kurrent_consumer_message_count_total`           | Counter   | Messages consumed           |
| `messaging_kurrent_consumer_commit_latency`                | Histogram | Commit latency              |
| `messaging_kurrent_consumer_lag`                           | Gauge     | Consumer lag                |
| `messaging_kurrent_producer_queue_length`                  | Gauge     | Producer queue length       |
| `messaging_kurrent_producer_message_count_total`           | Counter   | Messages produced           |
| `messaging_kurrent_producer_produce_duration_milliseconds` | Histogram | Produce duration            |
