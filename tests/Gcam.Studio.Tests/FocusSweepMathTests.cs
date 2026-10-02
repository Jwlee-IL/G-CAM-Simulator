using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Tests;

public sealed class FocusSweepMathTests
{
    [Fact]
    public void Planes_UniformInverseDistance_ExactBounds()
    {
        var planes = FocusSweepMath.Planes(80);
        Assert.Equal(81, planes.Length); Assert.Equal(110, planes[0]); Assert.Equal(3000, planes[^1]);
        double step = (1 / 3000d - 1 / 110d) / 80;
        for (int i = 1; i < planes.Length; i++) Assert.Equal(step, 1 / planes[i] - 1 / planes[i - 1], 12);
    }
    [Fact]
    public void Interval_InterpolatesRawHalfMaximum_WithoutNoiseInterpretation()
    {
        var track = Curve(0, 2, 6, 2, 0);
        Assert.Equal(300, track.Sharpest.PlaneMm);
        Assert.Equal(225, track.Interval.LoMm); Assert.Equal(375, track.Interval.HiMm);
        Assert.Equal(150, track.Interval.WidthMm); Assert.False(track.MultipleModes);
    }
    [Theory]
    [InlineData(0, 2, 6, 5, 4, false, true)]
    [InlineData(4, 5, 6, 2, 0, true, false)]
    [InlineData(4, 4, 4, 4, 4, true, true)]
    public void Interval_CensorsSweepEdges(double a, double b, double c, double d, double e, bool near, bool far)
    {
        var track = Curve(a, b, c, d, e);
        Assert.Equal(near, track.Interval.NearCensored); Assert.Equal(far, track.Interval.FarCensored);
        Assert.Null(track.Interval.WidthMm);
    }
    [Fact]
    public void Interval_FlagsDisjointModes_AndBoundaryMaximum()
    {
        Assert.True(Curve(0, 6, 0, 5, 0).MultipleModes);
        Assert.True(Curve(6, 4, 2, 1, 0).BoundaryMaximum);
        Assert.True(Curve(0, 1, 2, 4, 6).BoundaryMaximum);
    }
    [Fact]
    public void Linking_UsesAngle_NotMillimetres_AndIsOneToOne()
    {
        FocusSample[] heads = [new(100, 10, 0, 5), new(100, 20, 0, 4)];
        FocusSample[] candidates = [new(1000, 200, 0, 4), new(1000, 100, 0, 5)];
        Assert.Equal(new[] { 1, 0 }, FocusSweepMath.Link(heads, candidates));
    }
    private static FocusTrack Curve(params double[] values) => FocusSweepMath.Describe(values.Select((v, i) =>
        new FocusSample(100 * (i + 1), 0, 0, v)).ToArray());
}
