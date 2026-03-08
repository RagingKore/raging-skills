# Incremental Generator Pipeline - Deep Dive

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Provider Types](#provider-types)
- [Transformation Operators](#transformation-operators)
- [Caching and Equality](#caching-and-equality)
- [EquatableArray Pattern](#equatablearray-pattern)
- [Pipeline Composition Patterns](#pipeline-composition-patterns)
- [Performance Guidelines](#performance-guidelines)

---

## Architecture Overview

The incremental generator pipeline is a **directed acyclic graph (DAG)** of transformations. Each node:

1. Receives input from upstream nodes
2. Applies a transformation
3. Caches the output using structural equality
4. Only re-executes when the cached input differs from the new input

This design means the pipeline behaves like LINQ with deferred execution: transformations are defined once in `Initialize()` and the compiler re-evaluates the graph on every keystroke/edit, but actual computation only runs for changed inputs.

```
┌──────────────────┐     ┌──────────┐     ┌──────────────────────┐
│ SyntaxProvider   │────>│ Select() │────>│ RegisterSourceOutput │
│ .ForAttribute... │     │ (model)  │     │ (emit .g.cs)         │
└──────────────────┘     └──────────┘     └──────────────────────┘
                              │
                    ┌─────────┴───────────┐
                    │ Equality check:     │
                    │ if model == cached  │
                    │   → skip downstream │
                    └─────────────────────┘
```

## Provider Types

### Built-in Providers

```csharp
// All available from IncrementalGeneratorInitializationContext
public void Initialize(IncrementalGeneratorInitializationContext context) {
    // Syntax-based providers (most common)
    IncrementalValuesProvider<T> syntaxBased = context.SyntaxProvider.CreateSyntaxProvider(...);
    IncrementalValuesProvider<T> attrBased = context.SyntaxProvider.ForAttributeWithMetadataName(...);

    // Compilation (use sparingly - defeats caching if passed downstream)
    IncrementalValueProvider<Compilation> compilation = context.CompilationProvider;

    // Additional files (files added via <AdditionalFiles> in .csproj)
    IncrementalValuesProvider<AdditionalText> texts = context.AdditionalTextsProvider;

    // MSBuild properties and .editorconfig
    IncrementalValueProvider<AnalyzerConfigOptionsProvider> options = context.AnalyzerConfigOptionsProvider;

    // Referenced assemblies
    IncrementalValuesProvider<MetadataReference> refs = context.MetadataReferencesProvider;

    // Parse options (language version, preprocessor symbols)
    IncrementalValueProvider<ParseOptions> parseOpts = context.ParseOptionsProvider;
}
```

### Singular vs. Plural Providers

- **`IncrementalValueProvider<T>`** - Yields exactly ONE value (e.g., `CompilationProvider`, `ParseOptionsProvider`)
- **`IncrementalValuesProvider<T>`** - Yields MANY values (e.g., `AdditionalTextsProvider`, results from `ForAttributeWithMetadataName`)

This distinction matters for `Combine()`:

```csharp
// Singular + Singular → Singular tuple
IncrementalValueProvider<(Compilation, ParseOptions)> pair =
    context.CompilationProvider.Combine(context.ParseOptionsProvider);

// Plural + Singular → Plural tuples (each item combined with the single value)
IncrementalValuesProvider<(AdditionalText, ParseOptions)> pairs =
    context.AdditionalTextsProvider.Combine(context.ParseOptionsProvider);
```

### ForAttributeWithMetadataName (Preferred)

The **fastest** way to find attributed types. Uses compiler-internal attribute indices instead of walking the syntax tree:

```csharp
var provider = context.SyntaxProvider.ForAttributeWithMetadataName(
    fullyQualifiedMetadataName: "MyNamespace.MyAttribute",
    predicate: static (node, _) => node is ClassDeclarationSyntax,
    transform: static (ctx, _) => {
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
        return new TypeModel(
            Name: symbol.Name,
            Namespace: symbol.ContainingNamespace.ToDisplayString(),
            Properties: ExtractProperties(symbol)
        );
    }
);
```

**`GeneratorAttributeSyntaxContext` members:**

| Member          | Type                            | Description                            |
|-----------------|---------------------------------|----------------------------------------|
| `TargetNode`    | `SyntaxNode`                    | The syntax node with the attribute     |
| `TargetSymbol`  | `ISymbol`                       | The symbol for the target node         |
| `SemanticModel` | `SemanticModel`                 | Semantic model for the containing file |
| `Attributes`    | `ImmutableArray<AttributeData>` | Matching attribute instances           |

### CreateSyntaxProvider (General Purpose)

More flexible but slower. Use when you need to match patterns other than attributes:

```csharp
var provider = context.SyntaxProvider.CreateSyntaxProvider(
    predicate: static (node, _) => node is InvocationExpressionSyntax {
        Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "MapGet" }
    },
    transform: static (ctx, _) => {
        var invocation = (InvocationExpressionSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetSymbolInfo(invocation).Symbol;
        return ExtractRouteInfo(invocation, symbol);
    }
);
```

**Critical rule:** The `predicate` runs on EVERY syntax node change and must be fast. Do only syntactic checks here. Move semantic analysis to `transform`.

## Transformation Operators

### Select (1:1 Transform)

```csharp
IncrementalValuesProvider<string> names = provider.Select(
    static (model, _) => model.Name
);
```

### Where (Filter)

```csharp
IncrementalValuesProvider<TypeModel> publicTypes = provider.Where(
    static model => model.IsPublic
);
```

### SelectMany (1:N Expansion)

```csharp
IncrementalValuesProvider<PropertyModel> allProperties = provider.SelectMany(
    static (model, _) => model.Properties
);
```

### Collect (Batch into ImmutableArray)

Converts `IncrementalValuesProvider<T>` to `IncrementalValueProvider<ImmutableArray<T>>`:

```csharp
IncrementalValueProvider<ImmutableArray<TypeModel>> allTypes = provider.Collect();

context.RegisterSourceOutput(allTypes, static (spc, types) => {
    // Generate a single file referencing all collected types
    var sb = new StringBuilder();
    foreach (var type in types) {
        sb.AppendLine($"typeof({type.Namespace}.{type.Name}),");
    }
    spc.AddSource("AllTypes.g.cs", sb.ToString());
});
```

**Warning:** `Collect()` creates a single downstream node that depends on ALL items. If any item changes, the entire downstream re-runs. Use judiciously.

### Combine (Pair with Another Provider)

```csharp
// Combine plural with singular: each TypeModel paired with options
var combined = provider.Combine(context.AnalyzerConfigOptionsProvider);

context.RegisterSourceOutput(combined, static (spc, pair) => {
    var (model, options) = pair;
    options.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNs);
    // Generate using both model and options
});
```

## Caching and Equality

### How Caching Works

Every node in the pipeline stores its previous output. When re-executed:

1. The node computes a new output
2. It compares the new output to the cached output using **structural equality**
3. If equal: downstream nodes are **not** re-executed (cache hit)
4. If different: the new output replaces the cache and downstream nodes re-execute

### Why Model Objects Matter

Roslyn types (`ISymbol`, `SyntaxNode`, `Compilation`) use **reference equality**. They are always "different" between compilations, defeating caching entirely. This is why you must extract data into plain model objects:

```csharp
// BAD: ISymbol breaks caching - downstream ALWAYS re-runs
var bad = context.SyntaxProvider.ForAttributeWithMetadataName(
    "MyAttr",
    predicate: static (n, _) => true,
    transform: static (ctx, _) => ctx.TargetSymbol  // Returns ISymbol
);

// GOOD: Record struct has value equality - caching works
var good = context.SyntaxProvider.ForAttributeWithMetadataName(
    "MyAttr",
    predicate: static (n, _) => true,
    transform: static (ctx, _) => new TypeModel(
        ctx.TargetSymbol.Name,
        ctx.TargetSymbol.ContainingNamespace.ToDisplayString()
    )
);
```

### Equality Requirements

Your model types MUST implement correct equality. Options:

1. **`record struct`** - Free value equality for all fields (recommended)
2. **`record class`** - Free value equality, but adds allocation
3. **Manual `IEquatable<T>`** - Full control, use for complex scenarios

```csharp
// Preferred: record struct
public readonly record struct PropertyModel(
    string Name,
    string TypeName,
    bool IsReadOnly
);

// For collections, use EquatableArray<T>
public readonly record struct TypeModel(
    string Name,
    string Namespace,
    EquatableArray<PropertyModel> Properties
);
```

## EquatableArray Pattern

`ImmutableArray<T>` does NOT implement structural equality. You need a wrapper:

```csharp
using System.Collections;
using System.Collections.Immutable;

/// <summary>
/// An immutable array wrapper with structural equality semantics.
/// </summary>
public readonly struct EquatableArray<T>(ImmutableArray<T> array)
    : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T> {

    public ImmutableArray<T> Array { get; } = array;

    public int Length => Array.Length;
    public T this[int index] => Array[index];

    public bool Equals(EquatableArray<T> other) =>
        Array.AsSpan().SequenceEqual(other.Array.AsSpan());

    public override bool Equals(object? obj) =>
        obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode() {
        var hash = new HashCode();
        foreach (var item in Array) {
            hash.Add(item);
        }
        return hash.ToHashCode();
    }

    public ImmutableArray<T>.Enumerator GetEnumerator() => Array.GetEnumerator();
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)Array).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Array).GetEnumerator();

    public static implicit operator EquatableArray<T>(ImmutableArray<T> array) => new(array);
    public static implicit operator ImmutableArray<T>(EquatableArray<T> array) => array.Array;
}
```

## Pipeline Composition Patterns

### Pattern: Attribute + Global Options

```csharp
public void Initialize(IncrementalGeneratorInitializationContext context) {
    var types = context.SyntaxProvider.ForAttributeWithMetadataName(
        "MyLib.AutoSerializeAttribute",
        predicate: static (n, _) => n is ClassDeclarationSyntax or RecordDeclarationSyntax,
        transform: static (ctx, _) => ExtractModel(ctx)
    );

    var typesWithOptions = types.Combine(context.AnalyzerConfigOptionsProvider);

    context.RegisterSourceOutput(typesWithOptions, static (spc, pair) => {
        var (model, options) = pair;
        options.GlobalOptions.TryGetValue("build_property.MyGenerator_EmitDebugInfo", out var debug);
        EmitSerializer(spc, model, debug == "true");
    });
}
```

### Pattern: Collect All Then Generate Single File

```csharp
var allTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
    "MyLib.RegisterServiceAttribute",
    predicate: static (n, _) => n is ClassDeclarationSyntax,
    transform: static (ctx, _) => ExtractServiceModel(ctx)
).Collect();

context.RegisterSourceOutput(allTypes, static (spc, services) => {
    var sb = new StringBuilder("""
        // <auto-generated/>
        namespace MyLib.Generated;

        public static class ServiceRegistration {
            public static IServiceCollection AddGeneratedServices(this IServiceCollection services) {
        """);
    foreach (var svc in services) {
        sb.AppendLine($"        services.AddTransient<{svc.Interface}, {svc.Implementation}>();");
    }
    sb.AppendLine("        return services;");
    sb.AppendLine("    }");
    sb.AppendLine("}");
    spc.AddSource("ServiceRegistration.g.cs", sb.ToString());
});
```

### Pattern: Additional Files as Input

```csharp
var sqlFiles = context.AdditionalTextsProvider
    .Where(static file => file.Path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
    .Select(static (file, ct) => (
        Name: Path.GetFileNameWithoutExtension(file.Path),
        Content: file.GetText(ct)?.ToString() ?? ""
    ));

context.RegisterSourceOutput(sqlFiles, static (spc, sql) => {
    spc.AddSource($"{sql.Name}Query.g.cs", $$"""
        // <auto-generated/>
        namespace Queries;

        public static partial class {{sql.Name}}Query {
            public const string Sql = """
                {{sql.Content}}
                """;
        }
        """);
});
```

## Performance Guidelines

1. **Keep `predicate` fast** - Only do syntactic checks (node type, identifier name). Never access `SemanticModel` in the predicate.
2. **Extract minimal data in `transform`** - Only pull what you need from `ISymbol`. The less data, the more cache hits.
3. **Use `ForAttributeWithMetadataName` over `CreateSyntaxProvider`** - It's internally optimized with compiler attribute indices.
4. **Avoid `CompilationProvider` when possible** - It changes on every edit, defeating caching for all downstream nodes.
5. **Don't `Collect()` unless necessary** - It creates a bottleneck. Prefer per-item generation when possible.
6. **Make transforms `static`** - Prevents accidental closure over the generator instance.
7. **Use `RegisterImplementationSourceOutput` for build-only code** - Avoids IDE overhead for code that only matters during actual builds.
8. **Profile with `DOTNET_COMPILER_PERF_LOG`** - Set this environment variable to get generator timing information.
