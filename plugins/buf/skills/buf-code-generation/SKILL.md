---
name: buf-code-generation
description: |
  Buf code generation with buf generate, buf.gen.yaml configuration, and managed mode. Use when
  setting up code generation for Go, Java, Python, C++, C#, or other languages from Protobuf files.
  Also use when configuring buf.gen.yaml plugins (local, remote, protoc_builtin), using managed mode
  for file options, generating from BSR modules, limiting generation to specific types or paths,
  or migrating from protoc to buf generate.
---

# Buf Code Generation

`buf generate` produces code stubs from Protobuf files using protoc plugins. It's faster and simpler than `protoc`, configured through `buf.gen.yaml`.

## Quick Start

```bash
# Generate from current workspace
buf generate

# Generate from BSR module
buf generate buf.build/bufbuild/protovalidate

# Generate from specific version
buf generate buf.build/foo/bar:v1.0.0

# Generate from GitHub
buf generate https://github.com/foo/bar.git
```

## buf.gen.yaml — Plugin Configuration

### Local Plugins

Plugins installed on your `$PATH`:

```yaml
version: v2
plugins:
  - local: protoc-gen-go
    out: gen/go
    opt: paths=source_relative
  - local: protoc-gen-go-grpc
    out: gen/go
    opt: paths=source_relative
```

### Remote Plugins (BSR)

Code generation happens on the BSR — no local plugin installation needed:

```yaml
version: v2
plugins:
  - remote: buf.build/protocolbuffers/go
    out: gen/go
    opt: paths=source_relative
  - remote: buf.build/grpc/go
    out: gen/go
    opt: paths=source_relative
```

### protoc Built-in Plugins

```yaml
version: v2
plugins:
  - protoc_builtin: cpp
    out: gen/cpp
  - protoc_builtin: java
    out: gen/java
  - protoc_builtin: python
    out: gen/python
```

### Local Plugin with Arguments

```yaml
plugins:
  - local: ["protoc-gen-custom", "--flag=value"]
    out: gen/custom
```

## Common Language Configurations

### Go

```yaml
version: v2
managed:
  enabled: true
  override:
    - file_option: go_package_prefix
      value: github.com/acme/api/gen/go
plugins:
  - local: protoc-gen-go
    out: gen/go
    opt: paths=source_relative
  - local: protoc-gen-go-grpc
    out: gen/go
    opt: paths=source_relative
```

### Java

```yaml
version: v2
managed:
  enabled: true
plugins:
  - remote: buf.build/protocolbuffers/java
    out: gen/java
```

### Python

```yaml
version: v2
plugins:
  - protoc_builtin: python
    out: gen/python
  - protoc_builtin: pyi
    out: gen/python
```

### TypeScript (Connect)

```yaml
version: v2
managed:
  enabled: true
plugins:
  - remote: buf.build/connectrpc/es
    out: gen/ts
    opt: target=ts
```

### C#

```yaml
version: v2
managed:
  enabled: true
  override:
    - file_option: csharp_namespace
      value: Acme.Api
plugins:
  - protoc_builtin: csharp
    out: gen/csharp
```

## Managed Mode

Managed mode lets consumers control file/field options during generation, removing the need for producers to hard-code language-specific options in `.proto` files.

```yaml
version: v2
managed:
  enabled: true
  override:
    - file_option: go_package_prefix
      value: github.com/acme/gen/go
    - file_option: java_package_prefix
      value: com.acme
  disable:
    - module: buf.build/googleapis/googleapis  # Don't modify third-party
```

Key behaviors:
- `java_multiple_files` defaults to `true`
- `java_package` defaults to `com.<proto_package>`
- `go_package` defaults to last package component
- `optimize_for` defaults to `SPEED`
- `disable` takes precedence over `override`
- Last matching `override` rule wins

## Inputs

### Specify in buf.gen.yaml

```yaml
version: v2
inputs:
  - directory: .
  - module: buf.build/googleapis/googleapis
    types:
      - google.api.HttpRule
    path:
      - google/api
  - git_repo: https://github.com/foo/bar.git
    branch: main
```

### Specify on Command Line

```bash
# Current workspace (default)
buf generate

# BSR module
buf generate buf.build/acme/petapis

# GitHub repo
buf generate https://github.com/foo/bar.git
```

## Limiting Generation

### By Path

```bash
# Only generate for files in proto/foo
buf generate --path proto/foo

# Exclude paths
buf generate --exclude-path proto/baz

# Both
buf generate --path proto/foo --exclude-path proto/foo/test
```

Or in buf.gen.yaml:

```yaml
inputs:
  - directory: .
    path:
      - proto/foo/
    exclude_path:
      - proto/foo/baz.proto
```

### By Type

Generate only specific fully-qualified types and their dependencies:

```bash
buf generate --type foo.v1.User --type foo.v1.UserService
```

Or in buf.gen.yaml:

```yaml
inputs:
  - module: buf.build/acme/api
    types:
      - foo.v1.User
      - foo.v1.UserService
```

### Including Dependencies

```bash
# Include imported files
buf generate --include-imports

# Include Well-Known Types (requires --include-imports)
buf generate --include-imports --include-wkt
```

## Clean Output

Delete output directories before generating:

```yaml
version: v2
clean: true
plugins:
  - local: protoc-gen-go
    out: gen/go
```

## Alternative Templates

```bash
# Use a different config file
buf generate --template buf.gen.go.yaml

# Separate configs per language
buf generate --template buf.gen.go.yaml
buf generate --template buf.gen.java.yaml

# Inline JSON config
buf generate --template '{"version":"v2","plugins":[{"protoc_builtin":"go","out":"gen/go"}]}'
```

## Output Directory

```bash
# Prepend directory to all out paths
buf generate -o gen/
buf generate https://github.com/foo/bar.git -o bar/
```

## Plugin Strategy

The `strategy` field controls how plugins receive input:

| Strategy              | Behavior                            |
|-----------------------|-------------------------------------|
| `directory` (default) | One plugin invocation per directory |
| `all`                 | Single invocation with all files    |

```yaml
plugins:
  - local: protoc-gen-go
    out: gen/go
    strategy: all
```

## Migration from protoc

Replace `protoc` commands with `buf generate`:

```bash
# protoc
protoc -I proto --go_out=gen/go --go_opt=paths=source_relative proto/**/*.proto

# buf generate (equivalent)
buf generate
```

With this `buf.gen.yaml`:

```yaml
version: v2
plugins:
  - local: protoc-gen-go
    out: gen/go
    opt: paths=source_relative
```

Key differences:
- No `-I` flags needed — Buf discovers files from `buf.yaml` modules
- No glob patterns — Buf builds all files in the workspace
- Plugin options go in `buf.gen.yaml`, not command-line flags
