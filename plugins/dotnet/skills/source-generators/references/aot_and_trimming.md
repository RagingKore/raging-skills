# Source Generators for AOT and Trimming

## Table of Contents

- [Why Generators Enable AOT](#why-generators-enable-aot)
- [The AOT Problem: Runtime Code Generation](#the-aot-problem-runtime-code-generation)
- [Replacing Reflection Patterns](#replacing-reflection-patterns)
- [Framework Source Generators](#framework-source-generators)
- [Trim Annotations for Generator Authors](#trim-annotations-for-generator-authors)
- [Making a Library AOT-Safe with Generators](#making-a-library-aot-safe-with-generators)
- [MSBuild Configuration](#msbuild-configuration)
- [Testing AOT Compatibility](#testing-aot-compatibility)

---

## Why Generators Enable AOT

Native AOT compiles your application **ahead of time** directly to machine code. This means:

1. **No JIT compiler** at runtime
2. **No `System.Reflection.Emit`** (can't generate IL dynamically)
3. **IL trimming** removes unreferenced code to reduce binary size
4. **Trimmer uses static analysis** - it can't see what reflection accesses at runtime

Source generators solve all of these by moving code generation to compile time. The generated code is plain C# - fully visible to the trimmer, fully compilable by AOT.

## The AOT Problem: Runtime Code Generation

These patterns **break** under AOT/trimming:

| Pattern                                           | Why It Breaks                     |
|---------------------------------------------------|-----------------------------------|
| `Activator.CreateInstance(type)`                  | Type might be trimmed away        |
| `Type.GetMembers()` via reflection                | Members might be trimmed          |
| `DynamicMethod` / `Reflection.Emit`               | No runtime IL generation in AOT   |
| `Assembly.LoadFrom()`                             | No dynamic assembly loading       |
| `JsonSerializer.Serialize<T>()` (reflection mode) | Property accessors use reflection |
| `DllImport` P/Invoke                              | IL stub generated at runtime      |
| `RegexOptions.Compiled`                           | Uses Reflection.Emit              |

## Replacing Reflection Patterns

### Serialization: System.Text.Json Source Generator

**Before (reflection, not AOT-safe):**
```csharp
var json = JsonSerializer.Serialize(myObj);
```

**After (source-generated, AOT-safe):**
```csharp
[JsonSerializable(typeof(WeatherForecast))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class AppJsonContext : JsonSerializerContext { }

// Usage
var json = JsonSerializer.Serialize(forecast, AppJsonContext.Default.WeatherForecast);
```

### P/Invoke: LibraryImport Source Generator

**Before (runtime IL stub, not AOT-safe):**
```csharp
[DllImport("nativelib", EntryPoint = "to_lower", CharSet = CharSet.Unicode)]
internal static extern string ToLower(string str);
```

**After (source-generated marshalling, AOT-safe):**
```csharp
[LibraryImport("nativelib", EntryPoint = "to_lower", StringMarshalling = StringMarshalling.Utf16)]
internal static partial string ToLower(string str);
```

### Regex: GeneratedRegex Source Generator

**Before (Reflection.Emit, not AOT-safe):**
```csharp
private static readonly Regex _pattern = new("abc|def", RegexOptions.Compiled | RegexOptions.IgnoreCase);
```

**After (source-generated, AOT-safe):**
```csharp
[GeneratedRegex("abc|def", RegexOptions.IgnoreCase)]
private static partial Regex AbcOrDefRegex();
```

### COM Interop: GeneratedComInterface

**Before (runtime COM interop, not AOT-safe):**
```csharp
[ComImport]
[Guid("...")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMyComInterface {
    void DoWork();
}
```

**After (source-generated, AOT-safe):**
```csharp
[GeneratedComInterface]
[Guid("...")]
partial interface IMyComInterface {
    void DoWork();
}
```

### Configuration Binding: Configuration Source Generator

**Before (reflection-based, not AOT-safe):**
```csharp
builder.Services.Configure<MySettings>(configuration.GetSection("MySettings"));
```

**After (.NET 8+, source-generated binding):**
The configuration binder source generator is automatically enabled when `PublishAot=true` or trimming is enabled. It intercepts `Bind`, `Get`, and `Configure` calls.

### Factory Patterns: Custom Generator

**Before (reflection, not AOT-safe):**
```csharp
public T Create<T>() where T : new() => Activator.CreateInstance<T>();
```

**After (generated factory):**
```csharp
// Your generator emits:
public static class Factory {
    public static MyService CreateMyService() => new MyService();
    public static MyRepo CreateMyRepo() => new MyRepo();
}
```

### DI Registration: Custom Generator

**Before (assembly scanning, not AOT-safe):**
```csharp
// Scans assembly via reflection
services.AddServicesFromAssembly(typeof(Program).Assembly);
```

**After (generated registration):**
```csharp
// Generator scans at compile time and emits:
public static class GeneratedServiceRegistration {
    public static IServiceCollection AddAppServices(this IServiceCollection services) {
        services.AddTransient<IUserService, UserService>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        return services;
    }
}
```

## Framework Source Generators

The .NET runtime ships these generators, all AOT-safe:

| Generator          | Attribute                 | Replaces                                 |
|--------------------|---------------------------|------------------------------------------|
| JSON serialization | `[JsonSerializable]`      | Reflection-based JSON                    |
| Regex              | `[GeneratedRegex]`        | `RegexOptions.Compiled`                  |
| P/Invoke           | `[LibraryImport]`         | `[DllImport]`                            |
| COM interop        | `[GeneratedComInterface]` | `[ComImport]`                            |
| Logging            | `[LoggerMessage]`         | String interpolation in logging          |
| Config binding     | Automatic (interceptor)   | Reflection-based `Bind()`                |
| Request delegates  | Automatic (RDG)           | Runtime `Map*` delegates in Minimal APIs |

## Trim Annotations for Generator Authors

When writing a library that uses a source generator to be trim-safe, annotate the non-generated fallback paths:

### `[RequiresUnreferencedCode]` - Mark Trim-Incompatible Code

```csharp
[RequiresUnreferencedCode(
    "Use the source-generated overload instead. See https://docs.example.com/migration")]
public void RegisterAll(Assembly assembly) {
    // Reflection-based fallback
    foreach (var type in assembly.GetTypes()) { /* ... */ }
}

// Source-generated alternative (trim-safe, no annotation needed)
public void RegisterAll(Action<IServiceCollection> configure) {
    configure(services); // Generated code calls this
}
```

### `[DynamicallyAccessedMembers]` - Preserve Specific Members

```csharp
public T Deserialize<[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.PublicConstructors |
    DynamicallyAccessedMemberTypes.PublicProperties)] T>(string json) {
    // Trimmer preserves constructors + properties on T
}
```

### `[UnconditionalSuppressMessage]` - Suppress When You Know Better

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2026",
    Justification = "Type is preserved via [DynamicDependency] on the calling method")]
private static object CreateInstance(Type type) {
    return Activator.CreateInstance(type)!;
}
```

## Making a Library AOT-Safe with Generators

### Step 1: Enable Analyzers

```xml
<PropertyGroup>
  <IsTrimmable>true</IsTrimmable>
  <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
  <EnableAotAnalyzer>true</EnableAotAnalyzer>
  <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```

### Step 2: Identify Reflection Usage

Build with analyzers enabled. Fix every `IL2026`, `IL2046`, `IL2057`-`IL2091` warning:

- Replace `Type.GetMembers()` with generated member access
- Replace `Activator.CreateInstance` with generated factory
- Replace `Assembly.GetTypes()` with generated type list

### Step 3: Provide Generated Alternative

Ship a source generator that produces trim-safe code:

```csharp
// Generator emits this for each [MySerializable] class:
public static class MyClassSerializer {
    public static void Serialize(MyClass obj, Utf8JsonWriter writer) {
        writer.WriteStartObject();
        writer.WriteString("Name", obj.Name);
        writer.WriteNumber("Age", obj.Age);
        writer.WriteEndObject();
    }
}
```

### Step 4: Gate the Reflection Fallback

```csharp
#if !DISABLE_REFLECTION_FALLBACK
[RequiresUnreferencedCode("Use source generation for trim-safe serialization")]
public static string Serialize<T>(T obj) {
    // Reflection-based fallback for non-AOT scenarios
}
#endif
```

## MSBuild Configuration

### Consumer-Side AOT Setup

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <!-- Optional: see generated source -->
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\GeneratedFiles</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

### Library-Side Trim Compatibility

```xml
<PropertyGroup>
  <IsTrimmable>true</IsTrimmable>
  <IsAotCompatible>true</IsAotCompatible>
  <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
  <EnableAotAnalyzer>true</EnableAotAnalyzer>
  <EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>
</PropertyGroup>
```

### Optimization Preferences

```xml
<!-- Optimize for binary size -->
<OptimizationPreference>Size</OptimizationPreference>

<!-- Optimize for throughput -->
<OptimizationPreference>Speed</OptimizationPreference>
```

## Testing AOT Compatibility

### Publish Test

```bash
dotnet publish -c Release -r linux-x64
```

Watch for trim/AOT warnings during publish. Any `IL2xxx` or `IL3xxx` warning indicates potential runtime failure.

### Verify No Warnings

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <!-- Or specifically: -->
  <WarningsAsErrors>IL2026;IL2046;IL2057;IL2070;IL2072;IL2075;IL2077</WarningsAsErrors>
</PropertyGroup>
```

### Integration Test Pattern

```csharp
// Verify generated code works identically to reflection fallback
[Fact]
public void SerializeProducesIdenticalOutput() {
    var obj = new MyClass { Name = "Test", Age = 42 };

    var generatedJson = GeneratedSerializer.Serialize(obj);
    var reflectionJson = JsonSerializer.Serialize(obj);

    Assert.Equal(reflectionJson, generatedJson);
}
```
