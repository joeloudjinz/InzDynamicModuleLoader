namespace InzDynamicModuleLoader.Bench.Shared;

public static class Aggregation
{
    public static double Median(IReadOnlyList<double> values) => Percentile(values, 50);

    /// <summary>Linear-interpolation percentile (type-7, as used by NumPy/Excel). p in [0,100].</summary>
    public static double Percentile(IReadOnlyList<double> values, double p)
    {
        if (values.Count == 0) throw new ArgumentException("values must be non-empty", nameof(values));
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 1) return sorted[0];
        var rank = (p / 100.0) * (sorted.Length - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        var frac = rank - lo;
        return sorted[lo] + frac * (sorted[hi] - sorted[lo]);
    }
}
