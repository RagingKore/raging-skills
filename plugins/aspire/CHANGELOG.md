# Changelog

and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-03-10

### Added

- Six auto-loaded skills covering the full Aspire development lifecycle:
  - `aspire`: Core AppHost orchestration, resource model, service references, parameters, and networking
  - `aspire-service-defaults`: ServiceDefaults project with OpenTelemetry, health checks, and resilience
  - `aspire-integrations`: Integration patterns and configuration approaches
  - `aspire-testing`: Integration testing with TUnit, replacing TestContainers
  - `aspire-deployment`: Publishing to Docker Compose, Kubernetes, and Azure Container Apps
  - `aspire-extensibility`: Eventing system, custom resources, and dashboard configuration
- One migration agent:
  - `aspire-migration-helper`: Converts Docker Compose files to Aspire AppHost
- Targets Aspire 13.x on .NET 10
