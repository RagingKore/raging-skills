---
name: protobuf-style-guide
description: |
  Buf's official Protobuf style guide for writing .proto files. Use when writing new .proto files,
  reviewing Protobuf schemas, naming messages/enums/services/fields, structuring packages, choosing
  file layout, designing RPC request/response types, or applying Protobuf naming conventions.
  Also use when asking about Protobuf best practices, proto3 style, or Buf style recommendations.
---

# Protobuf Style Guide

Buf's official style guide for Protobuf. This is a concise reference for writing consistent, maintainable `.proto` schemas. The requirements map directly to the `STANDARD` lint category in the Buf CLI.

## File Layout

Lay out `.proto` files in this order:

1. License header (if applicable)
2. File overview comment
3. `syntax` declaration
4. `package` declaration
5. Imports (sorted alphabetically)
6. File options
7. Everything else (messages, enums, services)

`buf format` enforces items 3-7 automatically.

## Files and Packages

- **Every file MUST have a `package` declaration.**
- All files of the same package MUST be in the same directory.
- All files MUST be in a directory that matches their package name.
- Package names MUST be `lower_snake_case`.
- The last component of a package MUST be a version (e.g., `v1`, `v1beta1`, `v1alpha2`).
- Filenames MUST be `lower_snake_case.proto`.

Example directory structure:

```
.
├── buf.yaml
└── proto
    └── foo
        └── bar
            ├── bat
            │   └── v1
            │       └── bat.proto          // package foo.bar.bat.v1
            └── baz
                └── v1
                    ├── baz.proto          // package foo.bar.baz.v1
                    └── baz_service.proto  // package foo.bar.baz.v1
```

### File Options Consistency

All files in the same package MUST share the same values for these options (or all leave them unset):

- `csharp_namespace`
- `go_package`
- `java_multiple_files`
- `java_package`
- `php_namespace`
- `ruby_package`
- `swift_prefix`

```protobuf
// foo_one.proto
syntax = "proto3";
package foo.v1;
option go_package = "foov1";
option java_multiple_files = true;
option java_package = "com.foo.v1";
```

All other files with `package foo.v1` MUST set these same three options to the same values.

## Imports

- Do NOT use `public` imports.
- Do NOT use `weak` imports.

## Messages

- Message names: **`PascalCase`**.
- Field names: **`lower_snake_case`**.
- Oneof names: **`lower_snake_case`**.
- Use **pluralized names** for `repeated` fields.
- Name fields after their type when possible (e.g., field of type `FooBar` → name `foo_bar`).
- **Avoid nested messages and enums** — you may want to reuse them outside their parent later.

```protobuf
message CreateUserRequest {
  string user_name = 1;
  repeated string email_addresses = 2;  // Pluralized
}
```

## Enums

- Enum names: **`PascalCase`**.
- Enum value names: **`UPPER_SNAKE_CASE`**.
- **Prefix** all values with the `UPPER_SNAKE_CASE` of the enum name.
- **Suffix** the zero value with `_UNSPECIFIED`.
- Do NOT use `allow_alias`.

```protobuf
enum Status {
  STATUS_UNSPECIFIED = 0;
  STATUS_ACTIVE = 1;
  STATUS_INACTIVE = 2;
}
```

## Services

- Service names: **`PascalCase`**, suffixed with **`Service`**.
- RPC names: **`PascalCase`**.
- All RPC request/response messages MUST be **unique** across the schema.
- Name request/response messages as `MethodNameRequest`/`MethodNameResponse` or `ServiceNameMethodNameRequest`/`ServiceNameMethodNameResponse`.

```protobuf
service UserService {
  rpc CreateUser(CreateUserRequest) returns (CreateUserResponse);
  rpc GetUser(GetUserRequest) returns (GetUserResponse);
  rpc DeleteUser(DeleteUserRequest) returns (DeleteUserResponse);
}
```

### Never Use `google.protobuf.Empty`

Define custom empty request/response messages per RPC instead. This allows adding fields later without breaking changes.

```protobuf
// GOOD — future-proof
message HealthCheckRequest {}
message HealthCheckResponse {}

service HealthService {
  rpc Check(HealthCheckRequest) returns (HealthCheckResponse);
}
```

