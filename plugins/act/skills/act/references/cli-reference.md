# Act CLI Reference

## Command syntax

```text
act [event] [flags]
```

When no event is specified, `push` is the default.

## Events

| Event               | Description                      |
|---------------------|----------------------------------|
| `push`              | Push to a branch (default)       |
| `pull_request`      | Pull request opened/synchronized |
| `schedule`          | Scheduled (cron) workflows       |
| `workflow_dispatch` | Manually triggered workflows     |
| `release`           | Release created/published        |
| `issues`            | Issue events                     |
| `issue_comment`     | Comment on an issue or PR        |
| `workflow_call`     | Reusable workflow invocation     |
| (any valid event)   | Any GitHub webhook event name    |

## Core flags

### Workflow and job selection

| Flag              | Description                                |
|-------------------|--------------------------------------------|
| `-W, --workflows` | Path to workflow file or directory         |
| `-j, --job`       | Run a specific job by name                 |
| `-l, --list`      | List available workflows and jobs          |
| `-n, --dryrun`    | Validate workflow without executing        |
| `--detect-event`  | Use the event type from the default branch |

```sh
act -W .github/workflows/ci.yml
act -j build
act -l push
act -n
```

### Secrets and variables

| Flag            | Description                              |
|-----------------|------------------------------------------|
| `-s, --secret`  | Set a secret (prompts if no value given) |
| `--secret-file` | Load secrets from a dotenv file          |
| `--var`         | Set a repository variable                |
| `--var-file`    | Load variables from a dotenv file        |
| `--env`         | Set an environment variable              |
| `--env-file`    | Load env vars from a dotenv file         |
| `--input`       | Set an input for workflow_dispatch       |
| `--input-file`  | Load inputs from a dotenv file           |

```sh
act -s MY_SECRET
act -s MY_SECRET=value
act --secret-file .secrets
act --var MY_VAR=value
act --var-file .vars
act --env MY_ENV=value
act --env-file .env
act --input name=value
act --input-file inputs.env
```

### Runner and platform configuration

| Flag                         | Description                                |
|------------------------------|--------------------------------------------|
| `-P, --platform`             | Map a platform label to a Docker image     |
| `--container-architecture`   | Set container CPU architecture             |
| `--container-daemon-socket`  | Path to Docker daemon socket               |
| `--pull`                     | Pull Docker images before running          |
| `--action-offline-mode`      | Use only locally cached actions and images |

```sh
act -P ubuntu-latest=catthehacker/ubuntu:act-latest
act --container-architecture linux/amd64
act --container-daemon-socket /var/run/docker.sock
act --pull=false
act --action-offline-mode
```

### Event simulation

| Flag               | Description                            |
|--------------------|----------------------------------------|
| `-e, --eventpath`  | Path to event JSON payload             |
| `--matrix`         | Filter matrix to specific values       |

```sh
act -e event.json
act -j test --matrix os:ubuntu-latest
```

### Artifacts

| Flag                      | Description                           |
|---------------------------|---------------------------------------|
| `--artifact-server-path`  | Local directory for artifact storage  |
| `--artifact-server-addr`  | Address for the artifact server       |
| `--artifact-server-port`  | Port for the artifact server          |

```sh
act --artifact-server-path /tmp/artifacts
act --artifact-server-addr 0.0.0.0
act --artifact-server-port 34567
```

### Output and debugging

| Flag                    | Description                                    |
|-------------------------|------------------------------------------------|
| `-v, --verbose`         | Verbose output (use twice for extra verbosity) |
| `-g, --graph`           | Draw workflow dependency graph                 |
| `--json`                | Output in JSON format                          |
| `--no-cache-server`     | Disable the cache server                       |
| `--log-prefix-job-id`   | Use job ID as log prefix                       |

```sh
act -v
act -v -v
act -g
act -l --json
```

### Container options

| Flag                    | Description                                 |
|-------------------------|---------------------------------------------|
| `--container-cap-add`   | Add Linux capabilities to containers        |
| `--container-cap-drop`  | Drop Linux capabilities from containers     |
| `--container-options`   | Extra Docker options for job containers     |
| `--use-gitignore`       | Respect .gitignore when mounting workdir    |
| `--privileged`          | Run containers in privileged mode           |
| `--userns`              | User namespace for containers               |
| `--rm`                  | Remove containers after run                 |
| `--reuse`               | Reuse containers between runs               |
| `--bind`                | Bind mount workdir instead of copying       |

```sh
act --container-cap-add SYS_PTRACE
act --container-options "--memory=4g"
act --privileged
act --reuse
act --bind
```

### GitHub-specific overrides

| Flag                  | Description                          |
|-----------------------|--------------------------------------|
| `--defaultbranch`     | Override the default branch name     |
| `--remote-name`       | Override git remote name             |
| `--github-instance`   | GitHub instance URL (for GHES)       |

```sh
act --defaultbranch main
act --remote-name upstream
act --github-instance github.company.com
```

## Common command combinations

```sh
# List all available workflows and jobs
act -l

# Run a specific job from a specific workflow
act -j test -W .github/workflows/ci.yml

# Run with secrets from a file and verbose output
act --secret-file .secrets -v

# Run pull_request event with custom payload
act pull_request -e pull_request_event.json

# Run without Docker, using host tools
act -P ubuntu-latest=-self-hosted

# Run with specific matrix values
act -j test --matrix os:ubuntu-latest --matrix node:18

# Dry run to validate workflow syntax
act -n

# Run offline with cached images
act --pull=false --action-offline-mode

# Run with artifacts support
act --artifact-server-path ./artifacts

# Run workflow_dispatch with inputs
act workflow_dispatch --input deploy_env=staging --input version=1.2.3
```
