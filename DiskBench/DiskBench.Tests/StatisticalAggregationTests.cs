using DiskBench.Metrics;
using Xunit;

namespace DiskBench.Tests;

/// <summary>
/// Tests for statistical aggregation functions.
/// </summary>
public class StatisticalAggregationTests
{
    [Fact]
    public void Mean_EmptyArray_ReturnsZero()
    {
        var result = StatisticalAggregation.Mean(ReadOnlySpan<double>.Empty);
        Assert.Equal(0, result);
    }

    [Fact]
    public void Mean_SingleValue_ReturnsThatValue()
    {
        double[] values = [42.0];
        var result = StatisticalAggregation.Mean(values);
        Assert.Equal(42.0, result);
    }

    [Fact]
    public void Mean_MultipleValues_ReturnsCorrectMean()
    {
        double[] values = [1.0, 2.0, 3.0, 4.0, 5.0];
        var result = StatisticalAggregation.Mean(values);
        Assert.Equal(3.0, result);
    }

    [Fact]
    public void StandardDeviation_SingleValue_ReturnsZero()
    {
        double[] values = [42.0];
        var result = StatisticalAggregation.StandardDeviation(values);
        Assert.Equal(0, result);
    }

    [Fact]
    public void StandardDeviation_IdenticalValues_ReturnsZero()
    {
        double[] values = [5.0, 5.0, 5.0, 5.0];
        var result = StatisticalAggregation.StandardDeviation(values);
        Assert.Equal(0, result);
    }

    [Fact]
    public void StandardDeviation_KnownValues_ReturnsCorrectResult()
    {
        // Using values with known standard deviation
        double[] values = [2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0];
        var result = StatisticalAggregation.StandardDeviation(values);
        
        // Population std dev is 2, sample std dev is ~2.138
        Assert.InRange(result, 2.1, 2.2);
    }

    [Fact]
    public void BootstrapConfidenceInterval_SingleValue_ReturnsSameValue()
    {
        double[] values = [100.0];
        var (lower, upper) = StatisticalAggregation.BootstrapConfidenceInterval(values, 1000, 42);
        
        Assert.Equal(100.0, lower);
        Assert.Equal(100.0, upper);
    }

    [Fact]
    public void BootstrapConfidenceInterval_IdenticalValues_ReturnsNarrowInterval()
    {
        double[] values = [100.0, 100.0, 100.0, 100.0, 100.0];
        var (lower, upper) = StatisticalAggregation.BootstrapConfidenceInterval(values, 1000, 42);
        
        Assert.Equal(100.0, lower);
        Assert.Equal(100.0, upper);
    }

    [Fact]
    public void BootstrapConfidenceInterval_VariedValues_ReturnsReasonableInterval()
    {
        double[] values = [90.0, 95.0, 100.0, 105.0, 110.0];
        var (lower, upper) = StatisticalAggregation.BootstrapConfidenceInterval(values, 10000, 42);
        
        // Mean is 100, CI should contain the mean
        Assert.True(lower <= 100);
        Assert.True(upper >= 100);
        Assert.True(lower < upper);
    }

    [Fact]
    public void BootstrapConfidenceInterval_Deterministic_WithSameSeed()
    {
        double[] values = [90.0, 95.0, 100.0, 105.0, 110.0];
        
        var (lower1, upper1) = StatisticalAggregation.BootstrapConfidenceInterval(values, 1000, 42);
        var (lower2, upper2) = StatisticalAggregation.BootstrapConfidenceInterval(values, 1000, 42);
        
        Assert.Equal(lower1, lower2);
        Assert.Equal(upper1, upper2);
    }

    [Fact]
    public void Aggregate_ComputesAllStats()
    {
        double[] values = [100.0, 110.0, 90.0, 105.0, 95.0];
        
        var stats = StatisticalAggregation.Aggregate(values, computeCI: true, bootstrapIterations: 1000);
        
        Assert.Equal(100.0, stats.Mean);
        Assert.True(stats.StandardDeviation > 0);
        Assert.NotNull(stats.ConfidenceInterval95);
        Assert.True(stats.ConfidenceInterval95.Value.Lower <= stats.Mean);
        Assert.True(stats.ConfidenceInterval95.Value.Upper >= stats.Mean);
    }

    [Fact]
    public void Aggregate_WithoutCI_DoesNotComputeCI()
    {
        double[] values = [100.0, 110.0, 90.0];
        
        var stats = StatisticalAggregation.Aggregate(values, computeCI: false);
        
        Assert.Null(stats.ConfidenceInterval95);
    }
}
