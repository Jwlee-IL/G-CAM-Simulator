using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Tests;

/// <summary>LUT v2 (<see cref="FloodCalibrationMethod.LatticeGrowth"/>) on floods shaped like the engine's real lattice:
/// under the four-output DPC network the rows stay straight and the edge columns bow inward at mid-height by
/// 0.13 / 0.99 / 1.30 / 2.25 spacings (12 / 30 / 34 / 44 crystals; noise-free expected positions, TODO-41 turn 2).
/// Spots are sampled with counting noise at the recipe's calibration budget (2 000 histories per crystal, ≈ 35 % in the
/// 662 keV window ⇒ 700 events per crystal) over a 10 % uniform continuum.</summary>
public sealed class LatticeGrowthTests
{
    private const int PerCrystal = 700;

    /// <summary>Outermost centres at ±0.93 of the raw plane (measured); the bow shifts a column toward the axis in
    /// proportion to its distance from the axis, maximal at mid-height and zero at the outer rows.</summary>
    private static (double X, double Y) Centre(int n, double bow, int c, int r)
    {
        double s = 1.86 / (n - 1), half = (n - 1) / 2.0;
        double x = (c - half) * s, y = (r - half) * s, edge = half * s;
        return (x - x / edge * bow * s * (1 - y / edge * (y / edge)), y);
    }

    private static List<(double X, double Y)> Flood(int n, double bow, double fwhmSpacings, int seed,
        int missing = -1, int mergedWithRight = -1)
    {
        var rng = new DefaultRandom(seed);
        double s = 1.86 / (n - 1), sigma = fwhmSpacings * s / 2.3548;
        var points = new List<(double, double)>();
        void Spot((double X, double Y) centre, int events)
        {
            for (int e = 0; e < events; e++)
                points.Add((centre.X + sigma * Sampling.Gaussian(rng), centre.Y + sigma * Sampling.Gaussian(rng)));
        }
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
            {
                int k = r * n + c;
                if (k == missing || mergedWithRight >= 0 && k == mergedWithRight + 1) continue;
                var p = Centre(n, bow, c, r);
                if (k == mergedWithRight)
                {
                    var q = Centre(n, bow, c + 1, r);
                    Spot(((p.X + q.X) / 2, (p.Y + q.Y) / 2), 2 * PerCrystal);
                }
                else Spot(p, PerCrystal);
            }
        int continuum = n * n * PerCrystal / 9; // 10 % of all events
        for (int e = 0; e < continuum; e++) points.Add((-0.95 + 1.9 * rng.NextDouble(), -0.95 + 1.9 * rng.NextDouble()));
        return points;
    }

    private static FloodLut Calibrate(List<(double X, double Y)> points, int n) =>
        FloodLut.Calibrate(points, n, n, new FloodCalibrationConfig { Method = FloodCalibrationMethod.LatticeGrowth });

    [Theory]
    [InlineData(12, 0.13, 0.50, 1201)]
    [InlineData(30, 0.99, 0.50, 3001)]
    [InlineData(34, 1.30, 0.58, 3401)]
    public void BowedLatticeWithCountingNoise_IsIdentifiedFromMeasuredMaxima(int n, double bow, double fwhm, int seed)
    {
        var lut = Calibrate(Flood(n, bow, fwhm, seed), n);
        Assert.True(lut.Succeeded, lut.Failure);
        Assert.Equal(1.0 / 8 * 32, lut.SmoothingSigmaBins);
        double s = 1.86 / (n - 1);
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
            {
                var p = Centre(n, bow, c, r);
                Assert.Equal(r * n + c, lut.Lookup(p.X, p.Y));
                // The marker is a measured maximum of this crystal's spot (closer than any neighbour's centre half-way point).
                var m = lut.Peaks[r * n + c];
                Assert.True(Math.Sqrt((m.X - p.X) * (m.X - p.X) + (m.Y - p.Y) * (m.Y - p.Y)) < 0.5 * s);
                int b = FloodLut.Bin(m.X, m.Y, lut.BinsX, lut.BinsY);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int q = b + dy * lut.BinsX + dx;
                        if (q >= 0 && q < lut.Density.Length) Assert.True(lut.Density[b] >= lut.Density[q]);
                    }
            }
    }

    [Theory]
    [InlineData(30, 0.99, 0.50, 3002, 15 * 30, -1)]      // missing edge spot (column 0, mid-height)
    [InlineData(30, 0.99, 0.50, 3003, 0, -1)]            // missing corner spot
    [InlineData(30, 0.99, 0.50, 3004, -1, 15 * 30 + 14)] // two central spots merged into one
    [InlineData(30, 0.99, 0.50, 3005, -1, 3 * 30)]       // an edge-column spot merged with its neighbour
    [InlineData(30, 0.99, 1.20, 3006, -1, -1)]           // physically overlapping spots (FWHM 1.2 spacings)
    public void UnsupportedMergedOrOverlappingSpots_Refuse(int n, double bow, double fwhm, int seed, int missing, int merged)
    {
        var lut = Calibrate(Flood(n, bow, fwhm, seed, missing, merged), n);
        Assert.False(lut.Succeeded);
        Assert.Empty(lut.Peaks);
        Assert.Equal(-1, lut.Lookup(0, 0));
    }

    [Fact]
    public void LegacyDefault_IgnoresLatticeGrowthAndKeepsConfiguredSmoothing()
    {
        var points = Flood(12, 0.13, 0.50, 1202);
        var legacy = FloodLut.Calibrate(points, 12, 12, new FloodCalibrationConfig());
        Assert.Equal(1.0, legacy.SmoothingSigmaBins);
        Assert.Equal(FloodCalibrationMethod.Legacy, new FloodCalibrationConfig().Method);
    }
}
