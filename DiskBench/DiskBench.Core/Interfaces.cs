namespace DiskBench.Core;

/// <summary>
/// Information about a prepared file.
/// </summary>
public sealed class PrepareResult
{
    /// <summary>
    /// Path to the prepared file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Actual file size in bytes.
    /// </summary>
    public required long FileSize { get; init; }

    /// <summary>
    /// Physical sector size of the underlying device.
    /// </summary>
    public required int PhysicalSectorSize { get; init; }

    /// <summary>
    /// Logical sector size (for alignment requirements).
    /// </summary>
    public required int LogicalSectorSize { get; init; }

    /// <summary>
    /// Whether the file was reused (already existed with correct size).
    /// </summary>
    public required bool WasReused { get; init; }

    /// <summary>
    /// Whether SetFileValidData was used for instant allocation.
    /// </summary>
    public required bool UsedSetValidData { get; init; }

    /// <summary>
    /// Any warnings generated during preparation.
    /// </summary>
    public IReadOnlyList<string>? Warnings { get; init; }
}

/// <summary>
/// Interface for the low-level benchmark IO engine.
/// </summary>
public interface IBenchmarkEngine : IAsyncDisposable
{
    /// <summary>
    /// Prepares a test file for benchmarking.
    /// </summary>
    /// <param name="spec">File preparation specification.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Information about the prepared file.</returns>
    Task<PrepareResult> PrepareAsync(
        PrepareSpec spec,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a single benchmark trial.
    /// </summary>
    /// <param name="spec">Trial specification.</param>
    /// <param name="progress">Progress reporter for the trial.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Trial result.</returns>
    Task<TrialResult> RunTrialAsync(
        TrialSpec spec,
        IProgress<TrialProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the sector size for a file path.
    /// </summary>
    int GetSectorSize(string filePath);

    /// <summary>
    /// Gets detailed information about a specific drive.
    /// </summary>
    /// <param name="drivePath">Drive path (e.g., "C:\").</param>
    /// <returns>Drive details or null if unavailable.</returns>
    DriveDetails? GetDriveDetails(string drivePath);

    /// <summary>
    /// Gets detailed information about all available drives.
    /// </summary>
    /// <returns>List of drive details.</returns>
    IReadOnlyList<DriveDetails> GetAllDriveDetails();
}

/// <summary>
/// Sink for receiving benchmark events (for renderers/reporters).
/// </summary>
public interface IBenchmarkSink
{
    /// <summary>
    /// Called when a benchmark run starts.
    /// </summary>
    void OnBenchmarkStart(BenchmarkPlan plan);

    /// <summary>
    /// Called when a workload starts.
    /// </summary>
    void OnWorkloadStart(WorkloadSpec workload, int workloadIndex, int totalWorkloads);

    /// <summary>
    /// Called when a trial starts.
    /// </summary>
    void OnTrialStart(WorkloadSpec workload, int trialNumber, int totalTrials);

    /// <summary>
    /// Called periodically during a trial with progress.
    /// </summary>
    void OnTrialProgress(WorkloadSpec workload, int trialNumber, TrialProgress progress);

    /// <summary>
    /// Called when a trial completes.
    /// </summary>
    void OnTrialComplete(WorkloadSpec workload, int trialNumber, TrialResult result);

    /// <summary>
    /// Called when a workload completes.
    /// </summary>
    void OnWorkloadComplete(WorkloadSpec workload, WorkloadResult result);

    /// <summary>
    /// Called when the benchmark run completes.
    /// </summary>
    void OnBenchmarkComplete(BenchmarkResult result);

    /// <summary>
    /// Called when an error occurs.
    /// </summary>
    void OnError(string message, Exception? exception = null);

    /// <summary>
    /// Called for warning messages.
    /// </summary>
    void OnWarning(string message);
}

/// <summary>
/// A no-op benchmark sink for when no reporting is needed.
/// </summary>
public sealed class NullBenchmarkSink : IBenchmarkSink
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static NullBenchmarkSink Instance { get; } = new();

    private NullBenchmarkSink() { }

    /// <inheritdoc />
    public void OnBenchmarkStart(BenchmarkPlan plan) { }

    /// <inheritdoc />
    public void OnWorkloadStart(WorkloadSpec workload, int workloadIndex, int totalWorkloads) { }

    /// <inheritdoc />
    public void OnTrialStart(WorkloadSpec workload, int trialNumber, int totalTrials) { }

    /// <inheritdoc />
    public void OnTrialProgress(WorkloadSpec workload, int trialNumber, TrialProgress progress) { }

    /// <inheritdoc />
    public void OnTrialComplete(WorkloadSpec workload, int trialNumber, TrialResult result) { }

    /// <inheritdoc />
    public void OnWorkloadComplete(WorkloadSpec workload, WorkloadResult result) { }

    /// <inheritdoc />
    public void OnBenchmarkComplete(BenchmarkResult result) { }

    /// <inheritdoc />
    public void OnError(string message, Exception? exception = null) { }

    /// <inheritdoc />
    public void OnWarning(string message) { }
}
