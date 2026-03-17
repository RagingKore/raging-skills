---
name: aspire-deployment
description: |
  .NET Aspire deployment expert for publishing and deploying distributed applications to Docker Compose,
  Kubernetes, Azure Container Apps, and Azure App Service. Covers the aspire publish command for
  generating parameterized deployment artifacts, aspire deploy for executing deployments, compute
  environments (AddDockerComposeEnvironment, AddKubernetesEnvironment), hybrid deployments where
  different services target different platforms via WithComputeEnvironment, container image configuration,
  registry settings, parameter placeholder resolution, secrets management in published artifacts,
  CI/CD pipeline integration with aspire publish, and the deprecated aspire publish --publisher manifest
  output. Use when deploying an Aspire app, running aspire publish, generating a Docker Compose file
  from Aspire, publishing to Azure Container Apps, deploying to Kubernetes, calling
  PublishAsAzureContainerApp or PublishAsKubernetes, configuring container images, setting up CI/CD
  for Aspire, managing deployment parameters, creating hybrid deployments across platforms,
  or debugging aspire deploy issues.
---

# .NET Aspire Deployment

Publishing and deploying Aspire distributed applications to Docker Compose, Kubernetes, Azure Container Apps,
and Azure App Service targeting Aspire 13.x on .NET 10.

## Quick Decision Matrix

| Need                              | Command / API                                      | Notes                                         |
|-----------------------------------|----------------------------------------------------|-----------------------------------------------|
| Generate deployment artifacts     | `aspire publish -o artifacts/`                     | Parameterized files, no deployment            |
| Deploy directly                   | `aspire deploy`                                    | Resolves parameters and deploys               |
| Target Docker Compose             | `AddDockerComposeEnvironment("docker")`            | Generates docker-compose.yml                  |
| Target Kubernetes                 | `AddKubernetesEnvironment("k8s")`                  | Generates Kubernetes manifests                |
| Target Azure Container Apps       | `PublishAsAzureContainerApp()`                     | Azure-specific resource configuration         |
| Target Azure App Service          | Publish to App Service                             | Web app hosting                               |
| Hybrid platforms                  | `.WithComputeEnvironment(env)`                     | Per-resource platform targeting               |
| Legacy manifest (deprecated)      | `aspire publish --publisher manifest -o diag/`     | JSON manifest, prefer compute environments    |

## Publishing Overview

Aspire separates deployment into two phases: **publish** generates parameterized artifacts, **deploy** resolves
parameters and executes the deployment.

```bash
# Generate artifacts without deploying
aspire publish -o artifacts/

# Deploy directly (resolves parameters interactively)
aspire deploy
```

The `aspire publish` command produces intermediate deployment files with parameter placeholders like
`${PG_PASSWORD}`. These placeholders are resolved at deploy time or by CI/CD pipelines. This separation of
concerns keeps structure (what to deploy) separate from values (environment-specific secrets and configuration).

> The legacy `aspire publish --publisher manifest -o diagnostics/` command is deprecated. Use compute
> environments instead.

## Compute Environments

Register compute environments in the AppHost to control which publishing target each resource uses:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Register compute environments
var docker = builder.AddDockerComposeEnvironment("docker");
var k8s = builder.AddKubernetesEnvironment("k8s");

var db = builder.AddPostgres("pg")
    .WithDataVolume();

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(db);

builder.Build().Run();
```

When a single environment is registered, all resources target it by default. When multiple environments exist,
use `WithComputeEnvironment()` to assign resources explicitly.

## Docker Compose

Register a Docker Compose environment and publish to generate a `docker-compose.yml`:

```csharp
var docker = builder.AddDockerComposeEnvironment("docker");
```

Publish and run:

```bash
# Generate Docker Compose artifacts
aspire publish -o artifacts/

# Run with Docker Compose
docker compose -f artifacts/docker-compose.yml up --build
```

The generated `docker-compose.yml` includes service definitions, volume mounts, port mappings, environment
variables, and dependency ordering derived from the AppHost resource model. Parameter placeholders in the
generated file must be resolved before running, either through a `.env` file or environment variable
substitution.

## Kubernetes

Register a Kubernetes environment to generate Kubernetes manifests:

```csharp
var k8s = builder.AddKubernetesEnvironment("k8s");
```

For per-resource Kubernetes targeting:

```csharp
var api = builder.AddProject<Projects.Api>("api")
    .PublishAsKubernetes();
```

Publishing generates Kubernetes manifests (Deployments, Services, ConfigMaps, Secrets) in the output directory:

```bash
aspire publish -o artifacts/

# Apply to cluster
kubectl apply -f artifacts/
```

The generated manifests include resource requests, liveness probes derived from health checks, service discovery
configuration, and secret references. Review and customize the manifests before applying to production clusters.

## Azure Container Apps

Target Azure Container Apps for serverless container hosting:

```csharp
var api = builder.AddProject<Projects.Api>("api")
    .PublishAsAzureContainerApp();
