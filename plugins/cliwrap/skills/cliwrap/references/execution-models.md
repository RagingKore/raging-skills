# CliWrap Execution Models

## Overview

CliWrap provides five distinct execution models for running external processes. Each model offers a different
trade-off between simplicity, memory efficiency, and streaming control. Choose the model that best fits the
data volume, latency requirements, and processing style of the target scenario.

All models share the same fluent command-building API. The difference lies in how output is consumed after the
process starts. Every model returns a task (or async enumerable / observable) that completes when the process
exits, and every model respects the configured validation, environment variables, credentials, and pipe
sources set on the command.

The five models, ordered from simplest to most advanced:

1. **ExecuteAsync**; fire and forget with exit metadata only.
2. **ExecuteBufferedAsync**; capture stdout and stderr as in-memory strings.
3. **Pipe-based ExecuteAsync**; route output to arbitrary `PipeTarget` destinations.
4. **ListenAsync** (pull-based event stream); consume events via `IAsyncEnumerable<CommandEvent>`.
5. **Observe** (push-based event stream); consume events via `IObservable<CommandEvent>`.

---

## ExecuteAsync (Basic)

The simplest execution model. It runs the process and returns a `CommandResult` containing only exit metadata.
Standard output and standard error are discarded by default (routed to `PipeTarget.Null`) unless explicit pipe
targets are configured separately.

### CommandResult Properties

| Property    | Type             | Description                                          |
| ----------- | ---------------- | ---------------------------------------------------- |
| ExitCode    | `int`            | The process exit code. Zero typically means success. |
| IsSuccess   | `bool`           | Whether the exit code indicates success.             |
| StartTime   | `DateTimeOffset` | UTC timestamp when the process started.              |
| ExitTime    | `DateTimeOffset` | UTC timestamp when the process exited.               |
| RunTime     | `TimeSpan`       | Wall-clock duration from start to exit.              |

### Basic Example

```csharp
using CliWrap;

var result = await Cli.Wrap("path/to/exe")
    .WithArguments(["--foo", "bar"])
    .WithWorkingDirectory("work/dir/path")
    .ExecuteAsync();

// Inspect the result
Console.WriteLine($"Exit code: {result.ExitCode}");
Console.WriteLine($"Success: {result.IsSuccess}");
Console.WriteLine($"Started: {result.StartTime}");
Console.WriteLine($"Exited: {result.ExitTime}");
Console.WriteLine($"Duration: {result.RunTime}");
```

### Accessing ProcessId Before Awaiting

`ExecuteAsync` returns a `CommandTask<CommandResult>`, not a plain `Task<CommandResult>`. The `CommandTask`
type exposes a `ProcessId` property that becomes available immediately after the process spawns, even before
the task is awaited. This is useful for logging, monitoring, or correlating processes externally.

```csharp
using CliWrap;

var task = Cli.Wrap("foo").ExecuteAsync();

// The process has already started at this point.
// Access the process ID without awaiting.
var processId = task.ProcessId;
Console.WriteLine($"Spawned process with ID: {processId}");

// Now await completion.
var result = await task;
Console.WriteLine($"Process {processId} exited with code {result.ExitCode}");
```

### When to Use

Use `ExecuteAsync` when only the exit code matters and stdout/stderr content is irrelevant. Typical scenarios
include running migrations, invoking build tools where the console output is informational only, or launching
fire-and-forget side-effect commands.

---

## ExecuteBufferedAsync

Captures the entire standard output and standard error streams into in-memory strings. This is the most
convenient model when the full text of both streams is needed after the process completes.

### Setup

Import the `CliWrap.Buffered` namespace to access the extension method.

```csharp
using CliWrap;
using CliWrap.Buffered;
```

### BufferedCommandResult Properties

`BufferedCommandResult` inherits all properties from `CommandResult` and adds:

| Property       | Type     | Description                             |
| -------------- | -------- | --------------------------------------- |
| StandardOutput | `string` | The entire captured stdout as a string. |
| StandardError  | `string` | The entire captured stderr as a string. |

### Basic Example

```csharp
using CliWrap;
using CliWrap.Buffered;

var result = await Cli.Wrap("path/to/exe")
    .WithArguments(["--foo", "bar"])
    .ExecuteBufferedAsync();

Console.WriteLine($"Exit code: {result.ExitCode}");
Console.WriteLine($"Stdout: {result.StandardOutput}");
Console.WriteLine($"Stderr: {result.StandardError}");
```

### Tuple Deconstruction

