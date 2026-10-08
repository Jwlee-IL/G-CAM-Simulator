using Gcam.Configuration;
using Gcam.Detector;

namespace Gcam.Tests;

public sealed class FloodTopologyTests
{
    private static readonly double[] X = [-.8, -.5, 0, .5, .8];
    private static readonly double[] Y = [-.75, -.25, .25, .75];
    private static (double X, double Y) Centre(int c, int r) => (X[c] + .025 * Y[r] * Y[r], Y[r] + .035 * X[c] * X[c]);

    private static List<(double X, double Y)> Fixture(int missing = -1, bool continuum = false, bool merge = false)
    {
        var points = new List<(double, double)>();
        void Spot(double x, double y, int height)
        {
            // Deterministic Gaussian raster: no sampling variance in the fixture oracle.
            for (int iy = -5; iy <= 5; iy++)
                for (int ix = -5; ix <= 5; ix++)
                    for (int k = 0; k < (int)(height * Math.Exp(-(ix * ix + iy * iy) / 4.0)); k++)
                        points.Add((x + ix * .006, y + iy * .006));
        }
        for (int r = 0; r < Y.Length; r++)
            for (int c = 0; c < X.Length; c++)
            {
                if (r * X.Length + c == missing || merge && r == 1 && c is 1 or 2) continue;
                var p = Centre(c, r);
                Spot(p.X, p.Y, c == 0 && r == 0 ? 8 : 100);
            }
        if (continuum) Spot(.25, 0, 40); // Taller than the weakest true crystal, between lattice slots.
        if (merge) Spot(-.25, -.25, 180);
        return points;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurvedCompressedUnequalGrid_UsesOnlyMeasuredMaxima(bool continuum)
    {
        var config = new FloodCalibrationConfig { Method = FloodCalibrationMethod.TopologyAware };
        var lut = FloodLut.Calibrate(Fixture(continuum: continuum), X.Length, Y.Length, config);
        Assert.True(lut.Succeeded, lut.Failure);
        for (int r = 0; r < Y.Length; r++)
            for (int c = 0; c < X.Length; c++)
            {
                var p = Centre(c, r);
                Assert.Equal(r * X.Length + c, lut.Lookup(p.X, p.Y));
                var m = lut.Peaks[r * X.Length + c];
                int b = FloodLut.Bin(m.X, m.Y, lut.BinsX, lut.BinsY);
                double value = lut.Density[b];
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++) Assert.True(value >= lut.Density[b + dy * lut.BinsX + dx]);
            }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(7, false)]
    [InlineData(-1, true)]
    public void UnsupportedOrMergedSpots_RefuseWithoutInsertingMarkers(int missing, bool merge)
    {
        var lut = FloodLut.Calibrate(Fixture(missing, merge: merge), X.Length, Y.Length,
            new() { Method = FloodCalibrationMethod.TopologyAware });
        Assert.False(lut.Succeeded);
        Assert.Empty(lut.Peaks);
        Assert.Equal(-1, lut.Lookup(0, 0));
    }
}
