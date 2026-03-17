---
name: act
description: |
  Guides running and debugging GitHub Actions workflows locally with nektos/act for fast feedback and offline
  CI/CD validation. Use this skill whenever:
  - Running or debugging GitHub Actions workflows locally before pushing
  - Writing or editing GitHub Actions workflow YAML files (.github/workflows/)
  - Configuring act settings (.actrc, .secrets, .vars, event payloads)
  - Choosing Docker images or runner platforms for local workflow execution
  - Simulating GitHub events (push, pull_request, workflow_dispatch, schedule)
  - Troubleshooting workflows that pass on GitHub but fail locally (or vice versa)
  - Setting up CI/CD pipelines and wanting to validate them without pushing
  - Discussing GitHub Actions best practices, job selection, matrix builds, or secrets management
  - Encountering .actrc files, act CLI flags, or nektos/act references
  Even if the user does not mention act by name, use this skill when the task involves testing or validating
  GitHub Actions workflows locally, or authoring CI/CD pipelines with a local-first approach.
---

## Quick start

Verify act is installed and Docker is running:

```sh
act --version
docker info > /dev/null 2>&1 && echo "Docker is running"
```

Run the default push event against all workflows:

```sh
act
```

On first run, act prompts for a default image size (micro, medium, large). For most workflows, choose medium
(`catthehacker/ubuntu:act-latest`). This selection is saved to `~/.actrc`.

## Core usage patterns

### Event-based execution

Specify a GitHub event name to trigger matching workflows. The default event is `push`.

```sh
act push                    # run push-triggered workflows
act pull_request            # run pull_request-triggered workflows
act schedule                # run schedule-triggered workflows
act workflow_dispatch       # run manually-triggered workflows
```

### Targeting specific workflows and jobs

Narrow execution to a single workflow file, a single job, or both:

```sh
act -W .github/workflows/ci.yml          # run a single workflow file
act -j build                              # run only the job named "build"
act -j test -W .github/workflows/ci.yml   # combine both
act -l                                    # list all workflows and jobs (dry run)
act -l push                               # list workflows for a specific event
```

Start with `act -l` to discover available workflows and jobs before running anything.

### Providing secrets and variables

Never hardcode secrets on the command line. Use file-based or prompt-based approaches to keep secrets out of
shell history:

```sh
act -s MY_SECRET                          # prompt for value (secure, no echo)
act --secret-file .secrets                # load from dotenv file
act --var MY_VAR=value                    # set a repository variable
act --var-file .vars                      # load variables from file
act -s GITHUB_TOKEN="$(gh auth token)"    # GitHub API access via gh CLI
```

The `.secrets` and `.vars` files use dotenv format. Add them to `.gitignore`. For the full dotenv syntax and
all file types, read `references/configuration.md`.

### Inputs for workflow_dispatch

Provide inputs to manually-triggered workflows via flags or a file:

```sh
act workflow_dispatch --input name=value
act workflow_dispatch --input-file inputs.env
```

### Matrix filtering

Run a specific matrix combination instead of the full matrix. This is useful for testing a single OS/version
combination without waiting for the entire matrix:

```sh
act -j test --matrix os:ubuntu-latest --matrix node:18
```

Note that `--matrix` filters existing matrix values; it cannot add new combinations.

## Runner configuration

### Docker-based runners (default)

Choose a Docker image tier based on what tools the workflow needs. Override the default with the `-P` flag or
persist the choice in `.actrc`:

| Tier   | Image                                | Size   | When to use                               |
|--------|--------------------------------------|--------|-------------------------------------------|
| Micro  | `node:16-buster-slim`                | ~200MB | Node.js-only workflows                    |
| Medium | `catthehacker/ubuntu:act-latest`     | ~500MB | Most workflows (recommended default)      |
| Large  | `catthehacker/ubuntu:full-latest`    | ~12GB  | Workflows needing many preinstalled tools |

```sh
act -P ubuntu-latest=catthehacker/ubuntu:act-latest
act -P ubuntu-22.04=catthehacker/ubuntu:act-22.04
```

For Apple Silicon Macs running x86 images, set the architecture in `.actrc`:

```text
--container-architecture linux/amd64
```

### Self-hosted mode (no Docker)

Run directly on the host OS without containers. Use this when Docker is unavailable, when the workflow needs
host-level access, or when testing on macOS/Windows natively:

```sh
act -P ubuntu-latest=-self-hosted
act -P macos-latest=-self-hosted
```