`BufferedCommandResult` supports deconstruction into a `(int ExitCode, string StandardOutput, string
StandardError)` tuple. This enables concise variable binding when all three values are needed.

```csharp
using CliWrap;
using CliWrap.Buffered;

var (exitCode, stdOut, stdErr) = await Cli.Wrap("foo").ExecuteBufferedAsync();

Console.WriteLine($"Exit: {exitCode}");
Console.WriteLine($"Out: {stdOut}");
Console.WriteLine($"Err: {stdErr}");
```

### Implicit String Conversion

When only standard output is needed, `BufferedCommandResult` supports implicit conversion to `string`. The
conversion yields the `StandardOutput` property. This makes one-liner patterns possible.

```csharp
using CliWrap;
using CliWrap.Buffered;

string stdOut = await Cli.Wrap("foo").ExecuteBufferedAsync();
Console.WriteLine(stdOut);
```

### Custom Encoding

By default, CliWrap decodes both streams using the system default encoding. Override this by passing an
explicit encoding. Two overloads are available: one that applies a single encoding to both streams, and one
that sets stdout and stderr encodings independently.

```csharp
using System.Text;
using CliWrap;
using CliWrap.Buffered;

// Single encoding for both streams.
var result = await Cli.Wrap("foo")
    .ExecuteBufferedAsync(Encoding.UTF8);

Console.WriteLine(result.StandardOutput);
```

```csharp
using System.Text;
using CliWrap;
using CliWrap.Buffered;

// Separate encodings: ASCII for stdout, UTF-8 for stderr.
var result = await Cli.Wrap("foo")
    .ExecuteBufferedAsync(Encoding.ASCII, Encoding.UTF8);

Console.WriteLine(result.StandardOutput);
Console.WriteLine(result.StandardError);
```

### Memory Risk Warning

`ExecuteBufferedAsync` accumulates the entire output in memory. If the target process produces large volumes
of output (megabytes or more) or emits binary data, this model can cause excessive memory allocation, GC
pressure, or `OutOfMemoryException`. For large or unbounded output, prefer the pipe-based model or one of the
event stream models instead.

### When to Use

Use `ExecuteBufferedAsync` when the expected output is small to moderate in size (a few kilobytes to a few
megabytes), the full text is needed after completion, and streaming is not required. Typical scenarios include
capturing version strings, reading JSON responses from CLI tools, or collecting diagnostic summaries.

---

## Pipe-based ExecuteAsync

Route standard output and standard error to one or more `PipeTarget` destinations while still using the base
`ExecuteAsync` model. This approach provides fine-grained control over where output goes without buffering
everything in memory at once.

### Pipe Targets

CliWrap provides several built-in `PipeTarget` factories:

| Factory                          | Description                                            |
| -------------------------------- | ------------------------------------------------------ |
| `PipeTarget.ToStringBuilder(sb)` | Append decoded text to a `StringBuilder`.              |
| `PipeTarget.ToStream(stream)`    | Write raw bytes to any writable `Stream`.              |
| `PipeTarget.ToFile(path)`        | Write raw bytes to a file at the given path.           |
| `PipeTarget.ToDelegate(action)`  | Invoke a delegate for each line of decoded text.       |
| `PipeTarget.Merge(targets...)`   | Fan out to multiple targets simultaneously.            |
| `PipeTarget.Null`                | Discard all data. This is the default for both streams.|

### StringBuilder Example

```csharp
using System.Text;
using CliWrap;

var stdOutBuffer = new StringBuilder();
var stdErrBuffer = new StringBuilder();

var result = await Cli.Wrap("path/to/exe")
    .WithArguments(["--foo", "bar"])
    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
    .ExecuteAsync();

Console.WriteLine($"Exit code: {result.ExitCode}");
Console.WriteLine($"Stdout: {stdOutBuffer}");
Console.WriteLine($"Stderr: {stdErrBuffer}");
```

### Stream Example

Write stdout directly to a file stream, avoiding any string allocation.

```csharp
using CliWrap;

await using var outputFile = File.Create("output.bin");

var result = await Cli.Wrap("path/to/exe")
    .WithStandardOutputPipe(PipeTarget.ToStream(outputFile))
    .ExecuteAsync();
```

### File Example

A shorthand for writing stdout to a file path without manually opening a stream.

```csharp
using CliWrap;

var result = await Cli.Wrap("path/to/exe")
    .WithStandardOutputPipe(PipeTarget.ToFile("output.txt"))
    .ExecuteAsync();
```

### Delegate Example

Process each line of stdout as it arrives. The delegate receives one line at a time.

