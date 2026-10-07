using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Tests.Harness;
using Xunit;

namespace Gcam.Tests;

/// <summary>TODO-34 (DR-2): the blind resolved-pair test on synthetic images with known answers, and the angle convention.
/// Tolerances are derived in place.</summary>
public sealed class ResolvedPairTests
{
    private const double Z = 1000;          // source plane (mm from the detector)
    private const double Step = 1.0;        // grid step (mm)

    private static ResolvedPair.GridFrame Frame(int n) => new(n, -(n - 1) / 2.0 * Step, Step, Z);

    // Two separable tents of half-base b grid steps at x = x1, x2 (grid units from the centre), on row y = 0.
    private static double[] Tents(int n, double b, params (double X, double Height)[] peaks)
    {
        var img = new double[n * n];
        int c = n / 2;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                double ty = Math.Max(0, 1 - Math.Abs(y - c) / b), v = 0;
                foreach (var (px, hgt) in peaks) v += hgt * Math.Max(0, 1 - Math.Abs(x - c - px) / b);
                img[y * n + x] = v * ty;
            }
        return img;
    }

    [Theory]
    [InlineData(8, 10)]
    [InlineData(8, 12)]
    [InlineData(8, 14)]
    [InlineData(6, 10)]
    public void TwoEqualTents_SecondPeakProminence_IsDeltaOverWidthMinusOne(int b, int delta)
    {
        // Equal tents of FWHM b (half-base b), b ≤ Δ ≤ 2b: peaks of height 1, the col between them at 2 − Δ/b (the sum is
        // flat there), so the relative prominence above baseline 0 is Δ/b − 1. Entries are sums of two rationals: 1e-12.
        const int n = 61;
        var img = Tents(n, b, (-delta / 2.0, 1), (delta / 2.0, 1));
        var peaks = ResolvedPair.Peaks(img, n);
        Span<int> second = stackalloc int[1];
        int top = ResolvedPair.SecondPeaks(peaks, 0, [1e-9], second);
        int idx2 = second[0];
        var p2 = peaks.Single(p => p.Index == idx2);
        Assert.Equal((double)delta / b - 1, (p2.Value - Math.Max(p2.Saddle, 0)) / p2.Value, 12);
        Assert.NotEqual(top, second[0]);
        // The test with v at the analytic value ± a margin: resolved just below, not just above.
        double rel = (double)delta / b - 1;
        var frame = Frame(n);
        var s1 = frame.Point(n / 2 * n + n / 2 - delta / 2); var s2 = frame.Point(n / 2 * n + n / 2 + delta / 2);
        double radius = ResolvedPair.AngleDeg(Z, 0, 0, delta / 2.0 * Step, 0);   // Δ/2 in degrees
        var res = new bool[2];
        ResolvedPair.Test(img, frame, false, s1, s2, radius * 1.01, [rel - 1e-9, rel + 1e-9], res);
        Assert.True(res[0]);
        Assert.False(res[1]);
    }

    [Fact]
    public void Plateau_GivesOnePeak()
    {
        // A flat square top (equal values) on a cone: the raster tie-break leaves exactly one local maximum.
        const int n = 31;
        var img = new double[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                img[y * n + x] = Math.Min(5.0, 20 - Math.Max(Math.Abs(x - 15), Math.Abs(y - 15)));
        Assert.Single(ResolvedPair.Peaks(img, n));
    }

    [Fact]
    public void Pedestal_DoesNotChangeTheMedianBaselineTest()
    {
        // (P − max(S, b)) / (P − b) is invariant under adding c to every pixel when b is the median (which moves by c).
        // Each difference is exact to ~2 ulp of the values (≤ 1e3): 1e-12 relative on the ratio.
        const int n = 61;
        var img = Tents(n, 8, (-6, 1), (6, 0.7));
        var shifted = img.Select(v => v + 123.0).ToArray();
        double r0 = Rel(img, ResolvedPair.Median(img, n * n)), r1 = Rel(shifted, ResolvedPair.Median(shifted, n * n));
        Assert.Equal(r0, r1, 12);
        static double Rel(double[] im, double b)
        {
            var pk = ResolvedPair.Peaks(im, n);
            Span<int> s = stackalloc int[1];
            ResolvedPair.SecondPeaks(pk, b, [1e-9], s);
            int idx = s[0];
            var p = pk.Single(q => q.Index == idx);
            return (p.Value - Math.Max(p.Saddle, b)) / (p.Value - b);
        }
    }

    [Fact]
    public void Median_IsTheMiddleOrderStatistic()
    {
        var rnd = new Random(5);
        for (int t = 0; t < 50; t++)
        {
            int count = 1 + rnd.Next(300);
            var a = Enumerable.Range(0, count).Select(_ => Math.Round(rnd.NextDouble() * 20)).ToArray();
            var sorted = a.OrderBy(v => v).ToArray();
            Assert.Equal(sorted[count / 2], ResolvedPair.Median(a, count));
        }
    }

    [Fact]
    public void AssignmentRadius_IsCappedAtOneElement()
    {
        // Truth at ±1.5 elements (Δ = 3); image peaks at −0.2 and +1.5 elements. With r = Δ/2 the peak at −0.2 would count
        // for the source at −1.5 (1.3 < 1.5 elements); with the cap r = 1 element it does not. Deterministic.
        const int n = 121;
        var frame = Frame(n);
        double el = 5;                                       // one element = 5 grid steps here
        var img = Tents(n, 3, (-0.2 * el, 1), (1.5 * el, 0.9));
        var s1 = (X: -1.5 * el * Step, Y: 0.0); var s2 = (X: 1.5 * el * Step, Y: 0.0);
        double elemDeg = ResolvedPair.AngleDeg(Z, 0, 0, el * Step, 0);
        var res = new bool[1];
        ResolvedPair.Test(img, frame, false, s1, s2, 1.5 * elemDeg, [0.25], res);
        Assert.True(res[0]);
        ResolvedPair.Test(img, frame, false, s1, s2, Math.Min(1.5, 1.0) * elemDeg, [0.25], res);
        Assert.False(res[0]);
    }

    [Fact]
    public void SingleSource_IsNeverResolved()
    {
        const int n = 61;
        var frame = Frame(n);
        var img = Tents(n, 8, (0, 1));
        var res = new bool[3];
        ResolvedPair.Test(img, frame, true, (-5, 0), (5, 0), 10, [0.01, 0.25, 0.5], res);
        Assert.All(res, Assert.False);
    }

    [Fact]
    public void Fwhm_OfATent_IsItsHalfBaseInAngle()
    {
        // A tent is linear on each flank, so linear interpolation finds the half-height points exactly: ±b/2 around the peak.
        const int n = 61;
        var frame = Frame(n);
        var img = Tents(n, 8, (0, 1));
        var (fx, fy) = ResolvedPair.Fwhm(img, frame, 0);
        double expected = 2 * Math.Atan(4 * Step / Z) * 180 / Math.PI;
        Assert.Equal(expected, fx, 12);
        Assert.Equal(expected, fy, 12);
    }

    [Fact]
    public void ElementAngles_AtOneAndFiveMetres()
    {
        // One element = atan(c / D); a source one element off axis at distance z sits at z·tan(atan(c/D)) = z·c/D.
        var c = ConfigLoader.Load(RepoPaths.Sample("scenario_handheld.json"));
        double elem = AngularResolutionStudy.ElementDeg(c);
        Assert.Equal(Math.Atan(1.0 / 55.0) * 180 / Math.PI, elem, 14);
        foreach (double z in new[] { 1000.0, 5000.0 })
        {
            double x = z * Math.Tan(elem * Math.PI / 180);
            Assert.Equal(z / 55.0, x, 9);
            // AngleDeg is acos of a normalised dot product: its argument carries ≲ 4u relative error, and acos amplifies that
            // by 1 / sin θ, so the angle is good to (180/π)·4u / sin θ degrees.
            double tol = 180 / Math.PI * 4 * Math.Pow(2, -53) / Math.Sin(elem * Math.PI / 180);
            Assert.True(Math.Abs(elem - ResolvedPair.AngleDeg(z, 0, 0, x, 0)) <= tol);
        }
    }

    [Theory]
    [InlineData(10, 1.0)]
    [InlineData(12, 1.0)]
    [InlineData(20, 0.25)]   // Δ ≥ 2b: no overlap, the weak tent stands on the baseline: prominence = its height
    public void SecondPeak_AbsoluteProminence_IsTheAnalyticValue(int delta, double weak)
    {
        // Tents of half-base b = 8 (heights 1 and `weak`), baseline 0. For b ≤ Δ ≤ 2b and equal heights the col is at
        // 2 − Δ/b, so the absolute prominence is 1 − (2 − Δ/b) = Δ/b − 1; for Δ ≥ 2b the col is 0 and the prominence is the
        // height. Rational entries: 1e-12.
        const int n = 81, b = 8;
        var img = Tents(n, b, (-delta / 2.0, 1), (delta / 2.0, weak));
        double expected = delta >= 2 * b ? weak : (double)delta / b - 1;
        var frame = Frame(n);
        var s1 = frame.Point(n / 2 * n + n / 2 - delta / 2); var s2 = frame.Point(n / 2 * n + n / 2 + delta / 2);
        var res = new bool[1]; var prom = new double[1];
        ResolvedPair.Test(img, frame, false, s1, s2, 10, [0.01], res, prom);
        Assert.True(res[0]);
        Assert.Equal(expected, prom[0], 12);
        // The shape-only overload gives the same verdict; the floor only removes passes.
        var res2 = new bool[1];
        ResolvedPair.Test(img, frame, false, s1, s2, 10, [0.01], res2);
        Assert.Equal(res[0], res2[0]);
        Assert.True(ResolvedPair.PassesFloor(res[0], prom[0], expected));
        Assert.False(ResolvedPair.PassesFloor(res[0], prom[0], Math.BitIncrement(expected)));
        Assert.False(ResolvedPair.PassesFloor(false, prom[0], 0));
    }

    [Fact]
    public void SelectFloor_IsTheSmallestFloorWithExceedanceAtMostAlpha()
    {
        // Brute force over the candidate floors {0} ∪ {just above each observed value}: the selected F has exceedance
        // (share of statistics ≥ F) ≤ α, and every smaller candidate exceeds α. Ties and zeros (no second peak) included.
        var rnd = new Random(34);
        foreach (double alpha in new[] { 0.0, 0.01, 0.05, 0.2 })
            for (int t = 0; t < 40; t++)
            {
                int n = 1 + rnd.Next(400);
                var x = Enumerable.Range(0, n).Select(_ => rnd.NextDouble() < 0.3 ? 0 : Math.Round(rnd.NextDouble() * 50) / 10).ToArray();
                double f = ResolvedPair.SelectFloor(x, alpha);
                double Exceed(double floor) => x.Count(v => v > 0 && v >= floor) / (double)n;
                Assert.True(Exceed(f) <= alpha + 1e-12, $"exceedance {Exceed(f)} > {alpha}");
                // Every observed positive value below F, used as a floor, lets more than α through (so F is the smallest).
                foreach (double c in x.Where(v => v > 0 && v < f).Distinct())
                    Assert.True(Exceed(c) > alpha, $"the smaller floor {c} also satisfies α = {alpha}");
            }
    }
}
