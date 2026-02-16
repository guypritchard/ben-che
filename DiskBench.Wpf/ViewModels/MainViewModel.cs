using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskBench.Core;
using DiskBench.Win32;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace DiskBench.Wpf;

public partial class MainViewModel : ObservableObject, IBenchmarkSink
{
    private CancellationTokenSource? _cts;
    private readonly WindowsIoEngine _engine;
    private const int MaxSpeedSamples = 200;
    private double _chartWindowSeconds = 45;
    private readonly ObservableCollection<ObservablePoint> _speedSamples = new();
    private readonly ObservableCollection<ObservablePoint> _limitSamples = new();
    private readonly ISeries[] _speedSeries;
    private readonly Axis[] _speedXAxes;
    private readonly Axis[] _speedYAxes;
    private double _lastSampleSecond = -1;

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

        _speedSeries = CreateSpeedSeries();
        _speedXAxes = CreateSpeedXAxes();
        _speedYAxes = CreateSpeedYAxes();
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
    private bool _bypassOsCache = true;

    [ObservableProperty]
    private bool _isRunning;

    // Progress tracking fields
    private int _totalWorkloads;
    private int _currentWorkloadIndex;
    private int _totalTrials;
    private int _currentTrialIndex;
    private readonly TimeSpan _progressUpdateInterval = TimeSpan.FromMilliseconds(100);
    private DateTime _lastProgressUpdateUtc = DateTime.MinValue;

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
    private bool _isWarmup;

    [ObservableProperty]
    private bool _isPhaseActive;

    [ObservableProperty]
    private bool _isFinalizing;

    [ObservableProperty]
    private ObservableCollection<WorkloadResultViewModel> _results;

    public IReadOnlyList<ISeries> SpeedSeries => _speedSeries;
    public IReadOnlyList<Axis> SpeedXAxes => _speedXAxes;
    public IReadOnlyList<Axis> SpeedYAxes => _speedYAxes;

    [ObservableProperty]
    private double _theoreticalLimitMBps;

    [ObservableProperty]
    private string _theoreticalLimitValue = "Unknown";

    [ObservableProperty]
    private string _theoreticalLimitDetail = "Connection limit unavailable";

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
    public bool ShowLiveValues => IsPhaseActive && !IsWarmup && !IsFinalizing;
    public bool ShowWarmup => IsPhaseActive && IsWarmup;
    public bool ShowFinalizing => IsPhaseActive && IsFinalizing;
    public bool ShowPendingValues => IsPhaseActive && (IsWarmup || IsFinalizing);
    public bool ShowIdle => !IsPhaseActive;
    public bool HasTheoreticalLimit => TheoreticalLimitMBps > 0;

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
            _chartWindowSeconds = Math.Max(5, duration);
            var trials = int.Parse(SelectedTrials);
            
            var filePath = BenchmarkPathHelper.BuildTestFilePath(SelectedDrive ?? string.Empty, SelectedProfile);

            var plan = UsageProfiles.CreatePlan(
                SelectedProfile,
                filePath,
                fileSize,
                trials,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(duration));

            if (BypassOsCache)
            {
                plan = ApplyCacheBypass(plan);
            }

