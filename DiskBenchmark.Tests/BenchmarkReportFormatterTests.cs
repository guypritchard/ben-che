using DiskBenchmark.Core;
using DiskBenchmark.Core.Models;

namespace DiskBenchmark.Tests;

/// <summary>
/// Tests for BenchmarkReportFormatter.
/// </summary>
public class BenchmarkReportFormatterTests
{
    private static BenchmarkReport CreateSampleReport()
    {
        return new BenchmarkReport
        {
            TargetPath = "C:\\TestPath",
            DriveInfo = new DriveDetails
            {
                Name = "C:\\",
                VolumeLabel = "TestDrive",
                DriveFormat = "NTFS",
                TotalSizeBytes = 500L * 1024 * 1024 * 1024,
                AvailableFreeSpaceBytes = 250L * 1024 * 1024 * 1024,
                IsNetworkDrive = false,
                IsRemovable = false
            },
            StartTime = DateTimeOffset.Now.AddMinutes(-5),
            EndTime = DateTimeOffset.Now,
            Options = new BenchmarkOptions { TargetPath = "C:\\TestPath" },
            Results =
            [
                new BenchmarkResult
                {
                    OperationType = BenchmarkOperationType.SequentialWrite,
                    BlockSize = BlockSizes.Large,
                    TotalBytes = 1024L * 1024 * 1024,
                    Duration = TimeSpan.FromSeconds(5)
                },
                new BenchmarkResult
                {
                    OperationType = BenchmarkOperationType.SequentialRead,
                    BlockSize = BlockSizes.Large,
                    TotalBytes = 1024L * 1024 * 1024,
                    Duration = TimeSpan.FromSeconds(3)
                }
            ]
        };
    }

    [Fact]
    public void FormatAsText_ShouldContainDriveInfo()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var text = BenchmarkReportFormatter.FormatAsText(report);

        // Assert
        Assert.Contains("DISK BENCHMARK REPORT", text);
        Assert.Contains("TestDrive", text);
        Assert.Contains("NTFS", text);
        Assert.Contains("Seq", text); // "Seq Read" or "Seq Write"
    }

    [Fact]
    public void FormatAsText_ShouldContainResults()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var text = BenchmarkReportFormatter.FormatAsText(report);

        // Assert
        Assert.Contains("MB/s", text);
        Assert.Contains("RESULTS", text);
    }

    [Fact]
    public void FormatAsJson_ShouldBeValidJson()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var json = BenchmarkReportFormatter.FormatAsJson(report);

        // Assert
        Assert.NotEmpty(json);
        Assert.StartsWith("{", json);
        Assert.Contains("targetPath", json);
        Assert.Contains("results", json);
    }

    [Fact]
    public void FormatAsCsv_ShouldHaveHeaders()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var csv = BenchmarkReportFormatter.FormatAsCsv(report);

        // Assert
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2); // Header + at least one result
        Assert.Contains("Operation", lines[0]);
        Assert.Contains("BlockSize", lines[0]);
        Assert.Contains("ThroughputMBps", lines[0]);
    }

    [Fact]
    public void FormatAsCsv_ShouldHaveCorrectRowCount()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var csv = BenchmarkReportFormatter.FormatAsCsv(report);

        // Assert
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(report.Results.Count + 1, lines.Length); // Header + results
    }

    [Fact]
    public void FormatAsText_WithErrors_ShouldShowErrors()
    {
        // Arrange
        var report = CreateSampleReport() with
        {
            Errors = ["Test error 1", "Test error 2"]
        };

        // Act
        var text = BenchmarkReportFormatter.FormatAsText(report);

        // Assert
        Assert.Contains("ERRORS", text);
        Assert.Contains("Test error 1", text);
        Assert.Contains("Test error 2", text);
    }
}
