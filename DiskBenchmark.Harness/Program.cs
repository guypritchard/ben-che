using DiskBenchmark.Core;
using DiskBenchmark.Core.Models;
using Spectre.Console;

namespace DiskBenchmark.Harness;

/// <summary>
/// Interactive test harness for the disk benchmark library.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        PrintBanner();

        // Parse command line or use interactive mode
        var targetPath = args.Length > 0 ? args[0] : GetTargetPathInteractive();
        
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            AnsiConsole.MarkupLine("[red]No target path specified. Exiting.[/]");
            return 1;
        }

        // Validate path exists
        if (!Directory.Exists(targetPath) && !File.Exists(targetPath))
        {
            AnsiConsole.MarkupLine($"[red]Error: Path not found: {targetPath.EscapeMarkup()}[/]");
            return 1;
        }

        var (options, saveResults, outputPath) = ParseCommandLineArgs(targetPath, args);
        
        // Show configuration
        var configTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn("[grey]Setting[/]")
            .AddColumn("[grey]Value[/]");
        
        configTable.AddRow("Target", $"[white]{targetPath.EscapeMarkup()}[/]");
        configTable.AddRow("Test File Size", $"[white]{options.TestFileSizeBytes / (1024 * 1024)} MB[/]");
        configTable.AddRow("Iterations", $"[white]{options.Iterations}[/]");
        if (saveResults == true)
        {
            configTable.AddRow("Save Results", $"[green]Yes[/]");
            if (!string.IsNullOrEmpty(outputPath))
                configTable.AddRow("Output Path", $"[white]{outputPath.EscapeMarkup()}[/]");
        }
        AnsiConsole.Write(configTable);
        AnsiConsole.WriteLine();

        var engine = new DiskBenchmarkEngine();
        
        // Show drive info
        var driveInfo = engine.GetDriveDetails(targetPath);
        PrintDriveInfo(driveInfo);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            AnsiConsole.MarkupLine("\n[yellow]Cancelling benchmark...[/]");
        };

        options = options with { CancellationToken = cts.Token };

        try
        {
            BenchmarkReport? report = null;

            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[cyan]Running benchmark...[/]");
                    
                    engine.ProgressChanged += (_, e) =>
                    {
                        task.Description = $"[cyan]{e.OperationDescription}[/]";
                        task.Value = e.PercentComplete;
                    };

                    report = await engine.RunBenchmarkAsync(options);
                    task.Value = 100;
                });

            AnsiConsole.WriteLine();

            // Display results using Spectre.Console
            if (report != null)
            {
                BenchmarkReportFormatter.WriteToConsole(report);

                // Save results if requested via command line
                if (saveResults == true)
                {
                    await SaveResultsAsync(report, outputPath);
                }

                return report.IsSuccessful ? 0 : 1;
            }

            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void PrintBanner()
    {
        AnsiConsole.Write(
            new FigletText("DISK BENCH")
                .Color(Color.Cyan1)
                .Centered());
        
        AnsiConsole.Write(
            new Rule("[grey]Disk Benchmark Tool v1.0 (.NET 10)[/]")
                .RuleStyle(Style.Parse("cyan"))
                .Centered());
        
        AnsiConsole.WriteLine();
    }

    private static string GetTargetPathInteractive()
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .ToList();

        if (drives.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No drives available.[/]");
            return "";
        }

        // Build selection prompt
        var choices = drives.Select(d =>
        {
            var driveType = d.DriveType switch
            {
                DriveType.Fixed => "[blue]Local[/]",
                DriveType.Removable => "[yellow]Removable[/]",
                DriveType.Network => "[green]Network[/]",
                DriveType.CDRom => "[grey]CD-ROM[/]",
                _ => "[grey]Other[/]"
            };
            var freeSpace = FormatBytes(d.AvailableFreeSpace);
            return $"{d.Name} {d.VolumeLabel} ({driveType}) - {freeSpace} free";
        }).ToList();

        choices.Add("[grey]Enter custom path...[/]");

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Select a drive to benchmark:[/]")
                .PageSize(10)
                .HighlightStyle(Style.Parse("cyan"))
                .AddChoices(choices));

        if (selection.Contains("Enter custom path"))
        {
            return AnsiConsole.Ask<string>("[cyan]Enter path:[/]");
        }

        // Extract drive letter from selection
        var driveLetter = selection.Split(' ')[0];
        return driveLetter;
    }

    private static (BenchmarkOptions Options, bool? SaveResults, string? OutputPath) ParseCommandLineArgs(string targetPath, string[] args)
    {
        // Parse optional arguments
        long testSizeMB = 256; // Default 256 MB
        int iterations = 3;
        bool runSmall = true, runMedium = true, runLarge = true;
        bool? saveResults = null;
        string? outputPath = null;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith("--size=") && long.TryParse(args[i][7..], out var size))
                testSizeMB = size;
            else if (args[i].StartsWith("--iterations=") && int.TryParse(args[i][13..], out var iter))
                iterations = iter;
            else if (args[i] == "--no-small")
                runSmall = false;
            else if (args[i] == "--no-medium")
                runMedium = false;
            else if (args[i] == "--no-large")
                runLarge = false;
            else if (args[i] == "--quick")
            {
                testSizeMB = 64;
                iterations = 1;
            }
            else if (args[i] == "--save")
                saveResults = true;
            else if (args[i] == "--no-save")
                saveResults = false;
            else if (args[i].StartsWith("--output="))
            {
                outputPath = args[i][9..];
                saveResults = true;
            }
        }

        var options = new BenchmarkOptions
        {
            TargetPath = targetPath,
            TestFileSizeBytes = testSizeMB * 1024 * 1024,
            Iterations = iterations,
            RunSmallBlocks = runSmall,
            RunMediumBlocks = runMedium,
            RunLargeBlocks = runLarge
        };

        return (options, saveResults, outputPath);
    }

    private static void PrintDriveInfo(DriveDetails info)
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow("[grey]Name:[/]", $"[white]{info.Name.EscapeMarkup()}[/]");
        grid.AddRow("[grey]Volume:[/]", $"[white]{(info.VolumeLabel ?? "N/A").EscapeMarkup()}[/]");
        grid.AddRow("[grey]Format:[/]", $"[white]{info.DriveFormat ?? "Unknown"}[/]");
        grid.AddRow("[grey]Total:[/]", $"[white]{info.TotalSizeFormatted}[/]");
        grid.AddRow("[grey]Free:[/]", $"[white]{info.AvailableSpaceFormatted}[/]");
        grid.AddRow("[grey]Type:[/]", $"[white]{GetDriveType(info)}[/]");

        var panel = new Panel(grid)
            .Header("[yellow]Drive Information[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Yellow);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static string GetDriveType(DriveDetails info)
    {
        if (info.IsNetworkDrive) return "Network";
        if (info.IsRemovable) return "Removable";
        return "Local Disk";
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 40 => $"{bytes / (double)(1L << 40):F2} TB",
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F2} MB",
        _ => $"{bytes / (double)(1L << 10):F2} KB"
    };

    private static async Task SaveResultsAsync(BenchmarkReport report, string? outputPath)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        
        // Use provided output path or default to desktop
        string basePath;
        if (!string.IsNullOrEmpty(outputPath))
        {
            // If output path is a directory, create files there
            if (Directory.Exists(outputPath))
            {
                basePath = Path.Combine(outputPath, $"DiskBenchmark_{timestamp}");
            }
            else
            {
                // Use the path as-is (without extension, we'll add them)
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                basePath = Path.GetExtension(outputPath) != "" 
                    ? Path.Combine(Path.GetDirectoryName(outputPath) ?? "", Path.GetFileNameWithoutExtension(outputPath))
                    : outputPath;
            }
        }
        else
        {
            basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"DiskBenchmark_{timestamp}");
        }

        // Save in multiple formats
        var jsonPath = basePath + ".json";
        var csvPath = basePath + ".csv";
        var textPath = basePath + ".txt";

        await File.WriteAllTextAsync(jsonPath, BenchmarkReportFormatter.FormatAsJson(report));
        await File.WriteAllTextAsync(csvPath, BenchmarkReportFormatter.FormatAsCsv(report));
        await File.WriteAllTextAsync(textPath, BenchmarkReportFormatter.FormatAsText(report));

        AnsiConsole.MarkupLine("[green]Results saved to:[/]");
        AnsiConsole.MarkupLine($"  [grey]•[/] [link]{jsonPath.EscapeMarkup()}[/]");
        AnsiConsole.MarkupLine($"  [grey]•[/] [link]{csvPath.EscapeMarkup()}[/]");
        AnsiConsole.MarkupLine($"  [grey]•[/] [link]{textPath.EscapeMarkup()}[/]");
    }
}
