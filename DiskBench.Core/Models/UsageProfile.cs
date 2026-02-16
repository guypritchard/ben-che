namespace DiskBench.Core;

/// <summary>
/// Predefined usage profiles representing real-world workload patterns.
/// </summary>
public enum UsageProfileType
{
    /// <summary>
    /// Video streaming: Large sequential reads, low queue depth.
    /// Simulates streaming video content from disk.
    /// </summary>
    VideoStreaming,

    /// <summary>
    /// Software compilation: Many small random reads, mixed writes.
    /// Simulates reading source files and writing object/binary outputs.
    /// </summary>
    Compiling,

    /// <summary>
    /// Web browsing: Small random reads/writes, bursty access.
    /// Simulates browser cache access and page loading.
    /// </summary>
    WebBrowsing,

    /// <summary>
    /// Gaming: Large sequential reads, random texture streaming.
    /// Simulates asset loading and save file operations.
    /// </summary>
    Gaming,

    /// <summary>
    /// Database server: Random reads/writes, various block sizes.
    /// Simulates OLTP database access patterns.
    /// </summary>
    Database,

    /// <summary>
    /// Virtual machine host: Random I/O, large blocks, high queue depth.
    /// Simulates VM disk access patterns.
    /// </summary>
    VirtualMachine,

    /// <summary>
    /// File server: Mixed sequential/random, varied sizes.
    /// Simulates typical file server workload.
    /// </summary>
    FileServer,

    /// <summary>
    /// Photo/video editing: Large sequential reads, large writes.
    /// Simulates media editing workflows.
    /// </summary>
    MediaEditing,

    /// <summary>
    /// OS/boot operations: Small random reads, occasional writes.
    /// Simulates operating system disk access.
    /// </summary>
    OperatingSystem,

    /// <summary>
    /// Backup operations: Large sequential writes.
    /// Simulates backup software writing data.
    /// </summary>
    Backup
}

/// <summary>
/// Represents a component workload within a usage profile.
/// </summary>
public sealed class ProfileWorkload
{
    /// <summary>
    /// Name of this workload component.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of what this workload simulates.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Weight of this workload (0-100). Higher weight = more time spent on this pattern.
    /// Weights are relative within a profile.
    /// </summary>
    public required int Weight { get; init; }

    /// <summary>
    /// Block size for this workload.
    /// </summary>
    public required int BlockSize { get; init; }

    /// <summary>
    /// Access pattern.
    /// </summary>
    public AccessPattern Pattern { get; init; }

    /// <summary>
    /// Write percentage (0-100).
    /// </summary>
    public int WritePercent { get; init; }

    /// <summary>
    /// Queue depth.
    /// </summary>
    public int QueueDepth { get; init; } = 1;

    /// <summary>
    /// Number of threads.
    /// </summary>
    public int Threads { get; init; } = 1;

    /// <summary>
    /// Whether to bypass OS cache.
    /// </summary>
    public bool NoBuffering { get; init; } = true;

    /// <summary>
    /// Whether to use write-through.
    /// </summary>
    public bool WriteThrough { get; init; }
}

/// <summary>
/// Defines a usage profile with its component workloads.
/// </summary>
public sealed class UsageProfile
{
    /// <summary>
    /// Profile type identifier.
    /// </summary>
    public required UsageProfileType Type { get; init; }

    /// <summary>
    /// Display name for the profile.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of what this profile simulates.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Component workloads that make up this profile.
    /// </summary>
    public required IReadOnlyList<ProfileWorkload> Workloads { get; init; }

    /// <summary>
    /// Recommended minimum file size for accurate results.
    /// </summary>
    public long RecommendedFileSize { get; init; } = 1L * 1024 * 1024 * 1024; // 1 GB default
}

/// <summary>
/// Factory for creating predefined usage profiles.
/// </summary>
public static class UsageProfiles
{
    /// <summary>
    /// Gets all predefined usage profiles.
    /// </summary>
    public static IReadOnlyList<UsageProfile> All { get; } = CreateAllProfiles();

    /// <summary>
    /// Gets a profile by type.
    /// </summary>
    public static UsageProfile Get(UsageProfileType type)
    {
        return All.First(p => p.Type == type);
    }

