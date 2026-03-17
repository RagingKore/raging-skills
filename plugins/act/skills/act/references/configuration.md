# Act Configuration

## `.actrc` file

The `.actrc` file stores default flags so they do not need to be repeated on every invocation. Act reads `.actrc`
from multiple locations in this precedence order (later wins):

1. `/etc/actrc` (system-wide)
2. `$XDG_CONFIG_HOME/act/actrc` or `~/.config/act/actrc` (user-level, XDG)
3. `~/.actrc` (user-level, legacy)
4. `./.actrc` (project-level)
5. CLI arguments (highest precedence)

Each line in `.actrc` is a flag, exactly as it would appear on the command line.

### Example project `.actrc`

```text
-P ubuntu-latest=catthehacker/ubuntu:act-latest
-P ubuntu-22.04=catthehacker/ubuntu:act-22.04
--container-architecture linux/amd64
--secret-file .secrets
--var-file .vars
--action-offline-mode
```

Commit the project `.actrc` to version control so the team uses consistent settings. Do not include secrets in
`.actrc`; use `--secret-file` pointing to a gitignored file instead.

## Secrets file (`.secrets`)

Dotenv-format file containing workflow secrets. Referenced via `--secret-file .secrets` in `.actrc` or on the
command line.

```sh
# GitHub API token (use gh CLI for convenience)
GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

# Application secrets
DATABASE_URL=postgres://user:pass@localhost:5432/mydb
AWS_ACCESS_KEY_ID=AKIAIOSFODNN7EXAMPLE
AWS_SECRET_ACCESS_KEY=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY

# Multiline values use quotes
SSH_PRIVATE_KEY="-----BEGIN RSA PRIVATE KEY-----
MIIEpAIBAAKCAQEA...
-----END RSA PRIVATE KEY-----"
```

Supported syntax:

- `KEY=value` (plain)
- `KEY="value with spaces"` (quoted)
- `export KEY=value` (shell export format)
- Lines starting with `#` are comments
- Empty lines are ignored

Add `.secrets` to `.gitignore`.

## Variables file (`.vars`)

Same dotenv format, for repository variables (not secrets). Referenced via `--var-file .vars`.

```sh
NODE_ENV=development
DEPLOY_ENV=staging
LOG_LEVEL=debug
```

## Inputs file

For `workflow_dispatch` inputs. Same dotenv format. Referenced via `--input-file inputs.env`.

```sh
environment=staging
version=1.2.3
dry_run=true
```

## Environment file (`.env`)

Additional environment variables loaded via `--env-file .env`. These are set in the runner environment but are
not secrets (they appear in logs).

```sh
CI=true
TERM=xterm-256color
```

## Event payload files

JSON files that simulate GitHub webhook payloads. Referenced via `-e event.json`.

### Push event

```json
{
  "ref": "refs/heads/main",
  "before": "0000000000000000000000000000000000000000",
  "after": "abc1234def5678"
}
```

### Tagged push

```json
{
  "ref": "refs/tags/v1.0.0"
}
```

### Pull request

```json
{
  "action": "opened",
  "number": 42,
  "pull_request": {
    "head": {
      "ref": "feature-branch",
      "sha": "abc1234"
    },
    "base": {
      "ref": "main",
      "sha": "def5678"
    },
    "title": "Add new feature",
    "draft": false
  }
}
```

### Workflow dispatch with inputs

```json
{
  "inputs": {
    "environment": "staging",
    "version": "1.2.3"
  }
}
```

### Issue comment (for `/command`-style triggers)

```json
{
  "action": "created",
  "comment": {
    "body": "/deploy staging"
  },
  "issue": {
    "number": 42,
    "pull_request": {
      "url": "https://api.github.com/repos/owner/repo/pulls/42"
    }
  }
}
```

## Special environment variables

### Set automatically by act

| Variable | Value  | Purpose                                     |
|----------|--------|---------------------------------------------|
| `ACT`    | `true` | Indicates the workflow is running under act |

Use `if: ${{ !env.ACT }}` in workflow steps to skip steps that only work on GitHub.

### Common variables to provide

| Variable             | How to provide                       | Purpose                                    |
|----------------------|--------------------------------------|--------------------------------------------|
| `GITHUB_TOKEN`       | `-s GITHUB_TOKEN="$(gh auth token)"` | GitHub API access for actions that need it |
| `RUNNER_DEBUG`       | `--env RUNNER_DEBUG=1`               | Enable runner diagnostic logging           |
| `ACTIONS_STEP_DEBUG` | `--env ACTIONS_STEP_DEBUG=true`      | Enable step debug logging                  |

## Docker image selection

### Default image tiers

When act runs for the first time, it prompts for a default image size:

| Tier   | Image                                | Disk    | Preinstalled tools                      |
|--------|--------------------------------------|---------|-----------------------------------------|
| Micro  | `node:16-buster-slim`                | ~200 MB | Node.js only                            |
| Medium | `catthehacker/ubuntu:act-latest`     | ~500 MB | Common tools (git, curl, jq, etc.)      |
| Large  | `catthehacker/ubuntu:full-latest`    | ~12 GB  | Closest to GitHub-hosted runner toolset |

### Platform-specific images

```text
# Ubuntu versions
-P ubuntu-latest=catthehacker/ubuntu:act-latest
-P ubuntu-24.04=catthehacker/ubuntu:act-24.04
-P ubuntu-22.04=catthehacker/ubuntu:act-22.04
-P ubuntu-20.04=catthehacker/ubuntu:act-20.04

# Full-featured variants
-P ubuntu-latest=catthehacker/ubuntu:full-latest
-P ubuntu-22.04=catthehacker/ubuntu:full-22.04

# Self-hosted (no Docker)
-P ubuntu-latest=-self-hosted
-P macos-latest=-self-hosted
-P windows-latest=-self-hosted
```

### Architecture override

For Apple Silicon (M1/M2/M3) Macs running x86 workflow images:

```text
--container-architecture linux/amd64
```

This is commonly needed and should be in the project `.actrc` for teams with mixed architectures.
