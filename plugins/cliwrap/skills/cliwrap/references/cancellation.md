# Cancellation and Timeout Patterns

## Overview

CliWrap provides comprehensive cancellation support through the standard .NET `CancellationToken` mechanism. Every
execution model accepts one or two cancellation tokens, enabling both simple timeout scenarios and sophisticated
graceful shutdown workflows. Passing a cancellation token to every CliWrap call is essential; without one, a
long-running or hung process will live indefinitely, surviving application shutdown and leaking system resources.

This reference covers every cancellation pattern from basic timeouts through production-grade ASP.NET Core
integration, with particular emphasis on the dual-token graceful/forceful termination model that distinguishes
CliWrap from naive process management.

## Basic Cancellation

Create a `CancellationTokenSource`, optionally schedule automatic cancellation after a delay, and pass the token
to `ExecuteAsync`. When the token fires, CliWrap kills the underlying process and throws
`OperationCanceledException`.

```csharp
using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(10));

var result = await Cli.Wrap("foo").ExecuteAsync(cts.Token);
```

The call to `CancelAfter` starts a countdown. If the process completes before the deadline, nothing happens and
the result is returned normally. If the deadline elapses while the process is still running, CliWrap terminates
the process immediately and raises `OperationCanceledException`.

### Exception Handling

Wrap the call in a `try`/`catch` block to handle the cancellation. The exception carries no special payload
beyond confirming that the operation did not complete; inspect the `CancellationToken` itself to determine
whether the cancellation was manual or timeout-driven.

```csharp
try
{
    await Cli.Wrap("foo").ExecuteAsync(cts.Token);
}
catch (OperationCanceledException)
{
    // Command was canceled or timed out.
    // Inspect cts.IsCancellationRequested for confirmation.
}
```

Avoid catching the base `Exception` type. Catching `OperationCanceledException` specifically keeps the handler
narrow and prevents masking unrelated failures such as `CommandExecutionException` from a non-zero exit code.

## Timeout via CancellationTokenSource Constructor

For a pure timeout scenario where no manual cancellation is needed, pass the timeout directly to the
`CancellationTokenSource` constructor. This is the most concise approach.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

try
{
    await Cli.Wrap("foo").ExecuteAsync(cts.Token);
}
catch (OperationCanceledException)
{
    // Timeout occurred after 10 seconds.
}
```

The constructor overload accepting `TimeSpan` is functionally identical to creating the source and then calling
`CancelAfter`. Choose whichever reads more clearly in context. The constructor form is preferable when the
timeout is fixed and known at creation time. The `CancelAfter` form is preferable when the timeout is computed
later or when manual cancellation may also be needed before the deadline.

## Graceful vs Forceful Termination

Many command-line tools respond to interrupt signals. A well-behaved process receiving `Ctrl+C` (SIGINT on
Unix, `CTRL_C_EVENT` on Windows) will flush buffers, close file handles, and exit cleanly. Killing such a
process outright may corrupt output files, leave locks behind, or produce incomplete results.

CliWrap addresses this with a dual-token pattern. `ExecuteAsync` accepts two `CancellationToken` parameters.
The first token triggers forceful termination (process kill). The second token triggers graceful termination
(interrupt signal). This ordering is counterintuitive; the graceful token is the **second** parameter.

```csharp
Task<CommandResult> ExecuteAsync(
    CancellationToken forcefulCancellationToken = default,
    CancellationToken gracefulCancellationToken = default
);
```

### Dual-Token Setup

Create two independent `CancellationTokenSource` instances. Schedule the graceful token to fire first, giving
the process time to handle the signal. Schedule the forceful token to fire later as a fallback.

```csharp
using var forcefulCts = new CancellationTokenSource();
using var gracefulCts = new CancellationTokenSource();

// Forceful fallback after 10 seconds.
forcefulCts.CancelAfter(TimeSpan.FromSeconds(10));
// Graceful attempt after 7 seconds.
gracefulCts.CancelAfter(TimeSpan.FromSeconds(7));