            var runner = new BenchmarkRunner(_engine, this);
            var result = await runner.RunAsync(plan, _cts.Token);

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
            IsWarmup = false;
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
            IsWarmup = false;
            IsPhaseActive = false;
            IsFinalizing = false;
            ResetSpeedChart();
        });
        _lastProgressUpdateUtc = DateTime.MinValue;
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
        _lastProgressUpdateUtc = DateTime.MinValue;
        Application.Current.Dispatcher.Invoke(() =>
        {
            _currentTrialIndex = trialNumber - 1; // trialNumber is 1-based
            _totalTrials = totalTrials;
            CurrentPhase = $"Trial {trialNumber}/{totalTrials}";
            IsWarmup = false;
            IsFinalizing = false;
            IsPhaseActive = true;
            Progress = 0;
            ProgressText = "0%";
            ResetSpeedChart();
        });
    }

    public void OnTrialProgress(WorkloadSpec workload, int trialNumber, TrialProgress progress)
    {
        var now = DateTime.UtcNow;
        if (!progress.IsFinalizing && progress.PercentComplete < 100)
        {
            if ((now - _lastProgressUpdateUtc) < _progressUpdateInterval)
                return;
        }
        _lastProgressUpdateUtc = now;

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var phase = progress.IsWarmup ? "Warmup" : "Measuring";

            if (progress.IsFinalizing)
            {
                CurrentPhase = "Finalizing...";
                Progress = 0;
                ProgressText = "";
            }
            else
            {
                CurrentPhase = $"{phase} - {progress.Elapsed.TotalSeconds:F0}s / {progress.Duration.TotalSeconds:F0}s";
                Progress = progress.PercentComplete;
                ProgressText = $"{Progress:F0}%";
            }

            CurrentSpeed = FormatSpeed(progress.CurrentBytesPerSecond);
            CurrentIops = FormatIops(progress.CurrentIops);
            IsWarmup = progress.IsWarmup;
            IsFinalizing = progress.IsFinalizing;
            IsPhaseActive = true;

            if (!progress.IsWarmup && !progress.IsFinalizing)
            {
                var mbps = progress.CurrentBytesPerSecond / (1024.0 * 1024.0);
                AppendSpeedSample(progress.Elapsed.TotalSeconds, mbps);
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    public void OnTrialComplete(WorkloadSpec workload, int trialNumber, TrialResult result)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            CurrentPhase = "Trial complete";
            IsWarmup = false;
            IsPhaseActive = false;
            IsFinalizing = false;
            Progress = 0;
            ProgressText = "";
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
            IsWarmup = false;
            IsPhaseActive = false;
            IsFinalizing = false;
            Progress = 0;
            ProgressText = "";
        });
    }

    partial void OnIsWarmupChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLiveValues));
        OnPropertyChanged(nameof(ShowWarmup));
        OnPropertyChanged(nameof(ShowPendingValues));
    }

    partial void OnIsPhaseActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLiveValues));
        OnPropertyChanged(nameof(ShowWarmup));
        OnPropertyChanged(nameof(ShowFinalizing));
        OnPropertyChanged(nameof(ShowPendingValues));
        OnPropertyChanged(nameof(ShowIdle));
    }

    partial void OnIsFinalizingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLiveValues));
        OnPropertyChanged(nameof(ShowFinalizing));
        OnPropertyChanged(nameof(ShowPendingValues));
    }

    partial void OnTheoreticalLimitMBpsChanged(double value)
    {
        OnPropertyChanged(nameof(HasTheoreticalLimit));
        RefreshLimitSeries();
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
            if (drive.IsReady &&
                (drive.DriveType == System.IO.DriveType.Fixed ||
                 drive.DriveType == System.IO.DriveType.Removable))
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
            UpdateTheoreticalLimit(null);
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
            UpdateTheoreticalLimit(details);
        }
        catch
        {
            DriveInfo = null;
            UpdateTheoreticalLimit(null);
        }
        
        OnPropertyChanged(nameof(HasDriveInfo));
    }

    private static BenchmarkPlan ApplyCacheBypass(BenchmarkPlan plan)
    {
        var workloads = plan.Workloads.Select(workload => new WorkloadSpec
        {
            Name = workload.Name,
            FilePath = workload.FilePath,
            FileSize = workload.FileSize,
            BlockSize = workload.BlockSize,
            Pattern = workload.Pattern,
            WritePercent = workload.WritePercent,
            QueueDepth = workload.QueueDepth,
            Threads = workload.Threads,
            Region = workload.Region,
            FlushPolicy = workload.FlushPolicy,
            FlushInterval = workload.FlushInterval,
            NoBuffering = true,
            WriteThrough = true
        }).ToList();

        return new BenchmarkPlan
        {
            Name = plan.Name,
            Workloads = workloads,
            Trials = plan.Trials,
            WarmupDuration = plan.WarmupDuration,
            MeasuredDuration = plan.MeasuredDuration,
            Seed = plan.Seed,
            ComputeConfidenceIntervals = plan.ComputeConfidenceIntervals,
            BootstrapIterations = plan.BootstrapIterations,
            CollectTimeSeries = plan.CollectTimeSeries,
            ReuseExistingFiles = plan.ReuseExistingFiles,
            DeleteOnComplete = plan.DeleteOnComplete,
            TrackAllocations = plan.TrackAllocations
        };
    }

    private ISeries[] CreateSpeedSeries()
    {
        var speedStroke = new SolidColorPaint(new SKColor(88, 166, 255), 2);
        var speedFill = new SolidColorPaint(new SKColor(88, 166, 255, 40));
        var limitStroke = new SolidColorPaint(new SKColor(163, 113, 247), 1);

        return
        [
            new LineSeries<ObservablePoint>
            {
                Values = _speedSamples,
                Stroke = speedStroke,
                Fill = speedFill,
                GeometrySize = 0,
                LineSmoothness = 0.2,
                Name = "Speed"
            },
            new LineSeries<ObservablePoint>
            {
                Values = _limitSamples,
                Stroke = limitStroke,
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
                Name = "Limit"
            }
        ];
    }

    private Axis[] CreateSpeedXAxes()
    {
        var labelPaint = new SolidColorPaint(new SKColor(139, 148, 158));
        var separatorPaint = new SolidColorPaint(new SKColor(48, 54, 61, 140));

        return
        [
            new Axis
            {
                Labeler = value => $"{value:0}s",
                TextSize = 10,
                LabelsPaint = labelPaint,
                SeparatorsPaint = separatorPaint,
                MinLimit = 0,
                MinStep = 5
            }
        ];
    }

    private Axis[] CreateSpeedYAxes()
    {
        var labelPaint = new SolidColorPaint(new SKColor(139, 148, 158));
        var separatorPaint = new SolidColorPaint(new SKColor(48, 54, 61, 140));

        return
        [
            new Axis
            {
                Labeler = value => $"{value:0} MB/s",
                TextSize = 10,
                LabelsPaint = labelPaint,
                SeparatorsPaint = separatorPaint,
                MinLimit = 0
            }
        ];
    }

    private void ResetSpeedChart()
    {
        _speedSamples.Clear();
        _limitSamples.Clear();
        _lastSampleSecond = -1;
        if (SpeedXAxes.Count > 0)
        {
            var window = Math.Max(5, _chartWindowSeconds);
            SpeedXAxes[0].MinLimit = 0;
            SpeedXAxes[0].MaxLimit = window;
        }
    }

    private void AppendSpeedSample(double elapsedSeconds, double mbps)
    {
        if (elapsedSeconds <= _lastSampleSecond)
            return;

        _lastSampleSecond = elapsedSeconds;
        _speedSamples.Add(new ObservablePoint(elapsedSeconds, mbps));

        if (TheoreticalLimitMBps > 0)
        {
            _limitSamples.Add(new ObservablePoint(elapsedSeconds, TheoreticalLimitMBps));
        }

        while (_speedSamples.Count > MaxSpeedSamples)
        {
            _speedSamples.RemoveAt(0);
            if (_limitSamples.Count > 0)
            {
                _limitSamples.RemoveAt(0);
            }
        }

        if (SpeedXAxes.Count > 0)
        {
            var window = Math.Max(5, _chartWindowSeconds);
            var min = Math.Max(0, elapsedSeconds - window);
            SpeedXAxes[0].MinLimit = min;
            SpeedXAxes[0].MaxLimit = Math.Max(window, elapsedSeconds);
        }
    }

    private void RefreshLimitSeries()
    {
        _limitSamples.Clear();
        if (TheoreticalLimitMBps <= 0)
        {
            return;
        }

        foreach (var point in _speedSamples)
        {
            _limitSamples.Add(new ObservablePoint(point.X, TheoreticalLimitMBps));
        }
    }

    private void UpdateTheoreticalLimit(DriveDetails? details)
    {
        var estimate = EstimateTheoreticalLimit(details);
        if (estimate.LimitMBps is null || estimate.LimitMBps <= 0)
        {
            TheoreticalLimitMBps = 0;
            TheoreticalLimitValue = "Unknown";
            TheoreticalLimitDetail = estimate.Detail;
            return;
        }

        TheoreticalLimitMBps = estimate.LimitMBps.Value;
        TheoreticalLimitValue = FormatSpeed(TheoreticalLimitMBps * 1024 * 1024);
        TheoreticalLimitDetail = estimate.Detail;
    }

    private static (double? LimitMBps, string Detail) EstimateTheoreticalLimit(DriveDetails? details)
    {
        if (details == null)
        {
            return (null, "Connection limit unavailable");
        }

        return details.BusType switch
        {
            StorageBusType.NVMe => EstimateNvmeLimit(details),
            StorageBusType.Sata => EstimateSataLimit(details),
            StorageBusType.Sas => EstimateSasLimit(details),
            StorageBusType.Usb => EstimateUsbLimit(details),
            StorageBusType.Sd or StorageBusType.Mmc => (90, "SD/UHS-I (est.)"),
            StorageBusType.Ufs => (1500, "UFS (est.)"),
            StorageBusType.Scsi => (320, "SCSI Ultra320"),
            StorageBusType.iScsi => (125, "iSCSI (1 GbE est.)"),
            StorageBusType.Raid or StorageBusType.StorageSpaces or StorageBusType.Virtual or StorageBusType.FileBackedVirtual => (null, "Array/virtualized (limit varies)"),
            _ => (null, GetBusVersionDetail(details, $"{details.BusTypeDescription} (limit unknown)"))
        };
    }

    private static (double? LimitMBps, string Detail) EstimateNvmeLimit(DriveDetails details)
    {
        var gen = GetPcieGeneration(details);
        if (gen.HasValue)
        {
            return gen.Value switch
            {
                3 => (3940, "PCIe Gen3 x4 (adapter reported)"),
                4 => (7880, "PCIe Gen4 x4 (adapter reported)"),
                5 => (15750, "PCIe Gen5 x4 (adapter reported)"),
                _ => (7000, $"PCIe Gen{gen.Value} x4 (adapter reported)")
            };
        }

        return (7000, GetBusVersionDetail(details, "PCIe Gen4 x4 (est.)"));
    }

    private static (double? LimitMBps, string Detail) EstimateSataLimit(DriveDetails details)
    {
        var major = details.BusMajorVersion;
        return major switch
        {
            3 => (550, "SATA III 6 Gb/s (adapter reported)"),
            2 => (300, "SATA II 3 Gb/s (adapter reported)"),
            1 => (150, "SATA I 1.5 Gb/s (adapter reported)"),
            _ => (550, "SATA III 6 Gb/s (est.)")
        };
    }

    private static (double? LimitMBps, string Detail) EstimateSasLimit(DriveDetails details)
    {
        var major = details.BusMajorVersion;
        return major switch
        {
            3 => (1200, "SAS 12 Gb/s (adapter reported)"),
            2 => (600, "SAS 6 Gb/s (adapter reported)"),
            1 => (300, "SAS 3 Gb/s (adapter reported)"),
            _ => (1200, "SAS 12 Gb/s (est.)")
        };
    }

    private static (double? LimitMBps, string Detail) EstimateUsbLimit(DriveDetails details)
    {
        var major = details.BusMajorVersion;
        var minor = details.BusMinorVersion ?? 0;

        if (major is >= 3)
        {
            if (minor >= 2)
            {
                return (1000, "USB 3.2 10 Gb/s (adapter reported)");
            }

            if (minor >= 1)
            {
                return (1000, "USB 3.1 10 Gb/s (adapter reported)");
            }

            return (450, "USB 3.0 5 Gb/s (adapter reported)");
        }

        if (major == 2)
        {
            return (40, "USB 2.0 Hi-Speed (adapter reported)");
        }

        var hint = $"{details.VendorId} {details.ProductId}";
        if (hint.Contains("USB2", StringComparison.OrdinalIgnoreCase) ||
            hint.Contains("USB 2", StringComparison.OrdinalIgnoreCase))
        {
            return (40, "USB 2.0 Hi-Speed (est.)");
        }

        if (hint.Contains("USB3", StringComparison.OrdinalIgnoreCase) ||
            hint.Contains("USB 3", StringComparison.OrdinalIgnoreCase) ||
            hint.Contains("USB 3.0", StringComparison.OrdinalIgnoreCase) ||
            hint.Contains("USB 3.1", StringComparison.OrdinalIgnoreCase) ||
            hint.Contains("USB 3.2", StringComparison.OrdinalIgnoreCase))
        {
            return (450, "USB 3.x SuperSpeed (est.)");
        }

        return (450, "USB 3.x SuperSpeed (est.)");
    }

    private static int? GetPcieGeneration(DriveDetails details)
    {
        var major = details.BusMajorVersion;
        if (major is null || major <= 0)
        {
            return null;
        }

        return major is >= 3 and <= 6 ? major : null;
    }

    private static string GetBusVersionDetail(DriveDetails details, string fallback)
    {
        if (details.BusMajorVersion is null)
        {
            return fallback;
        }

        var minor = details.BusMinorVersion ?? 0;
        return $"{fallback} (bus v{details.BusMajorVersion}.{minor})";
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

internal static class BenchmarkPathHelper
{
    public static string BuildTestFilePath(string selectedDrive, UsageProfile profile)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"diskbench_{profile.Type}_{timestamp}.dat";

        var driveRoot = GetDriveRoot(selectedDrive);
        var windowsRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? string.Empty;
        var tempRoot = Path.GetPathRoot(Path.GetTempPath()) ?? string.Empty;

        string baseDirectory;
        if (string.Equals(driveRoot, windowsRoot, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(driveRoot, tempRoot, StringComparison.OrdinalIgnoreCase))
        {
            baseDirectory = Path.Combine(Path.GetTempPath(), "DiskBench", $"run_{timestamp}");
        }
        else
        {
            baseDirectory = Path.Combine(driveRoot, "DiskBench", $"run_{timestamp}");
        }

        return Path.Combine(baseDirectory, fileName);
    }

    private static string GetDriveRoot(string? selectedDrive)
    {
        if (string.IsNullOrWhiteSpace(selectedDrive))
        {
            return Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        }

        var trimmed = selectedDrive.Trim();
        var spaceIndex = trimmed.AsSpan().IndexOf(' ');
        if (spaceIndex > 0)
        {
            trimmed = trimmed[..spaceIndex];
        }

        if (trimmed.Length == 2 && trimmed[1] == ':')
        {
            trimmed += "\\";
        }

        if (trimmed.Length >= 3 && trimmed[1] == ':')
        {
            return trimmed[..3];
        }

        return Path.GetPathRoot(trimmed) ?? trimmed;
    }
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
        else if (p99 >= 100)
        {
            LatencyValue = p99.ToString("F0");
            LatencyUnit = "\u00B5s";
        }
        else if (p99 >= 10)
        {
            LatencyValue = p99.ToString("F1");
            LatencyUnit = "\u00B5s";
        }
        else if (p99 >= 1)
        {
            LatencyValue = p99.ToString("F2");
            LatencyUnit = "\u00B5s";
        }
        else
        {
            LatencyValue = "<1";
            LatencyUnit = "\u00B5s";
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
        
        // Bar percent (normalized to max ~7000 MB/s for NVMe)
        var maxMbps = 7000.0;
        BarPercent = Math.Min(mbps / maxMbps * 100.0, 100.0);
        
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
    public double BarPercent { get; }
    
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
            >= 100 => $"{us:F0}\u00B5s",
            >= 10 => $"{us:F1}\u00B5s",
            _ => $"{us:F2}\u00B5s"
        };
    }
}
