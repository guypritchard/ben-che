using System.Diagnostics;
using System.Security.Cryptography;
using DiskBenchmark.Core.Models;

namespace DiskBenchmark.Core;

/// <summary>
/// High-performance disk benchmark engine using modern .NET 10 features.
/// </summary>
public sealed partial class DiskBenchmarkEngine : IDiskBenchmarkEngine
{
    private const string TestFileName = "diskbench_testfile.tmp";

    /// <summary>
    /// Event raised when benchmark progress updates.
    /// </summary>
    public event EventHandler<BenchmarkProgressEventArgs>? ProgressChanged;

    /// <inheritdoc />
    public async Task<BenchmarkReport> RunBenchmarkAsync(BenchmarkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Directory.Exists(options.TargetPath) && !Path.Exists(options.TargetPath))
        {
            throw new DirectoryNotFoundException($"Target path not found: {options.TargetPath}");
        }

        var startTime = DateTimeOffset.Now;
        var results = new List<BenchmarkResult>();
        var errors = new List<string>();
        var driveInfo = GetDriveDetails(options.TargetPath);
        var testFilePath = GetTestFilePath(options.TargetPath);

        // Validate we have enough space
        var requiredSpace = options.TestFileSizeBytes * 2; // Buffer for safety
        if (driveInfo.AvailableFreeSpaceBytes < requiredSpace)
        {
            throw new InvalidOperationException(
                $"Insufficient disk space. Required: {requiredSpace / (1024 * 1024)} MB, " +
                $"Available: {driveInfo.AvailableFreeSpaceBytes / (1024 * 1024)} MB");
        }

        var blockSizes = GetBlockSizesToTest(options);
        var totalOperations = CalculateTotalOperations(options, blockSizes);
        var currentOperation = 0;

        try
        {
            foreach (var blockSize in blockSizes)
            {
                options.CancellationToken.ThrowIfCancellationRequested();

                // Sequential Write Tests
                if (options.RunSequentialWrite)
                {
                    currentOperation++;
                    ReportProgress(currentOperation, totalOperations, 
                        $"Sequential Write ({FormatBlockSize(blockSize)})");

                    var writeResults = new List<BenchmarkResult>();
                    for (int i = 0; i < options.Iterations; i++)
                    {
                        // Delete existing file before each write iteration for accurate timing
                        DeleteTestFile(testFilePath);
                        
                        var result = await RunWriteBenchmarkAsync(
                            testFilePath,
                            blockSize,
                            options.TestFileSizeBytes,
                            options.CancellationToken);
                        writeResults.Add(result);
                    }
                    results.Add(AverageResults(writeResults));
                }

                // Sequential Read Tests - reuse the file from write test if available
                if (options.RunSequentialRead)
                {
                    currentOperation++;
                    ReportProgress(currentOperation, totalOperations, 
                        $"Sequential Read ({FormatBlockSize(blockSize)})");

                    // Ensure we have a test file to read
                    if (!File.Exists(testFilePath))
                    {
                        await CreateTestFileAsync(testFilePath, blockSize, options.TestFileSizeBytes, options.CancellationToken);
                    }

                    var readResults = new List<BenchmarkResult>();
                    for (int i = 0; i < options.Iterations; i++)
                    {
                        var result = await RunReadBenchmarkAsync(
                            testFilePath,
                            blockSize,
                            options.CancellationToken);
                        readResults.Add(result);
                    }
                    results.Add(AverageResults(readResults));
                }

                // Clean up after each block size test
                if (options.CleanupAfterTest)
                {
                    DeleteTestFile(testFilePath);
                }
            }
        }
        catch (OperationCanceledException)
        {
            errors.Add("Benchmark was cancelled by user.");
        }
        catch (Exception ex)
        {
            errors.Add($"Benchmark error: {ex.Message}");
        }
        finally
        {
            if (options.CleanupAfterTest)
            {
                CleanupTestFiles(options.TargetPath);
            }
        }

