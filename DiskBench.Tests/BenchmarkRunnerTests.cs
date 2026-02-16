using DiskBench.Core;
using Xunit;

namespace DiskBench.Tests;

/// <summary>
/// Tests for the BenchmarkRunner using the fake engine.
/// </summary>
public class BenchmarkRunnerTests
{
    [Fact]
    public async Task RunAsync_SingleWorkload_ReturnsResult()
    {
        await using var engine = new FakeBenchmarkEngine();
        var runner = new BenchmarkRunner(engine);

        var plan = new BenchmarkPlan
        {
            Workloads =
            [
                new WorkloadSpec
                {
                    FilePath = "test.dat",
                    FileSize = 1024 * 1024,
                    BlockSize = 4096,
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 1
                }
            ],
            Trials = 1,
            WarmupDuration = TimeSpan.Zero,
            MeasuredDuration = TimeSpan.FromMilliseconds(100)
        };

        var result = await runner.RunAsync(plan);

        Assert.NotNull(result);
        Assert.Single(result.Workloads);
        Assert.Single(result.Workloads[0].Trials);
    }

    [Fact]
    public async Task RunAsync_MultipleTrials_AggregatesResults()
    {
        await using var engine = new FakeBenchmarkEngine();
        var runner = new BenchmarkRunner(engine);

        var plan = new BenchmarkPlan
        {
            Workloads =
            [
                new WorkloadSpec
                {
                    FilePath = "test.dat",
                    FileSize = 1024 * 1024,
                    BlockSize = 4096,
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 1
                }
            ],
            Trials = 3,
            WarmupDuration = TimeSpan.Zero,
            MeasuredDuration = TimeSpan.FromMilliseconds(50)
        };

        var result = await runner.RunAsync(plan);

        Assert.Equal(3, result.Workloads[0].Trials.Count);
        Assert.True(result.Workloads[0].MeanBytesPerSecond > 0);
        Assert.True(result.Workloads[0].MeanIops > 0);
    }

    [Fact]
    public async Task RunAsync_MultipleWorkloads_ExecutesAll()
    {
        await using var engine = new FakeBenchmarkEngine();
        var runner = new BenchmarkRunner(engine);

        var plan = new BenchmarkPlan
        {
            Workloads =
            [
                new WorkloadSpec
                {
                    Name = "Read",
                    FilePath = "test.dat",
                    FileSize = 1024 * 1024,
                    BlockSize = 4096,
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 1
                },
                new WorkloadSpec
                {
                    Name = "Write",
                    FilePath = "test.dat",
                    FileSize = 1024 * 1024,
                    BlockSize = 4096,
                    Pattern = AccessPattern.Random,
                    WritePercent = 100,
                    QueueDepth = 1
                }
            ],
            Trials = 1,
            WarmupDuration = TimeSpan.Zero,
            MeasuredDuration = TimeSpan.FromMilliseconds(50)
        };

        var result = await runner.RunAsync(plan);

        Assert.Equal(2, result.Workloads.Count);
        Assert.Equal("Read", result.Workloads[0].Workload.Name);
        Assert.Equal("Write", result.Workloads[1].Workload.Name);
    }

    [Fact]
    public async Task RunAsync_WithConfidenceIntervals_ComputesCI()
    {
        await using var engine = new FakeBenchmarkEngine();
        var runner = new BenchmarkRunner(engine);

        var plan = new BenchmarkPlan
        {
            Workloads =
            [
                new WorkloadSpec
                {
                    FilePath = "test.dat",
                    FileSize = 1024 * 1024,
                    BlockSize = 4096,
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 1
                }
            ],
            Trials = 3,
            WarmupDuration = TimeSpan.Zero,
            MeasuredDuration = TimeSpan.FromMilliseconds(50),
            ComputeConfidenceIntervals = true,
            BootstrapIterations = 1000
        };

        var result = await runner.RunAsync(plan);

        Assert.NotNull(result.Workloads[0].ThroughputCI);
        Assert.NotNull(result.Workloads[0].IopsCI);
    }

