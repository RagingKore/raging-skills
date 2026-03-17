---
name: aspire
description: |
  .NET Aspire orchestration expert for AppHost, resource model, service discovery, and distributed
  application patterns. Covers project resources (AddProject), container resources (AddContainer),
  service references (WithReference), dependency ordering (WaitFor, WaitForCompletion), parameters
  and secrets (AddParameter), data persistence (WithVolume, WithDataVolume), endpoint configuration
  (WithHttpEndpoint, WithHttpsEndpoint, WithEndpoint, WithExternalHttpEndpoints), existing
  infrastructure (AddConnectionString), networking, container host resolution, and resource lifecycle.
  Supports both standard Aspire client integration packages and explicit environment-variable-only
  approaches. Use when creating an Aspire app, setting up an AppHost, adding a DistributedApplication,
  calling AddProject or AddContainer, wiring WithReference, configuring WaitFor ordering, adding
  Aspire parameters, managing connection strings, attaching volumes, configuring endpoints, enabling
  service discovery, orchestrating a distributed application, working with Aspire resources, setting
  up persistent containers, or debugging Aspire orchestration issues.
---

# .NET Aspire Orchestration

Core patterns for `Aspire.Hosting` AppHost orchestration, the resource model, service discovery, and distributed
application composition targeting Aspire 13.x on .NET 10.

## Quick Decision Matrix

| Need                              | API                                              | Notes                                      |
|-----------------------------------|--------------------------------------------------|--------------------------------------------|
| Add a .NET project                | `AddProject<T>("name")`                          | Type-anchored, launch profile resolved     |
| Add a .NET project by path        | `AddProject("name", "../path.csproj")`           | No type anchor needed                      |
| Add a container                   | `AddContainer("name", "image:tag")`              | OCI image reference                        |
| Pass connection info              | `WithReference(resource)`                        | Injects env vars for service discovery     |
| Order startup                     | `WaitFor(resource)`                              | Wait until started                         |
| Order after completion            | `WaitForCompletion(resource)`                    | Wait until healthy exit                    |
| Accept a secret                   | `AddParameter("name", secret: true)`             | Prompted at launch or set via config       |
| Persist container data            | `WithDataVolume()` / `WithVolume("n", "/path")`  | Named Docker volume                        |
| Expose to host                    | `WithHttpEndpoint(port: 8080)`                   | Map a port on the host                     |
| Use existing infrastructure       | `AddConnectionString("name")`                    | Reads `ConnectionStrings__name`            |

## Quick Start

Create a minimal AppHost with a single API project:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Api>("api");

builder.Build().Run();
```

The `Projects.Api` type is auto-generated when the AppHost references the project. Ensure the AppHost `.csproj`
includes:

```xml
<ItemGroup>
    <ProjectReference Include="..\Api\Api.csproj" />
</ItemGroup>
```

## Project Resources

Add .NET projects to the application model with `AddProject`. Use the generic overload for type-safe project
references or the path overload for projects outside the solution:

```csharp
// Type-anchored (recommended) - uses launch profile settings
var api = builder.AddProject<Projects.Api>("api");

// Path-based - useful for external projects
var worker = builder.AddProject("worker", "../Worker/Worker.csproj");

// Skip launch profile (use Kestrel defaults)
var backend = builder.AddProject<Projects.Backend>("backend")
    .WithoutLaunchProfile();
```

## Container Resources

Add OCI containers with `AddContainer`. Configure endpoints to make them reachable by other resources:

```csharp
var redis = builder.AddContainer("cache", "redis:7")
    .WithEndpoint(targetPort: 6379, scheme: "tcp", name: "tcp");

var mongo = builder.AddContainer("mongo", "mongo:7")
    .WithEndpoint(targetPort: 27017, scheme: "tcp", name: "mongo");
```

> Prefer the built-in Aspire hosting packages (`AddRedis()`, `AddPostgres()`, etc.) over raw `AddContainer()`
> when an official integration exists. Use `AddContainer()` for images without Aspire integrations.

## Service References

Wire service-to-service communication with `WithReference()`. This injects connection information as environment
variables in the consuming project:

```csharp
var db = builder.AddPostgres("pg").AddDatabase("catalog");
var cache = builder.AddRedis("cache");

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(db)
    .WithReference(cache);

var frontend = builder.AddProject<Projects.Frontend>("frontend")
    .WithReference(api);
