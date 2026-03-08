---
name: buf-linting
description: |
  Buf lint rules and configuration for Protobuf files. Use when configuring buf lint,
  understanding lint categories (MINIMAL, BASIC, STANDARD, COMMENTS, UNARY_RPC), looking up
  specific lint rule names, fixing lint errors, ignoring lint rules with comments, customizing
  lint configuration in buf.yaml, or integrating buf lint into CI/CD. Also use when asking
  which lint rules to use or how to suppress specific lint warnings.
---

# Buf Linting

`buf lint` enforces Protobuf best practices through configurable rules organized into categories of increasing strictness.

## Quick Start

```bash
# Lint with default STANDARD rules
buf lint

# Lint with JSON output
buf lint --error-format=json

# Lint specific files
buf lint --path proto/foo/v1/foo.proto

# Lint a BSR module
buf lint buf.build/acme/petapis

# List all available rules
buf config ls-lint-rules
```

## Categories

Three hierarchical categories (each includes all rules from the previous):

### `MINIMAL`
Fundamental rules for modern Protobuf development. No downside to applying them.

| Rule                      | What it checks                                    |
|---------------------------|---------------------------------------------------|
| `DIRECTORY_SAME_PACKAGE`  | All files in a directory share the same package   |
| `PACKAGE_DEFINED`         | All files have a package declaration              |
| `PACKAGE_DIRECTORY_MATCH` | Files are in directories matching their package   |
| `PACKAGE_NO_IMPORT_CYCLE` | No circular package imports (v2 only)             |
| `PACKAGE_SAME_DIRECTORY`  | All files with same package are in same directory |

### `BASIC`
Everything in MINIMAL, plus widely accepted style rules.

Additional rules:
- `ENUM_FIRST_VALUE_ZERO` — First enum value is zero
- `ENUM_NO_ALLOW_ALIAS` — No `allow_alias` option
- `ENUM_PASCAL_CASE` — Enum names are PascalCase
- `ENUM_VALUE_UPPER_SNAKE_CASE` — Enum values are UPPER_SNAKE_CASE
- `FIELD_LOWER_SNAKE_CASE` — Field names are lower_snake_case
- `FIELD_NOT_REQUIRED` — No required fields (v2 only)
- `IMPORT_NO_PUBLIC` — No public imports
- `IMPORT_NO_WEAK` — No weak imports (deprecated)
- `IMPORT_USED` — All imports are used
- `MESSAGE_PASCAL_CASE` — Message names are PascalCase
- `ONEOF_LOWER_SNAKE_CASE` — Oneof names are lower_snake_case
- `PACKAGE_LOWER_SNAKE_CASE` — Package names are lower_snake_case
- `PACKAGE_SAME_CSHARP_NAMESPACE` — Consistent csharp_namespace per package
- `PACKAGE_SAME_GO_PACKAGE` — Consistent go_package per package
- `PACKAGE_SAME_JAVA_MULTIPLE_FILES` — Consistent java_multiple_files per package
- `PACKAGE_SAME_JAVA_PACKAGE` — Consistent java_package per package
- `PACKAGE_SAME_PHP_NAMESPACE` — Consistent php_namespace per package
- `PACKAGE_SAME_RUBY_PACKAGE` — Consistent ruby_package per package
- `PACKAGE_SAME_SWIFT_PREFIX` — Consistent swift_prefix per package
- `RPC_PASCAL_CASE` — RPC names are PascalCase
- `SERVICE_PASCAL_CASE` — Service names are PascalCase
- `SYNTAX_SPECIFIED` — All files specify a syntax

### `STANDARD` (Default)
Everything in BASIC, plus Buf's recommended rules for modern Protobuf.

Additional rules:
- `ENUM_VALUE_PREFIX` — Values prefixed with UPPER_SNAKE_CASE enum name
- `ENUM_ZERO_VALUE_SUFFIX` — Zero values end with `_UNSPECIFIED`
- `FILE_LOWER_SNAKE_CASE` — Filenames are lower_snake_case.proto
- `PACKAGE_VERSION_SUFFIX` — Package ends with version (v1, v1beta1, etc.)
- `PROTOVALIDATE` — Protovalidate constraints are valid
- `RPC_REQUEST_STANDARD_NAME` — Request messages named `MethodNameRequest`
- `RPC_RESPONSE_STANDARD_NAME` — Response messages named `MethodNameResponse`
- `RPC_REQUEST_RESPONSE_UNIQUE` — All request/response messages are unique
- `SERVICE_SUFFIX` — Services suffixed with `Service`

