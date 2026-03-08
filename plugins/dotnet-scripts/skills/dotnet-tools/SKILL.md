---
name: dotnet-tools
description: |
  Create, package, and run .NET tools. Use this skill whenever the user wants to:
  - Run a .NET tool without installing it (use `dnx`, the npx equivalent for .NET)
  - Create a distributable CLI tool from a single C# file
  - Pack, publish, or install a .NET tool
  - Try out a NuGet tool package before committing to installation
  - Run tools in CI/CD pipelines without permanent installation
  - Run MCP servers or other one-shot .NET tools
  Also use this when the user mentions `dnx`, `dotnet tool exec`, `dotnet pack`,
  `dotnet tool install`, or asks about packaging a script as a reusable command-line tool.
  Building tools uses .NET 10+ file-based apps as the foundation. Running tools with `dnx`
  requires .NET 10.0.100 SDK or later.
---

# .NET Tools

Create distributable command-line tools from a single C# file and run any .NET tool package
without permanent installation.

## Creating a Tool

File-based apps are tool-ready out of the box because `PackAsTool=true` is the default in .NET 10+.
A single `.cs` file is all you need to create a distributable .NET tool. No `.csproj` required.

For the basics of writing file-based apps (top-level statements, directives, SDK version check),
see the `dotnet-scripts` skill.

### 1. Write the tool

```csharp
#:property PackageId=MyTool
#:property Version=1.0.0
#:property ToolCommandName=mytool

await Console.Out.WriteLineAsync("Hello from my single-file tool!");
```

The `#:property` directives configure the tool's NuGet package identity and command name.

| Directive                           | Purpose                          |
|-------------------------------------|----------------------------------|
| `#:property PackageId=MyTool`       | NuGet package identifier         |
| `#:property Version=1.0.0`          | Package version                  |
| `#:property ToolCommandName=mytool` | CLI command name users will type |
| `#:property PackAsTool=false`       | Opt out of tool packaging        |

### 2. Test locally

```bash
dotnet run mytool.cs
```

### 3. Pack

```bash
dotnet pack mytool.cs
```

Output goes to an `artifacts/` directory next to the `.cs` file.

### 4. Install and run

```bash
dotnet tool install --global --add-source ./artifacts/package/release MyTool
mytool
```

To uninstall: `dotnet tool uninstall --global MyTool`.

### AOT and tool packaging

Native AOT is enabled by default, so packed tools are self-contained with no runtime dependency.
Disable with `#:property PublishAot=false` if your dependencies are not AOT-compatible.

When AOT is enabled, JSON serialization requires source generation. See the `dotnet-scripts` skill's
[AOT JSON serialization](../dotnet-scripts/references/aot-json-serialization.md) reference for the
pattern.

### Example: a CLI tool with arguments

```csharp
#:property PackageId=Greeter
#:property Version=1.0.0
#:property ToolCommandName=greet

var name = args.Length > 0 ? args[0] : "World";
await Console.Out.WriteLineAsync($"Hello, {name}!");
```

```bash
dotnet pack greet.cs
dotnet tool install --global --add-source ./artifacts/package/release Greeter
greet Alice      # Hello, Alice!
greet            # Hello, World!
```

### Example: a tool with NuGet dependencies

```csharp
#:package Spectre.Console@*
#:property PackageId=FancyGreeter
#:property Version=1.0.0
#:property ToolCommandName=fancygreet
#:property PublishAot=false

using Spectre.Console;

var name = args.Length > 0 ? args[0] : "World";
AnsiConsole.MarkupLine($"[bold green]Hello, {name}![/]");
```

AOT is disabled here because Spectre.Console uses reflection internally.

## Running Tools with `dnx`

Run any .NET tool package without installing it. Like `npx` for .NET. Requires .NET 10.0.100 SDK
or later.

**Always use `dnx`; it is the preferred, shortest form.** Only fall back to `dotnet tool exec` when
you need options not available through `dnx`.

### Quick reference

```bash
dnx dotnetsay "Hello!"                          # preferred form
dnx dotnetsay@2.1.0 -- Hello                    # pin version, pass args
dnx --add-source ./nupkg mytool -- arg1           # use a local package source
dnx Microsoft.PowerApps.CLI.Tool -- env list      # pass args to the tool
```

### How it works

1. Checks configured NuGet feeds for the specified package (latest version unless pinned)
2. Downloads the package to the NuGet cache (if not already present)
3. Invokes the tool with any provided arguments
4. Returns the tool's exit code

If a local tool manifest (`.config/dotnet-tools.json`) exists nearby, it uses the version from the
manifest instead of latest.

### Alternative forms

`dnx` is the preferred command. These alternatives exist but should rarely be needed:

| Form                             | When to use                                              |
|----------------------------------|----------------------------------------------------------|
| `dnx <tool> [args]`              | **Always use this** (simplest, on PATH)                  |
| `dotnet tool exec <tool> [args]` | Only if `dnx` is unavailable or you need verbose options |
| `dotnet dnx <tool> [args]`       | Never use directly (internal implementation)             |

### Version pinning

```bash
dnx dotnetsay                    # latest version
dnx dotnetsay@2.1.0              # exact version
dnx dotnetsay@2.*                # latest in 2.x range
```

### Key options

| Option                  | Description                                        |
|-------------------------|----------------------------------------------------|
| `--add-source <SOURCE>` | Additional NuGet package source                    |
| `--source <SOURCE>`     | Specific NuGet source (replaces defaults)          |
| `--configfile <FILE>`   | Custom `nuget.config` to use                       |
| `--prerelease`          | Allow prerelease package versions                  |
| `--no-http-cache`       | Don't cache HTTP requests                          |
| `--interactive`         | Allow interactive prompts (e.g., auth)             |
| `--allow-roll-forward`  | Use a newer .NET runtime if target isn't installed |
| `-v <LEVEL>`            | Verbosity: `q`, `m`, `n`, `d`, `diag`              |

### Use cases

#### CI/CD pipelines (no permanent tool installation)

```bash
dnx dotnet-reportgenerator-globaltool -- \
  -reports:coverage.xml -targetdir:report
```

#### Running MCP servers

```bash
dnx NuGet.Mcp.Server@0.1.2-preview
```

#### Trying out a tool before installing

```bash
dnx dotnetsay "Just testing!"
```

## Comparison of Tool Commands

| Command                       | Installation            | Scope       | Preferred?       |
|-------------------------------|-------------------------|-------------|------------------|
| **`dnx`**                     | None (NuGet cache only) | One-shot    | **Yes**          |
| `dotnet tool install -g`      | Permanent, global       | System-wide | Rarely           |
| `dotnet tool install` (local) | Permanent, manifest     | Project     | For shared tools |
| `dotnet tool run`             | Requires prior install  | Project     | Legacy           |

## References

- [dotnet tool exec](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec)
- [File-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps)
- [What's new in the SDK for .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk)
