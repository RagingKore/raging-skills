# CliWrap Pitfalls and Gotchas

A comprehensive reference covering every common mistake when using CliWrap in .NET projects.
Each pitfall includes a description, root cause, wrong code, correct code, and explanation.

## Buffered Execution with Large Output

`ExecuteBufferedAsync` reads the entire stdout and stderr streams into in-memory strings. When the wrapped process
produces a large volume of output, the buffered result accumulates without limit. Programs such as `docker logs`,
`journalctl`, database dumps, or binary file processors can emit gigabytes of data. The CLR will attempt to
allocate ever-larger strings until the process throws `OutOfMemoryException` or the system starts thrashing.

The problem is subtle because small test runs succeed. Production workloads with months of logs or large datasets
trigger the failure.

### Wrong

```csharp
// Buffers ALL output into a single string; fatal for large streams.
var result = await Cli.Wrap("docker")
    .WithArguments(["logs", "--follow", "my-container"])
    .ExecuteBufferedAsync();

string allLogs = result.StandardOutput; // potentially gigabytes
```

### Correct: line-by-line processing

```csharp
// Process each line as it arrives; constant memory usage.
await Cli.Wrap("docker")
    .WithArguments(["logs", "--follow", "my-container"])
    .ListenAsync((@event, ct) =>
    {
        if (@event is StandardOutputCommandEvent stdOut)
            ProcessLogLine(stdOut.Text);

        return default;
    });
```

### Correct: pipe to file

```csharp
// Stream output directly to a file without loading into memory.
await Cli.Wrap("docker")
    .WithArguments(["logs", "my-container"])
    .WithStandardOutputPipe(PipeTarget.ToFile("container.log"))
    .ExecuteAsync();
```

Reserve `ExecuteBufferedAsync` for commands whose output is known to be small and bounded. For anything unbounded,
use `ListenAsync` for line-by-line processing or `PipeTarget.ToFile` to stream directly to disk. When merging
multiple targets is needed, use `PipeTarget.Merge` to fan out to both a file and a delegate simultaneously.

## String Arguments and Injection

CliWrap offers two overloads of `WithArguments`. The raw string overload accepts a single pre-formatted string
and passes it verbatim to the process. It performs no escaping. When user-supplied values are interpolated into
that string, a malicious or accidental input can break quoting boundaries, inject extra arguments, or alter the
command's meaning entirely.

This is the CLI equivalent of SQL injection. A value containing quotes, spaces, semicolons, or shell
metacharacters can corrupt the argument list.

### Wrong

```csharp
// User input is interpolated directly into a raw argument string.
string userMessage = "Fix the \"bug\"; rm -rf /";

var cmd = Cli.Wrap("git")
    .WithArguments($"commit -m \"{userMessage}\"");
// Resulting arguments are malformed; quoting is broken.
```

### Correct: array overload

```csharp
// Each element becomes a single, properly escaped argument.
string userMessage = "Fix the \"bug\"; rm -rf /";

var cmd = Cli.Wrap("git")
    .WithArguments(["commit", "-m", userMessage]);
// CliWrap escapes the message correctly as one argument.
```

### Correct: builder overload

```csharp
// The builder API also escapes each value independently.
string userMessage = "Fix the \"bug\"; rm -rf /";

var cmd = Cli.Wrap("git")
    .WithArguments(args => args
        .Add("commit")
        .Add("-m")
        .Add(userMessage));
```

Both the array and builder overloads handle platform-specific escaping automatically. On Windows, arguments are
escaped following the MSVC C runtime convention. On Unix, each element is passed as a discrete `argv` entry with
no shell interpretation. Always prefer these overloads when any argument value comes from user input,
configuration, or external sources.

The raw string overload is acceptable only for fully static, developer-controlled argument strings where
readability benefits outweigh the risk.

## PipeTarget.Null vs Stream.Null

`PipeTarget.Null` tells CliWrap to not create a pipe for the stream at all. The child process receives no handle
for that stream. Most programs tolerate this, but some detect whether stdout is connected and change behavior
accordingly. Certain tools skip output formatting, switch to a different mode, or outright fail when stdout is
absent. Terminal-aware programs that query `isatty()` may also behave unexpectedly.

`PipeTarget.ToStream(Stream.Null)` keeps the pipe open and connected. The process writes to it normally. CliWrap
reads the data and discards it by writing into `Stream.Null`. The child process sees a valid, writable stdout
handle.

### Wrong

```csharp
// Closes the stdout handle entirely; some tools misbehave.
var result = await Cli.Wrap("some-tool")
    .WithStandardOutputPipe(PipeTarget.Null)
    .ExecuteAsync();
// "some-tool" may error because it detected no stdout.
```

