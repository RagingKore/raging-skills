# Server Configuration Reference

## Table of Contents

- [Configuration Precedence](#configuration-precedence)
- [Configuration Formats](#configuration-formats)
- [Networking](#networking)
- [Cluster Configuration](#cluster-configuration)
- [Database Settings](#database-settings)
- [Garbage Collection](#garbage-collection)
- [Container Considerations](#container-considerations)

---

## Configuration Precedence

KurrentDB applies configuration from multiple sources in ascending priority order. A higher-priority source overrides any lower-priority source.

| Priority    | Source                 | Example                                  |
|-------------|------------------------|------------------------------------------|
| 1 (lowest)  | Built-in defaults      | —                                        |
| 2           | YAML config file       | `kurrentdb.conf`                         |
| 3           | JSON config files      | `*.json` files under `config/` directory |
| 4           | Environment variables  | `KURRENTDB_` prefix                      |
| 5 (highest) | Command-line arguments | `--` prefix                              |

### Key Behaviors

- The server **refuses to start** if it encounters unknown configuration options.
- Use `--what-if` to print the effective merged configuration without starting the server.

### YAML Config (`kurrentdb.conf`)

YAML configuration files must start with `---` and use spaces for indentation (no tabs).

```yaml
---
Db: /var/lib/kurrentdb
Log: /var/log/kurrentdb
```

### JSON Config

Place `*.json` files in the `config/` directory. As of v26.0, YAML config takes precedence over JSON config.

### Environment Variables

Use the `KURRENTDB_` prefix with double underscores (`__`) for nesting.

```bash
export KURRENTDB_CLUSTER_SIZE=3
export KURRENTDB_DB=/data/kurrentdb
```

### Command-Line Arguments

```bash
kurrentdb --Db /data/kurrentdb --ClusterSize 3
```

---

## Networking

### Ports

| Port | Protocol      | Setting                             | Purpose                             |
|------|---------------|-------------------------------------|-------------------------------------|
| 2113 | HTTP/gRPC     | `NodePort` / `NodeIp`               | Client communication, admin UI      |
| 1112 | TCP           | `ReplicationPort` / `ReplicationIp` | Internal cluster replication        |
| 1113 | TCP (License) | `TcpPlugin:EnableExternalTcp`       | Legacy external TCP client protocol |

### Keepalive

Default keepalive interval and timeout are both **10 seconds**.

### AtomPub

Disabled by default. Enable with `EnableAtomPubOverHttp`. This protocol is **deprecated** and planned for removal.

### NAT / Advertised Addresses

When running behind NAT, load balancers, or in containers, configure advertised addresses so clients and other nodes can reach the server.

| Setting                       | Purpose                                  |
|-------------------------------|------------------------------------------|
| `NodeHostAdvertiseAs`         | External hostname for node communication |
| `NodePortAdvertiseAs`         | External port for node communication     |
| `AdvertiseHostToClientAs`     | Hostname clients should use              |
| `AdvertiseNodePortToClientAs` | Port clients should use                  |

### Kestrel Settings

Fine-tune the HTTP server via `kestrelsettings.json`:

```json
{
  "Kestrel": {
    "Limits": {
      "MaxConcurrentConnections": 100
    },
    "Protocols": {
      "Http2": {
        "InitialConnectionWindowSize": 131072,
        "InitialStreamWindowSize": 98304
      }
    }
  }
}
```

---

## Cluster Configuration

### Cluster Size

| Setting       | Default | Notes                                                                 |
|---------------|---------|-----------------------------------------------------------------------|
| `ClusterSize` | 1       | Must be an **odd number** (1, 3, or 5). Cannot be dynamically scaled. |

A cluster of size `2n + 1` tolerates `n` node failures while maintaining quorum.

| Cluster Size | Quorum | Tolerated Failures |
|--------------|--------|--------------------|
| 1            | 1      | 0                  |
| 3            | 2      | 1                  |
| 5            | 3      | 2                  |

### Node Roles

| Role                | Writes | Quorum Member | Purpose                                           |
|---------------------|--------|---------------|---------------------------------------------------|
| **Leader**          | Yes    | Yes           | Handles all write operations                      |
| **Follower**        | No     | Yes           | Participates in quorum, serves reads              |
| **ReadOnlyReplica** | No     | No            | Serves reads only, not part of the cluster quorum |

### Cluster Discovery

**DNS Discovery:**

```yaml
---
ClusterDns: cluster.example.com
DiscoverViaDns: true
ClusterSize: 3
```

**Gossip Seed Discovery:**

```yaml
---
GossipSeed: 192.168.1.10:2113,192.168.1.11:2113,192.168.1.12:2113
DiscoverViaDns: false
ClusterSize: 3
```

### Gossip Settings

| Setting                     | Default | Description                    |
|-----------------------------|---------|--------------------------------|
| `GossipIntervalMs`          | 2000    | Interval between gossip rounds |
| `GossipTimeoutMs`           | 2500    | Timeout for gossip requests    |
| `GossipAllowedDifferenceMs` | 60000   | Max allowed clock difference   |
| `LeaderElectionTimeoutMs`   | 1000    | Leader election timeout        |

### Node Priority

`NodePriority` influences leader election. A higher value makes the node more likely to become the leader.

---

## Database Settings

### Storage

| Setting                    | Default                      | Description                                                   |
|----------------------------|------------------------------|---------------------------------------------------------------|
| `Db`                       | `/var/lib/kurrentdb` (Linux) | Data file location                                            |
| `MemDb`                    | —                            | **DEPRECATED in v25.1.** Use ramfs instead.                   |
| `UnsafeDisableFlushToDisk` | `false`                      | Disables fsync. **DATA LOSS RISK** — never use in production. |

### Cache Settings

| Setting                   | Default             | Description                                      |
|---------------------------|---------------------|--------------------------------------------------|
| `ChunksCacheSize`         | 536871424 (~512 MB) | Size of the chunks cache in bytes                |
| `CachedChunks`            | -1 (all)            | Number of chunks to cache. -1 caches all chunks. |
| `StreamInfoCacheCapacity` | 100000 (v25.1+)     | Stream info cache entries. 0 = dynamic sizing.   |

### Timeout Settings

| Setting            | Default | Description             |
|--------------------|---------|-------------------------|
| `PrepareTimeoutMs` | 2000    | Prepare phase timeout   |
| `CommitTimeoutMs`  | 2000    | Commit phase timeout    |
| `WriteTimeoutMs`   | 2000    | Write operation timeout |

### Thread Settings

| Setting              | Default | Description                                   |
|----------------------|---------|-----------------------------------------------|
| `ReaderThreadsCount` | 0       | Auto-calculated: 2x processors, min 4, max 16 |
| `WorkerThreads`      | —       | **DEPRECATED in v26.0** — has no effect       |

---

## Garbage Collection

Server GC is enabled by default since v25.1.

| Setting                | Default | Description                                                 |
|------------------------|---------|-------------------------------------------------------------|
| `HeapHardLimitPercent` | 60%     | Maximum managed heap size as percentage of available memory |

---

## Container Considerations

When running in containers, set resource limits appropriately for GC tuning.

**The following options do NOT auto-configure in containers:**

- `StreamInfoCacheCapacity`
- `ReaderThreadsCount`
- `WorkerThreads` (deprecated)

These must be set explicitly when running in containerized environments.

```yaml
---
StreamInfoCacheCapacity: 50000
ReaderThreadsCount: 4
```
