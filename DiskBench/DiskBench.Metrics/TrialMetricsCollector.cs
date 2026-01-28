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
    public LatencyHistogram Histogram => _histogram;

    /// <summary>
    /// Gets the throughput time series (if enabled).
    /// </summary>
    public ThroughputTimeSeries? TimeSeries => _timeSeries;

    /// <summary>
    /// Gets the total bytes transferred.
    /// </summary>
    public long TotalBytes => _totalBytes;

    /// <summary>
    /// Gets the total operations completed.
    /// </summary>
    public long TotalOperations => _totalOperations;

    /// <summary>
    /// Gets the read operation count.
    /// </summary>
    public long ReadOperations => _readOperations;

    /// <summary>
    /// Gets the write operation count.
    /// </summary>
    public long WriteOperations => _writeOperations;

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
        _histogram.RecordLatencyTicks(latencyTicks);

        // Update counters
        _totalBytes += bytes;
        _totalOperations++;

        if (isWrite)
        {
            _writeOperations++;
        }
        else
        {
            _readOperations++;
        }

        // Update time series if enabled
        if (_timeSeries != null)
        {
            int second = (int)((completionTimestamp - _startTimestamp) / _ticksPerSecond);

            if (second == _currentSecond)
            {
                _lastSecondBytes += bytes;
                _lastSecondOps++;
            }
            else
            {
                // Flush previous second
                if (_currentSecond >= 0 && _lastSecondOps > 0)
                {
                    _timeSeries.Record(_currentSecond, _lastSecondBytes, _lastSecondOps);
                }

                _currentSecond = second;
                _lastSecondBytes = bytes;
                _lastSecondOps = 1;
            }
        }
    }

    /// <summary>
    /// Flushes any pending time series data.
    /// </summary>
    public void Flush()
    {
        if (_timeSeries != null && _lastSecondOps > 0)
        {
            _timeSeries.Record(_currentSecond, _lastSecondBytes, _lastSecondOps);
            _lastSecondBytes = 0;
            _lastSecondOps = 0;
        }
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void Reset()
    {
        _histogram.Reset();
        _timeSeries?.Reset();
        _totalBytes = 0;
        _totalOperations = 0;
        _readOperations = 0;
        _writeOperations = 0;
        _lastSecondBytes = 0;
        _lastSecondOps = 0;
        _currentSecond = 0;
    }
}
