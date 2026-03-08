---
name: buf-cli
description: |
  Comprehensive Buf CLI reference for Protobuf API management. Use when running buf commands,
  installing Buf, setting up a workspace, using buf build, buf lint, buf format, buf generate,
  buf breaking, buf dep, buf export, buf convert, buf curl, buf push, or any buf subcommand.
  Also use when troubleshooting buf CLI errors, configuring buf inputs, or integrating buf into
  CI/CD pipelines.
---

# Buf CLI

The Buf CLI is the primary tool for modern, fast Protobuf API management. It replaces `protoc` with a simpler, faster workflow for building, linting, formatting, generating code, and detecting breaking changes.

## Installation

```bash
# macOS / Linux (Homebrew — recommended)
brew install bufbuild/buf/buf

# npm
npm install @bufbuild/buf
npx buf --version

# Windows (Scoop)
scoop install buf

# Windows (WinGet)
winget install bufbuild.buf

# Go
go install github.com/bufbuild/buf/cmd/buf@latest

# Docker
docker run --volume "$(pwd):/workspace" --workdir /workspace bufbuild/buf lint

# Binary (Linux/macOS)
BIN="/usr/local/bin" && \
VERSION="$(curl -s https://api.github.com/repos/bufbuild/buf/releases/latest | grep tag_name | cut -d '"' -f4 | sed 's/^v//')" && \
curl -sSL "https://github.com/bufbuild/buf/releases/download/v${VERSION}/buf-$(uname -s)-$(uname -m)" \
  -o "${BIN}/buf" && chmod +x "${BIN}/buf"
```

Homebrew also installs `protoc-gen-buf-breaking`, `protoc-gen-buf-lint`, and shell completions.

## Core Commands

| Command                        | Purpose                                           |
|--------------------------------|---------------------------------------------------|
| `buf build`                    | Compile `.proto` files and verify they're valid   |
| `buf lint`                     | Lint Protobuf files against best practices        |
| `buf format`                   | Format `.proto` files for consistency             |
| `buf breaking`                 | Detect breaking changes against a reference       |
| `buf generate`                 | Generate code stubs using protoc plugins          |
| `buf dep update`               | Update dependencies in `buf.lock`                 |
| `buf push`                     | Push modules to the Buf Schema Registry           |
| `buf export`                   | Export module content to a directory              |
| `buf convert`                  | Convert messages between binary and JSON          |
| `buf curl`                     | Invoke RPC endpoints (like curl for gRPC/Connect) |
| `buf config init`              | Create a default `buf.yaml` configuration         |
| `buf config ls-lint-rules`     | List all available lint rules                     |
| `buf config ls-breaking-rules` | List all available breaking rules                 |

## Quick Start

```bash
# Initialize a workspace
buf config init

# Build and verify proto files compile
buf build

# Lint with default STANDARD rules
buf lint

# Format in place
buf format -w

# Check for breaking changes against main branch
buf breaking --against '.git#branch=main'

# Generate code
buf generate

# Update dependencies
buf dep update
```

## Configuration Files

Buf uses three YAML config files at the workspace root:

| File           | Purpose                                               |
|----------------|-------------------------------------------------------|
| `buf.yaml`     | Workspace/module definitions, lint and breaking rules |
| `buf.gen.yaml` | Code generation plugin configuration                  |
| `buf.lock`     | Dependency version pinning (auto-managed)             |

Minimal `buf.yaml`:

```yaml
version: v2
modules:
  - path: proto
lint:
  use:
    - STANDARD
breaking:
  use:
    - FILE
```

## Inputs

Most commands accept various input types beyond local directories:

```bash
# Local workspace (default — current directory)
buf lint

# Specific directory
buf lint proto/

# BSR module
buf lint buf.build/acme/petapis

# BSR module at specific version
buf lint buf.build/acme/petapis:v1.0.0

# Git repository
buf lint 'https://github.com/foo/bar.git'

# Git branch
buf lint '.git#branch=main'

# Git tag
buf lint '.git#tag=v1.0.0'

# Git tag with subdirectory
buf lint '.git#tag=v1.0.0,subdir=proto'

# Tarball
buf lint 'https://github.com/foo/bar/archive/main.tar.gz#strip_components=1'

# protoc output via stdin
protoc -I . --include_source_info $(find . -name '*.proto') -o /dev/stdout | buf lint -
```

