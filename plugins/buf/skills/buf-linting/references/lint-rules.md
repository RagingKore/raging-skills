# Buf Lint Rules — Complete Reference

Every built-in lint rule with its category membership, description, and violation examples.

## Category Membership Summary

| Rule                              | MINIMAL | BASIC | STANDARD | Other         |
|-----------------------------------|---------|-------|----------|---------------|
| DIRECTORY_SAME_PACKAGE            | x       | x     | x        |               |
| PACKAGE_DEFINED                   | x       | x     | x        |               |
| PACKAGE_DIRECTORY_MATCH           | x       | x     | x        |               |
| PACKAGE_NO_IMPORT_CYCLE           | x*      | x*    | x*       |               |
| PACKAGE_SAME_DIRECTORY            | x       | x     | x        |               |
| ENUM_FIRST_VALUE_ZERO             |         | x     | x        |               |
| ENUM_NO_ALLOW_ALIAS               |         | x     | x        |               |
| ENUM_PASCAL_CASE                  |         | x     | x        |               |
| ENUM_VALUE_UPPER_SNAKE_CASE       |         | x     | x        |               |
| FIELD_LOWER_SNAKE_CASE            |         | x     | x        |               |
| FIELD_NOT_REQUIRED                |         | x*    | x*       |               |
| IMPORT_NO_PUBLIC                  |         | x     | x        |               |
| IMPORT_NO_WEAK                    |         | x     | x        |               |
| IMPORT_USED                       |         | x     | x        |               |
| MESSAGE_PASCAL_CASE               |         | x     | x        |               |
| ONEOF_LOWER_SNAKE_CASE            |         | x     | x        |               |
| PACKAGE_LOWER_SNAKE_CASE          |         | x     | x        |               |
| PACKAGE_SAME_CSHARP_NAMESPACE     |         | x     | x        |               |
| PACKAGE_SAME_GO_PACKAGE           |         | x     | x        |               |
| PACKAGE_SAME_JAVA_MULTIPLE_FILES  |         | x     | x        |               |
| PACKAGE_SAME_JAVA_PACKAGE         |         | x     | x        |               |
| PACKAGE_SAME_PHP_NAMESPACE        |         | x     | x        |               |
| PACKAGE_SAME_RUBY_PACKAGE         |         | x     | x        |               |
| PACKAGE_SAME_SWIFT_PREFIX         |         | x     | x        |               |
| RPC_PASCAL_CASE                   |         | x     | x        |               |
| SERVICE_PASCAL_CASE               |         | x     | x        |               |
| SYNTAX_SPECIFIED                  |         | x     | x        |               |
| ENUM_VALUE_PREFIX                 |         |       | x        |               |
| ENUM_ZERO_VALUE_SUFFIX            |         |       | x        |               |
| FILE_LOWER_SNAKE_CASE             |         |       | x        |               |
| PACKAGE_VERSION_SUFFIX            |         |       | x        |               |
| PROTOVALIDATE                     |         |       | x        |               |
| RPC_REQUEST_STANDARD_NAME         |         |       | x        |               |
| RPC_RESPONSE_STANDARD_NAME        |         |       | x        |               |
| RPC_REQUEST_RESPONSE_UNIQUE       |         |       | x        |               |
| SERVICE_SUFFIX                    |         |       | x        |               |
| COMMENT_ENUM                      |         |       |          | COMMENTS      |
| COMMENT_ENUM_VALUE                |         |       |          | COMMENTS      |
| COMMENT_FIELD                     |         |       |          | COMMENTS      |
| COMMENT_MESSAGE                   |         |       |          | COMMENTS      |
| COMMENT_ONEOF                     |         |       |          | COMMENTS      |
| COMMENT_RPC                       |         |       |          | COMMENTS      |
| COMMENT_SERVICE                   |         |       |          | COMMENTS      |
| RPC_NO_CLIENT_STREAMING           |         |       |          | UNARY_RPC     |
| RPC_NO_SERVER_STREAMING           |         |       |          | UNARY_RPC     |
| STABLE_PACKAGE_NO_IMPORT_UNSTABLE |         |       |          | uncategorized |

`*` = v2 configuration only

## Individual Rules

### DIRECTORY_SAME_PACKAGE
**Categories:** MINIMAL, BASIC, STANDARD

All files in a given directory must be in the same package.

### PACKAGE_DEFINED
**Categories:** MINIMAL, BASIC, STANDARD

All files must have a package declaration.

### PACKAGE_DIRECTORY_MATCH
**Categories:** MINIMAL, BASIC, STANDARD

All files must be in a directory that matches their package name.

### PACKAGE_NO_IMPORT_CYCLE
**Categories:** MINIMAL (v2), BASIC (v2), STANDARD (v2)

Detects package import cycles. The Protobuf compiler outlaws circular file imports, but it's still possible to introduce package cycles. This causes problems for languages that rely on package-based imports (like Go).

### PACKAGE_SAME_DIRECTORY
**Categories:** MINIMAL, BASIC, STANDARD

All files with a given package must be in the same directory.

