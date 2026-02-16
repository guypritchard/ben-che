namespace DiskBench.Core;

/// <summary>
/// Specifies how to prepare a test file.
/// </summary>
public sealed class PrepareSpec
{
    /// <summary>
    /// Path to the file to prepare.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Target file size in bytes.
    /// </summary>
    public required long FileSize { get; init; }

    /// <summary>
    /// Whether to reuse an existing file if it matches the size.
    /// </summary>
    public bool ReuseIfExists { get; init; } = true;

    /// <summary>
    /// Whether to use SetFileValidData for instant allocation (requires privilege).
    /// </summary>
    public bool UseSetValidData { get; init; } = true;

    /// <summary>
    /// Optional pattern to write during preparation (null = zero-fill).
    /// </summary>
    public IReadOnlyList<byte>? FillPattern { get; init; }
}

/// <summary>
/// Specifies parameters for running a single trial.
/// </summary>
public sealed class TrialSpec
{
    /// <summary>
    /// The workload configuration.
    /// </summary>
    public required WorkloadSpec Workload { get; init; }

    /// <summary>
    /// Duration of warmup period.
    /// </summary>
    public TimeSpan WarmupDuration { get; init; }

    /// <summary>
    /// Duration of measured period.
    /// </summary>
    public TimeSpan MeasuredDuration { get; init; }

    /// <summary>
    /// Random seed for this trial.
    /// </summary>
    public int Seed { get; init; }

    /// <summary>
    /// Trial number (1-based).
    /// </summary>
    public int TrialNumber { get; init; }

    /// <summary>
    /// Whether to collect per-second time series.
    /// </summary>
    public bool CollectTimeSeries { get; init; }

    /// <summary>
    /// Whether to track allocations during measured window.
    /// </summary>
    public bool TrackAllocations { get; init; }

    /// <summary>
    /// Sector size for alignment (discovered during prepare).
    /// </summary>
    public int SectorSize { get; init; } = 512;
}
