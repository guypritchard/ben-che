using DiskBenchmark.Core.Models;

namespace DiskBenchmark.Tests;

/// <summary>
/// Tests for DriveDetails model.
/// </summary>
public class DriveDetailsTests
{
    [Theory]
    [InlineData(1024L * 1024 * 1024 * 1024, "1.00 TB")]
    [InlineData(500L * 1024 * 1024 * 1024, "500.00 GB")]
    [InlineData(256L * 1024 * 1024, "256.00 MB")]
    [InlineData(64L * 1024, "64.00 KB")]
    [InlineData(512L, "512 B")]
    public void TotalSizeFormatted_ShouldFormatCorrectly(long bytes, string expected)
    {
        // Arrange
        var details = new DriveDetails
        {
            Name = "C:\\",
            TotalSizeBytes = bytes,
            AvailableFreeSpaceBytes = bytes / 2
        };

        // Act & Assert
        Assert.Equal(expected, details.TotalSizeFormatted);
    }

    [Fact]
    public void DriveDetails_NetworkDrive_ShouldSetCorrectly()
    {
        // Arrange & Act
        var details = new DriveDetails
        {
            Name = @"\\server\share",
            VolumeLabel = "Network Share",
            IsNetworkDrive = true,
            IsRemovable = false,
            TotalSizeBytes = 0,
            AvailableFreeSpaceBytes = 0
        };

        // Assert
        Assert.True(details.IsNetworkDrive);
        Assert.False(details.IsRemovable);
    }

    [Fact]
    public void DriveDetails_RemovableDrive_ShouldSetCorrectly()
    {
        // Arrange & Act
        var details = new DriveDetails
        {
            Name = "E:\\",
            VolumeLabel = "USB Drive",
            DriveFormat = "exFAT",
            IsNetworkDrive = false,
            IsRemovable = true,
            TotalSizeBytes = 32L * 1024 * 1024 * 1024,
            AvailableFreeSpaceBytes = 16L * 1024 * 1024 * 1024
        };

        // Assert
        Assert.False(details.IsNetworkDrive);
        Assert.True(details.IsRemovable);
        Assert.Equal("exFAT", details.DriveFormat);
    }
}
