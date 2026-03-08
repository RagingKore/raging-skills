---
name: buf-configuration
description: |
  Buf configuration file reference for buf.yaml, buf.gen.yaml, and buf.lock. Use when creating
  or editing buf.yaml, configuring modules and workspaces, setting up buf.gen.yaml for code
  generation, understanding buf.lock dependency pinning, configuring managed mode, setting up
  multi-module workspaces, or migrating from v1 to v2 configuration format. Also use when
  asking about Buf modules, workspaces, dependencies, or buf dep commands.
---

# Buf Configuration

Buf uses three YAML configuration files at the workspace root. All use `version: v2` format.

## buf.yaml — Workspace and Module Configuration

Defines the workspace, modules, lint rules, and breaking change rules.

### Minimal Configuration

```yaml
# Single module at repo root
version: v2
```

Equivalent to:

```yaml
version: v2
modules:
  - path: .
lint:
  use:
    - STANDARD
breaking:
  use:
    - FILE
```

### Single Module with Subdirectory

```yaml
version: v2
modules:
  - path: proto
    name: buf.build/acme/petapis
lint:
  use:
    - STANDARD
breaking:
  use:
    - FILE
deps:
  - buf.build/googleapis/googleapis
```

### Multi-Module Workspace

```yaml
version: v2
modules:
  - path: proto/api
    name: buf.build/acme/api
  - path: proto/common
    name: buf.build/acme/common
  - path: vendor
    lint:
      use:
        - MINIMAL          # Override: vendor only needs minimal
    breaking:
      use:
        - PACKAGE
deps:
  - buf.build/googleapis/googleapis
  - buf.build/grpc/grpc
lint:
  use:
    - STANDARD             # Default for all modules
breaking:
  use:
    - FILE                 # Default for all modules
```

### Module Fields

| Field      | Description                                                            |
|------------|------------------------------------------------------------------------|
| `path`     | Directory containing .proto files (relative to buf.yaml)               |
| `name`     | BSR module name (e.g., `buf.build/acme/petapis`)                       |
| `includes` | Subdirectories to include (when multiple modules share a path)         |
| `excludes` | Subdirectories to exclude                                              |
| `lint`     | Module-level lint config (completely overrides workspace defaults)     |
| `breaking` | Module-level breaking config (completely overrides workspace defaults) |

### Lint Configuration Options

```yaml
lint:
  use:                                     # Categories/rules to use
    - STANDARD
  except:                                  # Rules to exclude
    - FILE_LOWER_SNAKE_CASE
  ignore:                                  # Paths to ignore entirely
    - proto/vendor
    - proto/legacy/old.proto
  ignore_only:                             # Ignore specific rules for paths
    ENUM_PASCAL_CASE:
      - proto/legacy
  disallow_comment_ignores: false          # Allow // buf:lint:ignore
  enum_zero_value_suffix: _UNSPECIFIED     # Zero value suffix
  rpc_allow_same_request_response: false   # Allow shared request/response
  rpc_allow_google_protobuf_empty_requests: false
  rpc_allow_google_protobuf_empty_responses: false
  service_suffix: Service                  # Service name suffix
  disable_builtin: false                   # Disable all built-in rules
```

### Breaking Configuration Options

```yaml
breaking:
  use:                                     # Category to use
    - FILE                                 # FILE, PACKAGE, WIRE_JSON, or WIRE
  except:                                  # Rules to exclude
    - EXTENSION_MESSAGE_NO_DELETE
  ignore:                                  # Paths to ignore
    - proto/vendor
  ignore_only:                             # Ignore rules for paths
    FIELD_SAME_JSON_NAME:
      - proto/legacy
  ignore_unstable_packages: true           # Ignore alpha/beta packages
  disable_builtin: false                   # Disable built-in rules
```

### Dependencies

```yaml
deps:
  - buf.build/googleapis/googleapis               # Latest on default label
  - buf.build/bufbuild/protovalidate:demo         # Specific label
  - buf.build/foo/bar:f05a6f4403ce4327bae4f50f    # Specific commit
```

Manage with:
```bash
buf dep update    # Resolve and update buf.lock
buf dep prune     # Remove unused deps
```

