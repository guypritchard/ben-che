using DiskBench.Core;

namespace DiskBench.Cli;

/// <summary>
/// Console-based benchmark sink that displays progress and results.
/// </summary>
internal sealed class ConsoleBenchmarkSink : IBenchmarkSink
{
    private int _currentWorkload;
    private int _totalWorkloads;
    private int _lastProgressLine = -1;

    public void OnBenchmarkStart(BenchmarkPlan plan)
    {
        Console.WriteLine($"Starting benchmark: {plan.Name ?? "Unnamed"}");
        Console.WriteLine($"  Workloads: {plan.Workloads.Count}");
        Console.WriteLine($"  Trials per workload: {plan.Trials}");
        Console.WriteLine($"  Warmup: {plan.WarmupDuration.TotalSeconds}s, Measured: {plan.MeasuredDuration.TotalSeconds}s");
        Console.WriteLine();

        _totalWorkloads = plan.Workloads.Count;
    }

    public void OnWorkloadStart(WorkloadSpec workload, int workloadIndex, int totalWorkloads)
    {
        _currentWorkload = workloadIndex + 1;
        Console.WriteLine($"┌─ Workload {_currentWorkload}/{totalWorkloads}: {workload.GetDisplayName()}");
        Console.WriteLine($"│  Block: {FormatSize(workload.BlockSize)}, QD: {workload.QueueDepth}, " +
                         $"Pattern: {workload.Pattern}, R/W: {100 - workload.WritePercent}%/{workload.WritePercent}%");
        Console.WriteLine($"│  Flags: {(workload.NoBuffering ? "NO_BUFFERING" : "BUFFERED")}" +
                         $"{(workload.WriteThrough ? " WRITE_THROUGH" : string.Empty)}");
    }

    public void OnTrialStart(WorkloadSpec workload, int trialNumber, int totalTrials)
    {
        Console.Write($"│  Trial {trialNumber}/{totalTrials}: ");
        _lastProgressLine = Console.CursorTop;
    }

    public void OnTrialProgress(WorkloadSpec workload, int trialNumber, TrialProgress progress)
    {
        if (_lastProgressLine >= 0 && Console.CursorTop == _lastProgressLine)
        {
            if (progress.IsFinalizing)
            {
                Console.Write($"\r???  Trial {trialNumber}: [Finalizing] Draining IO...                ");
                return;
            }

            var phase = progress.IsWarmup ? "Warmup" : "Running";
            var throughput = FormatThroughput(progress.CurrentBytesPerSecond);
            var iops = FormatIops(progress.CurrentIops);
            Console.Write($"\r???  Trial {trialNumber}: [{phase}] {progress.PercentComplete:F0}% - {throughput} ({iops})    ");
        }
    }

    public void OnTrialComplete(WorkloadSpec workload, int trialNumber, TrialResult result)
    {
        Console.WriteLine($"\r│  Trial {trialNumber}: {FormatThroughput(result.BytesPerSecond)} " +
                         $"({FormatIops(result.Iops)}) - Lat: p50={result.Latency.P50Us:F1}µs, " +
                         $"p99={result.Latency.P99Us:F1}µs                    ");
    }

    public void OnWorkloadComplete(WorkloadSpec workload, WorkloadResult result)
    {
        Console.WriteLine("│");
        Console.WriteLine($"│  ══ Summary ══");
        Console.WriteLine($"│  Throughput: {FormatThroughput(result.MeanBytesPerSecond)} " +
                         $"(±{FormatThroughput(result.StdDevBytesPerSecond)})");
        Console.WriteLine($"│  IOPS:       {FormatIops(result.MeanIops)} (±{result.StdDevIops:F1})");
        Console.WriteLine($"│  Latency:    p50={result.MeanLatency.P50Us:F1}µs, " +
                         $"p90={result.MeanLatency.P90Us:F1}µs, " +
                         $"p99={result.MeanLatency.P99Us:F1}µs, " +
                         $"p99.9={result.MeanLatency.P999Us:F1}µs");

        if (result.ThroughputCI.HasValue)
        {
            Console.WriteLine($"│  95% CI:     [{FormatThroughput(result.ThroughputCI.Value.Lower)}, " +
                             $"{FormatThroughput(result.ThroughputCI.Value.Upper)}]");
        }

        Console.WriteLine($"└─────────────────────────────────────────────────────────────────");
        Console.WriteLine();
    }

    public void OnBenchmarkComplete(BenchmarkResult result)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                           BENCHMARK COMPLETE                          ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  Workload                    │ Throughput      │ IOPS      │ p99 Lat ║");
        Console.WriteLine("╠──────────────────────────────┼─────────────────┼───────────┼─────────╣");

        foreach (var workload in result.Workloads)
        {
            var name = workload.Workload.GetDisplayName().PadRight(28)[..28];
            var throughput = FormatThroughput(workload.MeanBytesPerSecond).PadLeft(13);
            var iops = FormatIops(workload.MeanIops).PadLeft(9);
            var lat = $"{workload.MeanLatency.P99Us:F0}µs".PadLeft(7);
            Console.WriteLine($"║  {name} │ {throughput} │ {iops} │ {lat} ║");
        }

        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"Total duration: {result.Duration.TotalSeconds:F1}s");
        Console.WriteLine($"System: {result.SystemInfo?.OsVersion}");
        Console.WriteLine($"Processors: {result.SystemInfo?.LogicalProcessors}");
        Console.WriteLine($"Memory: {FormatSize(result.SystemInfo?.TotalMemoryBytes ?? 0)}");
    }

    public void OnError(string message, Exception? exception = null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n│  ERROR: {message}");
        if (exception != null)
        {
            Console.WriteLine($"│  {exception.GetType().Name}: {exception.Message}");
        }
        Console.ResetColor();
    }

    public void OnWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"│  ⚠ Warning: {message}");
        Console.ResetColor();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes == 0) return "0 B";
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

    private static string FormatThroughput(double bytesPerSecond)
    {
        if (bytesPerSecond >= 1024 * 1024 * 1024)
            return $"{bytesPerSecond / (1024 * 1024 * 1024):F2} GB/s";
        if (bytesPerSecond >= 1024 * 1024)
            return $"{bytesPerSecond / (1024 * 1024):F2} MB/s";
        if (bytesPerSecond >= 1024)
            return $"{bytesPerSecond / 1024:F2} KB/s";
        return $"{bytesPerSecond:F0} B/s";
    }

    private static string FormatIops(double iops)
    {
        if (iops >= 1_000_000)
            return $"{iops / 1_000_000:F2}M IOPS";
        if (iops >= 1000)
            return $"{iops / 1000:F1}K IOPS";
        return $"{iops:F0} IOPS";
    }
}
