---
name: buf-breaking-changes
description: |
  Buf breaking change detection for Protobuf schemas. Use when running buf breaking, understanding
  breaking change categories (FILE, PACKAGE, WIRE_JSON, WIRE), looking up specific breaking change
  rules, configuring breaking change detection in buf.yaml, comparing against git branches/tags,
  integrating buf breaking into CI/CD, or understanding which Protobuf changes are safe vs breaking.
  Also use when deprecating fields/enums/messages, reserving field numbers, or evolving APIs safely.
---

# Buf Breaking Change Detection

`buf breaking` detects breaking changes between your current Protobuf schema and a reference point (git branch, tag, BSR module, etc.). It ensures backward compatibility.

## Quick Start

```bash
# Compare against main branch
buf breaking --against '.git#branch=main'

# Compare against a git tag
buf breaking --against '.git#tag=v1.0.0'

# Compare against BSR
buf breaking --against buf.build/acme/petapis

# Compare all modules against their BSR versions
buf breaking --against-registry

# JSON output
buf breaking --against '.git#branch=main' --error-format=json
```

## Categories

Four categories from strictest to most lenient:

### `FILE` (Default)
Detects changes that move generated code between files. **Strictest.** Protects all generated languages including C++ and Python.

Use when: you share `.proto` files or generated code with clients you don't control.

### `PACKAGE`
Like FILE but only per-package. Safe for languages where moving types between files is fine (like Go).

Use when: all consumers are in Go or similar package-based languages.

### `WIRE_JSON`
Detects changes that break wire (binary) or JSON encoding. **Recommended minimum** because JSON is ubiquitous.

Use when: using Connect, gRPC-Gateway, gRPC JSON, or any JSON-encoded Protobuf.

### `WIRE`
Only detects changes that break binary wire encoding. **Most lenient.**

Use when: you can guarantee only binary-encoded messages are decoded.

### Which Category to Choose?

| Scenario                                      | Category        |
|-----------------------------------------------|-----------------|
| Sharing proto files with external consumers   | `FILE`          |
| All consumers in Go or similar                | `PACKAGE`       |
| Using JSON encoding (Connect, gRPC-Gateway)   | `WIRE_JSON`     |
| Binary-only encoding, full control of clients | `WIRE`          |
| Unsure                                        | `FILE` (safest) |

**Don't mix individual rules** — choose one of the four categories.

## Configuration

```yaml
version: v2
breaking:
  use:
    - FILE
  except:
    - RPC_NO_DELETE          # Exclude specific rules
  ignore:
    - proto/vendor           # Ignore paths
  ignore_only:
    FIELD_SAME_JSON_NAME:
      - proto/legacy
  ignore_unstable_packages: true  # Ignore alpha/beta packages
```

### Per-Module Override

```yaml
version: v2
modules:
  - path: proto/api
  - path: proto/internal
    breaking:
      use:
        - WIRE_JSON           # Internal API: only care about wire compat
breaking:
  use:
    - FILE                    # Default: strictest for external APIs
```

## Usage Examples

### Local Git Comparisons

```bash
# Against branch
buf breaking --against '.git#branch=main'

# Against tag
buf breaking --against '.git#tag=v1.0.0'

# Against tag with subdirectory
buf breaking --against '.git#tag=v1.0.0,subdir=proto'
```

### Remote Comparisons

```bash
# Remote GitHub repo
buf breaking --against 'https://github.com/org/repo.git'

# Remote with specific branch
buf breaking --against 'https://github.com/org/repo.git#branch=main'

# BSR module
buf breaking --against buf.build/acme/petapis

# All workspace modules against BSR
buf breaking --against-registry

# Tarball
buf breaking --against "https://github.com/org/repo/archive/${COMMIT}.tar.gz#strip_components=1"
```

### Limit Scope

```bash
# Only check specific files
buf breaking --against '.git#branch=main' --path path/to/foo.proto

# Override config inline
buf breaking --against '.git#branch=main' --config '{"version":"v2","breaking":{"use":["PACKAGE"]}}'
```

### JSON Output

```bash
buf breaking --against '.git#branch=main' --error-format=json | jq .
```

Output format:
```json
{
  "path": "acme/pet/v1/pet.proto",
  "start_line": 18,
  "start_column": 3,
  "end_line": 18,
  "end_column": 9,
  "type": "FIELD_SAME_TYPE",
  "message": "Field \"1\" on message \"Pet\" changed type from \"enum\" to \"string\"."
}
```

## Safe API Evolution Patterns

### Deprecate Instead of Delete