### `COMMENTS` (Separate Category)
Enforces comments on Protobuf elements. Not part of the strictness hierarchy.

- `COMMENT_ENUM`, `COMMENT_ENUM_VALUE`, `COMMENT_FIELD`
- `COMMENT_MESSAGE`, `COMMENT_ONEOF`, `COMMENT_RPC`, `COMMENT_SERVICE`

```yaml
# Add specific comment rules alongside STANDARD
version: v2
lint:
  use:
    - STANDARD
    - COMMENT_ENUM
    - COMMENT_MESSAGE
    - COMMENT_SERVICE
```

### `UNARY_RPC` (Separate Category)
Outlaws streaming RPCs. Useful for protocols that don't support streaming (e.g., Twirp).

- `RPC_NO_CLIENT_STREAMING`
- `RPC_NO_SERVER_STREAMING`

## Configuration

Configure lint in `buf.yaml`:

```yaml
version: v2
lint:
  use:
    - STANDARD                    # Category to use
  except:
    - FILE_LOWER_SNAKE_CASE       # Exclude specific rules
  ignore:
    - proto/vendor                # Ignore entire directories
    - proto/legacy/old.proto      # Ignore specific files
  ignore_only:
    ENUM_PASCAL_CASE:             # Ignore rule for specific paths
      - proto/legacy
    BASIC:                        # Can ignore entire categories
      - proto/third_party
  disallow_comment_ignores: false # Allow // buf:lint:ignore (default)
  enum_zero_value_suffix: _UNSPECIFIED
  rpc_allow_same_request_response: false
  rpc_allow_google_protobuf_empty_requests: false
  rpc_allow_google_protobuf_empty_responses: false
  service_suffix: Service
```

### Default Configuration

If `buf.yaml` has no lint section, the defaults are:

```yaml
lint:
  use:
    - STANDARD
  enum_zero_value_suffix: _UNSPECIFIED
  rpc_allow_same_request_response: false
  rpc_allow_google_protobuf_empty_requests: false
  rpc_allow_google_protobuf_empty_responses: false
  service_suffix: Service
```

### Per-Module Overrides

Modules can override workspace-level lint settings entirely (no merging):

```yaml
version: v2
modules:
  - path: proto/api
  - path: proto/vendor
    lint:
      use:
        - MINIMAL    # Vendor code only needs minimal rules
lint:
  use:
    - STANDARD       # Default for all other modules
```

## Comment Ignores

Suppress lint rules for specific lines using comments. Enabled by default in v2 configs.

```protobuf
// Context explaining why this ignore is needed.
// buf:lint:ignore PACKAGE_LOWER_SNAKE_CASE
// buf:lint:ignore PACKAGE_VERSION_SUFFIX
package A;
```

Rules:
- Place `// buf:lint:ignore RULE_ID` directly above the offending line
- Add a context comment above the ignore (above, not on same line)
- Multiple ignores: one per line
- Disable with `disallow_comment_ignores: true` in `buf.yaml`

## Buf Check Plugins

Extend linting with custom rules:

```yaml
version: v2
lint:
  use:
    - STANDARD
    - CATEGORY_FROM_PLUGIN
  except:
    - RULE_ID_FROM_PLUGIN
plugins:
  - plugin: buf-plugin-foo
    options:
      timestamp_suffix: _time
```

List all rules including plugin rules: `buf config ls-lint-rules`

## CI/CD Integration

```bash
# Basic CI lint check
buf lint --error-format=json

# Lint with inline config override
buf lint --config '{"version":"v2","lint":{"use":["BASIC"]}}'

# GitHub Actions
- uses: bufbuild/buf-action@v1
  with:
    token: ${{ secrets.BUF_TOKEN }}
```

## Complete Rules Reference

For detailed descriptions of every rule, examples of violations, and their category memberships, see [references/lint-rules.md](references/lint-rules.md).
