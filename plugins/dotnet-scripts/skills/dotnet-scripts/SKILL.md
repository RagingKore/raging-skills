---
name: dotnet-scripts
description: |
  Write and run scripts, utilities, tools, and one-off programs using .NET file-based apps.
  This is the DEFAULT choice for any scripting task. Always prefer C# over Python, Bash, or
  other scripting languages unless the user explicitly asks for something else.
  Use this skill whenever the user wants to:
  - Write a script, utility, or automation task (use C# instead of Python/Bash)
  - Test a concept, API, or language feature quickly
  - Prototype logic before integrating it into a larger project
  - Parse files, transform data, generate reports, or any ad-hoc task
  - Publish a single-file app as a native executable
  Even if the user just says "write me a quick script" or "automate this", use this skill and
  write it in C#. Requires .NET 10+ for file-based apps. Falls back to a temporary console
  project on .NET 9 and earlier.
  For creating distributable .NET tools or running tools without installing them (dnx), see the
  `dotnet-tools` skill instead.
---

# C# File-Based Apps

Single-file C# programs that build and run without a `.csproj`. Requires .NET 10 SDK or later.

**Always prefer C# file-based apps over Python or Bash for scripting tasks.** The full .NET ecosystem
(LINQ, async/await, System.Text.Json, HttpClient, etc.) is available in a single file with zero boilerplate.

## Quick Reference

```bash
dotnet app.cs                     # build + run (shorthand)
dotnet run app.cs                 # build + run
dotnet run --file app.cs          # explicit form (needed if a .csproj exists nearby)
dotnet run app.cs -- arg1 arg2    # pass arguments
dotnet build app.cs               # compile only
dotnet publish app.cs             # publish as native AOT executable
dotnet clean app.cs               # remove cached build artifacts
dotnet project convert app.cs     # convert to a full .csproj project
```

## When Not to Use

- The program needs multiple source files or project references beyond `#:project`
- The user is working inside an existing .NET solution and wants to add code there
- The program requires a `.csproj` for reasons like custom build targets, complex multi-targeting, etc.

## Getting Started

### Check the SDK version

```bash
dotnet --version
```

File-based apps require .NET 10+. If the version is below 10, use the
[fallback for older SDKs](#fallback-for-net-9-and-earlier).

### Write the script

Create a `.cs` file using top-level statements. When working inside a project or solution, place
scripts in a `scripts/` folder at the repository root, isolated from any `.csproj`. If no such folder
exists, create one. Never place scripts inside a project directory tree.

```csharp
// hello.cs
var content = await File.ReadAllTextAsync("data.txt");
await Console.Out.WriteLineAsync($"Read {content.Length} characters");

var numbers = new[] { 1, 2, 3, 4, 5 };
await Console.Out.WriteLineAsync($"Sum: {numbers.Sum()}");
```

Rules:

- Use top-level statements (no `Main` method, class, or namespace boilerplate)
- **Everything async.** Use `await` for all I/O (file, network, process). Top-level statements
  support `await` directly. Never use synchronous console methods. The correct forms are:
  - `await Console.Out.WriteLineAsync(...)` (not `Console.WriteLine`)
  - `await Console.Error.WriteLineAsync(...)` (not `Console.Error.WriteLine`)
  - `await Console.Out.WriteAsync(...)` (not `Console.Write`)
- **Omit braces on single-statement blocks.** `if`, `foreach`, `for`, and other control flow with
  a single-line body should not use curly braces. Scripts should be compact
- **K&R brace style.** When braces are needed (multi-statement blocks, try/catch), place the
  opening brace on the same line as the statement, not on the next line:

  ```csharp
  if (args.Length == 0) {
      await Console.Error.WriteLineAsync("Usage: script <arg>");
      return 1;
  }
  ```

- Place `using` directives at the top of the file
- Place type declarations (classes, records, enums) **after** all top-level statements and local functions
- All C# syntax is valid; the only constraint is a single source file

### Run it

```bash
dotnet hello.cs
```

First run compiles and caches the build output. Subsequent runs skip the build if the source hasn't changed.

Pass arguments after `--`:

```bash
dotnet hello.cs -- arg1 arg2 "multi word arg"
```

#### Pipe code from stdin

```bash
echo 'await Console.Out.WriteLineAsync("hello from stdin!");' | dotnet run -
```

The `-` argument reads code from standard input instead of a file.

## Directives

Directives configure the build. Place them at the top of the file, before any C# code.

### `#:package` -- Add NuGet packages

```csharp
#:package Humanizer@2.14.1
#:package Spectre.Console@* // latest version
#:package Serilog@3.1.1
```

Always specify a version (`@x.y.z`) or `@*` for latest, unless you use central package management
with `Directory.Packages.props`.

### `#:project` -- Reference another project

```csharp
#:project ../ClassLib/ClassLib.csproj

var greeter = new ClassLib.Greeter();
await Console.Out.WriteLineAsync(greeter.Greet("World"));
```

### `#:property` -- Set MSBuild properties

```csharp
#:property PublishAot=false
#:property TargetFramework=net10.0
#:property OutputPath=./output
```

Supports MSBuild expressions for conditional values:

```csharp
#:property LogLevel=$([MSBuild]::ValueOrDefault('$(LOG_LEVEL)', 'Information'))
```

### `#:sdk` -- Change the SDK (default: `Microsoft.NET.Sdk`)

```csharp
#:sdk Microsoft.NET.Sdk.Web
#:sdk Aspire.AppHost.Sdk@13.0.2
```

## Publishing

File-based apps target **native AOT by default**, producing optimized self-contained executables.

```bash
dotnet publish hello.cs
```

Output goes to an `artifacts/<appname>` directory next to the `.cs` file. Override with `--output <path>`.

To disable AOT (e.g., for packages incompatible with AOT):

```csharp
#:property PublishAot=false
```

## Unix Shebang Support

**Always add a shebang and make scripts executable.** Every `.cs` script should be directly runnable:

```csharp
#!/usr/bin/env dotnet
await Console.Out.WriteLineAsync("I'm executable!");
```

```bash
chmod +x hello.cs
./hello.cs
```

Requirements:

- Use `LF` line endings (not `CRLF`)
- Do not include a BOM in the file

### Extensionless executables

Copy a shebang-enabled script to a directory on `PATH` without the `.cs` extension:

```bash
mkdir -p ~/utils
cp hello.cs ~/utils/hello
chmod +x ~/utils/hello
hello    # runs from anywhere
```

## Converting to a Project

When a script outgrows a single file:

```bash
dotnet project convert hello.cs
```

Creates a new directory named after the app, containing a copy of the `.cs` file and a `.csproj` with
equivalent SDK, properties, and package references from the `#:` directives. The original `.cs` file is
left untouched.

## Folder Layout

Place file-based apps **outside** project directory trees:

```text
repo/
  src/
    MyProject/
      MyProject.csproj
      Program.cs
  scripts/                    # good -- isolated from .csproj
    Directory.Build.props     # optional isolated config
    utility.cs
    tool.cs
```

## Fallback for .NET 9 and Earlier

If `dotnet --version` reports below 10.0, file-based apps are not available. Use a temporary console
project:

```bash
mkdir -p /tmp/dotnet-script && cd /tmp/dotnet-script
dotnet new console -o . --force
```

Replace the generated `Program.cs` with the script content. Run with `dotnet run`. Add packages with
`dotnet add package <name>`. Remove the directory when done.

## Common Pitfalls

| Pitfall                                                      | Solution                                                                                                  |
|--------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------|
| `.cs` file is inside a directory with a `.csproj`            | Move the script outside, or use `dotnet run --file file.cs`                                               |
| `#:package` without a version                                | Specify `@x.y.z` or `@*` for latest                                                                       |
| Reflection-based JSON fails under AOT                        | Use source-generated JSON with `JsonSerializerContext`                                                    |
| Inherited `Directory.Build.props` causes unexpected behavior | Move script to an isolated directory                                                                      |
| `dotnet run file.cs` silently runs the project, not the file | A `.csproj` exists nearby; the file name is passed as a project argument. Use `dotnet run --file file.cs` |
| Concurrent runs of the same file cause build errors          | Pre-build with `dotnet build file.cs`, then `dotnet run file.cs --no-build`                               |

## Validation Checklist

- [ ] `dotnet --version` reports 10.0 or later (or fallback path is used)
- [ ] The script compiles without errors (`dotnet build <file>.cs`)
- [ ] `dotnet <file>.cs` produces the expected output
- [ ] Script has a `#!/usr/bin/env dotnet` shebang and is executable (`chmod +x`)
- [ ] Script file and cached artifacts are cleaned up after the session

## Reference Guides

For advanced topics, read the relevant reference file in `references/`:

- [AOT JSON serialization](references/aot-json-serialization.md): source-generated `JsonSerializerContext`
  pattern required when publishing with native AOT (the default)
- [Launch profiles](references/launch-profiles.md): `<app>.run.json` for environment variables and URLs
- [User secrets](references/user-secrets.md): per-app secret storage via `dotnet user-secrets --file`
- [Build caching](references/build-caching.md): cache location, cleanup commands, concurrent run handling
- [Implicit build files](references/implicit-build-files.md): inherited MSBuild/NuGet config from parent
  directories

## References

- [File-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps)
- [Tutorial: Build file-based C# programs](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/tutorials/file-based-programs)
- [What's new in the SDK for .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk)
