// Custom ActivityListener for in-process trace collection
// Use when you need custom processing without the full OpenTelemetry SDK

using System.Collections.Concurrent;
using System.Diagnostics;

namespace MyApp.Telemetry;

/// <summary>
/// Collects Activity (span) data in-process for custom processing,
/// health checks, or diagnostic endpoints.
/// </summary>
public sealed class CustomTraceCollector : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<CompletedSpan> _spans = new();
    private readonly int _maxSpans;
    private int _count;

    public CustomTraceCollector(string[] sourceNames, int maxSpans = 1000)
    {
        _maxSpans = maxSpans;
        var sourceSet = sourceNames.ToHashSet();

        _listener = new ActivityListener
        {
            // Filter which ActivitySources to listen to
            ShouldListenTo = source =>
                sourceNames.Length == 0 || sourceSet.Contains(source.Name),

            // Sampling: determine what data to collect
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
            {
                // AllDataAndRecorded: create Activity, set IsAllDataRequested=true, set Recorded flag
                // AllData: create Activity, set IsAllDataRequested=true (no Recorded flag)
                // PropagationData: create Activity with minimal data for context propagation
                // None: don't create Activity
                return ActivitySamplingResult.AllDataAndRecorded;
            },

            // Called when an Activity starts
            ActivityStarted = activity =>
            {
                // Could add real-time monitoring here
            },

            // Called when an Activity stops - this is where we capture data
            ActivityStopped = activity =>
            {
                // Enforce max capacity (evict old spans)
                while (Interlocked.Increment(ref _count) > _maxSpans)
                {
                    if (_spans.TryDequeue(out _))
                        Interlocked.Decrement(ref _count);
                    else
                        break;
                }

                _spans.Enqueue(new CompletedSpan
                {
                    TraceId = activity.TraceId.ToString(),
                    SpanId = activity.SpanId.ToString(),
                    ParentSpanId = activity.ParentSpanId.ToString(),
                    OperationName = activity.OperationName,
                    DisplayName = activity.DisplayName,
                    Source = activity.Source.Name,
                    Kind = activity.Kind,
                    StartTime = activity.StartTimeUtc,
                    Duration = activity.Duration,
                    Status = activity.Status,
                    StatusDescription = activity.StatusDescription,
                    Tags = activity.TagObjects
                        .ToDictionary(t => t.Key, t => t.Value?.ToString()),
                    Events = activity.Events
                        .Select(e => new SpanEvent(e.Name, e.Timestamp))
                        .ToList()
                });
            }
        };

        // Register - this starts receiving callbacks
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>Get recent completed spans (non-destructive snapshot)</summary>
    public IReadOnlyList<CompletedSpan> GetRecentSpans()
        => _spans.ToArray();

    /// <summary>Drain all spans (destructive)</summary>
    public IEnumerable<CompletedSpan> DrainSpans()
    {
        while (_spans.TryDequeue(out var span))
        {
            Interlocked.Decrement(ref _count);
            yield return span;
        }
    }

    /// <summary>Get spans grouped by TraceId for trace reconstruction</summary>
    public IReadOnlyDictionary<string, List<CompletedSpan>> GetTraces()
        => _spans.ToArray()
            .GroupBy(s => s.TraceId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartTime).ToList());

    public void Dispose()
    {
        // Note: callbacks in progress may still fire briefly after Dispose
        _listener.Dispose();
    }
}

public record CompletedSpan
{
    public string TraceId { get; init; } = "";
    public string SpanId { get; init; } = "";
    public string ParentSpanId { get; init; } = "";
    public string OperationName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Source { get; init; } = "";
    public ActivityKind Kind { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public TimeSpan Duration { get; init; }
    public ActivityStatusCode Status { get; init; }
    public string? StatusDescription { get; init; }
    public Dictionary<string, string?> Tags { get; init; } = new();
    public List<SpanEvent> Events { get; init; } = new();
}

public record SpanEvent(string Name, DateTimeOffset Timestamp);

// ============================================================================
// USAGE: Register as singleton and expose via diagnostic endpoint
// ============================================================================

// In Program.cs:
// builder.Services.AddSingleton(new CustomTraceCollector(["MyApp.Orders", "MyApp.Payments"]));

// Diagnostic endpoint:
// app.MapGet("/diagnostics/traces", (CustomTraceCollector collector) =>
// {
//     var traces = collector.GetTraces();
//     return Results.Ok(new
//     {
//         TraceCount = traces.Count,
//         Traces = traces.Select(t => new
//         {
//             TraceId = t.Key,
//             SpanCount = t.Value.Count,
//             RootSpan = t.Value.FirstOrDefault()?.OperationName,
//             TotalDuration = t.Value.Max(s => s.StartTime + s.Duration) - t.Value.Min(s => s.StartTime)
//         })
//     });
// });
