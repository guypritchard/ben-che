namespace DiskBenchmark.Core.Models;

/// <summary>
/// Results from a single benchmark operation.
/// </summary>
public sealed record BenchmarkResult
{
    /// <summary>
    /// The type of operation performed.
    /// </summary>
    public required BenchmarkOperationType OperationType { get; init; }

    /// <summary>
    /// Block size used for the operation in bytes.
    /// </summary>
    public required int BlockSize { get; init; }

    /// <summary>
    /// Total bytes transferred during the benchmark.
    /// </summary>
    public required long TotalBytes { get; init; }

    /// <summary>
    /// Duration of the benchmark operation.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Throughput in megabytes per second.
    /// </summary>
    public double ThroughputMBps => TotalBytes / Duration.TotalSeconds / (1024 * 1024);

    /// <summary>
    /// Throughput in megabits per second.
    /// </summary>
    public double ThroughputMbps => ThroughputMBps * 8;

    /// <summary>
    /// I/O operations per second.
    /// </summary>
    public double Iops => TotalBytes / BlockSize / Duration.TotalSeconds;

    /// <summary>
    /// Average latency per operation in microseconds.
    /// </summary>
    public double AverageLatencyMicroseconds => Duration.TotalMicroseconds / (TotalBytes / BlockSize);

    /// <summary>
    /// Friendly description of block size category.
    /// </summary>
    public string BlockSizeCategory => BlockSize switch
    {
        <= BlockSizes.Small => "Small (4 KB)",
        <= BlockSizes.Medium => "Medium (64 KB)",
        _ => "Large (1 MB)"
    };

    public override string ToString() =>
        $"{OperationType} [{BlockSizeCategory}]: {ThroughputMBps:F2} MB/s | {Iops:F0} IOPS | {AverageLatencyMicroseconds:F2} μs latency";
}

/// <summary>
/// Type of benchmark operation.
/// </summary>
public enum BenchmarkOperationType
{
    SequentialRead,
    SequentialWrite,
    RandomRead,
    RandomWrite
}
