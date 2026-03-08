# dotnet

Comprehensive .NET development skills for C# 14, logging, configuration, dependency injection,
testing, source generators, and observability.

## Overview

The dotnet plugin bundles eight specialized auto-loaded skills covering the most common areas of
.NET development. Each skill activates based on what you are working on, whether writing C#
classes, adding structured logging, configuring dependency injection, localizing apps, writing
tests, or implementing telemetry. Skills target .NET 6+ with emphasis on .NET 9/10 and C# 14
best practices.

## Skills

### Auto-Loaded

**csharp**

Activates when writing C# classes, interfaces, tests, or asking about C# 14 features. Generates
production-ready code with modern language features, .NET 10 performance patterns, vertical
alignment, and comprehensive XML documentation.

**logging**

Activates when adding logging, using `[LoggerMessage]`, configuring `ILogger`, or discussing
structured logs. Provides zero-allocation, high-performance logging patterns with proper event
IDs, log levels, scoping, exception handling, and data redaction.

**configuration**

Activates when binding configuration, setting up `appsettings.json`, working with `IOptions`,
or validating options. Covers all configuration sources, the options pattern, validation
strategies, and custom providers.

**dependency-injection**

Activates when registering services, configuring lifetimes, working with keyed services, or
troubleshooting DI patterns. Guidance on transient, scoped, singleton services, factory
registrations, decorator pattern, scope management, and captive dependency prevention.

**resx**

Activates when localizing, internationalizing, translating apps, or working with `.resx`
resource files. Covers `ResourceManager`, `IStringLocalizer`, satellite assemblies, culture
fallback, and multi-language application setup.

**tunit**

Activates when writing tests, fixing failing tests, setting up test infrastructure, or
improving test coverage. Fluent async assertions, dynamic test generation, property-based
testing, Docker container testing, and database testing with EF Core.

**source-generators**

Activates when writing Roslyn source generators, packaging generators in NuGet, enabling
AOT/trimming compatibility, or replacing reflection with compile-time generation. Covers
incremental pipeline design, debugging, and testing strategies.

**telemetry**

Activates when implementing observability, adding tracing, creating metrics, or setting up
OpenTelemetry. Complete .NET observability stack covering distributed tracing, metrics,
EventSource, high-performance instrumentation, and integration with Prometheus, Grafana,
Jaeger, and Application Insights.