```

Publishing generates Bicep templates and parameter files for Azure deployment. The `aspire deploy` command
handles resource provisioning, image pushing, and environment configuration:

```bash
# Publish Bicep artifacts
aspire publish -o artifacts/

# Deploy to Azure (interactive parameter resolution)
aspire deploy
```

Azure Container Apps deployment provisions a Container Apps Environment, configures managed identity, sets up
ingress rules, and maps Aspire endpoints to Container Apps ingress configuration.

## Hybrid Deployments

Target different platforms for different services using `WithComputeEnvironment()`. This enables scenarios where
databases run in Docker Compose locally while application services deploy to Kubernetes:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var docker = builder.AddDockerComposeEnvironment("docker");
var k8s = builder.AddKubernetesEnvironment("k8s");

// Database stays in Docker Compose
var db = builder.AddPostgres("pg")
    .WithDataVolume()
    .WithComputeEnvironment(docker);

// API deploys to Kubernetes
var api = builder.AddProject<Projects.Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithComputeEnvironment(k8s);

// Worker also deploys to Kubernetes
var worker = builder.AddProject<Projects.Worker>("worker")
    .WithReference(db)
    .WaitFor(db)
    .WithComputeEnvironment(k8s);

builder.Build().Run();
```

Publishing a hybrid AppHost generates artifacts for each compute environment. Cross-environment references are
resolved through connection strings and service discovery configuration that bridges the environments.

## Parameter Handling

Published artifacts contain parameter placeholders that must be resolved at deploy time:

```yaml
# In generated docker-compose.yml
services:
  pg:
    environment:
      POSTGRES_PASSWORD: ${PG_PASSWORD}
```

Supply parameter values through:

- **Interactive prompts** - `aspire deploy` prompts for unresolved parameters
- **Environment variables** - Set matching env vars before running `aspire deploy`
- **Parameter files** - Provide a parameters file with `--parameters` flag
- **CI/CD secrets** - Inject from pipeline secret stores

For secrets, never commit parameter values to source control. Use pipeline secret variables, Azure Key Vault
references, or Kubernetes secrets:

```bash
# Supply parameters via environment variables in CI
export PG_PASSWORD="${{ secrets.PG_PASSWORD }}"
aspire deploy
```

## Container Images

Configure container image names and registries for published artifacts:

```csharp
var api = builder.AddProject<Projects.Api>("api")
    .PublishAsAzureContainerApp();
```

Override image settings in project files:

```xml
<PropertyGroup>
    <ContainerRegistry>myregistry.azurecr.io</ContainerRegistry>
    <ContainerRepository>myapp/api</ContainerRepository>
    <ContainerImageTag>$(Version)</ContainerImageTag>
</PropertyGroup>
```

For container resources, specify the image directly:

```csharp
var ui = builder.AddContainer("ui", "myregistry.azurecr.io/myapp/ui:latest");
```

Published artifacts reference these image coordinates. Ensure the deployment environment has pull access to the
configured registries.

## CI/CD Integration

Use `aspire publish` in CI/CD pipelines to generate deployment artifacts, then apply them with platform-specific
tooling:

```yaml
# GitHub Actions example
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install Aspire workload
        run: dotnet workload install aspire

      - name: Publish artifacts
        run: |
          cd src/AppHost
          aspire publish -o ${{ github.workspace }}/artifacts/

      - name: Deploy to Kubernetes
        run: kubectl apply -f ${{ github.workspace }}/artifacts/
        env:
          PG_PASSWORD: ${{ secrets.PG_PASSWORD }}
```

Store published artifacts as pipeline artifacts for audit trails and rollback capability. Version artifacts
alongside source code to maintain traceability.

## Integration Matrix

| Platform              | Publish | Deploy  | Notes                                  |
|-----------------------|---------|---------|----------------------------------------|
| Docker Compose        | Yes     | Preview | Local development, simple hosting      |
| Kubernetes            | Yes     | Preview | Production orchestration               |
| Azure Container Apps  | Yes     | Preview | Serverless containers on Azure         |
| Azure App Service     | Yes     | Preview | Traditional web app hosting            |

## Learn More

| Topic                          | How to Find                                                                                          |
|--------------------------------|------------------------------------------------------------------------------------------------------|
| Aspire deployment overview     | `microsoft_docs_search(query=".NET Aspire publish deploy overview")`                                 |
| Azure Container Apps deploy    | `microsoft_docs_search(query=".NET Aspire deploy Azure Container Apps")`                             |
| Kubernetes deployment          | `microsoft_docs_search(query=".NET Aspire Kubernetes deployment manifests")`                         |
| Docker Compose publishing      | `microsoft_docs_search(query=".NET Aspire Docker Compose publish")`                                  |
| CI/CD with Aspire              | `microsoft_docs_search(query=".NET Aspire CI/CD pipeline deployment")`                               |
| Container image configuration  | `microsoft_docs_search(query=".NET container image registry configuration")`                         |
