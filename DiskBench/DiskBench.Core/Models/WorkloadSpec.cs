namespace DiskBench.Core;

/// <summary>
/// Defines a region within a file for IO operations.
/// </summary>
/// <param name="Offset">Starting offset in bytes (must be aligned for unbuffered IO).</param>
/// <param name="Length">Length in bytes (0 means use file size minus offset).</param>
public readonly record struct FileRegion(long Offset, long Length)
{
    /// <summary>
    /// A region representing the entire file.
    /// </summary>
    public static FileRegion EntireFile => new(0, 0);

    /// <summary>
    /// Creates a region from offset to end of file.
    /// </summary>
    public static FileRegion FromOffset(long offset) => new(offset, 0);
}

/// <summary>
/// Specifies a workload configuration for benchmarking.
/// </summary>
public sealed class WorkloadSpec
{
    /// <summary>
    /// Path to the target file for IO operations.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Target file size in bytes. File will be created/resized to this size.
    /// For accurate results, use a size larger than system RAM.
    /// </summary>
    public required long FileSize { get; init; }

    /// <summary>
    /// IO block size in bytes. Must be aligned to sector size for unbuffered IO.
    /// Common values: 4096 (4KB), 65536 (64KB), 1048576 (1MB).
    /// </summary>
    public required int BlockSize { get; init; }

    /// <summary>
    /// The access pattern (sequential or random).
    /// </summary>
    public AccessPattern Pattern { get; init; } = AccessPattern.Sequential;

    /// <summary>
    /// Percentage of IO operations that are writes (0-100).
    /// 0 = read-only, 100 = write-only, 50 = mixed.
    /// </summary>
    public int WritePercent { get; init; }

    /// <summary>
    /// Queue depth - number of outstanding IO operations.
    /// Higher values improve throughput but increase latency.
    /// </summary>
    public int QueueDepth { get; init; } = 1;

    /// <summary>
    /// Number of worker threads. Each thread maintains its own queue.
    /// Total outstanding IOs = QueueDepth * Threads.
    /// </summary>
    public int Threads { get; init; } = 1;

    /// <summary>
    /// Region of the file to operate on. Use FileRegion.EntireFile for the whole file.
    /// </summary>
    public FileRegion Region { get; init; } = FileRegion.EntireFile;

    /// <summary>
    /// When to flush file buffers.
    /// </summary>
    public FlushPolicy FlushPolicy { get; init; } = FlushPolicy.None;

    /// <summary>
    /// Interval between flushes when FlushPolicy is Interval.
    /// </summary>
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether to use FILE_FLAG_NO_BUFFERING (bypasses OS cache).
    /// Requires aligned buffers, IO sizes, and file offsets.
    /// Recommended for measuring actual device performance.
    /// </summary>
    public bool NoBuffering { get; init; } = true;

    /// <summary>
    /// Whether to use FILE_FLAG_WRITE_THROUGH (bypasses write cache).
    /// Forces writes to complete on physical media before returning.
    /// </summary>
    public bool WriteThrough { get; init; }

    /// <summary>
    /// Optional name for this workload (for reporting).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Creates a descriptive name for the workload based on its configuration.
    /// </summary>
    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(Name))
        {
            return Name;
        }

        var patternStr = Pattern == AccessPattern.Sequential ? "Seq" : "Rand";
        var opStr = WritePercent switch
        {
            0 => "Read",
            100 => "Write",
            _ => $"Mix{WritePercent}"
        };
        var sizeStr = FormatBlockSize(BlockSize);
        return $"{patternStr}{opStr}_{sizeStr}_Q{QueueDepth}T{Threads}";
    }

    private static string FormatBlockSize(int blockSize)
    {
        return blockSize switch
        {
            >= 1048576 when blockSize % 1048576 == 0 => $"{blockSize / 1048576}M",
            >= 1024 when blockSize % 1024 == 0 => $"{blockSize / 1024}K",
            _ => $"{blockSize}B"
        };
    }
}
