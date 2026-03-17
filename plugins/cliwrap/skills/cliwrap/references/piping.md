# CliWrap Piping Reference

## Overview

CliWrap models stdin, stdout, and stderr as composable pipe objects. `PipeSource` supplies data to a process
via stdin. `PipeTarget` consumes data from stdout or stderr. The `|` operator provides syntactic sugar that
mirrors shell piping. All pipe configurations are immutable; each method returns a new command instance.

## PipeSource (stdin)

`PipeSource` controls what data flows into the standard input stream of the target process. Every factory
method returns an instance of `PipeSource` that can be passed to `WithStandardInputPipe` or used with the
`|` operator.

### Factory Methods

| Method                              | Description                                          |
| ----------------------------------- | ---------------------------------------------------- |
| `PipeSource.Null`                   | Provide empty stdin; no data is written               |
| `PipeSource.FromStream(stream)`     | Read from any readable `Stream`                       |
| `PipeSource.FromFile("path")`       | Read from a file on disk                              |
| `PipeSource.FromBytes(byte[])`      | Read from a byte array                                |
| `PipeSource.FromString("text")`     | Read from a string (UTF-8 encoded)                    |
| `PipeSource.FromCommand(command)`   | Read from the stdout of another `Command`             |

### PipeSource.Null

Provide an empty stdin stream. The process receives EOF immediately.

```csharp
var result = await Cli.Wrap("cat")
    .WithStandardInputPipe(PipeSource.Null)
    .ExecuteAsync();
```

### PipeSource.FromStream

Pipe data from any readable `Stream` into stdin. The caller is responsible for the lifetime of the stream.
Ensure the stream remains open until execution completes.

```csharp
await using var input = File.OpenRead("input.txt");

var result = await Cli.Wrap("wc")
    .WithArguments(["-l"])
    .WithStandardInputPipe(PipeSource.FromStream(input))
    .ExecuteAsync();
```

A `MemoryStream` works equally well for in-memory data.

```csharp
var data = Encoding.UTF8.GetBytes("line1\nline2\nline3\n");
using var memoryStream = new MemoryStream(data);

var result = await Cli.Wrap("sort")
    .WithStandardInputPipe(PipeSource.FromStream(memoryStream))
    .ExecuteAsync();
```

### PipeSource.FromFile

Open a file and stream its contents into stdin. CliWrap manages the file handle internally.

```csharp
var result = await Cli.Wrap("grep")
    .WithArguments(["error"])
    .WithStandardInputPipe(PipeSource.FromFile("/var/log/app.log"))
    .ExecuteAsync();
```

### PipeSource.FromBytes

Pipe raw bytes into stdin. Useful for binary data or pre-encoded content.

```csharp
byte[] payload = Encoding.UTF8.GetBytes("{\"key\": \"value\"}");

var result = await Cli.Wrap("jq")
    .WithArguments([".key"])
    .WithStandardInputPipe(PipeSource.FromBytes(payload))
    .ExecuteAsync();
```

### PipeSource.FromString

Pipe a string into stdin. The string is encoded as UTF-8 before writing.

```csharp
var json = """
    {
        "name": "CliWrap",
        "version": "3.7.0"
    }
    """;

var result = await Cli.Wrap("jq")
    .WithArguments([".name"])
    .WithStandardInputPipe(PipeSource.FromString(json))
    .ExecuteAsync();
```

### PipeSource.FromCommand

Pipe the stdout of one command into the stdin of another. This is the programmatic equivalent of shell
command chaining with `|`.

```csharp
var source = Cli.Wrap("echo")
    .WithArguments(["Hello from the source process"]);

var result = await Cli.Wrap("tr")
    .WithArguments(["a-z", "A-Z"])
    .WithStandardInputPipe(PipeSource.FromCommand(source))
    .ExecuteBufferedAsync();

// result.StandardOutput contains "HELLO FROM THE SOURCE PROCESS"
```

## PipeTarget (stdout and stderr)

`PipeTarget` controls where the process writes its standard output and standard error streams. Every factory
method returns an instance of `PipeTarget` that can be passed to `WithStandardOutputPipe`,
`WithStandardErrorPipe`, or used with the `|` operator.

### Factory Methods