// Note: graceful token is the SECOND parameter.
var result = await Cli.Wrap("foo")
    .ExecuteAsync(forcefulCts.Token, gracefulCts.Token);
```

### Termination Timeline

The timeline for the example above proceeds as follows.

1. **At 7 seconds**: the graceful token fires. CliWrap sends an interrupt signal (`Ctrl+C` / SIGINT) to the
   process. The process receives the signal and may begin its shutdown sequence: flushing logs, closing database
   connections, writing final output.
2. **Between 7 and 10 seconds**: the process has a 3-second window (10 minus 7) to handle the interrupt and
   exit on its own. If it exits during this window, CliWrap collects the result normally and throws
   `OperationCanceledException` because the graceful token was triggered.
3. **At 10 seconds**: if the process is still running, the forceful token fires. CliWrap kills the process
   immediately, regardless of its state. `OperationCanceledException` is thrown.

The gap between the graceful and forceful deadlines is the grace period. Size it according to the target
process. A tool that flushes a small buffer needs one or two seconds. A database migration tool flushing
transactions may need 30 seconds or more.

### Signal Behavior by Platform

On Unix-like systems, the graceful cancellation sends SIGINT to the process. On Windows, it sends
`CTRL_C_EVENT` to the process console. Both are the standard "polite shutdown" signals that most CLI tools
handle. Forceful cancellation calls `Process.Kill()` on all platforms, which is equivalent to SIGKILL on Unix
and `TerminateProcess` on Windows.

## Method-Level Wrapper Pattern

Production code rarely calls CliWrap directly at the call site. Instead, wrap each CLI interaction in a
dedicated async method. The method accepts a standard `CancellationToken` from its caller and internally
constructs the dual-token wiring.

```csharp
public async Task GitPushAsync(CancellationToken cancellationToken = default)
{
    using var forcefulCts = new CancellationTokenSource();

    // When the external token fires, schedule forceful kill after a 3-second grace period.
    await using var link = cancellationToken.Register(() =>
        forcefulCts.CancelAfter(TimeSpan.FromSeconds(3))
    );

    await Cli.Wrap("git")
        .WithArguments(["push"])
        .ExecuteAsync(forcefulCts.Token, cancellationToken);
}
```

### How the Wiring Works

1. The caller's `cancellationToken` is passed as the **graceful** token (second parameter). When it fires,
   CliWrap sends an interrupt signal to `git push`.
2. The `Register` callback fires at the same moment, scheduling `forcefulCts.CancelAfter(3 seconds)`. This
   starts the forceful countdown.
3. `git push` receives the interrupt and begins its shutdown. It has 3 seconds to finish.
4. If `git push` exits within the 3-second window, execution completes with `OperationCanceledException`.
5. If `git push` is still running after 3 seconds, the forceful token fires and CliWrap kills the process.

This pattern keeps the caller's API simple (a single `CancellationToken`) while providing robust
graceful-then-forceful behavior internally. The grace period is an implementation detail of the method, chosen
based on the specific tool being wrapped.

### Parameterizing the Grace Period

For flexibility, accept the grace period as a parameter with a sensible default.

```csharp
public async Task GitPushAsync(
    CancellationToken cancellationToken = default,
    TimeSpan? gracePeriod = null)
{
    var grace = gracePeriod ?? TimeSpan.FromSeconds(3);
    using var forcefulCts = new CancellationTokenSource();

    await using var link = cancellationToken.Register(() =>
        forcefulCts.CancelAfter(grace)
    );

    await Cli.Wrap("git")
        .WithArguments(["push"])
        .ExecuteAsync(forcefulCts.Token, cancellationToken);
}
```

Callers that know their process needs more time can pass a longer grace period without changing the method's
internal structure.

## Cancellation with All Execution Models

Every CliWrap execution model supports cancellation tokens. The token semantics are identical across all
models: graceful sends an interrupt, forceful kills the process, and `OperationCanceledException` is thrown.

### ExecuteAsync

Returns a `CommandResult` with exit code, start time, and exit time.

```csharp
await Cli.Wrap("foo").ExecuteAsync(cts.Token);
```

Both tokens are supported.

```csharp
await Cli.Wrap("foo").ExecuteAsync(forcefulCts.Token, gracefulCts.Token);
```

### ExecuteBufferedAsync

Returns a `BufferedCommandResult` with captured standard output and standard error in addition to the base
result properties.

```csharp
await Cli.Wrap("foo").ExecuteBufferedAsync(cts.Token);
```

Both tokens are supported.

```csharp
await Cli.Wrap("foo").ExecuteBufferedAsync(forcefulCts.Token, gracefulCts.Token);
```

### ListenAsync

Returns an `IAsyncEnumerable<CommandEvent>` that yields stdout and stderr events as they arrive. Cancellation
terminates the enumeration and kills the process.

```csharp
await foreach (var cmdEvent in Cli.Wrap("foo").ListenAsync(cts.Token))
{
    // Process events as they arrive.
}
```

Both tokens are supported.

```csharp
await foreach (var cmdEvent in Cli.Wrap("foo").ListenAsync(forcefulCts.Token, gracefulCts.Token))
{
    // Process events as they arrive.
}
```

### Observe

Returns an `IObservable<CommandEvent>` for reactive pipelines. Pass the token when subscribing.

```csharp
await Cli.Wrap("foo")
    .Observe(cts.Token)
    .ForEachAsync(cmdEvent =>
    {
        // Process events reactively.
    });
