---
name: bullseye
description: |
  Create .NET build scripts using Bullseye and SimpleExec as dotnet file-based scripts. Bullseye
  runs a target dependency graph from a single .cs file with no project boilerplate; use it instead
  of MSBuild XML, PowerShell, or shell scripts for build automation. Use this skill when the user
  mentions Bullseye, build targets, build scripts in C#, target dependency graphs, SimpleExec,
  creating a build.cs file, or wants to automate build/test/pack/publish steps in .NET. Also use
  when the user asks about build automation or CI/CD scripts for .NET, or is comparing Cake, Nuke,
  or FAKE with simpler alternatives.
---

# Bullseye Build Scripts

Bullseye is a .NET library for running a target dependency graph. Targets are defined in a C#
file, and each target can depend on other targets and run an action. Bullseye is almost always
paired with SimpleExec for running shell commands.

Use K&R brace style (opening brace on the same line) in build scripts to keep them compact.

Every `.cs` script file must start with `#!/usr/bin/env dotnet` and be made executable (`chmod +x`). This lets users
run `./build.cs` directly without typing `dotnet run`.

## Quick Start

Create a `build.cs` file in the repository root using the dotnet file-based script format:

```csharp
#!/usr/bin/env dotnet

#:package Bullseye@6.*
#:package SimpleExec@13.*

using static Bullseye.Targets;
using static SimpleExec.Command;

Target("build", () => RunAsync("dotnet", "build --configuration Release"));

Target("test", dependsOn: ["build"], () =>
    RunAsync("dotnet", "test --configuration Release --no-build"));

Target("default", dependsOn: ["test"]);

await RunTargetsAndExitAsync(args);
```

Run it:

```sh
dotnet run build.cs              # runs "default" target
dotnet run build.cs -- build     # runs "build" only
dotnet run build.cs -- -h        # shows help
```

The `--` separator tells `dotnet run` that everything after it is for the app, not for `dotnet`.
File-based scripts are self-contained (no csproj needed), cross-platform, and work anywhere the
.NET SDK is installed.

## Defining Targets

Use `using static Bullseye.Targets` to bring the static `Target()` method into scope. Every
overload takes a `name` as the first argument.

### Simple target

```csharp
Target("clean", () => RunAsync("dotnet", "clean"));
```

### Target with dependencies

Dependencies are an `IEnumerable<string>` listing target names that must run first. Use collection
expressions for readability:

```csharp
Target("build", () => RunAsync("dotnet", "build -c Release"));
Target("test", dependsOn: ["build"], () => RunAsync("dotnet", "test -c Release --no-build"));
Target("pack", dependsOn: ["build"], () => RunAsync("dotnet", "pack -c Release --no-build"));
```

### Dependency-only target

A target with no action is useful for grouping:

```csharp
Target("default", dependsOn: ["test", "pack"]);
```

### Target with description

Descriptions appear in `--list-targets` output:

```csharp
Target("publish", "Push packages to NuGet.org", dependsOn: ["pack"], () =>
    RunAsync("dotnet", "nuget push **/*.nupkg --source nuget.org"));
```

### forEach target

Run an action once per input. Bullseye reports progress for each input individually:

```csharp
var frameworks = new[] { "net8.0", "net9.0" };

Target("test", dependsOn: ["build"], forEach: frameworks, framework =>
    RunAsync("dotnet", $"test -c Release --no-build -f {framework}"));
```

The generic `Target<TInput>` overload is inferred from the `forEach` parameter. Works with any
`IEnumerable<TInput>`; anonymous types are convenient for multi-dimensional inputs:

```csharp
var frameworks = new[] { "net8.0", "net9.0" };
var configurations = new[] { "Release", "Debug" };
var matrix = frameworks
    .SelectMany(f => configurations.Select(c => new { Framework = f, Config = c }));

Target("build", forEach: configurations, config =>
    RunAsync("dotnet", $"build -c {config}"));

Target("test", dependsOn: ["build"], forEach: matrix, item =>
    RunAsync("dotnet", $"test -f {item.Framework} -c {item.Config} --no-build"));
```