    [Fact]
    public async Task RunAsync_Cancellation_Throws()
    {
        await using var engine = new FakeBenchmarkEngine();
        var runner = new BenchmarkRunner(engine);

        var plan = new BenchmarkPlan
        {
            Workloads =
            [
                new WorkloadSpec
                {
                    FilePath = "test.dat",
                    FileSize = 1024 * 1024,
                    BlockSize = 4096,
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 1
                }
            ],
            Trials = 1,
            WarmupDuration = TimeSpan.Zero,
            MeasuredDuration = TimeSpan.FromSeconds(10)
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(plan, cts.Token));
    }

    [Fact]
    public async Task RunAsync_InvalidBlockSize_ThrowsArgumentException()
    {
        await using var engine = new FakeBenchmarkEngine();
        var runner = new BenchmarkRunner(engine);

        var plan = new BenchmarkPlan
        {
            Workloads =
            [
                new WorkloadSpec
                {
                    FilePath = "test.dat",
                    FileSize = 1024 * 1024,
                    BlockSize = 0, // Invalid
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 1
                }
            ],
            Trials = 1
        };

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(plan));
    }

    [Fact]
    public async Task RunAsync_EmptyWorkloads_ThrowsArgumentException()
    {
        await using var engine = new FakeBenchmarkEngine();
        var runner = new BenchmarkRunner(engine);

        var plan = new BenchmarkPlan
        {
            Workloads = [],
            Trials = 1
        };

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(plan));
    }

    [Fact]
    public async Task RunAsync_SinkReceivesEvents()
    {
        await using var engine = new FakeBenchmarkEngine();
        var sink = new TestBenchmarkSink();
        var runner = new BenchmarkRunner(engine, sink);

        var plan = new BenchmarkPlan
        {
            Workloads =
            [
                new WorkloadSpec
                {
                    FilePath = "test.dat",
                    FileSize = 1024 * 1024,
                    BlockSize = 4096,
                    Pattern = AccessPattern.Random,
                    WritePercent = 0,
                    QueueDepth = 1
                }
            ],
            Trials = 2,
            WarmupDuration = TimeSpan.Zero,
            MeasuredDuration = TimeSpan.FromMilliseconds(50)
        };

        await runner.RunAsync(plan);

        Assert.True(sink.BenchmarkStarted);
        Assert.True(sink.BenchmarkCompleted);
        Assert.Equal(1, sink.WorkloadStartCount);
        Assert.Equal(1, sink.WorkloadCompleteCount);
        Assert.Equal(2, sink.TrialStartCount);
        Assert.Equal(2, sink.TrialCompleteCount);
    }

    private sealed class TestBenchmarkSink : IBenchmarkSink
    {
        public bool BenchmarkStarted { get; private set; }
        public bool BenchmarkCompleted { get; private set; }
        public int WorkloadStartCount { get; private set; }
        public int WorkloadCompleteCount { get; private set; }
        public int TrialStartCount { get; private set; }
        public int TrialCompleteCount { get; private set; }
        public int ProgressCount { get; private set; }
        public int WarningCount { get; private set; }

        public void OnBenchmarkStart(BenchmarkPlan plan) => BenchmarkStarted = true;
        public void OnBenchmarkComplete(BenchmarkResult result) => BenchmarkCompleted = true;
        public void OnWorkloadStart(WorkloadSpec workload, int workloadIndex, int totalWorkloads) => WorkloadStartCount++;
        public void OnWorkloadComplete(WorkloadSpec workload, WorkloadResult result) => WorkloadCompleteCount++;
        public void OnTrialStart(WorkloadSpec workload, int trialNumber, int totalTrials) => TrialStartCount++;
        public void OnTrialComplete(WorkloadSpec workload, int trialNumber, TrialResult result) => TrialCompleteCount++;
        public void OnTrialProgress(WorkloadSpec workload, int trialNumber, TrialProgress progress) => ProgressCount++;
        public void OnError(string message, Exception? exception = null) { }
        public void OnWarning(string message) => WarningCount++;
    }
}