### Correct

```csharp
// Keeps the pipe open but discards all data silently.
var result = await Cli.Wrap("some-tool")
    .WithStandardOutputPipe(PipeTarget.ToStream(Stream.Null))
    .ExecuteAsync();
// "some-tool" writes normally; CliWrap discards everything.
```

Use `PipeTarget.Null` only when confident the target process does not depend on a connected stdout. When in
doubt, prefer `PipeTarget.ToStream(Stream.Null)`. The performance difference is negligible. The same distinction
applies to stderr piping.

## Missing Cancellation Token

CliWrap methods accept `CancellationToken` parameters. When no token is passed, the execution has no timeout and
no way to be cancelled externally. If the child process hangs (waiting for input, stuck in a loop, deadlocked on
a resource), the calling code blocks indefinitely.

On application shutdown, the `await` never completes. The child process becomes orphaned and continues running in
the background, consuming system resources. In server scenarios, this leads to resource leaks that accumulate
over time.

### Wrong

```csharp
// No cancellation token; if "long-task" hangs, this awaits forever.
var result = await Cli.Wrap("long-task")
    .WithArguments(["--process", inputFile])
    .ExecuteAsync();
// Application shutdown cannot stop this; the process becomes orphaned.
```

### Correct: timeout-based token

```csharp
// Create a token that cancels after 30 seconds.
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

var result = await Cli.Wrap("long-task")
    .WithArguments(["--process", inputFile])
    .ExecuteAsync(cts.Token);
```

### Correct: linked token with application lifetime

```csharp
// Link to the host application's shutdown token and a timeout.
using var cts = CancellationTokenSource.CreateLinkedTokenSource(
    appLifetime.ApplicationStopping);
cts.CancelAfter(TimeSpan.FromMinutes(5));

var result = await Cli.Wrap("long-task")
    .WithArguments(["--process", inputFile])
    .ExecuteAsync(cts.Token);
```

Always pass a `CancellationToken`. Even for quick commands, a timeout token prevents edge cases where the process
unexpectedly blocks. In ASP.NET Core, link to `IHostApplicationLifetime.ApplicationStopping` so processes are
terminated on shutdown.

## Forgetting Command Immutability

`Command` objects in CliWrap are immutable. Every fluent method (`WithArguments`, `WithWorkingDirectory`,
`WithEnvironmentVariables`, etc.) returns a new `Command` instance. The original instance is never modified.
Developers accustomed to mutable builder patterns (such as `StringBuilder` or `IServiceCollection`) may call a
method and discard the return value, expecting the original object to be updated in place.

### Wrong

```csharp
var cmd = Cli.Wrap("dotnet");

// These calls return NEW commands; "cmd" is unchanged.
cmd.WithArguments(["build", "--configuration", "Release"]);
cmd.WithWorkingDirectory("/src/MyProject");

// Executes the original command with no arguments and default directory.
var result = await cmd.ExecuteAsync();
```

### Correct

```csharp
var cmd = Cli.Wrap("dotnet")
    .WithArguments(["build", "--configuration", "Release"])
    .WithWorkingDirectory("/src/MyProject");

// "cmd" now includes arguments and working directory.
var result = await cmd.ExecuteAsync();
```

### Correct: incremental composition

```csharp
var cmd = Cli.Wrap("dotnet");
cmd = cmd.WithArguments(["build", "--configuration", "Release"]);
cmd = cmd.WithWorkingDirectory("/src/MyProject");

var result = await cmd.ExecuteAsync();
```

The immutability design enables safe sharing and composition of command templates. A base command can be defined
once and extended differently for multiple use cases without mutation concerns. Treat `Command` like `string` or
`DateTime`: always capture the return value.

## Non-Zero Exit Code Exceptions

By default, CliWrap throws `CommandExecutionException` when the process exits with a non-zero code. This is
sensible for most programs where non-zero means failure. However, many standard tools use non-zero exit codes for
non-error conditions. `grep` returns 1 when no lines match. `diff` returns 1 when files differ (2 for errors).
`robocopy` returns various codes between 0 and 7 for different success statuses.

Wrapping the call in a try/catch to handle expected non-zero codes is verbose and obscures intent. It also
catches unrelated exceptions and makes the control flow harder to follow.

### Wrong

```csharp
// Using try/catch to handle an expected exit code; verbose and fragile.
try
{
    var result = await Cli.Wrap("grep")
        .WithArguments(["-r", "pattern", "/src"])
        .ExecuteBufferedAsync();

    ProcessMatches(result.StandardOutput);
}
catch (CommandExecutionException ex)
{
    if (ex.ExitCode == 1)
    {
        // No matches found; this is normal, not an error.
        ProcessMatches(string.Empty);
    }
    else
    {
        throw; // Actual error.
    }
}
```

