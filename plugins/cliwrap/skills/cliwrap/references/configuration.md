# CliWrap Configuration Reference

## Overview

CliWrap models every CLI invocation as an immutable `Command` object. Configuration is applied through a set of
`With*` methods, each returning a new `Command` instance. This design enables safe reuse, composition, and
branching of command configurations without side effects.

This reference covers every configuration method on `Command`, including all overloads, default behavior, platform
considerations, and the `CommandResult` type returned after execution.

## Command Immutability

`Command` is an immutable value object. Every `With*` method returns a brand new `Command` instance; the original
remains unchanged. This is critical for building reusable base commands that branch into multiple variations.

```csharp
var baseCmd = Cli.Wrap("git");
var pushCmd = baseCmd.WithArguments(["push"]);
var pullCmd = baseCmd.WithArguments(["pull"]);

// baseCmd still has no arguments.
// pushCmd and pullCmd are independent commands derived from the same base.
```

Forgetting this property leads to a common mistake: calling `With*` methods without capturing the return value.

```csharp
// BUG: the return value is discarded; cmd is never modified.
var cmd = Cli.Wrap("git");
cmd.WithArguments(["status"]); // result thrown away

// CORRECT: capture the new instance.
var cmd = Cli.Wrap("git");
cmd = cmd.WithArguments(["status"]);
```

Because `Command` is immutable, it is safe to store base commands as `static readonly` fields, pass them across
threads, and derive specialized variants at call sites without synchronization.

## WithArguments

`WithArguments` defines the arguments passed to the target executable. It provides four overloads, each suited to
a different scenario. The array and builder overloads handle escaping automatically; the raw string overload does
not.

### Array Syntax (Preferred)

Pass a collection of argument values. CliWrap escapes each element individually, so values containing spaces,
quotes, or special characters are handled correctly.

```csharp
var cmd = Cli.Wrap("git").WithArguments(["commit", "-m", "my commit"]);
```

This is the recommended overload for the majority of use cases. It is concise, safe, and easy to read. The
collection can be any `IEnumerable<string>`, including arrays, lists, and LINQ projections.

```csharp
var files = new[] { "file one.txt", "file two.txt" };
var cmd = Cli.Wrap("git").WithArguments(
    new[] { "add" }.Concat(files)
);
```

### Builder Syntax (Fluent)

Use the fluent `ArgumentsBuilder` when constructing arguments programmatically, when mixing string and non-string
values, or when adding arguments conditionally within a single expression.

```csharp
var cmd = Cli.Wrap("git").WithArguments(args => args
    .Add("clone")
    .Add("https://github.com/Tyrrrz/CliWrap")
    .Add("--depth")
    .Add(20)
);
```

The `Add` method accepts `string`, `int`, `long`, `double`, and other primitive types. Non-string values are
converted using the invariant culture, avoiding locale-dependent formatting issues (e.g., decimal separators).

Conditional logic fits naturally inside the builder expression.

```csharp
var shallow = true;

var cmd = Cli.Wrap("git").WithArguments(args => args
    .Add("clone")
    .Add("https://github.com/Tyrrrz/CliWrap")
    .Add(shallow ? "--depth" : null!)
    .Add(shallow ? "1" : null!)
);
```

### Imperative Builder Syntax

For complex branching that does not fit a single fluent chain, use the imperative form of the builder delegate.
This is the same `ArgumentsBuilder`; the only difference is coding style.

```csharp
var forcePush = true;

var cmd = Cli.Wrap("git").WithArguments(args =>
{
    args.Add("push");

    if (forcePush)
        args.Add("--force");
});
```

This overload is useful when the argument list depends on multiple conditions, loops, or external data sources.

```csharp
var tags = new[] { "v1.0", "v2.0" };

var cmd = Cli.Wrap("git").WithArguments(args =>
{
    args.Add("push");
    args.Add("origin");

    foreach (var tag in tags)
        args.Add(tag);
});
```

### Raw String Syntax (Avoid)

Pass the entire argument string as a single pre-formatted value. CliWrap forwards it verbatim to the process
without any escaping or validation.

```csharp
var cmd = Cli.Wrap("git").WithArguments("commit -m \"my commit\"");
```

> **WARNING**: The raw string overload expects the caller to handle all escaping. Incorrect escaping causes
> silent bugs and opens the door to command injection vulnerabilities. Always prefer the array or builder syntax
> unless working with a legacy string that is already correctly formatted and trusted.

