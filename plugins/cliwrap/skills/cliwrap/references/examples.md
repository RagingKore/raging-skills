# CliWrap Compound Examples

## Git Workflow Automation

Automate a full clone, commit, and push cycle with environment variables, working directory binding, and
conditional argument building. The base command object captures shared configuration; each step derives from it.

**Features combined:** `WithWorkingDirectory`, `WithEnvironmentVariables`, `ExecuteBufferedAsync`,
conditional logic on buffered output, immutable command reuse.

```csharp
using CliWrap;
using CliWrap.Buffered;

var repoPath = "/tmp/my-repo";

var git = Cli.Wrap("git")
    .WithWorkingDirectory(repoPath)
    .WithEnvironmentVariables(env => env
        .Set("GIT_AUTHOR_NAME", "CI Bot")
        .Set("GIT_AUTHOR_EMAIL", "ci@example.com")
        .Set("GIT_COMMITTER_NAME", "CI Bot")
        .Set("GIT_COMMITTER_EMAIL", "ci@example.com")
    );

// Clone if the directory is empty
var cloneResult = await Cli.Wrap("git")
    .WithArguments(["clone", "https://github.com/org/repo.git", repoPath])
    .WithValidation(CommandResultValidation.None)
    .ExecuteBufferedAsync();

if (cloneResult.ExitCode != 0)
{
    Console.Error.WriteLine($"Clone failed: {cloneResult.StandardError}");
    return;
}

// Check for uncommitted changes
var status = await git
    .WithArguments(["status", "--porcelain"])
    .ExecuteBufferedAsync();

if (string.IsNullOrWhiteSpace(status.StandardOutput))
{
    Console.WriteLine("Working tree is clean; nothing to commit.");
    return;
}

// Stage, commit, push
await git.WithArguments(["add", "."]).ExecuteAsync();

var commitArgs = new ArgumentsBuilder();
commitArgs.Add("commit");
commitArgs.Add("-m");
commitArgs.Add("Automated commit from CI");

// Conditionally sign commits when a GPG key is available
var gpgKey = Environment.GetEnvironmentVariable("GPG_KEY_ID");
if (!string.IsNullOrEmpty(gpgKey))
{
    commitArgs.Add("--gpg-sign");
    commitArgs.Add(gpgKey);
}

await git.WithArguments(commitArgs.Build()).ExecuteAsync();
await git.WithArguments(["push", "origin", "main"]).ExecuteAsync();

Console.WriteLine("Changes committed and pushed.");
```

## Docker Build with Streaming Output

Build a Docker image while streaming `stdout` and `stderr` in real time via `ListenAsync`. Report build progress
to the caller and enforce a timeout with cancellation.

**Features combined:** `ListenAsync` event stream, `CancellationTokenSource` with timeout,
`StartedCommandEvent` / `StandardOutputCommandEvent` / `StandardErrorCommandEvent` pattern matching.

**Additional package:** `CliWrap.EventStream` (included in the main CliWrap NuGet package).

```csharp
using CliWrap;
using CliWrap.EventStream;

async Task BuildDockerImageAsync(
    string dockerfilePath,
    string contextPath,
    string tag,
    IProgress<string> progress,
    CancellationToken cancellationToken = default)
{
    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeoutCts.CancelAfter(TimeSpan.FromMinutes(10));

    var cmd = Cli.Wrap("docker")
        .WithArguments([
            "build",
            "-f", dockerfilePath,
            "-t", tag,
            "--progress", "plain",
            contextPath
        ])
        .WithValidation(CommandResultValidation.None);

    int? processId = null;

    await foreach (var evt in cmd.ListenAsync(timeoutCts.Token))
    {
        switch (evt)
        {
            case StartedCommandEvent started:
                processId = started.ProcessId;
                progress.Report($"Build started (PID {started.ProcessId})");
                break;

            case StandardOutputCommandEvent stdOut:
                progress.Report(stdOut.Text);
                break;

            case StandardErrorCommandEvent stdErr:
                // Docker writes build output to stderr
                progress.Report(stdErr.Text);
                break;

            case ExitedCommandEvent exited:
                if (exited.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"Docker build failed with exit code {exited.ExitCode}.");
                progress.Report("Build completed successfully.");
                break;
        }
    }
}
```

