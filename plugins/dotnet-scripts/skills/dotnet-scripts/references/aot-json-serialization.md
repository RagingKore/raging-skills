# Source-Generated JSON (AOT Compatibility)

Native AOT is on by default for file-based apps. Reflection-based `JsonSerializer.Serialize<T>(value)` fails at
runtime under AOT. Use source-generated serialization instead.

## Pattern

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

var person = new Person("Alice", 30);
var json = JsonSerializer.Serialize(person, AppJsonContext.Default.Person);
await Console.Out.WriteLineAsync(json);

record Person(string Name, int Age);

[JsonSerializable(typeof(Person))]
partial class AppJsonContext : JsonSerializerContext;
```

The `JsonSerializerContext` subclass generates serialization code at compile time, avoiding reflection entirely.

## Deserialization

```csharp
var json = """{"Name":"Alice","Age":30}""";
var person = JsonSerializer.Deserialize(json, AppJsonContext.Default.Person);
await Console.Out.WriteLineAsync($"{person!.Name} is {person.Age}");
```

The same context metadata that drives serialization also drives deserialization. Pass the matching
`JsonTypeInfo<T>` property from the context instead of using the generic `Deserialize<T>` overload.

## Multiple Types in One Context

Register every type the app needs in a single context class:

```csharp
[JsonSerializable(typeof(Person))]
[JsonSerializable(typeof(Address))]
[JsonSerializable(typeof(List<Person>))]
partial class AppJsonContext : JsonSerializerContext;
```

Each `[JsonSerializable]` attribute causes the source generator to emit a typed property on
`AppJsonContext.Default` (e.g., `AppJsonContext.Default.ListPerson`).

## Serializing Collections

Collections like `List<T>` need their own `[JsonSerializable]` entry. The generated property name
concatenates the collection and element type names.

```csharp
var people = new List<Person>
{
    new("Alice", 30),
    new("Bob", 25)
};

var json = JsonSerializer.Serialize(people, AppJsonContext.Default.ListPerson);
var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListPerson);
```

## Common Error Without Source Generation

When a type is missing from the context (or no context is used at all), AOT builds throw:

```text
System.InvalidOperationException:
Metadata for type 'Person' was not provided to the serializer.
```

If you see this message, add a `[JsonSerializable(typeof(...))]` attribute for the failing type
to your `JsonSerializerContext` subclass and pass the matching context property to the
serialize/deserialize call.

## When to Use

- Any file-based app that serializes or deserializes JSON
- Required when `PublishAot=true` (the default)
- Can be skipped if you set `#:property PublishAot=false`