The leading `-` in `-self-hosted` is required syntax. This mode uses whatever tools are installed on the host,
so ensure the host has all dependencies the workflow expects.

### Offline and cached mode

Avoid pulling images and actions on every run. This is especially useful in air-gapped environments or to
avoid Docker Hub rate limits:

```sh
act --pull=false                # skip pulling fresh images
act --action-offline-mode       # use only locally cached actions and images
```

Combine both in `.actrc` after the first successful run for the fastest iteration cycle.

## Event simulation

Simulate any GitHub event by providing a JSON payload. This is essential for testing workflows that depend on
event-specific data like branch names, tag references, or PR metadata:

```sh
act pull_request -e event.json
```

Example `event.json` for a pull request targeting main:

```json
{
  "pull_request": {
    "head": { "ref": "feature-branch" },
    "base": { "ref": "main" }
  }
}
```

For tagged pushes:

```json
{
  "ref": "refs/tags/v1.0.0"
}
```

For workflow_dispatch with inputs:

```json
{
  "inputs": {
    "environment": "staging",
    "version": "1.2.3"
  }
}
```

Read `references/configuration.md` for more event payload examples.

## Configuration overview

### `.actrc`

Persist frequently-used flags so the team uses consistent settings. Commit this file to version control. Act
reads `.actrc` from system, home, and project directories (project-level wins).

```text
-P ubuntu-latest=catthehacker/ubuntu:act-latest
--container-architecture linux/amd64
--secret-file .secrets
--action-offline-mode
```

### `.secrets` and `.vars`

Dotenv-format files for secrets and variables. Add both to `.gitignore`. Read `references/configuration.md`
for the full dotenv syntax, supported formats, and additional file types (`.env`, input files, event payloads).

## Artifacts

Enable artifact upload/download by specifying a local directory:

```sh
act --artifact-server-path /tmp/artifacts
```

Without this flag, `actions/upload-artifact` and `actions/download-artifact` silently succeed but store
nothing. Cross-run artifact downloads are not supported.

## Debugging workflow

Follow this progression when a workflow fails locally:

1. **List first**: Run `act -l` to confirm the workflow and job names are correct
2. **Dry run**: Run `act -n` to validate workflow parsing without executing anything
3. **Verbose mode**: Run `act -v` to see Docker commands and step-level output
4. **Extra verbose**: Run `act -v -v` for maximum detail including container internals
5. **Debug logging**: Add `--env ACTIONS_STEP_DEBUG=true` for GitHub Actions debug output
6. **Environment check**: Run `act --env-file .env` to inject additional env vars for diagnostics

When a step fails locally but passes on GitHub, the most common causes are missing tools in the Docker image,
missing secrets, and architecture mismatches on Apple Silicon. Read `references/compatibility.md` for
per-action notes and a full troubleshooting guide.

## Conditional skipping

Some workflow steps cannot run locally (deployment, OIDC tokens, GPU workloads). Skip them using the `ACT`
environment variable that act sets automatically:

```yaml
- name: Deploy to production
  if: ${{ !env.ACT }}
  run: ./deploy.sh
```

This approach keeps workflows valid on both GitHub and act. Place the `if` condition on individual steps
rather than entire jobs to maximize local test coverage.

## Best practices

- Commit `.actrc` to the repo so the team uses identical flags and runner images
- Use `--secret-file` pointing to a gitignored file; never pass `-s VALUE` directly, because the value
  appears in shell history
- Provide `GITHUB_TOKEN` via `gh auth token` for workflows that call the GitHub API; many composite actions
  use it implicitly even when the workflow YAML does not reference it
- Start every session with `act -l` to discover workflows and `act -n` to validate syntax
- Use `--action-offline-mode` after the first successful run; subsequent runs avoid network calls and are
  significantly faster
- Prefer the medium image tier unless specific tools are missing; switch to large only when needed because it
  is 24x the size
- Use `--bind` to mount the working directory instead of copying it; this speeds up large repos and makes file
  changes visible immediately

## Reference material

- **Act CLI reference**: Read `references/cli-reference.md`
- **Act configuration**: Read `references/configuration.md`
- **Action compatibility and troubleshooting**: Read `references/compatibility.md`

### External links

- [act GitHub repository](https://github.com/nektos/act)
- [act documentation](https://nektosact.com/)
- [act installation guide](https://nektosact.com/installation/index.html)