## Processing Large Log Files with Piped Commands

Pipe a log file through `grep` and then `wc` to count matching lines. Demonstrate command chaining with the
pipe operator (`|`) and write results to an output file.

**Features combined:** `PipeSource.FromFile`, pipe operator for command chaining, `PipeTarget.ToFile`,
`PipeTarget.Merge`, `PipeTarget.ToStringBuilder`.

```csharp
using System.Text;
using CliWrap;

var logFile = "/var/log/app/production.log";
var resultFile = "/tmp/error-summary.txt";

// Count lines containing "ERROR" in the log file
var countBuilder = new StringBuilder();

var pipeline =
    PipeSource.FromFile(logFile)
    | Cli.Wrap("grep").WithArguments(["--ignore-case", "ERROR"])
    | Cli.Wrap("wc").WithArguments(["-l"])
    | PipeTarget.Merge(
        PipeTarget.ToFile(resultFile),
        PipeTarget.ToStringBuilder(countBuilder)
    );

await pipeline.ExecuteAsync();

var errorCount = countBuilder.ToString().Trim();
Console.WriteLine($"Found {errorCount} error lines. Results saved to {resultFile}.");

// Extract unique error codes and write them to a separate file
var uniqueErrors =
    PipeSource.FromFile(logFile)
    | Cli.Wrap("grep").WithArguments(["-oP", @"ERROR\[\K[A-Z0-9]+"])
    | Cli.Wrap("sort").WithArguments(["-u"])
    | PipeTarget.ToFile("/tmp/unique-error-codes.txt");

await uniqueErrors.ExecuteAsync();
```

## Long-Lived Process with Graceful Shutdown

Start a background web server and manage its lifecycle with dual-token cancellation. The first token sends an
interrupt signal (graceful); a follow-up timer forces termination if the process does not exit in time. Integrate
this pattern into an ASP.NET Core hosted service.

**Features combined:** `ExecuteAsync` with graceful and forceful cancellation tokens,
`IHostedService` integration, `CommandTask` for process tracking.

**Additional package:** `Microsoft.Extensions.Hosting.Abstractions`.

```csharp
using CliWrap;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class DevServerService : IHostedService, IAsyncDisposable
{
    private readonly ILogger<DevServerService> _logger;
    private CancellationTokenSource? _gracefulCts;
    private CancellationTokenSource? _forcefulCts;
    private CommandTask<CommandResult>? _serverTask;

    public DevServerService(ILogger<DevServerService> logger) => _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _gracefulCts = new CancellationTokenSource();
        _forcefulCts = new CancellationTokenSource();

        // When graceful cancellation is requested, schedule forceful kill after 5 seconds
        _gracefulCts.Token.Register(() => _forcefulCts!.CancelAfter(TimeSpan.FromSeconds(5)));

        _serverTask = Cli.Wrap("npx")
            .WithArguments(["serve", "-l", "3000"])
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(_forcefulCts.Token, _gracefulCts.Token);

        _logger.LogInformation(
            "Dev server started (PID {ProcessId}).",
            _serverTask.ProcessId);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_serverTask is null)
            return;

        _logger.LogInformation("Requesting graceful shutdown of dev server...");
        _gracefulCts!.Cancel();

        try
        {
            await _serverTask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Dev server was forcefully terminated.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _gracefulCts?.Dispose();
        _forcefulCts?.Dispose();

        if (_serverTask is not null)
            await _serverTask.Task.ContinueWith(_ => { });
    }
}
```

## FFmpeg Video Processing with Progress Monitoring

Pipe an HTTP stream through FFmpeg for transcoding. Monitor progress via `stderr` event streaming, enforce a
timeout, and write the transcoded output to a file.

**Features combined:** `PipeSource.FromStream` (from `HttpClient`), `ListenAsync` for stderr progress,
`PipeTarget.ToFile`, cancellation with timeout.

