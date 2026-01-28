using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DiskBench.Core;
using DiskBench.Win32;

namespace DiskBench.Cli;

/// <summary>
/// DiskBench command-line interface.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Entry point.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        return command switch
        {
            "run" => await RunCommandAsync(args[1..]).ConfigureAwait(false),
            "quick" => await QuickCommandAsync(args[1..]).ConfigureAwait(false),
            "profile" => await ProfileCommandAsync(args[1..]).ConfigureAwait(false),
            "profiles" => ListProfiles(),
            "info" => InfoCommand(args[1..]),
            "-h" or "--help" or "help" => PrintUsage(),
            _ => PrintUnknownCommand(command)
        };
    }

    private static int PrintUsage()
    {
        Console.WriteLine("""
            DiskBench - Storage Benchmarking Tool

            Usage: diskbench <command> [options]

            Commands:
              run       Run a benchmark with specified options
              quick     Run a quick benchmark with common workloads
              profile   Run a usage profile benchmark (real-world patterns)
              profiles  List all available usage profiles
              info      Display disk information

            Profile Command (simplest):
              diskbench profile <name> [drive|path]

              Examples:
                diskbench profile gaming              # Test in current directory
                diskbench profile gaming D:\          # Test on D: drive
                diskbench profile gaming C:\temp      # Test in specific folder
                diskbench profile database -s 8G      # Override file size

              Options:
                -s, --size <size>      Override file size (default: profile-specific)
                -t, --trials <n>       Number of trials per workload (default: 3)
                -d, --duration <sec>   Measured duration in seconds (default: 30)
                -o, --output <file>    Output JSON file for results

            Run Command (advanced):
              diskbench run [options]

              Options:
                -f, --file <path>      Target file path (default: diskbench_test.dat)
                -s, --size <size>      Test file size (e.g., 1G, 512M) (default: 1G)
                -t, --trials <n>       Number of trials per workload (default: 3)
                -d, --duration <sec>   Measured duration in seconds (default: 30)
                -w, --warmup <sec>     Warmup duration in seconds (default: 5)
                -o, --output <file>    Output JSON file for results
                --buffered             Use buffered IO

            Available Profiles:
              gaming, streaming, compiling, browsing, database,
              vm, fileserver, media, os, backup

            Examples:
              diskbench profile gaming
              diskbench profile database D:\test.dat -s 8G
              diskbench quick
              diskbench info C:\
            """);
        return 0;
    }

    private static int PrintUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Use 'diskbench --help' for usage information.");
        return 1;
    }

    private static async Task<int> RunCommandAsync(string[] args)
    {
        // Parse arguments
        string file = "diskbench_test.dat";
        string size = "1G";
        int trials = 3;
        int duration = 30;
        int warmup = 5;
        string? output = null;
        bool buffered = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-f" or "--file":
                    file = args[++i];
                    break;
                case "-s" or "--size":
                    size = args[++i];
                    break;
                case "-t" or "--trials":
                    trials = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "-d" or "--duration":
                    duration = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "-w" or "--warmup":
                    warmup = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "-o" or "--output":
                    output = args[++i];
                    break;
                case "--buffered":
                    buffered = true;
                    break;
            }
        }

        return await RunBenchmarkAsync(file, size, trials, duration, warmup, output, buffered).ConfigureAwait(false);
    }

    private static async Task<int> QuickCommandAsync(string[] args)
    {
        string? file = null;
        string size = "1G";

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith('-'))
            {
                switch (arg)
                {
                    case "-f" or "--file":
                        file = args[++i];
                        break;
                    case "-s" or "--size":
                        size = args[++i];
                        break;
                }
            }
            else if (file == null)
            {
                file = arg;
            }
        }

        file = GenerateTestFilePath(file, "quick");
        return await RunQuickBenchmarkAsync(file, size).ConfigureAwait(false);
    }

    private static async Task<int> ProfileCommandAsync(string[] args)
    {
        string? profileName = null;
        string? file = null;
        string? sizeOverride = null;
        int trials = 3;
        int duration = 30;
        string? output = null;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            
            // Check if it's a flag or a positional argument
            if (arg.StartsWith('-'))
            {
                switch (arg)
                {
                    case "-p" or "--profile":
                        profileName = args[++i];
                        break;
                    case "-f" or "--file":
                        file = args[++i];
                        break;
                    case "-s" or "--size":
                        sizeOverride = args[++i];
                        break;
                    case "-t" or "--trials":
                        trials = int.Parse(args[++i], CultureInfo.InvariantCulture);
                        break;
                    case "-d" or "--duration":
                        duration = int.Parse(args[++i], CultureInfo.InvariantCulture);
                        break;
                    case "-o" or "--output":
                        output = args[++i];
                        break;
                }
            }
            else if (profileName == null)
            {
                // First positional argument is the profile name
                profileName = arg;
            }
            else if (file == null)
            {
                // Second positional argument is the file path
                file = arg;
            }
        }

        if (string.IsNullOrEmpty(profileName))
        {
            Console.Error.WriteLine("Error: Profile name is required.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Usage: diskbench profile <name> [file] [options]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Examples:");
            Console.Error.WriteLine("  diskbench profile gaming");
            Console.Error.WriteLine("  diskbench profile gaming D:\\");
            Console.Error.WriteLine("  diskbench profile gaming -s 4G -d 60");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Use 'diskbench profiles' to see available profiles.");
            return 1;
        }

        var profile = ResolveProfile(profileName);
        if (profile == null)
        {
            Console.Error.WriteLine($"Error: Unknown profile '{profileName}'");
            Console.Error.WriteLine("Use 'diskbench profiles' to see available profiles.");
            return 1;
        }

        // Generate file path
        file = GenerateTestFilePath(file, profileName);

        long? fileSize = sizeOverride != null ? ParseSize(sizeOverride) : null;
        return await RunProfileBenchmarkAsync(profile, file, fileSize, trials, duration, output).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates a test file path based on input.
    /// If input is null, uses current directory.
    /// If input is a drive letter or directory, creates a unique filename there.
    /// If input is a full file path, uses it directly.
    /// </summary>
    private static string GenerateTestFilePath(string? input, string profileName)
    {
        // No input - use current directory with profile-named file
        if (string.IsNullOrEmpty(input))
        {
            return $"diskbench_{profileName}_{DateTime.Now:yyyyMMdd_HHmmss}.dat";
        }

        // Check if it's a drive root (e.g., "D:", "D:\", "D:/")
        if (input.Length <= 3 && input.Length >= 2 && char.IsLetter(input[0]) && input[1] == ':')
        {
            var drive = input[..2] + Path.DirectorySeparatorChar;
            return Path.Combine(drive, $"diskbench_{profileName}_{DateTime.Now:yyyyMMdd_HHmmss}.dat");
        }

        // Check if it's an existing directory
        if (Directory.Exists(input))
        {
            return Path.Combine(input, $"diskbench_{profileName}_{DateTime.Now:yyyyMMdd_HHmmss}.dat");
        }

        // Assume it's a full file path
        return input;
    }

    private static int ListProfiles()
    {
        Console.WriteLine();
        Console.WriteLine("Available Usage Profiles:");
        Console.WriteLine("=========================");
        Console.WriteLine();

        foreach (var profile in UsageProfiles.All)
        {
            var shortName = GetProfileShortName(profile.Type);
            Console.WriteLine($"  {shortName,-12} {profile.Name}");
            Console.WriteLine($"              {profile.Description}");
            Console.WriteLine($"              Recommended file size: {FormatSize(profile.RecommendedFileSize)}");
            Console.WriteLine($"              Workloads: {profile.Workloads.Count}");
            Console.WriteLine();
        }

        Console.WriteLine("Usage: diskbench profile <name> [file] [options]");
        Console.WriteLine("       diskbench profile gaming D:\\test.dat");
        Console.WriteLine();
        return 0;
    }

    private static UsageProfile? ResolveProfile(string name)
    {
        var normalized = name.ToLowerInvariant().Trim();
        return normalized switch
        {
            "gaming" or "game" => UsageProfiles.Get(UsageProfileType.Gaming),
            "streaming" or "video" or "videostreaming" => UsageProfiles.Get(UsageProfileType.VideoStreaming),
            "compiling" or "compile" or "build" or "compilation" => UsageProfiles.Get(UsageProfileType.Compiling),
            "browsing" or "browser" or "web" or "webbrowsing" => UsageProfiles.Get(UsageProfileType.WebBrowsing),
            "database" or "db" or "sql" or "oltp" => UsageProfiles.Get(UsageProfileType.Database),
            "vm" or "virtualmachine" or "hyperv" or "virtual" => UsageProfiles.Get(UsageProfileType.VirtualMachine),
            "fileserver" or "server" or "nas" or "file" => UsageProfiles.Get(UsageProfileType.FileServer),
            "media" or "mediaediting" or "editing" or "video-edit" => UsageProfiles.Get(UsageProfileType.MediaEditing),
            "os" or "operatingsystem" or "system" or "boot" => UsageProfiles.Get(UsageProfileType.OperatingSystem),
            "backup" or "archive" => UsageProfiles.Get(UsageProfileType.Backup),
            _ => null
        };
    }

    private static string GetProfileShortName(UsageProfileType type)
    {
        return type switch
        {
            UsageProfileType.VideoStreaming => "streaming",
            UsageProfileType.Compiling => "compiling",
            UsageProfileType.WebBrowsing => "browsing",
            UsageProfileType.Gaming => "gaming",
            UsageProfileType.Database => "database",
            UsageProfileType.VirtualMachine => "vm",
            UsageProfileType.FileServer => "fileserver",
            UsageProfileType.MediaEditing => "media",
            UsageProfileType.OperatingSystem => "os",
            UsageProfileType.Backup => "backup",
            _ => type.ToString().ToLowerInvariant()
        };
    }

    private static async Task<int> RunProfileBenchmarkAsync(
        UsageProfile profile,
        string file,
        long? fileSize,
        int trials,
        int duration,
        string? output)
    {
        var plan = UsageProfiles.CreatePlan(
            profile,
            file,
            fileSize,
            trials,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(duration));

        var sink = new ConsoleBenchmarkSink();
        await using var engine = new WindowsIoEngine();
        var runner = new BenchmarkRunner(engine, sink);

        try
        {
            Console.WriteLine();
            Console.WriteLine("======================================================================");
            Console.WriteLine($"           DiskBench - {profile.Name} Profile Benchmark");
            Console.WriteLine("======================================================================");
            Console.WriteLine();
            Console.WriteLine($"Profile: {profile.Name}");
            Console.WriteLine($"  {profile.Description}");
            Console.WriteLine();
            Console.WriteLine($"Workloads: {profile.Workloads.Count}");
            foreach (var workload in profile.Workloads)
            {
                Console.WriteLine($"  - {workload.Name} (weight: {workload.Weight}%)");
            }
            Console.WriteLine();

            var result = await runner.RunAsync(plan).ConfigureAwait(false);

            if (output != null)
            {
                var json = JsonSerializer.Serialize(result, JsonOptions);
                await File.WriteAllTextAsync(output, json).ConfigureAwait(false);
                Console.WriteLine($"\nResults written to: {output}");
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("\nBenchmark cancelled.");
            return 1;
        }
#pragma warning disable CA1031 // Catch general exception for CLI error handling
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static int InfoCommand(string[] args)
    {
        string? path = null;
        bool showAll = false;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith('-'))
            {
                switch (arg)
                {
                    case "-p" or "--path":
                        path = args[++i];
                        break;
                    case "-a" or "--all":
                        showAll = true;
                        break;
                }
            }
            else if (path == null)
            {
                path = arg;
            }
        }

        if (showAll || path == null)
        {
            DisplayAllDrives();
        }
        else
        {
            DisplayDiskInfo(path);
        }
        return 0;
    }

    private static async Task<int> RunBenchmarkAsync(
        string file,
        string size,
        int trials,
        int duration,
        int warmup,
        string? output,
        bool buffered)
    {
        var fileSizeBytes = ParseSize(size);
        var plan = CreateDefaultPlan(file, fileSizeBytes, trials, duration, warmup, !buffered);

        var sink = new ConsoleBenchmarkSink();
        await using var engine = new WindowsIoEngine();
        var runner = new BenchmarkRunner(engine, sink);

        try
        {
            Console.WriteLine();
            Console.WriteLine("======================================================================");
            Console.WriteLine("                    DiskBench Storage Benchmark                       ");
            Console.WriteLine("======================================================================");
            Console.WriteLine();

            var result = await runner.RunAsync(plan).ConfigureAwait(false);

            if (output != null)
            {
                var json = JsonSerializer.Serialize(result, JsonOptions);
                await File.WriteAllTextAsync(output, json).ConfigureAwait(false);
                Console.WriteLine($"\nResults written to: {output}");
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("\nBenchmark cancelled.");
            return 1;
        }
#pragma warning disable CA1031 // Catch general exception for CLI error handling
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> RunQuickBenchmarkAsync(string file, string size)
    {
        var fileSizeBytes = ParseSize(size);

        var workloads = new List<WorkloadSpec>
        {
            new()
            {
                Name = "Seq Read 1M Q1",
                FilePath = file,
                FileSize = fileSizeBytes,
                BlockSize = 1 * 1024 * 1024,
                Pattern = AccessPattern.Sequential,
                WritePercent = 0,
                QueueDepth = 1,
                NoBuffering = true
            },
            new()
            {
                Name = "Seq Write 1M Q1",
                FilePath = file,
                FileSize = fileSizeBytes,
                BlockSize = 1 * 1024 * 1024,
                Pattern = AccessPattern.Sequential,
                WritePercent = 100,
                QueueDepth = 1,
                NoBuffering = true
            },
            new()
            {
                Name = "Rand Read 4K Q1",
                FilePath = file,
                FileSize = fileSizeBytes,
                BlockSize = 4 * 1024,
                Pattern = AccessPattern.Random,
                WritePercent = 0,
                QueueDepth = 1,
                NoBuffering = true
            },
            new()
            {
                Name = "Rand Read 4K Q32",
                FilePath = file,
                FileSize = fileSizeBytes,
                BlockSize = 4 * 1024,
                Pattern = AccessPattern.Random,
                WritePercent = 0,
                QueueDepth = 32,
                NoBuffering = true
            },
            new()
            {
                Name = "Rand Write 4K Q1",
                FilePath = file,
                FileSize = fileSizeBytes,
                BlockSize = 4 * 1024,
                Pattern = AccessPattern.Random,
                WritePercent = 100,
                QueueDepth = 1,
                NoBuffering = true
            },
            new()
            {
                Name = "Rand Write 4K Q32",
                FilePath = file,
                FileSize = fileSizeBytes,
                BlockSize = 4 * 1024,
                Pattern = AccessPattern.Random,
                WritePercent = 100,
                QueueDepth = 32,
                NoBuffering = true
            }
        };

        var plan = new BenchmarkPlan
        {
            Name = "Quick Benchmark",
            Workloads = workloads,
            Trials = 3,
            WarmupDuration = TimeSpan.FromSeconds(3),
            MeasuredDuration = TimeSpan.FromSeconds(15),
            CollectTimeSeries = true,
            ComputeConfidenceIntervals = true
        };

        var sink = new ConsoleBenchmarkSink();
        await using var engine = new WindowsIoEngine();
        var runner = new BenchmarkRunner(engine, sink);

        try
        {
            Console.WriteLine();
            Console.WriteLine("======================================================================");
            Console.WriteLine("                    DiskBench Quick Benchmark                         ");
            Console.WriteLine("======================================================================");
            Console.WriteLine();

            await runner.RunAsync(plan).ConfigureAwait(false);
            return 0;
        }
#pragma warning disable CA1031 // Catch general exception for CLI error handling
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void DisplayDiskInfo(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Console.WriteLine($"\nDisk Information for: {fullPath}");
        Console.WriteLine(new string('═', 60));

        var engine = new WindowsIoEngine();

        if (Directory.Exists(fullPath) || File.Exists(fullPath))
        {
            var root = Path.GetPathRoot(fullPath);
            if (root != null)
            {
                var details = engine.GetDriveDetails(root);
                if (details != null)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  {details.BusTypeIcon} Drive:             {details.DriveLetter}");
                    if (!string.IsNullOrEmpty(details.VolumeLabel))
                        Console.WriteLine($"    Volume Label:     {details.VolumeLabel}");
                    Console.WriteLine();

                    Console.WriteLine("  ─── Device ───");
                    if (!string.IsNullOrEmpty(details.ProductId))
                        Console.WriteLine($"    Product:          {details.ProductId}");
                    if (!string.IsNullOrEmpty(details.VendorId))
                        Console.WriteLine($"    Vendor:           {details.VendorId}");
                    if (!string.IsNullOrEmpty(details.SerialNumber))
                        Console.WriteLine($"    Serial:           {details.SerialNumber}");
                    Console.WriteLine();

                    Console.WriteLine("  ─── Connection ───");
                    Console.WriteLine($"    Bus Type:         {details.BusTypeDescription}");
                    Console.WriteLine($"    Performance Tier: {details.PerformanceTier}");
                    Console.WriteLine($"    Removable:        {(details.IsRemovable ? "Yes" : "No")}");
                    Console.WriteLine($"    Command Queuing:  {(details.SupportsCommandQueuing ? "Yes (NCQ/TCQ)" : "No")}");
                    Console.WriteLine();

                    Console.WriteLine("  ─── Storage ───");
                    Console.WriteLine($"    File System:      {details.FileSystem}");
                    Console.WriteLine($"    Total Size:       {FormatBytes(details.TotalSize)}");
                    Console.WriteLine($"    Free Space:       {FormatBytes(details.FreeSpace)} ({100.0 * details.FreeSpace / details.TotalSize:F1}%)");
                    Console.WriteLine();

                    Console.WriteLine("  ─── Sector Size ───");
                    Console.WriteLine($"    Logical:          {details.LogicalSectorSize} bytes");
                    Console.WriteLine($"    Physical:         {details.PhysicalSectorSize} bytes");
                    if (details.PhysicalSectorSize > details.LogicalSectorSize)
                        Console.WriteLine($"    Note:             Advanced Format (AF) disk - 512e emulation");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("  Unable to retrieve drive details.");
                }
            }
        }
        else
        {
            Console.WriteLine("  Path does not exist.");
        }
    }

    private static void DisplayAllDrives()
    {
        Console.WriteLine("\n╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                           AVAILABLE DRIVES                                   ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");

        var engine = new WindowsIoEngine();
        var drives = engine.GetAllDriveDetails();

        Console.WriteLine("║  Icon │ Drive │ Bus Type     │ Size      │ Free      │ Product               ║");
        Console.WriteLine("╠───────┼───────┼──────────────┼───────────┼───────────┼───────────────────────╣");

        foreach (var drive in drives)
        {
            var product = drive.ProductId ?? "Unknown";
            if (product.Length > 20) product = product[..17] + "...";

            Console.WriteLine($"║  {drive.BusTypeIcon,-4} │ {drive.DriveLetter,-5} │ {drive.BusTypeDescription,-12} │ {FormatBytes(drive.TotalSize),9} │ {FormatBytes(drive.FreeSpace),9} │ {product,-21} ║");
        }

        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
    }

    private static BenchmarkPlan CreateDefaultPlan(string file, long fileSize, int trials, int duration, int warmup, bool noBuffering)
    {
        return new BenchmarkPlan
        {
            Name = "Default Benchmark",
            Workloads =
            [
                new WorkloadSpec
                {
                    Name = "Sequential Read 1M",
                    FilePath = file,
                    FileSize = fileSize,
                    BlockSize = 1024 * 1024,
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 0,
                    QueueDepth = 1,
                    NoBuffering = noBuffering
                },
                new WorkloadSpec
                {
                    Name = "Sequential Write 1M",
                    FilePath = file,
                    FileSize = fileSize,
                    BlockSize = 1024 * 1024,
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 100,
                    QueueDepth = 1,
                    NoBuffering = noBuffering
                },
                new WorkloadSpec
                {
                    Name = "Random Read 4K Q1",
                    FilePath = file,
                    FileSize = fileSize,
                    BlockSize = 4096,
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 1,
                    NoBuffering = noBuffering
                },
                new WorkloadSpec
                {
                    Name = "Random Read 4K Q32",
                    FilePath = file,
                    FileSize = fileSize,
                    BlockSize = 4096,
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 32,
                    NoBuffering = noBuffering
                }
            ],
            Trials = trials,
            WarmupDuration = TimeSpan.FromSeconds(warmup),
            MeasuredDuration = TimeSpan.FromSeconds(duration),
            CollectTimeSeries = true
        };
    }

    private static long ParseSize(string size)
    {
        size = size.Trim().ToUpperInvariant();

        if (size.EndsWith("TB", StringComparison.Ordinal))
            return long.Parse(size[..^2], CultureInfo.InvariantCulture) * 1024L * 1024 * 1024 * 1024;
        if (size.EndsWith('T'))
            return long.Parse(size[..^1], CultureInfo.InvariantCulture) * 1024L * 1024 * 1024 * 1024;
        if (size.EndsWith("GB", StringComparison.Ordinal))
            return long.Parse(size[..^2], CultureInfo.InvariantCulture) * 1024L * 1024 * 1024;
        if (size.EndsWith('G'))
            return long.Parse(size[..^1], CultureInfo.InvariantCulture) * 1024L * 1024 * 1024;
        if (size.EndsWith("MB", StringComparison.Ordinal))
            return long.Parse(size[..^2], CultureInfo.InvariantCulture) * 1024L * 1024;
        if (size.EndsWith('M'))
            return long.Parse(size[..^1], CultureInfo.InvariantCulture) * 1024L * 1024;
        if (size.EndsWith("KB", StringComparison.Ordinal))
            return long.Parse(size[..^2], CultureInfo.InvariantCulture) * 1024L;
        if (size.EndsWith('K'))
            return long.Parse(size[..^1], CultureInfo.InvariantCulture) * 1024L;
        if (size.EndsWith('B'))
            return long.Parse(size[..^1], CultureInfo.InvariantCulture);

        return long.Parse(size, CultureInfo.InvariantCulture);
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int i = 0;
        double value = bytes;
        while (value >= 1024 && i < suffixes.Length - 1)
        {
            value /= 1024;
            i++;
        }
        return $"{value:F2} {suffixes[i]}";
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024 * 1024):F0} TB",
            >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):F0} GB",
            >= 1024L * 1024 => $"{bytes / (1024.0 * 1024):F0} MB",
            >= 1024L => $"{bytes / 1024.0:F0} KB",
            _ => $"{bytes} B"
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
