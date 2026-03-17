# Aspire Resource Patterns

Advanced resource configuration patterns for .NET Aspire AppHost orchestration.

## Table of Contents

- [Certificate Configuration](#certificate-configuration)
- [Container File Injection](#container-file-injection)
- [Persistent Containers](#persistent-containers)
- [Advanced Endpoint Patterns](#advanced-endpoint-patterns)
- [Polyglot Resource Support](#polyglot-resource-support)

---

## Certificate Configuration

Configure HTTPS certificates for container resources that require TLS.

### Developer Certificate

Use the ASP.NET Core developer certificate for local development:

```csharp
var api = builder.AddProject<Projects.Api>("api")
    .WithHttpsDeveloperCertificate("HTTPS_CERT_PATH", "HTTPS_CERT_PASSWORD");
```

This exports the dev certificate as a PFX file, mounts it into the container, and sets the specified environment
variables to the path and password. The consuming project reads these to configure Kestrel.

### Custom Certificate

Provide a specific certificate file for staging or production-like environments:

```csharp
var certPassword = builder.AddParameter("cert-password", secret: true);

var api = builder.AddContainer("api", "my-api:latest")
    .WithHttpsCertificate("HTTPS_CERT_PATH", "HTTPS_CERT_PASSWORD",
        certFilePath: "/certs/api.pfx",
        password: certPassword);
```

### Certificate Trust Scopes

Control which resources trust the developer certificate:

```csharp
var frontend = builder.AddNpmApp("frontend", "../frontend")
    .WithHttpsDeveloperCertificate("NODE_EXTRA_CA_CERTS",
        trustScope: CertificateTrustScope.AppHost);
```

| Trust Scope                       | Behavior                                        |
|-----------------------------------|-------------------------------------------------|
| `CertificateTrustScope.None`      | Export only, do not configure trust             |
| `CertificateTrustScope.AppHost`   | Trust within the Aspire AppHost process         |
| `CertificateTrustScope.Container` | Trust inside the container OS certificate store |

---

## Container File Injection

Mount files and directories into containers at startup. Use this for configuration files, seed data, or
initialization scripts.

### Single File

```csharp
var postgres = builder.AddPostgres("pg")
    .WithContainerFiles("/docker-entrypoint-initdb.d",
        new ContainerFile("init.sql", """
            CREATE TABLE IF NOT EXISTS catalog (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL
            );
            """));
```

### Multiple Files and Directories

```csharp
var nginx = builder.AddContainer("proxy", "nginx:latest")
    .WithContainerFiles("/etc/nginx",
        new ContainerDirectory("conf.d",
            new ContainerFile("default.conf", File.ReadAllText("nginx/default.conf")),
            new ContainerFile("upstream.conf", File.ReadAllText("nginx/upstream.conf"))),
        new ContainerFile("nginx.conf", File.ReadAllText("nginx/nginx.conf")));
```

### File Permissions

Set POSIX permissions on injected files:

```csharp
var app = builder.AddContainer("app", "my-app:latest")
    .WithContainerFiles("/app/config",
        new ContainerFile("settings.json", configJson) { Permissions = "644" },
        new ContainerFile("entrypoint.sh", scriptContent) { Permissions = "755" });
```

### Publish-Only Files

Inject files only when publishing (not during local development):

```csharp
var app = builder.AddContainer("app", "my-app:latest")
    .PublishWithContainerFiles("/app/config",
        new ContainerFile("production.json", prodConfig));
```

This is useful for production configuration that should not interfere with local development settings.

---

## Persistent Containers

By default, Aspire destroys and recreates containers on each run. Mark containers as persistent to preserve data
between development sessions:

```csharp
var db = builder.AddPostgres("pg")
    .WithDataVolume()
    .AsContainerPreservesDataBetweenRuns();
```

When `AsContainerPreservesDataBetweenRuns()` is applied:

- The container is **not** removed on AppHost shutdown
- On next startup, Aspire reuses the existing container if it matches (same image, same config)
- Data in named volumes persists regardless, but this avoids container recreation overhead
- Changed configuration (new env vars, different image tag) forces a container replacement

Combine with `WithDataVolume()` or `WithVolume()` for full data persistence:

```csharp
var mongo = builder.AddContainer("mongo", "mongo:7")
    .WithVolume("mongo-data", "/data/db")
    .WithEndpoint(targetPort: 27017, scheme: "tcp", name: "mongo")
    .AsContainerPreservesDataBetweenRuns();
```

---

## Advanced Endpoint Patterns

### Multiple Named Endpoints

Expose multiple endpoints for different consumers:

```csharp
var api = builder.AddProject<Projects.Api>("api")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithHttpEndpoint(port: 5001, name: "internal")
    .WithHttpsEndpoint(port: 5002, name: "admin");
```

Consumers reference specific endpoints:

```csharp
// In consuming project - resolves the "internal" endpoint
builder.Services.AddHttpClient("api-internal", client =>
{
    client.BaseAddress = new Uri("http://_internal.api");
});
```

### Proxy-Less TCP Endpoints

For non-HTTP protocols, use raw TCP endpoints:

```csharp
var mqtt = builder.AddContainer("mqtt", "eclipse-mosquitto:2")
    .WithEndpoint(port: 1883, targetPort: 1883, scheme: "tcp", name: "mqtt");
```

### Container-to-Container Networking

Containers within the same Aspire application share a Docker network. Reference other containers by resource
name:

```csharp
var redis = builder.AddRedis("cache");

// The Nginx config can reference "cache" by hostname
var proxy = builder.AddContainer("proxy", "nginx:latest")
    .WithReference(redis)
    .WithContainerFiles("/etc/nginx/conf.d",
        new ContainerFile("upstream.conf", """
            upstream cache_backend {
                server cache:6379;
            }
            """));
```

### Callback-Based Endpoint Configuration

Use `WithEndpoint` overloads for dynamic configuration:

```csharp
var api = builder.AddProject<Projects.Api>("api")
    .WithEndpoint("https", endpoint =>
    {
        endpoint.Port = 5001;
        endpoint.IsProxied = false;
    });
```

---

## Polyglot Resource Support

Aspire orchestrates non-.NET applications alongside .NET projects.

### Node.js / npm

```csharp
var api = builder.AddProject<Projects.Api>("api");

var frontend = builder.AddNpmApp("frontend", "../frontend", "dev")
    .WithReference(api)
    .WithHttpEndpoint(env: "PORT", targetPort: 3000)
    .WithExternalHttpEndpoints();
```

The third parameter is the npm script name (`npm run dev`). The `PORT` environment variable tells the Node.js
app which port to listen on.

### Python

```csharp
var pythonService = builder.AddPythonApp("ml-service", "../ml-service", "main.py")
    .WithHttpEndpoint(targetPort: 8000, env: "PORT")
    .WithReference(db);
```

### Vite

```csharp
var frontend = builder.AddViteApp("spa", "../spa")
    .WithReference(api)
    .WithHttpEndpoint(targetPort: 5173)
    .WithExternalHttpEndpoints();
```

### Environment Variable Injection

All polyglot resources receive Aspire-injected environment variables for referenced services. The application
code reads these variables using its native configuration mechanism (e.g., `process.env` in Node.js,
`os.environ` in Python).

### Common Polyglot Patterns

| Language   | Method                     | Script / Entry Point   | Port Env Var |
|------------|----------------------------|------------------------|--------------|
| Node.js    | `AddNpmApp("n", path, s)`  | npm script name        | `PORT`       |
| Python     | `AddPythonApp("n", p, e)`  | Python file path       | `PORT`       |
| Vite       | `AddViteApp("n", path)`    | auto (`vite dev`)      | auto         |
| Go / Rust  | `AddExecutable("n", path)` | Binary path            | custom       |

### Executable Resources

For compiled binaries (Go, Rust, etc.):

```csharp
var goService = builder.AddExecutable("go-api", "../go-api/bin/server")
    .WithHttpEndpoint(targetPort: 8080, env: "HTTP_PORT")
    .WithReference(db);
```