```csharp
using CliWrap;
using CliWrap.EventStream;
using System.Text.RegularExpressions;

async Task TranscodeStreamAsync(
    string sourceUrl,
    string outputPath,
    TimeSpan totalDuration,
    IProgress<double> progress,
    CancellationToken cancellationToken = default)
{
    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeoutCts.CancelAfter(TimeSpan.FromMinutes(30));

    var cmd = Cli.Wrap("ffmpeg")
        .WithArguments([
            "-i", sourceUrl,
            "-c:v", "libx264",
            "-preset", "fast",
            "-c:a", "aac",
            "-movflags", "+faststart",
            "-y",
            outputPath
        ])
        .WithValidation(CommandResultValidation.None);

    var timePattern = new Regex(@"time=(\d{2}):(\d{2}):(\d{2})\.(\d{2})");

    await foreach (var evt in cmd.ListenAsync(timeoutCts.Token))
    {
        switch (evt)
        {
            case StartedCommandEvent started:
                Console.WriteLine($"FFmpeg started (PID {started.ProcessId})");
                break;

            case StandardErrorCommandEvent stdErr:
                // FFmpeg writes progress to stderr
                var match = timePattern.Match(stdErr.Text);
                if (match.Success)
                {
                    var current = new TimeSpan(
                        0,
                        int.Parse(match.Groups[1].Value),
                        int.Parse(match.Groups[2].Value),
                        int.Parse(match.Groups[3].Value),
                        int.Parse(match.Groups[4].Value) * 10);

                    var percent = current / totalDuration * 100.0;
                    progress.Report(Math.Min(percent, 100.0));
                }
                break;

            case ExitedCommandEvent exited when exited.ExitCode != 0:
                throw new InvalidOperationException(
                    $"FFmpeg exited with code {exited.ExitCode}.");
        }
    }

    progress.Report(100.0);
}
```

## Database Backup with pg_dump

Run `pg_dump` with credentials passed via environment variables (never command-line arguments). Pipe the output
through `gzip` for compression and handle connection failures.

**Features combined:** `WithEnvironmentVariables` for secrets, pipe operator for command chaining,
`PipeTarget.ToFile`, `WithValidation(None)` with manual exit code checking.

```csharp
using CliWrap;
using CliWrap.Buffered;

async Task<string> BackupDatabaseAsync(
    string host,
    string database,
    string username,
    string password,
    string outputDir)
{
    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    var backupPath = Path.Combine(outputDir, $"{database}-{timestamp}.sql.gz");

    var pgDump = Cli.Wrap("pg_dump")
        .WithArguments([
            "--host", host,
            "--port", "5432",
            "--format", "plain",
            "--no-owner",
            "--no-acl",
            database
        ])
        .WithEnvironmentVariables(env => env
            .Set("PGUSER", username)
            .Set("PGPASSWORD", password)
        )
        .WithValidation(CommandResultValidation.None);

    // Pipe pg_dump output through gzip into a file
    var pipeline =
        pgDump
        | Cli.Wrap("gzip").WithArguments(["-9"])
        | PipeTarget.ToFile(backupPath);

    var result = await pipeline.ExecuteAsync();

    if (result.ExitCode != 0)
    {
        // Capture detailed error by running pg_dump alone in buffered mode
        var errorResult = await pgDump.ExecuteBufferedAsync();
        throw new InvalidOperationException(
            $"pg_dump failed (exit code {errorResult.ExitCode}): {errorResult.StandardError}");
    }

    var fileSize = new FileInfo(backupPath).Length;
    Console.WriteLine(
        $"Backup complete: {backupPath} ({fileSize / 1024.0 / 1024.0:F1} MB)");

    return backupPath;
}
```

## Running Multiple Commands in Parallel

Execute several independent CLI tools concurrently using `Task.WhenAll`. Collect all results and handle partial
failures without cancelling the entire batch.

**Features combined:** `ExecuteBufferedAsync` across multiple commands, `Task.WhenAll`,
`WithValidation(None)` for individual error handling.