Scenarios where the raw string overload may be acceptable:

- Forwarding a user-provided argument string that has already been validated and escaped.
- Interfacing with tools that require a specific quoting style not produced by the automatic escaper.

In every other case, treat the raw overload as a last resort.

## ArgumentsBuilder.Escape

`ArgumentsBuilder` exposes a static `Escape` method for rare situations where manual escaping is necessary outside
the context of a builder chain. The method applies the same platform-aware escaping logic used internally by the
`Add` methods.

```csharp
var escaped = ArgumentsBuilder.Escape(rawValue);
```

Use this when constructing argument strings manually for logging, diagnostics, or interop with APIs that accept
pre-formatted command lines. Do not use it as a substitute for the array or builder overloads when configuring a
`Command`.

## WithWorkingDirectory

Set the working directory for the child process. The directory must exist before execution; CliWrap does not create
it.

```csharp
var cmd = Cli.Wrap("git").WithWorkingDirectory("c:/projects/my project/");
```

The path can be absolute or relative. Relative paths are resolved against the current process's working directory
at the time of execution.

**Default behavior**: when no working directory is specified, the child process inherits
`Directory.GetCurrentDirectory()` from the parent process. This matches the behavior of `System.Diagnostics.Process`.

Paths with spaces or special characters require no additional escaping; CliWrap passes the value directly to the
operating system.

```csharp
var cmd = Cli.Wrap("dotnet")
    .WithWorkingDirectory("/home/user/my solution/src/MyProject")
    .WithArguments(["build"]);
```

## WithEnvironmentVariables

Configure environment variables for the child process. Variables are applied on top of the variables inherited from
the parent process. This means the child process starts with a copy of the parent's full environment, then the
specified overrides are merged in.

### Builder Syntax

```csharp
var cmd = Cli.Wrap("git").WithEnvironmentVariables(env => env
    .Set("GIT_AUTHOR_NAME", "John")
    .Set("GIT_AUTHOR_EMAIL", "john@email.com")
);
```

The builder supports fluent chaining and conditional logic, following the same pattern as `WithArguments`.

```csharp
var useProxy = true;

var cmd = Cli.Wrap("curl").WithEnvironmentVariables(env =>
{
    env.Set("HOME", "/tmp");

    if (useProxy)
        env.Set("HTTPS_PROXY", "http://proxy.corp:8080");
});
```

### Dictionary Syntax

Pass a dictionary of key-value pairs directly. This is convenient when the variables are already stored in a
dictionary or configuration object.

```csharp
var cmd = Cli.Wrap("git").WithEnvironmentVariables(new Dictionary<string, string?>
{
    ["GIT_AUTHOR_NAME"] = "John",
    ["GIT_AUTHOR_EMAIL"] = "john@email.com"
});
```

### Removing Inherited Variables

Set a variable's value to `null` to remove it from the child process's environment. This is useful for stripping
sensitive or interfering variables inherited from the parent.

```csharp
var cmd = Cli.Wrap("dotnet").WithEnvironmentVariables(env => env
    .Set("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
    .Set("ASPNETCORE_ENVIRONMENT", null) // remove if inherited
);
```

With the dictionary syntax, the same approach applies.

```csharp
var cmd = Cli.Wrap("dotnet").WithEnvironmentVariables(new Dictionary<string, string?>
{
    ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
    ["ASPNETCORE_ENVIRONMENT"] = null
});
```

### Important Notes

- Variable names are case-sensitive on Linux and macOS, case-insensitive on Windows.
- The parent process's entire environment is inherited by default. `WithEnvironmentVariables` only adds overrides;
  it does not replace the inherited set.
- Calling `WithEnvironmentVariables` multiple times replaces the previously configured overrides (not the inherited
  variables). Only the last call's overrides are applied.

## WithResourcePolicy

Control operating system level resource constraints for the child process, including CPU priority, processor
affinity, and working set limits.

### Builder Syntax

```csharp
var cmd = Cli.Wrap("ffmpeg").WithResourcePolicy(policy => policy
    .SetPriority(ProcessPriorityClass.High)
    .SetAffinity(0b1010)
    .SetMinWorkingSet(1024)
    .SetMaxWorkingSet(4096)
);
```

Each setter is optional. Omit any setter to leave that resource dimension at its default.

