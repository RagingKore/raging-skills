# Migration Guide

## Table of Contents

- [Version History](#version-history)
- [Versioning Scheme](#versioning-scheme)
- [Rolling Upgrade Path](#rolling-upgrade-path)
- [EventStoreDB to KurrentDB Naming Changes](#eventstoredb-to-kurrentdb-naming-changes)
- [Configuration Renames (v25.0)](#configuration-renames-v250)
- [.NET Client Migration](#net-client-migration)
- [Java Client Migration](#java-client-migration)
- [Breaking Changes by Version](#breaking-changes-by-version)
- [Deprecations](#deprecations)
- [Package Repositories](#package-repositories)

---

## Version History

| Version            | Type | Notes                                             |
|--------------------|------|---------------------------------------------------|
| EventStoreDB 22.10 | LTS  | Legacy                                            |
| EventStoreDB 23.10 | LTS  | Last version before rebrand                       |
| EventStoreDB 24.10 | LTS  | Introduced stream policies, encryption-at-rest    |
| KurrentDB 25.0     | STS  | Rebrand from EventStoreDB                         |
| KurrentDB 25.1     | STS  | Server GC, StreamInfoCacheCapacity default change |
| KurrentDB 26.0     | LTS  | Current LTS release                               |

---

## Versioning Scheme

| Type                         | Major Version      | Release Cadence | Support Duration   |
|------------------------------|--------------------|-----------------|--------------------|
| **LTS** (Long-Term Support)  | Even (24, 26, ...) | ~1 per year     | 2 years            |
| **STS** (Short-Term Support) | Odd (25, ...)      | As needed       | Until next release |

Format: `Major.Minor.Patch`

---

## Rolling Upgrade Path

Rolling upgrades are supported but **must follow the sequential upgrade path**:

```
22.10 -> 23.10 -> 24.10 -> 25.0 -> 25.1 -> 26.0
```

Skipping versions is not supported.

---

## EventStoreDB to KurrentDB Naming Changes

| Category         | Before                | After                |
|------------------|-----------------------|----------------------|
| Product          | EventStoreDB          | KurrentDB            |
| Cloud            | EventStore Cloud      | Kurrent Cloud        |
| Config file      | `eventstore.conf`     | `kurrentdb.conf`     |
| Service name     | `eventstore`          | `kurrentdb`          |
| Config directory | `/etc/eventstore/`    | `/etc/kurrentdb/`    |
| Data directory   | `/var/lib/eventstore` | `/var/lib/kurrentdb` |
| Env prefix       | `EVENTSTORE_`         | `KURRENTDB_`         |
| HTTP headers     | `ES-*`                | `Kurrent-*`          |
| Content type     | `vnd.eventstore`      | `vnd.kurrent`        |
| Metrics prefix   | `eventstore_*`        | `kurrentdb_*`        |

---

## Configuration Renames (v25.0)

The following configuration keys were renamed in v25.0:

| Before                        | After                           |
|-------------------------------|---------------------------------|
| `ExtIp`                       | `NodeIp`                        |
| `HttpPort`                    | `NodePort`                      |
| `IntIp`                       | `ReplicationIp`                 |
| `IntTcpPort`                  | `ReplicationPort`               |
| `HttpPortAdvertiseAs`         | `NodePortAdvertiseAs`           |
| `ExtHostAdvertiseAs`          | `NodeHostAdvertiseAs`           |
| `IntHostAdvertiseAs`          | `ReplicationHostAdvertiseAs`    |
| `IntTcpPortAdvertiseAs`       | `ReplicationTcpPortAdvertiseAs` |
| `AdvertiseHttpPortToClientAs` | `AdvertiseNodePortToClientAs`   |

---

## .NET Client Migration

### Package Changes

| Before                           | After              |
|----------------------------------|--------------------|
| `EventStore.Client.Grpc.Streams` | `KurrentDB.Client` |

### Connection String Changes

| Before             | After                   |
|--------------------|-------------------------|
| `esdb://`          | `kurrentdb://`          |
| `esdb+discover://` | `kurrentdb+discover://` |

### Class Name Changes

| Before                       | After                     |
|------------------------------|---------------------------|
| `EventStoreDBClient`         | `KurrentDBClient`         |
| `EventStoreDBClientSettings` | `KurrentDBClientSettings` |

### Example Migration

**Before:**

```csharp
var settings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
var client = new EventStoreClient(settings);
```

**After:**

```csharp
var settings = KurrentDBClientSettings.Create("kurrentdb://localhost:2113?tls=false");
var client = new KurrentDBClient(settings);
```

---

## Java Client Migration

Java client migration can be automated using **OpenRewrite**.

### Package Changes

| Before                                  | After                         |
|-----------------------------------------|-------------------------------|
| `com.eventstore:db-client-java` (Maven) | `io.kurrent:kurrentdb-client` |
| `com.eventstore.dbclient` (package)     | `io.kurrent.dbclient`         |

### Connection String Changes

| Before    | After        |
|-----------|--------------|
| `esdb://` | `kurrent://` |

### Class Name Changes

| Before               | After             |
|----------------------|-------------------|
| `EventStoreDBClient` | `KurrentDBClient` |

### API Changes

| Before                          | After                      | Notes                        |
|---------------------------------|----------------------------|------------------------------|
| `expectedRevision(long)`        | `streamRevision(long)`     | For numeric revision values  |
| `expectedRevision(StreamState)` | `streamState(StreamState)` | For stream state enum values |

---

## Breaking Changes by Version

### v25.0

- Configuration keys renamed (see [Configuration Renames](#configuration-renames-v250))
- Stricter certificate requirements for TLS
- Service name and file paths changed from `eventstore` to `kurrentdb`

### v25.1

- `StreamInfoCacheCapacity` default changed from dynamic to **100000**
- **Server GC** enabled by default (previously workstation GC)

### v26.0

- `WorkerThreads` deprecated and has no effect
- **YAML config takes precedence over JSON config** (previously JSON had higher priority)

---

## Deprecations

| Feature          | Deprecated In | Replacement     | Notes                                    |
|------------------|---------------|-----------------|------------------------------------------|
| TCP API          | v23.10+       | gRPC clients    | Not supported after v23.10               |
| `MemDb`          | v25.1         | ramfs           | Mount tmpfs/ramfs for the data directory |
| `WorkerThreads`  | v26.0         | —               | Setting has no effect                    |
| AtomPub HTTP API | Planned       | gRPC / HTTP API | Still available but planned for removal  |

---

## Package Repositories

All packages are hosted on Cloudsmith (`cloudsmith.io/~eventstore`).

| Repository        | Purpose            | Use Case                       |
|-------------------|--------------------|--------------------------------|
| `kurrent-lts`     | LTS releases only  | Production — stability focused |
| `kurrent-latest`  | LTS + STS releases | Production — latest features   |
| `kurrent-preview` | Preview builds     | Non-production testing only    |