```csharp
using CliWrap;

var result = await Cli.Wrap("path/to/exe")
    .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
    {
        Console.WriteLine($"[OUT] {line}");
    }))
    .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
    {
        Console.WriteLine($"[ERR] {line}");
    }))
    .ExecuteAsync();
```

### Merge Example

Fan out stdout to multiple targets simultaneously. For instance, capture stdout in a `StringBuilder` while
also writing it to a file.

```csharp
using System.Text;
using CliWrap;

var stdOutBuffer = new StringBuilder();

var result = await Cli.Wrap("path/to/exe")
    .WithStandardOutputPipe(PipeTarget.Merge(
        PipeTarget.ToStringBuilder(stdOutBuffer),
        PipeTarget.ToFile("stdout-copy.txt")
    ))
    .ExecuteAsync();

Console.WriteLine($"Captured stdout: {stdOutBuffer}");
```

### When to Use

Use pipe-based execution when output must be routed to specific destinations (files, streams, delegates) or
when multiple consumers need the same data. This model avoids the all-or-nothing buffering of
`ExecuteBufferedAsync` and provides more flexibility than discarding output entirely with basic `ExecuteAsync`.

---

## Pull-based Event Stream (ListenAsync)

Returns an `IAsyncEnumerable<CommandEvent>` that yields events as the process runs. The consumer pulls events
at its own pace, which means back-pressure is naturally applied. If the consumer is slow, the process will
block on writing to its output pipes until the consumer catches up.

### Setup

Import the `CliWrap.EventStream` namespace to access the extension method.

```csharp
using CliWrap;
using CliWrap.EventStream;
```

### Event Types

| Event Type                     | Property    | Type     | Emitted When                        |
| ------------------------------ | ----------- | -------- | ----------------------------------- |
| `StartedCommandEvent`          | `ProcessId` | `int`    | The process has started.            |
| `StandardOutputCommandEvent`   | `Text`      | `string` | A line of stdout text is available. |
| `StandardErrorCommandEvent`    | `Text`      | `string` | A line of stderr text is available. |
| `ExitedCommandEvent`           | `ExitCode`  | `int`    | The process has exited.             |

Events are emitted in chronological order. `StartedCommandEvent` is always first; `ExitedCommandEvent` is
always last. Output events are interleaved between them in the order they were produced by the process.

### Basic Example

```csharp
using CliWrap;
using CliWrap.EventStream;

var cmd = Cli.Wrap("foo").WithArguments(["bar"]);

await foreach (var cmdEvent in cmd.ListenAsync())
{
    switch (cmdEvent)
    {
        case StartedCommandEvent started:
            Console.WriteLine($"Process started; ID: {started.ProcessId}");
            break;
        case StandardOutputCommandEvent stdOut:
            Console.WriteLine($"Out> {stdOut.Text}");
            break;
        case StandardErrorCommandEvent stdErr:
            Console.WriteLine($"Err> {stdErr.Text}");
            break;
        case ExitedCommandEvent exited:
            Console.WriteLine($"Process exited; Code: {exited.ExitCode}");
            break;
    }
}
```

### Cancellation Support

Pass a `CancellationToken` to `ListenAsync` to cancel the process externally. When the token is triggered,
CliWrap sends a kill signal to the process and the enumeration terminates with an
`OperationCanceledException`.

```csharp
using CliWrap;
using CliWrap.EventStream;

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

var cmd = Cli.Wrap("long-running-tool");

await foreach (var cmdEvent in cmd.ListenAsync(cts.Token))
{
    switch (cmdEvent)
    {
        case StandardOutputCommandEvent stdOut:
            Console.WriteLine(stdOut.Text);
            break;
        case ExitedCommandEvent exited:
            Console.WriteLine($"Exited: {exited.ExitCode}");
            break;
    }
}
```

### Custom Encoding

Override the default encoding for stream decoding. Two overloads exist: one encoding for both streams, or
separate encodings for stdout and stderr.

```csharp
using System.Text;
using CliWrap;
using CliWrap.EventStream;

var cmd = Cli.Wrap("foo").WithArguments(["bar"]);

// Single encoding for both streams.
await foreach (var cmdEvent in cmd.ListenAsync(Encoding.UTF8))
{
    // Handle events...
}
```

```csharp
using System.Text;
using CliWrap;
using CliWrap.EventStream;

var cmd = Cli.Wrap("foo").WithArguments(["bar"]);

// Separate encodings: ASCII for stdout, UTF-8 for stderr.
await foreach (var cmdEvent in cmd.ListenAsync(Encoding.ASCII, Encoding.UTF8))
{
    // Handle events...
}
```