    /// <summary>
    /// Generates WorkloadSpec instances from a usage profile.
    /// </summary>
    /// <param name="profile">The usage profile.</param>
    /// <param name="filePath">Path to the test file.</param>
    /// <param name="fileSize">Optional file size override (uses profile recommendation if not specified).</param>
    /// <returns>List of workload specifications weighted by profile.</returns>
    public static IReadOnlyList<WorkloadSpec> GenerateWorkloads(
        UsageProfile profile,
        string filePath,
        long? fileSize = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var actualFileSize = fileSize ?? profile.RecommendedFileSize;
        var workloads = new List<WorkloadSpec>();

        foreach (var pw in profile.Workloads)
        {
            workloads.Add(new WorkloadSpec
            {
                Name = pw.Name,
                FilePath = filePath,
                FileSize = actualFileSize,
                BlockSize = pw.BlockSize,
                Pattern = pw.Pattern,
                WritePercent = pw.WritePercent,
                QueueDepth = pw.QueueDepth,
                Threads = pw.Threads,
                NoBuffering = pw.NoBuffering,
                WriteThrough = pw.WriteThrough
            });
        }

        return workloads;
    }

    /// <summary>
    /// Creates a BenchmarkPlan from a usage profile.
    /// </summary>
    /// <param name="profile">The usage profile.</param>
    /// <param name="filePath">Path to the test file.</param>
    /// <param name="fileSize">Optional file size override.</param>
    /// <param name="trials">Number of trials per workload.</param>
    /// <param name="warmupDuration">Warmup duration per workload.</param>
    /// <param name="measuredDuration">Measured duration per workload (weighted by profile).</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>A configured benchmark plan.</returns>
    public static BenchmarkPlan CreatePlan(
        UsageProfile profile,
        string filePath,
        long? fileSize = null,
        int trials = 3,
        TimeSpan? warmupDuration = null,
        TimeSpan? measuredDuration = null,
        int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var actualFileSize = fileSize ?? profile.RecommendedFileSize;
        var warmup = warmupDuration ?? TimeSpan.FromSeconds(5);
        var baseDuration = measuredDuration ?? TimeSpan.FromSeconds(30);

        // Build workloads with profile-prefixed names
        var workloads = new List<WorkloadSpec>();
        foreach (var pw in profile.Workloads)
        {
            workloads.Add(new WorkloadSpec
            {
                Name = $"{profile.Name}: {pw.Name}",
                FilePath = filePath,
                FileSize = actualFileSize,
                BlockSize = pw.BlockSize,
                Pattern = pw.Pattern,
                WritePercent = pw.WritePercent,
                QueueDepth = pw.QueueDepth,
                Threads = pw.Threads,
                NoBuffering = pw.NoBuffering,
                WriteThrough = pw.WriteThrough
            });
        }

        return new BenchmarkPlan
        {
            Name = $"{profile.Name} Profile",
            Workloads = workloads,
            Trials = trials,
            WarmupDuration = warmup,
            MeasuredDuration = baseDuration,
            Seed = seed ?? 0
        };
    }

    private static IReadOnlyList<UsageProfile> CreateAllProfiles()
    {
        return
        [
            CreateVideoStreamingProfile(),
            CreateCompilingProfile(),
            CreateWebBrowsingProfile(),
            CreateGamingProfile(),
            CreateDatabaseProfile(),
            CreateVirtualMachineProfile(),
            CreateFileServerProfile(),
            CreateMediaEditingProfile(),
            CreateOperatingSystemProfile(),
            CreateBackupProfile()
        ];
    }

    private static UsageProfile CreateVideoStreamingProfile()
    {
        return new UsageProfile
        {
            Type = UsageProfileType.VideoStreaming,
            Name = "Video Streaming",
            Description = "Simulates streaming video content: large sequential reads at various bitrates.",
            RecommendedFileSize = 4L * 1024 * 1024 * 1024, // 4 GB
            Workloads =
            [
                new ProfileWorkload
                {
                    Name = "4K Streaming",
                    Description = "4K video streaming (~25 Mbps)",
                    Weight = 40,
                    BlockSize = 1024 * 1024, // 1 MB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 0,
                    QueueDepth = 2,
                    Threads = 1,
                    NoBuffering = false // Video players use OS cache
                },
                new ProfileWorkload
                {
                    Name = "HD Streaming",
                    Description = "1080p video streaming (~8 Mbps)",
                    Weight = 35,
                    BlockSize = 512 * 1024, // 512 KB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 0,
                    QueueDepth = 2,
                    Threads = 1,
                    NoBuffering = false
                },
                new ProfileWorkload
                {
                    Name = "Seek/Skip",
                    Description = "Seeking within video content",
                    Weight = 25,
                    BlockSize = 256 * 1024, // 256 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 1,
                    Threads = 1,
                    NoBuffering = false
                }
            ]
        };
    }

