using System.Security.Cryptography;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

/// <summary>TODO-35 (DA-10): the closed-form parts of Cs-137-under-Co-60 stripping — the stripped-count variance, the
/// exact Poisson sampler, the exact Currie limits (against brute force, zero background and the Cornish–Fisher expansion),
/// the gain-shifted window, the ratio identities on response maps and the linearity of the stripped statistic. Every
/// tolerance is derived in place; none is borrowed.</summary>
public sealed class CsUnderCoTests
{
    private static double PoissonPmf(double mu, int k) => Math.Exp(-mu + k * Math.Log(mu) - StrippingStatistics.LogGamma(k + 1.0));

    [Fact]
    public void LogGamma_MatchesFactorials()
    {
        // ln Γ(n+1) = ln n!; the Stirling series is accurate to ~1e-15 relative for x ≥ 7 and exact recursion below.
        double fact = 1;
        for (int n = 1; n <= 30; n++)
        {
            fact *= n;
            Assert.Equal(Math.Log(fact), StrippingStatistics.LogGamma(n + 1.0), 10);
        }
    }

    [Theory]
    [InlineData(4.0)]      // Knuth branch
    [InlineData(40.0)]     // PTRS
    [InlineData(4000.0)]   // PTRS, large mean
    public void Poisson_HasPoissonMeanVarianceAndTail(double lambda)
    {
        // M draws. SE(mean) = √(λ/M). SE(sample variance) = λ·√(2/(M−1) + κ/M) with the Poisson excess kurtosis κ = 1/λ.
        // Tail P(X ≥ λ + 2√λ) against the exact PMF sum, SE = √(p(1−p)/M). All at 4 SE.
        const int m = 400_000;
        var rng = DefaultRandom.FromKey(DefaultRandom.Key(3501, (uint)lambda));
        var x = new double[m];
        int cut = (int)Math.Ceiling(lambda + 2 * Math.Sqrt(lambda)), tail = 0;
        for (int i = 0; i < m; i++) { x[i] = StrippingStatistics.Poisson(rng, lambda); if (x[i] >= cut) tail++; }
        var (mean, variance) = Stat.Moments(x);
        Stat.Within(mean, lambda, Math.Sqrt(lambda / m), what: "mean");
        Stat.Within(variance, lambda, lambda * Math.Sqrt(2.0 / (m - 1) + 1.0 / lambda / m), what: "variance");
        double pTail = 1;
        for (int k = 0; k < cut; k++) pTail -= PoissonPmf(lambda, k);
        Stat.Within(tail / (double)m, pTail, Stat.BinomialSigma(pTail, m), what: "upper tail");
    }

    [Fact]
    public void StrippedCount_VarianceIsN662PlusRSquaredNRef()
    {
        // Y = n₁ − R·n₂, independent Poisson: Var Y = μ₁ + R²μ₂. The sample variance of M draws has relative SE
        // √(2/(M−1) + κ/M), κ = (μ₁ + R⁴μ₂)/σ⁴ the excess kurtosis of Y (fourth cumulant over σ⁴); 4 SE.
        const int m = 400_000;
        const double mu1 = 50, mu2 = 80, r = 0.856;
        var rng = DefaultRandom.FromKey(DefaultRandom.Key(3502, 0));
        var y = new double[m];
        for (int i = 0; i < m; i++) y[i] = StrippingStatistics.Poisson(rng, mu1) - r * StrippingStatistics.Poisson(rng, mu2);
        var (mean, variance) = Stat.Moments(y);
        double s2 = mu1 + r * r * mu2, kappa = (mu1 + Math.Pow(r, 4) * mu2) / (s2 * s2);
        Stat.Within(mean, mu1 - r * mu2, Math.Sqrt(s2 / m), what: "mean");
        Stat.Within(variance, s2, s2 * Math.Sqrt(2.0 / (m - 1) + kappa / m), what: "variance");
    }

    [Fact]
    public void CurrieExact_ZeroBackground_IsMinusLnBeta()
    {
        // No background: Y ≡ 0 under H₀, so Y_C = 0 and nothing is a false positive; detection needs n₁ ≥ 1, so
        // 1 − e^(−S) = 1 − β gives S = −ln β = 2.9957 (bisection to 1e-9 relative).
        var e = StrippingStatistics.CurrieExact(0, 0, 0.856);
        Assert.Equal(0, e.Threshold);
        Assert.Equal(0, e.ActualAlpha);
        Assert.Equal(-Math.Log(0.05), e.DetectionLimit, 6);
    }