| Method                                                        | Description                                  |
| ------------------------------------------------------------- | -------------------------------------------- |
| `PipeTarget.Null`                                             | Discard output; closes the stream handle      |
| `PipeTarget.ToStream(stream)`                                 | Write to any writable `Stream`                |
| `PipeTarget.ToFile("path")`                                   | Write to a file on disk                       |
| `PipeTarget.ToStringBuilder(sb)`                              | Append text to a `StringBuilder`              |
| `PipeTarget.ToDelegate(Action<string>)`                       | Invoke a callback for each line               |
| `PipeTarget.ToDelegate(Func<string, Task>)`                   | Invoke an async callback for each line        |
| `PipeTarget.ToDelegate(Func<string, CancellationToken, Task>)` | Invoke an async callback with cancellation  |
| `PipeTarget.Merge(target1, target2, ...)`                     | Replicate output to multiple targets at once  |

### PipeTarget.Null

Discard all output from the stream.

```csharp
var result = await Cli.Wrap("echo")
    .WithArguments(["discarded"])
    .WithStandardOutputPipe(PipeTarget.Null)
    .ExecuteAsync();
```

**Warning about PipeTarget.Null.** This target closes the underlying stream handle entirely rather than
consuming and discarding the bytes. Some processes detect a closed handle and alter their behavior. For
example, a process that checks whether stdout is connected to a terminal or pipe may skip output formatting,
reduce buffering, or fail outright when the handle is closed.

To discard data while keeping the stream handle open, use `PipeTarget.ToStream(Stream.Null)` instead.
`Stream.Null` is a built-in .NET stream that accepts writes silently without storing anything, but the handle
remains valid from the child process perspective.

```csharp
// Safe discard: handle stays open, data is consumed and thrown away
var result = await Cli.Wrap("some-sensitive-tool")
    .WithStandardOutputPipe(PipeTarget.ToStream(Stream.Null))
    .ExecuteAsync();
```

Use `PipeTarget.Null` when the process does not care about the handle state and performance matters. Use
`PipeTarget.ToStream(Stream.Null)` when the process might behave differently with a closed handle.

### PipeTarget.ToStream

Write output to any writable `Stream`. The caller owns the stream lifetime.

```csharp
await using var output = File.Create("output.txt");

await Cli.Wrap("ls")
    .WithArguments(["-la"])
    .WithStandardOutputPipe(PipeTarget.ToStream(output))
    .ExecuteAsync();
```

### PipeTarget.ToFile

Write output directly to a file. CliWrap manages the file handle internally.

```csharp
await Cli.Wrap("ls")
    .WithArguments(["-la", "/usr"])
    .WithStandardOutputPipe(PipeTarget.ToFile("listing.txt"))
    .ExecuteAsync();
```

### PipeTarget.ToStringBuilder

Append output text to a `StringBuilder`. Useful for capturing output without `ExecuteBufferedAsync`.

```csharp
var stdOut = new StringBuilder();
var stdErr = new StringBuilder();

await Cli.Wrap("dotnet")
    .WithArguments(["build"])
    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
    .ExecuteAsync();

Console.WriteLine($"Build output: {stdOut}");
Console.WriteLine($"Build errors: {stdErr}");
```

### PipeTarget.ToDelegate (synchronous)

Invoke a synchronous callback for each line of output. Lines are split on newline boundaries.

```csharp
await Cli.Wrap("ping")
    .WithArguments(["-c", "4", "localhost"])
    .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
    {
        Console.WriteLine($"[PING] {line}");
    }))
    .ExecuteAsync();
```

### PipeTarget.ToDelegate (async)

Invoke an async callback for each line. Enables non-blocking processing of output.

```csharp
await Cli.Wrap("tail")
    .WithArguments(["-f", "/var/log/syslog"])
    .WithStandardOutputPipe(PipeTarget.ToDelegate(async line =>
    {
        await File.AppendAllTextAsync("filtered.log", line + Environment.NewLine);
    }))
    .ExecuteAsync();
```

### PipeTarget.ToDelegate (async with CancellationToken)

Invoke an async callback that receives a `CancellationToken`. The token is triggered when the execution is
cancelled, allowing the delegate to abort long-running work.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

await Cli.Wrap("long-running-tool")
    .WithStandardOutputPipe(PipeTarget.ToDelegate(async (line, ct) =>
    {
        await ProcessLineAsync(line, ct);
    }))
    .ExecuteAsync(cts.Token);
