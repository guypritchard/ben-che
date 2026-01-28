using DiskBench.Metrics;

namespace DiskBench.Core;

/// <summary>
/// Contains per-second throughput data for time series analysis.
/// </summary>
public sealed class TimeSeriesSample
{
    /// <summary>
    /// Second offset from start of measured period.
    /// </summary>
    public required int SecondOffset { get; init; }

    /// <summary>
    /// Bytes transferred during this second.
    /// </summary>
    public required long Bytes { get; init; }

    /// <summary>
    /// IO operations completed during this second.
    /// </summary>
    public required long Operations { get; init; }

    /// <summary>
    /// Throughput in bytes per second.
    /// </summary>
    public double BytesPerSecond => Bytes;

    /// <summary>
    /// Throughput in IOPS.
    /// </summary>
    public double Iops => Operations;
}

/// <summary>
/// Latency percentiles in microseconds.
/// </summary>
public sealed class LatencyPercentiles
{
    /// <summary>
    /// Minimum latency.
    /// </summary>
    public required double MinUs { get; init; }

    /// <summary>
    /// 50th percentile (median) latency.
    /// </summary>
    public required double P50Us { get; init; }

    /// <summary>
    /// 90th percentile latency.
    /// </summary>
    public required double P90Us { get; init; }

    /// <summary>
    /// 95th percentile latency.
    /// </summary>
    public required double P95Us { get; init; }

    /// <summary>
    /// 99th percentile latency.
    /// </summary>
    public required double P99Us { get; init; }

    /// <summary>
    /// 99.9th percentile latency.
    /// </summary>
    public required double P999Us { get; init; }

    /// <summary>
    /// Maximum latency.
    /// </summary>
    public required double MaxUs { get; init; }

    /// <summary>
    /// Mean latency.
    /// </summary>
    public required double MeanUs { get; init; }

    /// <summary>
    /// Creates latency percentiles from a histogram.
    /// </summary>
    public static LatencyPercentiles FromHistogram(LatencyHistogram histogram, double ticksPerMicrosecond)
    {
        ArgumentNullException.ThrowIfNull(histogram);
        return new LatencyPercentiles
        {
            MinUs = histogram.MinTicks / ticksPerMicrosecond,
            P50Us = histogram.GetPercentileTicks(0.50) / ticksPerMicrosecond,
            P90Us = histogram.GetPercentileTicks(0.90) / ticksPerMicrosecond,
            P95Us = histogram.GetPercentileTicks(0.95) / ticksPerMicrosecond,
            P99Us = histogram.GetPercentileTicks(0.99) / ticksPerMicrosecond,
            P999Us = histogram.GetPercentileTicks(0.999) / ticksPerMicrosecond,
            MaxUs = histogram.MaxTicks / ticksPerMicrosecond,
            MeanUs = histogram.MeanTicks / ticksPerMicrosecond
        };
    }
}

/// <summary>
/// Result of a single benchmark trial.
/// </summary>
public sealed class TrialResult
{
    /// <summary>
    /// Trial number (1-based).
    /// </summary>
    public required int TrialNumber { get; init; }

    /// <summary>
    /// Total bytes transferred during measured period.
    /// </summary>
    public required long TotalBytes { get; init; }

    /// <summary>
    /// Total IO operations during measured period.
    /// </summary>
    public required long TotalOperations { get; init; }

    /// <summary>
    /// Read operations count.
    /// </summary>
    public required long ReadOperations { get; init; }

    /// <summary>
    /// Write operations count.
    /// </summary>
    public required long WriteOperations { get; init; }

    /// <summary>
    /// Actual measured duration.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Throughput in bytes per second.
    /// </summary>
    public double BytesPerSecond => TotalBytes / Duration.TotalSeconds;

    /// <summary>
    /// Throughput in IOPS.
    /// </summary>
    public double Iops => TotalOperations / Duration.TotalSeconds;

    /// <summary>
    /// Latency percentiles.
    /// </summary>
    public required LatencyPercentiles Latency { get; init; }

    /// <summary>
    /// Per-second time series (if collected).
    /// </summary>
    public IReadOnlyList<TimeSeriesSample>? TimeSeries { get; init; }

    /// <summary>
    /// Bytes allocated during measured window (if tracked).
    /// </summary>
    public long? AllocatedBytes { get; init; }

    /// <summary>
    /// Any warnings generated during the trial.
    /// </summary>
    public IReadOnlyList<string>? Warnings { get; init; }
}

/// <summary>
/// Aggregated result for a workload across all trials.
/// </summary>
public sealed record WorkloadResult
{
    /// <summary>
    /// The workload specification.
    /// </summary>
    public required WorkloadSpec Workload { get; init; }

    /// <summary>
    /// Results from individual trials.
    /// </summary>
    public required IReadOnlyList<TrialResult> Trials { get; init; }

    /// <summary>
    /// Mean throughput in bytes per second.
    /// </summary>
    public required double MeanBytesPerSecond { get; init; }

    /// <summary>
    /// Standard deviation of throughput in bytes per second.
    /// </summary>
    public required double StdDevBytesPerSecond { get; init; }

    /// <summary>
    /// Mean IOPS.
    /// </summary>
    public required double MeanIops { get; init; }

    /// <summary>
    /// Standard deviation of IOPS.
    /// </summary>
    public required double StdDevIops { get; init; }

    /// <summary>
    /// Aggregated latency percentiles (mean across trials).
    /// </summary>
    public required LatencyPercentiles MeanLatency { get; init; }

    /// <summary>
    /// 95% confidence interval for throughput (if computed).
    /// </summary>
    public (double Lower, double Upper)? ThroughputCI { get; init; }

    /// <summary>
    /// 95% confidence interval for IOPS (if computed).
    /// </summary>
    public (double Lower, double Upper)? IopsCI { get; init; }
}

/// <summary>
/// Complete benchmark result.
/// </summary>
public sealed class BenchmarkResult
{
    /// <summary>
    /// The benchmark plan that was executed.
    /// </summary>
    public required BenchmarkPlan Plan { get; init; }

    /// <summary>
    /// Results for each workload.
    /// </summary>
    public required IReadOnlyList<WorkloadResult> Workloads { get; init; }

    /// <summary>
    /// When the benchmark started.
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// When the benchmark completed.
    /// </summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// Total duration of the benchmark.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// System information at time of benchmark.
    /// </summary>
    public SystemInfo? SystemInfo { get; init; }
}

/// <summary>
/// System information collected during benchmark.
/// </summary>
public sealed class SystemInfo
{
    /// <summary>
    /// Operating system version.
    /// </summary>
    public required string OsVersion { get; init; }

    /// <summary>
    /// Processor name.
    /// </summary>
    public string? ProcessorName { get; init; }

    /// <summary>
    /// Number of logical processors.
    /// </summary>
    public required int LogicalProcessors { get; init; }

    /// <summary>
    /// Total physical memory in bytes.
    /// </summary>
    public required long TotalMemoryBytes { get; init; }

    /// <summary>
    /// .NET runtime version.
    /// </summary>
    public required string RuntimeVersion { get; init; }
}
