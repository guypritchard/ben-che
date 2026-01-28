using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskBench.Core;
using DiskBench.Win32;

namespace DiskBench.Wpf;

public partial class MainViewModel : ObservableObject, IBenchmarkSink
{
    private CancellationTokenSource? _cts;
    private readonly WindowsIoEngine _engine;

    public MainViewModel()
    {
        _engine = new WindowsIoEngine();
        
        // Initialize collections
        Profiles = new ObservableCollection<UsageProfile>(UsageProfiles.All);
        SelectedProfile = Profiles.FirstOrDefault(p => p.Type == UsageProfileType.Gaming);
        
        RefreshDrives();
        
        FileSizes = ["1 GB", "2 GB", "4 GB", "8 GB", "16 GB", "32 GB", "64 GB"];
        SelectedFileSize = "8 GB";
        
        Durations = ["10 sec", "30 sec", "60 sec", "120 sec"];
        SelectedDuration = "30 sec";
        
        Trials = ["1", "2", "3", "5"];
        SelectedTrials = "3";
        
        Results = new ObservableCollection<WorkloadResultViewModel>();
    }

    #region Properties

    [ObservableProperty]
    private ObservableCollection<UsageProfile> _profiles;

    [ObservableProperty]
    private UsageProfile? _selectedProfile;

    [ObservableProperty]
    private ObservableCollection<string> _drives = new();

    [ObservableProperty]
    private string? _selectedDrive;

    [ObservableProperty]
    private DriveInfoViewModel? _driveInfo;

    [ObservableProperty]
    private ObservableCollection<string> _fileSizes;

    [ObservableProperty]
    private string _selectedFileSize;

    [ObservableProperty]
    private ObservableCollection<string> _durations;

    [ObservableProperty]
    private string _selectedDuration;

    [ObservableProperty]
    private ObservableCollection<string> _trials;

    [ObservableProperty]
    private string _selectedTrials;

    [ObservableProperty]
    private bool _isRunning;

    // Progress tracking fields
    private int _totalWorkloads;
    private int _currentWorkloadIndex;
    private int _totalTrials;
    private int _currentTrialIndex;

    [ObservableProperty]
    private string _currentWorkloadName = "";

    [ObservableProperty]
    private string _currentPhase = "";

    [ObservableProperty]
    private string _currentSpeed = "0 MB/s";

    [ObservableProperty]
    private string _currentIops = "0";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = "0%";

    [ObservableProperty]
    private ObservableCollection<WorkloadResultViewModel> _results;

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private bool _isQuickMode;

    [ObservableProperty]
    private string _quickModeDrive = "";

    public bool IsNotRunning => !IsRunning;
    public bool HasDriveInfo => DriveInfo != null;
    public bool HasResults => Results.Count > 0;
    public bool ShowEmptyState => !IsRunning && Results.Count == 0;
    public bool ShowConfigPanel => !IsQuickMode;

    #endregion

    #region Commands

