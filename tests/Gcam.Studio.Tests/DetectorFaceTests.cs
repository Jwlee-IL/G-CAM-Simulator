using Gcam.Studio.Core.Detector;

namespace Gcam.Studio.Tests;

public sealed class DetectorFaceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(.02)]
    [InlineData(.1)]
    [InlineData(.2)]
    public void Geometry_ExactGapCoverageAndArea(double gap)
    {
        var face = DetectorFace.Create(3, .6, gap, Enumerable.Repeat(1d, 9).ToArray());
        Assert.Equal(Math.Pow((.6 - gap) / .6, 2), face.ActiveAreaFraction, 12);
        Assert.Equal(face.SizeMm * face.SizeMm, face.Crystals.Concat(face.Gaps).Sum(r => r.Width * r.Height), 12);
        var first = face.Crystals[0];
        Assert.Equal(gap / 2, first.X, 12); Assert.Equal(gap / 2, first.Y, 12);
        Assert.Equal(.6 - gap, first.Width, 12);
        if (gap == 0) Assert.Empty(face.Gaps);
        else
        {
            Assert.Equal(gap / 2, face.Gaps[0].Width, 12);
            Assert.Contains(face.Gaps, g => Math.Abs(g.Width - gap) < 1e-12);
            foreach (var a in face.Gaps)
            foreach (var b in face.Crystals)
            {
                double width = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
                double height = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
                Assert.True(width <= 1e-12 || height <= 1e-12); // floating-point edge arithmetic only
            }
        }
    }

    [Theory]
    [InlineData(-.1)]
    [InlineData(.6)]
    [InlineData(double.NaN)]
    public void Geometry_RejectsInvalidGap(double gap)
        => Assert.Throws<ArgumentOutOfRangeException>(() => DetectorFace.Create(1, .6, gap, [1]));
}
