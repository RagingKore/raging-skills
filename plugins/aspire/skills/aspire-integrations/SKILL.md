---
name: aspire-integrations
description: |
  .NET Aspire integrations routing guide for hosting and client packages across databases, messaging,
  caching, and storage. Covers the two-package pattern (hosting package in AppHost, client package in
  app project), both standard Aspire client integration and explicit environment-variable-only approaches,
  and quick reference for common integrations including PostgreSQL (AddPostgres), SQL Server
  (AddSqlServer), Redis (AddRedis), MongoDB (AddMongoDB), RabbitMQ (AddRabbitMQ), NATS, Azure Service
  Bus (AddAzureServiceBus), Azure Event Hubs, Cosmos DB, Keycloak, Seq, Ollama, Elasticsearch, Garnet,
  Azure Blob Storage, and MinIO. Use when adding an Aspire integration, calling AddPostgres or AddRedis
  or AddRabbitMQ or AddSqlServer or AddMongoDB, configuring an aspire database, setting up aspire
  messaging or aspire caching, choosing between Aspire client packages and standard packages,
  or looking up which NuGet package provides a specific Aspire integration.
---

# .NET Aspire Integrations

Integration patterns and quick reference for Aspire hosting and client packages targeting Aspire 13.x on .NET 10.

## How Integrations Work

Every Aspire integration follows a two-package pattern:

1. **Hosting package** (AppHost) - Configures the resource in the application model. Installed in the AppHost
   project.
2. **Client package** (app project) - Configures the client library in the consuming application. Installed in
   the service project.

```
AppHost (orchestrator)           App Project (consumer)
┌──────────────────────┐         ┌──────────────────────┐
│ Aspire.Hosting.Redis │────────>│ Aspire.StackExchange │
│                      │ injects │ .Redis               │
│ builder.AddRedis()   │ env vars│                      │
└──────────────────────┘         │ builder.AddRedis     │
                                 │ Client("cache")      │
                                 └──────────────────────┘
```

The hosting package registers the resource and generates connection information. The client package reads that
connection information from environment variables and configures the appropriate client library.

## Configuration Approaches

Both approaches are equally valid. Choose based on project requirements.

### Standard: Aspire Client Packages

Install the Aspire client NuGet package in the app project. It auto-configures the client library using service
discovery and Aspire-injected environment variables:

```csharp
// AppHost
var cache = builder.AddRedis("cache");
var api = builder.AddProject<Projects.Api>("api")
    .WithReference(cache);

// Api project - Aspire.StackExchange.Redis
builder.AddRedisClient("cache");
```

Benefits: automatic health checks, telemetry, retry policies, and configuration binding.

### Explicit: Standard Packages Only

The AppHost maps resource outputs to environment variables. The app project uses standard libraries with no
Aspire client dependencies:

```csharp
// AppHost - same as above
var cache = builder.AddRedis("cache");
var api = builder.AddProject<Projects.Api>("api")
    .WithReference(cache);

// Api project - standard StackExchange.Redis, no Aspire package
var connStr = builder.Configuration.GetConnectionString("cache");
var redis = ConnectionMultiplexer.Connect(connStr);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
```

Benefits: no Aspire dependency in app projects, portable code, works outside Aspire.

## Common Integrations Quick Reference

### Databases

| Resource   | Hosting Package                 | AppHost API                  | Client Package                                                                       |
|------------|---------------------------------|------------------------------|--------------------------------------------------------------------------------------|
| PostgreSQL | `Aspire.Hosting.PostgreSQL`     | `AddPostgres("pg")`          | `Aspire.Npgsql` / `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`                     |
| SQL Server | `Aspire.Hosting.SqlServer`      | `AddSqlServer("sql")`        | `Aspire.Microsoft.Data.SqlClient` / `Aspire.Microsoft.EntityFrameworkCore.SqlServer` |
| MySQL      | `Aspire.Hosting.MySql`          | `AddMySql("mysql")`          | `Aspire.MySqlConnector` / `Aspire.Pomelo.EntityFrameworkCore.MySql`                  |
| MongoDB    | `Aspire.Hosting.MongoDB`        | `AddMongoDB("mongo")`        | `Aspire.MongoDB.Driver`                                                              |
| Cosmos DB  | `Aspire.Hosting.Azure.CosmosDB` | `AddAzureCosmosDB("cosmos")` | `Aspire.Microsoft.Azure.Cosmos`                                                      |

