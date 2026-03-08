# Queries & Embedded Web UI Reference

## Table of Contents

- [Embedded Web UI Overview](#embedded-web-ui-overview)
- [Accessing the Web UI](#accessing-the-web-ui)
- [Web UI Pages](#web-ui-pages)
- [SQL Query Engine](#sql-query-engine)
- [Virtual Tables](#virtual-tables)
- [Column Reference](#column-reference)
- [Query Syntax & Examples](#query-syntax--examples)
- [JSON Field Access](#json-field-access)
- [Querying User-Defined Indexes](#querying-user-defined-indexes)
- [Performance Guide](#performance-guide)
- [Using the Query Editor](#using-the-query-editor)
- [Legacy Web UI](#legacy-web-ui)

---

## Embedded Web UI Overview

**Available since v25.0.** The embedded Web UI provides visual cluster management, real-time monitoring, and ad-hoc SQL query capabilities directly in the browser — no external tools required.

The new embedded UI replaces the legacy web interface (`/web`) which is reaching end of life. Feature parity is being split between the embedded UI and [Kurrent Navigator](https://navigator.kurrent.io/).

---

## Accessing the Web UI

Navigate to `http(s)://<SERVER_IP>:2113` in your browser. The embedded UI is served on the same port as the gRPC/HTTP API.

Default credentials: `admin` / `changeit`

---

## Web UI Pages

### Dashboard (Free)

The entry point showing:

- **Cluster status**: Cluster state and node count
- **Cluster nodes**: List of nodes with individual status
- **Node resources**: CPU, memory, and disk utilization
- **Node metrics** (License Required): Events written/read counts and open connections

### Database Stats (License Required)

Requires [secondary indexes](indexes.md) to be enabled. Shows:

| Section               | Details                                                                            |
|-----------------------|------------------------------------------------------------------------------------|
| Stream categories     | Categories with event types per category, event counts, earliest/latest timestamps |
| Streams per category  | Stream count per category                                                          |
| Events per category   | Event count per category                                                           |
| Avg. stream length    | Average stream length per category                                                 |
| Explicit transactions | Whether deprecated explicit transactions exist in the database                     |
| Stream stats          | Longest stream per category                                                        |

### Logs (License Required)

Real-time log streaming with:

- Filter by log level
- Filter by message content
- Streams from the moment the page is opened (no historical logs)

### Configuration (License Required)

Read-only configuration viewer showing:

- All configuration options with descriptions
- Current value for each option
- Source of each value (default, environment variable, configuration file)
- Filterable by name, value, and source

### Plugins (License Required)

Lists all loaded plugins and subsystems with name, version, and description.

### Query (License Required)

SQL query editor for ad-hoc data exploration. See [SQL Query Engine](#sql-query-engine) below for full details.

### License

Displays license type, expiration date, and status.

### Navigator

Link to download Kurrent Navigator, the desktop application that will replace the legacy web UI.

---

## SQL Query Engine

**Available since v25.1. Requires a license.**

The query engine provides SQL-based ad-hoc querying of event data, powered by an embedded DuckDB instance. It is designed for **temporary, exploratory use** — not production workloads.

### How It Works

```
SQL Query → DuckDB Engine → Predicate pushdown on indexed fields
                          → Sequential event reads for data/metadata fields
                          → Result set returned to browser
```

The secondary indexes feature builds an index table in DuckDB for default indexes (category and event type). The query engine uses this to offload predicates on indexed fields to DuckDB for fast filtering. Queries that access event `data` or `metadata` read events directly from the transaction log.

### Important Constraints

- **JSON only**: The engine only accesses `data` and `metadata` fields when both use JSON format. Binary-serialized events cannot be queried for their content.
- **No advanced query planning**: KurrentDB can only use the default secondary indexes for predicate pushdown. Complex queries may not perform optimally.
- **Sequential processing**: When reading event data/metadata, the engine processes events sequentially. Large datasets will be slow.
- **Memory limits**: Queries that enumerate large result sets (e.g., `SELECT * FROM all_events`) can exhaust browser or server memory.

---

## Virtual Tables

Three virtual table types are available:

| Virtual Table      | Syntax            | Description                                   | Example                   |
|--------------------|-------------------|-----------------------------------------------|---------------------------|
| Stream             | `stream:{name}`   | Events from a specific stream                 | `stream:order-abc123`     |
| Category           | `category:{name}` | Events from all streams in a category         | `category:order`          |
| All events         | `all_events`      | Every event in the database                   | `all_events`              |
| User-defined index | `index:{name}`    | Events matching a user-defined index (v26.0+) | `index:orders-by-country` |

**There is no virtual table for event types.** Query distinct event types using:

```sql
SELECT DISTINCT event_type FROM all_events;
```

---

## Column Reference

### Indexed Fields (Fast — No Event Read Required)

These columns are stored in the DuckDB secondary index. Filtering on them uses predicate pushdown and is extremely fast.

| Column         | Type      | Description                                                  |
|----------------|-----------|--------------------------------------------------------------|
| `stream`       | string    | Stream name                                                  |
| `category`     | string    | Stream category (derived from stream name, before first `-`) |
| `event_type`   | string    | Event type name                                              |
| `event_number` | integer   | Position of the event within its stream                      |
| `log_position` | integer   | Global log position in the transaction log                   |
| `created_at`   | timestamp | Event creation timestamp                                     |

### Data Fields (Slow — Requires Full Event Read)

These columns require reading the actual event from the transaction log. Use sparingly and always combine with indexed field filters.

| Column     | Type | Description                   |
|------------|------|-------------------------------|
| `data`     | JSON | Event body (deserialized)     |
| `metadata` | JSON | Event metadata (deserialized) |

### User-Defined Index Fields (v26.0+)

When querying a user-defined index via `index:{name}`, additional columns are available based on the index definition. Field columns are prefixed with `field_`:

| Column Pattern | Example         | Description                                     |
|----------------|-----------------|-------------------------------------------------|
| `field_{name}` | `field_country` | Extracted field value from the index definition |

---

## Query Syntax & Examples

Standard SQL syntax is supported via DuckDB. All queries are read-only.

### Basic Queries

```sql
-- List all distinct streams
SELECT DISTINCT stream FROM all_events;

-- Count streams in the database
SELECT count(DISTINCT stream) FROM all_events;

-- List all event types
SELECT DISTINCT event_type FROM all_events;

-- Count events per category
SELECT category, COUNT(*) AS event_count
FROM all_events
GROUP BY category
ORDER BY event_count DESC;
```

### Filtering by Indexed Fields

```sql
-- All events of a specific type in a category
SELECT * FROM category:order
WHERE event_type = 'OrderPlaced';

-- Events from a specific stream
SELECT stream, event_type, event_number, created_at
FROM stream:order-abc123;

-- Events in a time range (indexed field — fast)
SELECT stream, event_type, created_at
FROM category:order
WHERE created_at >= '2026-01-01' AND created_at < '2026-02-01';

-- Count events per event type in a category
SELECT event_type, COUNT(*) AS cnt
FROM category:order
GROUP BY event_type
ORDER BY cnt DESC;
```

### Selecting Specific Columns (Recommended)

```sql
-- GOOD: Only indexed fields — very fast, no event read
SELECT stream, event_type, event_number, log_position, created_at
FROM category:order
WHERE event_type = 'OrderPlaced';

-- BAD: SELECT * forces reading ALL event data — slow on large datasets
SELECT * FROM category:order;
```

### Aggregations

```sql
-- Events per day
SELECT DATE_TRUNC('day', created_at) AS day, COUNT(*) AS events
FROM category:order
GROUP BY day
ORDER BY day DESC;

-- Stream length distribution
SELECT stream, COUNT(*) AS length
FROM category:order
GROUP BY stream
ORDER BY length DESC
LIMIT 20;
```

### Joins

```sql
-- Join events from two categories (use with caution on large datasets)
SELECT o.stream AS order_stream, p.stream AS payment_stream, o.created_at
FROM category:order o
JOIN category:payment p ON o.data->>'orderId' = p.data->>'orderId'
WHERE o.event_type = 'OrderPlaced'
  AND p.event_type = 'PaymentReceived';
```

**Warning**: Joins involving `data` or `metadata` fields read events from both sides. This can generate massive IO on large datasets.

---

## JSON Field Access

Use PostgreSQL-compatible JSON operator syntax to access fields within `data` and `metadata`:

### Operators

| Operator   | Returns | Description                                    |
|------------|---------|------------------------------------------------|
| `->>'key'` | text    | Extract JSON field as text                     |
| `->'key'`  | JSON    | Extract JSON field as JSON (for nested access) |

### Examples

```sql
-- Access top-level fields
SELECT data->>'orderId' AS order_id,
       data->>'amount' AS amount,
       data->>'country' AS country
FROM category:order
WHERE event_type = 'OrderPlaced';

-- Filter by JSON field value
SELECT *
FROM stream:order-abc123
WHERE data->>'status' = 'shipped';

-- Numeric comparison on JSON field
SELECT * FROM stream:order-abc123
WHERE (data->>'amount')::DOUBLE > 100.0;

-- Nested JSON access
SELECT data->'address'->>'city' AS city
FROM category:order
WHERE event_type = 'OrderPlaced';

-- Aggregation on JSON fields
SELECT data->>'region' AS region, COUNT(*) AS order_count
FROM category:order
WHERE event_type = 'OrderPlaced'
GROUP BY data->>'region'
ORDER BY order_count DESC;

-- Access metadata fields
SELECT metadata->>'correlationId' AS correlation_id,
       metadata->>'causationId' AS causation_id
FROM stream:order-abc123;
```

### JSON + Indexed Field Combination (Best Practice)

Always filter by indexed fields first, then by JSON fields. This minimizes the number of events that need to be read from the log:

```sql
-- GOOD: Filter by event_type (indexed) first, then by JSON field
SELECT data->>'orderId' AS order_id, data->>'amount' AS amount
FROM category:order
WHERE event_type = 'OrderPlaced'
  AND data->>'country' = 'Mauritius';

-- BAD: Only filtering by JSON field — scans ALL events in category
SELECT *
FROM category:order
WHERE data->>'country' = 'Mauritius';
```

---

## Querying User-Defined Indexes

**Available since v26.0.** User-defined indexes create queryable virtual tables accessible via the `index:{name}` syntax.

### Reading via SQL

```sql
-- All records in the index
SELECT * FROM index:orders-by-country;

-- Filter by the indexed field (uses field_ prefix)
SELECT * FROM index:orders-by-country
WHERE field_country = 'Mauritius'
LIMIT 10;

-- Aggregation on indexed field
SELECT field_country, COUNT(*) AS order_count
FROM index:orders-by-country
GROUP BY field_country
ORDER BY order_count DESC;
```

### Reading via gRPC Client (Alternative)

User-defined indexes can also be read via the filtered `$all` API without the SQL query UI:

```csharp
// All events matching the index
var all = client.ReadAllAsync(Direction.Forwards, Position.Start,
    StreamFilter.Prefix("$idx-user-orders-by-country"));

// Filtered by specific field value
var mauritius = client.ReadAllAsync(Direction.Forwards, Position.Start,
    StreamFilter.Prefix("$idx-user-orders-by-country:Mauritius"));
```

### Index Name Conventions

| Index Type            | Stream Prefix Pattern                  | SQL Virtual Table                     |
|-----------------------|----------------------------------------|---------------------------------------|
| Category (built-in)   | `$idx-ce-{CATEGORYNAME}`               | N/A (use `category:{name}`)           |
| Event type (built-in) | `$idx-et-{EVENTTYPE}`                  | N/A (use indexed `event_type` column) |
| User-defined          | `$idx-user-{INDEX-NAME}`               | `index:{name}`                        |
| User-defined + field  | `$idx-user-{INDEX-NAME}:{FIELD-VALUE}` | `WHERE field_{name} = 'value'`        |

---

## Performance Guide

### Query Speed by Type

| Query Pattern                        | Speed     | IO Impact                       | Notes                                               |
|--------------------------------------|-----------|---------------------------------|-----------------------------------------------------|
| Index-only columns only              | Very fast | Minimal                         | DuckDB predicate pushdown, no event reads           |
| Index-only with `COUNT`/`DISTINCT`   | Very fast | Minimal                         | ~130M events scanned in < 2 seconds                 |
| Index filter + JSON field access     | Moderate  | Proportional to matching events | Only matching events are read from log              |
| JSON field filter only               | Slow      | High                            | Scans all events in the virtual table scope         |
| `SELECT *` on large tables           | Very slow | Extreme                         | Reads all event data + metadata; can exhaust memory |
| `GROUP BY`/`ORDER BY` on data fields | Slow      | High                            | Requires full enumeration even with `LIMIT`         |
| Joins on data fields                 | Very slow | Extreme                         | Reads events from both sides of the join            |

### Best Practices

1. **Always prefer indexed columns in `WHERE` clauses** — `stream`, `category`, `event_type`, `event_number`, `log_position`, `created_at`
2. **Never use `SELECT *` on large datasets** — select only the columns you need
3. **Use `stream:{name}` or `category:{name}` instead of `all_events`** to limit scan scope
4. **Combine indexed + JSON filters** — filter by `event_type` or `category` first, then by JSON fields
5. **Be cautious with `GROUP BY`/`ORDER BY`** — these require full result enumeration even with `LIMIT`, so ensure the base dataset is filtered down first
6. **Avoid joins on data fields across large categories** — the IO cost compounds multiplicatively
7. **Use user-defined indexes (v26.0+)** for frequently queried JSON fields instead of ad-hoc JSON filtering
8. **For production workloads**, build read models or use projections instead of ad-hoc queries

### When to Use Queries vs Alternatives

| Need                         | Solution                            |
|------------------------------|-------------------------------------|
| Quick data exploration       | SQL queries in Web UI               |
| One-off analysis             | SQL queries in Web UI               |
| Frequent field-based lookups | User-defined index (v26.0+)         |
| Real-time event processing   | Catch-up subscriptions              |
| Scalable event processing    | Persistent subscriptions            |
| Complex aggregations         | External read models                |
| Production read paths        | Projections or external read models |

---

## Using the Query Editor

### Step-by-Step

1. Open the KurrentDB embedded UI at `http(s)://<SERVER_IP>:2113`
2. Click **Query** in the sidebar navigation
3. Enter your SQL query in the code editor
4. Execute by clicking **Run** or pressing `Ctrl+Enter` (Windows/Linux) / `⌘+Enter` (macOS)
5. Results appear in tabular format below the editor

### Editor Features

- **Quick reference**: Click the `?` icon next to the editor for syntax help and available virtual tables/fields
- **Tabular results**: Query output displayed as a scrollable table
- **Direct URL**: Access the query page directly at `https://<SERVER_IP>:2113/ui/query`

### Authentication

The query feature requires the user to be part of the `$admins` group. This is because queries operate through the `$all` stream and secondary indexes, which require admin-level access.

---

## Legacy Web UI

The legacy interface at `SERVER_IP:2113/web` provides additional features not yet in the embedded UI:

| Feature                  | Description                                                           |
|--------------------------|-----------------------------------------------------------------------|
| Stream Browser           | Browse recently created/changed streams, view individual events       |
| Projections              | Manage system and user projections (create, start, stop, edit, debug) |
| Query (Legacy)           | JavaScript projection-based queries (not SQL)                         |
| Persistent Subscriptions | View, create, edit, delete subscriptions; replay parked messages      |
| Admin                    | Manage subsystems, trigger scavenge operations, shutdown server       |
| Users                    | View and manage KurrentDB users                                       |

The legacy UI will be deprecated. Use the embedded UI and Kurrent Navigator going forward.
