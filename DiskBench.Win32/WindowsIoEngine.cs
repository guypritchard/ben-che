using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DiskBench.Core;
using DiskBench.Metrics;

namespace DiskBench.Win32;

/// <summary>
/// Windows IO engine using overlapped I/O and IO Completion Ports (IOCP).
/// Provides high-performance, low-overhead disk benchmarking.
/// </summary>
public sealed class WindowsIoEngine : IBenchmarkEngine
{
    private readonly WindowsIoEngineOptions _options;
    private bool _disposed;

    /// <summary>
    /// Creates a new Windows IO engine with default options.
    /// </summary>
    public WindowsIoEngine() : this(new WindowsIoEngineOptions())
    {
    }

    /// <summary>
    /// Creates a new Windows IO engine with specified options.
    /// </summary>
    public WindowsIoEngine(WindowsIoEngineOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<PrepareResult> PrepareAsync(
        PrepareSpec spec,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var (logicalSectorSize, physicalSectorSize) = DiskInfo.GetSectorSize(spec.FilePath);
        var warnings = new List<string>();
        bool wasReused = false;
        bool usedSetValidData = false;

        // Check if file exists and matches size
        if (spec.ReuseIfExists && File.Exists(spec.FilePath))
        {
            var existingInfo = new FileInfo(spec.FilePath);
            if (existingInfo.Length == spec.FileSize)
            {
                wasReused = true;
                return new PrepareResult
                {
                    FilePath = spec.FilePath,
                    FileSize = spec.FileSize,
                    PhysicalSectorSize = physicalSectorSize,
                    LogicalSectorSize = logicalSectorSize,
                    WasReused = true,
                    UsedSetValidData = false,
                    Warnings = warnings.Count > 0 ? warnings : null
                };
            }
        }

        // Ensure directory exists
        var directory = Path.GetDirectoryName(spec.FilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Create/resize the file
        await Task.Run(() =>
        {
            var handle = NativeMethods.CreateFileW(
                spec.FilePath,
                NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
                NativeMethods.FILE_SHARE_READ,
                IntPtr.Zero,
                NativeMethods.CREATE_ALWAYS,
                NativeMethods.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);

            if (handle == NativeMethods.INVALID_HANDLE_VALUE)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create test file.");
            }

            try
            {
                // Set file size
                if (!NativeMethods.SetFilePointerEx(handle, spec.FileSize, out _, 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set file pointer.");
                }

                if (!NativeMethods.SetEndOfFile(handle))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set end of file.");
                }

                // Try to use SetFileValidData for instant allocation
                if (spec.UseSetValidData)
                {
                    if (NativeMethods.SetFileValidData(handle, spec.FileSize))
                    {
                        usedSetValidData = true;
                    }
                    else
                    {
                        warnings.Add("SetFileValidData failed (requires SeManageVolumePrivilege). File will be zero-filled.");
                    }
                }
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        }, cancellationToken).ConfigureAwait(false);

        if (!usedSetValidData)
        {
            warnings.Add("SetFileValidData unavailable. Materializing file with writes to avoid sparse zero-read artifacts.");
            await MaterializeFileAsync(spec, progress, cancellationToken).ConfigureAwait(false);
        }

        return new PrepareResult
        {
            FilePath = spec.FilePath,
            FileSize = spec.FileSize,
            PhysicalSectorSize = physicalSectorSize,
            LogicalSectorSize = logicalSectorSize,
            WasReused = wasReused,
            UsedSetValidData = usedSetValidData,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }

    private static async Task MaterializeFileAsync(
        PrepareSpec spec,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        const int chunkSize = 4 * 1024 * 1024;
        var buffer = new byte[chunkSize];
        var pattern = spec.FillPattern;

        if (pattern is { Count: > 0 })
        {
            FillPattern(buffer, pattern);
        }

        await using var stream = new FileStream(
            spec.FilePath,
            FileMode.Open,
            FileAccess.Write,
            FileShare.Read,
            chunkSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        long remaining = spec.FileSize;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int bytesToWrite = (int)Math.Min(remaining, buffer.Length);
            if (pattern is { Count: > 0 } && bytesToWrite != buffer.Length)
            {
                FillPattern(buffer.AsSpan(0, bytesToWrite), pattern);
            }

            await stream.WriteAsync(buffer.AsMemory(0, bytesToWrite), cancellationToken).ConfigureAwait(false);
            remaining -= bytesToWrite;

            if (progress != null)
            {
                var written = spec.FileSize - remaining;
                progress.Report((double)written / spec.FileSize);
            }
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void FillPattern(Span<byte> buffer, IReadOnlyList<byte> pattern)
    {
        if (pattern.Count == 0)
        {
            buffer.Clear();
            return;
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = pattern[i % pattern.Count];
        }
    }

    /// <inheritdoc />
    public async Task<TrialResult> RunTrialAsync(
        TrialSpec spec,
        IProgress<TrialProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var workload = spec.Workload;
        var warnings = new List<string>();

        // Validate alignment requirements for NO_BUFFERING
        if (workload.NoBuffering)
        {
            if (workload.BlockSize % spec.SectorSize != 0)
            {
                throw new InvalidOperationException(
                    $"Block size ({workload.BlockSize}) must be a multiple of sector size ({spec.SectorSize}) for unbuffered IO.");
            }
        }

        // Warn about flush policy
        if (workload.FlushPolicy == FlushPolicy.EveryIO)
        {
            warnings.Add("FlushPolicy.EveryIO will significantly impact performance measurements.");
        }

        // Run the actual trial
        return await Task.Run(() => RunTrialInternal(spec, progress, warnings, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    private TrialResult RunTrialInternal(
        TrialSpec spec,
        IProgress<TrialProgress>? progress,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var workload = spec.Workload;
        var totalSlots = workload.QueueDepth * workload.Threads;
        var alignment = workload.NoBuffering ? spec.SectorSize : 1;

        // Set thread priority if configured
        if (_options.RaiseThreadPriority)
        {
            NativeMethods.SetThreadPriority(NativeMethods.GetCurrentThread(), NativeMethods.THREAD_PRIORITY_HIGHEST);
        }

        // Set thread affinity if configured
        if (_options.PinToCore >= 0)
        {
            nuint affinityMask = 1u << _options.PinToCore;
            NativeMethods.SetThreadAffinityMask(NativeMethods.GetCurrentThread(), affinityMask);
        }

        // Open file with appropriate flags
        uint flags = NativeMethods.FILE_FLAG_OVERLAPPED;
        if (workload.NoBuffering) flags |= NativeMethods.FILE_FLAG_NO_BUFFERING;
        if (workload.WriteThrough) flags |= NativeMethods.FILE_FLAG_WRITE_THROUGH;
        if (workload.Pattern == AccessPattern.Sequential) flags |= NativeMethods.FILE_FLAG_SEQUENTIAL_SCAN;
        else flags |= NativeMethods.FILE_FLAG_RANDOM_ACCESS;

        uint access = NativeMethods.GENERIC_READ;
        if (workload.WritePercent > 0) access |= NativeMethods.GENERIC_WRITE;

        var fileHandle = NativeMethods.CreateFileW(
            workload.FilePath,
            access,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE | NativeMethods.FILE_SHARE_DELETE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            flags,
            IntPtr.Zero);

        if (fileHandle == NativeMethods.INVALID_HANDLE_VALUE)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open file: {workload.FilePath}");
        }

        try
        {
            // Create IOCP
            var iocpHandle = NativeMethods.CreateIoCompletionPort(fileHandle, IntPtr.Zero, 0, 1);
            if (iocpHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create IO completion port.");
            }

            try
            {
                return RunWithIocp(spec, fileHandle, iocpHandle, totalSlots, alignment, progress, warnings, cancellationToken);
            }
            finally
            {
                NativeMethods.CloseHandle(iocpHandle);
            }
        }
        finally
        {
            // Final flush if configured
            if (workload.FlushPolicy == FlushPolicy.AtEnd && workload.WritePercent > 0)
            {
                NativeMethods.FlushFileBuffers(fileHandle);
            }

            NativeMethods.CloseHandle(fileHandle);
        }
    }

    private static TrialResult RunWithIocp(
        TrialSpec spec,
        IntPtr fileHandle,
        IntPtr iocpHandle,
        int totalSlots,
        int alignment,
        IProgress<TrialProgress>? progress,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var workload = spec.Workload;
        var ticksPerMicrosecond = LatencyHistogram.TicksPerMicrosecond;

        using var slotPool = new IoSlotPool(totalSlots, workload.BlockSize, alignment);

        // Fill write buffers with data
        if (workload.WritePercent > 0)
        {
            slotPool.FillWriteBuffersRandom(spec.Seed);
        }

        // Create offset generator
        long regionLength = workload.Region.Length > 0 ? workload.Region.Length : (workload.FileSize - workload.Region.Offset);
        var offsetGen = new OffsetGenerator(
            workload.Pattern,
            workload.FileSize,
            workload.BlockSize,
            workload.Region.Offset,
            regionLength,
            spec.Seed);

        // Determine read/write threshold (0-255 scale for hot path)
        int writeThreshold = (int)(workload.WritePercent * 2.55);
        var writeRandom = new Random(spec.Seed + 1);
        var writeDecisions = new byte[65536];
        writeRandom.NextBytes(writeDecisions);
        int writeDecisionIndex = 0;

        // Metrics collector
        var maxSeconds = (int)(spec.WarmupDuration.TotalSeconds + spec.MeasuredDuration.TotalSeconds + 10);
        var metrics = new TrialMetricsCollector(maxSeconds, spec.CollectTimeSeries);

        // Allocation tracking
        long allocsBefore = 0;
        long allocsAfter = 0;

        // Timing
        var warmupDurationTicks = (long)(spec.WarmupDuration.TotalSeconds * Stopwatch.Frequency);
        var measuredDurationTicks = (long)(spec.MeasuredDuration.TotalSeconds * Stopwatch.Frequency);

        var trialStart = Stopwatch.GetTimestamp();
        var warmupEnd = trialStart + warmupDurationTicks;
        var measuredStart = warmupEnd;
        var measuredEnd = measuredStart + measuredDurationTicks;

        bool inMeasuredPhase = spec.WarmupDuration == TimeSpan.Zero;
        bool measuredStarted = inMeasuredPhase;

        if (inMeasuredPhase && spec.TrackAllocations)
        {
            allocsBefore = GC.GetAllocatedBytesForCurrentThread();
        }

        // Completion entries buffer (reused)
        var completionEntries = new OverlappedEntry[totalSlots];

        // Issue initial IOs
        for (int i = 0; i < totalSlots; i++)
        {
            var slot = slotPool[i];
            IssueIo(slot, fileHandle, offsetGen, writeThreshold, writeDecisions, ref writeDecisionIndex);
        }

        // Main completion loop
        var lastProgressTime = trialStart;
        var progressIntervalTicks = Stopwatch.Frequency / 4; // 4Hz progress updates

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = Stopwatch.GetTimestamp();

            // Check phase transitions
            if (!measuredStarted && now >= warmupEnd)
            {
                // Transition to measured phase
                measuredStarted = true;
                inMeasuredPhase = true;
                measuredStart = now;
                measuredEnd = now + measuredDurationTicks;
                metrics.Reset();

                if (spec.TrackAllocations)
                {
                    allocsBefore = GC.GetAllocatedBytesForCurrentThread();
                }
            }

            // Check if measured phase is complete
            if (inMeasuredPhase && now >= measuredEnd)
            {
                break;
            }

            // Wait for completions
            bool gotCompletion = NativeMethods.GetQueuedCompletionStatusEx(
                iocpHandle,
                completionEntries,
                (uint)totalSlots,
                out uint numCompleted,
                100, // 100ms timeout
                false);

            if (!gotCompletion)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == 258) // WAIT_TIMEOUT
                {
                    continue;
                }
                if (error == NativeMethods.ERROR_OPERATION_ABORTED)
                {
                    break;
                }
                throw new Win32Exception(error, "GetQueuedCompletionStatusEx failed.");
            }

            now = Stopwatch.GetTimestamp();

            // Process completions
            for (int i = 0; i < numCompleted; i++)
            {
                ref var entry = ref completionEntries[i];
                
                // Find the slot by OVERLAPPED pointer
                var slot = slotPool.FindByOverlapped(entry.Overlapped);
                if (slot == null || !slot.IsPending)
                {
                    continue;
                }

                slot.IsPending = false;
                long latencyTicks = now - slot.SubmitTimestamp;
                int bytesTransferred = (int)entry.NumberOfBytesTransferred;
                if (bytesTransferred <= 0)
                {
                    continue;
                }

                // Record metrics only during measured phase
                if (inMeasuredPhase)
                {
                    metrics.RecordCompletion(now, latencyTicks, bytesTransferred, slot.IsWrite);
                }

                // Re-issue IO if we're still running
                if (now < measuredEnd)
                {
                    IssueIo(slot, fileHandle, offsetGen, writeThreshold, writeDecisions, ref writeDecisionIndex);
                }
            }

            // Report progress
            if (progress != null && now - lastProgressTime >= progressIntervalTicks)
            {
                lastProgressTime = now;
                var elapsed = TimeSpan.FromSeconds((double)(now - (inMeasuredPhase ? measuredStart : trialStart)) / Stopwatch.Frequency);
                var duration = inMeasuredPhase ? spec.MeasuredDuration : spec.WarmupDuration;
                var elapsedSeconds = elapsed.TotalSeconds;

                progress.Report(new TrialProgress
                {
                    IsWarmup = !inMeasuredPhase,
                    Elapsed = elapsed,
                    Duration = duration,
                    CurrentBytesPerSecond = elapsedSeconds > 0 ? metrics.TotalBytes / elapsedSeconds : 0,
                    CurrentIops = elapsedSeconds > 0 ? metrics.TotalOperations / elapsedSeconds : 0,
                    TotalBytes = metrics.TotalBytes,
                    TotalOperations = metrics.TotalOperations
                });
            }
        }

        if (progress != null)
        {
            var finalizeSeconds = spec.MeasuredDuration.TotalSeconds;
            progress.Report(new TrialProgress
            {
                IsWarmup = false,
                IsFinalizing = true,
                Elapsed = spec.MeasuredDuration,
                Duration = spec.MeasuredDuration,
                CurrentBytesPerSecond = finalizeSeconds > 0 ? metrics.TotalBytes / finalizeSeconds : 0,
                CurrentIops = finalizeSeconds > 0 ? metrics.TotalOperations / finalizeSeconds : 0,
                TotalBytes = metrics.TotalBytes,
                TotalOperations = metrics.TotalOperations
            });
        }

        // Drain pending IOs
        DrainPendingIos(fileHandle, iocpHandle, slotPool, completionEntries, cancellationToken);

        // Track allocations
        if (spec.TrackAllocations)
        {
            allocsAfter = GC.GetAllocatedBytesForCurrentThread();
            long allocated = allocsAfter - allocsBefore;
            if (allocated > 0)
            {
                warnings.Add($"Allocated {allocated} bytes during measured window.");
            }
        }

        // Finalize metrics
        metrics.Flush();

        // Calculate actual duration
        var actualEnd = Stopwatch.GetTimestamp();
        var actualDuration = TimeSpan.FromSeconds((double)(actualEnd - measuredStart) / Stopwatch.Frequency);

        // Build time series samples
        List<Core.TimeSeriesSample>? timeSeries = null;
        if (spec.CollectTimeSeries && metrics.TimeSeries != null)
        {
            var snapshot = metrics.TimeSeries.CreateSnapshot();
            timeSeries = new List<Core.TimeSeriesSample>();
            foreach (var sample in snapshot.Samples)
            {
                timeSeries.Add(new Core.TimeSeriesSample
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
            Latency = LatencyPercentiles.FromHistogram(metrics.Histogram, ticksPerMicrosecond),
            TimeSeries = timeSeries,
            AllocatedBytes = spec.TrackAllocations ? allocsAfter - allocsBefore : null,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }

    private static void IssueIo(
        IoSlot slot,
        IntPtr fileHandle,
        OffsetGenerator offsetGen,
        int writeThreshold,
        byte[] writeDecisions,
        ref int writeDecisionIndex)
    {
        long offset = offsetGen.GetNextOffset();
        bool isWrite = writeDecisions[writeDecisionIndex++ & 0xFFFF] < writeThreshold;

        slot.Configure(offset, slot.Size, isWrite, Stopwatch.GetTimestamp());
        slot.IsPending = true;

        // Update OVERLAPPED with slot index as internal data for fast lookup
        ref var overlapped = ref slot.Overlapped;
        overlapped.OffsetLow = (int)(offset & 0xFFFFFFFF);
        overlapped.OffsetHigh = (int)(offset >> 32);

        bool success;
        if (isWrite)
        {
            success = NativeMethods.WriteFile(fileHandle, slot.Buffer, (uint)slot.Size, out _, ref overlapped);
        }
        else
        {
            success = NativeMethods.ReadFile(fileHandle, slot.Buffer, (uint)slot.Size, out _, ref overlapped);
        }

        if (!success)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != NativeMethods.ERROR_IO_PENDING)
            {
                slot.IsPending = false;
                throw new Win32Exception(error, isWrite ? "WriteFile failed" : "ReadFile failed");
            }
        }
    }

    private static void DrainPendingIos(
        IntPtr fileHandle,
        IntPtr iocpHandle,
        IoSlotPool slotPool,
        OverlappedEntry[] completionEntries,
        CancellationToken cancellationToken)
    {
        // Cancel all pending IOs
        NativeMethods.CancelIoEx(fileHandle, IntPtr.Zero);

        // Wait for completions with timeout
        var drainStart = Stopwatch.GetTimestamp();
        var drainTimeout = Stopwatch.Frequency * 5; // 5 second timeout

        while (slotPool.GetPendingCount() > 0 && !cancellationToken.IsCancellationRequested)
        {
            if (Stopwatch.GetTimestamp() - drainStart > drainTimeout)
            {
                break; // Timeout
            }

            bool gotCompletion = NativeMethods.GetQueuedCompletionStatusEx(
                iocpHandle,
                completionEntries,
                (uint)slotPool.Count,
                out uint numCompleted,
                100,
                false);

            if (gotCompletion)
            {
                for (int i = 0; i < numCompleted; i++)
                {
                    var slot = slotPool.FindByOverlapped(completionEntries[i].Overlapped);
                    if (slot != null)
                    {
                        slot.IsPending = false;
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public int GetSectorSize(string filePath)
    {
        var (logical, _) = DiskInfo.GetSectorSize(filePath);
        return logical;
    }

    /// <inheritdoc />
    public DriveDetails? GetDriveDetails(string drivePath)
    {
        return DiskInfo.GetDriveDetails(drivePath);
    }

    /// <inheritdoc />
    public IReadOnlyList<DriveDetails> GetAllDriveDetails()
    {
        return DiskInfo.GetAllDriveDetails();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Options for the Windows IO engine.
/// </summary>
public sealed class WindowsIoEngineOptions
{
    /// <summary>
    /// Whether to raise thread priority during benchmarking.
    /// </summary>
    public bool RaiseThreadPriority { get; init; }

    /// <summary>
    /// Core to pin the thread to (-1 = no pinning).
    /// </summary>
    public int PinToCore { get; init; } = -1;

    /// <summary>
    /// Whether to verify read data integrity.
    /// </summary>
    public bool VerifyReads { get; init; }
}