    [Theory]
    [InlineData(2.3, 3.1, 0.7)]
    [InlineData(0.6, 0.6, 0.856)]
    [InlineData(9.2, 13.1, 0.7003)]
    public void CurrieExact_MatchesBruteForceEnumeration(double mu1, double mu2, double r)
    {
        // Definition by enumeration of every (a, b) pair to 80 counts (dropped mass < 1e-40 at these means): Y_C is the
        // smallest support value with P(Y > Y_C) ≤ α; detection at the returned L_D reaches 1 − β and 1e-4 counts less
        // does not (the bisection tolerance is 1e-9 relative). Values a − R·b equal in exact arithmetic but split by
        // double rounding (R = 0.7: 7 − 0.7·10 = −9e-16) are one support value: keys are merged at 1e-9, the same tie
        // guard the implementation uses. Y_C equal to 1e-9; actual α to 1e-12.
        var e = StrippingStatistics.CurrieExact(mu1, mu2, r);
        var support = new SortedDictionary<double, double>();
        for (int a = 0; a <= 80; a++)
            for (int b = 0; b <= 80; b++)
            {
                double y = Math.Round(a - r * b, 9), p = PoissonPmf(mu1, a) * PoissonPmf(mu2, b);
                support[y] = support.GetValueOrDefault(y) + p;
            }
        double Tail(double c) => support.Where(kv => kv.Key > c).Sum(kv => kv.Value);
        double yc = support.Keys.First(v => Tail(v) <= 0.05);
        Assert.Equal(yc, e.Threshold, 9);
        Assert.Equal(Tail(yc), e.ActualAlpha, 12);
        double Detect(double s)
        {
            double t = 0;
            for (int a = 0; a <= 120; a++)
                for (int b = 0; b <= 80; b++)
                    if (Math.Round(a - r * b, 9) > yc + 1e-10) t += PoissonPmf(mu1 + s, a) * PoissonPmf(mu2, b);
            return t;
        }
        Assert.True(Detect(e.DetectionLimit) >= 0.95 - 1e-12);
        Assert.True(Detect(e.DetectionLimit - 1e-4) < 0.95);
    }

    [Theory]
    [InlineData(551.3, 787.2, 0.7003)]
    [InlineData(5513, 7872, 0.7003)]
    [InlineData(27600, 32250, 0.856)]
    public void CurrieExact_LargeMeans_AgreesWithTheOtherSummationOrder(double mu1, double mu2, double r)
    {
        // Independent code path: P(Y > c) summed over n₁ instead of n₂, P(Y > c) = Σ_a p₁(a)·P(n₂ < (a − c)/R), with its
        // own PMFs. At the implementation's Y_C the tail must be the reported actual α (1e-9 absolute: both sums drop
        // < 1e-30), and at the reported L_D the detection probability must be ≥ 1 − β − 1e-9, and < 1 − β at L_D − 1e-6.
        var e = StrippingStatistics.CurrieExact(mu1, mu2, r);
        double Tail(double m1, double m2, double c)
        {
            double sd1 = Math.Sqrt(m1), sd2 = Math.Sqrt(m2), t = 0;
            int lo1 = Math.Max(0, (int)(m1 - 14 * sd1 - 30)), hi1 = (int)(m1 + 14 * sd1 + 30), hi2 = (int)(m2 + 14 * sd2 + 30);
            var cdf2 = new double[hi2 + 2];
            for (int b = 0; b <= hi2; b++) cdf2[b + 1] = cdf2[b] + PoissonPmf(m2, b);
            for (int a = lo1; a <= hi1; a++)
            {
                // n₂ < (a − c)/R strictly, with the same 1e-9 tie guard: n₂ ≤ ceil((a − c)/R − 1e-9) − 1.
                double x = (a - c) / r;
                int bMax = (int)Math.Ceiling(x - 1e-9) - 1;
                double p2 = bMax < 0 ? 0 : cdf2[Math.Min(bMax, hi2) + 1];
                t += PoissonPmf(m1, a) * p2;
            }
            return t;
        }
        Assert.Equal(e.ActualAlpha, Tail(mu1, mu2, e.Threshold), 9);
        Assert.True(e.ActualAlpha <= 0.05);
        Assert.True(Tail(mu1 + e.DetectionLimit, mu2, e.Threshold) >= 0.95 - 1e-9);
        Assert.True(Tail(mu1 + e.DetectionLimit - 1e-6, mu2, e.Threshold) < 0.95);
    }

