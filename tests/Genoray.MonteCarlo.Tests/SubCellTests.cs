using System;
using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Decoding;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>
/// Sub-cell peak interpolation: the bare argmax quantizes the source estimate to the recon-grid step (RMS = step/√12,
/// independent of counts); fitting the correlation-peak shape recovers a fractional offset and beats that floor. The
/// coded-aperture peak is a TENT (mask-cell autocorrelation), so the tent estimator is exact for an ideal tent while
/// the parabola is biased toward the cell; the MC study confirms tent is the most robust across recon-step regimes.
/// </summary>
public class SubCellTests
{
    // A 3×3 recon whose centre row carries a tent of slope 1 with apex at sub-cell offset u (peak at bx=1). The
    // centre column's y-neighbours are equal so the y estimate is 0 — isolating the x axis.
    private static DetectorImage TentImage(double u, double slope = 1.0, double peak = 10.0)
    {
        var img = new DetectorImage(3, 3);
        double f0 = peak - slope * Math.Abs(u); // apex sample (distance |u| from bx)
        double fm = peak - slope * (1.0 + u);  // lower neighbour (distance 1+u)
        double fp = peak - slope * (1.0 - u);  // upper neighbour (distance 1-u)
        double side = Math.Min(fm, fp) - 1.0;  // rows above/below: lower, and equal → dy = 0
        for (int x = 0; x < 3; x++) { img[x, 0] = side; img[x, 2] = side; }
        img[0, 1] = fm; img[1, 1] = f0; img[2, 1] = fp;
        return img;
    }

    // --- Estimator math (no MC): the tent estimator is exact for a tent; parabola is biased; Gaussian recovers a Gaussian ---

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.35)]
    [InlineData(-0.3)]
    public void Tent_IsExactForAnIdealTent(double u)
    {
        var (dx, dy) = PeakInterpolation.Estimate(TentImage(u), 1, 1, SubCellMethod.Tent);
        Assert.Equal(u, dx, 6);
        Assert.Equal(0.0, dy, 6);
    }

    [Fact]
    public void Parabolic_IsBiasedTowardTheCellForATent()
    {
        // For a tent of offset u the parabola returns ≈ u/(2(1−u)) < u — it under-shoots toward the integer cell.
        const double u = 0.3;
        double dx = PeakInterpolation.Estimate(TentImage(u), 1, 1, SubCellMethod.Parabolic).dx;
        Assert.True(dx < u, $"parabola should under-shoot the tent apex: {dx:F4} vs u={u}");
        Assert.Equal(u / (2.0 * (1.0 - u)), dx, 4);
    }

    [Fact]
    public void Gaussian_IsExactForAGaussianPeak()
    {
        // Log of a Gaussian is an exact parabola, so the log-parabola estimator recovers the apex exactly.
        const double u = 0.25, sigma = 1.1, amp = 100.0;
        double S(double d) => amp * Math.Exp(-(d - u) * (d - u) / (2.0 * sigma * sigma));
        var img = new DetectorImage(3, 3);
        double side = Math.Min(S(-1), S(1)) - 1.0;
        for (int x = 0; x < 3; x++) { img[x, 0] = side; img[x, 2] = side; }
        img[0, 1] = S(-1); img[1, 1] = S(0); img[2, 1] = S(1);
        double dx = PeakInterpolation.Estimate(img, 1, 1, SubCellMethod.Gaussian).dx;
        Assert.Equal(u, dx, 4);
    }

    [Fact]
    public void None_AndGridEdges_ReturnZero()
    {
        var img = TentImage(0.3);
        Assert.Equal((0.0, 0.0), PeakInterpolation.Estimate(img, 1, 1, SubCellMethod.None));
        // A peak on the grid edge has no neighbour to fit on that axis → no shift.
        Assert.Equal(0.0, PeakInterpolation.Estimate(img, 0, 1, SubCellMethod.Tent).dx, 12);
    }

    [Fact]
    public void Gaussian_FallsBackToParabolaWhenANeighbourIsNonPositive()
    {
        // The ±1 decoding array drives neighbours negative near the peak sidelobes; the log is undefined there, so
        // Gaussian must fall back to the plain parabola (not throw / NaN).
        var img = new DetectorImage(3, 3);
        for (int x = 0; x < 3; x++) { img[x, 0] = -5; img[x, 2] = -5; }
        img[0, 1] = -2; img[1, 1] = 10; img[2, 1] = 3;   // fm < 0
        double g = PeakInterpolation.Estimate(img, 1, 1, SubCellMethod.Gaussian).dx;
        double p = PeakInterpolation.Estimate(img, 1, 1, SubCellMethod.Parabolic).dx;
        Assert.False(double.IsNaN(g));
        Assert.Equal(p, g, 12);
    }

    // --- MC study: interpolation beats the argmax quantization floor; tent is the matched estimator ---

    [Fact]
    public void Interpolation_BeatsTheQuantizationFloor()
    {
        var cfg = new SimulationConfig { Seed = 20260718 };
        // Coarse recon step (1.8 mm) — the regime where the grid is kept coarse for speed and interpolation earns
        // its keep. Sweep a point source across ~2 cells and decode one noise-free mean image per position.
        var (_, s) = new SubCellStudy(new DefaultSimulationFactory())
            .Run(cfg, reconStepMm: 1.8, sweepHalfWidthMm: 2.25, samples: 25, sourceYMm: 0.0, photonCount: 500_000);

        // The raw argmax sits on the quantization floor (RMS ≈ step/√12).
        Assert.InRange(s.RmsNoneMm / s.QuantFloorMm, 0.7, 1.5);

        // Every interpolator beats the raw argmax, and the tent estimator beats the quantization floor by a wide
        // margin (≈4× here) — a precision the argmax cannot reach at any count.
        Assert.True(s.RmsParabolicMm < s.RmsNoneMm, $"parabolic {s.RmsParabolicMm:F3} !< none {s.RmsNoneMm:F3}");
        Assert.True(s.RmsTentMm < s.RmsNoneMm * 0.5, $"tent {s.RmsTentMm:F3} should halve none {s.RmsNoneMm:F3}");
        Assert.True(s.RmsGaussianMm < s.RmsNoneMm, $"gaussian {s.RmsGaussianMm:F3} !< none {s.RmsNoneMm:F3}");
        Assert.True(s.RmsTentMm < s.QuantFloorMm, $"tent {s.RmsTentMm:F3} should beat the floor {s.QuantFloorMm:F3}");
    }
}
