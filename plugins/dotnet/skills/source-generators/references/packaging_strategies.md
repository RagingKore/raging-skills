# Source Generator Packaging Strategies

## Table of Contents

- [Overview](#overview)
- [Strategy 1: Generator-Only Package](#strategy-1-generator-only-package)
- [Strategy 2: Library with Embedded Generator](#strategy-2-library-with-embedded-generator)
- [Strategy 3: Runtime Types + Generator](#strategy-3-runtime-types--generator)
- [Strategy 4: Multi-Targeting Generator](#strategy-4-multi-targeting-generator)
- [Embedding Generated Code in a Package](#embedding-generated-code-in-a-package)
- [Bundling Generator Dependencies](#bundling-generator-dependencies)
- [NuGet Analyzer Folder Convention](#nuget-analyzer-folder-convention)
- [MSBuild Props and Targets](#msbuild-props-and-targets)
- [Common Packaging Pitfalls](#common-packaging-pitfalls)

---

## Overview

Source generators ship inside NuGet packages using the `analyzers/dotnet/cs` folder convention. The compiler loads DLLs from this folder as Roslyn components. There are several packaging strategies depending on whether you ship runtime types alongside the generator.

**Critical constraint:** Generator DLLs MUST target `netstandard2.0`. The Roslyn compiler host loads them in its own process and only supports this target.

## Strategy 1: Generator-Only Package

The generator IS the package. No runtime types. Consumers get generated code at compile time with zero runtime footprint.

```xml
<!-- MyGenerator.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <IsRoslynComponent>true</IsRoslynComponent>
    <LangVersion>latest</LangVersion>

    <!-- NuGet packaging -->
    <PackageId>MyGenerator</PackageId>
    <Version>1.0.0</Version>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
    <DevelopmentDependency>true</DevelopmentDependency>
    <NoPackageAnalysis>true</NoPackageAnalysis>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0" PrivateAssets="all" />
  </ItemGroup>

  <!-- Place DLL in analyzers folder -->
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs"
          Visible="false" />
  </ItemGroup>
</Project>
```

**Key properties explained:**

| Property                               | Why                                                                      |
|----------------------------------------|--------------------------------------------------------------------------|
| `IncludeBuildOutput=false`             | Prevents the DLL from going into `lib/` (it's not a runtime dependency)  |
| `SuppressDependenciesWhenPacking=true` | Prevents `Microsoft.CodeAnalysis` from appearing as a package dependency |
| `DevelopmentDependency=true`           | Marks as dev-only; won't flow to consumer's consumers                    |
| `NoPackageAnalysis=true`               | Suppresses NuGet warnings about missing `lib/` folder                    |

## Strategy 2: Library with Embedded Generator

You have a library (runtime types, interfaces, attributes) and want the generator to be invisible - consumers just reference the library and get generated code automatically.

```
MyLibrary/
├── MyLibrary/                  # Runtime types (net8.0+)
│   ├── MyLibrary.csproj
│   ├── IMyService.cs
│   └── MyAttribute.cs
├── MyLibrary.Generator/        # Generator (netstandard2.0)
│   ├── MyLibrary.Generator.csproj
│   └── MyGenerator.cs
└── MyLibrary.sln
```

```xml
<!-- MyLibrary.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <PackageId>MyLibrary</PackageId>
  </PropertyGroup>

  <!-- Reference generator as analyzer, don't ship as runtime dep -->
  <ItemGroup>
    <ProjectReference Include="..\MyLibrary.Generator\MyLibrary.Generator.csproj"
                      PrivateAssets="all"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>

  <!-- Include generator DLL in package's analyzer folder -->
  <ItemGroup>
    <None Include="..\MyLibrary.Generator\$(OutputPath)\MyLibrary.Generator.dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs"
          Visible="false" />
  </ItemGroup>
</Project>
```

```xml
<!-- MyLibrary.Generator.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <IsRoslynComponent>true</IsRoslynComponent>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

**Result:** Consumers install `MyLibrary`, get both runtime types AND compile-time generation. The generator DLL never appears as a runtime reference.

## Strategy 3: Runtime Types + Generator

Same as Strategy 2 but the generator has its own identity. Useful when the generator is optional or can work with multiple libraries.

```xml
<!-- MyLibrary.csproj - ships ONLY runtime types -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
  </PropertyGroup>
</Project>

<!-- MyLibrary.Generator.csproj - ships as separate analyzer package -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
    <DevelopmentDependency>true</DevelopmentDependency>
  </PropertyGroup>
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs" />
  </ItemGroup>
</Project>
```

Consumer installs both:
```xml
<PackageReference Include="MyLibrary" Version="1.0.0" />
<PackageReference Include="MyLibrary.Generator" Version="1.0.0"
                  PrivateAssets="all" />
```

## Strategy 4: Multi-Targeting Generator

When your generator must support both old-style `.csproj` (Visual Studio analyzer VSIX) and new-style (NuGet), or needs to target multiple Roslyn versions:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <IsRoslynComponent>true</IsRoslynComponent>
  </PropertyGroup>

  <!-- Support multiple Roslyn versions via conditional refs -->
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.3.0" PrivateAssets="all" />
  </ItemGroup>

  <!-- For NuGet distribution -->
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs"
          Visible="false" />
  </ItemGroup>
</Project>
```

**Roslyn version targeting guidance:**

| Minimum Roslyn | Required For                      |
|----------------|-----------------------------------|
| 4.0.0          | `IIncrementalGenerator` basic API |
| 4.3.1          | `ForAttributeWithMetadataName`    |
| 4.8.0          | Interceptors (experimental)       |

Target the lowest version you need. Generators must work with whatever Roslyn version the consumer's SDK ships.

## Embedding Generated Code in a Package

**Scenario:** You want to ship pre-generated code as part of your library package, without exposing the generator to consumers. The generated code is "baked in" at your build time.

### Approach A: Build-Time Generation → Pack as Content

Run the generator during your build, capture the output, and include it as regular source:

```xml
<!-- In your library .csproj -->
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>

<!-- Exclude from compilation (already compiled via generator) but include in package -->
<ItemGroup>
  <Compile Remove="Generated/**" />
  <None Include="Generated/**" Pack="true" PackagePath="contentFiles/cs/any/" />
</ItemGroup>
```

### Approach B: Internal Generated Code

Generate code as `internal` so it's part of your library's implementation but invisible to consumers:

```csharp
// In the generator, emit with internal visibility
spc.AddSource("InternalHelper.g.cs", """
    // <auto-generated/>
    namespace MyLibrary.Internal;

    internal static class GeneratedHelper {
        internal static void RegisterAll() {
            // Generated registration code
        }
    }
    """);
```

Combined with Strategy 2 (embedded generator), the generated code compiles into your library DLL. Consumers never see the generator or the generated source - they only see the public API.

### Approach C: Source-Only Package (contentFiles)

Ship generated source as content files that get compiled into the consumer's assembly:

```xml
<ItemGroup>
  <None Include="GeneratedSources\**\*.cs"
        Pack="true"
        PackagePath="contentFiles/cs/any/MyLib/"
        BuildAction="Compile" />
</ItemGroup>
```

## Bundling Generator Dependencies

Generators run inside the compiler process. If your generator depends on third-party libraries (e.g., `Humanizer`, `Newtonsoft.Json`), those DLLs must be **embedded** in the package:

```xml
<PropertyGroup>
  <IncludeBuildOutput>false</IncludeBuildOutput>
  <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Humanizer.Core" Version="2.14.1" PrivateAssets="all" GeneratePathProperty="true" />
</ItemGroup>

<!-- Bundle dependency DLLs alongside the generator -->
<ItemGroup>
  <None Include="$(PkgHumanizer_Core)\lib\netstandard2.0\Humanizer.dll"
        Pack="true"
        PackagePath="analyzers/dotnet/cs"
        Visible="false" />
  <None Include="$(OutputPath)\$(AssemblyName).dll"
        Pack="true"
        PackagePath="analyzers/dotnet/cs"
        Visible="false" />
</ItemGroup>
```

**Important:** All DLLs in the `analyzers/dotnet/cs` folder are loaded into the compiler's assembly load context. Name conflicts with the consumer's dependencies can cause issues. Keep dependencies minimal.

## NuGet Analyzer Folder Convention

```
package/
├── analyzers/
│   └── dotnet/
│       ├── cs/                    # C# only
│       │   ├── MyGenerator.dll
│       │   └── MyDependency.dll
│       └── vb/                    # VB.NET only (if needed)
│           └── MyGenerator.dll
├── lib/
│   └── net9.0/
│       └── MyLibrary.dll          # Runtime types (optional)
├── build/
│   ├── MyPackage.props            # MSBuild props (optional)
│   └── MyPackage.targets          # MSBuild targets (optional)
└── MyPackage.nuspec
```

**Language-agnostic analyzers:** Place DLLs directly in `analyzers/dotnet/` (without `cs/` or `vb/` subfolder) to load for any .NET language.

## MSBuild Props and Targets

Ship custom MSBuild properties and targets to configure generation behavior:

```xml
<!-- build/MyPackage.props - loaded early, set defaults -->
<Project>
  <PropertyGroup>
    <MyGenerator_EmitDebugInfo Condition="'$(MyGenerator_EmitDebugInfo)' == ''">false</MyGenerator_EmitDebugInfo>
  </PropertyGroup>
  <ItemGroup>
    <CompilerVisibleProperty Include="MyGenerator_EmitDebugInfo" />
  </ItemGroup>
</Project>
```

```xml
<!-- build/MyPackage.targets - loaded late, can use other properties -->
<Project>
  <ItemGroup>
    <!-- Make config files available as additional files -->
    <AdditionalFiles Include="@(None)" Condition="'%(Extension)' == '.myconfig'" />
  </ItemGroup>
</Project>
```

The generator reads MSBuild properties via `AnalyzerConfigOptionsProvider`:

```csharp
var combined = provider.Combine(context.AnalyzerConfigOptionsProvider);
context.RegisterSourceOutput(combined, static (spc, pair) => {
    var (model, options) = pair;
    options.GlobalOptions.TryGetValue("build_property.MyGenerator_EmitDebugInfo", out var debug);
    // Use debug flag in generation
});
```

**Property name format:** MSBuild properties are exposed as `build_property.PropertyName` (case-sensitive).

## Common Packaging Pitfalls

### 1. Generator DLL Ends Up in `lib/`

**Symptom:** Consumer gets runtime `FileNotFoundException` or the generator doesn't run.
**Fix:** Set `IncludeBuildOutput=false` and explicitly place in `analyzers/dotnet/cs`.

### 2. Dependencies Leak to Consumer

**Symptom:** Consumer sees `Microsoft.CodeAnalysis` in their dependency tree.
**Fix:** Use `PrivateAssets="all"` on all generator `PackageReference` items. Use `SuppressDependenciesWhenPacking=true`.

### 3. Missing `ReferenceOutputAssembly="false"` on ProjectReference

**Symptom:** The generator DLL becomes a compile-time reference, not just an analyzer.
**Fix:** Always include both `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`.

### 4. Generator Doesn't Load (Version Mismatch)

**Symptom:** Generator silently does nothing.
**Fix:** Ensure `Microsoft.CodeAnalysis.CSharp` version is compatible with the consumer's SDK. Target the lowest version needed.

### 5. `[Generator]` Attribute Not Found

**Symptom:** Compiler doesn't discover the generator.
**Fix:** Ensure `Microsoft.CodeAnalysis.Analyzers` is referenced (it provides the attribute). Also check the generator targets `netstandard2.0`.

### 6. Dependency Conflict in Compiler Host

**Symptom:** `TypeLoadException` or `MissingMethodException` at build time.
**Fix:** Minimize third-party dependencies. If unavoidable, bundle with exact version and consider ILMerge or ILRepack.