    private static UsageProfile CreateCompilingProfile()
    {
        return new UsageProfile
        {
            Type = UsageProfileType.Compiling,
            Name = "Software Compilation",
            Description = "Simulates compiling software: reading many source files, writing object files and binaries.",
            RecommendedFileSize = 2L * 1024 * 1024 * 1024, // 2 GB
            Workloads =
            [
                new ProfileWorkload
                {
                    Name = "Source Reads",
                    Description = "Reading source code files",
                    Weight = 40,
                    BlockSize = 4 * 1024, // 4 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 8,
                    Threads = 4, // Parallel compilation
                    NoBuffering = false // Compiler uses OS cache
                },
                new ProfileWorkload
                {
                    Name = "Object Writes",
                    Description = "Writing compiled object files",
                    Weight = 30,
                    BlockSize = 64 * 1024, // 64 KB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 100,
                    QueueDepth = 4,
                    Threads = 4,
                    NoBuffering = false
                },
                new ProfileWorkload
                {
                    Name = "Link Phase",
                    Description = "Reading objects, writing binary output",
                    Weight = 20,
                    BlockSize = 128 * 1024, // 128 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 30,
                    QueueDepth = 8,
                    Threads = 2,
                    NoBuffering = false
                },
                new ProfileWorkload
                {
                    Name = "Header Parsing",
                    Description = "Reading header files repeatedly",
                    Weight = 10,
                    BlockSize = 4 * 1024, // 4 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 16,
                    Threads = 4,
                    NoBuffering = false
                }
            ]
        };
    }

    private static UsageProfile CreateWebBrowsingProfile()
    {
        return new UsageProfile
        {
            Type = UsageProfileType.WebBrowsing,
            Name = "Web Browsing",
            Description = "Simulates web browser disk access: cache reads/writes, history, cookies.",
            RecommendedFileSize = 1L * 1024 * 1024 * 1024, // 1 GB
            Workloads =
            [
                new ProfileWorkload
                {
                    Name = "Cache Reads",
                    Description = "Reading cached web content",
                    Weight = 45,
                    BlockSize = 8 * 1024, // 8 KB (typical web asset)
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 4,
                    Threads = 2,
                    NoBuffering = false
                },
                new ProfileWorkload
                {
                    Name = "Cache Writes",
                    Description = "Writing new cache entries",
                    Weight = 25,
                    BlockSize = 16 * 1024, // 16 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 100,
                    QueueDepth = 2,
                    Threads = 1,
                    NoBuffering = false
                },
                new ProfileWorkload
                {
                    Name = "Large Downloads",
                    Description = "Downloading larger files",
                    Weight = 20,
                    BlockSize = 256 * 1024, // 256 KB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 100,
                    QueueDepth = 2,
                    Threads = 1,
                    NoBuffering = false
                },
                new ProfileWorkload
                {
                    Name = "DB Operations",
                    Description = "Browser database (history, cookies)",
                    Weight = 10,
                    BlockSize = 4 * 1024, // 4 KB page size
                    Pattern = AccessPattern.Random,
                    WritePercent = 30,
                    QueueDepth = 1,
                    Threads = 1,
                    NoBuffering = false
                }
            ]
        };
    }

    private static UsageProfile CreateGamingProfile()
    {
        return new UsageProfile
        {
            Type = UsageProfileType.Gaming,
            Name = "Gaming",
            Description = "Simulates game disk access: asset loading, texture streaming, save operations.",
            RecommendedFileSize = 8L * 1024 * 1024 * 1024, // 8 GB
            Workloads =
            [
                new ProfileWorkload
                {
                    Name = "Initial Load",
                    Description = "Loading game assets at startup/level load",
                    Weight = 35,
                    BlockSize = 1024 * 1024, // 1 MB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 0,
                    QueueDepth = 8,
                    Threads = 2,
                    NoBuffering = true // Games often bypass cache
                },
                new ProfileWorkload
                {
                    Name = "Texture Streaming",
                    Description = "Streaming textures during gameplay",
                    Weight = 40,
                    BlockSize = 256 * 1024, // 256 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 16,
                    Threads = 2,
                    NoBuffering = true
                },
                new ProfileWorkload
                {
                    Name = "Audio Streaming",
                    Description = "Streaming audio content",
                    Weight = 15,
                    BlockSize = 64 * 1024, // 64 KB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 0,
                    QueueDepth = 4,
                    Threads = 1,
                    NoBuffering = false
                },
                new ProfileWorkload
                {
                    Name = "Save/Checkpoint",
                    Description = "Saving game progress",
                    Weight = 10,
                    BlockSize = 128 * 1024, // 128 KB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 100,
                    QueueDepth = 2,
                    Threads = 1,
                    NoBuffering = false,
                    WriteThrough = true // Ensure saves are durable
                }
            ]
        };
    }

