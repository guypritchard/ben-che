using DiskBenchmark.Core.Models;

namespace DiskBenchmark.Tests;

/// <summary>
/// Unit tests for BenchmarkResult.
/// </summary>
public class BenchmarkResultTests
{
    [Fact]
    public void ThroughputMBps_ShouldCalculateCorrectly()
    {
        // Arrange - 1000 MB over 10 seconds = 100 MB/s
        var result = new BenchmarkResult
        {
            OperationType = BenchmarkOperationType.SequentialRead,
            BlockSize = BlockSizes.Large,
            TotalBytes = 1000L * 1024 * 1024, // 1000 MB
            Duration = TimeSpan.FromSeconds(10)
        };

        // Act & Assert
        Assert.Equal(100, result.ThroughputMBps, precision: 1);
    }

    [Fact]
    public void ThroughputMbps_ShouldBe8TimesMBps()
    {
        // Arrange
        var result = new BenchmarkResult
        {
            OperationType = BenchmarkOperationType.SequentialWrite,
            BlockSize = BlockSizes.Large,
            TotalBytes = 1024L * 1024 * 1024,
            Duration = TimeSpan.FromSeconds(10)
        };

        // Act & Assert
        Assert.Equal(result.ThroughputMBps * 8, result.ThroughputMbps, precision: 1);
    }

    [Fact]
    public void Iops_ShouldCalculateCorrectly()
    {
        // Arrange - 1 MB blocks, 100 MB total, 1 second = 100 IOPS
        var result = new BenchmarkResult
        {
            OperationType = BenchmarkOperationType.SequentialRead,
            BlockSize = 1024 * 1024, // 1 MB
            TotalBytes = 100L * 1024 * 1024, // 100 MB
            Duration = TimeSpan.FromSeconds(1)
        };

        // Act & Assert
        Assert.Equal(100, result.Iops, precision: 1);
    }

    [Fact]
    public void BlockSizeCategory_ShouldReturnCorrectCategory()
    {
        // Small
        var small = new BenchmarkResult
        {
            OperationType = BenchmarkOperationType.SequentialRead,
            BlockSize = BlockSizes.Small,
            TotalBytes = 1024,
            Duration = TimeSpan.FromSeconds(1)
        };
        Assert.Contains("Small", small.BlockSizeCategory);

        // Medium
        var medium = new BenchmarkResult
        {
            OperationType = BenchmarkOperationType.SequentialRead,
            BlockSize = BlockSizes.Medium,
            TotalBytes = 1024,
            Duration = TimeSpan.FromSeconds(1)
        };
        Assert.Contains("Medium", medium.BlockSizeCategory);

        // Large
        var large = new BenchmarkResult
        {
            OperationType = BenchmarkOperationType.SequentialRead,
            BlockSize = BlockSizes.Large,
            TotalBytes = 1024,
            Duration = TimeSpan.FromSeconds(1)
        };
        Assert.Contains("Large", large.BlockSizeCategory);
    }

    [Theory]
    [InlineData(BenchmarkOperationType.SequentialRead)]
    [InlineData(BenchmarkOperationType.SequentialWrite)]
    [InlineData(BenchmarkOperationType.RandomRead)]
    [InlineData(BenchmarkOperationType.RandomWrite)]
    public void ToString_ShouldContainOperationType(BenchmarkOperationType opType)
    {
        // Arrange
        var result = new BenchmarkResult
        {
            OperationType = opType,
            BlockSize = BlockSizes.Medium,
            TotalBytes = 1024L * 1024,
            Duration = TimeSpan.FromSeconds(1)
        };

        // Act
        var str = result.ToString();

        // Assert
        Assert.Contains(opType.ToString(), str);
    }
}