```

### PipeTarget.Merge

Replicate the same output stream to multiple targets simultaneously. Every byte written to stdout (or stderr)
is forwarded to all targets in the merge set.

```csharp
var stdOutBuffer = new StringBuilder();

var cmd = Cli.Wrap("dotnet")
    .WithArguments(["test"])
    .WithStandardOutputPipe(PipeTarget.Merge(
        PipeTarget.ToFile("test-results.txt"),
        PipeTarget.ToStringBuilder(stdOutBuffer),
        PipeTarget.ToDelegate(Console.WriteLine)
    ));

await cmd.ExecuteAsync();

// stdOutBuffer, test-results.txt, and console all contain the same output
```

Merge targets for stderr as well.

```csharp
var errorLog = new StringBuilder();

await Cli.Wrap("some-tool")
    .WithStandardErrorPipe(PipeTarget.Merge(
        PipeTarget.ToFile("errors.log"),
        PipeTarget.ToStringBuilder(errorLog)
    ))
    .ExecuteAsync();
```

## Pipe Operator `|`

CliWrap overloads the C# `|` (bitwise OR) operator to provide shell-like piping syntax. The operator is
syntactic sugar over `WithStandardInputPipe` and `WithStandardOutputPipe`. All combinations shown below
produce immutable `Command` instances ready for execution.

### String to stdin

Pipe a string directly into a command. Equivalent to `WithStandardInputPipe(PipeSource.FromString(...))`.

```csharp
var cmd = "Hello world" | Cli.Wrap("cat");

var result = await cmd.ExecuteBufferedAsync();
// result.StandardOutput == "Hello world"
```

### Stream to stdin

Pipe a readable `Stream` into a command. Equivalent to
`WithStandardInputPipe(PipeSource.FromStream(...))`.

```csharp
await using var input = File.OpenRead("input.txt");

var cmd = input | Cli.Wrap("wc");
await cmd.ExecuteAsync();
```

### HTTP stream to stdin

Any `Stream` works, including network streams from `HttpClient`.

```csharp
using var httpClient = new HttpClient();
await using var input = await httpClient.GetStreamAsync(
    "https://example.com/image.png"
);

var cmd = input | Cli.Wrap("convert")
    .WithArguments(["png:-", "output.jpg"]);

await cmd.ExecuteAsync();
```

### stdout to StringBuilder

Pipe stdout into a `StringBuilder`. Equivalent to
`WithStandardOutputPipe(PipeTarget.ToStringBuilder(...))`.

```csharp
var buffer = new StringBuilder();

var cmd = Cli.Wrap("echo")
    .WithArguments(["captured text"]) | buffer;

await cmd.ExecuteAsync();
// buffer.ToString() == "captured text"
```

### Command chaining

Pipe stdout of one command into stdin of the next. Chain as many commands as needed. Equivalent to
`WithStandardInputPipe(PipeSource.FromCommand(...))`.

```csharp
var cmd = Cli.Wrap("echo")
    .WithArguments(["hello world"]) |
    Cli.Wrap("tr")
        .WithArguments(["a-z", "A-Z"]) |
    Cli.Wrap("rev");

var result = await cmd.ExecuteBufferedAsync();
// result.StandardOutput == "DLROW OLLEH"
```

### stdout and stderr to delegates

Pipe stdout and stderr to separate callbacks using a tuple of delegates.

```csharp
var cmd = Cli.Wrap("dotnet")
    .WithArguments(["build"]) |
    (Console.WriteLine, Console.Error.WriteLine);

await cmd.ExecuteAsync();
```

### stdout and stderr to parent console streams

Pipe both streams to the parent process console. Useful for forwarding subprocess output transparently.

```csharp
await using var stdOut = Console.OpenStandardOutput();
await using var stdErr = Console.OpenStandardError();

var cmd = Cli.Wrap("dotnet")
    .WithArguments(["run"]) | (stdOut, stdErr);

await cmd.ExecuteAsync();
```

### stdout and stderr to PipeTarget tuple

Pipe stdout and stderr to separate `PipeTarget` instances using a tuple.

```csharp
var outputBuffer = new StringBuilder();

