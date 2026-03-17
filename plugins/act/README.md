# Act Plugin

Run [GitHub Actions](https://docs.github.com/en/actions) workflows locally with
[nektos/act](https://github.com/nektos/act). Test CI/CD pipelines, debug workflow failures, simulate events,
and validate configuration before pushing to GitHub.

## Skills

| Skill   | Purpose                                                                          |
|---------|----------------------------------------------------------------------------------|
| **act** | CLI usage, configuration, event simulation, runner selection, and troubleshooting |

## Prerequisites

- [act](https://nektosact.com/installation/index.html) installed (`brew install act` on macOS)
- [Docker](https://docs.docker.com/get-docker/) running (unless using `-self-hosted` mode)

## Quick Start

Ask Claude Code about any act or GitHub Actions local testing topic:

- "Run my GitHub Actions workflow locally"
- "Test only the build job from my CI pipeline"
- "Set up .actrc for this project"
- "Debug why my workflow fails in act but passes on GitHub"
- "Run my workflow with secrets without Docker"
- "Simulate a pull_request event locally"
