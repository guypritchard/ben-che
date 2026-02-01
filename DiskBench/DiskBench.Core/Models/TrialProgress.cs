namespace DiskBench.Core;

/// <summary>
/// Progress information during a trial.
/// </summary>
public sealed class TrialProgress
{
    /// <summary>
    /// Whether the trial is in warmup phase.
    /// </summary>
    public required bool IsWarmup { get; init; }

    /// <summary>
    /// Whether the trial is finalizing (draining IO) after measured phase.
    /// </summary>
    public bool IsFinalizing { get; init; }

    /// <summary>
    /// Current phase elapsed time.
    /// </summary>
    public required TimeSpan Elapsed { get; init; }

    /// <summary>
    /// Total phase duration.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Percentage complete (0-100).
    /// </summary>
    public double PercentComplete => Duration.TotalMilliseconds > 0
        ? Math.Min(100, Elapsed.TotalMilliseconds / Duration.TotalMilliseconds * 100)
        : 0;

    /// <summary>
    /// Current throughput in bytes per second.
    /// </summary>
    public required double CurrentBytesPerSecond { get; init; }

    /// <summary>
    /// Current IOPS.
    /// </summary>
    public required double CurrentIops { get; init; }

    /// <summary>
    /// Total bytes transferred so far.
    /// </summary>
    public required long TotalBytes { get; init; }

    /// <summary>
    /// Total operations completed so far.
    /// </summary>
    public required long TotalOperations { get; init; }
}