var cmd = Cli.Wrap("dotnet")
    .WithArguments(["test"]) |
    (PipeTarget.ToFile("output.txt"), PipeTarget.ToStringBuilder(outputBuffer));

await cmd.ExecuteAsync();
// stdout goes to output.txt, stderr goes to outputBuffer
```

### Complex multi-stage pipeline

Combine string input, multiple command stages, and delegate output in a single expression.

```csharp
var cmd =
    "Hello world" |
    Cli.Wrap("tr")
        .WithArguments(["a-z", "A-Z"]) |
    Cli.Wrap("rev") |
    (Console.WriteLine, Console.Error.WriteLine);

await cmd.ExecuteAsync();
// Console prints "DLROW OLLEH"
```

### Full pipeline with PipeSource and PipeTarget

Combine explicit `PipeSource` on the left and `PipeTarget` on the right.

```csharp
var cmd = PipeSource.FromFile("input.txt") |
    Cli.Wrap("sort") |
    Cli.Wrap("uniq") |
    PipeTarget.ToFile("sorted-unique.txt");

await cmd.ExecuteAsync();
```

## Configuration Method Equivalents

Every pipe operator expression has an equivalent using explicit configuration methods. The table below maps
each pattern.

| Pipe Operator Expression                           | Equivalent Configuration Method                              |
| -------------------------------------------------- | ------------------------------------------------------------ |
| `"text" \| Cli.Wrap("foo")`                        | `.WithStandardInputPipe(PipeSource.FromString("text"))`      |
| `stream \| Cli.Wrap("foo")`                        | `.WithStandardInputPipe(PipeSource.FromStream(stream))`      |
| `Cli.Wrap("foo") \| stringBuilder`                 | `.WithStandardOutputPipe(PipeTarget.ToStringBuilder(sb))`    |
| `Cli.Wrap("foo") \| Cli.Wrap("bar")`               | `.WithStandardInputPipe(PipeSource.FromCommand(foo))`        |
| `Cli.Wrap("foo") \| (stdOut, stdErr)`              | `.WithStandardOutputPipe(...).WithStandardErrorPipe(...)`    |

### Full explicit example

The following two code blocks produce identical behavior.

Using the pipe operator:

```csharp
await using var input = File.OpenRead("input.txt");
await using var output = File.Create("output.txt");

var cmd = input | Cli.Wrap("sort") | Cli.Wrap("uniq");
await cmd.ExecuteAsync();
```

Using configuration methods:

```csharp
await using var input = File.OpenRead("input.txt");
await using var output = File.Create("output.txt");

var cmd = Cli.Wrap("uniq")
    .WithStandardInputPipe(
        PipeSource.FromCommand(
            Cli.Wrap("sort")
                .WithStandardInputPipe(PipeSource.FromStream(input))
        )
    )
    .WithStandardOutputPipe(PipeTarget.ToStream(output));

await cmd.ExecuteAsync();
```

### stdin and stdout configured together

```csharp
await using var input = File.OpenRead("input.txt");
await using var output = File.Create("output.txt");

await Cli.Wrap("tr")
    .WithArguments(["a-z", "A-Z"])
    .WithStandardInputPipe(PipeSource.FromStream(input))
    .WithStandardOutputPipe(PipeTarget.ToStream(output))
    .ExecuteAsync();
```

### stdin, stdout, and stderr configured together

```csharp
var stdOutBuffer = new StringBuilder();
var stdErrBuffer = new StringBuilder();

await Cli.Wrap("dotnet")
    .WithArguments(["build", "--no-restore"])
    .WithStandardInputPipe(PipeSource.Null)
    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
    .ExecuteAsync();

Console.WriteLine($"Output: {stdOutBuffer}");
Console.WriteLine($"Errors: {stdErrBuffer}");
```

## Combining Pipes with Execution Models

Piping is orthogonal to execution models. Any piped command can use `ExecuteAsync`,
`ExecuteBufferedAsync`, or `ListenAsync`. The execution model determines how results are returned; piping
determines where data flows.

### Pipes with ExecuteAsync

`ExecuteAsync` returns a `CommandResult` with exit code and timing. Pipe targets receive the data during
execution.

```csharp
var buffer = new StringBuilder();

var result = await (
    PipeSource.FromFile("input.txt") |
    Cli.Wrap("grep").WithArguments(["error"]) |
    PipeTarget.ToStringBuilder(buffer)
).ExecuteAsync();