    [Theory]
    [InlineData(551.3, 787.2, 0.7003)]
    [InlineData(5513, 7872, 0.7003)]
    [InlineData(27600, 32250, 0.856)]
    public void CurrieExact_ConvergesToCornishFisher(double mu1, double mu2, double r)
    {
        // Large means: the exact quantiles follow the first-order Cornish–Fisher expansion. H₀: Y has σ₀² = μ₁ + R²μ₂,
        // third cumulant κ₃ = μ₁ − R³μ₂; the upper α quantile is m + σ₀(z + (z² − 1)γ₀/6), γ₀ = κ₃/σ₀³. H₁ adds Poisson(S)
        // to n₁: σ₁² = σ₀² + S, κ₃ + S; L_D solves L_C = S − σ₁(z − (z² − 1)γ₁/6).
        // Tolerance, per quantile: (i) three times the next Cornish–Fisher order, σ·|(z³ − 3z)κ₄/(24σ⁴) − (2z³ − 5z)γ²/36|
        // (κ₄ = μ₁ + R⁴μ₂); (ii) the lattice ripple Y carries because n₁ and R·n₂ are lattices whose frequencies nearly
        // align (|φ_Y(t)| = exp(−μ₁(1 − cos t) − μ₂(1 − cos Rt)) has secondary peaks away from t = 0): by the inversion
        // formula the CDF departs from its smooth expansion by at most (1/π)∫_π^T |φ_Y(t)|/t dt, which moves the quantile by
        // that over the density f = φ(z)/σ; T = 2π·2000, and the finer ripple (periods < 1/2000 count) moves a quantile
        // by at most half its period, 2.5e-4.
        double z = StrippingStatistics.Z95;
        var e = StrippingStatistics.CurrieExact(mu1, mu2, r);
        double s0 = Math.Sqrt(mu1 + r * r * mu2), k3 = mu1 - r * r * r * mu2, k4 = mu1 + Math.Pow(r, 4) * mu2;
        double g0 = k3 / Math.Pow(s0, 3);
        double lc = s0 * (z + (z * z - 1) * g0 / 6);
        double s = lc + z * s0;
        for (int i = 0; i < 100; i++)
        {
            double s1 = Math.Sqrt(s0 * s0 + s), g1 = (k3 + s) / Math.Pow(s1, 3);
            s = lc + s1 * (z - (z * z - 1) * g1 / 6);
        }
        double Next(double sig, double kk3, double kk4) =>
            sig * Math.Abs((z * z * z - 3 * z) * kk4 / (24 * Math.Pow(sig, 4)) - (2 * z * z * z - 5 * z) * Math.Pow(kk3 / Math.Pow(sig, 3), 2) / 36);
        double Ripple(double m1, double m2)
        {
            double sig = Math.Sqrt(m1 + r * r * m2), tMax = 2 * Math.PI * 2000, h = 0.1 / sig, integral = 0;
            double Phi(double t) => Math.Exp(-m1 * (1 - Math.Cos(t)) - m2 * (1 - Math.Cos(r * t))) / t;
            double prev = Phi(Math.PI);
            for (double t = Math.PI + h; t <= tMax; t += h) { double cur = Phi(t); integral += 0.5 * h * (prev + cur); prev = cur; }
            double density = Math.Exp(-z * z / 2) / Math.Sqrt(2 * Math.PI) / sig;
            return integral / Math.PI / density + 2.5e-4;
        }
        double sd1 = Math.Sqrt(s0 * s0 + s);
        double tolC = 3 * Next(s0, k3, k4) + Ripple(mu1, mu2);
        double tolD = tolC + 3 * Next(sd1, k3 + s, k4 + s) + Ripple(mu1 + s, mu2);
        Assert.InRange(e.CriticalLevel, lc - tolC, lc + tolC);
        Assert.InRange(e.DetectionLimit, s - tolD, s + tolD);
    }

