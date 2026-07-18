using System;
using System.Collections.Generic;
using System.Linq;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>
/// True (cascade) coincidence summing: two gammas from ONE decay both deposit in the crystal, so their energies SUM
/// into one event (Co-60 1173+1332 → 2505). It is RATE-independent and ∝ ε² (geometric, grows as the source nears the
/// camera) — the opposite of random pile-up (∝ rate). Cs-137's single line gives no summing; Na-22's back-to-back
/// 511s can't both reach a one-sided detector.
/// </summary>
public class CascadeSummingTests
{
    // --- DecayScheme: per-decay correlated emission (no MC geometry) ---

    [Fact]
    public void Cs137_IsASingleUncorrelatedLine()
    {
        var scheme = DecayScheme.For(Isotope.Cs137);
        var rng = new DefaultRandom(1);
        var buf = new List<(double e, Vector3 dir)>();
        int emitted = 0;
        for (int i = 0; i < 5000; i++)
        {
            scheme.Sample(rng, buf);
            Assert.True(buf.Count <= 1);                    // never a coincident pair
            if (buf.Count == 1) { Assert.Equal(661.7, buf[0].e, 1); emitted++; }
        }
        Assert.InRange(emitted / 5000.0, 0.80, 0.90);       // branching ≈ 0.851
        Assert.Empty(scheme.SumPeaks);                      // no cascade → nothing to sum
    }

    [Fact]
    public void Co60_EmitsThe1173And1332Cascade()
    {
        var scheme = DecayScheme.For(Isotope.Co60);
        var rng = new DefaultRandom(2);
        var buf = new List<(double e, Vector3 dir)>();
        int both = 0;
        for (int i = 0; i < 3000; i++)
        {
            scheme.Sample(rng, buf);
            if (buf.Count == 2) { both++; Assert.Contains(buf, p => Math.Abs(p.e - 1173.2) < 1); Assert.Contains(buf, p => Math.Abs(p.e - 1332.5) < 1); }
        }
        Assert.True(both > 2900, $"Co-60 should almost always emit both lines, got {both}/3000");
        Assert.Equal(2505.7, scheme.SumPeaks.Single().energyKeV, 1);
    }

    [Fact]
    public void Na22_511sAreEmittedBackToBack()
    {
        var scheme = DecayScheme.For(Isotope.Na22);
        var rng = new DefaultRandom(3);
        var buf = new List<(double e, Vector3 dir)>();
        int pairs = 0;
        for (int i = 0; i < 5000; i++)
        {
            scheme.Sample(rng, buf);
            var anni = buf.Where(p => Math.Abs(p.e - 511.0) < 1).ToArray();
            if (anni.Length == 2)
            {
                pairs++;
                // exactly anti-parallel: dot(d1, d2) ≈ −1
                Assert.Equal(-1.0, anni[0].dir.Dot(anni[1].dir), 6);
            }
        }
        Assert.True(pairs > 4000, $"β+ branch (~90%) should usually give a 511 pair, got {pairs}/5000");
    }

    // --- MC study: ∝ε² scaling and the Cs-137 null ---

    [Fact]
    public void Co60_SumPeakScalesAsEpsilonSquared()
    {
        var study = new CascadeSummingStudy(detHalfWidthMm: 10.0, detHalfHeightMm: 10.0, maskOpenFraction: 0.5);
        double[] dists = [16, 21, 28, 37];
        var (rows, _, _, _) = study.Run(DecayScheme.For(Isotope.Co60), dists, decays: 5_000_000, seed: 777,
                                        spectrumDistanceMm: 16.0);

        // Summing is real and present at close range, and vanishes with distance.
        Assert.True(rows[0].SumPeakPerDecay > 1e-5, $"expected a measurable sum peak near the camera, got {rows[0].SumPeakPerDecay:E2}");
        Assert.True(rows[^1].SumPeakPerDecay < rows[0].SumPeakPerDecay, "summing should fall with distance");

        // The defining signature: the SUM yield scales as the SQUARE of the single-line yield (slope ≈ 2 on log-log).
        var pts = rows.Where(r => r.SumPeakPerDecay > 0 && r.SinglePhotopeakPerDecay > 0).ToArray();
        double slope = LogLogSlope(pts.Select(p => p.SinglePhotopeakPerDecay).ToArray(),
                                   pts.Select(p => p.SumPeakPerDecay).ToArray());
        Assert.InRange(slope, 1.5, 2.8);
    }

    [Fact]
    public void Cs137_ShowsNoCascadeSumming()
    {
        var study = new CascadeSummingStudy(detHalfWidthMm: 10.0, detHalfHeightMm: 10.0, maskOpenFraction: 0.5);
        double[] dists = [16, 24, 36];
        var (rows, _, _, _) = study.Run(DecayScheme.For(Isotope.Cs137), dists, decays: 3_000_000, seed: 777,
                                        spectrumDistanceMm: 16.0);
        Assert.All(rows, r => Assert.Equal(0.0, r.SumPeakPerDecay));   // single line → no coincidence sum
        Assert.All(rows, r => Assert.True(r.SinglePhotopeakPerDecay > 0));  // but the photopeak is there
    }

    private static double LogLogSlope(double[] x, double[] y)
    {
        double[] lx = x.Select(v => Math.Log(v)).ToArray(), ly = y.Select(v => Math.Log(v)).ToArray();
        double mx = lx.Average(), my = ly.Average();
        double sxy = 0, sxx = 0;
        for (int i = 0; i < lx.Length; i++) { sxy += (lx[i] - mx) * (ly[i] - my); sxx += (lx[i] - mx) * (lx[i] - mx); }
        return sxy / sxx;
    }
}
