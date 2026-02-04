using DiskBench.Metrics;
using Xunit;

namespace DiskBench.Tests;

/// <summary>
/// Tests for the LatencyHistogram class.
/// </summary>
public class LatencyHistogramTests
{
    [Fact]
    public void RecordLatencyTicks_SingleValue_RecordsCorrectly()
    {
        var histogram = new LatencyHistogram();

        histogram.RecordLatencyTicks(100);

        Assert.Equal(1, histogram.Count);
        Assert.Equal(100, histogram.SumTicks);
        Assert.Equal(100, histogram.MinTicks);
        Assert.Equal(100, histogram.MaxTicks);
        Assert.Equal(100, histogram.MeanTicks);
    }

    [Fact]
    public void RecordLatencyTicks_MultipleValues_CalculatesStatsCorrectly()
    {
        var histogram = new LatencyHistogram();

        histogram.RecordLatencyTicks(100);
        histogram.RecordLatencyTicks(200);
        histogram.RecordLatencyTicks(300);

        Assert.Equal(3, histogram.Count);
        Assert.Equal(600, histogram.SumTicks);
        Assert.Equal(100, histogram.MinTicks);
        Assert.Equal(300, histogram.MaxTicks);
        Assert.Equal(200, histogram.MeanTicks);
    }

    [Fact]
    public void GetPercentile_Median_ReturnsApproximateMedian()
    {
        var histogram = new LatencyHistogram();

        // Add values from 1 to 100
        for (int i = 1; i <= 100; i++)
        {
            histogram.RecordLatencyTicks(i);
        }

        var p50 = histogram.GetPercentileTicks(0.5);
        
        // Should be approximately 50 (allowing for bucket quantization)
        Assert.InRange(p50, 45, 55);
    }

    [Fact]
    public void GetPercentile_P99_ReturnsHighValue()
    {
        var histogram = new LatencyHistogram();

        for (int i = 1; i <= 1000; i++)
        {
            histogram.RecordLatencyTicks(i);
        }

        var p99 = histogram.GetPercentileTicks(0.99);

        // Should be approximately 990 (allowing for bucket quantization)
        Assert.InRange(p99, 950, 1000);
    }

    [Fact]
    public void GetPercentile_LargeValues_HandlesCorrectly()
    {
        var histogram = new LatencyHistogram();

        // Test with large values (simulating microsecond to millisecond latencies)
        histogram.RecordLatencyTicks(1_000);
        histogram.RecordLatencyTicks(10_000);
        histogram.RecordLatencyTicks(100_000);
        histogram.RecordLatencyTicks(1_000_000);

        Assert.Equal(4, histogram.Count);
        Assert.Equal(1_000, histogram.MinTicks);
        Assert.Equal(1_000_000, histogram.MaxTicks);
    }

    [Fact]
    public void Reset_ClearsAllData()
    {
        var histogram = new LatencyHistogram();

        histogram.RecordLatencyTicks(100);
        histogram.RecordLatencyTicks(200);
        histogram.Reset();

        Assert.Equal(0, histogram.Count);
        Assert.Equal(0, histogram.SumTicks);
        Assert.Equal(0, histogram.MinTicks);
        Assert.Equal(0, histogram.MaxTicks);
    }

    [Fact]
    public void CreateSnapshot_CreatesIndependentCopy()
    {
        var histogram = new LatencyHistogram();
        histogram.RecordLatencyTicks(100);
        histogram.RecordLatencyTicks(200);

        var snapshot = histogram.CreateSnapshot();

        // Modify original
        histogram.RecordLatencyTicks(300);

        // Snapshot should be unchanged
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(300, snapshot.SumTicks);
    }

    [Fact]
    public void Merge_CombinesHistograms()
    {
        var h1 = new LatencyHistogram();
        var h2 = new LatencyHistogram();

        h1.RecordLatencyTicks(100);
        h1.RecordLatencyTicks(200);
        h2.RecordLatencyTicks(50);
        h2.RecordLatencyTicks(300);

        h1.Merge(h2);

        Assert.Equal(4, h1.Count);
        Assert.Equal(650, h1.SumTicks);
        Assert.Equal(50, h1.MinTicks);
        Assert.Equal(300, h1.MaxTicks);
    }

    [Fact]
    public void GetPercentile_EmptyHistogram_ReturnsZero()
    {
        var histogram = new LatencyHistogram();

        Assert.Equal(0, histogram.GetPercentileTicks(0.5));
        Assert.Equal(0, histogram.GetPercentileTicks(0.99));
    }

    [Fact]
    public void RecordLatencyTicks_ZeroValue_HandlesCorrectly()
    {
        var histogram = new LatencyHistogram();

        histogram.RecordLatencyTicks(0);
        histogram.RecordLatencyTicks(0);

        Assert.Equal(2, histogram.Count);
        Assert.Equal(0, histogram.MinTicks);
        Assert.Equal(0, histogram.MaxTicks);
    }

    [Fact]
    public void RecordLatencyTicks_NegativeValue_TreatedAsZero()
    {
        var histogram = new LatencyHistogram();

        histogram.RecordLatencyTicks(-100);

        Assert.Equal(1, histogram.Count);
        Assert.Equal(0, histogram.MinTicks);
    }
}