### Correct

```csharp
// Disable exit code validation; inspect the code directly.
var result = await Cli.Wrap("grep")
    .WithArguments(["-r", "pattern", "/src"])
    .WithValidation(CommandResultValidation.None)
    .ExecuteBufferedAsync();

if (result.ExitCode == 0)
    ProcessMatches(result.StandardOutput);
else if (result.ExitCode == 1)
    ProcessMatches(string.Empty); // No matches; expected.
else
    throw new InvalidOperationException(
        $"grep failed with exit code {result.ExitCode}: {result.StandardError}");
```

Use `CommandResultValidation.None` whenever the process uses non-zero exit codes for non-error outcomes. Validate
the exit code explicitly with clear conditional logic. This is more readable, avoids exception overhead for
expected paths, and separates genuine errors from expected statuses.

## Swapping Cancellation Token Order

`ExecuteAsync` accepts two `CancellationToken` parameters:

```csharp
ExecuteAsync(CancellationToken forcefulCt, CancellationToken gracefulCt)
```

The first token triggers a forceful kill (`SIGKILL` / `TerminateProcess`). The second token triggers a graceful
shutdown signal (`SIGINT` / `Ctrl+C`). This order is unintuitive because most developers expect the "gentle"
option first.

Swapping the parameters means a graceful cancellation attempt sends `SIGKILL` instead of `SIGINT`. The process
is killed immediately with no opportunity to flush buffers, close file handles, or perform cleanup logic.

### Wrong

```csharp
using var gracefulCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
using var forcefulCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

// WRONG ORDER: graceful token in forceful position, forceful in graceful position.
await Cli.Wrap("server")
    .ExecuteAsync(gracefulCts.Token, forcefulCts.Token);
// After 10 seconds the process is killed with no warning;
// after 30 seconds a SIGINT is sent to an already-dead process.
```

### Correct

```csharp
using var gracefulCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
using var forcefulCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

// CORRECT ORDER: forceful first, graceful second.
await Cli.Wrap("server")
    .ExecuteAsync(forcefulCts.Token, gracefulCts.Token);
// After 10 seconds: SIGINT sent, process can clean up.
// After 30 seconds: SIGKILL sent if process is still alive.
```

The intended pattern is: graceful timeout < forceful timeout. The graceful token fires first, giving the process
a window to shut down cleanly. If it does not exit within the forceful timeout, the process is killed
unconditionally. Name variables clearly and add comments to prevent accidental swaps during refactoring.

## Blocking with .Result or .Wait()

CliWrap is fully asynchronous. All execution methods return `Task` or `IAsyncEnumerable`. Blocking on these with
`.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` can cause deadlocks in environments with a synchronization
context (ASP.NET pre-Core, WPF, WinForms, Blazor Server).

The deadlock occurs because the blocking call occupies the synchronization context thread. When the async
operation completes and attempts to resume on that context, it cannot because the thread is blocked waiting for
the result. Both sides wait on each other indefinitely.

Even in console applications where a deadlock is unlikely, blocking on async code wastes a thread pool thread
and defeats the purpose of the asynchronous API.

### Wrong

```csharp
// Blocking on async; deadlocks in UI/ASP.NET synchronization contexts.
var result = Cli.Wrap("dotnet")
    .WithArguments(["--info"])
    .ExecuteBufferedAsync()
    .GetAwaiter()
    .GetResult();
```

### Also wrong

```csharp
// .Result and .Wait() have the same deadlock potential.
var task = Cli.Wrap("dotnet")
    .WithArguments(["--info"])
    .ExecuteBufferedAsync();

task.Task.Wait();           // deadlock risk
var result = task.Task.Result; // deadlock risk
```

### Correct

```csharp
// Use async/await throughout the call chain.
var result = await Cli.Wrap("dotnet")
    .WithArguments(["--info"])
    .ExecuteBufferedAsync();
```

If calling from a synchronous method that cannot be made async (e.g., a legacy interface implementation),
consider running the work on a background thread to avoid capturing the synchronization context:

```csharp
// Last resort for synchronous callers; avoids capturing the sync context.
var result = Task.Run(async () =>
    await Cli.Wrap("dotnet")
        .WithArguments(["--info"])
        .ExecuteBufferedAsync()
).GetAwaiter().GetResult();
```

This workaround should be temporary. Prefer propagating async throughout the call stack.

## Disposing Streams Too Early

When piping input from a `Stream` or piping output to a `Stream`, the stream must remain open for the entire
duration of the process execution. CliWrap reads from input streams and writes to output streams concurrently
with the process. Disposing the stream before `ExecuteAsync` completes causes `ObjectDisposedException` or
silent data loss.