```

Connection strings appear as `ConnectionStrings__{ResourceName}` environment variables. Service endpoints appear
as URL-formatted values consumed through service discovery.

## Dependency Ordering

Control startup order with `WaitFor()` and `WaitForCompletion()`:

```csharp
var db = builder.AddPostgres("pg").AddDatabase("catalog");
var migration = builder.AddProject<Projects.DbMigrator>("migrator")
    .WithReference(db)
    .WaitFor(db);

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WaitForCompletion(migration);
```

| Method                   | Waits until              | Use case                          |
|--------------------------|--------------------------|-----------------------------------|
| `WaitFor(resource)`      | Resource reports started | Databases, caches, APIs           |
| `WaitForCompletion(res)` | Resource exits healthy   | Migrations, seed jobs, init tasks |

Resources with health checks must report healthy before dependents start. Add health checks to containers using
the built-in mechanisms or custom health check implementations.

## Parameters and Secrets

Declare parameters for values that vary per environment. Mark secrets so they are never logged:

```csharp
var dbPassword = builder.AddParameter("db-password", secret: true);
var featureFlag = builder.AddParameter("enable-feature-x");

var db = builder.AddPostgres("pg", password: dbPassword);

var api = builder.AddProject<Projects.Api>("api")
    .WithEnvironment("FEATURE_X", featureFlag);
```

Supply parameter values in `appsettings.json` under the `Parameters` section:

```json
{
  "Parameters": {
    "db-password": "dev-only-password",
    "enable-feature-x": "true"
  }
}
```

## Data Persistence

Attach named volumes to containers so data survives restarts:

```csharp
// Convenience method - auto-names the volume
var db = builder.AddPostgres("pg")
    .WithDataVolume();

// Explicit volume name and mount path
var grafana = builder.AddContainer("grafana", "grafana/grafana:latest")
    .WithVolume("grafana-data", "/var/lib/grafana");
```

Use `WithDataVolume()` on Aspire hosting integrations that support it. Use `WithVolume("name", "/path")` for raw
containers or custom mount paths.

## Endpoint Configuration

Configure how resources expose network endpoints:

```csharp
var api = builder.AddProject<Projects.Api>("api")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithHttpsEndpoint(port: 5001, name: "secure");

// Container with specific port mapping
var ui = builder.AddContainer("ui", "my-ui:latest")
    .WithHttpEndpoint(port: 3000, targetPort: 80, name: "http");

// Expose all HTTP endpoints externally (publish manifests)
var gateway = builder.AddProject<Projects.Gateway>("gateway")
    .WithExternalHttpEndpoints();
```

| Parameter    | Purpose                                             |
|--------------|-----------------------------------------------------|
| `port`       | Host-side port (what callers connect to)            |
| `targetPort` | Container-side port (what the process listens on)   |
| `name`       | Endpoint name for named service discovery           |
| `scheme`     | Protocol scheme (`http`, `https`, `tcp`)            |

When `port` is omitted, Aspire assigns a dynamic port. When `targetPort` is omitted, it defaults to `port`.

## Existing Infrastructure

Reference pre-existing resources that Aspire does not manage:

```csharp
var legacyDb = builder.AddConnectionString("legacy-db");

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(legacyDb);
```

Set the connection string in AppHost configuration:

```json
{
  "ConnectionStrings": {
    "legacy-db": "Server=prod-sql.corp.local;Database=Legacy;..."
  }
}
```

## Service Discovery

`WithReference()` enables automatic service discovery. Consuming projects resolve service URLs by resource name:

```csharp
// In the consuming project's configuration or HTTP client setup
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri("https+http://api");
});
```

The `https+http://` scheme prefix tells service discovery to prefer HTTPS, falling back to HTTP. Use
`http://servicename` for plain HTTP.

For named endpoints, use the `_endpointName.serviceName` pattern:

```
https+http://_public.api   // resolves the "public" endpoint of "api"
```

## Configuration Approaches

Two equal approaches for integrating Aspire resources with application code:

**Standard (Aspire client packages):** Install Aspire client NuGet packages in app projects. They read
Aspire-injected environment variables automatically.

```csharp
// In Api project - uses Aspire.Npgsql.EntityFrameworkCore.PostgreSQL
builder.AddNpgsqlDbContext<CatalogContext>("catalog");
```

