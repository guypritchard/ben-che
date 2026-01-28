namespace DiskBench.Metrics;

/// <summary>
/// Provides statistical aggregation utilities including bootstrap confidence intervals.
/// </summary>
public static class StatisticalAggregation
{
    /// <summary>
    /// Computes the mean of a set of values.
    /// </summary>
    public static double Mean(ReadOnlySpan<double> values)
    {
        if (values.Length == 0) return 0;
        double sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }
        return sum / values.Length;
    }

    /// <summary>
    /// Computes the sample standard deviation.
    /// </summary>
    public static double StandardDeviation(ReadOnlySpan<double> values)
    {
        if (values.Length < 2) return 0;

        double mean = Mean(values);
        double sumSquares = 0;
        for (int i = 0; i < values.Length; i++)
        {
            double diff = values[i] - mean;
            sumSquares += diff * diff;
        }
        return Math.Sqrt(sumSquares / (values.Length - 1));
    }

    /// <summary>
    /// Computes a bootstrap 95% confidence interval for the mean.
    /// </summary>
    /// <param name="values">Sample values.</param>
    /// <param name="iterations">Number of bootstrap iterations.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>Tuple of (lower bound, upper bound).</returns>
    /// <remarks>
    /// Uses System.Random which is appropriate for statistical bootstrapping.
    /// This is not used for security purposes.
    /// </remarks>
#pragma warning disable CA5394 // Random is appropriate for statistical bootstrapping, not security
    public static (double Lower, double Upper) BootstrapConfidenceInterval(
        ReadOnlySpan<double> values,
        int iterations = 10000,
        int? seed = null)
    {
        if (values.Length < 2)
        {
            double val = values.Length > 0 ? values[0] : 0;
            return (val, val);
        }

        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var bootstrapMeans = new double[iterations];
        var valuesArray = values.ToArray(); // Need array for random access

        for (int i = 0; i < iterations; i++)
        {
            double sum = 0;
            for (int j = 0; j < valuesArray.Length; j++)
            {
                sum += valuesArray[random.Next(valuesArray.Length)];
            }
            bootstrapMeans[i] = sum / valuesArray.Length;
        }

        Array.Sort(bootstrapMeans);

        // 95% CI: 2.5th and 97.5th percentiles
        int lowerIndex = (int)(iterations * 0.025);
        int upperIndex = (int)(iterations * 0.975);

        return (bootstrapMeans[lowerIndex], bootstrapMeans[upperIndex]);
    }
#pragma warning restore CA5394

    /// <summary>
    /// Computes a bootstrap confidence interval from per-second time series samples.
    /// </summary>
    public static (double Lower, double Upper) BootstrapTimeSeriesCI(
        TimeSeriesSnapshot snapshot,
        Func<TimeSeriesSample, double> selector,
        int iterations = 10000,
        int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(selector);

        if (snapshot.Samples.Count < 2)
        {
            double val = snapshot.Samples.Count > 0 ? selector(snapshot.Samples[0]) : 0;
            return (val, val);
        }

        var values = new double[snapshot.Samples.Count];
        for (int i = 0; i < snapshot.Samples.Count; i++)
        {
            values[i] = selector(snapshot.Samples[i]);
        }

        return BootstrapConfidenceInterval(values, iterations, seed);
    }

    /// <summary>
    /// Aggregates multiple trial results.
    /// </summary>
    public static AggregatedStats Aggregate(ReadOnlySpan<double> values, bool computeCI = false, int bootstrapIterations = 10000)
    {
        double mean = Mean(values);
        double stdDev = StandardDeviation(values);

        (double Lower, double Upper)? ci = null;
        if (computeCI && values.Length >= 2)
        {
            ci = BootstrapConfidenceInterval(values, bootstrapIterations, seed: 42);
        }

        return new AggregatedStats(mean, stdDev, ci);
    }
}

/// <summary>
/// Aggregated statistics.
/// </summary>
/// <param name="Mean">Mean value.</param>
/// <param name="StandardDeviation">Standard deviation.</param>
/// <param name="ConfidenceInterval95">Optional 95% confidence interval.</param>
public readonly record struct AggregatedStats(
    double Mean,
    double StandardDeviation,
    (double Lower, double Upper)? ConfidenceInterval95);
