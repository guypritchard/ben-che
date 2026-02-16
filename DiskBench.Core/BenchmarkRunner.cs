namespace DiskBench.Core;

/// <summary>
/// Executes benchmark plans using a benchmark engine.
/// </summary>
public sealed class BenchmarkRunner
{
    private readonly IBenchmarkEngine _engine;
    private readonly IBenchmarkSink _sink;

    /// <summary>
    /// Creates a new benchmark runner.
    /// </summary>
    /// <param name="engine">The IO engine to use.</param>
    /// <param name="sink">Sink for benchmark events.</param>
    public BenchmarkRunner(IBenchmarkEngine engine, IBenchmarkSink? sink = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _sink = sink ?? NullBenchmarkSink.Instance;
    }

    /// <summary>
    /// Runs a complete benchmark plan.
    /// </summary>
    /// <param name="plan">The benchmark plan to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete benchmark result.</returns>
    public async Task<BenchmarkResult> RunAsync(BenchmarkPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var startTime = DateTimeOffset.UtcNow;
        _sink.OnBenchmarkStart(plan);

        ValidatePlan(plan);

        Dictionary<string, FileStream>? deleteOnCloseHandles = null;
        HashSet<string>? deleteOnCloseDirectories = null;
        if (plan.DeleteOnComplete)
        {
            deleteOnCloseHandles = new Dictionary<string, FileStream>(StringComparer.OrdinalIgnoreCase);
            deleteOnCloseDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var workloadResults = new List<WorkloadResult>();

        try
        {
            for (int i = 0; i < plan.Workloads.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var workload = plan.Workloads[i];
                _sink.OnWorkloadStart(workload, i, plan.Workloads.Count);

                var result = await RunWorkloadAsync(
                        plan,
                        workload,
                        i,
                        deleteOnCloseHandles,
                        deleteOnCloseDirectories,
                        cancellationToken)
                    .ConfigureAwait(false);
                workloadResults.Add(result);

                _sink.OnWorkloadComplete(workload, result);
            }
        }
        finally
        {
            if (deleteOnCloseHandles != null)
            {
                foreach (var handle in deleteOnCloseHandles.Values)
                {
                    await handle.DisposeAsync().ConfigureAwait(false);
                }
            }

            if (deleteOnCloseDirectories != null)
            {
                TryDeleteDirectories(deleteOnCloseDirectories);
            }
        }

        var endTime = DateTimeOffset.UtcNow;

        // Clean up test files if requested
        if (plan.DeleteOnComplete)
        {
            CleanupTestFiles(plan);
        }

        var benchmarkResult = new BenchmarkResult
        {
            Plan = plan,
            Workloads = workloadResults,
            StartTime = startTime,
            EndTime = endTime,
            SystemInfo = CollectSystemInfo()
        };

        _sink.OnBenchmarkComplete(benchmarkResult);

        return benchmarkResult;
    }

    private void ValidatePlan(BenchmarkPlan plan)
    {
        if (plan.Workloads.Count == 0)
        {
            throw new ArgumentException("Plan must contain at least one workload.", nameof(plan));
        }

        foreach (var workload in plan.Workloads)
        {
            ValidateWorkload(workload);
        }
    }

    private void ValidateWorkload(WorkloadSpec workload)
    {
        if (string.IsNullOrWhiteSpace(workload.FilePath))
        {
            throw new ArgumentException("Workload file path cannot be empty.");
        }

        if (workload.FileSize <= 0)
        {
            throw new ArgumentException($"Invalid file size: {workload.FileSize}");
        }

        if (workload.BlockSize <= 0)
        {
            throw new ArgumentException($"Invalid block size: {workload.BlockSize}");
        }

        if (workload.QueueDepth <= 0)
        {
            throw new ArgumentException($"Invalid queue depth: {workload.QueueDepth}");
        }

        if (workload.Threads <= 0)
        {
            throw new ArgumentException($"Invalid thread count: {workload.Threads}");
        }

        if (workload.WritePercent < 0 || workload.WritePercent > 100)
        {
            throw new ArgumentException($"Write percent must be 0-100: {workload.WritePercent}");
        }

        // Warn about potential issues (only for buffered IO where caching matters)
        if (!workload.NoBuffering)
        {
            var ramBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            if (workload.FileSize < ramBytes)
            {
                _sink.OnWarning($"Test file ({FormatBytes(workload.FileSize)}) fits in RAM ({FormatBytes(ramBytes)}). " +
                    "Buffered reads may be served from memory cache instead of disk. " +
                    "Use a larger file or enable NO_BUFFERING for accurate disk performance.");
            }
            else
            {
                _sink.OnWarning("Using buffered IO - results include OS cache effects, not raw disk performance.");
            }
        }
    }

    private void CleanupTestFiles(BenchmarkPlan plan)
    {
        // Get unique file paths from all workloads
        var filePaths = plan.Workloads
            .Select(w => w.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var filePath in filePaths)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) &&
                    Directory.Exists(directory) &&
                    IsDirectoryEmpty(directory))
                {
                    Directory.Delete(directory, false);
                }
            }
            catch (IOException ex)
            {
                // Don't fail the benchmark if cleanup fails, just warn
                _sink.OnWarning($"Failed to delete test file '{filePath}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _sink.OnWarning($"Failed to delete test file '{filePath}': {ex.Message}");
            }
        }
    }

    private async Task<WorkloadResult> RunWorkloadAsync(
        BenchmarkPlan plan,
        WorkloadSpec workload,
        int workloadIndex,
        Dictionary<string, FileStream>? deleteOnCloseHandles,
        HashSet<string>? deleteOnCloseDirectories,
        CancellationToken cancellationToken)
    {
        // Prepare the file
        var prepareSpec = new PrepareSpec
        {
            FilePath = workload.FilePath,
            FileSize = workload.FileSize,
            ReuseIfExists = plan.ReuseExistingFiles
        };

        var prepareResult = await _engine.PrepareAsync(prepareSpec, null, cancellationToken).ConfigureAwait(false);

        if (prepareResult.Warnings != null)
        {
            foreach (var warning in prepareResult.Warnings)
            {
                _sink.OnWarning(warning);
            }
        }

        if (deleteOnCloseHandles != null)
        {
            EnsureDeleteOnCloseHandle(prepareResult.FilePath, deleteOnCloseHandles, deleteOnCloseDirectories);
        }

        // Validate alignment for NO_BUFFERING
        if (workload.NoBuffering)
        {
            var sectorSize = prepareResult.LogicalSectorSize;
            if (workload.BlockSize % sectorSize != 0)
            {
                throw new InvalidOperationException(
                    $"Block size ({workload.BlockSize}) must be a multiple of sector size ({sectorSize}) for unbuffered IO.");
            }

            if (workload.Region.Offset % sectorSize != 0)
            {
                throw new InvalidOperationException(
                    $"Region offset ({workload.Region.Offset}) must be aligned to sector size ({sectorSize}) for unbuffered IO.");
            }
        }

        // Run trials
        var trialResults = new List<TrialResult>();
#pragma warning disable CA5394 // Random.Shared is appropriate for seed generation, not security
        var seed = plan.Seed != 0 ? plan.Seed : Random.Shared.Next();
#pragma warning restore CA5394

        for (int trial = 1; trial <= plan.Trials; trial++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _sink.OnTrialStart(workload, trial, plan.Trials);

            var trialSpec = new TrialSpec
            {
                Workload = workload,
                WarmupDuration = plan.WarmupDuration,
                MeasuredDuration = plan.MeasuredDuration,
                Seed = seed + workloadIndex * 1000 + trial,
                TrialNumber = trial,
                CollectTimeSeries = plan.CollectTimeSeries,
                TrackAllocations = plan.TrackAllocations,
                SectorSize = prepareResult.LogicalSectorSize
            };

            var progress = new Progress<TrialProgress>(p => _sink.OnTrialProgress(workload, trial, p));
            var result = await _engine.RunTrialAsync(trialSpec, progress, cancellationToken).ConfigureAwait(false);

            trialResults.Add(result);
            _sink.OnTrialComplete(workload, trial, result);
        }

        // Aggregate results
        return AggregateTrials(workload, trialResults, plan.ComputeConfidenceIntervals, plan.BootstrapIterations);
    }

    private static void EnsureDeleteOnCloseHandle(
        string filePath,
        Dictionary<string, FileStream> handles,
        HashSet<string>? directories)
    {
        if (handles.ContainsKey(filePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!string.IsNullOrEmpty(directory))
        {
            directories?.Add(directory);
        }

        var stream = new FileStream(
            filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete,
            1,
            FileOptions.DeleteOnClose);

        handles.Add(filePath, stream);
    }

    private static void TryDeleteDirectories(HashSet<string> directories)
    {
        foreach (var directory in directories.OrderByDescending(d => d.Length))
        {
            try
            {
                if (Directory.Exists(directory) && IsDirectoryEmpty(directory))
                {
                    Directory.Delete(directory, false);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup
            }
        }
    }

    private static bool IsDirectoryEmpty(string directory)
    {
        return !Directory.EnumerateFileSystemEntries(directory).Any();
    }

    private static WorkloadResult AggregateTrials(
        WorkloadSpec workload,
        List<TrialResult> trials,
        bool computeCI,
        int bootstrapIterations)
    {
        var throughputs = trials.Select(t => t.BytesPerSecond).ToArray();
        var iops = trials.Select(t => t.Iops).ToArray();

        var meanThroughput = throughputs.Average();
        var stdDevThroughput = ComputeStdDev(throughputs);
        var meanIops = iops.Average();
        var stdDevIops = ComputeStdDev(iops);

        // Aggregate latencies (mean of percentiles across trials)
        var meanLatency = new LatencyPercentiles
        {
            MinUs = trials.Average(t => t.Latency.MinUs),
            P50Us = trials.Average(t => t.Latency.P50Us),
            P90Us = trials.Average(t => t.Latency.P90Us),
            P95Us = trials.Average(t => t.Latency.P95Us),
            P99Us = trials.Average(t => t.Latency.P99Us),
            P999Us = trials.Average(t => t.Latency.P999Us),
            MaxUs = trials.Max(t => t.Latency.MaxUs),
            MeanUs = trials.Average(t => t.Latency.MeanUs)
        };

        var result = new WorkloadResult
        {
            Workload = workload,
            Trials = trials,
            MeanBytesPerSecond = meanThroughput,
            StdDevBytesPerSecond = stdDevThroughput,
            MeanIops = meanIops,
            StdDevIops = stdDevIops,
            MeanLatency = meanLatency
        };

        if (computeCI && trials.Count >= 2)
        {
            result = result with
            {
                ThroughputCI = ComputeBootstrapCI(throughputs, bootstrapIterations),
                IopsCI = ComputeBootstrapCI(iops, bootstrapIterations)
            };
        }

        return result;
    }

    private static double ComputeStdDev(double[] values)
    {
        if (values.Length < 2) return 0;
        var mean = values.Average();
        var sumSquares = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSquares / (values.Length - 1));
    }

    private static (double Lower, double Upper) ComputeBootstrapCI(double[] values, int iterations)
    {
        if (values.Length < 2)
        {
            var val = values.Length > 0 ? values[0] : 0;
            return (val, val);
        }

#pragma warning disable CA5394 // Random with fixed seed is appropriate for statistical bootstrapping
        var random = new Random(42); // Deterministic for reproducibility
        var bootstrapMeans = new double[iterations];

        for (int i = 0; i < iterations; i++)
        {
            double sum = 0;
            for (int j = 0; j < values.Length; j++)
            {
                sum += values[random.Next(values.Length)];
            }
            bootstrapMeans[i] = sum / values.Length;
        }
#pragma warning restore CA5394

        Array.Sort(bootstrapMeans);

        int lowerIndex = (int)(iterations * 0.025);
        int upperIndex = (int)(iterations * 0.975);

        return (bootstrapMeans[lowerIndex], bootstrapMeans[upperIndex]);
    }

    private static SystemInfo CollectSystemInfo()
    {
        return new SystemInfo
        {
            OsVersion = Environment.OSVersion.ToString(),
            LogicalProcessors = Environment.ProcessorCount,
            TotalMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            RuntimeVersion = Environment.Version.ToString()
        };
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
        return $"{value:F1} {suffixes[i]}";
    }
}