### When to Use

Use `ListenAsync` when events must be processed one at a time in an async iteration loop and back-pressure is
desirable. This is the natural choice for most real-time streaming scenarios in modern C# code. Typical use
cases include live log tailing, progress reporting, and incremental parsing of structured output lines.

---

## Push-based Event Stream (Observe)

Returns an `IObservable<CommandEvent>` (a cold observable) that emits the same four event types as
`ListenAsync`. The observable starts the process upon subscription and pushes events at the rate the process
produces them. There is no back-pressure; the observer must keep up or buffer internally.

### Setup

Import the `CliWrap.EventStream` namespace and install the `System.Reactive` NuGet package. The
`System.Reactive` package provides the LINQ operators and `ForEachAsync` extension method needed to subscribe
to the observable.

```xml
<PackageReference Include="CliWrap" Version="3.*" />
<PackageReference Include="System.Reactive" Version="6.*" />
```

```csharp
using System.Reactive;
using CliWrap;
using CliWrap.EventStream;
```

### Basic Example

```csharp
using System.Reactive;
using CliWrap;
using CliWrap.EventStream;

var cmd = Cli.Wrap("foo").WithArguments(["bar"]);

await cmd.Observe().ForEachAsync(cmdEvent =>
{
    switch (cmdEvent)
    {
        case StartedCommandEvent started:
            Console.WriteLine($"Process started; ID: {started.ProcessId}");
            break;
        case StandardOutputCommandEvent stdOut:
            Console.WriteLine($"Out> {stdOut.Text}");
            break;
        case StandardErrorCommandEvent stdErr:
            Console.WriteLine($"Err> {stdErr.Text}");
            break;
        case ExitedCommandEvent exited:
            Console.WriteLine($"Process exited; Code: {exited.ExitCode}");
            break;
    }
});
```

### Rx.NET Stream Transformation

The real power of the push-based model is integration with Rx.NET operators. Filter, transform, buffer,
throttle, and combine event streams using the full Reactive Extensions toolkit.

```csharp
using System.Reactive.Linq;
using CliWrap;
using CliWrap.EventStream;

var cmd = Cli.Wrap("foo").WithArguments(["--verbose"]);

// Filter to only stdout events and extract the text.
await cmd.Observe()
    .OfType<StandardOutputCommandEvent>()
    .Select(e => e.Text)
    .Where(text => text.Contains("WARNING"))
    .ForEachAsync(line =>
    {
        Console.WriteLine($"[WARN] {line}");
    });
```

```csharp
using System.Reactive.Linq;
using CliWrap;
using CliWrap.EventStream;

var cmd = Cli.Wrap("sensor-reader");

// Buffer stdout events in 5-second windows.
await cmd.Observe()
    .OfType<StandardOutputCommandEvent>()
    .Buffer(TimeSpan.FromSeconds(5))
    .ForEachAsync(batch =>
    {
        Console.WriteLine($"Received {batch.Count} readings in the last 5 seconds.");
    });
```

### Custom Encoding

Override the default encoding in the same way as `ListenAsync`. Two overloads exist.

```csharp
using System.Text;
using CliWrap;
using CliWrap.EventStream;

var cmd = Cli.Wrap("foo");

// Single encoding for both streams.
var observable = cmd.Observe(Encoding.UTF8);
```

```csharp
using System.Text;
using CliWrap;
using CliWrap.EventStream;

var cmd = Cli.Wrap("foo");

// Separate encodings: ASCII for stdout, UTF-8 for stderr.
var observable = cmd.Observe(Encoding.ASCII, Encoding.UTF8);
```

### When to Use

Use `Observe` when the consuming code already uses Reactive Extensions, when Rx operators (Buffer, Throttle,
CombineLatest, Merge) simplify the processing pipeline, or when multiple subscribers need to observe the same
process. Avoid this model if Rx.NET is not already a dependency; `ListenAsync` covers most streaming scenarios
without the extra package.

---

## Comparison Table

| Model                  | Returns                          | Back-pressure | Memory Risk | Best For                                |
| ---------------------- | -------------------------------- | ------------- | ----------- | --------------------------------------- |
| ExecuteAsync           | `CommandResult`                  | N/A           | None        | Exit code only; output discarded.       |
| ExecuteBufferedAsync   | `BufferedCommandResult`          | N/A           | High        | Small to moderate text output.          |
| Pipe-based ExecuteAsync| `CommandResult`                  | Depends       | Low         | Routing output to files or streams.     |
| ListenAsync            | `IAsyncEnumerable<CommandEvent>` | Yes           | Low         | Real-time line-by-line processing.      |
| Observe                | `IObservable<CommandEvent>`      | No            | Low         | Rx.NET pipelines; multi-subscriber.     |