This commonly happens when using `using` statements that scope the stream to a block that ends before the
`await`.

### Wrong

```csharp
// The "using" block disposes the stream before ExecuteAsync completes.
Command cmd;

using (var input = File.OpenRead("data.txt"))
{
    cmd = Cli.Wrap("processor")
        .WithStandardInputPipe(PipeSource.FromStream(input));
}
// "input" is disposed here; execution has not started yet.

await cmd.ExecuteAsync(); // ObjectDisposedException
```

### Also wrong

```csharp
// Fire-and-forget without awaiting; stream disposed while process reads.
using var output = File.Create("result.txt");

var task = Cli.Wrap("generator")
    .WithStandardOutputPipe(PipeTarget.ToStream(output))
    .ExecuteAsync();

// Method returns; "output" is disposed while the process is still writing.
```

### Correct

```csharp
// Stream lifetime spans the entire execution.
await using var input = File.OpenRead("data.txt");

await Cli.Wrap("processor")
    .WithStandardInputPipe(PipeSource.FromStream(input))
    .ExecuteAsync();
// "input" is disposed only after execution completes.
```

### Correct: output stream

```csharp
// Output stream remains open until execution finishes.
await using var output = File.Create("result.txt");

await Cli.Wrap("generator")
    .WithStandardOutputPipe(PipeTarget.ToStream(output))
    .ExecuteAsync();
// "output" is disposed and flushed after all data is written.
```

Use `await using` (or `using` in synchronous-like patterns) placed so the scope covers the entire `await`. When
composing commands dynamically, keep stream references accessible until after execution completes. The same
applies to `MemoryStream` instances used as pipe targets; read from them only after awaiting execution.

## Encoding Mismatches

`ExecuteBufferedAsync` and `ListenAsync` decode process output using a default encoding (`Console.OutputEncoding`
on .NET, typically UTF-8 on modern systems). When the wrapped process writes in a different encoding (e.g.,
Windows-1252, Shift-JIS, UTF-16, or the system's legacy OEM code page), non-ASCII characters are garbled or
replaced with the replacement character.

This is common on Windows where many older tools output in the system's OEM code page (e.g., code page 437 or
850) rather than UTF-8. Database clients, legacy enterprise tools, and locale-specific programs are frequent
offenders.

### Wrong

```csharp
// Default encoding; garbled output if the process uses a different encoding.
var result = await Cli.Wrap("legacy-tool")
    .WithArguments(["export", "--format", "csv"])
    .ExecuteBufferedAsync();

// result.StandardOutput contains garbled characters like "caf\uFFFD" instead of "cafe".
```

### Correct: specify encoding for buffered execution

```csharp
// Match the encoding to the process output.
var result = await Cli.Wrap("legacy-tool")
    .WithArguments(["export", "--format", "csv"])
    .ExecuteBufferedAsync(
        encoding: Encoding.GetEncoding("windows-1252"));

// result.StandardOutput correctly decodes accented characters.
```

### Correct: specify encoding per stream

```csharp
// Different encodings for stdout and stderr.
var result = await Cli.Wrap("legacy-tool")
    .WithArguments(["export", "--format", "csv"])
    .ExecuteBufferedAsync(
        stdOutEncoding: Encoding.GetEncoding("windows-1252"),
        stdErrEncoding: Encoding.UTF8);
```

### Correct: specify encoding with ListenAsync

```csharp
// Encoding parameter for event-driven processing.
await Cli.Wrap("legacy-tool")
    .WithArguments(["export", "--format", "csv"])
    .ListenAsync(
        (@event, ct) =>
        {
            if (@event is StandardOutputCommandEvent stdOut)
                ProcessLine(stdOut.Text);

            return default;
        },
        encoding: Encoding.GetEncoding("windows-1252"));
```

To determine the correct encoding, check the tool's documentation, inspect raw output bytes with a hex editor,
or set the `LANG`/`LC_ALL` environment variable to force UTF-8 output on tools that respect locale settings:

```csharp
// Force UTF-8 output via environment variable.
var result = await Cli.Wrap("legacy-tool")
    .WithArguments(["export"])
    .WithEnvironmentVariables(env => env
        .Set("LANG", "en_US.UTF-8"))
    .ExecuteBufferedAsync(encoding: Encoding.UTF8);
```

On Windows, register the code page provider to access legacy encodings:

```csharp
// Required once at startup for non-UTF/ASCII encodings on .NET Core+.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var encoding = Encoding.GetEncoding("windows-1252");
```

Without this registration, `Encoding.GetEncoding("windows-1252")` throws on .NET Core and later. Register the
provider early in application startup before any CliWrap calls.