    private static UsageProfile CreateDatabaseProfile()
    {
        return new UsageProfile
        {
            Type = UsageProfileType.Database,
            Name = "Database Server",
            Description = "Simulates OLTP database: random page reads/writes, log writes.",
            RecommendedFileSize = 4L * 1024 * 1024 * 1024, // 4 GB
            Workloads =
            [
                new ProfileWorkload
                {
                    Name = "Page Reads",
                    Description = "Reading database pages",
                    Weight = 45,
                    BlockSize = 8 * 1024, // 8 KB page
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 32,
                    Threads = 4,
                    NoBuffering = true
                },
                new ProfileWorkload
                {
                    Name = "Page Writes",
                    Description = "Writing dirty pages",
                    Weight = 25,
                    BlockSize = 8 * 1024, // 8 KB page
                    Pattern = AccessPattern.Random,
                    WritePercent = 100,
                    QueueDepth = 16,
                    Threads = 2,
                    NoBuffering = true
                },
                new ProfileWorkload
                {
                    Name = "Log Writes",
                    Description = "Transaction log writes",
                    Weight = 20,
                    BlockSize = 64 * 1024, // 64 KB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 100,
                    QueueDepth = 8,
                    Threads = 1,
                    NoBuffering = true,
                    WriteThrough = true // Logs must be durable
                },
                new ProfileWorkload
                {
                    Name = "Index Scans",
                    Description = "Sequential index scans",
                    Weight = 10,
                    BlockSize = 64 * 1024, // 64 KB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 0,
                    QueueDepth = 8,
                    Threads = 2,
                    NoBuffering = true
                }
            ]
        };
    }

    private static UsageProfile CreateVirtualMachineProfile()
    {
        return new UsageProfile
        {
            Type = UsageProfileType.VirtualMachine,
            Name = "Virtual Machine",
            Description = "Simulates VM disk access: random I/O, large blocks, high concurrency.",
            RecommendedFileSize = 16L * 1024 * 1024 * 1024, // 16 GB
            Workloads =
            [
                new ProfileWorkload
                {
                    Name = "VM Random I/O",
                    Description = "Random read/write from virtual disk",
                    Weight = 50,
                    BlockSize = 64 * 1024, // 64 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 30,
                    QueueDepth = 32,
                    Threads = 4,
                    NoBuffering = true
                },
                new ProfileWorkload
                {
                    Name = "VM Boot/Load",
                    Description = "Sequential reads during boot",
                    Weight = 25,
                    BlockSize = 1024 * 1024, // 1 MB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 0,
                    QueueDepth = 16,
                    Threads = 2,
                    NoBuffering = true
                },
                new ProfileWorkload
                {
                    Name = "VM Paging",
                    Description = "Memory paging operations",
                    Weight = 25,
                    BlockSize = 4 * 1024, // 4 KB page
                    Pattern = AccessPattern.Random,
                    WritePercent = 50,
                    QueueDepth = 64,
                    Threads = 4,
                    NoBuffering = true
                }
            ]
        };
    }

    private static UsageProfile CreateFileServerProfile()
    {
        return new UsageProfile
        {
            Type = UsageProfileType.FileServer,
            Name = "File Server",
            Description = "Simulates file server: mixed file sizes, concurrent access.",
            RecommendedFileSize = 4L * 1024 * 1024 * 1024, // 4 GB
            Workloads =
            [
                new ProfileWorkload
                {
                    Name = "Small Files",
                    Description = "Reading/writing small documents",
                    Weight = 35,
                    BlockSize = 16 * 1024, // 16 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 20,
                    QueueDepth = 16,
                    Threads = 8,
                    NoBuffering = false
                },
                new ProfileWorkload
                {
                    Name = "Large Files",
                    Description = "Transferring large files",
                    Weight = 35,
                    BlockSize = 1024 * 1024, // 1 MB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 30,
                    QueueDepth = 8,
                    Threads = 4,
                    NoBuffering = false
                },
                new ProfileWorkload
                {
                    Name = "Directory Operations",
                    Description = "Metadata/directory access",
                    Weight = 20,
                    BlockSize = 4 * 1024, // 4 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 10,
                    QueueDepth = 8,
                    Threads = 4,
                    NoBuffering = false
                },
                new ProfileWorkload
                {
                    Name = "Search/Index",
                    Description = "File indexing and search",
                    Weight = 10,
                    BlockSize = 32 * 1024, // 32 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 5,
                    QueueDepth = 4,
                    Threads = 2,
                    NoBuffering = false
                }
            ]
        };
    }