**Explicit (environment variables only):** The AppHost translates resource outputs into environment variables.
App code reads them with standard .NET libraries, no Aspire client package required.

```csharp
// In AppHost
var db = builder.AddPostgres("pg").AddDatabase("catalog");
var api = builder.AddProject<Projects.Api>("api")
    .WithReference(db);

// In Api project - standard Npgsql, no Aspire package
var connStr = builder.Configuration.GetConnectionString("catalog");
builder.Services.AddDbContext<CatalogContext>(o => o.UseNpgsql(connStr));
```

Both approaches work. The explicit approach avoids Aspire client dependencies in app projects, keeping them
portable.

## Networking

Understand how containers and projects communicate within an Aspire application:

- **Container-to-container:** Resources on the same Docker network resolve each other by resource name as hostname.
  A Redis container named `"cache"` is reachable at `cache:6379` from other containers.
- **Project-to-container:** .NET projects use service discovery. `WithReference()` injects the container endpoint
  URL as an environment variable. The project resolves it with `https+http://cache` or reads
  `ConnectionStrings__cache`.
- **Container-to-project:** Containers reach .NET projects by resource name. The AppHost configures reverse proxy
  endpoints that route traffic to the project's Kestrel port.
- **External access:** Mark resources with `WithExternalHttpEndpoints()` to expose them outside the Aspire network.
  Without this, endpoints are internal-only in published manifests.

Port mapping follows Docker conventions: `port` is the host-facing port and `targetPort` is what the process
listens on inside the container. Omit `port` for dynamic allocation during development.

## Environment Variable Injection

`WithReference()` and `WithEnvironment()` inject values as environment variables in the target resource.
Understand the naming conventions:

| Source                          | Environment variable pattern                         |
|---------------------------------|------------------------------------------------------|
| Connection string resource      | `ConnectionStrings__{ResourceName}`                  |
| Service endpoint (HTTP)         | `services__{name}__http__0` (managed by discovery)   |
| Service endpoint (HTTPS)        | `services__{name}__https__0` (managed by discovery)  |
| Explicit environment variable   | Custom name via `WithEnvironment("KEY", value)`      |
| Parameter                       | Custom name via `WithEnvironment("KEY", parameter)`  |

Inspect injected variables in the Aspire dashboard under the resource details panel.

## AppHost Configuration

Key environment variables that control AppHost behavior:

| Variable                             | Purpose                                    | Default  |
|--------------------------------------|--------------------------------------------|----------|
| `ASPIRE_CONTAINER_RUNTIME`           | Container runtime (`docker` or `podman`)   | `docker` |
| `ASPIRE_ALLOW_UNSECURED_TRANSPORT`   | Allow HTTP in non-development environments | `false`  |
| `DOTNET_DASHBOARD_OTLP_ENDPOINT_URL` | Override the dashboard OTLP endpoint       | auto     |

## Resource Lifecycle

Understand the sequence Aspire follows when starting the application:

1. **Dependency resolution** - Build a DAG from `WaitFor()` / `WaitForCompletion()` declarations
2. **Container creation** - Pull images, create containers with configured volumes and endpoints
3. **File injection** - Mount any files declared with `WithContainerFiles()`
4. **Startup** - Start resources in dependency order; wait for health checks where configured
5. **Health monitoring** - Continuously monitor health check endpoints throughout the lifetime
6. **Shutdown** - Stop resources in reverse dependency order on SIGTERM or Ctrl+C

## Advanced Patterns

See [references/resource-patterns.md](references/resource-patterns.md) for certificate configuration, container
file injection, persistent containers, advanced endpoint patterns, and polyglot resource support.

## Learn More

| Topic                 | How to Find                                                                  |
|-----------------------|------------------------------------------------------------------------------|
| Aspire overview       | `microsoft_docs_search(query=".NET Aspire overview orchestration")`          |
| Built-in integrations | `microsoft_docs_search(query=".NET Aspire integrations hosting components")` |
| Service discovery     | `microsoft_docs_search(query=".NET Aspire service discovery configuration")` |
| Custom resource types | `microsoft_docs_search(query=".NET Aspire custom resource implementation")`  |
| Dashboard             | `microsoft_docs_search(query=".NET Aspire dashboard standalone")`            |
| Deployment            | `microsoft_docs_search(query=".NET Aspire deploy Azure Container Apps")`     |
