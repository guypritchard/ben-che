using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DiskBench.Metrics;

/// <summary>
/// A low-overhead latency histogram using log2 bucketing with sub-buckets.
/// Designed for zero-allocation recording on the hot path.
/// 
/// Bucket structure:
/// - First 64 buckets: linear (0-63 ticks)
/// - Remaining: log2 buckets with 8 sub-buckets each
/// - Covers range from 1 tick to ~2^40 ticks (hours at QPC frequency)
/// </summary>
public sealed class LatencyHistogram
{
    // Linear buckets for very small values (0-63 ticks)
    private const int LinearBuckets = 64;

    // Number of sub-buckets per log2 bucket (power of 2 for fast division)
    private const int SubBucketsPerBucket = 8;
    private const int SubBucketShift = 3; // log2(8)

    // Number of log2 bucket groups (covering 2^6 to 2^40)
    private const int Log2BucketGroups = 34;

    // Total bucket count
    private const int TotalBuckets = LinearBuckets + (Log2BucketGroups * SubBucketsPerBucket);

    private readonly long[] _buckets;
    private long _count;
    private long _sum;
    private long _minTicks;
    private long _maxTicks;

    /// <summary>
    /// Gets the total number of recorded samples.
    /// </summary>
    public long Count => this._count;

    /// <summary>
    /// Gets the sum of all recorded latency values in ticks.
    /// </summary>
    public long SumTicks => this._sum;

    /// <summary>
    /// Gets the minimum recorded latency in ticks.
    /// </summary>
    public long MinTicks => this._count > 0 ? this._minTicks : 0;

    /// <summary>
    /// Gets the maximum recorded latency in ticks.
    /// </summary>
    public long MaxTicks => this._maxTicks;

    /// <summary>
    /// Gets the mean latency in ticks.
    /// </summary>
    public double MeanTicks => this._count > 0 ? (double)this._sum / this._count : 0;

    /// <summary>
    /// Creates a new latency histogram.
    /// </summary>
    public LatencyHistogram()
    {
        _buckets = new long[TotalBuckets];
        _minTicks = long.MaxValue;
    }

    /// <summary>
    /// Records a latency value in ticks.
    /// This method is designed for zero-allocation hot path usage.
    /// </summary>
    /// <param name="ticks">Latency in Stopwatch ticks.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordLatencyTicks(long ticks)
    {
        if (ticks < 0)
        {
            ticks = 0;
        }

        // Update stats (no branching in common path)
        this._count++;
        this._sum += ticks;

        // Branchless min/max update
        if (ticks < this._minTicks)
        {
            this._minTicks = ticks;
        }
        if (ticks > this._maxTicks)
        {
            this._maxTicks = ticks;
        }

        // Get bucket index
        int bucketIndex = GetBucketIndex(ticks);

        // Increment bucket (bounds check eliminated by JIT with constant array size)
        this._buckets[bucketIndex]++;
    }

    /// <summary>
    /// Gets the bucket index for a given tick value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetBucketIndex(long ticks)
    {
        if (ticks < LinearBuckets)
        {
            return (int)ticks;
        }

        // Find the highest set bit position (log2)
        int log2 = 63 - (int)ulong.LeadingZeroCount((ulong)ticks);

        // Bucket group (offset from linear section)
        int bucketGroup = log2 - 6; // 6 = log2(64)

        if (bucketGroup >= Log2BucketGroups)
        {
            return TotalBuckets - 1; // Clamp to last bucket
        }

        // Sub-bucket within the group
        // Extract the bits just below the highest bit
        int shift = log2 - SubBucketShift;
        int subBucket = shift >= 0 ? (int)((ticks >> shift) & (SubBucketsPerBucket - 1)) : 0;

        return LinearBuckets + (bucketGroup * SubBucketsPerBucket) + subBucket;
    }