### Caching

| Resource | Hosting Package         | AppHost API          | Client Package                            |
|----------|-------------------------|----------------------|-------------------------------------------|
| Redis    | `Aspire.Hosting.Redis`  | `AddRedis("cache")`  | `Aspire.StackExchange.Redis`              |
| Garnet   | `Aspire.Hosting.Garnet` | `AddGarnet("cache")` | `Aspire.StackExchange.Redis` (compatible) |

### Messaging

| Resource          | Hosting Package                              | AppHost API                              | Client Package                               |
|-------------------|----------------------------------------------|------------------------------------------|----------------------------------------------|
| RabbitMQ          | `Aspire.Hosting.RabbitMQ`                    | `AddRabbitMQ("rabbit")`                  | `Aspire.RabbitMQ.Client`                     |
| NATS              | `Aspire.Hosting.Nats`                        | `AddNats("nats")`                        | `Aspire.NATS.Net`                            |
| Azure Service Bus | `Aspire.Hosting.Azure.ServiceBus`            | `AddAzureServiceBus("sb")`               | `Aspire.Azure.Messaging.ServiceBus`          |
| Azure Event Hubs  | `Aspire.Hosting.Azure.EventHubs`             | `AddAzureEventHubs("eh")`                | `Aspire.Azure.Messaging.EventHubs`           |

### Storage

| Resource            | Hosting Package                              | AppHost API                            | Client Package                               |
|---------------------|----------------------------------------------|----------------------------------------|----------------------------------------------|
| Azure Blob Storage  | `Aspire.Hosting.Azure.Storage`               | `AddAzureStorage("storage")`           | `Aspire.Azure.Storage.Blobs`                 |
| MinIO               | `Aspire.Hosting.Minio`                       | `AddMinio("minio")`                    | Standard AWS S3 SDK (S3-compatible)          |

### Other

| Resource         | Hosting Package                              | AppHost API                         | Client Package                                  |
|------------------|----------------------------------------------|-------------------------------------|-------------------------------------------------|
| Keycloak         | `Aspire.Hosting.Keycloak`                    | `AddKeycloak("keycloak")`           | Standard OIDC libraries                         |
| Seq              | `Aspire.Hosting.Seq`                         | `AddSeq("seq")`                     | `Aspire.Seq`                                    |
| Ollama           | `Aspire.Hosting.Ollama`                      | `AddOllama("ollama")`               | Standard Ollama client                          |
| Elasticsearch    | `Aspire.Hosting.Elasticsearch`               | `AddElasticsearch("es")`            | `Aspire.Elastic.Clients.Elasticsearch`          |

## Adding an Integration

Follow this pattern for any integration:

**Step 1.** Install the hosting package in the AppHost project:

```bash
dotnet add src/AppHost/AppHost.csproj package Aspire.Hosting.PostgreSQL
```

**Step 2.** Configure the resource in `Program.cs` of the AppHost:

```csharp
var db = builder.AddPostgres("pg")
    .WithDataVolume()
    .AddDatabase("catalog");

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(db)
    .WaitFor(db);
```

**Step 3.** Install the client package in the consuming project (if using Aspire client approach):

```bash
dotnet add src/Api/Api.csproj package Aspire.Npgsql.EntityFrameworkCore.PostgreSQL
```

**Step 4.** Configure the client in the consuming project:

```csharp
// Standard approach with Aspire client package
builder.AddNpgsqlDbContext<CatalogContext>("catalog");

// OR explicit approach with standard package
var connStr = builder.Configuration.GetConnectionString("catalog");
builder.Services.AddDbContext<CatalogContext>(o => o.UseNpgsql(connStr));
```

## Official Docs Reference

For the complete and current integration catalog with detailed configuration options, see the official gallery:

**https://aspire.dev/integrations/gallery/**

The gallery lists all officially supported integrations, community integrations, their NuGet packages, and
configuration documentation.

## Learn More

| Topic                          | How to Find                                                                                          |
|--------------------------------|------------------------------------------------------------------------------------------------------|
| Integration overview           | `microsoft_docs_search(query=".NET Aspire integrations overview")`                                   |
| Custom integration authoring   | `microsoft_docs_search(query=".NET Aspire custom integration component")`                            |
| Service discovery              | `microsoft_docs_search(query=".NET Aspire service discovery configuration")`                         |
| Integration testing            | `microsoft_docs_search(query=".NET Aspire integration testing")`                                     |