    [Fact]
    public void ForGain_IsTheWindowOfAScaledPulseHeight()
    {
        // The measured pulse height (1 + g)(E + n), n ~ N(0, σ(E)), falls in [L, H] with probability
        // Acceptance_{[L/(1+g), H/(1+g)]}(E). Checked against M direct Gaussian draws: binomial SE, 4 SE.
        var w = new CountingWindow("side", 727.87, 860.21, 0.07);
        const double g = 0.012, energy = 740;
        var shifted = StrippingStatistics.ForGain(w, g, "side@g");
        double sigma = 0.07 * Math.Sqrt(661.7 * energy) / 2.3548200450309493;
        const int m = 1_000_000;
        var rng = DefaultRandom.FromKey(DefaultRandom.Key(3503, 0));
        int hits = 0;
        for (int i = 0; i < m; i++)
        {
            double pulse = (1 + g) * (energy + sigma * Sampling.Gaussian(rng));
            if (pulse >= 727.87 && pulse <= 860.21) hits++;
        }
        double p = shifted.Acceptance(energy);
        Stat.Within(hits / (double)m, p, Stat.BinomialSigma(p, m), what: "acceptance");
    }

    private static (SimulationConfig Config, CountingWindow[] Windows) Head1m()
    {
        var c = Rigs.Handheld(seed: 3510);
        c.Geometry.SourceMaskDistanceMm = 1000 - c.Geometry.MaskDetectorDistanceMm;
        var f = c.Detector.EnergyResolutionFwhm;
        return (c, [new CountingWindow("cs662", 595.53, 727.87, f), new CountingWindow("co1332", 1199.25, 1465.75, f), new CountingWindow("side", 727.87, 860.21, f)]);
    }

    private static readonly AmbientEvidenceRequest.SourceLine[] CoLines =
        [new() { EnergyKeV = 1173.2, Intensity = 0.999 }, new() { EnergyKeV = 1332.5, Intensity = 0.999 }];

    [Fact]
    public void StrippingRatio_SameMapStripsToZero_IndependentMapsAgreeWithinTheirMonteCarloError()
    {
        // (b) R = D/P from one map strips that map's own total to zero (identity, 1e-12 relative). Two independent maps
        // give R values that differ by their Monte Carlo error: SE(R)/R = √((SE_D/D)² + (SE_P/P)²) (delta method; D and
        // P from one map are mutually exclusive scores of the same trials, covariance −E[d]E[p]/n, negligible against
        // the variances because a photon's window score is ≪ 1); difference within 4 √(SE₁² + SE₂²).
        var (config, windows) = Head1m();
        var a = AmbientEvidence.SourceLines(config, CoLines, 200_000, 3511, 10u, windows);
        var b = AmbientEvidence.SourceLines(config, CoLines, 200_000, 3512, 10u, windows);
        foreach (int rw in new[] { 1, 2 })
        {
            double ra = a.TotalRate(0) / a.TotalRate(rw), rb = b.TotalRate(0) / b.TotalRate(rw);
            Assert.True(Math.Abs(a.TotalRate(0) - ra * a.TotalRate(rw)) <= 1e-12 * a.TotalRate(0));
            double Se(GateMaps m, double r) => r * Math.Sqrt(Math.Pow(m.TotalRateStandardError[0] / m.TotalRate(0), 2)
                + Math.Pow(m.TotalRateStandardError[rw] / m.TotalRate(rw), 2));
            Stat.Within(ra - rb, 0, Math.Sqrt(Math.Pow(Se(a, ra), 2) + Math.Pow(Se(b, rb), 2)), what: $"R difference, window {rw}");
        }
    }