    /// <summary>
    /// Gets the approximate tick value for a bucket index.
    /// </summary>
    private static long GetBucketValue(int bucketIndex)
    {
        if (bucketIndex < LinearBuckets)
        {
            return bucketIndex;
        }

        int adjusted = bucketIndex - LinearBuckets;
        int bucketGroup = adjusted / SubBucketsPerBucket;
        int subBucket = adjusted % SubBucketsPerBucket;

        // Base value for this bucket group
        long baseValue = 1L << (bucketGroup + 6);
        long subBucketSize = baseValue / SubBucketsPerBucket;

        return baseValue + (subBucket * subBucketSize) + (subBucketSize / 2);
    }

    /// <summary>
    /// Gets the percentile value in ticks.
    /// </summary>
    /// <param name="percentile">Percentile (0.0 to 1.0).</param>
    /// <returns>Latency in ticks at the given percentile.</returns>
    public long GetPercentileTicks(double percentile)
    {
        if (this._count == 0)
        {
            return 0;
        }

        percentile = Math.Clamp(percentile, 0.0, 1.0);
        long targetCount = (long)(percentile * this._count);

        long runningCount = 0;
        for (int i = 0; i < TotalBuckets; i++)
        {
            runningCount += this._buckets[i];
            if (runningCount >= targetCount)
            {
                return GetBucketValue(i);
            }
        }

        return this._maxTicks;
    }

    /// <summary>
    /// Gets a percentile value in a different time unit.
    /// </summary>
    /// <param name="percentile">Percentile (0.0 to 1.0).</param>
    /// <returns>Latency at the given percentile.</returns>
    public double GetPercentile(double percentile)
    {
        return this.GetPercentileTicks(percentile);
    }

    /// <summary>
    /// Resets the histogram to initial state.
    /// </summary>
    public void Reset()
    {
        Array.Clear(this._buckets);
        this._count = 0;
        this._sum = 0;
        this._minTicks = long.MaxValue;
        this._maxTicks = 0;
    }

    /// <summary>
    /// Creates a snapshot of the histogram data.
    /// </summary>
    public HistogramSnapshot CreateSnapshot()
    {
        return new HistogramSnapshot(
            count: this._count,
            sumTicks: this._sum,
            minTicks: this.MinTicks,
            maxTicks: this._maxTicks,
            meanTicks: this.MeanTicks,
            buckets: (long[])this._buckets.Clone());
    }

    /// <summary>
    /// Merges another histogram into this one.
    /// </summary>
    public void Merge(LatencyHistogram other)
    {
        ArgumentNullException.ThrowIfNull(other);
        for (int i = 0; i < TotalBuckets; i++)
        {
            this._buckets[i] += other._buckets[i];
        }
        this._count += other._count;
        this._sum += other._sum;
        if (other._minTicks < this._minTicks)
        {
            this._minTicks = other._minTicks;
        }
        if (other._maxTicks > this._maxTicks)
        {
            this._maxTicks = other._maxTicks;
        }
    }

    /// <summary>
    /// Gets the number of ticks per microsecond for the current system.
    /// </summary>
    public static double TicksPerMicrosecond => Stopwatch.Frequency / 1_000_000.0;

    /// <summary>
    /// Gets the number of ticks per millisecond for the current system.
    /// </summary>
    public static double TicksPerMillisecond => Stopwatch.Frequency / 1_000.0;
}

/// <summary>
/// Immutable snapshot of histogram data.
/// </summary>
public sealed class HistogramSnapshot
{
    /// <summary>Total sample count.</summary>
    public long Count { get; }
    /// <summary>Sum of all samples in ticks.</summary>
    public long SumTicks { get; }
    /// <summary>Minimum value in ticks.</summary>
    public long MinTicks { get; }
    /// <summary>Maximum value in ticks.</summary>
    public long MaxTicks { get; }
    /// <summary>Mean value in ticks.</summary>
    public double MeanTicks { get; }
    /// <summary>Copy of bucket counts.</summary>
    public IReadOnlyList<long> Buckets { get; }

    /// <summary>
    /// Creates a new histogram snapshot.
    /// </summary>
    public HistogramSnapshot(long count, long sumTicks, long minTicks, long maxTicks, double meanTicks, IReadOnlyList<long> buckets)
    {
        Count = count;
        SumTicks = sumTicks;
        MinTicks = minTicks;
        MaxTicks = maxTicks;
        MeanTicks = meanTicks;
        Buckets = buckets;
    }
}