## Common Flags

| Flag                  | Commands                 | Purpose                                    |
|-----------------------|--------------------------|--------------------------------------------|
| `--path`              | lint, breaking, generate | Limit to specific files/dirs               |
| `--exclude-path`      | generate                 | Exclude specific files/dirs                |
| `--config`            | all                      | Override config (file path or inline JSON) |
| `--error-format=json` | lint, breaking           | Output errors as JSON                      |
| `--exit-code`         | format                   | Non-zero exit if formatting needed         |
| `-w` / `--write`      | format                   | Rewrite files in place                     |
| `-d` / `--diff`       | format                   | Show diff of formatting changes            |
| `--against`           | breaking                 | Reference to compare against               |
| `--template`          | generate                 | Alternative buf.gen.yaml path              |
| `--include-imports`   | generate                 | Include imported files                     |
| `--include-wkt`       | generate                 | Include Well-Known Types                   |
| `--type`              | generate                 | Limit to specific types                    |

## `buf format`

```bash
# Preview formatted output to stdout
buf format

# Format files in place
buf format -w

# Show diff
buf format -d

# Check if formatting is needed (CI)
buf format --exit-code

# Format and check in one command
buf format -w --exit-code

# Format a specific file to another location
buf format proto/simple/v1/simple.proto -o formatted/simple.proto

# Format a BSR module
buf format buf.build/acme/weather -o formatted
```

`buf format` enforces:
- Consistent 2-space indentation
- Sorted imports
- Package before imports in file layout
- No trailing whitespace
- Consistent spacing around field numbers

## `buf convert`

Convert Protobuf messages between binary and JSON formats:

```bash
# Binary to JSON
buf convert --type foo.v1.MyMessage --from binary_file.bin

# JSON to binary
buf convert --type foo.v1.MyMessage --from message.json --to binary
```

## `buf curl`

Test gRPC/Connect APIs directly:

```bash
# Call an RPC endpoint
buf curl --data '{"name": "world"}' \
  http://localhost:8080/grpc.health.v1.Health/Check
```

## Editor / LSP Integration

The Buf CLI includes a full Language Server Protocol (LSP) server via `buf lsp serve`. Features:

- Go to definition and references
- Auto-complete for messages, fields, enums, packages
- Hover information
- Format on save
- Organize imports, deprecation code actions
- Syntax highlighting and workspace symbols

### Editor Setup

| Editor       | Setup                                                                                |
|--------------|--------------------------------------------------------------------------------------|
| **VS Code**  | Install `bufbuild.vscode-buf` extension. Disable other proto plugins first           |
| **Neovim**   | Configure `buf` as LSP server in `init.lua` via `vim.lsp.config` or `nvim-lspconfig` |
| **Vim**      | Use `vim-lsp` + `vim-lsp-settings` plugins. Remove deprecated `bufbuild/vim-buf`     |
| **IntelliJ** | Install Buf plugin from JetBrains Marketplace                                        |
| **Other**    | Any editor supporting LSP can use `buf lsp serve` directly                           |

## CI/CD Integration

```bash
# GitHub Actions — lint and breaking checks
- uses: bufbuild/buf-action@v1
  with:
    token: ${{ secrets.BUF_TOKEN }}

# Generic CI — lint
buf lint --error-format=json

# Generic CI — breaking changes against main
buf breaking --against 'https://github.com/org/repo.git#branch=main'

# Generic CI — format check
buf format --exit-code
```

## Detailed Reference

For complete CLI command reference with all subcommands and flags, see [references/commands.md](references/commands.md).

For installation details across all platforms, see [references/installation.md](references/installation.md).
