using DiskBenchmark.Core.Models;

namespace DiskBenchmark.Core;

/// <summary>
/// Interface for disk benchmark operations.
/// </summary>
public interface IDiskBenchmarkEngine
{
    /// <summary>
    /// Runs a complete benchmark suite on the specified target.
    /// </summary>
    /// <param name="options">Benchmark configuration options.</param>
    /// <returns>A complete benchmark report.</returns>
    Task<BenchmarkReport> RunBenchmarkAsync(BenchmarkOptions options);

    /// <summary>
    /// Runs a single benchmark operation.
    /// </summary>
    /// <param name="targetPath">Target path to benchmark.</param>
    /// <param name="operationType">Type of operation to perform.</param>
    /// <param name="blockSize">Block size in bytes.</param>
    /// <param name="totalBytes">Total bytes to read/write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the benchmark operation.</returns>
    Task<BenchmarkResult> RunSingleBenchmarkAsync(
        string targetPath,
        BenchmarkOperationType operationType,
        int blockSize,
        long totalBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets drive details for the specified path.
    /// </summary>
    /// <param name="path">Path to analyze.</param>
    /// <returns>Drive details for the path.</returns>
    DriveDetails GetDriveDetails(string path);
}