Console.WriteLine($"Exit code: {result.ExitCode}");
Console.WriteLine($"Matched lines: {buffer}");
```

### Pipes with ExecuteBufferedAsync

`ExecuteBufferedAsync` captures stdout and stderr into `BufferedCommandResult`. Explicit pipe targets still
receive data; buffered output captures whatever is not redirected elsewhere.

```csharp
var result = await (
    PipeSource.FromFile("data.json") |
    Cli.Wrap("jq").WithArguments([".items[]"])
).ExecuteBufferedAsync();

Console.WriteLine($"Parsed items: {result.StandardOutput}");
```

When combining `ExecuteBufferedAsync` with explicit stdout pipe targets, the buffered result captures the
output as well. Both destinations receive the data.

```csharp
var buffer = new StringBuilder();

var result = await (
    Cli.Wrap("echo").WithArguments(["hello"]) |
    PipeTarget.ToStringBuilder(buffer)
).ExecuteBufferedAsync();

// buffer and result.StandardOutput both contain "hello"
```

### Pipes with ListenAsync

`ListenAsync` produces an `IAsyncEnumerable<CommandEvent>` for event-driven processing. Combine with piped
stdin for streaming workflows.

```csharp
var cmd = PipeSource.FromFile("input.txt") | Cli.Wrap("sort");

await foreach (var cmdEvent in cmd.ListenAsync())
{
    switch (cmdEvent)
    {
        case StandardOutputCommandEvent stdOutEvent:
            Console.WriteLine($"[OUT] {stdOutEvent.Text}");
            break;
        case StandardErrorCommandEvent stdErrEvent:
            Console.WriteLine($"[ERR] {stdErrEvent.Text}");
            break;
        case ExitedCommandEvent exitEvent:
            Console.WriteLine($"[EXIT] {exitEvent.ExitCode}");
            break;
    }
}
```

### Pipes with ObserveAsync

`ObserveAsync` pushes events into `IObservable<CommandEvent>` for Rx-based processing.

```csharp
var cmd = PipeSource.FromString("banana\napple\ncherry") | Cli.Wrap("sort");

await cmd.Observe().ForEachAsync(cmdEvent =>
{
    if (cmdEvent is StandardOutputCommandEvent stdOut)
    {
        Console.WriteLine($"Sorted: {stdOut.Text}");
    }
});
```

## Real-World Piping Examples

### Processing a large log file through grep and wc

Count the number of error lines in a large log file by chaining `grep` and `wc`. This avoids loading the
entire file into .NET memory.

```csharp
var result = await (
    PipeSource.FromFile("/var/log/application.log") |
    Cli.Wrap("grep").WithArguments(["-i", "error"]) |
    Cli.Wrap("wc").WithArguments(["-l"])
).ExecuteBufferedAsync();

var errorCount = int.Parse(result.StandardOutput.Trim());
Console.WriteLine($"Found {errorCount} error lines");
```

To also capture the matching lines for inspection, use `PipeTarget.Merge` on an intermediate step.

```csharp
var matchedLines = new StringBuilder();
var lineCount = new StringBuilder();

// Run grep, split output to both a file and the next pipe stage
var grepCmd = Cli.Wrap("grep")
    .WithArguments(["-i", "error"])
    .WithStandardInputPipe(PipeSource.FromFile("/var/log/application.log"))
    .WithStandardOutputPipe(PipeTarget.Merge(
        PipeTarget.ToStringBuilder(matchedLines),
        PipeTarget.ToFile("errors-snapshot.txt")
    ));

await grepCmd.ExecuteAsync();

Console.WriteLine($"Found {matchedLines.ToString().Split('\n').Length - 1} errors");
Console.WriteLine($"Full error lines saved to errors-snapshot.txt");
```

### Converting an image with ImageMagick

Convert a PNG image to JPEG using ImageMagick `convert`. Read from a file stream and write to another file
stream. The `-` arguments instruct ImageMagick to read from stdin and write to stdout.

```csharp
await using var input = File.OpenRead("photo.png");
await using var output = File.Create("photo.jpg");

await (
    input |
    Cli.Wrap("convert")
        .WithArguments(["png:-", "-quality", "85", "jpg:-"]) |
    PipeTarget.ToStream(output)
).ExecuteAsync();
```

Resize and convert in a single pipeline.

```csharp
await using var input = File.OpenRead("large-photo.png");

