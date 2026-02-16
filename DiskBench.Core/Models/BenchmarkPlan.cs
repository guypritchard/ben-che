namespace DiskBench.Core;

/// <summary>
/// Defines a complete benchmark plan with multiple workloads and global options.
/// </summary>
public sealed class BenchmarkPlan
{
    /// <summary>
    /// List of workloads to execute.
    /// </summary>
    public required IReadOnlyList<WorkloadSpec> Workloads { get; init; }

    /// <summary>
    /// Number of trials per workload. More trials improve statistical confidence.
    /// </summary>
    public int Trials { get; init; } = 3;

    /// <summary>
    /// Duration of warmup period before each trial (not measured).
    /// Allows caches and controller firmware to stabilize.
    /// </summary>
    public TimeSpan WarmupDuration { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Duration of measured period per trial.
    /// </summary>
    public TimeSpan MeasuredDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Random seed for reproducible IO patterns. Use 0 for random seed.
    /// </summary>
    public int Seed { get; init; }

    /// <summary>
    /// Whether to compute 95% bootstrap confidence intervals.
    /// </summary>
    public bool ComputeConfidenceIntervals { get; init; }

    /// <summary>
    /// Number of bootstrap iterations for confidence interval calculation.
    /// </summary>
    public int BootstrapIterations { get; init; } = 10000;

    /// <summary>
    /// Whether to track per-second time series data.
    /// Useful for detecting cache effects and SLC exhaustion.
    /// </summary>
    public bool CollectTimeSeries { get; init; } = true;

    /// <summary>
    /// Whether to reuse existing test files if they exist and match size.
    /// </summary>
    public bool ReuseExistingFiles { get; init; } = true;

    /// <summary>
    /// Whether to automatically delete test files when benchmark completes.
    /// </summary>
    public bool DeleteOnComplete { get; init; } = true;

    /// <summary>
    /// Whether to check for allocations during measured window (diagnostic).
    /// </summary>
    public bool TrackAllocations { get; init; }

    /// <summary>
    /// Optional plan name for reporting.
    /// </summary>
    public string? Name { get; init; }
}
