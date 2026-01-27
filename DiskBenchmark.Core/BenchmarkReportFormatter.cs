using System.Text;
using DiskBenchmark.Core.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace DiskBenchmark.Core;

/// <summary>
/// Formats benchmark reports for display.
/// </summary>
public static class BenchmarkReportFormatter
{
    /// <summary>
    /// Writes the benchmark report directly to the console using Spectre.Console.
    /// </summary>
    public static void WriteToConsole(BenchmarkReport report)
    {
        // Header
        var header = new FigletText("DISK BENCH")
            .Color(Color.Cyan1)
            .Centered();
        AnsiConsole.Write(header);
        AnsiConsole.WriteLine();

        // Drive Info Panel
        var driveGrid = new Grid();
        driveGrid.AddColumn();
        driveGrid.AddColumn();
        driveGrid.AddRow("[grey]Target:[/]", $"[white]{report.TargetPath.EscapeMarkup()}[/]");
        driveGrid.AddRow("[grey]Volume:[/]", $"[white]{(report.DriveInfo.VolumeLabel ?? "N/A").EscapeMarkup()}[/]");
        driveGrid.AddRow("[grey]Format:[/]", $"[white]{report.DriveInfo.DriveFormat ?? "Unknown"}[/]");
        driveGrid.AddRow("[grey]Total Size:[/]", $"[white]{report.DriveInfo.TotalSizeFormatted}[/]");
        driveGrid.AddRow("[grey]Free Space:[/]", $"[white]{report.DriveInfo.AvailableSpaceFormatted}[/]");
        driveGrid.AddRow("[grey]Drive Type:[/]", $"[white]{GetDriveTypeString(report.DriveInfo)}[/]");

        var drivePanel = new Panel(driveGrid)
            .Header("[cyan]Drive Information[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1);
        AnsiConsole.Write(drivePanel);
        AnsiConsole.WriteLine();

        // Timing Info
        var timingGrid = new Grid();
        timingGrid.AddColumn();
        timingGrid.AddColumn();
        timingGrid.AddRow("[grey]Started:[/]", $"[white]{report.StartTime:yyyy-MM-dd HH:mm:ss}[/]");
        timingGrid.AddRow("[grey]Duration:[/]", $"[white]{report.TotalDuration.TotalSeconds:F1} seconds[/]");

        var timingPanel = new Panel(timingGrid)
            .Header("[cyan]Timing[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1);
        AnsiConsole.Write(timingPanel);
        AnsiConsole.WriteLine();

        // Results Table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title("[cyan]Benchmark Results[/]");

        table.AddColumn(new TableColumn("[yellow]Operation[/]").LeftAligned());
        table.AddColumn(new TableColumn("[yellow]Block Size[/]").RightAligned());
        table.AddColumn(new TableColumn("[yellow]Throughput[/]").RightAligned());
        table.AddColumn(new TableColumn("[yellow]IOPS[/]").RightAligned());
        table.AddColumn(new TableColumn("[yellow]Latency[/]").RightAligned());

        foreach (var result in report.Results)
        {
            var opType = result.OperationType switch
            {
                BenchmarkOperationType.SequentialRead => "[green]Sequential Read[/]",
                BenchmarkOperationType.SequentialWrite => "[blue]Sequential Write[/]",
                BenchmarkOperationType.RandomRead => "[green]Random Read[/]",
                BenchmarkOperationType.RandomWrite => "[blue]Random Write[/]",
                _ => "[grey]Unknown[/]"
            };

            var blockSize = FormatBlockSize(result.BlockSize);
            var throughput = FormatThroughput(result.ThroughputMBps);
            var iops = FormatIops(result.Iops);
            var latency = FormatLatency(result.AverageLatencyMicroseconds);

            // Color throughput based on performance
            var throughputColor = result.ThroughputMBps switch
            {
                >= 1000 => "green",
                >= 100 => "yellow",
                _ => "red"
            };

            table.AddRow(
                opType,
                $"[white]{blockSize}[/]",
                $"[{throughputColor}]{throughput}[/]",
                $"[white]{iops}[/]",
                $"[white]{latency}[/]"
            );
        }

        AnsiConsole.Write(table);

        // Errors
        if (report.Errors.Count > 0)
        {
            AnsiConsole.WriteLine();
            var errorPanel = new Panel(string.Join("\n", report.Errors.Select(e => $"• {e.EscapeMarkup()}")))
                .Header("[red]Errors[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Red);
            AnsiConsole.Write(errorPanel);
        }
    }

    /// <summary>
    /// Creates a renderable report that can be written to console.
    /// </summary>
    public static IRenderable CreateReport(BenchmarkReport report)
    {
        var layout = new Rows(
            CreateDriveInfoPanel(report),
            new Text(""),
            CreateTimingPanel(report),
            new Text(""),
            CreateResultsTable(report)
        );

        if (report.Errors.Count > 0)
        {
            return new Rows(layout, new Text(""), CreateErrorPanel(report));
        }

        return layout;
    }

    private static Panel CreateDriveInfoPanel(BenchmarkReport report)
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow("[grey]Target:[/]", $"[white]{report.TargetPath.EscapeMarkup()}[/]");
        grid.AddRow("[grey]Volume:[/]", $"[white]{(report.DriveInfo.VolumeLabel ?? "N/A").EscapeMarkup()}[/]");
        grid.AddRow("[grey]Format:[/]", $"[white]{report.DriveInfo.DriveFormat ?? "Unknown"}[/]");
        grid.AddRow("[grey]Total Size:[/]", $"[white]{report.DriveInfo.TotalSizeFormatted}[/]");
        grid.AddRow("[grey]Free Space:[/]", $"[white]{report.DriveInfo.AvailableSpaceFormatted}[/]");
        grid.AddRow("[grey]Drive Type:[/]", $"[white]{GetDriveTypeString(report.DriveInfo)}[/]");

        return new Panel(grid)
            .Header("[cyan]Drive Information[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1);
    }

    private static Panel CreateTimingPanel(BenchmarkReport report)
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow("[grey]Started:[/]", $"[white]{report.StartTime:yyyy-MM-dd HH:mm:ss}[/]");
        grid.AddRow("[grey]Duration:[/]", $"[white]{report.TotalDuration.TotalSeconds:F1} seconds[/]");

        return new Panel(grid)
            .Header("[cyan]Timing[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1);
    }

    private static Table CreateResultsTable(BenchmarkReport report)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title("[cyan]Benchmark Results[/]");

        table.AddColumn(new TableColumn("[yellow]Operation[/]").LeftAligned());
        table.AddColumn(new TableColumn("[yellow]Block Size[/]").RightAligned());
        table.AddColumn(new TableColumn("[yellow]Throughput[/]").RightAligned());
        table.AddColumn(new TableColumn("[yellow]IOPS[/]").RightAligned());
        table.AddColumn(new TableColumn("[yellow]Latency[/]").RightAligned());

        foreach (var result in report.Results)
        {
            var opType = result.OperationType switch
            {
                BenchmarkOperationType.SequentialRead => "[green]Sequential Read[/]",
                BenchmarkOperationType.SequentialWrite => "[blue]Sequential Write[/]",
                BenchmarkOperationType.RandomRead => "[green]Random Read[/]",
                BenchmarkOperationType.RandomWrite => "[blue]Random Write[/]",
                _ => "[grey]Unknown[/]"
            };

            var throughputColor = result.ThroughputMBps switch
            {
                >= 1000 => "green",
                >= 100 => "yellow",
                _ => "red"
            };

            table.AddRow(
                opType,
                $"[white]{FormatBlockSize(result.BlockSize)}[/]",
                $"[{throughputColor}]{FormatThroughput(result.ThroughputMBps)}[/]",
                $"[white]{FormatIops(result.Iops)}[/]",
                $"[white]{FormatLatency(result.AverageLatencyMicroseconds)}[/]"
            );
        }

        return table;
    }

    private static Panel CreateErrorPanel(BenchmarkReport report)
    {
        return new Panel(string.Join("\n", report.Errors.Select(e => $"• {e.EscapeMarkup()}")))
            .Header("[red]Errors[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Red);
    }

    /// <summary>
    /// Formats a benchmark report as a plain text string for console display.
    /// </summary>
    public static string FormatAsText(BenchmarkReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("DISK BENCHMARK REPORT");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();
        sb.AppendLine($"Target:      {report.TargetPath}");
        sb.AppendLine($"Volume:      {report.DriveInfo.VolumeLabel ?? "N/A"}");
        sb.AppendLine($"Format:      {report.DriveInfo.DriveFormat ?? "Unknown"}");
        sb.AppendLine($"Total Size:  {report.DriveInfo.TotalSizeFormatted}");
        sb.AppendLine($"Free Space:  {report.DriveInfo.AvailableSpaceFormatted}");
        sb.AppendLine($"Drive Type:  {GetDriveTypeString(report.DriveInfo)}");
        sb.AppendLine();
        sb.AppendLine($"Started:     {report.StartTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Duration:    {report.TotalDuration.TotalSeconds:F1} seconds");
        sb.AppendLine();
        sb.AppendLine("RESULTS");
        sb.AppendLine(new string('-', 50));
        sb.AppendLine($"{"Operation",-18} {"Block",-8} {"Throughput",-12} {"IOPS",-10} {"Latency",-12}");
        sb.AppendLine(new string('-', 50));

        foreach (var result in report.Results)
        {
            var opType = result.OperationType switch
            {
                BenchmarkOperationType.SequentialRead => "Seq Read",
                BenchmarkOperationType.SequentialWrite => "Seq Write",
                BenchmarkOperationType.RandomRead => "Rnd Read",
                BenchmarkOperationType.RandomWrite => "Rnd Write",
                _ => "Unknown"
            };

            sb.AppendLine($"{opType,-18} {FormatBlockSize(result.BlockSize),-8} {FormatThroughput(result.ThroughputMBps),-12} {FormatIops(result.Iops),-10} {FormatLatency(result.AverageLatencyMicroseconds),-12}");
        }

        if (report.Errors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("ERRORS:");
            foreach (var error in report.Errors)
            {
                sb.AppendLine($"  • {error}");
            }
        }

        return sb.ToString();
    }

    private static string FormatBlockSize(int blockSize) => blockSize switch
    {
        <= 1024 => $"{blockSize} B",
        <= 1024 * 1024 => $"{blockSize / 1024} KB",
        _ => $"{blockSize / (1024 * 1024)} MB"
    };

    private static string FormatThroughput(double mbps) => mbps switch
    {
        >= 1000 => $"{mbps / 1000:F2} GB/s",
        >= 100 => $"{mbps:F0} MB/s",
        >= 10 => $"{mbps:F1} MB/s",
        _ => $"{mbps:F2} MB/s"
    };

    private static string FormatIops(double iops) => iops switch
    {
        >= 1_000_000 => $"{iops / 1_000_000:F2}M",
        >= 1000 => $"{iops / 1000:F1}K",
        _ => $"{iops:F0}"
    };

    private static string FormatLatency(double microseconds) => microseconds switch
    {
        >= 1_000_000 => $"{microseconds / 1_000_000:F2} s",
        >= 1000 => $"{microseconds / 1000:F2} ms",
        _ => $"{microseconds:F0} μs"
    };

    /// <summary>
    /// Formats a benchmark report as JSON.
    /// </summary>
    public static string FormatAsJson(BenchmarkReport report)
    {
        return System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Formats a benchmark report as CSV.
    /// </summary>
    public static string FormatAsCsv(BenchmarkReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Operation,BlockSize,TotalBytes,DurationMs,ThroughputMBps,IOPS,LatencyMicroseconds");

        foreach (var result in report.Results)
        {
            sb.AppendLine($"{result.OperationType},{result.BlockSize},{result.TotalBytes}," +
                         $"{result.Duration.TotalMilliseconds:F2},{result.ThroughputMBps:F2}," +
                         $"{result.Iops:F2},{result.AverageLatencyMicroseconds:F2}");
        }

        return sb.ToString();
    }

    private static string GetDriveTypeString(DriveDetails driveInfo)
    {
        if (driveInfo.IsNetworkDrive) return "Network";
        if (driveInfo.IsRemovable) return "Removable";
        return "Local";
    }
}