    [Fact]
    public void PerPixelRatios_StripTheirOwnMapToZero_GlobalRatioLeavesAnEdgeHeavyResidual()
    {
        // (c) With Rᵢ = Dᵢ/Pᵢ from the same map every pixel strips to zero, so the stripped reconstruction is zero at every
        // grid point (to 1e-9 of Σ|Dᵢ| — rounding only). With one global R it is not: Rᵢ rises towards the array edge
        // (more escape there: downscatter up, full-energy events down). The edge ring's R over the inner 8 × 8 pixels'
        // R in the co1332 window, from K = 6 independent maps, must exceed 1 by more than 4 standard errors of its mean.
        var (config, windows) = Head1m();
        var search = new CorrelationSearch(config);
        var strip = new StrippedSearch(search);
        int px = config.Detector.PixelsX, n = search.Pixels;
        var map = AmbientEvidence.SourceLines(config, CoLines, 200_000, 3513, 10u, windows);
        var w = new double[n];
        for (int i = 0; i < n; i++)
            w[i] = map.RatePerPixel[1][i] > 0 ? map.RatePerPixel[0][i] - map.RatePerPixel[0][i] / map.RatePerPixel[1][i] * map.RatePerPixel[1][i] : map.RatePerPixel[0][i];
        var recon = new double[strip.PaddedPoints];
        strip.Reconstruct(w, recon);
        double scale = map.RatePerPixel[0].Sum(Math.Abs);
        // Pixels without a reference count keep their downscatter (nothing to strip with): exclude that case explicitly.
        Assert.All(Enumerable.Range(0, n), i => Assert.True(map.RatePerPixel[1][i] > 0, $"pixel {i} has no co1332 counts"));
        Assert.All(recon.Take(search.GridPoints), v => Assert.True(Math.Abs(v) <= 1e-9 * scale));

        var ratios = new List<double>();
        for (int k = 0; k < 6; k++)
        {
            var m = AmbientEvidence.SourceLines(config, CoLines, 200_000, 3520 + k, 10u, windows);
            double eN = 0, eD = 0, cN = 0, cD = 0;
            for (int i = 0; i < n; i++)
            {
                int x = i % px, y = i / px;
                bool edge = x == 0 || y == 0 || x == px - 1 || y == px - 1, centre = x >= 4 && x < px - 4 && y >= 4 && y < px - 4;
                if (edge) { eN += m.RatePerPixel[0][i]; eD += m.RatePerPixel[1][i]; }
                if (centre) { cN += m.RatePerPixel[0][i]; cD += m.RatePerPixel[1][i]; }
            }
            ratios.Add(eN / eD / (cN / cD));
        }
        var (mean, se) = Stat.MeanWithError(ratios);
        Assert.True(mean - 1 > 4 * se, $"edge / centre R = {mean:F3} ± {se:F3}");
    }

    [Fact]
    public void StrippedStatistic_IsLinear_ExpectedNullIsZeroEverywhere()
    {
        // Z_s linearity: for expected (noiseless) Cs-free means λ₁ = Co + field, λ₂ likewise, with Rᵢ from the same Co map
        // and E₀ from the same ambient map, recon(λ₁ − Rᵢλ₂) − E₀ = 0 at every grid point (to 1e-9 of Σ|λ₁|; the decoder
        // is a linear map of the per-pixel weights).
        var (config, windows) = Head1m();
        var search = new CorrelationSearch(config);
        var strip = new StrippedSearch(search);
        int n = search.Pixels;
        var co = AmbientEvidence.SourceLines(config, CoLines, 100_000, 3530, 10u, windows);
        var spectrum = IncidentSpectrumFile.Load(RepoPaths.Sample("ambient/terrestrial-unscear2000-v1.json"),
            "4d48bc4272ce4c25ff6ae9b13ed7583e3c8f467afffdb0225956a70cc435b3f4");
        var amb = GateResponse.Ambient(config, AmbientGeometry.BareCrystalAllFaces, spectrum, 5_000, 3531, windows);
        const double aCo = 1e6, t = 60, field = 0.1;
        var lam = new double[n]; var e0w = new double[n];
        for (int i = 0; i < n; i++)
        {
            double r = co.RatePerPixel[2][i] > 0 ? co.RatePerPixel[0][i] / co.RatePerPixel[2][i] : 0;
            double l1 = t * aCo * co.RatePerPixel[0][i] + field * t * amb.RatePerPixel[0][i];
            double l2 = t * aCo * co.RatePerPixel[2][i] + field * t * amb.RatePerPixel[2][i];
            lam[i] = l1 - r * l2;
            e0w[i] = field * t * (amb.RatePerPixel[0][i] - r * amb.RatePerPixel[2][i]);
        }
        var recon = new double[strip.PaddedPoints]; var e0 = new double[strip.PaddedPoints];
        strip.Reconstruct(lam, recon);
        strip.Reconstruct(e0w, e0);
        double scale = Enumerable.Range(0, n).Sum(i => t * aCo * co.RatePerPixel[0][i] + field * t * amb.RatePerPixel[0][i]);
        for (int k = 0; k < search.GridPoints; k++) Assert.True(Math.Abs(recon[k] - e0[k]) <= 1e-9 * scale);
    }