    [RelayCommand]
    private async Task StartBenchmarkAsync()
    {
        if (SelectedProfile == null || string.IsNullOrEmpty(SelectedDrive))
            return;

        IsRunning = true;
        OnPropertyChanged(nameof(IsNotRunning));
        OnPropertyChanged(nameof(ShowEmptyState));
        
        Results.Clear();
        _cts = new CancellationTokenSource();

        try
        {
            var fileSize = ParseFileSize(SelectedFileSize);
            var duration = ParseDuration(SelectedDuration);
            var trials = int.Parse(SelectedTrials);
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filePath = Path.Combine(SelectedDrive, $"diskbench_{SelectedProfile.Type}_{timestamp}.dat");

            var plan = UsageProfiles.CreatePlan(
                SelectedProfile,
                filePath,
                fileSize,
                trials,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(duration));

            var runner = new BenchmarkRunner(_engine, this);
            var result = await runner.RunAsync(plan, _cts.Token);

            // Populate results
            foreach (var workloadResult in result.Workloads)
            {
                Results.Add(new WorkloadResultViewModel(workloadResult));
            }
            
            OnPropertyChanged(nameof(HasResults));
        }
        catch (OperationCanceledException)
        {
            // Cancelled by user
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Benchmark failed: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsRunning = false;
            OnPropertyChanged(nameof(IsNotRunning));
            OnPropertyChanged(nameof(ShowEmptyState));
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void StopBenchmark()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void Export()
    {
        // TODO: Export results to JSON/CSV
        MessageBox.Show("Export functionality coming soon!", "Export", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region IBenchmarkSink Implementation

    public void OnBenchmarkStart(BenchmarkPlan plan)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            CurrentPhase = "Starting...";
            _totalWorkloads = plan.Workloads.Count;
            _currentWorkloadIndex = 0;
            _totalTrials = 0;
            _currentTrialIndex = 0;
            Progress = 0;
            ProgressText = "0%";
        });
    }

    public void OnWorkloadStart(WorkloadSpec workload, int workloadIndex, int totalWorkloads)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _currentWorkloadIndex = workloadIndex;
            _totalWorkloads = totalWorkloads;
            CurrentWorkloadName = $"Workload {workloadIndex + 1}/{totalWorkloads}: {workload.Name ?? workload.GetDisplayName()}";
        });
    }

    public void OnTrialStart(WorkloadSpec workload, int trialNumber, int totalTrials)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _currentTrialIndex = trialNumber - 1; // trialNumber is 1-based
            _totalTrials = totalTrials;
            CurrentPhase = $"Trial {trialNumber}/{totalTrials}";
        });
    }

    public void OnTrialProgress(WorkloadSpec workload, int trialNumber, TrialProgress progress)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var phase = progress.IsWarmup ? "Warmup" : "Measuring";
            
            CurrentPhase = $"{phase} - {progress.Elapsed.TotalSeconds:F0}s / {progress.Duration.TotalSeconds:F0}s";
            CurrentSpeed = FormatSpeed(progress.CurrentBytesPerSecond);
            CurrentIops = FormatIops(progress.CurrentIops);
            
            // Calculate overall progress across all workloads and trials
            // Each workload has equal weight, each trial within a workload has equal weight
            var trialProgress = progress.Elapsed.TotalSeconds / progress.Duration.TotalSeconds;
            var trialWeight = 1.0 / Math.Max(1, _totalTrials);
            var workloadWeight = 1.0 / Math.Max(1, _totalWorkloads);
            
            var completedWorkloadsProgress = _currentWorkloadIndex * workloadWeight;
            var completedTrialsProgress = _currentTrialIndex * trialWeight * workloadWeight;
            var currentTrialProgress = trialProgress * trialWeight * workloadWeight;
            
            var overallProgress = (completedWorkloadsProgress + completedTrialsProgress + currentTrialProgress) * 100;
            
            Progress = Math.Min(overallProgress, 100);
            ProgressText = $"{Progress:F0}%";
        });
    }

    public void OnTrialComplete(WorkloadSpec workload, int trialNumber, TrialResult result)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            CurrentPhase = "Trial complete";
        });
    }

    public void OnWorkloadComplete(WorkloadSpec workload, WorkloadResult result)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Results.Add(new WorkloadResultViewModel(result));
            OnPropertyChanged(nameof(HasResults));
        });
    }

    public void OnBenchmarkComplete(BenchmarkResult result)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            CurrentPhase = "Complete!";
            Progress = 100;
            ProgressText = "100%";
        });
    }

    public void OnWarning(string message)
    {
        // Could show in UI, for now just ignore
    }

    public void OnError(string message, Exception? exception = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    #endregion

    #region Public Methods

    public void ParseCommandLineArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var argLower = arg.ToLowerInvariant();
            
            switch (argLower)
            {
                case "-p" or "--profile":
                    if (i + 1 < args.Length)
                    {
                        var profileName = args[++i].ToLowerInvariant();
                        SelectedProfile = Profiles.FirstOrDefault(p => 
                            p.Type.ToString().Equals(profileName, StringComparison.OrdinalIgnoreCase) ||
                            p.Name.Contains(profileName, StringComparison.OrdinalIgnoreCase));
                    }
                    break;
                    
                case "-d" or "--drive":
                    if (i + 1 < args.Length)
                    {
                        SetDriveFromArg(args[++i]);
                    }
                    break;
                    
                case "-s" or "--size":
                    if (i + 1 < args.Length)
                    {
                        var size = args[++i].ToUpperInvariant();
                        SelectedFileSize = FileSizes.FirstOrDefault(s => s.StartsWith(size)) ?? SelectedFileSize;
                    }
                    break;
                    
                case "--start" or "--auto":
                    AutoStart = true;
                    break;

                case "-q" or "--quick":
                    // Quick mode from context menu - use Gaming profile with quick settings
                    IsQuickMode = true;
                    AutoStart = true;
                    SelectedProfile = Profiles.FirstOrDefault(p => p.Type == UsageProfileType.Gaming);
                    SelectedFileSize = "1 GB";
                    SelectedDuration = "10 sec";
                    SelectedTrials = "1";
                    OnPropertyChanged(nameof(ShowConfigPanel));
                    break;

                default:
                    // Check if it's a drive path (e.g., "E:\" or "E:" or with extra junk from shell)
                    var cleanArg = arg.Trim().Trim('"', '\'');
                    if (cleanArg.Length >= 1 && char.IsLetter(cleanArg[0]))
                    {
                        // Could be a drive path
                        SetDriveFromArg(cleanArg);
                    }
                    break;
            }
        }
    }

    private void SetDriveFromArg(string drive)
    {
        // Clean up the drive path - context menus can pass malformed paths
        // Remove any quotes, extra colons, and normalize
        drive = drive.Trim().Trim('"', '\'');
        
        // Remove trailing junk characters that might be added by shell
        drive = drive.TrimEnd(':', '\\', '"', '\'', ' ');
        
        // Now we should have just the drive letter, or drive letter + colon
        if (string.IsNullOrEmpty(drive))
            return;
            
        // Get just the drive letter
        char driveLetter = char.ToUpperInvariant(drive[0]);
        if (!char.IsLetter(driveLetter))
            return;
            
        // Build clean path
        drive = $"{driveLetter}:\\";
        
        QuickModeDrive = drive;
        
        // Find matching drive in the list or add it
        var matchingDrive = Drives.FirstOrDefault(d => d.StartsWith(drive[..2], StringComparison.OrdinalIgnoreCase));
        if (matchingDrive != null)
        {
            SelectedDrive = matchingDrive;
        }
        else
        {
            // Add the drive to the list if not found
            Drives.Add(drive);
            SelectedDrive = drive;
        }
    }

    #endregion

    #region Private Methods

    private void RefreshDrives()
    {
        Drives.Clear();
        foreach (var drive in System.IO.DriveInfo.GetDrives())
        {
            if (drive.IsReady && drive.DriveType == System.IO.DriveType.Fixed)
            {
                Drives.Add($"{drive.Name} ({FormatSize(drive.TotalFreeSpace)} free)");
            }
        }
        SelectedDrive = Drives.FirstOrDefault();
        UpdateDriveInfo();
    }

    partial void OnSelectedDriveChanged(string? value)
    {
        UpdateDriveInfo();
    }

    private void UpdateDriveInfo()
    {
        if (string.IsNullOrEmpty(SelectedDrive))
        {
            DriveInfo = null;
            return;
        }

        try
        {
            var driveLetter = SelectedDrive[..3];
            var info = new DriveInfo(driveLetter);
            
            // Try to get detailed drive info including bus type
            var details = _engine.GetDriveDetails(driveLetter);
            
            var usedBytes = info.TotalSize - info.TotalFreeSpace;
            var usedPercent = info.TotalSize > 0 ? (double)usedBytes / info.TotalSize * 100 : 0;
            
            DriveInfo = new DriveInfoViewModel
            {
                FreeSpace = FormatSize(info.TotalFreeSpace),
                TotalSize = FormatSize(info.TotalSize),
                FileSystem = info.DriveFormat,
                DriveType = info.DriveType.ToString(),
                DriveName = !string.IsNullOrEmpty(info.VolumeLabel) ? info.VolumeLabel : "Local Disk",
                BusType = details?.BusTypeDescription ?? "Unknown",
                BusTypeIcon = details?.BusTypeIcon ?? "💿",
                UsedPercent = usedPercent,
                UsedText = $"{FormatSize(usedBytes)} used of {FormatSize(info.TotalSize)}"
            };
        }
        catch
        {
            DriveInfo = null;
        }
        
        OnPropertyChanged(nameof(HasDriveInfo));
    }

    private static long ParseFileSize(string size)
    {
        var parts = size.Split(' ');
        var value = long.Parse(parts[0]);
        return value * 1024 * 1024 * 1024; // GB to bytes
    }

    private static int ParseDuration(string duration)
    {
        var parts = duration.Split(' ');
        return int.Parse(parts[0]);
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024 * 1024):F1} TB",
            >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):F1} GB",
            >= 1024L * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / 1024.0:F1} KB"
        };
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        return bytesPerSecond switch
        {
            >= 1024 * 1024 * 1024 => $"{bytesPerSecond / (1024 * 1024 * 1024):F1} GB/s",
            >= 1024 * 1024 => $"{bytesPerSecond / (1024 * 1024):F1} MB/s",
            >= 1024 => $"{bytesPerSecond / 1024:F1} KB/s",
            _ => $"{bytesPerSecond:F0} B/s"
        };
    }

    private static string FormatIops(double iops)
    {
        return iops switch
        {
            >= 1000000 => $"{iops / 1000000:F1}M",
            >= 1000 => $"{iops / 1000:F1}K",
            _ => $"{iops:F0}"
        };
    }

    #endregion
}

