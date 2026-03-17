# aspire

Comprehensive .NET Aspire development skills for cloud-native application orchestration, testing,
deployment, and extensibility. Targets Aspire 13.x on .NET 10.

## Overview

The aspire plugin bundles six specialized auto-loaded skills and one migration agent covering the
full Aspire development lifecycle. Each skill activates based on what you are working on; whether
orchestrating distributed services, writing integration tests, deploying to Azure Container Apps,
or extending the resource model. Skills emphasize Aspire 13.x patterns with .NET 10.

## Skills

### Auto-Loaded

**aspire**

Activates when creating AppHost projects, adding resources, configuring service references, or
working with `DistributedApplication.CreateBuilder`. Covers `AddProject`, `AddContainer`,
`WithReference`, `WaitFor`, parameters, volumes, endpoints, service discovery, and resource
lifecycle.

**aspire-service-defaults**

Activates when setting up ServiceDefaults projects, configuring OpenTelemetry, adding health
checks, or setting up HTTP resilience. Provides the complete `AddServiceDefaults()` pattern with
telemetry, health probes, service discovery, and resilience configuration.

**aspire-integrations**

Activates when adding database, messaging, or caching integrations to Aspire applications.
Explains the hosting-plus-client package pattern and covers both standard Aspire client usage and
explicit environment variable configuration. Points to official docs for per-integration details.

**aspire-testing**

Activates when writing integration tests for Aspire applications or replacing TestContainers. Uses
TUnit as the testing framework with Shouldly assertions. Covers `DistributedApplicationTestingBuilder`,
test fixtures, resource access, health check waiting, and advanced patterns like database cleanup
with Respawn.

**aspire-deployment**

Activates when publishing or deploying Aspire applications. Covers `aspire publish`, `aspire deploy`,
Docker Compose, Kubernetes, Azure Container Apps, hybrid deployments, parameter handling, and CI/CD
integration.

**aspire-extensibility**

Activates when building custom resources, subscribing to lifecycle events, or configuring the Aspire
dashboard. Covers the eventing system, resource events, custom event publishing, event subscribers,
resource annotations, custom commands, and dashboard configuration.

### Agents

**aspire-migration-helper**

Triggers when migrating from Docker Compose to Aspire. Reads `docker-compose.yml` files and
generates equivalent AppHost `Program.cs` with proper resource mapping, dependency ordering,
volume configuration, and environment variable handling.

## Installation

```sh
claude plugins add ragingkore/raging-skills --plugin aspire
```

## Prerequisites

- .NET 10 SDK
- Aspire 13.x workload (`dotnet workload install aspire`)
- Docker or Podman (for container resources)