```protobuf
syntax = "proto3";

// Deprecate a field (safe — doesn't break)
message User {
  string name = 1;
  string email = 2 [deprecated = true];  // Mark deprecated, don't remove
}

// Deprecate an enum value
enum Status {
  STATUS_UNSPECIFIED = 0;
  STATUS_ACTIVE = 1;
  STATUS_INACTIVE = 2 [deprecated = true];
}

// Deprecate a message
message OldRequest {
  option deprecated = true;
  string name = 1;
}

// Deprecate a service
service OldService {
  option deprecated = true;
  rpc GetOld(GetOldRequest) returns (GetOldResponse);
}
```

### Reserve After Deletion

When you must delete, reserve the field number and name to prevent reuse:

```protobuf
message User {
  reserved 2;
  reserved "email";
  string name = 1;
  // field 2 (email) was removed
}

enum Status {
  reserved 2;
  reserved "STATUS_INACTIVE";
  STATUS_UNSPECIFIED = 0;
  STATUS_ACTIVE = 1;
}
```

### Version Your Packages

Create new package versions for breaking changes:

```
proto/acme/api/v1/  →  proto/acme/api/v2/
```

Serve both versions simultaneously and migrate callers.

## CI/CD Integration

### GitHub Actions

```yaml
- uses: bufbuild/buf-action@v1
  with:
    token: ${{ secrets.BUF_TOKEN }}
```

### Generic CI

```bash
# Against remote main (works even with shallow clones)
buf breaking --against 'https://github.com/org/repo.git#branch=main'

# Against BSR (no git needed)
buf breaking --against-registry
```

CI services often do shallow clones, so local `git` branches may not be available. Use remote URLs or BSR references instead.

## Custom Options

`buf breaking` does NOT detect changes to custom options (like `google.api.http`). Custom option semantics vary infinitely and can't be generically validated. Use Buf check plugins for custom option validation.

## Rules Quick Reference

### Deletion Rules

| Rule                                        | FILE | PKG | WIRE_JSON | WIRE |
|---------------------------------------------|------|-----|-----------|------|
| ENUM_NO_DELETE                              | x    |     |           |      |
| ENUM_VALUE_NO_DELETE                        | x    | x   |           |      |
| ENUM_VALUE_NO_DELETE_UNLESS_NAME_RESERVED   |      |     | x         |      |
| ENUM_VALUE_NO_DELETE_UNLESS_NUMBER_RESERVED |      |     | x         | x    |
| EXTENSION_MESSAGE_NO_DELETE                 | x    | x   |           |      |
| EXTENSION_NO_DELETE                         | x    |     |           |      |
| FIELD_NO_DELETE                             | x    | x   |           |      |
| FIELD_NO_DELETE_UNLESS_NAME_RESERVED        |      |     | x         |      |
| FIELD_NO_DELETE_UNLESS_NUMBER_RESERVED      |      |     | x         | x    |
| MESSAGE_NO_DELETE                           | x    |     |           |      |
| ONEOF_NO_DELETE                             | x    | x   |           |      |
| RPC_NO_DELETE                               | x    | x   |           |      |
| SERVICE_NO_DELETE                           | x    |     |           |      |

### Sameness Rules

| Rule                                   | FILE | PKG | WIRE_JSON | WIRE |
|----------------------------------------|------|-----|-----------|------|
| ENUM_VALUE_SAME_NAME                   | x    | x   | x         |      |
| FIELD_SAME_CARDINALITY                 | x    | x   | x         | x    |
| FIELD_SAME_DEFAULT                     | x    | x   | x         | x    |
| FIELD_SAME_JSON_NAME                   | x    | x   | x         |      |
| FIELD_SAME_JSTYPE                      | x    | x   |           |      |
| FIELD_SAME_NAME                        | x    | x   |           |      |
| FIELD_SAME_ONEOF                       | x    | x   | x         | x    |
| FIELD_SAME_TYPE                        | x    | x   | x         | x    |
| FIELD_SAME_UTF8_VALIDATION             | x    | x   | x         | x    |
| FIELD_WIRE_COMPATIBLE_CARDINALITY      |      |     | x         | x    |
| FIELD_WIRE_COMPATIBLE_TYPE             |      |     | x         | x    |
| FIELD_WIRE_JSON_COMPATIBLE_CARDINALITY |      |     | x         |      |
| FIELD_WIRE_JSON_COMPATIBLE_TYPE        |      |     | x         |      |
| ONEOF_NO_DELETE                        | x    | x   |           |      |
| RESERVED_ENUM_NO_DELETE                | x    | x   | x         | x    |
| RESERVED_MESSAGE_NO_DELETE             | x    | x   | x         | x    |

For the complete rules list with detailed descriptions, see [references/breaking-rules.md](references/breaking-rules.md).
