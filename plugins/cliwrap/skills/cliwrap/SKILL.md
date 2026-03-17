---
name: cliwrap
description: >-
  This skill should be used when the user asks to "run a CLI command", "execute a process",
  "pipe command output", "use CliWrap", "wrap a command-line tool", "stream process output",
  "cancel a running process", "replace System.Diagnostics.Process", "shell out from .NET", or
  "run an external tool from C#". Also triggers when code imports `CliWrap`, `CliWrap.Buffered`,
  or `CliWrap.EventStream`, or when the user works with process execution, command piping, or
  async CLI invocation in .NET.
---

# CliWrap

CliWrap provides an airtight abstraction over `System.Diagnostics.Process` with a fluent, immutable API. It
eliminates the deadlock-prone, error-prone patterns of raw `Process` usage and replaces them with async-first,
pipe-oriented command execution.

## Installation

```sh
dotnet add package CliWrap
```

Import `CliWrap.Buffered` for buffered execution or `CliWrap.EventStream` for event stream execution.

## Core Concepts

### Immutability

`Command` is immutable. Every `With*` method returns a new instance; the original is unchanged. Store configured
commands in variables and reuse them safely across calls.

```csharp
var baseCmd = Cli.Wrap("git").WithWorkingDirectory("/repo");
var status = baseCmd.WithArguments(["status"]);
var log = baseCmd.WithArguments(["log", "--oneline", "-5"]);
```

### Choosing an Execution Model

| Model                    | Use when                                          | Namespace             |
| ------------------------ | ------------------------------------------------- | --------------------- |
| `ExecuteAsync`           | Only exit code and timing matter                  | `CliWrap`             |
| `ExecuteBufferedAsync`   | Need stdout/stderr as strings and output is small | `CliWrap.Buffered`    |
| Pipe-based `ExecuteAsync`| Need to stream large output to files or delegates | `CliWrap`             |
| `ListenAsync`            | Need line-by-line processing with back-pressure   | `CliWrap.EventStream` |
| `Observe`                | Need Rx integration or push-based processing      | `CliWrap.EventStream` |

Consult `references/execution-models.md` for full examples of each model.

### Arguments: Prefer Array or Builder Syntax

Never pass user input via the raw string overload; it is vulnerable to injection and quoting errors.

```csharp
// Safe: array syntax (auto-escaped)
var cmd = Cli.Wrap("git").WithArguments(["commit", "-m", userMessage]);

// Safe: builder syntax (auto-escaped, supports conditionals)
var cmd = Cli.Wrap("git").WithArguments(args =>
{
    args.Add("push");
    if (force) args.Add("--force");
});
```

See `references/configuration.md` for all configuration options including environment variables, credentials,
working directory, resource policy, and validation.

## Piping

CliWrap models I/O as `PipeSource` (stdin) and `PipeTarget` (stdout/stderr). The `|` operator provides shorthand.

```csharp
// Pipe file to stdin, capture stdout to StringBuilder
var buffer = new StringBuilder();
await (File.OpenRead("input.txt") | Cli.Wrap("sort") | buffer).ExecuteAsync();

// Chain commands: equivalent to `cat data.csv | grep ERROR | wc -l`
var cmd = PipeSource.FromFile("data.csv")
    | Cli.Wrap("grep").WithArguments(["ERROR"])
    | Cli.Wrap("wc").WithArguments(["-l"]);
```

Key `PipeTarget` options: `Null`, `ToStream`, `ToFile`, `ToStringBuilder`, `ToDelegate`, `Merge`.
Key `PipeSource` options: `Null`, `FromStream`, `FromFile`, `FromBytes`, `FromString`, `FromCommand`.

Consult `references/piping.md` for all piping patterns, the merge target, and real-world examples.

## Cancellation and Timeouts

CliWrap supports a dual-token pattern: a graceful token (sends Ctrl+C interrupt) and a forceful token (kills the
process). Always provide a cancellation token to avoid orphaned processes.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var result = await Cli.Wrap("long-task").ExecuteAsync(cts.Token);
```

For processes that handle SIGINT, use the two-token overload to allow graceful shutdown with a forceful fallback.

Consult `references/cancellation.md` for the dual-token pattern, method-level wrappers, and timeout strategies.

## Event Streams

### Pull-based: `ListenAsync`

Returns `IAsyncEnumerable<CommandEvent>`. The consumer controls the pace; the process blocks when the consumer is
slow. Ideal for line-by-line processing with back-pressure.

```csharp
await foreach (var e in Cli.Wrap("tail").WithArguments(["-f", "log.txt"]).ListenAsync(ct))
{
    if (e is StandardOutputCommandEvent o) ProcessLine(o.Text);
}
```

### Push-based: `Observe`

Returns `IObservable<CommandEvent>`. The process pushes events at its own rate. Requires `System.Reactive`.
Ideal for Rx pipelines with operators like `Buffer`, `Throttle`, `Where`.

Consult `references/execution-models.md` for complete event stream examples and encoding options.

## Common Pitfalls

1. **Buffered + large output** causes `OutOfMemoryException`. Stream to file or use `ListenAsync` instead.
2. **String arguments** bypass escaping. Always use array or builder overloads.
3. **`PipeTarget.Null`** closes the stream handle entirely. To discard data while keeping the stream open, use
   `PipeTarget.ToStream(Stream.Null)`.
4. **Missing cancellation token** leaves orphaned processes on timeout or app shutdown.
5. **Forgetting immutability**: `cmd.WithArguments(...)` returns a new command; it does not mutate `cmd`.
6. **Non-zero exit code** throws by default. Disable with `WithValidation(CommandResultValidation.None)` when
   the tool uses non-zero codes for non-error conditions.

Consult `references/pitfalls.md` for detailed explanations and fixes for each pitfall.

## Reference Files

Detailed documentation organized by topic:

- **`references/execution-models.md`**: All five execution models with complete code examples
- **`references/piping.md`**: PipeSource, PipeTarget, pipe operator, command chaining, merge targets
- **`references/cancellation.md`**: Cancellation tokens, graceful vs forceful, timeouts, linked tokens
- **`references/configuration.md`**: Arguments, environment variables, credentials, working directory, validation
- **`references/pitfalls.md`**: Common mistakes with explanations and correct alternatives
- **`references/examples.md`**: Real-world compound scenarios combining multiple features
