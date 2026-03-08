# Diagnostics Reference

## Table of Contents

- [Statistics](#statistics)
- [Logging](#logging)
- [Metrics](#metrics)
- [OpenTelemetry Integration](#opentelemetry-integration)

---

## Statistics

### HTTP Endpoint

```
GET /stats
```

Returns per-node statistics in JSON format.

### Stats Stream

Statistics are optionally written to the `$stats-{host:port}` system stream as `$statsCollected` events with a default TTL of **24 hours**.

| Setting          | Default | Description                        |
|------------------|---------|------------------------------------|
| `StatsPeriodSec` | 30      | Collection interval in seconds     |
| `WriteStatsToDb` | `false` | Write stats events to the database |

### Key Statistics

| Category | Metric            | Description                   |
|----------|-------------------|-------------------------------|
| Process  | `proc-mem`        | Process memory usage          |
| Process  | `proc-cpu`        | Process CPU usage             |
| System   | `sys-freeMem`     | System free memory            |
| Runtime  | `GC`              | Garbage collection statistics |
| Network  | `TCP connections` | Active TCP connection count   |
| Queues   | `queues`          | Internal queue depths         |
| Storage  | `writer flush`    | Writer flush latency          |
| Cache    | `readIndex cache` | Read index cache hit rate     |

---

## Logging

### Log Output

KurrentDB supports structured and plain-text logging to both console and file.

### Configuration Settings

| Setting                 | Default           | Description                        |
|-------------------------|-------------------|------------------------------------|
| `Log`                   | —                 | Log file directory path            |
| `LogLevel`              | `Default`         | Minimum log level                  |
| `LogConsoleFormat`      | `Plain`           | Console format: `Plain` or `Json`  |
| `LogFileInterval`       | `Day`             | Log file rotation interval         |
| `LogFileRetentionCount` | 31                | Number of log files to retain      |
| `LogFileSize`           | 1073741824 (1 GB) | Maximum log file size in bytes     |
| `DisableLogFile`        | `false`           | Disable file logging               |
| `LogConfig`             | `logconfig.json`  | Path to advanced log configuration |

### Seq Integration

Configure Seq output in `logconfig.json`:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://seq.example.com:5341"
        }
      }
    ]
  }
}
```

---

## Metrics

All KurrentDB metrics use the `kurrentdb_` prefix (changed from `eventstore_` in v25.0).

### Export Formats

- **Prometheus** — Scrape endpoint
- **OpenTelemetry** — OTLP push

Configuration is managed via `metricsconfig.json`.

### Projection Metrics (v25.1+)

| Metric                                                          | Description                                       |
|-----------------------------------------------------------------|---------------------------------------------------|
| `kurrentdb_projection_state_size`                               | Current projection state size in bytes            |
| `kurrentdb_projection_state_size_bound`                         | Upper bound of projection state size              |
| `kurrentdb_projection_state_serialization_duration_max_seconds` | Max state serialization duration                  |
| `kurrentdb_projection_execution_duration_max_seconds`           | Max projection execution duration                 |
| `kurrentdb_projection_execution_duration_seconds_bucket`        | Execution duration histogram (**off by default**) |

### Persistent Subscription Metrics (v25.1+)

| Metric                                            | Description                     |
|---------------------------------------------------|---------------------------------|
| `kurrentdb_persistent_sub_parked_message_replays` | Count of parked message replays |
| `kurrentdb_persistent_sub_park_message_requests`  | Count of park message requests  |

### Thread Pool Metric (v26.0)

Thread pool queue length measured in seconds.

### Configurable Slow Message Thresholds (v26.0)

Customize slow message detection thresholds per bus:

```yaml
---
Metrics:
  SlowMessageMilliseconds:
    MainBus: 48
    StorageWriterBus: 500
```

---

## OpenTelemetry Integration

### Metrics and Logs Export

OTLP export is supported for metrics. Log export via OTLP requires a **license**.

### .NET Client Tracing

Add KurrentDB client instrumentation to your .NET application:

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder.AddKurrentDBClientInstrumentation();
    });
```

The activity source name is `"kurrentdb"`.

### Trace Attributes

| Attribute                         | Description                                    |
|-----------------------------------|------------------------------------------------|
| `db.user`                         | Authenticated user                             |
| `db.system`                       | Always `"eventstoredb"`                        |
| `db.operation`                    | Operation type (append, read, subscribe, etc.) |
| `db.eventstoredb.stream`          | Target stream name                             |
| `db.eventstoredb.subscription.id` | Subscription identifier                        |
| `db.eventstoredb.event.id`        | Event identifier                               |
| `db.eventstoredb.event.type`      | Event type name                                |