```csharp
using CliWrap;
using CliWrap.Buffered;

record HealthCheck(string Name, string Command, string[] Args);

async Task<Dictionary<string, (bool Healthy, string Output)>> RunHealthChecksAsync(
    CancellationToken cancellationToken = default)
{
    var checks = new HealthCheck[]
    {
        new("Redis", "redis-cli", ["ping"]),
        new("PostgreSQL", "pg_isready", ["-h", "localhost", "-p", "5432"]),
        new("Elasticsearch", "curl", ["-sf", "http://localhost:9200/_cluster/health"]),
        new("RabbitMQ", "rabbitmqctl", ["status"]),
    };

    var tasks = checks.Select(async check =>
    {
        var result = await Cli.Wrap(check.Command)
            .WithArguments(check.Args)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(cancellationToken);

        return (
            check.Name,
            Healthy: result.ExitCode == 0,
            Output: result.ExitCode == 0
                ? result.StandardOutput.Trim()
                : result.StandardError.Trim()
        );
    });

    var results = await Task.WhenAll(tasks);

    var report = results.ToDictionary(
        r => r.Name,
        r => (r.Healthy, r.Output));

    foreach (var (name, (healthy, output)) in report)
    {
        var status = healthy ? "OK" : "FAIL";
        Console.WriteLine($"[{status}] {name}: {output}");
    }

    return report;
}
```

## Interactive Process Communication via stdin

Write data to a process through `stdin` while capturing its response from `stdout`. Use `PipeSource.FromStream`
with a `MemoryStream` to simulate interactive input for tools that read from standard input.

**Features combined:** `PipeSource.FromStream`, `PipeTarget.ToStringBuilder`,
`WithStandardInputPipe`, `WithStandardOutputPipe`.

```csharp
using System.Text;
using CliWrap;

// Send SQL queries to sqlite3 via stdin and capture results
async Task<string> ExecuteSqlAsync(string databasePath, string sql)
{
    var input = new MemoryStream(Encoding.UTF8.GetBytes(sql + "\n.quit\n"));
    var outputBuilder = new StringBuilder();
    var errorBuilder = new StringBuilder();

    var result = await Cli.Wrap("sqlite3")
        .WithArguments([databasePath, "-header", "-column"])
        .WithStandardInputPipe(PipeSource.FromStream(input))
        .WithStandardOutputPipe(PipeTarget.ToStringBuilder(outputBuilder))
        .WithStandardErrorPipe(PipeTarget.ToStringBuilder(errorBuilder))
        .WithValidation(CommandResultValidation.None)
        .ExecuteAsync();

    if (result.ExitCode != 0)
        throw new InvalidOperationException(
            $"sqlite3 failed (exit code {result.ExitCode}): {errorBuilder}");

    return outputBuilder.ToString();
}

// Usage
var schema = await ExecuteSqlAsync(
    "/tmp/app.db",
    "SELECT name, sql FROM sqlite_master WHERE type='table';");

Console.WriteLine(schema);

// Pipe multiple statements in a batch
var batchSql = """
    CREATE TABLE IF NOT EXISTS logs (id INTEGER PRIMARY KEY, message TEXT, ts DATETIME);
    INSERT INTO logs (message, ts) VALUES ('startup', datetime('now'));
    SELECT COUNT(*) AS total FROM logs;
    """;

var batchResult = await ExecuteSqlAsync("/tmp/app.db", batchSql);
Console.WriteLine(batchResult);
```

## Observe with Rx.NET Operators

Use the `Observe()` method to get a push-based `IObservable<CommandEvent>` stream. Apply Rx.NET operators such as
`Buffer`, `Where`, and `Throttle` to aggregate and filter output events in real time.

**Features combined:** `Observe()` returning `IObservable<CommandEvent>`, Rx.NET `Buffer`, `Where`,
`Throttle`, `OfType` operators.

**Additional packages:** `System.Reactive` (Rx.NET).

