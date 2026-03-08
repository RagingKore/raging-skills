# Incrementalist Plugin

Integrate [Incrementalist](https://github.com/petabridge/Incrementalist) into .NET monorepos for git-based incremental builds and testing in GitHub Actions CI/CD pipelines. Analyzes git diffs, maps changed files to affected projects via Roslyn dependency analysis, and executes dotnet commands against only those projects.

## Skills

| Skill               | Purpose                                                                              |
|---------------------|--------------------------------------------------------------------------------------|
| **incrementalist**  | Full setup workflow — installation, repo analysis, config, workflow authoring, verify |

## Prerequisites

- .NET SDK installed
- `Incrementalist.Cmd` dotnet tool (`dotnet tool install Incrementalist.Cmd`)

## Quick Start

Ask Claude Code about any Incrementalist topic:

- "Set up incremental builds for my .NET monorepo"
- "Optimize my GitHub Actions CI pipeline to only test what changed"
- "Create an incrementalist.json config for my solution"
- "Help me configure selective testing with Incrementalist"
- "Which projects are affected by my changes?"