### Direct Syntax

Pass a `ResourcePolicy` record directly for cases where the policy is pre-built or loaded from configuration.

```csharp
var cmd = Cli.Wrap("ffmpeg").WithResourcePolicy(new ResourcePolicy(
    priority: ProcessPriorityClass.High,
    affinity: 0b1010,
    minWorkingSet: 1024,
    maxWorkingSet: 4096
));
```

### Resource Policy Options

| Option           | Type                   | Description                                         | Default          |
| ---------------- | ---------------------- | --------------------------------------------------- | ---------------- |
| `Priority`       | `ProcessPriorityClass` | Scheduling priority of the process                  | `Normal`         |
| `Affinity`       | `nint`                 | Bitmask of CPUs the process may run on              | All CPUs         |
| `MinWorkingSet`  | `nint`                 | Minimum physical memory (bytes) the OS should keep  | OS default       |
| `MaxWorkingSet`  | `nint`                 | Maximum physical memory (bytes) before page-outs    | OS default       |

> **WARNING**: Resource policy support varies across platforms. `Priority` is broadly supported on Windows, Linux,
> and macOS. `Affinity` is supported on Windows and Linux but not macOS. Working set limits (`MinWorkingSet`,
> `MaxWorkingSet`) are Windows-only. Attempting to set unsupported options may throw a `PlatformNotSupportedException`
> or be silently ignored, depending on the runtime and OS.

### Practical Usage

Resource policies are most useful for long-running or resource-intensive child processes (e.g., video encoding,
data processing, compilation) where limiting CPU or memory impact on the host system is important.

```csharp
// Limit a background encoding job to low priority and two specific CPU cores.
var cmd = Cli.Wrap("ffmpeg")
    .WithArguments(["-i", "input.mp4", "-c:v", "libx264", "output.mp4"])
    .WithResourcePolicy(policy => policy
        .SetPriority(ProcessPriorityClass.BelowNormal)
        .SetAffinity(0b0011) // cores 0 and 1 only
    );
```

## WithCredentials

Run the child process under a different user account. This is useful for service applications, build agents, or
any scenario requiring privilege separation.

### Builder Syntax

```csharp
var cmd = Cli.Wrap("git").WithCredentials(creds => creds
    .SetDomain("some_workspace")
    .SetUserName("johndoe")
    .SetPassword("securepassword123")
    .LoadUserProfile()
);
```

Each setter is optional. At minimum, provide `SetUserName` and `SetPassword` for the impersonation to take effect.

### Direct Syntax

```csharp
var cmd = Cli.Wrap("git").WithCredentials(new Credentials(
    domain: "some_workspace",
    userName: "johndoe",
    password: "securepassword123",
    loadUserProfile: true
));
```

### Credential Options

| Option            | Type     | Description                                               | Default  |
| ----------------- | -------- | --------------------------------------------------------- | -------- |
| `Domain`          | `string` | Windows domain or machine name for the user account       | `null`   |
| `UserName`        | `string` | Username for the account                                  | `null`   |
| `Password`        | `string` | Password for the account                                  | `null`   |
| `LoadUserProfile` | `bool`   | Load the user's profile (registry hive, env vars, etc.)   | `false`  |

> **WARNING**: Running a process under a different username is supported cross-platform (Windows, Linux, macOS).
> The `Domain` and `LoadUserProfile` options are Windows-only. On non-Windows platforms, setting these options
> may throw or be ignored. Always guard platform-specific credential options behind runtime checks when targeting
> multiple operating systems.

### Security Considerations

- Avoid hardcoding passwords in source code. Load credentials from environment variables, secret managers, or
  configuration providers.
- The `Password` value is passed to the operating system's process creation API. CliWrap does not persist or log it.
- When `LoadUserProfile` is `true`, the target user's registry hive is loaded, which can be slow on first use.

```csharp
// Load credentials from environment variables instead of hardcoding.
var cmd = Cli.Wrap("deploy-tool").WithCredentials(creds => creds
    .SetUserName(Environment.GetEnvironmentVariable("DEPLOY_USER")!)
    .SetPassword(Environment.GetEnvironmentVariable("DEPLOY_PASSWORD")!)
);
```

## WithValidation

Control how CliWrap interprets the exit code of the completed process. By default, CliWrap throws an exception
when the process exits with a non-zero code.

