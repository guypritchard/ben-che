using DiskBenchmark.Core;
using DiskBenchmark.Core.Models;

namespace DiskBenchmark.Tests;

/// <summary>
/// Unit tests for BenchmarkOptions.
/// </summary>
public class BenchmarkOptionsTests
{
    [Fact]
    public void DefaultOptions_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new BenchmarkOptions
        {
            TargetPath = "C:\\"
        };

        // Assert
        Assert.Equal(1L * 1024 * 1024 * 1024, options.TestFileSizeBytes);
        Assert.Equal(3, options.Iterations);
        Assert.True(options.RunSequentialRead);
        Assert.True(options.RunSequentialWrite);
        Assert.True(options.RunSmallBlocks);
        Assert.True(options.RunMediumBlocks);
        Assert.True(options.RunLargeBlocks);
        Assert.True(options.CleanupAfterTest);
    }

    [Fact]
    public void BlockSizes_ShouldHaveCorrectValues()
    {
        Assert.Equal(4 * 1024, BlockSizes.Small);
        Assert.Equal(64 * 1024, BlockSizes.Medium);
        Assert.Equal(1024 * 1024, BlockSizes.Large);
    }

    [Fact]
    public void CustomOptions_ShouldOverrideDefaults()
    {
        // Arrange & Act
        var options = new BenchmarkOptions
        {
            TargetPath = "D:\\",
            TestFileSizeBytes = 512 * 1024 * 1024,
            Iterations = 5,
            RunSmallBlocks = false,
            RunMediumBlocks = true,
            RunLargeBlocks = false
        };

        // Assert
        Assert.Equal(512 * 1024 * 1024, options.TestFileSizeBytes);
        Assert.Equal(5, options.Iterations);
        Assert.False(options.RunSmallBlocks);
        Assert.True(options.RunMediumBlocks);
        Assert.False(options.RunLargeBlocks);
    }
}
