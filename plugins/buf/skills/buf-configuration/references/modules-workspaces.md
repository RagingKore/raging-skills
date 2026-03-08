# Modules and Workspaces Reference

## Core Concepts

A **module** is a collection of Protobuf files configured, built, and versioned as a logical unit. A **workspace** contains one or more modules in a `buf.yaml` file.

## Workspace Layout

```
workspace_root/
├── buf.yaml           # Workspace configuration
├── buf.gen.yaml       # Code generation config
├── buf.lock           # Dependency lock file
├── proto/
│   └── acme/
│       └── weatherapi/
│           └── v1/
│               ├── api.proto
│               ├── calculate.proto
│               └── conversion.proto
├── vendor/
│   └── units/
│       └── v1/
│           ├── imperial.proto
│           └── metric.proto
├── LICENSE
└── README.md
```

## Configuration

```yaml
version: v2
modules:
  - path: proto
    name: buf.build/acme/weatherapi
  - path: vendor
    lint:
      use:
        - MINIMAL
    breaking:
      use:
        - PACKAGE
deps:
  - buf.build/googleapis/googleapis
  - buf.build/grpc/grpc
lint:
  use:
    - STANDARD
breaking:
  use:
    - PACKAGE
```

## Single-Module Workspaces

For repos with one module at the root:

```yaml
version: v2
```

This is equivalent to `modules: [{path: "."}]`.

With a name and settings:

```yaml
version: v2
name: buf.build/foo/bar
lint:
  use:
    - STANDARD
breaking:
  use:
    - FILE
deps:
  - buf.build/googleapis/googleapis
```

Always specify `modules` explicitly if the module is in a subdirectory:

```yaml
version: v2
modules:
  - path: proto
    name: buf.build/foo/bar
```

## Multi-Module Workspaces

Multiple modules can share a path if they don't share `.proto` files. Use `includes` and `excludes`:

```yaml
version: v2
modules:
  - path: proto/common
    name: buf.build/acme/weather
    includes:
      - proto/common/weather
  - path: proto/common
    name: buf.build/acme/location
    includes:
      - proto/common/location
    excludes:
      - proto/common/location/test
```

## Dependency Management

Modules in the same workspace can import each other without declaring dependencies. External dependencies go in `deps`:

```yaml
deps:
  - buf.build/googleapis/googleapis              # Latest default label
  - buf.build/bufbuild/protovalidate:demo        # Specific label
  - buf.build/foo/bar:f05a6f4403ce4327bae4f50f   # Specific commit
```

### Import Resolution

Buf resolves imports by:
1. Looking in workspace modules first (local resolution)
2. Then checking BSR based on `deps` declarations

Imports are relative to the module root (the `path` value):

```protobuf
// If vendor module path is "vendor", import files relative to "vendor/"
import "units/v1/imperial.proto";
import "units/v1/metric.proto";
```

## Unique File Path Requirement

All `.proto` file paths must be unique across workspace modules. This prevents ambiguous imports.

**Invalid** (both modules have `baz/baz.proto`):
```yaml
modules:
  - path: foo   # contains foo/baz/baz.proto
  - path: bar   # contains bar/baz/baz.proto
```

Both resolve to `baz/baz.proto` — Buf errors instead of silently picking one (like protoc would based on `-I` order).

## Module Cache

Downloaded modules are cached at:
1. `$BUF_CACHE_DIR` (if set)
2. `$XDG_CACHE_HOME` (fallback: `$HOME/.cache` on Linux/Mac, `%LocalAppData%` on Windows)

Local workspace modules override the cache for named modules.

## BSR Module References

Format: `<BSR_INSTANCE>/<ORGANIZATION>/<REPOSITORY>`

```
buf.build/acme/petapis           # Public BSR
example.buf.dev/acme/petapis     # Pro (custom subdomain)
buf.example.com/acme/petapis     # Enterprise (custom domain)
```

## Pushing to BSR

```bash
buf push                     # Push all named modules
buf push --label v1.0.0      # Push with a label
```

All modules in the workspace are pushed in dependency order. They must have `name` values that resolve to BSR repositories.

## Module Documentation

Add `README.md` at the module root. It's displayed as primary documentation on the BSR and triggers new commits when changed.

## Migration

Migrate v1 config to v2:

```bash
buf config migrate
```
