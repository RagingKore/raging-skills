# Indexes Reference

## Table of Contents

- [Default Index](#default-index)
- [Secondary Indexes](#secondary-indexes)
- [User-Defined Indexes](#user-defined-indexes)
- [Ad-Hoc SQL Queries](#ad-hoc-sql-queries)

---

## Default Index

The default index maps **stream name hash + event number** to the logical position in the transaction log. It is stored separately from data files.

### Architecture

```
Write path:  Event -> Memtable (in-memory) -> PTable (on-disk sorted list)
Read path:   Stream name hash + event# -> Bloom filter check -> PTable lookup -> Log position
```

- **Memtables**: In-memory index entries, flushed to PTables when full
- **PTables**: On-disk sorted lists with midpoint caches for binary search
- **Bloom filters**: Auxiliary files (~1% of index size) providing fast "might exist" vs "definitely not" lookups
- **Auto-merging**: Two PTable files at the same level automatically merge to the next level

### Storage Estimation

Each index entry is **24 bytes**. Approximate index sizes:

| Event Count  | Index Size  | Bloom Filter Size |
|--------------|-------------|-------------------|
| 1 million    | ~24 MB      | ~240 KB           |
| 10 million   | ~240 MB     | ~2.4 MB           |
| 100 million  | ~2.4 GB     | ~24 MB            |
| 1 billion    | ~24 GB      | ~240 MB           |

### Configuration

| Setting                     | Default        | Description                                           |
|-----------------------------|----------------|-------------------------------------------------------|
| `Index`                     | data directory | Index file storage location                           |
| `MaxMemTableSize`           | `1000000`      | Events buffered in memtable before flushing to PTable |
| `IndexCacheDepth`           | `16`           | Midpoint cache depth for PTable binary search         |
| `SkipIndexVerify`           | `false`        | Skip index integrity verification on startup          |
| `MaxAutoMergeIndexLevel`    | `2147483647`   | Maximum PTable level for automatic merging            |
| `StreamExistenceFilterSize` | `256000000`    | Bloom filter size — match to expected stream count    |
| `IndexCacheSize`            | `0` (off)      | Index cache size in bytes                             |
| `UseIndexBloomFilters`      | `true`         | Enable bloom filter files for faster lookups          |

### Manual Merge

When `MaxAutoMergeIndexLevel` is set to limit automatic merges, trigger manual merges via:

```http
POST /admin/mergeindexes
```

### Tuning Tips

- Set `MaxAutoMergeIndexLevel` to prevent simultaneous large merges across cluster nodes — stagger manual merges instead
- Size `StreamExistenceFilterSize` to match your expected stream count for optimal bloom filter performance
- Place the index on fast storage (SSD/NVMe) separate from data files for better IO distribution

---

## Secondary Indexes

**Available since v25.1.** Secondary indexes are stored in DuckDB, separate from the main transaction log.

### Built-In Secondary Indexes

Two secondary indexes are **enabled by default**:

| Index Type       | Replaces Projection | Stream Pattern           |
|------------------|---------------------|--------------------------|
| Category index   | `$by_category`      | `$idx-ce-{CATEGORYNAME}` |
| Event type index | `$by_event_type`    | `$idx-et-{EVENTTYPE}`    |

### Benefits Over System Projections

| Metric              | System Projections        | Secondary Indexes           |
|---------------------|---------------------------|-----------------------------|
| Storage overhead    | Full link events          | Up to **50% less** storage  |
| Read performance    | Link resolution required  | Up to **10x faster** reads  |
| Write amplification | Yes (link events written) | No additional writes        |

### Usage

Read from secondary index streams using `$all` with a stream filter:

```csharp
// Using secondary index (preferred)
var events = client.ReadAllAsync(
    Direction.Forwards,
    Position.Start,
    StreamFilter.Prefix("$idx-et-OrderPlaced"),
    maxCount: 1000
);

// Instead of reading from projection output stream:
// var events = client.ReadStreamAsync(Direction.Forwards, "$et-OrderPlaced", StreamPosition.Start);
```

### Limitations (v25.1)

- Category index always uses **first mode** with `-` separator (not configurable like the `$by_category` projection)
- Deleted events are **not removed** from secondary indexes
- `$maxAge` and `$maxCount` are **not enforced** on index reads
- Reading requires `$admins` group membership (reads go through `$all`)
- **Eventually consistent** — slight delay between event write and index availability
- **Backup**: use volume snapshots — DuckDB files are not append-only and cannot be copied while running

---

## User-Defined Indexes

**Available since v26.0.** Create custom indexes based on event content using JavaScript filter and field selector functions.

### Creating an Index

```http
POST /v2/indexes/orders-by-country
Content-Type: application/json

{
  "filter": "rec => rec.schema.name == 'OrderCreated'",
  "fields": [
    {
      "name": "country",
      "selector": "rec => rec.value.country",
      "type": "INDEX_FIELD_TYPE_STRING"
    }
  ]
}
```

### Field Types

| Type                       | Description          |
|----------------------------|----------------------|
| `INDEX_FIELD_TYPE_STRING`  | String values        |
| `INDEX_FIELD_TYPE_DOUBLE`  | Floating point       |
| `INDEX_FIELD_TYPE_INT_32`  | 32-bit integer       |
| `INDEX_FIELD_TYPE_INT_64`  | 64-bit integer       |

### Record Object (available in filter/selector)

| Property                  | Description                         |
|---------------------------|-------------------------------------|
| `id`                      | Event unique identifier             |
| `timestamp`               | Event creation timestamp            |
| `position.stream`         | Source stream name                  |
| `position.streamRevision` | Event number within stream          |
| `position.logPosition`    | Global log position                 |
| `schema.name`             | Event type name                     |
| `schema.format`           | Serialization format                |
| `sequence`                | Sequence number                     |
| `redacted`                | Whether the event has been redacted |
| `value`                   | Parsed event body (JSON)            |
| `properties`              | Event metadata properties           |

### Reading User-Defined Indexes

Two approaches for reading:

**Stream filter prefix:**

```csharp
// All events in the index
var all = client.ReadAllAsync(Direction.Forwards, Position.Start,
    StreamFilter.Prefix("$idx-user-orders-by-country"));

// Filtered by field value
var mauritius = client.ReadAllAsync(Direction.Forwards, Position.Start,
    StreamFilter.Prefix("$idx-user-orders-by-country:Mauritius"));
```

**SQL query:**

```sql
SELECT * FROM index:orders-by-country
WHERE field_country = 'Mauritius'
LIMIT 10;
```

### Management API

| Operation | Method | Endpoint                           |
|-----------|--------|------------------------------------|
| Create    | POST   | `/v2/indexes/{name}`               |
| Get       | GET    | `/v2/indexes/{name}`               |
| Delete    | DELETE | `/v2/indexes/{name}`               |
| Start     | POST   | `/v2/indexes/{name}/start`         |
| Stop      | POST   | `/v2/indexes/{name}/stop`          |

### Current Limitations

- Maximum **1 field per index** (multiple fields planned for future)
- Updating index definitions after creation is not yet supported
- JsonPath selectors are planned but not yet available

---

## Ad-Hoc SQL Queries

**Available since v25.1. Requires a license.**

The web UI provides a SQL queries page powered by DuckDB for ad-hoc exploration of event data.

### Virtual Tables

| Table              | Description                           |
|--------------------|---------------------------------------|
| `stream:{name}`    | Events from a specific stream         |
| `category:{name}`  | Events from a category                |
| `all_events`       | All events in the database            |

### Column Types

**Indexed fields** (fast, no event data read required):

| Column          | Description                    |
|-----------------|--------------------------------|
| `stream`        | Stream name                    |
| `category`      | Stream category                |
| `event_type`    | Event type name                |
| `event_number`  | Position within the stream     |
| `log_position`  | Global log position            |
| `created_at`    | Event creation timestamp       |

**Data fields** (require full event read — slower):

| Column      | Type | Description                    |
|-------------|------|--------------------------------|
| `data`      | JSON | Event body                     |
| `metadata`  | JSON | Event metadata                 |

### Performance Characteristics

| Query Type         | Performance                                        |
|--------------------|----------------------------------------------------|
| Index-only queries | Very fast — 130M events scanned in under 2 seconds |
| `SELECT *`         | Forces reading all event data — high load          |
| Filtered with JSON | Moderate — depends on data volume scanned          |

### JSON Field Access

Use PostgreSQL-compatible syntax to access JSON fields:

```sql
-- Access a top-level field
SELECT data->>'orderId' AS order_id, data->>'amount' AS amount
FROM category:order
WHERE event_type = 'OrderPlaced';

-- Filter by JSON field value
SELECT *
FROM stream:order-abc123
WHERE data->>'status' = 'shipped';

-- Aggregations on JSON data
SELECT data->>'region' AS region, COUNT(*) AS order_count
FROM category:order
WHERE event_type = 'OrderPlaced'
GROUP BY data->>'region'
ORDER BY order_count DESC;
```

### Best Practices

- **Prefer index-only columns** in WHERE clauses for fast filtering
- **Avoid `SELECT *`** on large datasets — select only the columns you need
- **Use stream or category virtual tables** instead of `all_events` when possible to limit the scan scope
- **Combine indexed filters with JSON filters** — filter by `event_type` first, then by JSON field values