```csharp
// Default: throw on non-zero exit code.
var cmd = Cli.Wrap("git").WithValidation(CommandResultValidation.ZeroExitCode);

// Disable validation: never throw based on exit code.
var cmd = Cli.Wrap("git").WithValidation(CommandResultValidation.None);
```

### CommandResultValidation Options

| Value          | Behavior                                                                 |
| -------------- | ------------------------------------------------------------------------ |
| `ZeroExitCode` | Throw `CommandExecutionException` if exit code is not zero (default)     |
| `None`         | Accept any exit code without throwing                                    |

### When to Disable Validation

Many CLI tools use non-zero exit codes to signal non-error conditions.

- `grep` returns exit code 1 when no lines match the pattern.
- `diff` returns exit code 1 when files differ (not an error).
- `robocopy` uses exit codes 0 through 7 for various success states.
- Custom tools may use exit codes as structured output.

In these cases, set validation to `None` and inspect the exit code manually.

```csharp
var result = await Cli.Wrap("grep")
    .WithArguments(["-r", "TODO", "./src"])
    .WithValidation(CommandResultValidation.None)
    .ExecuteBufferedAsync();

if (result.ExitCode == 0)
{
    // Matches found; process result.StandardOutput.
}
else if (result.ExitCode == 1)
{
    // No matches found; not an error.
}
else
{
    // Actual error (permission denied, invalid arguments, etc.).
    throw new InvalidOperationException($"grep failed with exit code {result.ExitCode}");
}
```

### Exception Details

When validation is enabled and the process exits with a non-zero code, CliWrap throws a
`CommandExecutionException`. The exception includes:

- The exit code.
- The command that was executed (target path, arguments, working directory).
- Standard error output (when using `ExecuteBufferedAsync`), which often contains the tool's error message.

```csharp
try
{
    await Cli.Wrap("git")
        .WithArguments(["push", "origin", "main"])
        .ExecuteBufferedAsync();
}
catch (CommandExecutionException ex)
{
    Console.WriteLine($"Exit code: {ex.ExitCode}");
    Console.WriteLine($"Command: {ex.Command}");
}
```

## CommandResult

Every execution method returns a `CommandResult` (or `BufferedCommandResult` for `ExecuteBufferedAsync`).
`CommandResult` provides metadata about the completed process.

### Properties

| Property    | Type               | Description                                           |
| ----------- | ------------------ | ----------------------------------------------------- |
| `ExitCode`  | `int`              | Process exit code (0 typically indicates success)      |
| `IsSuccess` | `bool`             | `true` when `ExitCode` is 0                           |
| `StartTime` | `DateTimeOffset`   | Timestamp when the process started                    |
| `ExitTime`  | `DateTimeOffset`   | Timestamp when the process exited                     |
| `RunTime`   | `TimeSpan`         | Duration of execution (`ExitTime - StartTime`)        |

### Usage

```csharp
var result = await Cli.Wrap("dotnet")
    .WithArguments(["build", "--configuration", "Release"])
    .WithValidation(CommandResultValidation.None)
    .ExecuteAsync();

Console.WriteLine($"Exit code: {result.ExitCode}");
Console.WriteLine($"Success: {result.IsSuccess}");
Console.WriteLine($"Duration: {result.RunTime.TotalSeconds:F1}s");
Console.WriteLine($"Started: {result.StartTime:HH:mm:ss}");
Console.WriteLine($"Finished: {result.ExitTime:HH:mm:ss}");
```

`BufferedCommandResult` extends `CommandResult` with two additional properties:

| Property         | Type     | Description                              |
| ---------------- | -------- | ---------------------------------------- |
| `StandardOutput` | `string` | Captured standard output of the process  |
| `StandardError`  | `string` | Captured standard error of the process   |

```csharp
var result = await Cli.Wrap("dotnet")
    .WithArguments(["--version"])
    .ExecuteBufferedAsync();

Console.WriteLine($"Version: {result.StandardOutput.Trim()}");
Console.WriteLine($"Errors: {result.StandardError}");
Console.WriteLine($"Took: {result.RunTime.TotalMilliseconds}ms");
```

## Composing Configuration

The immutability of `Command` makes composition natural. Define a base command with shared configuration, then
derive specialized variants by calling additional `With*` methods. Each derived command is independent; changes
to one do not affect the others.

### Base Command Pattern

