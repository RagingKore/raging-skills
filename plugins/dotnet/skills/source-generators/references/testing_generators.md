# Testing Source Generators

## Table of Contents

- [Testing Approaches](#testing-approaches)
- [Unit Testing with CSharpGeneratorDriver](#unit-testing-with-csharpgeneratordriver)
- [Snapshot Testing](#snapshot-testing)
- [Testing Incremental Caching](#testing-incremental-caching)
- [Testing Diagnostics](#testing-diagnostics)
- [Test Project Setup](#test-project-setup)
- [Roslyn Test Infrastructure](#roslyn-test-infrastructure)
- [Best Practices](#best-practices)

---

## Testing Approaches

| Approach                         | When to Use                                                          |
|----------------------------------|----------------------------------------------------------------------|
| **CSharpGeneratorDriver** (unit) | Test generator logic in isolation. Fast, deterministic.              |
| **Snapshot testing**             | Verify generated output matches expected files. Good for regression. |
| **Incremental caching tests**    | Verify the pipeline only re-runs when inputs change.                 |
| **Diagnostic tests**             | Verify error/warning reporting for invalid input.                    |
| **Integration tests**            | Compile generated code + consumer code. Verify runtime behavior.     |

## Unit Testing with CSharpGeneratorDriver

The primary approach: create a compilation, run the generator, inspect the output.

### Minimal Test (C# 14, K&R)

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MyGenerator.Tests;

public class HelloGeneratorTests {
    [Fact]
    public void GeneratesHelloExtension() {
        // Arrange: source code the generator will process
        var source = """
            using MyLib;

            namespace TestApp;

            [GenerateHello]
            public partial class Greeter { }
            """;

        // Act: run the generator
        var (output, diagnostics) = RunGenerator(source);

        // Assert: verify output
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains(output, o => o.HintName == "Greeter.Hello.g.cs");

        var generated = output.First(o => o.HintName == "Greeter.Hello.g.cs");
        var text = generated.SourceText.ToString();
        Assert.Contains("SayHello", text);
        Assert.Contains("partial class GreeterExtensions", text);
    }

    private static (
        ImmutableArray<GeneratedSourceResult> Output,
        ImmutableArray<Diagnostic> Diagnostics
    ) RunGenerator(string source) {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new HelloGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics
        );

        var runResult = driver.GetRunResult();
        return (runResult.GeneratedTrees.Length > 0
            ? runResult.Results[0].GeneratedSources
            : [],
            diagnostics
        );
    }
}
```

### Helper: Reusable Test Infrastructure

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MyGenerator.Tests;

public static class GeneratorTestHelper {
    public static GeneratorDriverRunResult RunGenerator<TGenerator>(
        string source,
        IEnumerable<MetadataReference>? additionalReferences = null
    ) where TGenerator : IIncrementalGenerator, new() {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference> {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        // Add System.Runtime for [AttributeUsage] etc.
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(
            Path.Combine(runtimeDir, "System.Runtime.dll")));

        if (additionalReferences is not null) {
            references.AddRange(additionalReferences);
        }

        var compilation = CSharpCompilation.Create(
            "GeneratorTest",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new TGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out _,
            out _
        );

        return driver.GetRunResult();
    }

    public static string GetGeneratedSource(
        GeneratorDriverRunResult result,
        string hintName
    ) {
        var source = result.Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(s => s.HintName == hintName);

        return source.SourceText?.ToString()
            ?? throw new InvalidOperationException($"No generated source with hint name '{hintName}'");
    }
}
```

## Snapshot Testing

Use Verify (https://github.com/VerifyTests/Verify) for snapshot-based testing:

```csharp
using VerifyXunit;

namespace MyGenerator.Tests;

[UsesVerify]
public class SnapshotTests {
    [Fact]
    public Task GeneratedCodeMatchesSnapshot() {
        var source = """
            using MyLib;
            namespace TestApp;

            [GenerateHello]
            public partial class Greeter { }
            """;

        var result = GeneratorTestHelper.RunGenerator<HelloGenerator>(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result, "Greeter.Hello.g.cs");

        return Verify(generated);
    }
}
```

On first run, Verify creates a `.verified.txt` file with the generated output. Subsequent runs compare against it. If the output changes, the test fails and shows a diff.

### Verify Configuration for Generators

```csharp
// In ModuleInitializer.cs
using System.Runtime.CompilerServices;

public static class ModuleInit {
    [ModuleInitializer]
    public static void Init() {
        // Normalize line endings across OS
        VerifierSettings.ScrubLinesContaining("// <auto-generated");
        VerifierSettings.UseDirectory("Snapshots");
    }
}
```

## Testing Incremental Caching

Verify that the pipeline caches correctly - unchanged inputs should not cause re-generation:

```csharp
[Fact]
public void CachingWorks_UnchangedInputSkipsRegeneration() {
    var source = """
        using MyLib;
        namespace TestApp;

        [GenerateHello]
        public partial class Greeter { }
        """;

    var syntaxTree = CSharpSyntaxTree.ParseText(source);
    var compilation = CreateCompilation(syntaxTree);
    var generator = new HelloGenerator();

    GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

    // First run
    driver = driver.RunGeneratorsAndUpdateCompilation(
        compilation, out var comp1, out _);
    var result1 = driver.GetRunResult();

    // Second run with SAME compilation (simulates no-change edit)
    driver = driver.RunGeneratorsAndUpdateCompilation(
        comp1, out _, out _);
    var result2 = driver.GetRunResult();

    // Verify the output step was cached (not re-run)
    var step = result2.Results[0].TrackedOutputSteps
        .SelectMany(s => s.Value)
        .SelectMany(s => s.Outputs);

    Assert.All(step, output =>
        Assert.Equal(IncrementalStepRunReason.Cached, output.Reason));
}

[Fact]
public void CachingWorks_ChangedInputTriggersRegeneration() {
    var source1 = """
        using MyLib;
        namespace TestApp;
        [GenerateHello]
        public partial class Greeter { }
        """;

    var source2 = """
        using MyLib;
        namespace TestApp;
        [GenerateHello]
        public partial class Greeter2 { }
        """;

    var compilation1 = CreateCompilation(CSharpSyntaxTree.ParseText(source1));
    var generator = new HelloGenerator();
    GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

    driver = driver.RunGeneratorsAndUpdateCompilation(compilation1, out _, out _);

    // Change the source
    var compilation2 = CreateCompilation(CSharpSyntaxTree.ParseText(source2));
    driver = driver.RunGeneratorsAndUpdateCompilation(compilation2, out _, out _);
    var result = driver.GetRunResult();

    var step = result.Results[0].TrackedOutputSteps
        .SelectMany(s => s.Value)
        .SelectMany(s => s.Outputs);

    Assert.Contains(step, output =>
        output.Reason == IncrementalStepRunReason.Modified);
}
```

### IncrementalStepRunReason Values

| Reason      | Meaning                                      |
|-------------|----------------------------------------------|
| `New`       | First execution, no cached value             |
| `Modified`  | Input changed, output was recomputed         |
| `Cached`    | Input unchanged, cached output reused        |
| `Unchanged` | Recomputed but output is identical to cached |
| `Removed`   | Previously existed but no longer present     |

## Testing Diagnostics

```csharp
[Fact]
public void ReportsDiagnosticForNonPartialClass() {
    var source = """
        using MyLib;
        namespace TestApp;

        [GenerateHello]
        public class NotPartial { }   // Missing 'partial'
        """;

    var result = GeneratorTestHelper.RunGenerator<HelloGenerator>(source);
    var diagnostics = result.Diagnostics;

    var error = Assert.Single(diagnostics, d => d.Id == "MG001");
    Assert.Equal(DiagnosticSeverity.Error, error.Severity);
    Assert.Contains("must be partial", error.GetMessage());
}
```

## Test Project Setup

```xml
<!-- MyGenerator.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference the generator project directly (NOT as analyzer) -->
    <ProjectReference Include="..\MyGenerator\MyGenerator.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <!-- Optional: for snapshot testing -->
    <PackageReference Include="Verify.Xunit" Version="26.6.0" />
  </ItemGroup>
</Project>
```

**Important:** Reference the generator project as a normal `ProjectReference` (NOT with `OutputItemType="Analyzer"`). In tests, you instantiate the generator manually and pass it to `CSharpGeneratorDriver.Create()`.

## Roslyn Test Infrastructure

Microsoft provides testing utilities in `Microsoft.CodeAnalysis.CSharp.Testing`:

```xml
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.XUnit" Version="1.1.2" />
```

### Using CSharpSourceGeneratorVerifier

```csharp
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using VerifyCS = MyGenerator.Tests.CSharpSourceGeneratorVerifier<MyGenerator.HelloGenerator>;

namespace MyGenerator.Tests;

public class VerifierTests {
    [Fact]
    public async Task GeneratesExpectedOutput() {
        var input = """
            using MyLib;
            namespace TestApp;

            [GenerateHello]
            public partial class Greeter { }
            """;

        var expected = """
            // <auto-generated/>
            namespace Generated;

            public static partial class GreeterExtensions {
                public static string SayHello(this Greeter self) => $"Hello from {self}!";
            }
            """;

        await new VerifyCS.Test {
            TestState = {
                Sources = { input },
                GeneratedSources = {
                    (typeof(HelloGenerator),
                     "Greeter.Hello.g.cs",
                     SourceText.From(expected, Encoding.UTF8)),
                },
            },
        }.RunAsync();
    }
}
```

## Best Practices

1. **Test the generator, not Roslyn** - Focus on your transform logic. Don't test that `ForAttributeWithMetadataName` works.

2. **Use deterministic output** - Sort generated members alphabetically. Use consistent formatting. This makes snapshot tests stable.

3. **Test edge cases:**
   - Nested classes
   - Generic types
   - Multiple attributes on same type
   - Missing `partial` modifier
   - Types in global namespace
   - Types with no namespace

4. **Test caching** - Verify that unchanged inputs produce `Cached` run reasons. Poor caching hurts IDE performance.

5. **Test diagnostics separately** - Dedicated tests for each diagnostic ID with exact severity, location, and message.

6. **Keep test compilation minimal** - Only include references actually needed. Speeds up tests significantly.

7. **Use `CancellationToken` in tests** - Pass `CancellationToken.None` explicitly to mirror generator contract.

8. **Test with multiple language versions** - If your generator emits syntax that varies by language version.
