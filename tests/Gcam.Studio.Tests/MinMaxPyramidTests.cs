using Gcam.Studio.Core.Plotting;

namespace Gcam.Studio.Tests;

public sealed class MinMaxPyramidTests
{
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 20)]
    [InlineData(7, 20)]
    [InlineData(63, 17)]
    [InlineData(64, 17)]
    [InlineData(65, 17)]
    [InlineData(1000, 17)]
    [InlineData(1023, 31)]
    [InlineData(10000, 133)]
    public void Query_EqualsBruteForce_IncludingBothEnds(int count, int columns)
    {
        var rng = new Random(61);
        double[] data = Enumerable.Range(0, count).Select(_ => rng.NextDouble() * 200 - 100).ToArray();
        if (count > 0) { data[0] = -1000; data[^1] = 1000; }
        var pyramid = new MinMaxPyramid(data);
        for (int trial = 0; trial < 30; trial++)
        {
            double lo = trial == 0 ? 0 : rng.NextDouble() * count;
            double hi = trial == 0 ? count : lo + rng.NextDouble() * (count - lo);
            var query = pyramid.Query(lo, hi, columns);
            Assert.Equal(columns, query.Count);
            for (int c = 0; c < columns; c++)
            {
                int start = (int)Math.Ceiling(lo + (hi - lo) * c / columns);
                int end = (int)Math.Ceiling(lo + (hi - lo) * (c + 1) / columns);
                var range = query[c];
                Assert.Equal(start, range.Start);
                Assert.Equal(end, range.End);
                Assert.Equal(end == start, range.IsEmpty);
                if (end == start) continue;
                var brute = data[start..end];
                Assert.Equal(brute.Min(), range.Min);
                Assert.Equal(brute.Max(), range.Max);
                Assert.True(double.IsFinite(range.Min));
                Assert.True(double.IsFinite(range.Max));
            }
        }
    }

    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(1000)]
    public void Range_EqualsBruteForce_AcrossBlockBoundaries(int count)
    {
        var rng = new Random(64);
        double[] data = Enumerable.Range(0, count).Select(_ => rng.NextDouble()).ToArray();
        var pyramid = new MinMaxPyramid(data);
        for (int start = 0; start < count; start++)
        {
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            for (int end = start + 1; end <= count; end++)
            {
                min = Math.Min(min, data[end - 1]);
                max = Math.Max(max, data[end - 1]);
                Assert.Equal(new PlotRange(start, end, min, max), pyramid.Range(start, end));
            }
        }
    }

    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(1000)]
    [InlineData(10_000_000)]
    public void Storage_UsesAtMostOneSixteenthOfSampleCount(int count)
    {
        var pyramid = new MinMaxPyramid(new double[count]);
        Assert.InRange(pyramid.StoredDoubleCount, 0L, count / 16L);
    }

    [Fact]
    public void Range_ClipsOutsideData_RejectsNonFiniteSamples()
    {
        var pyramid = new MinMaxPyramid([5, -7, 11]);
        Assert.Equal(new PlotRange(0, 3, -7, 11), pyramid.Range(-30, 30));
        Assert.True(pyramid.Range(3, 30).IsEmpty);
        Assert.Throws<ArgumentException>(() => new MinMaxPyramid([double.NaN]));
        Assert.Throws<ArgumentException>(() => new MinMaxPyramid([double.PositiveInfinity]));
    }
}