```

Both tokens are supported.

```csharp
await Cli.Wrap("foo")
    .Observe(forcefulCts.Token, gracefulCts.Token)
    .ForEachAsync(cmdEvent =>
    {
        // Process events reactively.
    });
```

## Best Practices

### Always Pass a CancellationToken

Never call `ExecuteAsync()` without a token. An omitted token means `CancellationToken.None`, which provides
no mechanism to stop the process. If the target tool hangs, the process lives until the host application is
forcefully terminated, and even then it may survive as an orphan.

### Prefer the Dual-Token Pattern for Signal-Aware Processes

Tools like `git`, `docker`, `dotnet`, `ffmpeg`, and most modern CLI applications handle SIGINT gracefully.
Use the dual-token pattern to give them a chance to clean up. Reserve the single-token pattern for tools
that are known to ignore signals or that have no meaningful cleanup to perform.

### Size Timeouts Deliberately

Do not rely on a process to exit on its own. Always set an outer forceful timeout. Choose timeouts based on
the expected behavior of the tool.

- Fast commands (file lookups, version checks): 5 to 10 seconds total, 2-second grace period.
- Network operations (git push, API calls): 30 to 60 seconds total, 5-second grace period.
- Heavy processing (builds, video encoding): minutes, with a 10- to 30-second grace period.

### Wire Framework Cancellation Tokens

In ASP.NET Core, use `HttpContext.RequestAborted` as the graceful token. When the client disconnects or the
request times out, the token fires and the process receives an interrupt.

In hosted services (`BackgroundService`, `IHostedService`), use
`IHostApplicationLifetime.ApplicationStopping` as the graceful token. When the host begins shutdown, all
running processes receive an interrupt before the host proceeds to force-stop.

### Use the Method-Level Wrapper Pattern

Encapsulate each CLI interaction in a method that accepts a single `CancellationToken` and handles the
dual-token wiring internally. This keeps the cancellation logic centralized and prevents callers from
needing to understand the parameter order.

### Dispose CancellationTokenSource Instances

Always wrap `CancellationTokenSource` in a `using` declaration. Failing to dispose a
`CancellationTokenSource` that has a pending timer (from `CancelAfter`) leaks a timer handle. In
high-throughput scenarios, this can exhaust system resources.

## Common Mistakes

### Forgetting to Pass a Token

Calling `ExecuteAsync()` with no arguments leaves no way to stop the process. If the target tool enters an
infinite loop or waits for input that never arrives, the process runs indefinitely.

```csharp
// Wrong: no cancellation token.
var result = await Cli.Wrap("foo").ExecuteAsync();

