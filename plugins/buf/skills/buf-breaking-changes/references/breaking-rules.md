# Buf Breaking Change Rules — Complete Reference

## Categories Overview

| Category | Scope | Use When |
|----------|-------|----------|
| `FILE` | Generated source code per-file | Sharing protos with external consumers |
| `PACKAGE` | Generated source code per-package | Go or similar package-based languages |
| `WIRE_JSON` | Binary + JSON encoding | Using any JSON encoding (Connect, gRPC-Gateway) |
| `WIRE` | Binary encoding only | Binary-only, full control of clients |

## Deletion Rules

### ENUM_NO_DELETE
**Category:** FILE

No enum can be deleted from a file. Deprecate instead:
```protobuf
enum Foo {
  option deprecated = true;
  FOO_UNSPECIFIED = 0;
}
```

### ENUM_VALUE_NO_DELETE
**Categories:** FILE, PACKAGE

No enum value can be deleted. Deprecate instead:
```protobuf
enum Foo {
  FOO_UNSPECIFIED = 0;
  FOO_ONE = 1 [deprecated = true];
}
```

### ENUM_VALUE_NO_DELETE_UNLESS_NAME_RESERVED
**Category:** WIRE_JSON

Enum values can only be deleted if the name is reserved.

### ENUM_VALUE_NO_DELETE_UNLESS_NUMBER_RESERVED
**Categories:** WIRE, WIRE_JSON

Enum values can only be deleted if the number is reserved:
```protobuf
enum Foo {
  reserved 1;
  reserved "FOO_ONE";
  FOO_UNSPECIFIED = 0;
}
```

### EXTENSION_MESSAGE_NO_DELETE
**Categories:** FILE, PACKAGE

No extension range can be deleted from a message.

### EXTENSION_NO_DELETE (v2 only)
**Category:** FILE

No extension can be deleted from a file. Deprecate instead.

### FIELD_NO_DELETE
**Categories:** FILE, PACKAGE

No message field can be deleted. Deprecate instead:
```protobuf
message Bar {
  string one = 1 [deprecated = true];
}
```

### FIELD_NO_DELETE_UNLESS_NAME_RESERVED
**Category:** WIRE_JSON

Fields can only be deleted if the name is reserved (JSON uses names).

### FIELD_NO_DELETE_UNLESS_NUMBER_RESERVED
**Categories:** WIRE, WIRE_JSON

Fields can only be deleted if the number is reserved:
```protobuf
message Bar {
  reserved 1;
  reserved "one";
}
```

### MESSAGE_NO_DELETE
**Category:** FILE

No message can be deleted from a file.

### ONEOF_NO_DELETE
**Categories:** FILE, PACKAGE

No oneof can be deleted.

### RPC_NO_DELETE
**Categories:** FILE, PACKAGE

No RPC can be deleted from a service.

### SERVICE_NO_DELETE
**Category:** FILE

No service can be deleted from a file.

## Sameness Rules

### ENUM_SAME_JSON_FORMAT
**Categories:** FILE, PACKAGE, WIRE_JSON

An enum can't change from supporting JSON format to "best effort" (e.g., moving from proto3 to proto2).

### ENUM_SAME_TYPE
**Categories:** FILE, PACKAGE

An enum can't change from open to closed or vice versa.

### ENUM_VALUE_SAME_NAME
**Categories:** FILE, PACKAGE, WIRE_JSON

An enum value can't change its name for a given number. For aliased enums, the set of names must be a superset.

### FIELD_SAME_CARDINALITY
**Categories:** FILE, PACKAGE, WIRE_JSON, WIRE

A field can't change cardinality (e.g., optional to repeated).

### FIELD_SAME_CTYPE
**Categories:** FILE, PACKAGE

A field's `ctype` option can't change.

### FIELD_SAME_DEFAULT
**Categories:** FILE, PACKAGE, WIRE_JSON, WIRE

A field's default value can't change.

