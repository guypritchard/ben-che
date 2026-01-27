using DiskBenchmark.Core;
using DiskBenchmark.Core.Models;

namespace DiskBenchmark.Tests;

/// <summary>
/// Integration tests for DiskBenchmarkEngine.
/// These tests perform actual I/O operations.
/// </summary>
public class DiskBenchmarkEngineTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly DiskBenchmarkEngine _engine;

    public DiskBenchmarkEngineTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"DiskBenchmark_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _engine = new DiskBenchmarkEngine();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void GetDriveDetails_ShouldReturnValidDetails()
    {
        // Arrange
        var path = Path.GetTempPath();

        // Act
        var details = _engine.GetDriveDetails(path);

        // Assert
        Assert.NotNull(details);
        Assert.NotEmpty(details.Name);
        Assert.True(details.TotalSizeBytes > 0);
        Assert.True(details.AvailableFreeSpaceBytes > 0);
    }

    [Fact]
    public async Task RunSingleBenchmark_SequentialWrite_ShouldComplete()
    {
        // Arrange
        const int blockSize = BlockSizes.Medium;
        const long totalBytes = 10L * 1024 * 1024; // 10 MB for quick test

        // Act
        var result = await _engine.RunSingleBenchmarkAsync(
            _testDirectory,
            BenchmarkOperationType.SequentialWrite,
            blockSize,
            totalBytes);

        // Assert
        Assert.Equal(BenchmarkOperationType.SequentialWrite, result.OperationType);
        Assert.Equal(blockSize, result.BlockSize);
        Assert.Equal(totalBytes, result.TotalBytes);
        Assert.True(result.Duration.TotalMilliseconds > 0);
        Assert.True(result.ThroughputMBps > 0);
    }

    [Fact]
    public async Task RunSingleBenchmark_SequentialRead_ShouldComplete()
    {
        // Arrange - First create a file to read
        const int blockSize = BlockSizes.Medium;
        const long totalBytes = 10L * 1024 * 1024; // 10 MB

        await _engine.RunSingleBenchmarkAsync(
            _testDirectory,
            BenchmarkOperationType.SequentialWrite,
            blockSize,
            totalBytes);

        // Act
        var result = await _engine.RunSingleBenchmarkAsync(
            _testDirectory,
            BenchmarkOperationType.SequentialRead,
            blockSize,
            totalBytes);

        // Assert
        Assert.Equal(BenchmarkOperationType.SequentialRead, result.OperationType);
        Assert.True(result.ThroughputMBps > 0);
    }

    [Fact]
    public async Task RunBenchmark_WithMinimalOptions_ShouldComplete()
    {
        // Arrange
        var options = new BenchmarkOptions
        {
            TargetPath = _testDirectory,
            TestFileSizeBytes = 5L * 1024 * 1024, // 5 MB for quick test
            Iterations = 1,
            RunSmallBlocks = false,
            RunMediumBlocks = true,
            RunLargeBlocks = false
        };

        // Act
        var report = await _engine.RunBenchmarkAsync(options);

        // Assert
        Assert.NotNull(report);
        Assert.True(report.IsSuccessful);
        Assert.Equal(_testDirectory, report.TargetPath);
        Assert.NotEmpty(report.Results);
        Assert.True(report.TotalDuration.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task RunBenchmark_ShouldRaiseProgressEvents()
    {
        // Arrange
        var progressEvents = new List<BenchmarkProgressEventArgs>();
        _engine.ProgressChanged += (_, e) => progressEvents.Add(e);

        var options = new BenchmarkOptions
        {
            TargetPath = _testDirectory,
            TestFileSizeBytes = 2L * 1024 * 1024,
            Iterations = 1,
            RunSmallBlocks = false,
            RunMediumBlocks = true,
            RunLargeBlocks = false
        };

        // Act
        await _engine.RunBenchmarkAsync(options);

        // Assert
        Assert.NotEmpty(progressEvents);
        Assert.All(progressEvents, e => Assert.NotEmpty(e.OperationDescription));
    }

    [Fact]
    public async Task RunBenchmark_WithCancellation_ShouldStop()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var options = new BenchmarkOptions
        {
            TargetPath = _testDirectory,
            TestFileSizeBytes = 100L * 1024 * 1024, // Large enough to allow cancellation
            Iterations = 5,
            CancellationToken = cts.Token
        };

        // Cancel after a short delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            cts.Cancel();
        });

        // Act
        var report = await _engine.RunBenchmarkAsync(options);

        // Assert
        Assert.NotNull(report);
        Assert.Contains(report.Errors, e => e.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(BlockSizes.Small)]
    [InlineData(BlockSizes.Medium)]
    [InlineData(BlockSizes.Large)]
    public async Task RunSingleBenchmark_DifferentBlockSizes_ShouldComplete(int blockSize)
    {
        // Arrange
        const long totalBytes = 5L * 1024 * 1024;

        // Act
        var result = await _engine.RunSingleBenchmarkAsync(
            _testDirectory,
            BenchmarkOperationType.SequentialWrite,
            blockSize,
            totalBytes);

        // Assert
        Assert.Equal(blockSize, result.BlockSize);
        Assert.True(result.ThroughputMBps > 0);
        Assert.True(result.Iops > 0);
    }
}