### ENUM_FIRST_VALUE_ZERO
**Categories:** BASIC, STANDARD

First enum value must be the zero value. Required by proto3, enforced in proto2 by this rule.

```protobuf
// BAD
enum Scheme {
  SCHEME_FTP = 1;         // First value is not zero!
  SCHEME_UNSPECIFIED = 0;
}

// GOOD
enum Scheme {
  SCHEME_UNSPECIFIED = 0;
  SCHEME_FTP = 1;
}
```

### ENUM_NO_ALLOW_ALIAS
**Categories:** BASIC, STANDARD

Enums must not use `allow_alias`. Aliases cause issues with JSON serialization.

### ENUM_PASCAL_CASE
**Categories:** BASIC, STANDARD

Enum names must be PascalCase.

### ENUM_VALUE_PREFIX
**Categories:** STANDARD

Enum values must be prefixed with the UPPER_SNAKE_CASE of the enum name.

```protobuf
// BAD
enum Scheme {
  UNSPECIFIED = 0;
  HTTP = 1;
}

// GOOD
enum Scheme {
  SCHEME_UNSPECIFIED = 0;
  SCHEME_HTTP = 1;
}
```

### ENUM_VALUE_UPPER_SNAKE_CASE
**Categories:** BASIC, STANDARD

Enum values must be UPPER_SNAKE_CASE.

### ENUM_ZERO_VALUE_SUFFIX
**Categories:** STANDARD

Zero values must end with `_UNSPECIFIED` (configurable via `enum_zero_value_suffix`).

### FIELD_LOWER_SNAKE_CASE
**Categories:** BASIC, STANDARD

Field names must be lower_snake_case.

### FIELD_NOT_REQUIRED (v2 only)
**Categories:** BASIC, STANDARD

Fields must not be `required` (proto2) or `field_presence = LEGACY_REQUIRED` (Editions).

### FILE_LOWER_SNAKE_CASE
**Categories:** STANDARD

`.proto` filenames must be lower_snake_case.

### IMPORT_NO_PUBLIC
**Categories:** BASIC, STANDARD

No public imports allowed.

### IMPORT_USED
**Categories:** BASIC, STANDARD

All declared imports must be used.

### MESSAGE_PASCAL_CASE
**Categories:** BASIC, STANDARD

Message names must be PascalCase.

### ONEOF_LOWER_SNAKE_CASE
**Categories:** BASIC, STANDARD

Oneof names must be lower_snake_case.

### PACKAGE_LOWER_SNAKE_CASE
**Categories:** BASIC, STANDARD

Package names must be lower_snake_case.

### PACKAGE_SAME_<file_option>
**Categories:** BASIC, STANDARD

All files in the same package must share the same value for language-specific file options. Seven rules: `PACKAGE_SAME_CSHARP_NAMESPACE`, `PACKAGE_SAME_GO_PACKAGE`, `PACKAGE_SAME_JAVA_MULTIPLE_FILES`, `PACKAGE_SAME_JAVA_PACKAGE`, `PACKAGE_SAME_PHP_NAMESPACE`, `PACKAGE_SAME_RUBY_PACKAGE`, `PACKAGE_SAME_SWIFT_PREFIX`.

### PACKAGE_VERSION_SUFFIX
**Categories:** STANDARD

Last package component must be a version: `v\d+`, `v\d+test.*`, `v\d+(alpha|beta)\d*`, or `v\d+p\d+(alpha|beta)\d*`.

### PROTOVALIDATE
**Categories:** STANDARD

All protovalidate constraints must be valid (CEL expressions compile, types match, no contradictory rules).

### RPC_REQUEST_STANDARD_NAME / RPC_RESPONSE_STANDARD_NAME
**Categories:** STANDARD

Request messages must be named `MethodNameRequest` or `ServiceNameMethodNameRequest`. Response messages follow the same pattern with `Response`.

### RPC_REQUEST_RESPONSE_UNIQUE
**Categories:** STANDARD

All request and response messages must be unique across the schema. No sharing request/response types between RPCs.

### SERVICE_PASCAL_CASE
**Categories:** BASIC, STANDARD

Service names must be PascalCase.

### SERVICE_SUFFIX
**Categories:** STANDARD

Services must end with `Service` (configurable via `service_suffix`).

### STABLE_PACKAGE_NO_IMPORT_UNSTABLE
**Categories:** none (opt-in)

Stable versioned packages (e.g., `v1`) must not import unstable packages (e.g., `v1alpha1`, `v1beta`).

### COMMENT_* Rules
**Categories:** COMMENTS

Seven rules enforcing non-empty leading comments on: `COMMENT_ENUM`, `COMMENT_ENUM_VALUE`, `COMMENT_FIELD`, `COMMENT_MESSAGE`, `COMMENT_ONEOF`, `COMMENT_RPC`, `COMMENT_SERVICE`.

### RPC_NO_CLIENT_STREAMING / RPC_NO_SERVER_STREAMING
**Categories:** UNARY_RPC

RPCs must not use client or server streaming.
