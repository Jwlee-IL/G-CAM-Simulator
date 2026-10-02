using System.Diagnostics;
using Gcam.Studio.Core.Plotting;
using Xunit.Abstractions;

namespace Gcam.Studio.Tests;

public sealed class HistogramPlotTests(ITestOutputHelper output)
{
    [Fact]
    public void Steps_IncludePartialBins_AndEmptyBinHasFullWidth()
    {
        var series = new PlotSeries("counts", [10, 0, 5], Kind: PlotKind.Histogram, BinEdges: [0, 2, 5, 8]);
        series.Validate();
        var points = PlotGeometry.Build(series, new(series.Y), 1, 7, 600);
        Assert.Equal(new PlotPoint[] { new(1, 10), new(2, 10), new(2, 0), new(5, 0), new(5, 5), new(7, 5) }, points);
        Assert.Empty(PlotGeometry.Build(series, new(series.Y), 8, 10, 600));
    }

    [Fact]
    public void Envelope_RetainsExtremaOfBinsCrossingColumns()
    {
        var series = new PlotSeries("counts", [0, 3, 100, 1, 5], Kind: PlotKind.Histogram);
        var points = PlotGeometry.Build(series, new(series.Y), 0, 5, 2);
        Assert.Equal(new PlotPoint[] { new(1.25, 0), new(1.25, 100), new(3.75, 1), new(3.75, 100) }, points);
    }

    [Fact]
    public void BinLookup_UsesHalfOpenEdges_AndReadoutUsesCountsAndUnits()
    {
        var series = new PlotSeries("counts", [1234, 0], Kind: PlotKind.Histogram, BinEdges: [660.9, 663.9, 666.9]);
        Assert.Equal(-1, series.BinAt(660));
        Assert.Equal(0, series.BinAt(660.9));
        Assert.Equal(1, series.BinAt(663.9));
        Assert.Equal(-1, series.BinAt(666.9));
        Assert.Equal(-1, series.BinAt(double.NaN));
        Assert.Equal("662.4 keV (660.9 – 663.9) · 1,234 counts", PlotBinReadout.Format(series, 0, "keV", "counts"));
        var narrow = new PlotSeries("counts", [1], Origin: 0, Step: 0.03, Kind: PlotKind.Histogram);
        Assert.Contains("0.015", PlotBinReadout.Format(narrow, 0, "keV", "counts"));
    }

    [Fact]
    public void HistogramValidation_RejectsMissingEdges_NegativeCounts_AndNonIncreasingEdges()
    {
        Assert.Throws<ArgumentException>(() => new PlotSeries("bad", [1, 2], Kind: PlotKind.Histogram, BinEdges: [0, 1]).Validate());
        Assert.Throws<ArgumentException>(() => new PlotSeries("bad", [1, -1], Kind: PlotKind.Histogram).Validate());
        Assert.Throws<ArgumentException>(() => new PlotSeries("bad", [1, 2], Kind: PlotKind.Histogram, BinEdges: [0, 1, 1]).Validate());
        Assert.Throws<ArgumentException>(() => new PlotSeries("bad", [1], Kind: PlotKind.Histogram, BinEdges: [0, double.NaN]).Validate());
    }

    [Fact]
    public void VisibleExtent_ExcludesDistantPeak_IncludesIntersectingBins()
    {
        var series = new PlotSeries("counts", [1000, 2, 30, 3], Kind: PlotKind.Histogram);
        var range = PlotGeometry.VisibleRange(series, new(series.Y), 1.2, 3);
        Assert.Equal(2, range.Min); Assert.Equal(30, range.Max);
        var linear = PlotAutoScale.Calculate(range.Min, range.Max, true, false);
        Assert.Equal(0, linear.Min); Assert.Equal(32.4, linear.Max, 8);
        Assert.Equal(108, PlotAutoScale.Calculate(0, 50, true, false, 108).Max);
        Assert.Equal(Math.Pow(10, 1.5), PlotAutoScale.Calculate(0, 30, true, true).Max, 8);
        Assert.Equal(100, PlotAutoScale.Calculate(0, 50, true, true, 100).Max);
        Assert.Equal(54, PlotAutoScale.Calculate(0, 50, true, false).Max);
        Assert.True(PlotAutoScale.Calculate(0, 0, true, true).Max > PlotViewport.LogFloor);
    }