**Back-pressure column notes.** "Depends" for the pipe-based model means it inherits the back-pressure
characteristics of the chosen `PipeTarget`. A `ToDelegate` target applies back-pressure because the delegate
runs synchronously per line. A `ToStream` target applies back-pressure if the destination stream blocks on
write. `ToStringBuilder` does not apply back-pressure because appending to a `StringBuilder` is effectively
instantaneous.

---

## Combining Piping with Other Execution Models

Pipe configuration (`WithStandardOutputPipe`, `WithStandardErrorPipe`) is independent of the execution model.
Pipe targets are always honored regardless of which execution method is called. This means it is possible to
route output to a file or delegate while simultaneously consuming the event stream or buffered result.

### Pipe Targets with ListenAsync

Attach a pipe target to capture stderr to a file while processing stdout events in an async loop.

```csharp
using CliWrap;
using CliWrap.EventStream;

var cmd = Cli.Wrap("build-tool")
    .WithArguments(["--project", "src/MyApp"])
    .WithStandardErrorPipe(PipeTarget.ToFile("build-errors.log"));

await foreach (var cmdEvent in cmd.ListenAsync())
{
    switch (cmdEvent)
    {
        case StandardOutputCommandEvent stdOut:
            Console.WriteLine($"[BUILD] {stdOut.Text}");
            break;
        case ExitedCommandEvent exited:
            if (!exited.ExitCode.Equals(0))
                Console.WriteLine("Build failed. See build-errors.log for details.");
            break;
    }
}
```

### Pipe Targets with ExecuteBufferedAsync

Capture stdout as a string (via `ExecuteBufferedAsync`) while simultaneously writing stderr to a delegate for
immediate logging.

```csharp
using CliWrap;
using CliWrap.Buffered;

var result = await Cli.Wrap("data-exporter")
    .WithArguments(["--format", "json"])
    .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
    {
        Console.Error.WriteLine($"[WARN] {line}");
    }))
    .ExecuteBufferedAsync();

// result.StandardOutput contains the JSON payload.
// Stderr warnings were logged in real time via the delegate.
Console.WriteLine($"Exported {result.StandardOutput.Length} characters of JSON.");
```

### Multiple Pipe Targets with Merge

Use `PipeTarget.Merge` to fan out stdout to both a `StringBuilder` and a file, then process the combined
result after execution completes.

```csharp
using System.Text;
using CliWrap;

var stdOutBuffer = new StringBuilder();

var result = await Cli.Wrap("report-generator")
    .WithStandardOutputPipe(PipeTarget.Merge(
        PipeTarget.ToStringBuilder(stdOutBuffer),
        PipeTarget.ToFile("report-output.txt")
    ))
    .ExecuteAsync();

Console.WriteLine($"Report length: {stdOutBuffer.Length} characters.");
Console.WriteLine("Report also saved to report-output.txt.");
```

---

## Choosing the Right Model

Follow this decision process to select the appropriate execution model:

- **Output is irrelevant.** Use `ExecuteAsync`. No memory overhead; only exit metadata returned.
- **Full output needed after completion; size is bounded and small.** Use `ExecuteBufferedAsync`. Simplest API
  for capturing text.
- **Output must go to a specific destination (file, stream, or delegate).** Use pipe-based `ExecuteAsync` with
  the appropriate `PipeTarget`.
- **Output must be processed line by line as it arrives; async/await is preferred.** Use `ListenAsync`. Natural
  back-pressure prevents memory buildup.
- **Output must be processed with Rx.NET operators or multiple subscribers are needed.** Use `Observe`. Adds a
  `System.Reactive` dependency but unlocks powerful stream composition.

When requirements overlap (for example, real-time logging and post-completion analysis), combine pipe targets
with any execution model. Pipe targets run in parallel with the chosen execution method, so there is no
conflict.

---

## Summary of Required Namespaces and Packages

| Model                  | Namespace              | Extra NuGet Package |
| ---------------------- | ---------------------- | ------------------- |
| ExecuteAsync           | `CliWrap`              | None                |
| ExecuteBufferedAsync   | `CliWrap.Buffered`     | None                |
| Pipe-based ExecuteAsync| `CliWrap`              | None                |
| ListenAsync            | `CliWrap.EventStream`  | None                |
| Observe                | `CliWrap.EventStream`  | `System.Reactive`   |
