using DiskBench.Metrics;
using Xunit;

namespace DiskBench.Tests;

/// <summary>
/// Tests for the ThroughputTimeSeries class.
/// </summary>
public class ThroughputTimeSeriesTests
{
    [Fact]
    public void Record_SingleSecond_RecordsCorrectly()
    {
        var ts = new ThroughputTimeSeries(100);

        ts.Record(0, 1000, 10);

        Assert.Equal(1000, ts.GetBytes(0));
        Assert.Equal(10, ts.GetOperations(0));
        Assert.Equal(1, ts.CurrentSecond);
    }

    [Fact]
    public void Record_MultipleSecondsAccumulates()
    {
        var ts = new ThroughputTimeSeries(100);

        ts.Record(0, 1000, 10);
        ts.Record(0, 500, 5);
        ts.Record(1, 2000, 20);

        Assert.Equal(1500, ts.GetBytes(0));
        Assert.Equal(15, ts.GetOperations(0));
        Assert.Equal(2000, ts.GetBytes(1));
        Assert.Equal(20, ts.GetOperations(1));
    }

    [Fact]
    public void TotalBytes_SumsAllSeconds()
    {
        var ts = new ThroughputTimeSeries(100);

        ts.Record(0, 1000, 10);
        ts.Record(1, 2000, 20);
        ts.Record(2, 3000, 30);

        Assert.Equal(6000, ts.TotalBytes);
        Assert.Equal(60, ts.TotalOperations);
    }

    [Fact]
    public void Reset_ClearsAllData()
    {
        var ts = new ThroughputTimeSeries(100);

        ts.Record(0, 1000, 10);
        ts.Record(1, 2000, 20);
        ts.Reset();

        Assert.Equal(0, ts.CurrentSecond);
        Assert.Equal(0, ts.GetBytes(0));
        Assert.Equal(0, ts.GetBytes(1));
    }

    [Fact]
    public void CreateSnapshot_ReturnsCorrectData()
    {
        var ts = new ThroughputTimeSeries(100);

        ts.Record(0, 1000, 10);
        ts.Record(1, 2000, 20);
        ts.Record(2, 3000, 30);

        var snapshot = ts.CreateSnapshot();

        Assert.Equal(3, snapshot.Samples.Count);
        Assert.Equal(1000, snapshot.Samples[0].Bytes);
        Assert.Equal(10, snapshot.Samples[0].Operations);
        Assert.Equal(2000, snapshot.Samples[1].Bytes);
        Assert.Equal(3000, snapshot.Samples[2].Bytes);
    }

    [Fact]
    public void Record_BeyondMaxSeconds_Ignored()
    {
        var ts = new ThroughputTimeSeries(10);

        ts.Record(15, 1000, 10); // Beyond max

        Assert.Equal(0, ts.TotalBytes);
    }

    [Fact]
    public void TimeSeriesSnapshot_CalculatesMeanCorrectly()
    {
        var ts = new ThroughputTimeSeries(100);

        ts.Record(0, 1000, 100);
        ts.Record(1, 2000, 200);
        ts.Record(2, 3000, 300);

        var snapshot = ts.CreateSnapshot();

        Assert.Equal(2000, snapshot.MeanBytesPerSecond);
        Assert.Equal(200, snapshot.MeanIops);
    }

    [Fact]
    public void TimeSeriesSnapshot_CalculatesStdDevCorrectly()
    {
        var ts = new ThroughputTimeSeries(100);

        ts.Record(0, 1000, 10);
        ts.Record(1, 1000, 10);
        ts.Record(2, 1000, 10);

        var snapshot = ts.CreateSnapshot();

        // All same values = 0 std dev
        Assert.Equal(0, snapshot.StdDevBytesPerSecond);
        Assert.Equal(0, snapshot.StdDevIops);
    }
}
