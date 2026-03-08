# Buf CLI Plugin

Comprehensive Buf CLI and Protobuf style guide plugin for Claude Code. Covers the full Buf toolchain — building, linting, formatting, code generation, breaking change detection, and `.proto` file best practices.

## Skills

| Skill                    | Purpose                                                                     |
|--------------------------|-----------------------------------------------------------------------------|
| **buf-cli**              | Core CLI reference — commands, flags, installation, inputs, editor/LSP      |
| **protobuf-style-guide** | Buf's official style guide — naming, file layout, packages, enums, services |
| **buf-linting**          | Lint rules and categories (MINIMAL, BASIC, STANDARD, COMMENTS, UNARY_RPC)   |
| **buf-configuration**    | `buf.yaml`, `buf.gen.yaml`, `buf.lock`, modules, workspaces, managed mode   |
| **buf-code-generation**  | `buf generate` with local/remote/protoc_builtin plugins and managed mode    |
| **buf-breaking-changes** | Breaking change detection — FILE, PACKAGE, WIRE_JSON, WIRE categories       |

## Agents

| Agent                      | Purpose                                                                   |
|----------------------------|---------------------------------------------------------------------------|
| **protobuf-reviewer**      | Reviews `.proto` files for style guide compliance and lint best practices |
| **buf-migration-helper**   | Migrates from protoc to Buf or upgrades between config versions           |
| **buf-workspace-designer** | Designs workspace layouts and module boundaries for Protobuf projects     |

## Prerequisites

- [Buf CLI](https://buf.build/docs/cli/installation/) installed (`brew install bufbuild/buf/buf`)

## Quick Start

Ask Claude Code about any Buf topic:

- "How do I set up buf for my project?"
- "Lint my proto files"
- "What's the correct naming convention for enum values?"
- "Check for breaking changes against main"
- "Generate Go code from my protos"
- "Help me migrate from protoc to buf"
