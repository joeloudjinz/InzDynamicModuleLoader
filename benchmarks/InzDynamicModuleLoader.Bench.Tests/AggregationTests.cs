using InzDynamicModuleLoader.Bench.Shared;

namespace InzDynamicModuleLoader.Bench.Tests;

public class AggregationTests
{
    [Fact]
    public void Median_OddCount_ReturnsMiddle()
        => Assert.Equal(3.0, Aggregation.Median([5, 1, 3, 2, 4]), 6);

    [Fact]
    public void Median_EvenCount_ReturnsAverageOfMiddleTwo()
        => Assert.Equal(2.5, Aggregation.Median([1, 2, 3, 4]), 6);

    [Fact]
    public void Percentile_P25AndP75_MatchLinearInterpolation()
    {
        double[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        Assert.Equal(3.25, Aggregation.Percentile(data, 25), 6);
        Assert.Equal(7.75, Aggregation.Percentile(data, 75), 6);
    }

    [Fact]
    public void Median_Empty_Throws()
        => Assert.Throws<ArgumentException>(() => Aggregation.Median([]));
}