await (
    input |
    Cli.Wrap("convert")
        .WithArguments(["png:-", "-resize", "800x600", "-quality", "90", "jpg:-"]) |
    PipeTarget.ToFile("thumbnail.jpg")
).ExecuteAsync();
```

### Streaming an HTTP response through a CLI tool

Download a CSV file from a remote server and sort it without writing the original to disk.

```csharp
using var httpClient = new HttpClient();
await using var csvStream = await httpClient.GetStreamAsync(
    "https://data.example.com/reports/sales.csv"
);

var sorted = new StringBuilder();

await (
    csvStream |
    Cli.Wrap("sort")
        .WithArguments(["-t", ",", "-k", "2", "-n"]) |
    PipeTarget.ToStringBuilder(sorted)
).ExecuteAsync();

Console.WriteLine(sorted.ToString());
```

### Compressing a directory listing

Generate a directory listing, compress it with gzip, and write the compressed output to a file.

```csharp
await (
    Cli.Wrap("ls")
        .WithArguments(["-laR", "/usr/local"]) |
    Cli.Wrap("gzip")
        .WithArguments(["-9"]) |
    PipeTarget.ToFile("listing.gz")
).ExecuteAsync();
```

### Database dump with real-time progress

Pipe a database dump through a processing tool while monitoring progress via stderr.

```csharp
var progress = new StringBuilder();

await Cli.Wrap("pg_dump")
    .WithArguments(["--dbname=mydb", "--format=plain"])
    .WithStandardOutputPipe(PipeTarget.ToFile("backup.sql"))
    .WithStandardErrorPipe(PipeTarget.Merge(
        PipeTarget.ToStringBuilder(progress),
        PipeTarget.ToDelegate(line =>
        {
            Console.WriteLine($"[pg_dump] {line}");
        })
    ))
    .ExecuteAsync();
```

### Chaining multiple transformations

Apply multiple text transformations in sequence. Convert to uppercase, remove blank lines, number the
remaining lines, and capture the result.

```csharp
var result = await (
    PipeSource.FromFile("raw-notes.txt") |
    Cli.Wrap("tr")
        .WithArguments(["a-z", "A-Z"]) |
    Cli.Wrap("grep")
        .WithArguments(["-v", "^$"]) |
    Cli.Wrap("nl")
        .WithArguments(["-ba"])
).ExecuteBufferedAsync();

await File.WriteAllTextAsync("formatted-notes.txt", result.StandardOutput);
```

## Default Pipe Configuration

When no pipes are configured explicitly, CliWrap applies these defaults.

| Stream | Default                                                                                    |
| ------ | ------------------------------------------------------------------------------------------ |
| stdin  | `PipeSource.Null`; the process receives no input                                            |
| stdout | Depends on execution model; `ExecuteBufferedAsync` captures it, `ExecuteAsync` discards it  |
| stderr | Depends on execution model; `ExecuteBufferedAsync` captures it, `ExecuteAsync` discards it  |

Override any default by setting the corresponding pipe. Unset pipes retain their defaults.

```csharp
// Only override stderr; stdin and stdout keep their defaults
await Cli.Wrap("dotnet")
    .WithArguments(["build"])
    .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
    {
        Console.Error.WriteLine($"[BUILD] {line}");
    }))
    .ExecuteAsync();
```

## Summary of Key Points

- `PipeSource` feeds data into stdin. Use factory methods matching the data source type.
- `PipeTarget` captures data from stdout or stderr. Use factory methods matching the destination type.
- `PipeTarget.Null` closes the stream handle. Use `PipeTarget.ToStream(Stream.Null)` to discard data safely.
- `PipeTarget.Merge` replicates a single stream to multiple targets simultaneously.
- The `|` operator mirrors shell piping syntax. Every `|` expression has an equivalent configuration method.
- Piping composes freely with all execution models: `ExecuteAsync`, `ExecuteBufferedAsync`, `ListenAsync`,
  and `ObserveAsync`.
- Command chaining via `|` or `PipeSource.FromCommand` runs processes concurrently; data flows as it becomes
  available.
- Explicit pipe configuration is immutable. Each call returns a new `Command` instance.
