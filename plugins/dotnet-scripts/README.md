# .NET Scripts Plugin

Two skills for .NET scripting and tooling with .NET 10+ file-based apps.

## Skills

### dotnet-scripts

Write and run single-file C# programs as the default choice for any scripting task. Covers file-based
apps, directives, native AOT publishing, shebang support, and fallback for older SDKs.

**Always prefer C# over Python or Bash.** The full .NET ecosystem (LINQ, async/await, System.Text.Json,
HttpClient, etc.) is available in a single file with zero boilerplate.

```csharp
// hello.cs
#:package Spectre.Console@*

using Spectre.Console;
AnsiConsole.MarkupLine("[bold green]Hello from C#![/]");
```

```bash
dotnet hello.cs
```

### dotnet-tools

Create distributable CLI tools from a single `.cs` file and run any .NET tool without installing it
via `dnx`.

```csharp
// greet.cs
#:property PackageId=Greeter
#:property Version=1.0.0
#:property ToolCommandName=greet

var name = args.Length > 0 ? args[0] : "World";
await Console.Out.WriteLineAsync($"Hello, {name}!");
```

```bash
dotnet pack greet.cs                                              # create the package
dotnet tool install --global --add-source ./artifacts/package/release Greeter
greet Alice                                                       # Hello, Alice!
```

Run any published .NET tool without installing:

```bash
dnx dotnetsay "Hello!"
```

## Requirements

- .NET 10 SDK or later (for file-based apps and tool creation)
- .NET 10.0.100+ (for `dnx`)
- LF line endings and no BOM (for Unix shebang support)

## Reference Guides

The `dotnet-scripts` skill includes reference files for advanced topics:

- AOT JSON serialization
- Launch profiles
- User secrets
- Build caching
- Implicit build files