    [Fact]
    public void Configure_PreservesZoomAndPanUntilRangeChanges_AndResetStillFits()
    {
        var viewport = new PlotViewport();
        viewport.Configure(0, 1000, 0, 100, false);
        viewport.SetRange(new(600, 720)); viewport.Pan(10);
        viewport.Configure(0, 1000, 0, 200, false);
        Assert.Equal(610, viewport.XMin); Assert.Equal(730, viewport.XMax);
        viewport.Configure(0, 1000, 0, 200, true);
        Assert.Equal(610, viewport.XMin);
        viewport.Reset(); Assert.Equal(0, viewport.XMin); Assert.Equal(1000, viewport.XMax);
        viewport.SetRange(new(600, 720));
        viewport.Configure(0, 2000, 0, 200, true);
        Assert.Equal(0, viewport.XMin); Assert.Equal(2000, viewport.XMax);
        viewport.SetRange(new(-100, 50)); Assert.Equal(0, viewport.XMin); Assert.Equal(150, viewport.XMax);
    }

    [Fact]
    public void Labels_ClampBothEdges_AndUseAdditionalRowsWithoutCollisions()
    {
        var labels = PlotBandLayout.Arrange([(0, 120), (20, 120), (30, 120), (400, 100), (800, 900)], 800, 4);
        Assert.Equal(0, labels[0].Left); Assert.Equal(0, labels[0].Row);
        Assert.Equal(1, labels[1].Row); Assert.Equal(2, labels[2].Row);
        foreach (var label in labels) Assert.InRange(label.Left + label.Width, 0, 800);
        foreach (var a in labels)
            foreach (var b in labels.Where(b => b.Index != a.Index && b.Row == a.Row))
                Assert.True(a.Left + a.Width + 4 <= b.Left || b.Left + b.Width + 4 <= a.Left);
    }

    [Fact]
    public void LineGeometry_PreservesSingletonsAndRightEndpoint()
    {
        var line = new PlotSeries("line", [5, -2, 99], [0, 5, 10]);
        var points = PlotGeometry.Build(line, new(line.Y), 0, 10, 100);
        Assert.Equal(new PlotPoint[] { new(0, 5), new(0, 5), new(5, -2), new(5, -2), new(10, 99), new(10, 99) }, points);
    }

    [Fact]
    public void LineGeometry_LogColumns_KeepNearFieldSamplesSeparate()
    {
        // 81 planes uniform in 1/z from 110 to 3000 mm, as the focus sweep samples them.
        double[] planes = Enumerable.Range(0, 81).Select(i => 1 / (1 / 110.0 - i * (1 / 110.0 - 1 / 3000.0) / 80)).ToArray();
        planes[0] = 110; planes[^1] = 3000;
        var line = new PlotSeries("focus", planes.Select((_, i) => (double)i).ToArray(), planes);
        var linear = PlotGeometry.Build(line, new(line.Y), 110, 3000, 400);
        var log = PlotGeometry.Build(line, new(line.Y), 110, 3000, 400, logX: true);
        // Every sample keeps its own exact X on the log columns; linear columns merge the dense near-field planes.
        Assert.Equal(planes, log.Where((_, i) => i % 2 == 0).Select(p => p.X));
        Assert.True(linear.Count < log.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => PlotGeometry.Build(line, new(line.Y), 0, 3000, 400, logX: true));
    }

    [Fact]
    public void GeometryTiming_TenMillionLineAndHistogram_Headless()
    {
        var samples = new double[PlotSeries.MaximumSamples];
        for (int i = 0; i < samples.Length; i++) samples[i] = i % 997;
        var line = new PlotSeries("ADC", samples);
        var watch = Stopwatch.StartNew();
        var pyramid = new MinMaxPyramid(samples);
        output.WriteLine($"10 M pyramid build: {watch.Elapsed.TotalMilliseconds:F3} ms");
        Measure("10 M line geometry + visible query", line, pyramid, 0, samples.Length - 1, 1200);
        Measure("10 M line zoom geometry + visible query", line, pyramid, 4_000_000, 6_000_000, 1200);
        var histogram = new PlotSeries("counts", samples.Take(256).ToArray(), Step: 6, Kind: PlotKind.Histogram);
        Measure("256-bin histogram steps + visible query", histogram, new(histogram.Y), 0, 1536, 1200);
        Measure("10 M histogram envelope + visible query", line with { Kind = PlotKind.Histogram }, pyramid, 0, samples.Length, 1200);
    }

    private void Measure(string name, PlotSeries series, MinMaxPyramid pyramid, double lo, double hi, int columns)
    {
        for (int i = 0; i < 10; i++) PlotGeometry.Build(series, pyramid, lo, hi, columns);
        var times = new double[100];
        for (int i = 0; i < times.Length; i++)
        {
            long start = Stopwatch.GetTimestamp();
            var range = PlotGeometry.VisibleRange(series, pyramid, lo, hi);
            var points = PlotGeometry.Build(series, pyramid, lo, hi, columns);
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            Assert.False(range.IsEmpty); Assert.InRange(points.Count, 1, columns * 2);
        }
        Array.Sort(times);
        output.WriteLine($"{name}: median={times[50]:F3} ms, p95={times[95]:F3} ms; {columns} columns, 100 iterations (no WPF)");
    }
}
