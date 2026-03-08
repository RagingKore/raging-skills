# Managed Mode Reference

Managed mode is a feature of `buf generate` that lets API consumers control file and field options during code generation, even without control of the API itself.

## Enable

```yaml
version: v2
managed:
  enabled: true
```

## Before and After

**Without managed mode** — producers hard-code language options in `.proto` files:

```protobuf
syntax = "proto3";
package acme.weather.v1;
option java_multiple_files = true;
option java_outer_classname = "WeatherProto";
option java_package = "com.acme.weather.v1";
```

**With managed mode** — clean `.proto` files, options in `buf.gen.yaml`:

```protobuf
syntax = "proto3";
package acme.weather.v1;
```

```yaml
version: v2
managed:
  enabled: true
plugins:
  - remote: buf.build/protocolbuffers/java
    out: gen/proto/java
```

## Disable Rules

Exclude modules, paths, files, fields, or specific options from managed mode:

```yaml
managed:
  enabled: true
  disable:
    # Don't modify any file in a specific module
    - module: buf.build/googleapis/googleapis

    # Don't modify files in a specific path
    - path: foo/v1

    # Don't modify a specific file option for any file
    - file_option: csharp_namespace

    # Don't modify a specific field option
    - field_option: js_type

    # Don't modify a specific field
    - field: foo.bar.Baz.field_name

    # Combine module + path + option
    - module: buf.build/acme/weather
      path: weather/v1beta1/
      file_option: java_package
```

## Override Rules

Override default values for file and field options:

```yaml
managed:
  enabled: true
  override:
    # Set go_package_prefix for all files
    - file_option: go_package_prefix
      value: company.com/foo/bar

    # Override for specific module (last matching rule wins)
    - file_option: go_package_prefix
      module: buf.build/acme/weather
      value: x/y/z

    # Set go_package directly for specific path
    - file_option: go_package
      path: special/path/
      value: special/value/package/v1

    # Set field option for specific field
    - field_option: jstype
      field: package.Message.field
      value: JS_STRING
```

## Precedence Rules

1. `disable` takes precedence over `override` — if an option is disabled, overrides don't apply.
2. Disabling a base option (e.g., `go_package`) also disables its prefix/suffix variants.
3. For multiple rules modifying the same option, **last matching rule wins**.
4. If both `java_package_prefix` and `java_package_suffix` are the last rules, the result is `<prefix>.proto_package.<suffix>`.

## File Options

Managed mode handles these file options:

| Option                   | Default Behavior                        |
|--------------------------|-----------------------------------------|
| `cc_enable_arenas`       | Set to `true`                           |
| `csharp_namespace`       | Based on package (PascalCase segments)  |
| `go_package`             | Last package component (e.g., `foov1`)  |
| `go_package_prefix`      | Prefix for go_package                   |
| `java_multiple_files`    | Set to `true`                           |
| `java_package`           | `com.<proto_package>`                   |
| `java_package_prefix`    | Prefix for java_package                 |
| `java_package_suffix`    | Suffix for java_package                 |
| `java_string_check_utf8` | Set to `true`                           |
| `objc_class_prefix`      | Based on package                        |
| `optimize_for`           | Set to `SPEED`                          |
| `php_metadata_namespace` | Based on package                        |
| `php_namespace`          | Based on package (PascalCase)           |
| `ruby_package`           | Based on package (PascalCase with `::`) |
| `swift_prefix`           | Based on package                        |

## Field Options

| Option   | Applies To                               | Values                                |
|----------|------------------------------------------|---------------------------------------|
| `jstype` | int64, uint64, sint64, fixed64, sfixed64 | `JS_NORMAL`, `JS_STRING`, `JS_NUMBER` |

## Common Patterns

### Go

```yaml
managed:
  enabled: true
  override:
    - file_option: go_package_prefix
      value: github.com/acme/api/gen/go
```

### Java

```yaml
managed:
  enabled: true
  override:
    - file_option: java_package_prefix
      value: com.acme
```

### Multiple Languages

```yaml
managed:
  enabled: true
  override:
    - file_option: go_package_prefix
      value: github.com/acme/gen/go
    - file_option: java_package_prefix
      value: com.acme
    - file_option: csharp_namespace
      module: buf.build/acme/api
      value: Acme.Api
  disable:
    - module: buf.build/googleapis/googleapis   # Don't modify third-party
```
