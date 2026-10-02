using System;
using System.Collections.Generic;
using System.Linq;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Tests.Harness;
using Xunit;

namespace Gcam.Tests;

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

    [Fact]
    public void Co60_PhotonCountsPerDecay_FollowTheTableBranching()
    {
        // Each line is emitted independently with its Isotopes intensity b: P(2) = b², P(1) = 2b(1−b), P(0) = (1−b)².
        var scheme = DecayScheme.For(Isotope.Co60);
        double b = Isotopes.Get("Co-60").Lines[0].Intensity;
        Assert.Equal(b, Isotopes.Get("Co-60").Lines[1].Intensity);
        var rng = new DefaultRandom(11);
        var buf = new List<(double e, Vector3 dir)>();
        const int n = 400_000;
        var counts = new int[3];
        for (int i = 0; i < n; i++)
        {
            scheme.Sample(rng, buf);
            counts[buf.Count]++;
            Assert.All(buf, p => Assert.Contains(p.e, scheme.SingleLinesKeV));
        }
        double p1 = 2 * b * (1 - b);
        Stat.Within(counts[1] / (double)n, p1, Stat.BinomialSigma(p1, n), 4, "P(one photon)");
        // P(0) = 1e-6: the expected count is 0.4; Poisson(0.4) gives ≥ 5 with probability 6e-5.
        Assert.InRange(counts[0], 0, 4);
        Assert.Equal(scheme.MeanPhotonsPerDecay, Isotopes.Get("Co-60").Lines.Sum(l => l.Intensity), 12);
    }

    [Fact]
    public void Co60_AngularCorrelation_MatchesW()
    {
        // W(c) = 1 + c²/8 + c⁴/24 in Legendre form: A₀ = 1 + 1/24 + 1/120 = 1.05, A₂ = (1/12 + 1/42)/1.05,
        // A₄ = (1/105)/1.05 (c² = (2P₂+1)/3, c⁴ = (8P₄+20P₂+7)/35). E[P_k(c)] = A_k/(2k+1); tolerance 4 standard
        // errors of the sample mean, from the sampled values' own spread.
        double a2 = (1.0 / 12 + 1.0 / 42) / 1.05, a4 = 1.0 / 105 / 1.05;
        Assert.Equal(0.1020, a2, 4);   // the tabulated pure E2–E2 4→2→0 coefficients
        Assert.Equal(0.0091, a4, 4);
        var scheme = DecayScheme.For(Isotope.Co60);
        var rng = new DefaultRandom(12);
        var buf = new List<(double e, Vector3 dir)>();
        var p2 = new List<double>();
        var p4 = new List<double>();
        var bins = new long[10];
        while (p2.Count < 1_000_000)
        {
            scheme.Sample(rng, buf);
            if (buf.Count != 2) continue;
            double c = Math.Clamp(buf[0].dir.Dot(buf[1].dir), -1, 1);
            p2.Add((3 * c * c - 1) / 2);
            p4.Add((35 * c * c * c * c - 30 * c * c + 3) / 8);
            bins[Math.Min(9, (int)((c + 1) / 2 * 10))]++;
        }
        var (m2, s2) = Stat.MeanWithError(p2);
        var (m4, s4) = Stat.MeanWithError(p4);
        Stat.Within(m2, a2 / 5, s2, 4, "E[P2(cos θ)]");
        Stat.Within(m4, a4 / 9, s4, 4, "E[P4(cos θ)]");
        // Histogram against the integral of W: χ² over 10 bins (9 dof) below its 0.9999 quantile 33.7.
        static double G(double c) => c + c * c * c / 24 + Math.Pow(c, 5) / 120;
        double chi2 = 0;
        for (int i = 0; i < 10; i++)
        {
            double expected = (G(-1 + 0.2 * (i + 1)) - G(-1 + 0.2 * i)) / (G(1) - G(-1)) * p2.Count;
            chi2 += (bins[i] - expected) * (bins[i] - expected) / expected;
        }
        Assert.True(chi2 < 33.7, $"χ² = {chi2:F1} over 10 bins");
    }

    [Fact]
    public void Co60Cosine_IsTheInverseCdfOfW()
    {
        static double G(double c) => c + c * c * c / 24 + Math.Pow(c, 5) / 120;
        Assert.Equal(-1.0, DecayScheme.Co60Cosine(0.0), 12);
        Assert.Equal(1.0, DecayScheme.Co60Cosine(1.0), 12);
        double previous = -1;
        for (int i = 1; i < 1000; i++)
        {
            double u = i / 1000.0, c = DecayScheme.Co60Cosine(u);
            Assert.Equal((2 * u - 1) * G(1), G(c), 12);
            Assert.True(c > previous);
            previous = c;
        }
    }

    [Fact]
    public void Na22_DecaySchemeUsesTheIsotopeTable_AndPartnersFollowTheKnownPhoton()
    {
        // ENSDF (Basunia, NDS 127, 69 (2015)): β⁺ 89.96 %, 1274.537 keV 99.940 %; the 511 line is the pair, 2 × β⁺.
        var lines = Isotopes.Get("Na-22").Lines;
        Assert.Equal(2 * 0.8996, lines[0].Intensity, 12);
        Assert.Equal(0.9994, lines[1].Intensity, 12);
        var scheme = DecayScheme.For(Isotope.Na22);
        Assert.Equal(lines.Sum(l => l.Intensity), scheme.MeanPhotonsPerDecay, 12);
        Assert.Equal(3, scheme.MaxPhotonsPerDecay);
        Assert.Null(DecayScheme.Cascade("Cs-137"));
        Assert.Null(DecayScheme.Cascade("Ir-192"));
        Assert.Equal(Isotope.Co60, DecayScheme.Cascade("Co-60")!.Isotope);
        // A biased source aims one photon and draws the rest given it: a 511 → its partner is exactly opposite.
        var rng = new DefaultRandom(13);
        var energies = new List<double> { 1274.5, 511.0, 511.0 };
        var dirs = new Vector3[3];
        var aimed = new Vector3(0.1, -0.2, -1).Normalized();
        for (int known = 0; known < 3; known++)
        {
            scheme.Directions(rng, energies, known, aimed, dirs);
            Assert.Equal(aimed, dirs[known]);
            Assert.Equal(-1.0, dirs[1].Dot(dirs[2]), 12);
        }
    }

    // --- MC study: ∝ε² scaling and the Cs-137 null ---

    [Fact]
    public void Co60_SumPeakScalesAsEpsilonSquared()
    {
        var study = new CascadeSummingStudy(detHalfWidthMm: 10.0, detHalfHeightMm: 10.0, maskOpenFraction: 0.5);
        double[] dists = [16, 21, 28, 37];
        // 2·10⁷ decays per distance: over 24 seeds the fitted slope is 2.05 ± 0.18 (sd), so the range below sits
        // −3.1σ / +4.3σ from the mean. At the former 5·10⁶ the sd was 0.29 (−1.7σ to the lower edge) and this seed's
        // draw fell to 1.45 once the decay sampling changed (W(θ), fixed-draw directions; TODO-14 review).
        var (rows, _, _, _) = study.Run(DecayScheme.For(Isotope.Co60), dists, decays: 20_000_000, seed: 777,
                                        spectrumDistanceMm: 16.0);

        // Summing is real and present at close range, and vanishes with distance.
        Assert.True(rows[0].SumPeakPerDecay > 1e-5, $"expected a measurable sum peak near the camera, got {rows[0].SumPeakPerDecay:E2}");
        Assert.True(rows[^1].SumPeakPerDecay < rows[0].SumPeakPerDecay, "summing should fall with distance");

        // The ∝ε² geometric signature: the SUM yield scales as the SQUARE of the single-line yield (slope ≈ 2 on
        // log-log). (This confirms the geometry scaling; it does not by itself separate cascade from random
        // coincidence — rate-independence does. Here the pair is same-decay by construction.)
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