When introducing a multi-config test matrix, update upstream targets (like build) to handle all
configurations too. Otherwise `--no-build` will fail for configs that were never built.

### Async targets

Both `Action` and `Func<Task>` overloads exist. Use async when the action is naturally async:

```csharp
Target("deploy", dependsOn: ["publish"], async () => {
    await RunAsync("az", "webapp deploy --name myapp --src-path app.zip");
    await RunAsync("az", "webapp restart --name myapp");
});
```

## Running Targets

### RunTargetsAndExitAsync (default)

Parses `args`, runs the requested targets (or `"default"` if none specified), then calls
`Environment.Exit`. This is the right choice for build scripts because it sets the exit code
correctly for CI:

```csharp
await RunTargetsAndExitAsync(args);
```

### Custom exception handling

Pass a `messageOnly` predicate to show just the message (not the full stack trace) for expected
exceptions. This keeps CI output clean:

```csharp
await RunTargetsAndExitAsync(args, ex => ex is SimpleExec.ExitCodeException);
```

`SimpleExec.ExitCodeException` is thrown when a process exits with a non-zero code. Without the
filter, Bullseye prints the full stack trace, which is noisy for build failures.

### RunTargetsWithoutExitingAsync

Use this only when you need code to run after the targets complete. It throws
`TargetFailedException` on failure instead of calling `Environment.Exit`:

```csharp
try {
    await RunTargetsWithoutExitingAsync(args);
} catch (TargetFailedException) {
    // handle failure
} catch (InvalidUsageException ex) {
    Console.Error.WriteLine(ex.Message);
}
```

## CLI Options

Bullseye provides a built-in CLI. Run with `--help` to see all options:

| Short | Long                  | Description                                      |
|-------|-----------------------|--------------------------------------------------|
| `-c`  | `--clear`             | Clear the console before execution               |
| `-n`  | `--dry-run`           | Do a dry run without executing actions           |
| `-d`  | `--list-dependencies` | List all targets and dependencies, then exit     |
| `-i`  | `--list-inputs`       | List all targets and inputs, then exit           |
| `-l`  | `--list-targets`      | List all targets, then exit                      |
| `-t`  | `--list-tree`         | List all targets and dependency trees, then exit |
| `-N`  | `--no-color`          | Disable colored output                           |
| `-p`  | `--parallel`          | Run targets in parallel                          |
| `-s`  | `--skip-dependencies` | Do not run targets' dependencies                 |
| `-v`  | `--verbose`           | Enable verbose output                            |

Bullseye also respects the `NO_COLOR` environment variable.

CI environments (GitHub Actions, GitLab CI, TeamCity, Travis, AppVeyor) are auto-detected and
output is adjusted accordingly. Force a specific host mode with `--github-actions`, `--teamcity`,
etc.

## Project-Based Alternative

When you need more control (multiple source files, analyzers, or custom MSBuild properties),
use a full console project instead of a file-based script:

```sh
dotnet new console --name Targets
cd Targets
dotnet add package Bullseye
dotnet add package SimpleExec
```

Then define targets in `Program.cs` the same way. Run with `dotnet run --project Targets -- test`.
Add it to the solution so IDE tooling picks it up: `dotnet sln add Targets/Targets.csproj`

## Instance API

For advanced scenarios (multiple independent target graphs, testing, or embedding), create a
`Targets` instance instead of using the static API:

```csharp
using Bullseye;

var targets = new Targets();
targets.Add("build", () => Console.WriteLine("building..."));
targets.Add("test", dependsOn: ["build"], () => Console.WriteLine("testing..."));
targets.Add("default", dependsOn: ["test"]);

await targets.RunAndExitAsync(args);
```

Instance methods mirror the static API: `Add(...)` instead of `Target(...)`, and
`RunAndExitAsync`/`RunWithoutExitingAsync` instead of `RunTargetsAndExitAsync`/
`RunTargetsWithoutExitingAsync`.

## System.CommandLine Integration

When you need custom options alongside Bullseye targets, use `System.CommandLine` to handle
parsing and forward the Bullseye portion:

```csharp
#!/usr/bin/env dotnet

#:package Bullseye@6.*
#:package SimpleExec@13.*
#:package System.CommandLine@2.*

using System.CommandLine;
using System.CommandLine.Parsing;
using Bullseye;
using static Bullseye.Targets;
using static SimpleExec.Command;

var configOption = new Option<string>("--config", "-c") {
    Description = "Build configuration",
    DefaultValueFactory = _ => "Release",
};

var targetsArg = new Argument<string[]>("targets") {
    Description = "Bullseye targets to run",
    DefaultValueFactory = _ => [],
};

// Register Bullseye's own options so --dry-run, --parallel, etc. show in --help
var bullseyeOptions = Options.Definitions
    .Select(d => new Option<bool>(d.Aliases[0], [.. d.Aliases.Skip(1)])
        { Description = d.Description })
    .ToList();

var cmd = new RootCommand { configOption, targetsArg };
foreach (var opt in bullseyeOptions)
    cmd.Options.Add(opt);

// Cast resolves SetAction overload ambiguity between Action<ParseResult> and Func<ParseResult,Task>
cmd.SetAction((Func<ParseResult, Task>)(async cmdLine => {
    var config = cmdLine.GetValue(configOption);

    Target("build", () => RunAsync("dotnet", $"build -c {config}"));
    Target("test", dependsOn: ["build"], () => RunAsync("dotnet", $"test -c {config} --no-build"));
    Target("default", dependsOn: ["test"]);

    var parsedOptions = new Options(
        bullseyeOptions.Select(o => (o.Name, cmdLine.GetValue(o))));

    // Use named parameter: messageOnly is at position 5, not 3
    await RunTargetsAndExitAsync(
        cmdLine.GetRequiredValue(targetsArg), parsedOptions,
        messageOnly: ex => ex is SimpleExec.ExitCodeException);
}));

return await cmd.Parse(args).InvokeAsync();
```

The `SetAction` overload is ambiguous for async lambdas. Cast to `Func<ParseResult, Task>` to
resolve it. For `RunTargetsAndExitAsync` with `IOptions`, the `messageOnly` parameter is at
position 5 (after `unknownOptions` and `showHelp`); use the named parameter to avoid mistakes.

Use `CommandLine.Parse(args)` when you want to pre-parse args without System.CommandLine:

```csharp
var (targetNames, options, unknownOptions, showHelp) = CommandLine.Parse(args);
await targets.RunAndExitAsync(targetNames, options, unknownOptions, showHelp);
```

## Common Patterns

### Typical build script

Organize targets in dependency order, ending with `RunTargetsAndExitAsync`:

```csharp
#!/usr/bin/env dotnet

#:package Bullseye@6.*
#:package SimpleExec@13.*

using static Bullseye.Targets;
using static SimpleExec.Command;

var configuration = "Release";

Target("clean", () => RunAsync("dotnet", "clean -c Release -v minimal"));

Target("restore", dependsOn: ["clean"], () =>
    RunAsync("dotnet", "restore"));

Target("build", dependsOn: ["restore"], () =>
    RunAsync("dotnet", $"build -c {configuration} --no-restore"));

Target("test", dependsOn: ["build"], () =>
    RunAsync("dotnet", $"test -c {configuration} --no-build"));

Target("pack", dependsOn: ["build"], () =>
    RunAsync("dotnet", $"pack -c {configuration} --no-build"));

Target("default", dependsOn: ["test", "pack"]);

await RunTargetsAndExitAsync(args, ex => ex is SimpleExec.ExitCodeException);
```

### SimpleExec basics

`SimpleExec.Command.RunAsync(name, args)` runs a process and throws `ExitCodeException` on
non-zero exit. Use `ReadAsync` to capture stdout:

```csharp
using static SimpleExec.Command;

// Run a command
await RunAsync("dotnet", "build");

// Capture output
var (stdout, _) = await ReadAsync("dotnet", "--version");
```

### Parallel execution

Pass `--parallel` on the command line to run independent targets concurrently. Bullseye
determines which targets can run in parallel based on the dependency graph. Targets that do not
depend on each other run simultaneously.