// Correct: always pass a token.
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var result = await Cli.Wrap("foo").ExecuteAsync(cts.Token);
```

### Swapping the Token Order

The parameter order is `(forceful, graceful)`, not `(graceful, forceful)`. Swapping them sends the kill
signal first and the interrupt signal second. The process is killed immediately with no chance to shut down
cleanly.

```csharp
// Wrong: graceful token in the forceful position.
await Cli.Wrap("foo").ExecuteAsync(gracefulCts.Token, forcefulCts.Token);

// Correct: forceful first, graceful second.
await Cli.Wrap("foo").ExecuteAsync(forcefulCts.Token, gracefulCts.Token);
```

### Not Catching OperationCanceledException

When a token fires, `OperationCanceledException` is thrown. If the caller does not catch it, the exception
propagates and may crash the application or trigger unintended error-handling paths.

```csharp
// Wrong: unhandled cancellation exception.
await Cli.Wrap("foo").ExecuteAsync(cts.Token);

// Correct: handle cancellation explicitly.
try
{
    await Cli.Wrap("foo").ExecuteAsync(cts.Token);
}
catch (OperationCanceledException)
{
    // Log, return a default, or rethrow as appropriate.
}
```

### Setting Equal or Inverted Timeouts

If the graceful timeout is equal to or greater than the forceful timeout, the forceful token fires at the
same time as or before the graceful token. The process is killed before (or at the same instant) it receives
the interrupt signal, eliminating the grace period entirely.

```csharp
// Wrong: graceful fires at 10s, forceful fires at 10s. No grace period.
forcefulCts.CancelAfter(TimeSpan.FromSeconds(10));
gracefulCts.CancelAfter(TimeSpan.FromSeconds(10));

// Wrong: graceful fires at 15s, forceful fires at 10s. Process killed before interrupt.
forcefulCts.CancelAfter(TimeSpan.FromSeconds(10));
gracefulCts.CancelAfter(TimeSpan.FromSeconds(15));

// Correct: graceful fires at 7s, forceful fires at 10s. 3-second grace period.
forcefulCts.CancelAfter(TimeSpan.FromSeconds(10));
gracefulCts.CancelAfter(TimeSpan.FromSeconds(7));
```

### Reusing a Canceled CancellationTokenSource

Once a `CancellationTokenSource` has been canceled, it cannot be reset. Creating a new CliWrap call with
an already-canceled token causes immediate cancellation.

```csharp
// Wrong: reusing a canceled source.
cts.Cancel();
await Cli.Wrap("bar").ExecuteAsync(cts.Token); // Throws immediately.

// Correct: create a new source for each operation.
using var newCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
await Cli.Wrap("bar").ExecuteAsync(newCts.Token);
```

## ASP.NET Core Integration

ASP.NET Core provides `HttpContext.RequestAborted`, a `CancellationToken` that fires when the client
disconnects or the request times out. Combine it with the dual-token pattern to ensure that CLI processes
launched during request handling are cleaned up promptly.

### Minimal API Endpoint

```csharp
app.MapGet("/run", async (HttpContext ctx) =>
{
    using var forcefulCts = new CancellationTokenSource();
    var gracefulCt = ctx.RequestAborted;

    // When the request is aborted, schedule forceful kill after 5 seconds.
    await using var link = gracefulCt.Register(() =>
        forcefulCts.CancelAfter(TimeSpan.FromSeconds(5))
    );

    try
    {
        var result = await Cli.Wrap("my-tool")
            .WithArguments(["--process"])
            .ExecuteBufferedAsync(forcefulCts.Token, gracefulCt);

        return Results.Ok(result.StandardOutput);
    }
    catch (OperationCanceledException)
    {
        // Client disconnected or request timed out.
        return Results.StatusCode(499); // Client Closed Request.
    }
});
```

### Controller Action

The same pattern applies in MVC controllers. Inject the `CancellationToken` via the action parameter, which
ASP.NET Core automatically binds to `HttpContext.RequestAborted`.

```csharp
[ApiController]
[Route("api/[controller]")]
public class ReportController : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateReport(
        [FromBody] ReportRequest request,
        CancellationToken cancellationToken)
    {
        using var forcefulCts = new CancellationTokenSource();

        await using var link = cancellationToken.Register(() =>
            forcefulCts.CancelAfter(TimeSpan.FromSeconds(5))
        );

        try
        {
            var result = await Cli.Wrap("report-generator")
                .WithArguments(["--format", "pdf", "--input", request.InputPath])
                .ExecuteBufferedAsync(forcefulCts.Token, cancellationToken);

            return Ok(new { Output = result.StandardOutput });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, "Client disconnected before report generation completed.");
        }
    }
}
```

### Background Service

For long-running background tasks, wire up `IHostApplicationLifetime.ApplicationStopping` as the graceful
token. This ensures that CLI processes are interrupted cleanly when the host application begins its shutdown
sequence.

```csharp
public class DataSyncService : BackgroundService
{
    private readonly IHostApplicationLifetime _lifetime;