    private static UsageProfile CreateMediaEditingProfile()
    {
        return new UsageProfile
        {
            Type = UsageProfileType.MediaEditing,
            Name = "Media Editing",
            Description = "Simulates photo/video editing: large sequential reads, render writes.",
            RecommendedFileSize = 8L * 1024 * 1024 * 1024, // 8 GB
            Workloads =
            [
                new ProfileWorkload
                {
                    Name = "Timeline Preview",
                    Description = "Reading video frames for preview",
                    Weight = 40,
                    BlockSize = 4 * 1024 * 1024, // 4 MB frames
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 0,
                    QueueDepth = 4,
                    Threads = 2,
                    NoBuffering = true
                },
                new ProfileWorkload
                {
                    Name = "Render Output",
                    Description = "Writing rendered frames",
                    Weight = 35,
                    BlockSize = 4 * 1024 * 1024, // 4 MB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 100,
                    QueueDepth = 4,
                    Threads = 2,
                    NoBuffering = true
                },
                new ProfileWorkload
                {
                    Name = "Scratch/Preview",
                    Description = "Reading/writing preview files",
                    Weight = 15,
                    BlockSize = 256 * 1024, // 256 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 50,
                    QueueDepth = 8,
                    Threads = 2,
                    NoBuffering = false
                },
                new ProfileWorkload
                {
                    Name = "Project Files",
                    Description = "Loading/saving project metadata",
                    Weight = 10,
                    BlockSize = 64 * 1024, // 64 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 40,
                    QueueDepth = 2,
                    Threads = 1,
                    NoBuffering = false
                }
            ]
        };
    }

    private static UsageProfile CreateOperatingSystemProfile()
    {
        return new UsageProfile
        {
            Type = UsageProfileType.OperatingSystem,
            Name = "Operating System",
            Description = "Simulates OS disk access: boot, app loading, system operations.",
            RecommendedFileSize = 2L * 1024 * 1024 * 1024, // 2 GB
            Workloads =
            [
                new ProfileWorkload
                {
                    Name = "App Loading",
                    Description = "Loading application binaries",
                    Weight = 35,
                    BlockSize = 64 * 1024, // 64 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 8,
                    Threads = 4,
                    NoBuffering = false
                },
                new ProfileWorkload
                {
                    Name = "Page File",
                    Description = "Memory paging operations",
                    Weight = 25,
                    BlockSize = 4 * 1024, // 4 KB pages
                    Pattern = AccessPattern.Random,
                    WritePercent = 50,
                    QueueDepth = 32,
                    Threads = 4,
                    NoBuffering = true
                },
                new ProfileWorkload
                {
                    Name = "Prefetch",
                    Description = "Prefetch and superfetch reads",
                    Weight = 20,
                    BlockSize = 256 * 1024, // 256 KB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 0,
                    QueueDepth = 8,
                    Threads = 2,
                    NoBuffering = false
                },
                new ProfileWorkload
                {
                    Name = "System Writes",
                    Description = "Event logs, registry updates",
                    Weight = 20,
                    BlockSize = 4 * 1024, // 4 KB
                    Pattern = AccessPattern.Random,
                    WritePercent = 100,
                    QueueDepth = 4,
                    Threads = 2,
                    NoBuffering = false
                }
            ]
        };
    }

    private static UsageProfile CreateBackupProfile()
    {
        return new UsageProfile
        {
            Type = UsageProfileType.Backup,
            Name = "Backup",
            Description = "Simulates backup operations: large sequential writes with verification reads.",
            RecommendedFileSize = 8L * 1024 * 1024 * 1024, // 8 GB
            Workloads =
            [
                new ProfileWorkload
                {
                    Name = "Source Read",
                    Description = "Reading source data for backup",
                    Weight = 45,
                    BlockSize = 1024 * 1024, // 1 MB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 0,
                    QueueDepth = 8,
                    Threads = 4,
                    NoBuffering = true
                },
                new ProfileWorkload
                {
                    Name = "Backup Write",
                    Description = "Writing backup data",
                    Weight = 45,
                    BlockSize = 1024 * 1024, // 1 MB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 100,
                    QueueDepth = 8,
                    Threads = 4,
                    NoBuffering = true
                },
                new ProfileWorkload
                {
                    Name = "Verify",
                    Description = "Verification reads",
                    Weight = 10,
                    BlockSize = 1024 * 1024, // 1 MB
                    Pattern = AccessPattern.Sequential,
                    WritePercent = 0,
                    QueueDepth = 4,
                    Threads = 2,
                    NoBuffering = true
                }
            ]
        };
    }
}