public class DriveInfoViewModel
{
    public string FreeSpace { get; set; } = "";
    public string TotalSize { get; set; } = "";
    public string FileSystem { get; set; } = "";
    public string DriveType { get; set; } = "";
    public string DriveName { get; set; } = "";
    public string BusType { get; set; } = "";
    public string BusTypeIcon { get; set; } = "💾";
    public double UsedPercent { get; set; }
    public string UsedText { get; set; } = "";
}

public class WorkloadResultViewModel : ObservableObject
{
    public WorkloadResultViewModel(WorkloadResult result)
    {
        Name = result.Workload.Name ?? result.Workload.GetDisplayName();
        BlockSizeText = FormatBlockSize(result.Workload.BlockSize);
        PatternText = result.Workload.Pattern == AccessPattern.Sequential ? "Sequential" : "Random";
        QueueDepthText = $"QD{result.Workload.QueueDepth}";
        
        // Throughput
        var mbps = result.MeanBytesPerSecond / (1024 * 1024);
        if (mbps >= 1000)
        {
            ThroughputValue = (mbps / 1024).ToString("F2");
            ThroughputUnit = "GB/s";
        }
        else
        {
            ThroughputValue = mbps.ToString("F1");
            ThroughputUnit = "MB/s";
        }
        
        // IOPS
        if (result.MeanIops >= 1000000)
        {
            IopsValue = (result.MeanIops / 1000000).ToString("F2");
            IopsUnit = "M IOPS";
        }
        else if (result.MeanIops >= 1000)
        {
            IopsValue = (result.MeanIops / 1000).ToString("F1");
            IopsUnit = "K IOPS";
        }
        else
        {
            IopsValue = result.MeanIops.ToString("F0");
            IopsUnit = "IOPS";
        }
        
        // Latency
        var p99 = result.MeanLatency.P99Us;
        if (p99 >= 1000)
        {
            LatencyValue = (p99 / 1000).ToString("F1");
            LatencyUnit = "ms";
        }
        else
        {
            LatencyValue = p99.ToString("F0");
            LatencyUnit = "µs";
        }
        
        // Latency percentiles
        P50 = FormatLatency(result.MeanLatency.P50Us);
        P90 = FormatLatency(result.MeanLatency.P90Us);
        P99 = FormatLatency(result.MeanLatency.P99Us);
        P999 = FormatLatency(result.MeanLatency.P999Us);
        MaxLatency = FormatLatency(result.MeanLatency.MaxUs);
        
        // Colors based on operation type
        bool isWrite = result.Workload.WritePercent > 50;
        bool isMixed = result.Workload.WritePercent > 10 && result.Workload.WritePercent < 90;
        
        if (isMixed)
        {
            BarColorStart = Color.FromRgb(163, 113, 247); // Purple
            BarColorEnd = Color.FromRgb(219, 97, 162);    // Pink
            ThroughputColor = new SolidColorBrush(Color.FromRgb(163, 113, 247));
        }
        else if (isWrite)
        {
            BarColorStart = Color.FromRgb(248, 81, 73);   // Red
            BarColorEnd = Color.FromRgb(219, 97, 162);    // Pink
            ThroughputColor = new SolidColorBrush(Color.FromRgb(248, 81, 73));
        }
        else
        {
            BarColorStart = Color.FromRgb(88, 166, 255);  // Blue
            BarColorEnd = Color.FromRgb(57, 213, 255);    // Cyan
            ThroughputColor = new SolidColorBrush(Color.FromRgb(88, 166, 255));
        }
        
        // Bar width (normalized to max ~7000 MB/s for NVMe)
        var maxMbps = 7000.0;
        BarWidth = Math.Min(mbps / maxMbps * 600, 600);
        
        // Score badge
        if (mbps >= 3000)
        {
            ScoreLabel = "EXCELLENT";
            ScoreBackground = new SolidColorBrush(Color.FromArgb(40, 63, 185, 80));
            ScoreForeground = new SolidColorBrush(Color.FromRgb(63, 185, 80));
        }
        else if (mbps >= 1000)
        {
            ScoreLabel = "GREAT";
            ScoreBackground = new SolidColorBrush(Color.FromArgb(40, 88, 166, 255));
            ScoreForeground = new SolidColorBrush(Color.FromRgb(88, 166, 255));
        }
        else if (mbps >= 200)
        {
            ScoreLabel = "GOOD";
            ScoreBackground = new SolidColorBrush(Color.FromArgb(40, 210, 153, 34));
            ScoreForeground = new SolidColorBrush(Color.FromRgb(210, 153, 34));
        }
        else
        {
            ScoreLabel = "SLOW";
            ScoreBackground = new SolidColorBrush(Color.FromArgb(40, 248, 81, 73));
            ScoreForeground = new SolidColorBrush(Color.FromRgb(248, 81, 73));
        }
        
        // Latency color (green = good, red = bad)
        if (p99 < 100)
        {
            LatencyColor = new SolidColorBrush(Color.FromRgb(63, 185, 80));
        }
        else if (p99 < 1000)
        {
            LatencyColor = new SolidColorBrush(Color.FromRgb(210, 153, 34));
        }
        else
        {
            LatencyColor = new SolidColorBrush(Color.FromRgb(248, 81, 73));
        }
    }

