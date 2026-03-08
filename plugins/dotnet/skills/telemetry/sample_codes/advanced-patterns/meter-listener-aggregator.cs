// Custom MeterListener for in-process metric collection
// Use when you need custom aggregation, health checks, or diagnostic endpoints
// without configuring a full OTel pipeline

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace MyApp.Telemetry;

/// <summary>
/// In-process metric aggregator using MeterListener.
/// Collects measurements from specified Meters and provides snapshots.
/// </summary>
public sealed class InProcessMetricAggregator : IDisposable
{
    private readonly MeterListener _listener;
    private readonly ConcurrentDictionary<string, InstrumentAggregation> _instruments = new();

    public InProcessMetricAggregator(params string[] meterNames)
    {
        var nameSet = meterNames.ToHashSet();
        _listener = new MeterListener();

        // Called when any Instrument is created in the process
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (nameSet.Count == 0 || nameSet.Contains(instrument.Meter.Name))
            {
                // EnableMeasurementEvents opts this listener in for this instrument
                // The state object is stored and passed back in measurement callbacks
                var aggregation = new InstrumentAggregation(
                    instrument.Name, instrument.Meter.Name, instrument.GetType().Name);
                _instruments[instrument.Name] = aggregation;
                listener.EnableMeasurementEvents(instrument, aggregation);
            }
        };

        // Register callbacks for all numeric types instruments can use
        _listener.SetMeasurementEventCallback<byte>(OnMeasurement);
        _listener.SetMeasurementEventCallback<short>(OnMeasurement);
        _listener.SetMeasurementEventCallback<int>(OnMeasurement);
        _listener.SetMeasurementEventCallback<long>(OnMeasurement);
        _listener.SetMeasurementEventCallback<float>(OnMeasurement);
        _listener.SetMeasurementEventCallback<double>(OnMeasurement);
        _listener.SetMeasurementEventCallback<decimal>(OnMeasurement);

        _listener.Start();
    }

    private static void OnMeasurement<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state) where T : struct
    {
        // State is the InstrumentAggregation we passed in EnableMeasurementEvents
        // Using state avoids a dictionary lookup on every measurement (performance)
        if (state is InstrumentAggregation aggregation)
        {
            var value = Convert.ToDouble(measurement);
            var tagKey = FormatTags(tags);
            aggregation.Record(tagKey, value);
        }
    }

    private static string FormatTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (tags.Length == 0) return "";
        // Build a stable key from sorted tags
        Span<char> buffer = stackalloc char[256];
        var written = 0;
        foreach (var tag in tags)
        {
            if (written > 0 && written < buffer.Length - 1)
                buffer[written++] = ',';
            var entry = $"{tag.Key}={tag.Value}";
            entry.AsSpan().CopyTo(buffer[written..]);
            written += Math.Min(entry.Length, buffer.Length - written);
        }
        return new string(buffer[..written]);
    }

    /// <summary>Get a snapshot of all metric data</summary>
    public IReadOnlyDictionary<string, InstrumentSnapshot> GetSnapshot()
    {
        // For ObservableCounter/ObservableGauge, trigger callback collection
        _listener.RecordObservableInstruments();

        return _instruments.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.TakeSnapshot());
    }

    /// <summary>Get data for a specific instrument</summary>
    public InstrumentSnapshot? GetInstrument(string name)
    {
        _listener.RecordObservableInstruments();
        return _instruments.TryGetValue(name, out var agg) ? agg.TakeSnapshot() : null;
    }

    public void Dispose() => _listener.Dispose();
}

/// <summary>Thread-safe aggregation for a single instrument</summary>
public class InstrumentAggregation
{
    public string Name { get; }
    public string MeterName { get; }
    public string InstrumentType { get; }
    private readonly ConcurrentDictionary<string, SeriesData> _series = new();

    public InstrumentAggregation(string name, string meterName, string instrumentType)
    {
        Name = name;
        MeterName = meterName;
        InstrumentType = instrumentType;
    }

    public void Record(string tagKey, double value)
    {
        var series = _series.GetOrAdd(tagKey, _ => new SeriesData());
        series.Record(value);
    }

    public InstrumentSnapshot TakeSnapshot() => new()
    {
        Name = Name,
        MeterName = MeterName,
        InstrumentType = InstrumentType,
        Series = _series.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.TakeSnapshot())
    };
}

/// <summary>Thread-safe accumulation for a single tag combination</summary>
public class SeriesData
{
    private long _count;
    private double _sum;
    private double _min = double.MaxValue;
    private double _max = double.MinValue;
    private double _last;

    public void Record(double value)
    {
        Interlocked.Increment(ref _count);
        Volatile.Write(ref _last, value);

        // Thread-safe double accumulation using CAS
        double current;

        // Sum
        do { current = _sum; }
        while (Interlocked.CompareExchange(ref _sum, current + value, current) != current);

        // Min
        do { current = _min; if (current <= value) break; }
        while (Interlocked.CompareExchange(ref _min, value, current) != current);

        // Max
        do { current = _max; if (current >= value) break; }
        while (Interlocked.CompareExchange(ref _max, value, current) != current);
    }

    public SeriesSnapshot TakeSnapshot() => new()
    {
        Count = Volatile.Read(ref _count),
        Sum = Volatile.Read(ref _sum),
        Min = _count > 0 ? Volatile.Read(ref _min) : 0,
        Max = Volatile.Read(ref _max),
        Last = Volatile.Read(ref _last)
    };
}

public record InstrumentSnapshot
{
    public string Name { get; init; } = "";
    public string MeterName { get; init; } = "";
    public string InstrumentType { get; init; } = "";
    public Dictionary<string, SeriesSnapshot> Series { get; init; } = new();

    /// <summary>Total across all tag combinations</summary>
    public long TotalCount => Series.Values.Sum(s => s.Count);
    public double TotalSum => Series.Values.Sum(s => s.Sum);
}

public record SeriesSnapshot
{
    public long Count { get; init; }
    public double Sum { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Last { get; init; }
    public double Average => Count > 0 ? Sum / Count : 0;
}

// ============================================================================
// USAGE
// ============================================================================

// Registration:
// builder.Services.AddSingleton(new InProcessMetricAggregator("MyApp.Orders", "MyApp.Api"));

// Diagnostic/health endpoint:
// app.MapGet("/diagnostics/metrics", (InProcessMetricAggregator aggregator) =>
// {
//     var snapshot = aggregator.GetSnapshot();
//     return Results.Ok(snapshot.Select(kvp => new
//     {
//         Instrument = kvp.Key,
//         kvp.Value.InstrumentType,
//         kvp.Value.TotalCount,
//         kvp.Value.TotalSum,
//         Series = kvp.Value.Series.Select(s => new
//         {
//             Tags = s.Key,
//             s.Value.Count,
//             s.Value.Average,
//             s.Value.Min,
//             s.Value.Max,
//             s.Value.Last
//         })
//     }));
// });
