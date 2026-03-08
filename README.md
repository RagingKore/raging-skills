# Raging Skills Marketplace

A curated collection of Claude Code plugins for .NET development, architecture design,
event sourcing, diagrams, and productivity.

## Getting Started

### Add the Marketplace to Claude Code

1. Open Claude Code
2. Run:
    ```sh
    /plugin marketplace add https://github.com/ragingkore/raging-skills
    ```

3. Install any plugin:
    ```sh
    /plugin install <plugin-name>@raging-skills
    ```

4. Restart Claude Code to load changes

## Available Plugins

### .NET Development

- [dotnet](plugins/dotnet) - C# 14, logging, configuration, DI, source generators, and telemetry.
- [dotnet-scripts](plugins/dotnet-scripts) - Write and run single-file C# programs and distributable CLI tools with .NET 10+.
- [bullseye](plugins/bullseye) – Build automation with Bullseye target dependency graphs.
- [incrementalist](plugins/incrementalist) - Git-based incremental builds and testing for .NET monorepos in GitHub Actions.

### Architecture & Event Sourcing

- [domain-driven-design](plugins/domain-driven-design) - Strategic and tactical DDD patterns: aggregates, entities, value objects, event sourcing, CQRS.
- [dcb](plugins/dcb) – Dynamic Consistency Boundary for event-driven systems with KurrentDB/Axon Server.
- [kurrentdb](plugins/kurrentdb) – KurrentDB event-native database: streams, projections, subscriptions, and .NET client.

### Diagrams & Visualization

- [mermaid](plugins/mermaid) – Write correct Mermaid diagram syntax for all 23+ diagram types with expert-level precision.
- [beautiful-mermaid](plugins/beautiful-mermaid) - Render Mermaid diagrams as ASCII art or themed SVG files via bun.
- [excalidraw](plugins/excalidraw) – Generate architecture diagrams as .excalidraw files from codebase analysis.

### Protobuf & gRPC

- [buf](plugins/buf) – Buf CLI and Protobuf style guide for linting, formatting, and code generation.
- [proto-style](plugins/proto-style) - Protocol Buffer style conventions for edition 2023, based on Google AIPs.

### Web

- [astro](plugins/astro) – Build and deploy static websites with Astro, Content Collections, and islands architecture.
- [crawl4ai](plugins/crawl4ai) - Web crawling and data extraction toolkit with optimized extraction patterns.

### Conventions & Tooling

- [conventional-commits](plugins/conventional-commits) - Conventional Commits v1.0.0 specification with semantic versioning integration.
- [keep-a-changelog](plugins/keep-a-changelog) - Keep a Changelog 1.1.0 format guide for creating and maintaining CHANGELOG.md files.
- [markdown-style](plugins/markdown-style) - Markdown writing conventions for prose, structure, links, lists, code blocks, and tables.

### Agent Workflow

- [claude-output-style](plugins/claude-output-style) - Create and review custom output styles for Claude Code.
- [project-setup](plugins/project-setup) - Generate or update .project/ documentation structure for project onboarding.

## For Plugin Developers

Refer to [CLAUDE.md](CLAUDE.md) for marketplace structure, plugin organization, and contribution guidelines.

The official [Claude Code plugin documentation](https://docs.anthropic.com/en/docs/claude-code/plugins)
provides comprehensive guidance for plugin development.

## License

MIT License. See LICENSE file for details.

## Maintainer

[Sérgio Silveira](https://github.com/ragingkore)