    public string Name { get; }
    public string BlockSizeText { get; }
    public string PatternText { get; }
    public string QueueDepthText { get; }
    
    public string ThroughputValue { get; }
    public string ThroughputUnit { get; }
    public Brush ThroughputColor { get; }
    
    public string IopsValue { get; }
    public string IopsUnit { get; }
    
    public string LatencyValue { get; }
    public string LatencyUnit { get; }
    public Brush LatencyColor { get; }
    
    public string P50 { get; }
    public string P90 { get; }
    public string P99 { get; }
    public string P999 { get; }
    public string MaxLatency { get; }
    
    public Color BarColorStart { get; }
    public Color BarColorEnd { get; }
    public double BarWidth { get; }
    
    public string ScoreLabel { get; }
    public Brush ScoreBackground { get; }
    public Brush ScoreForeground { get; }

    private static string FormatBlockSize(int bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 => $"{bytes / (1024 * 1024)} MB",
            >= 1024 => $"{bytes / 1024} KB",
            _ => $"{bytes} B"
        };
    }

    private static string FormatLatency(double us)
    {
        return us switch
        {
            >= 1000 => $"{us / 1000:F1}ms",
            >= 100 => $"{us:F0}µs",
            >= 10 => $"{us:F1}µs",
            _ => $"{us:F2}µs"
        };
    }
}
