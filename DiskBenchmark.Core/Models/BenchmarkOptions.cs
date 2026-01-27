namespace DiskBenchmark.Core.Models;

/// <summary>
/// Configuration options for disk benchmark operations.
/// </summary>
public sealed record BenchmarkOptions
{
    /// <summary>
    /// The target path to benchmark (drive root or directory).
    /// </summary>
    public required string TargetPath { get; init; }

    /// <summary>
    /// Size of the test file in bytes. Default is 1 GB.
    /// </summary>
    public long TestFileSizeBytes { get; init; } = 1L * 1024 * 1024 * 1024;

    /// <summary>
    /// Number of iterations to average results. Default is 3.
    /// </summary>
    public int Iterations { get; init; } = 3;

    /// <summary>
    /// Whether to run sequential read tests.
    /// </summary>
    public bool RunSequentialRead { get; init; } = true;

    /// <summary>
    /// Whether to run sequential write tests.
    /// </summary>
    public bool RunSequentialWrite { get; init; } = true;

    /// <summary>
    /// Whether to run small block tests (4 KB).
    /// </summary>
    public bool RunSmallBlocks { get; init; } = true;

    /// <summary>
    /// Whether to run medium block tests (64 KB).
    /// </summary>
    public bool RunMediumBlocks { get; init; } = true;

    /// <summary>
    /// Whether to run large block tests (1 MB).
    /// </summary>
    public bool RunLargeBlocks { get; init; } = true;

    /// <summary>
    /// Whether to delete test files after benchmark completes.
    /// </summary>
    public bool CleanupAfterTest { get; init; } = true;

    /// <summary>
    /// Cancellation token for async operations.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
}

/// <summary>
/// Block size presets for different benchmark scenarios.
/// </summary>
public static class BlockSizes
{
    /// <summary>Small block size: 4 KB - typical for random I/O operations.</summary>
    public const int Small = 4 * 1024;

    /// <summary>Medium block size: 64 KB - balanced performance testing.</summary>
    public const int Medium = 64 * 1024;

    /// <summary>Large block size: 1 MB - sequential throughput testing.</summary>
    public const int Large = 1024 * 1024;
}
