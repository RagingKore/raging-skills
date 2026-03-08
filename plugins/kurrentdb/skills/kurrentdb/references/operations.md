# Operations Reference

## Table of Contents

- [Scavenging](#scavenging)
- [Auto-Scavenge](#auto-scavenge)
- [Backup and Restore](#backup-and-restore)
- [Archiving](#archiving)
- [Redaction (GDPR)](#redaction-gdpr)

---

## Scavenging

Scavenging removes deleted and expired events from disk to reclaim space. It is a **destructive** operation — scavenged events cannot be recovered.

### Starting and Stopping

| Action         | Method | Endpoint                  |
|----------------|--------|---------------------------|
| Start scavenge | POST   | `/admin/scavenge`         |
| Stop by ID     | DELETE | `/admin/scavenge/{id}`    |
| Stop current   | DELETE | `/admin/scavenge/current` |
| Get current    | GET    | `/admin/scavenge/current` |

Requires `$admin` or `$ops` credentials.

### Parameters

| Parameter         | Default | Description                         |
|-------------------|---------|-------------------------------------|
| `threads`         | 1       | Number of parallel scavenge threads |
| `threshold`       | 0       | Minimum space reclaim threshold (%) |
| `throttlePercent` | —       | Throttle between 1-100%             |
| `syncOnly`        | —       | Only synchronize, do not execute    |

### Scavenge Phases

1. **Beginning** — Initialization
2. **Accumulation** — Scans all chunks for deletable events
3. **Calculation** — Determines which chunks need rewriting
4. **Execution** — Rewrites chunks without deleted events
5. **Cleaning** — Removes old chunk files

The first scavenge is the longest because it must accumulate data from all chunks.

### Important Notes

- Scavenging must run on **each node independently**.
- Scavenge can be stopped and resumed.
- Progress events are written to the `$scavenges` stream.
- **Stop scavenge before performing file-copy backups.**

---

## Auto-Scavenge

**License Required — Available since v24.10**

Automated, scheduled cluster-wide scavenging.

### Behavior

- Uses CRON-based scheduling.
- Runs on **one node at a time**, preferring non-leader nodes.
- Managed via HTTP API.

### API Endpoints

| Action             | Method | Endpoint                   |
|--------------------|--------|----------------------------|
| Configure schedule | POST   | `/auto-scavenge/configure` |
| Get status         | GET    | `/auto-scavenge/status`    |
| Pause              | POST   | `/auto-scavenge/pause`     |
| Resume             | POST   | `/auto-scavenge/resume`    |

### Example: Configure Schedule

```bash
curl -X POST https://localhost:2113/auto-scavenge/configure \
  -H "Content-Type: application/json" \
  -d '{"schedule": "0 2 * * 0"}'
```

This schedules scavenging every Sunday at 2:00 AM.

---

## Backup and Restore

### Backup Methods

| Method                       | Complexity | Notes                                                              |
|------------------------------|------------|--------------------------------------------------------------------|
| **Disk snapshots**           | Low        | Preferred method — easiest and most reliable                       |
| **File copy (full)**         | Medium     | Copy all database and index files                                  |
| **File copy (differential)** | Medium     | Copy only changed files. **NOT supported with secondary indexes.** |

### What to Back Up

**Database files:**

- Chunk files (`chk-X.Y`)
- Checkpoint files (`*.chk`)
- DuckDB files

**Index files:**

- `indexmap`
- UUID lookup files
- Bloom filters
- Scavenge database
- Stream existence filter

### Backup Source

- Back up **one connected node** (or a quorum of nodes for maximum safety).
- A **ReadOnlyReplica** can serve as a backup source to avoid impacting the cluster.

### Full Backup Order

Follow this order to ensure consistency:

1. Index checkpoints
2. Index files
3. Database checkpoints
4. Chunk files

### Restore Procedure

1. **Stop** the target node.
2. **Copy** all backup files to the data directory.
3. **Copy** `chaser.chk` as `truncate.chk` to trigger truncation on startup.
4. **Start** the node.

### Critical Rules

- **NEVER** mix files from different nodes in a single restore.
- **Stop scavenge** and **pause auto-scavenge** before starting any backup.

---

## Archiving

**License Required — Available since v25.0**

Archive chunk files to cloud object storage (S3, Azure Blob, GCP Cloud Storage) for cold storage. All cluster nodes can transparently read from the archive.

### Architecture

- An **Archiver Node** (configured as a read-only replica) handles uploads.
- Other cluster nodes read archived chunks transparently when needed.
- Only **one Archiver per cluster**.

### Configuration

```yaml
---
Archive:
  Enabled: true
  RetainAtLeast:
    Days: 30
    LogicalBytes: 10737418240
  StorageType: S3
```

### Storage Providers

**Amazon S3:**

```yaml
Archive:
  StorageType: S3
  S3:
    Region: us-east-1
    Bucket: my-kurrentdb-archive
```

**Google Cloud Storage:**

```yaml
Archive:
  StorageType: GCP
  GCP:
    Bucket: my-kurrentdb-archive
```

Uses Application Default Credentials.

**Azure Blob Storage:**

```yaml
Archive:
  StorageType: Azure
  Azure:
    Container: my-kurrentdb-archive
    Auth: Default
```

Auth options: `Default`, `ConnectionString`, `SystemAssigned`, `UserAssigned`.

### Retention

The `RetainAtLeast` setting ensures a minimum amount of data is always available locally. At minimum, `MaxMemTableSize` events are kept locally.

### Critical Rules

- **Do not share** an archive bucket between different clusters.
- Only **one Archiver node** per cluster.
- The Archiver must be **restored from its own backup** (not another node's).

### Limitations

- Archive reads are queued **behind** other read operations.
- No local caching of archived chunks.
- Archived chunks are **not scavenged** further.

---

## Redaction (GDPR)

KurrentDB supports event redaction for GDPR data erasure requirements. Redaction replaces event data with a redaction marker while preserving the event's position in the stream. Refer to the KurrentDB documentation for detailed redaction procedures.