    [Fact]
    public void Family_SelectionThenValidation_RunsAndReportsProbabilities()
    {
        // Smoke test of the csco family at far-below-evidence budgets: the selection records one Z_s list per null
        // configuration; validation with thresholds from it reports counts and rates that are probabilities, ideal
        // conditions without background, and the exact critical level's actual α ≤ 0.05.
        var q = AmbientEvidenceTests.Request("csco");
        q.SourcePhotons = 5_000;
        q.Windows = [new() { Name = "cs662", LowKeV = 595.53, HighKeV = 727.87 }, new() { Name = "side", LowKeV = 727.87, HighKeV = 860.21 },
                     new() { Name = "co1332", LowKeV = 1199.25, HighKeV = 1465.75 }];
        q.CsUnderCo = new()
        {
            Phase = "selection", Scenario = "samples/scenario_handheld.json", SourceDetectorMm = 1000,
            CsLines = [new() { EnergyKeV = 661.7, Intensity = 0.851 }], CoLines = CoLines, CsWindow = "cs662",
            ReferenceWindows = ["side", "co1332"], GainShifts = [0.01],
            Scenes = [new() { Name = "coloc", CsElements = [0, 0], CoElements = [0, 0] }, new() { Name = "sep2", CsElements = [-1, 0], CoElements = [1, 0] }],
            DirectionsElements = [[2.5, 0]], CoActivitiesBq = [0, 1e6], CoDoseRatesMicroSvPerHour = [], FrontOnlyMaxCoBq = 0,
            CountMultiples = [1], CountRepeats = 20, ImagingCounts = [250], ImagingRepeats = 3, NullRepeats = 8,
            DeltaR = [0.05], ResidualRepeats = 3, CalibrationPhotons = 5_000
        };
        var selection = AmbientEvidenceTests.Json(CsUnderCoStudy.Run(q));
        var nulls = selection.GetProperty("Nulls");
        // 2 scenes × 1 live time × (ideal + bare at both Co levels + front-only at Co = 0) × … = 2 × (2 + 2 + 1) = 10.
        Assert.Equal(10, nulls.EnumerateObject().Count());
        Assert.All(nulls.EnumerateObject(), p => Assert.Equal(8, p.Value.GetArrayLength()));
        // Ideal, Co = 0: no counts, so no candidate.
        Assert.All(nulls.GetProperty("coloc|t=10|ideal|Co=0").EnumerateArray(), v => Assert.Equal(JsonValueKind.Null, v.ValueKind));

        var per = nulls.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.Number).Select(v => v.GetDouble()).DefaultIfEmpty(-1e300).Max() + 1e-4);
        string file = Path.Combine(Path.GetTempPath(), $"gcam-csco-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, JsonSerializer.Serialize(new { PerConfiguration = per, Universal = per.Values.Max(), Comparison = "RoundedAtLeast" }));
        try
        {
            q.Seed = 4102;
            q.CsUnderCo.Phase = "validation";
            q.CsUnderCo.Thresholds = new() { File = file, Sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))) };
            var v = AmbientEvidenceTests.Json(CsUnderCoStudy.Run(q));
            foreach (var c in v.GetProperty("Counts").EnumerateObject())
            {
                Assert.InRange(c.Value.GetProperty("ActualAlpha").GetDouble(), 0, 0.05);
                if (c.Name.Contains("|ideal|")) Assert.Equal(0, c.Value.GetProperty("Model662").GetDouble());
                foreach (var m in c.Value.GetProperty("Multiples").EnumerateArray())
                    foreach (var name in new[] { "DetectedExact", "DetectedPlugIn", "DetectedCalibrated" })
                        Assert.InRange(m.GetProperty(name).GetInt32(), 0, 20);
            }
            foreach (var c in v.GetProperty("Imaging").EnumerateObject())
                Assert.InRange(c.Value.GetProperty("ZCorrect").GetInt32(), 0, c.Value.GetProperty("ZTrusted").GetInt32());
            Assert.True(v.GetProperty("Residual").EnumerateObject().Any());
            double dose = v.GetProperty("CoDoseMicroSvPerHourPerBq").GetDouble();
            // Co-60 at 1 m: Σ (0.999/(4π·100²)) H*/Φ(E) ≈ 0.35 µSv/h per MBq (ICRP 74, unattenuated) — checked to the table.
            double expected = CoLines.Sum(l => l.Intensity / (4 * Math.PI * 1e4) * AmbientDose.PerFluence(l.EnergyKeV)) * 3600 * 1e-6;
            Assert.Equal(expected, dose, 15);
        }
        finally { File.Delete(file); }
    }
}