### Plugins (Custom Lint/Breaking Rules)

```yaml
plugins:
  - plugin: buf-plugin-foo
    options:
      timestamp_suffix: _time
```

## buf.gen.yaml — Code Generation Configuration

Configures `buf generate` — plugins, inputs, and managed mode.

### Basic Configuration

```yaml
version: v2
plugins:
  # Local plugin
  - local: protoc-gen-go
    out: gen/go
    opt: paths=source_relative

  # Remote BSR plugin
  - remote: buf.build/protocolbuffers/java
    out: gen/java

  # protoc built-in
  - protoc_builtin: cpp
    out: gen/cpp
```

### With Managed Mode

```yaml
version: v2
managed:
  enabled: true
  override:
    - file_option: go_package_prefix
      value: github.com/acme/api/gen/go
    - file_option: java_package_prefix
      value: com
plugins:
  - local: protoc-gen-go
    out: gen/go
    opt: paths=source_relative
  - local: protoc-gen-go-grpc
    out: gen/go
    opt: paths=source_relative
```

### With Inputs

```yaml
version: v2
inputs:
  - directory: .
  - module: buf.build/googleapis/googleapis
    types:
      - google.api.HttpRule
    path:
      - google/api
plugins:
  - local: protoc-gen-go
    out: gen/go
    opt: paths=source_relative
```

### Plugin Fields

| Field             | Description                                                |
|-------------------|------------------------------------------------------------|
| `local`           | Local plugin binary name (or list: `[binary, arg1, arg2]`) |
| `remote`          | BSR remote plugin (e.g., `buf.build/protocolbuffers/go`)   |
| `protoc_builtin`  | protoc built-in plugin (e.g., `cpp`, `java`, `python`)     |
| `out`             | Output directory (required)                                |
| `opt`             | Plugin options (list or comma-separated string)            |
| `strategy`        | Invocation strategy: `directory` or `all`                  |
| `include_imports` | Include imported files in generation                       |
| `include_wkt`     | Include Well-Known Types                                   |

### Input Fields

| Field          | Description                             |
|----------------|-----------------------------------------|
| `directory`    | Local directory                         |
| `module`       | BSR module reference                    |
| `git_repo`     | Git repository URL                      |
| `tarball`      | Tarball URL                             |
| `types`        | Limit to specific fully-qualified types |
| `path`         | Include specific paths                  |
| `exclude_path` | Exclude specific paths                  |

### Clean Option

```yaml
version: v2
clean: true    # Delete output dirs before generating
plugins:
  - local: protoc-gen-go
    out: gen/go
```

### Managed Mode Details

See [references/managed-mode.md](references/managed-mode.md) for the complete managed mode reference.

## buf.lock — Dependency Pinning

Auto-generated by `buf dep update`. Do not edit manually. Tracks exact dependency versions.

```yaml
version: v2
deps:
  - name: buf.build/googleapis/googleapis
    commit: e7e0d5e919a24cc8b9e1b6a9...
    digest: shake256:abc123...
```

## Modules and Workspaces

A **module** is a collection of `.proto` files configured, built, and versioned as a unit. A **workspace** contains one or more modules defined in `buf.yaml`.

### Key Concepts

- All modules in a workspace can import each other without declaring dependencies
- External dependencies are declared in `deps` and shared across all modules
- Module-level lint/breaking configs completely replace workspace defaults (no merging)
- All `.proto` file paths must be unique across workspace modules

### Workspace Layout

```
workspace_root/
├── buf.yaml
├── buf.gen.yaml
├── buf.lock
├── proto/
│   └── acme/
│       └── weatherapi/
│           └── v1/
│               ├── api.proto
│               └── calculate.proto
├── vendor/
│   └── units/
│       └── v1/
│           └── metric.proto
└── README.md
```

### Module Cache

Buf caches downloaded modules locally:
1. `$BUF_CACHE_DIR` (if set)
2. `$XDG_CACHE_HOME` (or `$HOME/.cache` on Linux/Mac, `%LocalAppData%` on Windows)

### Config Migration

Migrate from v1 to v2: `buf config migrate`

For detailed workspace patterns, see [references/modules-workspaces.md](references/modules-workspaces.md).