    public DataSyncService(IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // stoppingToken fires when the host begins shutdown.
        using var forcefulCts = new CancellationTokenSource();

        await using var link = stoppingToken.Register(() =>
            forcefulCts.CancelAfter(TimeSpan.FromSeconds(10))
        );

        try
        {
            await Cli.Wrap("data-sync")
                .WithArguments(["--mode", "continuous"])
                .ExecuteAsync(forcefulCts.Token, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down. Process received interrupt and exited (or was killed).
        }
    }
}
```

The `stoppingToken` provided by `BackgroundService.ExecuteAsync` is linked to
`IHostApplicationLifetime.ApplicationStopping`. When the host begins shutdown (via `Ctrl+C`, SIGTERM, or
`IHostApplicationLifetime.StopApplication()`), the token fires, the CLI process receives an interrupt, and
the forceful fallback engages 10 seconds later if needed.

## Combining Cancellation with Pipe Targets

When piping output to delegates or streams, cancellation still works the same way. The token terminates the
process and the pipe target receives no further data.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

var stdOutBuffer = new StringBuilder();

await Cli.Wrap("long-running-tool")
    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
    .ExecuteAsync(cts.Token);

// stdOutBuffer contains whatever was written before cancellation (if canceled)
// or the full output (if completed normally).
```

Combine with the dual-token pattern to allow graceful shutdown of the piped process.

```csharp
using var forcefulCts = new CancellationTokenSource();
using var gracefulCts = new CancellationTokenSource();

forcefulCts.CancelAfter(TimeSpan.FromSeconds(30));
gracefulCts.CancelAfter(TimeSpan.FromSeconds(25));

var stdOutBuffer = new StringBuilder();

try
{
    await Cli.Wrap("long-running-tool")
        .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
        .ExecuteAsync(forcefulCts.Token, gracefulCts.Token);
}
catch (OperationCanceledException)
{
    // Partial output is available in stdOutBuffer.
    var partialOutput = stdOutBuffer.ToString();
}
```

## Summary of Token Semantics

| Scenario                     | Forceful Token           | Graceful Token           | Behavior                                    |
| ---------------------------- | ------------------------ | ------------------------ | ------------------------------------------- |
| Single token                 | Provided                 | `default`                | Process killed immediately on cancellation  |
| Dual token                   | Provided                 | Provided                 | Interrupt first; kill after grace period     |
| No token                     | `default`                | `default`                | No cancellation possible; avoid this        |
| Graceful only                | `default`                | Provided                 | Interrupt sent; no forceful fallback         |
| Method-level wrapper         | Internal linked source   | Caller's token           | Clean API with internal grace period        |
| ASP.NET Core request         | Internal linked source   | `RequestAborted`         | Process stops when client disconnects       |
| Background service           | Internal linked source   | `stoppingToken`          | Process stops on host shutdown              |

The dual-token pattern with linked sources is the recommended approach for all production scenarios. It
provides clean shutdown behavior while guaranteeing that no process survives beyond the forceful deadline.
