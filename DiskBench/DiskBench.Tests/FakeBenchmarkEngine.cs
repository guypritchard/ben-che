using System.Diagnostics;
using DiskBench.Core;
using DiskBench.Metrics;
using CoreTimeSeriesSample = DiskBench.Core.TimeSeriesSample;

namespace DiskBench.Tests;

/// <summary>
/// A fake benchmark engine for testing purposes.
/// Simulates IO completions with deterministic latencies without touching disk.
/// </summary>
public sealed class FakeBenchmarkEngine : IBenchmarkEngine
{
    private readonly FakeEngineOptions _options;

    /// <summary>
    /// Creates a fake engine with default options.
    /// </summary>
    public FakeBenchmarkEngine() : this(new FakeEngineOptions())
    {
    }

    /// <summary>
    /// Creates a fake engine with specified options.
    /// </summary>
    public FakeBenchmarkEngine(FakeEngineOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task<PrepareResult> PrepareAsync(
        PrepareSpec spec,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(1.0);

        return Task.FromResult(new PrepareResult
        {
            FilePath = spec.FilePath,
            FileSize = spec.FileSize,
            PhysicalSectorSize = _options.SectorSize,
            LogicalSectorSize = _options.SectorSize,
            WasReused = true,
            UsedSetValidData = false
        });
    }

    /// <inheritdoc />
    public async Task<TrialResult> RunTrialAsync(
        TrialSpec spec,
        IProgress<TrialProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var workload = spec.Workload;
        var random = new Random(spec.Seed);

        // Simulate warmup
        if (spec.WarmupDuration > TimeSpan.Zero)
        {
            await SimulatePhaseAsync(spec, isWarmup: true, progress, random, cancellationToken)
                .ConfigureAwait(false);
        }

        // Simulate measured phase
        return await SimulatePhaseAsync(spec, isWarmup: false, progress, random, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TrialResult> SimulatePhaseAsync(
        TrialSpec spec,
        bool isWarmup,
        IProgress<TrialProgress>? progress,
        Random random,
        CancellationToken cancellationToken)
    {
        var workload = spec.Workload;
        var duration = isWarmup ? spec.WarmupDuration : spec.MeasuredDuration;
        var ticksPerUs = LatencyHistogram.TicksPerMicrosecond;

        var metrics = new TrialMetricsCollector((int)duration.TotalSeconds + 5, spec.CollectTimeSeries);

        var startTime = Stopwatch.GetTimestamp();
        var endTime = startTime + (long)(duration.TotalSeconds * Stopwatch.Frequency);
        var lastProgressReport = startTime;
        var progressInterval = Stopwatch.Frequency / 4; // 4Hz

        // Calculate simulated IOPS based on options
        double targetIops = CalculateTargetIops(workload);
        double nsPerIo = 1_000_000_000.0 / targetIops;

        long simulatedOps = 0;
        long simulatedBytes = 0;

        while (Stopwatch.GetTimestamp() < endTime && !cancellationToken.IsCancellationRequested)
        {
            // Simulate a batch of IOs
            int batchSize = workload.QueueDepth;
            
            for (int i = 0; i < batchSize; i++)
            {
                // Generate latency with some variance
                double baseLatencyUs = _options.BaseLatencyUs;
                if (_options.LatencyVariancePercent > 0)
                {
                    double variance = baseLatencyUs * _options.LatencyVariancePercent / 100.0;
                    baseLatencyUs += (random.NextDouble() * 2 - 1) * variance;
                }

                long latencyTicks = (long)(baseLatencyUs * ticksPerUs);
                bool isWrite = random.Next(100) < workload.WritePercent;

                if (!isWarmup)
                {
                    metrics.RecordCompletion(Stopwatch.GetTimestamp(), latencyTicks, workload.BlockSize, isWrite);
                }

                simulatedOps++;
                simulatedBytes += workload.BlockSize;
            }

            // Report progress periodically
            var now = Stopwatch.GetTimestamp();
            if (progress != null && now - lastProgressReport >= progressInterval)
            {
                lastProgressReport = now;
                var elapsed = TimeSpan.FromSeconds((double)(now - startTime) / Stopwatch.Frequency);
                var elapsedSeconds = elapsed.TotalSeconds;

                progress.Report(new TrialProgress
                {
                    IsWarmup = isWarmup,
                    Elapsed = elapsed,
                    Duration = duration,
                    CurrentBytesPerSecond = elapsedSeconds > 0 ? simulatedBytes / elapsedSeconds : 0,
                    CurrentIops = elapsedSeconds > 0 ? simulatedOps / elapsedSeconds : 0,
                    TotalBytes = simulatedBytes,
                    TotalOperations = simulatedOps
                });
            }

            // Small delay to avoid spinning too fast
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }

        if (isWarmup)
        {
            return null!; // Warmup result not used
        }

        metrics.Flush();
        var actualDuration = TimeSpan.FromSeconds((double)(Stopwatch.GetTimestamp() - startTime) / Stopwatch.Frequency);

        // Build time series
        List<CoreTimeSeriesSample>? timeSeries = null;
        if (spec.CollectTimeSeries && metrics.TimeSeries != null)
        {
            var snapshot = metrics.TimeSeries.CreateSnapshot();
            timeSeries = [];
            foreach (var sample in snapshot.Samples)
            {
                timeSeries.Add(new CoreTimeSeriesSample
                {
                    SecondOffset = sample.SecondOffset,
                    Bytes = sample.Bytes,
                    Operations = sample.Operations
                });
            }
        }

        return new TrialResult
        {
            TrialNumber = spec.TrialNumber,
            TotalBytes = metrics.TotalBytes,
            TotalOperations = metrics.TotalOperations,
            ReadOperations = metrics.ReadOperations,
            WriteOperations = metrics.WriteOperations,
            Duration = actualDuration,
            Latency = LatencyPercentiles.FromHistogram(metrics.Histogram, LatencyHistogram.TicksPerMicrosecond)
        };
    }

    private double CalculateTargetIops(WorkloadSpec workload)
    {
        // Base IOPS varies by access pattern and block size
        double baseIops = _options.BaseIops;

        // Sequential is faster than random
        if (workload.Pattern == AccessPattern.Sequential)
        {
            baseIops *= 2.0;
        }

        // Larger blocks mean fewer IOPS but more throughput
        if (workload.BlockSize > 4096)
        {
            baseIops *= 4096.0 / workload.BlockSize;
        }

        // Higher QD improves IOPS
        baseIops *= Math.Min(workload.QueueDepth, 32);

        return baseIops;
    }

    /// <inheritdoc />
    public int GetSectorSize(string filePath) => _options.SectorSize;

    /// <inheritdoc />
    public DriveDetails? GetDriveDetails(string drivePath) => new DriveDetails
    {
        DriveLetter = "C:",
        VolumeLabel = "Test Volume",
        TotalSize = 1_000_000_000_000,
        FreeSpace = 500_000_000_000,
        FileSystem = "NTFS",
        BusType = StorageBusType.NVMe,
        LogicalSectorSize = _options.SectorSize,
        PhysicalSectorSize = _options.SectorSize,
        ProductId = "Fake NVMe SSD",
        IsRemovable = false,
        SupportsCommandQueuing = true
    };

    /// <inheritdoc />
    public IReadOnlyList<DriveDetails> GetAllDriveDetails() => [GetDriveDetails("C:")!];

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Options for the fake benchmark engine.
/// </summary>
public sealed class FakeEngineOptions
{
    /// <summary>
    /// Simulated sector size.
    /// </summary>
    public int SectorSize { get; init; } = 512;

    /// <summary>
    /// Base latency in microseconds.
    /// </summary>
    public double BaseLatencyUs { get; init; } = 50.0;

    /// <summary>
    /// Latency variance percentage (0-100).
    /// </summary>
    public double LatencyVariancePercent { get; init; } = 20.0;

    /// <summary>
    /// Base IOPS (for 4K random QD1).
    /// </summary>
    public double BaseIops { get; init; } = 10000;
}
