using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DiskBench.Metrics;

/// <summary>
/// Combined metrics collector for a benchmark trial.
/// Aggregates histogram and time series in a single allocation-free interface.
/// </summary>
public sealed class TrialMetricsCollector
{
    private readonly LatencyHistogram _histogram;
    private readonly ThroughputTimeSeries? _timeSeries;
    private readonly long _startTimestamp;
    private readonly double _ticksPerSecond;

    private long _totalBytes;
    private long _totalOperations;
    private long _readOperations;
    private long _writeOperations;
    private long _lastSecondBytes;
    private long _lastSecondOps;
    private int _currentSecond;

    /// <summary>
    /// Gets the latency histogram.
    /// </summary>
    public LatencyHistogram Histogram => this._histogram;

    /// <summary>
    /// Gets the throughput time series (if enabled).
    /// </summary>
    public ThroughputTimeSeries? TimeSeries => this._timeSeries;

    /// <summary>
    /// Gets the total bytes transferred.
    /// </summary>
    public long TotalBytes => this._totalBytes;

    /// <summary>
    /// Gets the total operations completed.
    /// </summary>
    public long TotalOperations => this._totalOperations;

    /// <summary>
    /// Gets the read operation count.
    /// </summary>
    public long ReadOperations => this._readOperations;

    /// <summary>
    /// Gets the write operation count.
    /// </summary>
    public long WriteOperations => this._writeOperations;

    /// <summary>
    /// Creates a new trial metrics collector.
    /// </summary>
    /// <param name="maxDurationSeconds">Maximum duration for time series.</param>
    /// <param name="collectTimeSeries">Whether to collect time series data.</param>
    public TrialMetricsCollector(int maxDurationSeconds, bool collectTimeSeries = true)
    {
        _histogram = new LatencyHistogram();
        _timeSeries = collectTimeSeries ? new ThroughputTimeSeries(maxDurationSeconds + 10) : null;
        _startTimestamp = Stopwatch.GetTimestamp();
        _ticksPerSecond = Stopwatch.Frequency;
    }

    /// <summary>
    /// Records a completed IO operation.
    /// This method is designed for zero-allocation hot path usage.
    /// </summary>
    /// <param name="completionTimestamp">Timestamp when IO completed.</param>
    /// <param name="latencyTicks">IO latency in Stopwatch ticks.</param>
    /// <param name="bytes">Bytes transferred.</param>
    /// <param name="isWrite">Whether this was a write operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordCompletion(long completionTimestamp, long latencyTicks, int bytes, bool isWrite)
    {
        // Record latency
        this._histogram.RecordLatencyTicks(latencyTicks);

        // Update counters
        this._totalBytes += bytes;
        this._totalOperations++;

        if (isWrite)
        {
            this._writeOperations++;
        }
        else
        {
            this._readOperations++;
        }

        // Update time series if enabled
        if (this._timeSeries != null)
        {
            int second = (int)((completionTimestamp - this._startTimestamp) / this._ticksPerSecond);

            if (second == this._currentSecond)
            {
                this._lastSecondBytes += bytes;
                this._lastSecondOps++;
            }
            else
            {
                // Flush previous second
                if (this._currentSecond >= 0 && this._lastSecondOps > 0)
                {
                    this._timeSeries.Record(this._currentSecond, this._lastSecondBytes, this._lastSecondOps);
                }

                this._currentSecond = second;
                this._lastSecondBytes = bytes;
                this._lastSecondOps = 1;
            }
        }
    }

    /// <summary>
    /// Flushes any pending time series data.
    /// </summary>
    public void Flush()
    {
        if (this._timeSeries != null && this._lastSecondOps > 0)
        {
            this._timeSeries.Record(this._currentSecond, this._lastSecondBytes, this._lastSecondOps);
            this._lastSecondBytes = 0;
            this._lastSecondOps = 0;
        }
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void Reset()
    {
        this._histogram.Reset();
        this._timeSeries?.Reset();
        this._totalBytes = 0;
        this._totalOperations = 0;
        this._readOperations = 0;
        this._writeOperations = 0;
        this._lastSecondBytes = 0;
        this._lastSecondOps = 0;
        this._currentSecond = 0;
    }
}
