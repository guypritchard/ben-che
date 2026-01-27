namespace DiskBenchmark.Core.Models;

/// <summary>
/// Complete benchmark report containing all test results.
/// </summary>
public sealed record BenchmarkReport
{
    /// <summary>
    /// The target path that was benchmarked.
    /// </summary>
    public required string TargetPath { get; init; }

    /// <summary>
    /// Drive information for the target.
    /// </summary>
    public required DriveDetails DriveInfo { get; init; }

    /// <summary>
    /// When the benchmark started.
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// When the benchmark completed.
    /// </summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// Total benchmark duration.
    /// </summary>
    public TimeSpan TotalDuration => EndTime - StartTime;

    /// <summary>
    /// All benchmark results.
    /// </summary>
    public required IReadOnlyList<BenchmarkResult> Results { get; init; }

    /// <summary>
    /// Options used for this benchmark.
    /// </summary>
    public required BenchmarkOptions Options { get; init; }

    /// <summary>
    /// Any errors that occurred during benchmarking.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Whether the benchmark completed successfully.
    /// </summary>
    public bool IsSuccessful => Errors.Count == 0;
}

/// <summary>
/// Information about the target drive.
/// </summary>
public sealed record DriveDetails
{
    /// <summary>
    /// Drive name or root path.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Volume label if available.
    /// </summary>
    public string? VolumeLabel { get; init; }

    /// <summary>
    /// Drive format (NTFS, exFAT, etc.).
    /// </summary>
    public string? DriveFormat { get; init; }

    /// <summary>
    /// Total size of the drive in bytes.
    /// </summary>
    public long TotalSizeBytes { get; init; }

    /// <summary>
    /// Available free space in bytes.
    /// </summary>
    public long AvailableFreeSpaceBytes { get; init; }

    /// <summary>
    /// Whether this is a network drive.
    /// </summary>
    public bool IsNetworkDrive { get; init; }

    /// <summary>
    /// Whether this is a removable drive.
    /// </summary>
    public bool IsRemovable { get; init; }

    /// <summary>
    /// Total size formatted as string.
    /// </summary>
    public string TotalSizeFormatted => FormatBytes(TotalSizeBytes);

    /// <summary>
    /// Available space formatted as string.
    /// </summary>
    public string AvailableSpaceFormatted => FormatBytes(AvailableFreeSpaceBytes);

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 40 => $"{bytes / (double)(1L << 40):F2} TB",
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F2} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F2} KB",
        _ => $"{bytes} B"
    };
}
