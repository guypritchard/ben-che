using System.Runtime.CompilerServices;

namespace DiskBench.Metrics;

/// <summary>
/// Collects per-second throughput time series data.
/// Designed for zero-allocation on the hot path.
/// </summary>
public sealed class ThroughputTimeSeries
{
    private readonly long[] _bytes;
    private readonly long[] _operations;
    private readonly int _maxSeconds;
    private int _currentSecond;

    /// <summary>
    /// Gets the maximum number of seconds this time series can hold.
    /// </summary>
    public int MaxSeconds => _maxSeconds;

    /// <summary>
    /// Gets the current number of recorded seconds.
    /// </summary>
    public int CurrentSecond => _currentSecond;

    /// <summary>
    /// Creates a new throughput time series.
    /// </summary>
    /// <param name="maxSeconds">Maximum duration in seconds.</param>
    public ThroughputTimeSeries(int maxSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSeconds);
        _maxSeconds = maxSeconds;
        _bytes = new long[maxSeconds];
        _operations = new long[maxSeconds];
    }

    /// <summary>
    /// Records bytes and operations for a specific second.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Record(int second, long bytes, long operations)
    {
        if ((uint)second < (uint)_maxSeconds)
        {
            _bytes[second] += bytes;
            _operations[second] += operations;
            if (second >= _currentSecond)
            {
                _currentSecond = second + 1;
            }
        }
    }

    /// <summary>
    /// Gets the bytes transferred in a specific second.
    /// </summary>
    public long GetBytes(int second)
    {
        return (uint)second < (uint)_maxSeconds ? _bytes[second] : 0;
    }

    /// <summary>
    /// Gets the operations completed in a specific second.
    /// </summary>
    public long GetOperations(int second)
    {
        return (uint)second < (uint)_maxSeconds ? _operations[second] : 0;
    }

    /// <summary>
    /// Gets the total bytes across all seconds.
    /// </summary>
    public long TotalBytes
    {
        get
        {
            long total = 0;
            for (int i = 0; i < _currentSecond; i++)
            {
                total += _bytes[i];
            }
            return total;
        }
    }

    /// <summary>
    /// Gets the total operations across all seconds.
    /// </summary>
    public long TotalOperations
    {
        get
        {
            long total = 0;
            for (int i = 0; i < _currentSecond; i++)
            {
                total += _operations[i];
            }
            return total;
        }
    }

    /// <summary>
    /// Resets the time series.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_bytes, 0, _currentSecond);
        Array.Clear(_operations, 0, _currentSecond);
        _currentSecond = 0;
    }

    /// <summary>
    /// Creates a snapshot of the time series data.
    /// </summary>
    public TimeSeriesSnapshot CreateSnapshot()
    {
        var samples = new TimeSeriesSample[_currentSecond];
        for (int i = 0; i < _currentSecond; i++)
        {
            samples[i] = new TimeSeriesSample(i, _bytes[i], _operations[i]);
        }
        return new TimeSeriesSnapshot(samples);
    }
}

/// <summary>
/// A single time series sample.
/// </summary>
/// <param name="SecondOffset">Second offset from start.</param>
/// <param name="Bytes">Bytes transferred.</param>
/// <param name="Operations">Operations completed.</param>
public readonly record struct TimeSeriesSample(int SecondOffset, long Bytes, long Operations)
{
    /// <summary>
    /// Throughput in bytes per second.
    /// </summary>
    public double BytesPerSecond => Bytes;

    /// <summary>
    /// Throughput in IOPS.
    /// </summary>
    public double Iops => Operations;
}

/// <summary>
/// Immutable snapshot of time series data.
/// </summary>
public sealed class TimeSeriesSnapshot
{
    /// <summary>
    /// The time series samples.
    /// </summary>
    public IReadOnlyList<TimeSeriesSample> Samples { get; }

    /// <summary>
    /// Creates a new time series snapshot.
    /// </summary>
    public TimeSeriesSnapshot(IReadOnlyList<TimeSeriesSample> samples)
    {
        Samples = samples;
    }

    /// <summary>
    /// Gets the mean bytes per second.
    /// </summary>
    public double MeanBytesPerSecond => Samples.Count > 0
        ? Samples.Sum(s => s.Bytes) / (double)Samples.Count
        : 0;

    /// <summary>
    /// Gets the mean IOPS.
    /// </summary>
    public double MeanIops => Samples.Count > 0
        ? Samples.Sum(s => s.Operations) / (double)Samples.Count
        : 0;

    /// <summary>
    /// Computes standard deviation of bytes per second.
    /// </summary>
    public double StdDevBytesPerSecond
    {
        get
        {
            if (Samples.Count < 2) return 0;
            var mean = MeanBytesPerSecond;
            var sumSquares = Samples.Sum(s => (s.Bytes - mean) * (s.Bytes - mean));
            return Math.Sqrt(sumSquares / (Samples.Count - 1));
        }
    }

    /// <summary>
    /// Computes standard deviation of IOPS.
    /// </summary>
    public double StdDevIops
    {
        get
        {
            if (Samples.Count < 2) return 0;
            var mean = MeanIops;
            var sumSquares = Samples.Sum(s => (s.Operations - mean) * (s.Operations - mean));
            return Math.Sqrt(sumSquares / (Samples.Count - 1));
        }
    }
}