If you must use `Empty`, configure `buf.yaml`:

```yaml
version: v2
lint:
  use:
    - STANDARD
  rpc_allow_google_protobuf_empty_requests: true
  rpc_allow_google_protobuf_empty_responses: true
```

## Comments

- Use `//` comments (not `/* */`).
- Over-document with complete sentences.
- Place comments **above** the type, not inline.

```protobuf
// Represents a physical mailing address for a customer.
// Used for shipping and billing purposes.
message Address {
  // Street address line 1 (e.g., "123 Main St").
  string line_1 = 1;

  // Optional street address line 2 (e.g., "Apt 4B").
  string line_2 = 2;

  // City name.
  string city = 3;
}
```

## Recommendations

- **Always set up breaking change detection** (`buf breaking`).
- **Avoid widely used keywords** in package names (e.g., `internal` breaks Go imports).
- **Avoid streaming RPCs** unless absolutely necessary — they require special proxy/firewall configuration. Prefer polling and pagination.

## Quick Reference: Naming Conventions

| Element      | Convention                      | Example                         |
|--------------|---------------------------------|---------------------------------|
| Package      | `lower_snake_case` with version | `acme.weather.v1`               |
| File         | `lower_snake_case.proto`        | `weather_service.proto`         |
| Message      | `PascalCase`                    | `WeatherReport`                 |
| Field        | `lower_snake_case`              | `temperature_celsius`           |
| Oneof        | `lower_snake_case`              | `delivery_method`               |
| Enum         | `PascalCase`                    | `WeatherCondition`              |
| Enum value   | `UPPER_SNAKE_CASE` with prefix  | `WEATHER_CONDITION_SUNNY`       |
| Enum zero    | Prefix + `_UNSPECIFIED`         | `WEATHER_CONDITION_UNSPECIFIED` |
| Service      | `PascalCase` + `Service` suffix | `WeatherService`                |
| RPC          | `PascalCase`                    | `GetCurrentWeather`             |
| RPC request  | `MethodNameRequest`             | `GetCurrentWeatherRequest`      |
| RPC response | `MethodNameResponse`            | `GetCurrentWeatherResponse`     |

## Version Suffixes

Valid package version patterns:

```
v1, v2, v3                   # Stable
v1alpha, v1alpha1, v1alpha2  # Alpha
v1beta, v1beta1, v1beta2     # Beta
v1p1alpha1                   # Point release alpha
v1test, v1testfoo            # Test
```

## Complete Example

```protobuf
// Weather forecasting API for the ACME platform.
// Provides real-time weather data and forecasts.
syntax = "proto3";

package acme.weather.v1;

import "google/protobuf/timestamp.proto";

option go_package = "acmev1";
option java_multiple_files = true;
option java_package = "com.acme.weather.v1";

// Geographic coordinates for a location.
message Location {
  // Latitude in decimal degrees.
  double latitude = 1;

  // Longitude in decimal degrees.
  double longitude = 2;
}

// Describes the current weather at a location.
message WeatherCondition {
  // Temperature in Celsius.
  double temperature_celsius = 1;

  // Relative humidity as a percentage (0-100).
  int32 humidity_percent = 2;

  // Human-readable weather description.
  string description = 3;

  // Time of the observation.
  google.protobuf.Timestamp observed_at = 4;
}

// Type of precipitation observed.
enum PrecipitationType {
  PRECIPITATION_TYPE_UNSPECIFIED = 0;
  PRECIPITATION_TYPE_NONE = 1;
  PRECIPITATION_TYPE_RAIN = 2;
  PRECIPITATION_TYPE_SNOW = 3;
  PRECIPITATION_TYPE_SLEET = 4;
}

// Request for current weather at a location.
message GetCurrentWeatherRequest {
  // The location to get weather for.
  Location location = 1;
}

// Response containing current weather data.
message GetCurrentWeatherResponse {
  // Current weather conditions.
  WeatherCondition condition = 1;

  // Type of precipitation, if any.
  PrecipitationType precipitation_type = 2;
}

// Provides weather data and forecasts.
service WeatherService {
  // Get current weather conditions for a location.
  rpc GetCurrentWeather(GetCurrentWeatherRequest) returns (GetCurrentWeatherResponse);
}
```