### FIELD_SAME_JSON_NAME
**Categories:** FILE, PACKAGE, WIRE_JSON

A field's JSON name can't change.

### FIELD_SAME_JSTYPE
**Categories:** FILE, PACKAGE

A field's `jstype` option can't change.

### FIELD_SAME_NAME
**Categories:** FILE, PACKAGE

A field's name can't change for a given number.

### FIELD_SAME_ONEOF
**Categories:** FILE, PACKAGE, WIRE_JSON, WIRE

A field can't be moved into or out of a oneof.

### FIELD_SAME_TYPE
**Categories:** FILE, PACKAGE, WIRE_JSON, WIRE

A field's type can't change.

### FIELD_SAME_UTF8_VALIDATION
**Categories:** FILE, PACKAGE, WIRE_JSON, WIRE

A field's UTF-8 validation behavior can't change.

### FIELD_WIRE_COMPATIBLE_CARDINALITY
**Categories:** WIRE_JSON, WIRE

Wire-compatible cardinality changes are allowed (e.g., optional to repeated for scalar types).

### FIELD_WIRE_COMPATIBLE_TYPE
**Categories:** WIRE_JSON, WIRE

Wire-compatible type changes are allowed (e.g., int32 to int64).

### FIELD_WIRE_JSON_COMPATIBLE_CARDINALITY
**Category:** WIRE_JSON

JSON-compatible cardinality changes are allowed.

### FIELD_WIRE_JSON_COMPATIBLE_TYPE
**Category:** WIRE_JSON

JSON-compatible type changes are allowed.

### MESSAGE_NO_REMOVE_STANDARD_DESCRIPTOR_ACCESSOR
**Categories:** FILE, PACKAGE

Can't remove standard descriptor accessor from a message.

### MESSAGE_SAME_JSON_FORMAT
**Categories:** FILE, PACKAGE, WIRE_JSON

A message can't change JSON format support.

### MESSAGE_SAME_MESSAGE_SET_WIRE_FORMAT
**Categories:** FILE, PACKAGE, WIRE_JSON, WIRE

A message's message-set wire format setting can't change.

### MESSAGE_SAME_REQUIRED_FIELDS
**Categories:** FILE, PACKAGE, WIRE_JSON, WIRE

Can't add or remove required fields from a message.

### ONEOF_NO_DELETE
**Categories:** FILE, PACKAGE

A oneof can't be deleted.

### RESERVED_ENUM_NO_DELETE
**Categories:** FILE, PACKAGE, WIRE_JSON, WIRE

Reserved ranges/names can't be removed from enums.

### RESERVED_MESSAGE_NO_DELETE
**Categories:** FILE, PACKAGE, WIRE_JSON, WIRE

Reserved ranges/names can't be removed from messages.

## File Option Rules (FILE and PACKAGE only)

These rules check that file options don't change between versions:

- `FILE_SAME_CC_ENABLE_ARENAS`
- `FILE_SAME_CC_GENERIC_SERVICES`
- `FILE_SAME_CSHARP_NAMESPACE`
- `FILE_SAME_GO_PACKAGE`
- `FILE_SAME_JAVA_MULTIPLE_FILES`
- `FILE_SAME_JAVA_OUTER_CLASSNAME`
- `FILE_SAME_JAVA_PACKAGE`
- `FILE_SAME_JAVA_STRING_CHECK_UTF8`
- `FILE_SAME_OBJC_CLASS_PREFIX`
- `FILE_SAME_OPTIMIZE_FOR`
- `FILE_SAME_PHP_CLASS_PREFIX`
- `FILE_SAME_PHP_METADATA_NAMESPACE`
- `FILE_SAME_PHP_NAMESPACE`
- `FILE_SAME_RUBY_PACKAGE`
- `FILE_SAME_SWIFT_PREFIX`
- `FILE_SAME_SYNTAX`

All are in **FILE** and **PACKAGE** categories.
