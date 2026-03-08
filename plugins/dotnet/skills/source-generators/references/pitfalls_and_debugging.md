# Source Generator Pitfalls and Debugging

## Table of Contents

- [Critical Pitfalls](#critical-pitfalls)
- [Performance Pitfalls](#performance-pitfalls)
- [Packaging Pitfalls](#packaging-pitfalls)
- [IDE Integration Pitfalls](#ide-integration-pitfalls)
- [Debugging Techniques](#debugging-techniques)
- [Diagnostic Reporting](#diagnostic-reporting)
- [Troubleshooting Checklist](#troubleshooting-checklist)

---

## Critical Pitfalls

### 1. Passing Roslyn Types Downstream (Cache-Killer)

**The single most common and damaging mistake.**

Roslyn types (`ISymbol`, `ITypeSymbol`, `SyntaxNode`, `Compilation`, `SemanticModel`) use **reference equality**. They are always different between compilations, which means:

- Every keystroke creates new instances
- Equality checks always return `false`
- All downstream nodes re-execute on every edit
- IDE becomes sluggish or unresponsive

```csharp
// BAD: INamedTypeSymbol passed downstream
var bad = context.SyntaxProvider.ForAttributeWithMetadataName(
    "MyAttr",
    predicate: static (n, _) => n is ClassDeclarationSyntax,
    transform: static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol  // WRONG
);

// GOOD: Extract into equatable model
var good = context.SyntaxProvider.ForAttributeWithMetadataName(
    "MyAttr",
    predicate: static (n, _) => n is ClassDeclarationSyntax,
    transform: static (ctx, _) => {
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
        return new TypeModel(
            symbol.Name,
            symbol.ContainingNamespace.ToDisplayString(),
            symbol.DeclaredAccessibility.ToString()
        );
    }
);
```

### 2. Storing State on the Generator Class

The compiler controls generator lifetime. The same instance may or may not be reused across compilations. Never store mutable state:

```csharp
// BAD: Mutable state on the class
[Generator]
public class BrokenGenerator : IIncrementalGenerator {
    private readonly List<string> _processedTypes = new();  // WRONG

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        // _processedTypes persists across compilations unpredictably
    }
}

// GOOD: All state flows through the pipeline
[Generator]
public sealed class CorrectGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        // All data flows through the pipeline - no class-level state
    }
}
```

### 3. Throwing Exceptions

Unhandled exceptions in a generator **crash the compiler**. The user sees a build failure with an unhelpful stack trace. Always catch and emit diagnostics:

```csharp
transform: static (ctx, ct) => {
    try {
        return ExtractModel(ctx);
    } catch (Exception ex) {
        // Return a sentinel value; report diagnostic in output
        return TypeModel.Error(ex.Message);
    }
}

// In RegisterSourceOutput:
context.RegisterSourceOutput(provider, static (spc, model) => {
    if (model.HasError) {
        spc.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.InternalError,
            Location.None,
            model.ErrorMessage));
        return;
    }
    // Normal generation
});
```

### 4. Non-Deterministic Output

The compiler may run the generator in any order. If your output depends on iteration order of `HashSet`, `Dictionary`, or unordered LINQ queries, the generated code can change between runs, causing unnecessary rebuilds:

```csharp
// BAD: Dictionary iteration order is non-deterministic
foreach (var kvp in symbolDictionary) {
    sb.AppendLine($"case \"{kvp.Key}\": return {kvp.Value};");
}

// GOOD: Sort explicitly
foreach (var kvp in symbolDictionary.OrderBy(x => x.Key, StringComparer.Ordinal)) {
    sb.AppendLine($"case \"{kvp.Key}\": return {kvp.Value};");
}
```

### 5. Missing `[Generator]` Attribute

Without `[Generator]`, the compiler won't discover your generator. It will silently do nothing:

```csharp
// BAD: Missing attribute
public sealed class MyGenerator : IIncrementalGenerator { ... }

// GOOD:
[Generator]
public sealed class MyGenerator : IIncrementalGenerator { ... }
```

### 6. Wrong Target Framework

Generators **must** target `netstandard2.0`. Other targets will fail to load:

```xml
<!-- BAD -->
<TargetFramework>net9.0</TargetFramework>

<!-- GOOD -->
<TargetFramework>netstandard2.0</TargetFramework>
```

You can still use C# 14 features via `<LangVersion>latest</LangVersion>` because the C# version is independent of the target framework. You just can't use APIs that don't exist in `netstandard2.0` without polyfills.

### 7. Scanning for Indirect Attributes/Interfaces

`ForAttributeWithMetadataName` only finds **directly applied** attributes. It does NOT find:
- Attributes inherited from a base class
- Attributes on an implemented interface
- Attributes applied to a base type's members

```csharp
// This ONLY finds classes directly annotated with [MyAttr]
// It does NOT find subclasses of a class annotated with [MyAttr]
var provider = context.SyntaxProvider.ForAttributeWithMetadataName(
    "MyLib.MyAttrAttribute", ...);
```

If you need inheritance scanning, use `CreateSyntaxProvider` with semantic analysis in the transform.

### 8. Forgetting CancellationToken

The compiler can cancel generation at any point (user edits mid-compilation). Always pass and check `CancellationToken`:

```csharp
transform: static (ctx, ct) => {
    ct.ThrowIfCancellationRequested();
    var members = GetAllMembers(ctx.TargetSymbol, ct);
    ct.ThrowIfCancellationRequested();
    return new TypeModel(/* ... */);
}
```

## Performance Pitfalls

### Heavy Work in Predicate

The `predicate` in `CreateSyntaxProvider` runs on EVERY syntax node change. It must be trivially fast:

```csharp
// BAD: Semantic analysis in predicate
predicate: static (node, _) => {
    if (node is ClassDeclarationSyntax cls) {
        // This accesses semantic model - SLOW
        return cls.AttributeLists.Any(a => a.ToString().Contains("MyAttr"));
    }
    return false;
}

// GOOD: Pure syntactic check
predicate: static (node, _) => node is ClassDeclarationSyntax {
    AttributeLists.Count: > 0
}
```

### Unnecessary Collect()

`Collect()` batches everything into a single node. If one item changes, the entire batch re-runs:

```csharp
// BAD: Collect forces re-generation of ALL types when ANY changes
var all = provider.Collect();
context.RegisterSourceOutput(all, (spc, types) => {
    foreach (var t in types) {
        spc.AddSource($"{t.Name}.g.cs", Generate(t));
    }
});

// GOOD: Generate per-item (each type is independent)
context.RegisterSourceOutput(provider, (spc, type) => {
    spc.AddSource($"{type.Name}.g.cs", Generate(type));
});
```

Only use `Collect()` when you genuinely need all items together (e.g., generating a registry of all types).

### Accessing CompilationProvider Unnecessarily

The `Compilation` changes on every edit. Combining with it defeats caching:

```csharp
// BAD: Compilation changes on every keystroke
var combined = provider.Combine(context.CompilationProvider);

// GOOD: Extract what you need from the compilation once
var assemblyName = context.CompilationProvider.Select(
    static (c, _) => c.AssemblyName);
var combined = provider.Combine(assemblyName);
```

## Packaging Pitfalls

See [packaging_strategies.md](packaging_strategies.md) for detailed coverage. Quick summary:

| Mistake                                   | Symptom                       | Fix                                                        |
|-------------------------------------------|-------------------------------|------------------------------------------------------------|
| DLL in `lib/` not `analyzers/`            | Generator doesn't run         | `IncludeBuildOutput=false` + pack to `analyzers/dotnet/cs` |
| Dependencies leak                         | Consumer sees Roslyn deps     | `PrivateAssets="all"` + `SuppressDependenciesWhenPacking`  |
| Missing `ReferenceOutputAssembly="false"` | Generator becomes runtime dep | Add to `ProjectReference`                                  |
| Roslyn version too new                    | Generator silently fails      | Target lowest needed Roslyn version                        |

## IDE Integration Pitfalls

### Generator Output Not Visible

**Symptom:** IntelliSense doesn't show generated types.
**Fixes:**
1. Restart Visual Studio / Rider
2. Check generator actually runs: build and check `obj/` folder
3. Ensure `RegisterSourceOutput` (not `RegisterImplementationSourceOutput`) for IDE-visible code
4. Verify the generated source compiles without errors

### Slow IDE Performance

**Symptom:** Typing lag, high CPU usage.
**Cause:** Generator re-runs too frequently (caching broken).
**Fix:** Profile with generator driver tracing, fix equality on model types.

### Generated Files Not Updating

**Symptom:** Old generated code persists after changes.
**Fixes:**
1. Clean and rebuild
2. Delete `obj/` folder
3. Restart IDE
4. Check `hintName` uniqueness - duplicate names silently overwrite

## Debugging Techniques

### 1. Debugger.Launch()

Add `Debugger.Launch()` to attach the debugger when the compiler process starts:

```csharp
public void Initialize(IncrementalGeneratorInitializationContext context) {
#if DEBUG
    if (!System.Diagnostics.Debugger.IsAttached) {
        System.Diagnostics.Debugger.Launch();
    }
#endif
    // ... pipeline setup
}
```

Build the consuming project. A dialog will prompt you to attach a debugger. **Remove before shipping.**

### 2. EmitCompilerGeneratedFiles

Write generated files to disk for inspection:

```xml
<!-- In the CONSUMING project's .csproj -->
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\GeneratedFiles</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Generated files appear in `obj/GeneratedFiles/`.

### 3. Logging via Additional Files

Write debug info to an additional generated source:

```csharp
#if DEBUG
context.RegisterSourceOutput(provider.Collect(), static (spc, items) => {
    var log = new StringBuilder("// Generator Debug Log\n");
    foreach (var item in items) {
        log.AppendLine($"// Processed: {item.Name} in {item.Namespace}");
    }
    spc.AddSource("DebugLog.g.cs", log.ToString());
});
#endif
```

### 4. Unit Tests (Best Approach)

The most reliable debugging method. Write unit tests that create a compilation, run your generator, and inspect the output. You can set breakpoints in the generator code and step through.

### 5. Generator Driver Tracing

Set the environment variable before building:

```bash
export DOTNET_COMPILER_PERF_LOG=1
dotnet build
```

This logs generator timing and step execution to help identify performance issues.

## Diagnostic Reporting

### Defining Diagnostics

```csharp
public static class DiagnosticDescriptors {
    public static readonly DiagnosticDescriptor MustBePartial = new(
        id: "MG001",
        title: "Type must be partial",
        messageFormat: "Type '{0}' must be declared as partial to use [GenerateHello]",
        category: "MyGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NoPublicProperties = new(
        id: "MG002",
        title: "No public properties found",
        messageFormat: "Type '{0}' has no public properties to generate code for",
        category: "MyGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );
}
```

### Reporting Diagnostics

```csharp
context.RegisterSourceOutput(provider, static (spc, model) => {
    if (!model.IsPartial) {
        spc.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.MustBePartial,
            model.Location,       // Use Location from model
            model.Name));
        return; // Don't generate invalid code
    }
    // Normal generation
});
```

### Preserving Location Information

Store location data in your model for accurate diagnostic positioning:

```csharp
public readonly record struct TypeModel(
    string Name,
    string Namespace,
    bool IsPartial,
    Location? Location  // Preserve for diagnostics
);

// In transform:
transform: static (ctx, _) => new TypeModel(
    ctx.TargetSymbol.Name,
    ctx.TargetSymbol.ContainingNamespace.ToDisplayString(),
    ctx.TargetNode is TypeDeclarationSyntax { Modifiers: var mods }
        && mods.Any(SyntaxKind.PartialKeyword),
    ctx.TargetSymbol.Locations.FirstOrDefault()
)
```

**Note:** `Location` objects from Roslyn ARE safe to store in models (they are value-comparable). They are an exception to the "no Roslyn types" rule.

## Troubleshooting Checklist

When your generator doesn't work:

- [ ] `[Generator]` attribute present on the class?
- [ ] Implements `IIncrementalGenerator` (not deprecated `ISourceGenerator`)?
- [ ] Targets `netstandard2.0`?
- [ ] `EnforceExtendedAnalyzerRules` set to `true`?
- [ ] `IsRoslynComponent` set to `true`?
- [ ] `Microsoft.CodeAnalysis.Analyzers` referenced?
- [ ] Consumer project references with `OutputItemType="Analyzer"`?
- [ ] `ReferenceOutputAssembly="false"` on the project reference?
- [ ] Fully qualified attribute name correct (namespace + "Attribute" suffix)?
- [ ] `RegisterPostInitializationOutput` used for marker attributes?
- [ ] Generated source has unique `hintName`?
- [ ] No unhandled exceptions in transform/output?
- [ ] Build log shows generator execution?
- [ ] Clean build attempted?