```csharp
var git = Cli.Wrap("git")
    .WithWorkingDirectory("/repo")
    .WithEnvironmentVariables(env => env
        .Set("GIT_AUTHOR_NAME", "CI Bot")
    );

// Derive specialized commands from the shared base.
var status = await git.WithArguments(["status"]).ExecuteBufferedAsync();
var log = await git.WithArguments(["log", "--oneline", "-5"]).ExecuteBufferedAsync();
```

The `git` base command captures the working directory and environment. Each derived command adds its own arguments
without modifying the base.

### Factory Method Pattern

Encapsulate base command creation in a factory method for consistent configuration across an application.

```csharp
public static Command CreateGitCommand(string repoPath)
{
    return Cli.Wrap("git")
        .WithWorkingDirectory(repoPath)
        .WithValidation(CommandResultValidation.ZeroExitCode)
        .WithEnvironmentVariables(env => env
            .Set("GIT_TERMINAL_PROMPT", "0")
            .Set("GIT_AUTHOR_NAME", "CI Bot")
            .Set("GIT_AUTHOR_EMAIL", "ci@example.com")
        );
}

// Usage at call sites.
var git = CreateGitCommand("/repo");
var result = await git.WithArguments(["diff", "--stat"]).ExecuteBufferedAsync();
```

### Multi-Tool Composition

Build base commands for multiple tools with shared conventions (e.g., working directory, environment).

```csharp
var workDir = "/home/user/project";

var dotnet = Cli.Wrap("dotnet")
    .WithWorkingDirectory(workDir)
    .WithEnvironmentVariables(env => env
        .Set("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
        .Set("DOTNET_NOLOGO", "1")
    );

var git = Cli.Wrap("git")
    .WithWorkingDirectory(workDir);

// Build, test, commit, push.
await dotnet.WithArguments(["build", "-c", "Release"]).ExecuteAsync();
await dotnet.WithArguments(["test", "--no-build"]).ExecuteAsync();
await git.WithArguments(["add", "."]).ExecuteAsync();
await git.WithArguments(["commit", "-m", "Release build"]).ExecuteAsync();
await git.WithArguments(["push"]).ExecuteAsync();
```

### Configuration Layering

Apply configuration in layers, from general to specific. Each layer returns a new command that inherits all
previous configuration.

```csharp
// Layer 1: target executable.
var cmd = Cli.Wrap("kubectl");

// Layer 2: shared environment.
cmd = cmd.WithEnvironmentVariables(env => env
    .Set("KUBECONFIG", "/etc/kubernetes/admin.conf")
);

// Layer 3: specific invocation.
cmd = cmd
    .WithArguments(["get", "pods", "-n", "production", "-o", "json"])
    .WithValidation(CommandResultValidation.None);

var result = await cmd.ExecuteBufferedAsync();
```

Each layer can be stored, shared, and reused independently. This pattern works well when configuration comes from
multiple sources (e.g., app settings, user input, runtime conditions).

### Conditional Configuration

Because each `With*` call returns a new `Command`, conditional configuration is straightforward with standard
control flow.

```csharp
var cmd = Cli.Wrap("dotnet")
    .WithArguments(["publish", "-c", "Release"]);

if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    cmd = cmd.WithEnvironmentVariables(env => env
        .Set("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "1")
    );
}

if (Directory.Exists("/output"))
{
    cmd = cmd.WithArguments(args => args
        .Add("publish")
        .Add("-c").Add("Release")
        .Add("-o").Add("/output")
    );
}

await cmd.ExecuteAsync();
```

## Quick Reference

| Method                       | Purpose                                  | Default                          |
| ---------------------------- | ---------------------------------------- | -------------------------------- |
| `WithArguments(string[])`    | Set arguments (auto-escaped)             | No arguments                     |
| `WithArguments(Action<>)`    | Set arguments via builder                | No arguments                     |
| `WithArguments(string)`      | Set raw argument string (no escaping)    | No arguments                     |
| `WithWorkingDirectory`       | Set child process working directory      | `Directory.GetCurrentDirectory`  |
| `WithEnvironmentVariables`   | Set or remove environment variables      | Inherit from parent              |
| `WithResourcePolicy`         | Set CPU priority, affinity, memory       | OS defaults                      |
| `WithCredentials`            | Run under a different user account       | Current user                     |
| `WithValidation`             | Set exit code validation behavior        | `ZeroExitCode`                   |