```csharp
using System.Reactive.Linq;
using CliWrap;
using CliWrap.EventStream;

// Monitor a log file and aggregate lines into 2-second batches
async Task MonitorLogsWithRxAsync(CancellationToken cancellationToken = default)
{
    var cmd = Cli.Wrap("tail")
        .WithArguments(["-f", "/var/log/app/events.log"]);

    var subscription = cmd.Observe(cancellationToken)
        .OfType<StandardOutputCommandEvent>()
        .Select(e => e.Text)
        .Where(line => line.Contains("WARN") || line.Contains("ERROR"))
        .Buffer(TimeSpan.FromSeconds(2))
        .Where(batch => batch.Count > 0)
        .Subscribe(
            batch =>
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] {batch.Count} events in last 2s:");
                foreach (var line in batch)
                    Console.WriteLine($"  {line}");
            },
            ex => Console.Error.WriteLine($"Stream error: {ex.Message}"),
            () => Console.WriteLine("Monitoring stopped.")
        );

    // Keep monitoring until cancellation is requested
    try
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }
    catch (OperationCanceledException)
    {
        // Expected on shutdown
    }
    finally
    {
        subscription.Dispose();
    }
}

// Real-time throughput measurement: throttle output to one report per second
async Task MeasureThroughputAsync(CancellationToken cancellationToken = default)
{
    var lineCount = 0;

    var cmd = Cli.Wrap("find")
        .WithArguments(["/", "-type", "f", "-name", "*.log"]);

    var subscription = cmd.Observe(cancellationToken)
        .OfType<StandardOutputCommandEvent>()
        .Do(_ => Interlocked.Increment(ref lineCount))
        .Throttle(TimeSpan.FromSeconds(1))
        .Subscribe(
            _ => Console.WriteLine($"Files found so far: {lineCount}"),
            ex => Console.Error.WriteLine($"Error: {ex.Message}"),
            () => Console.WriteLine($"Done. Total files: {lineCount}")
        );

    // Wait for observable sequence to complete
    await cmd.Observe(cancellationToken)
        .LastOrDefaultAsync()
        .ToTask(cancellationToken);

    subscription.Dispose();
}
```

## Reusable Command Factory Pattern

Create a factory that produces pre-configured `Command` objects. Leverage the immutability of CliWrap commands to
derive specialized variants from a shared base without side effects.

**Features combined:** immutable `Command` object reuse, `WithArguments`, `WithEnvironmentVariables`,
`WithWorkingDirectory`, factory method pattern.

```csharp
using CliWrap;
using CliWrap.Buffered;

public sealed class KubeCommandFactory
{
    private readonly string _context;
    private readonly string _namespace;
    private readonly Command _base;

    public KubeCommandFactory(string context, string @namespace, string kubeconfig)
    {
        _context = context;
        _namespace = @namespace;

        // Base command holds env vars and validation only; arguments are built per-method
        // because WithArguments replaces (not appends) the argument list.
        _base = Cli.Wrap("kubectl")
            .WithEnvironmentVariables(env => env
                .Set("KUBECONFIG", kubeconfig)
            )
            .WithValidation(CommandResultValidation.ZeroExitCode);
    }

    // Include the shared flags in every argument list via the builder.
    private Command Build(Action<ArgumentsBuilder> configure) =>
        _base.WithArguments(args =>
        {
            args.Add("--context").Add(_context)
                .Add("--namespace").Add(_namespace);
            configure(args);
        });

    /// <summary>
    /// Retrieve resources as JSON.
    /// </summary>
    public Command Get(string resourceType, string? name = null) =>
        Build(args =>
        {
            args.Add("get").Add(resourceType);
            if (name is not null)
                args.Add(name);
            args.Add("-o").Add("json");
        });

    /// <summary>
    /// Apply a manifest file.
    /// </summary>
    public Command Apply(string manifestPath) =>
        Build(args => args.Add("apply").Add("-f").Add(manifestPath));

    /// <summary>
    /// Stream logs from a pod.
    /// </summary>
    public Command Logs(string podName, bool follow = false) =>
        Build(args =>
        {
            args.Add("logs").Add(podName);
            if (follow)
                args.Add("--follow");
        });

    /// <summary>
    /// Delete a resource.
    /// </summary>
    public Command Delete(string resourceType, string name) =>
        Build(args => args.Add("delete").Add(resourceType).Add(name));
}

// Usage
var kube = new KubeCommandFactory(
    context: "production",
    @namespace: "backend",
    kubeconfig: "/home/deploy/.kube/config");

// Fetch all pods
var podsJson = await kube.Get("pods")
    .ExecuteBufferedAsync();
Console.WriteLine(podsJson.StandardOutput);

// Apply a deployment manifest
await kube.Apply("/manifests/api-deployment.yaml")
    .ExecuteAsync();

// Stream logs with cancellation
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
await foreach (var evt in kube.Logs("api-pod-abc123", follow: true)
    .ListenAsync(cts.Token))
{
    if (evt is StandardOutputCommandEvent stdOut)
        Console.WriteLine(stdOut.Text);
}
```