        return new BenchmarkReport
        {
            TargetPath = options.TargetPath,
            DriveInfo = driveInfo,
            StartTime = startTime,
            EndTime = DateTimeOffset.Now,
            Results = results.AsReadOnly(),
            Options = options,
            Errors = errors.AsReadOnly()
        };
    }

    /// <inheritdoc />
    public async Task<BenchmarkResult> RunSingleBenchmarkAsync(
        string targetPath,
        BenchmarkOperationType operationType,
        int blockSize,
        long totalBytes,
        CancellationToken cancellationToken = default)
    {
        var testFilePath = GetTestFilePath(targetPath);
        var stopwatch = new Stopwatch();

        try
        {
            switch (operationType)
            {
                case BenchmarkOperationType.SequentialWrite:
                    stopwatch.Start();
                    await PerformSequentialWriteAsync(testFilePath, blockSize, totalBytes, cancellationToken);
                    stopwatch.Stop();
                    break;

                case BenchmarkOperationType.SequentialRead:
                    // Ensure file exists for read test
                    if (!File.Exists(testFilePath))
                    {
                        await PerformSequentialWriteAsync(testFilePath, blockSize, totalBytes, cancellationToken);
                    }
                    stopwatch.Start();
                    await PerformSequentialReadAsync(testFilePath, blockSize, cancellationToken);
                    stopwatch.Stop();
                    totalBytes = new FileInfo(testFilePath).Length;
                    break;

                default:
                    throw new NotSupportedException($"Operation type {operationType} is not yet supported.");
            }

            return new BenchmarkResult
            {
                OperationType = operationType,
                BlockSize = blockSize,
                TotalBytes = totalBytes,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception) when (operationType == BenchmarkOperationType.SequentialRead && !File.Exists(testFilePath))
        {
            throw new InvalidOperationException("Test file not found for read operation.");
        }
        finally
        {
            // Clean up the test file after each single benchmark call
            DeleteTestFile(testFilePath);
        }
    }

    private async Task<BenchmarkResult> RunWriteBenchmarkAsync(
        string testFilePath,
        int blockSize,
        long totalBytes,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        await PerformSequentialWriteAsync(testFilePath, blockSize, totalBytes, cancellationToken);
        stopwatch.Stop();

        return new BenchmarkResult
        {
            OperationType = BenchmarkOperationType.SequentialWrite,
            BlockSize = blockSize,
            TotalBytes = totalBytes,
            Duration = stopwatch.Elapsed
        };
    }

    private async Task<BenchmarkResult> RunReadBenchmarkAsync(
        string testFilePath,
        int blockSize,
        CancellationToken cancellationToken)
    {
        var fileSize = new FileInfo(testFilePath).Length;
        
        var stopwatch = Stopwatch.StartNew();
        await PerformSequentialReadAsync(testFilePath, blockSize, cancellationToken);
        stopwatch.Stop();

        return new BenchmarkResult
        {
            OperationType = BenchmarkOperationType.SequentialRead,
            BlockSize = blockSize,
            TotalBytes = fileSize,
            Duration = stopwatch.Elapsed
        };
    }

    private async Task CreateTestFileAsync(
        string testFilePath,
        int blockSize,
        long totalBytes,
        CancellationToken cancellationToken)
    {
        await PerformSequentialWriteAsync(testFilePath, blockSize, totalBytes, cancellationToken);
    }

    /// <inheritdoc />
    public DriveDetails GetDriveDetails(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath) ?? fullPath;

        // Handle network paths
        if (path.StartsWith(@"\\") || path.StartsWith("//"))
        {
            return GetNetworkDriveDetails(path);
        }

        var driveInfo = new DriveInfo(root);

        return new DriveDetails
        {
            Name = driveInfo.Name,
            VolumeLabel = driveInfo.IsReady ? driveInfo.VolumeLabel : null,
            DriveFormat = driveInfo.IsReady ? driveInfo.DriveFormat : null,
            TotalSizeBytes = driveInfo.IsReady ? driveInfo.TotalSize : 0,
            AvailableFreeSpaceBytes = driveInfo.IsReady ? driveInfo.AvailableFreeSpace : 0,
            IsNetworkDrive = driveInfo.DriveType == DriveType.Network,
            IsRemovable = driveInfo.DriveType == DriveType.Removable
        };
    }

    private static DriveDetails GetNetworkDriveDetails(string path)
    {
        var directoryInfo = new DirectoryInfo(path);
        
        // For network paths, we can't easily get total size without WMI
        // We'll use the available space from Directory methods if accessible
        long availableSpace = 0;
        long totalSpace = 0;

        try
        {
            var driveRoot = Path.GetPathRoot(path);
            if (driveRoot != null)
            {
                // Try to get space info using native methods
                if (GetDiskFreeSpaceEx(path, out var freeBytesAvailable, out var totalBytes, out _))
                {
                    availableSpace = (long)freeBytesAvailable;
                    totalSpace = (long)totalBytes;
                }
            }
        }
        catch
        {
            // Ignore errors when getting network drive info
        }

        return new DriveDetails
        {
            Name = path,
            VolumeLabel = "Network Share",
            DriveFormat = "Unknown",
            TotalSizeBytes = totalSpace,
            AvailableFreeSpaceBytes = availableSpace,
            IsNetworkDrive = true,
            IsRemovable = false
        };
    }

    [System.Runtime.InteropServices.LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf16)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool GetDiskFreeSpaceEx(
        string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);

    [System.Runtime.InteropServices.LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf16)]
    private static partial nint CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        nint lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        nint hTemplateFile);

    [System.Runtime.InteropServices.LibraryImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint hObject);

    [System.Runtime.InteropServices.LibraryImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool FlushFileBuffers(nint hFile);

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint CREATE_ALWAYS = 2;
    private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    private const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;
    private const uint FILE_FLAG_SEQUENTIAL_SCAN = 0x08000000;

    /// <summary>
    /// Flushes the file system cache for a file by opening it with no buffering.
    /// This forces subsequent reads to come from disk, not cache.
    /// </summary>
    private static void FlushFileFromCache(string filePath)
    {
        // Open the file with no buffering to invalidate cache
        var handle = CreateFileW(
            filePath,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            nint.Zero,
            OPEN_EXISTING,
            FILE_FLAG_NO_BUFFERING,
            nint.Zero);

        if (handle != nint.Zero && handle != new nint(-1))
        {
            CloseHandle(handle);
        }
    }

    private async Task PerformSequentialWriteAsync(
        string filePath,
        int blockSize,
        long totalBytes,
        CancellationToken cancellationToken)
    {
        // Use FILE_FLAG_NO_BUFFERING equivalent for more accurate results
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 0, // Unbuffered
            Options = FileOptions.WriteThrough | FileOptions.Asynchronous
        };

        await using var fileStream = new FileStream(filePath, options);

        // Create random data buffer to avoid compression affecting results
        var buffer = new byte[blockSize];
        RandomNumberGenerator.Fill(buffer);

        long bytesWritten = 0;
        while (bytesWritten < totalBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var bytesToWrite = (int)Math.Min(blockSize, totalBytes - bytesWritten);
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesToWrite), cancellationToken);
            bytesWritten += bytesToWrite;
        }

        await fileStream.FlushAsync(cancellationToken);
    }

    private async Task PerformSequentialReadAsync(
        string filePath,
        int blockSize,
        CancellationToken cancellationToken)
    {
        // Flush the file from OS cache to ensure we read from disk
        FlushFileFromCache(filePath);

        // Use RandomAccess API for unbuffered reading
        using var handle = File.OpenHandle(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileOptions.None);

        var fileLength = RandomAccess.GetLength(handle);
        var buffer = new byte[blockSize];
        long position = 0;

        while (position < fileLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var bytesToRead = (int)Math.Min(blockSize, fileLength - position);
            var bytesRead = await RandomAccess.ReadAsync(handle, buffer.AsMemory(0, bytesToRead), position, cancellationToken);
            
            if (bytesRead == 0) break;
            position += bytesRead;
        }
    }

    private static string GetTestFilePath(string targetPath)
    {
        var directory = Directory.Exists(targetPath) ? targetPath : Path.GetDirectoryName(targetPath) ?? targetPath;
        return Path.Combine(directory, TestFileName);
    }

    private static void DeleteTestFile(string testFilePath)
    {
        try
        {
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private static void CleanupTestFiles(string targetPath)
    {
        var directory = Directory.Exists(targetPath) ? targetPath : Path.GetDirectoryName(targetPath) ?? targetPath;
        var testFilePath = Path.Combine(directory, TestFileName);
        DeleteTestFile(testFilePath);
    }

    private static List<int> GetBlockSizesToTest(BenchmarkOptions options)
    {
        var sizes = new List<int>();
        
        if (options.RunSmallBlocks) sizes.Add(BlockSizes.Small);
        if (options.RunMediumBlocks) sizes.Add(BlockSizes.Medium);
        if (options.RunLargeBlocks) sizes.Add(BlockSizes.Large);
        
        return sizes;
    }

    private static int CalculateTotalOperations(BenchmarkOptions options, List<int> blockSizes)
    {
        var operationsPerSize = 0;
        if (options.RunSequentialRead) operationsPerSize++;
        if (options.RunSequentialWrite) operationsPerSize++;
        
        return blockSizes.Count * operationsPerSize;
    }

    private static BenchmarkResult AverageResults(List<BenchmarkResult> results)
    {
        if (results.Count == 0)
            throw new ArgumentException("No results to average.", nameof(results));

        var first = results[0];
        var totalDuration = TimeSpan.FromTicks(results.Sum(r => r.Duration.Ticks) / results.Count);

        return new BenchmarkResult
        {
            OperationType = first.OperationType,
            BlockSize = first.BlockSize,
            TotalBytes = first.TotalBytes,
            Duration = totalDuration
        };
    }

    private void ReportProgress(int current, int total, string operation)
    {
        ProgressChanged?.Invoke(this, new BenchmarkProgressEventArgs
        {
            CurrentOperation = current,
            TotalOperations = total,
            OperationDescription = operation,
            PercentComplete = (int)(current * 100.0 / total)
        });
    }

    private static string FormatBlockSize(int blockSize) => blockSize switch
    {
        <= 1024 => $"{blockSize} B",
        <= 1024 * 1024 => $"{blockSize / 1024} KB",
        _ => $"{blockSize / (1024 * 1024)} MB"
    };
}

/// <summary>
/// Event arguments for benchmark progress updates.
/// </summary>
public sealed class BenchmarkProgressEventArgs : EventArgs
{
    /// <summary>
    /// Current operation number.
    /// </summary>
    public int CurrentOperation { get; init; }

    /// <summary>
    /// Total number of operations.
    /// </summary>
    public int TotalOperations { get; init; }

    /// <summary>
    /// Description of the current operation.
    /// </summary>
    public required string OperationDescription { get; init; }

    /// <summary>
    /// Percentage complete (0-100).
    /// </summary>
    public int PercentComplete { get; init; }
}
