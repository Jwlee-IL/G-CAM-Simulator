using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>Counting statistics of Compton stripping (TODO-35, DA-4): the stripped count Y = n₆₆₂ − R·n_ref of two
/// independent Poisson windows, Currie's critical level L_C and detection limit L_D in the general form (the "blank" is
/// estimated through the reference window, not a paired blank) and exactly from the two Poisson distributions, a fast
/// exact Poisson sampler for large means, and the gain-shifted counting window.
///
/// Currie (Anal. Chem. 40, 1968, 586), α = β: under H₀ (no Cs) Y has mean μ₁ − R·μ₂ and variance σ₀² = μ₁ + R²·μ₂;
/// L_C = z·σ₀ above that mean; with S Cs counts added to window 1 the variance is σ₀² + S, so L_D = z² + 2·L_C
/// (= 2.706 + 3.29·σ₀ at z = 1.645). The paired-blank 2.71 + 4.65√B is the special case σ₀² = 2B. R's own error is not
/// in σ₀: R is one constant per instrument, so its error is a common bias (the Q3 systematic), not acquisition noise.</summary>
public static class StrippingStatistics
{
    /// <summary>One-sided 95 % normal quantile.</summary>
    public const double Z95 = 1.6448536269514722;

    /// <summary>Normal-approximation Currie limits in counts above the H₀ mean. <paramref name="leak"/> is the Cs-137
    /// count in the reference window per Cs count in window 1 (the side window holds a 3.4 σ photopeak tail, DA-7): it
    /// lowers the net signal to S·(1 − R·leak) and adds R²·leak·S to the variance. Returns L_D as the Cs count S in
    /// window 1 (solved by fixed-point iteration, a contraction for leak·R &lt; 1).</summary>
    public static (double Sigma0, double CriticalLevel, double DetectionLimit) CurrieNormal(double mu1, double mu2, double r,
        double leak = 0, double z = Z95)
    {
        double s0 = Math.Sqrt(Math.Max(0, mu1 + r * r * mu2));
        double lc = z * s0;
        double net = 1 - r * leak;
        double s = (z * z + 2 * lc) / net;
        for (int i = 0; i < 200; i++)
        {
            double next = (lc + z * Math.Sqrt(s0 * s0 + s * (1 + r * r * leak))) / net;
            if (Math.Abs(next - s) < 1e-12 * Math.Max(1, s)) { s = next; break; }
            s = next;
        }
        return (s0, lc, s);
    }

    /// <summary>Exact Currie limits from the two Poisson distributions. Decision: "detected" when Y = n₁ − R·n₂ &gt; Y_C.
    /// Y_C is the smallest support value of Y with P(Y &gt; Y_C | H₀) ≤ α (snapped to the exact double a − R·b that an
    /// acquisition computes, so ties behave as in the Monte Carlo); the actual α is returned. L_D is the smallest Cs count
    /// S in window 1 (with leak·S into window 2) with P(Y &gt; Y_C | S) ≥ 1 − β, by bisection. Uses
    /// P(Y &gt; c) = Σ_b p₂(b)·P(n₁ &gt; c + R·b); both Poisson supports are cut at ±12 σ + 25 (dropped mass &lt; 1e-30).</summary>
    public static ExactLimits CurrieExact(double mu1, double mu2, double r, double leak = 0, double alpha = 0.05, double beta = 0.05)
    {
        if (!(r >= 0)) throw new ArgumentOutOfRangeException(nameof(r));
        var h0 = new TwoPoisson(mu1, mu2, r);
        double lo = h0.MinValue - 1, hi = h0.MaxValue;    // tail(lo) = 1 > α, tail(hi) = 0 ≤ α
        for (int i = 0; i < 400 && hi - lo > 0; i++)
        {
            double mid = 0.5 * (lo + hi);
            if (mid <= lo || mid >= hi) break;
            if (h0.Tail(mid) <= alpha) hi = mid; else lo = mid;
        }
        double yc = h0.SnapToSupport(hi);
        if (h0.Tail(yc) > alpha) throw new InvalidOperationException("Exact critical level failed to snap to a support value.");
        double actualAlpha = h0.Tail(yc);

        double Detect(double s) => new TwoPoisson(mu1 + s, mu2 + leak * s, r).Tail(yc);
        double sLo = 0, sHi = Math.Max(10, 4 * (yc - (mu1 - r * mu2)) + 50);
        while (Detect(sHi) < 1 - beta) sHi *= 2;
        for (int i = 0; i < 200 && sHi - sLo > 1e-9 * Math.Max(1, sHi); i++)
        {
            double mid = 0.5 * (sLo + sHi);
            if (Detect(mid) >= 1 - beta) sHi = mid; else sLo = mid;
        }
        return new ExactLimits(yc, yc - (mu1 - r * mu2), actualAlpha, sHi);
    }

    /// <summary>Y_C: the decision threshold on Y = n₁ − R·n₂ (raw, not net); CriticalLevel = Y_C − (μ₁ − R·μ₂);
    /// DetectionLimit = Cs count in window 1.</summary>
    public sealed record ExactLimits(double Threshold, double CriticalLevel, double ActualAlpha, double DetectionLimit);

    /// <summary>Distribution of Y = n₁ − R·n₂ for independent n₁ ~ Poisson(μ₁), n₂ ~ Poisson(μ₂).</summary>
    private sealed class TwoPoisson
    {
        private readonly int _lo1, _lo2;
        private readonly double[] _sf1;      // P(n₁ ≥ _lo1 + i)
        private readonly double[] _p2;
        private readonly double _r;

        public TwoPoisson(double mu1, double mu2, double r)
        {
            _r = r;
            (_lo1, var p1) = Pmf(mu1);
            (_lo2, _p2) = Pmf(mu2);
            _sf1 = new double[p1.Length + 1];
            for (int i = p1.Length - 1; i >= 0; i--) _sf1[i] = _sf1[i + 1] + p1[i];
        }

        public double MinValue => _lo1 - _r * (_lo2 + _p2.Length - 1);
        public double MaxValue => _lo1 + _sf1.Length - 2 - _r * _lo2;

        /// <summary>P(n₁ ≥ k).</summary>
        private double Sf1(long k)
        {
            long i = k - _lo1;
            if (i <= 0) return 1;
            return i >= _sf1.Length ? 0 : _sf1[i];
        }

        /// <summary>P(Y &gt; c) = Σ_b p₂(b)·P(n₁ ≥ floor(c + R·b) + 1). The 1e-9 guard keeps an exact tie
        /// (c + R·b an integer up to rounding) on the "not greater" side.</summary>
        public double Tail(double c)
        {
            double t = 0;
            for (int j = 0; j < _p2.Length; j++)
            {
                double x = c + _r * (_lo2 + j);
                t += _p2[j] * Sf1((long)Math.Floor(x + 1e-9) + 1);
            }
            return t;
        }

        /// <summary>The support value a − R·b (computed exactly as an acquisition does) closest to v from at or above.</summary>
        public double SnapToSupport(double v)
        {
            double best = double.PositiveInfinity;
            for (int j = 0; j < _p2.Length; j++)
            {
                double b = _lo2 + j;
                double a = Math.Ceiling(v + _r * b - 1e-9);
                foreach (double aa in new[] { a - 1, a, a + 1 })
                {
                    if (aa < 0) continue;
                    double y = aa - _r * b;
                    if (y >= v - 1e-7 && y < best) best = y;
                }
            }
            return best;
        }

        private static (int Lo, double[] P) Pmf(double mu)
        {
            if (!(mu > 0)) return (0, [1.0]);
            double sd = Math.Sqrt(mu);
            int lo = Math.Max(0, (int)Math.Floor(mu - 12 * sd - 25));
            int hi = (int)Math.Ceiling(mu + 12 * sd + 25);
            var p = new double[hi - lo + 1];
            double logMu = Math.Log(mu);
            for (int k = lo; k <= hi; k++) p[k - lo] = Math.Exp(-mu + k * logMu - LogGamma(k + 1.0));
            return (lo, p);
        }
    }

    /// <summary>Exact Poisson draw: Knuth's product (<see cref="Sampling.PoissonExact"/>) below a mean of 10, Hörmann's
    /// transformed rejection with squeeze (PTRS; W. Hörmann, Insurance: Math. Econ. 12 (1993) 39, as in NumPy's
    /// <c>random_poisson_ptrs</c>) from 10 up. Both are exact samplers (no Gaussian branch); PTRS costs O(1) per draw
    /// instead of O(mean), which the stripping counts need (means up to ~6·10⁴).</summary>
    public static int Poisson(IRandom rng, double lambda)
    {
        if (!(lambda >= 10)) return Sampling.PoissonExact(rng, lambda);
        double slam = Math.Sqrt(lambda), loglam = Math.Log(lambda);
        double b = 0.931 + 2.53 * slam;
        double a = -0.059 + 0.02483 * b;
        double invalpha = 1.1239 + 1.1328 / (b - 3.4);
        double vr = 0.9277 - 3.6224 / (b - 2);
        while (true)
        {
            double u = rng.NextDouble() - 0.5;
            double v = rng.NextDouble();
            double us = 0.5 - Math.Abs(u);
            long k = (long)Math.Floor((2 * a / us + b) * u + lambda + 0.43);
            if (us >= 0.07 && v <= vr) return (int)k;
            if (k < 0 || (us < 0.013 && v > us)) continue;
            if (Math.Log(v) + Math.Log(invalpha) - Math.Log(a / (us * us) + b) <= -lambda + k * loglam - LogGamma(k + 1.0))
                return (int)k;
        }
    }

    /// <summary>ln Γ(x) for x ≥ 1 (Stirling series with the shift to x ≥ 7, as NumPy's <c>random_loggam</c>).</summary>
    public static double LogGamma(double x)
    {
        ReadOnlySpan<double> a = [8.333333333333333e-02, -2.777777777777778e-03, 7.936507936507937e-04, -5.952380952380952e-04,
            8.417508417508418e-04, -1.917526917526918e-03, 6.410256410256410e-03, -2.955065359477124e-02,
            1.796443723688307e-01, -1.39243221690590e+00];
        if (x == 1.0 || x == 2.0) return 0;
        double x0 = x;
        int n = 0;
        if (x <= 7.0) { n = (int)(7 - x); x0 = x + n; }
        double x2 = 1.0 / (x0 * x0);
        double gl0 = a[9];
        for (int k = 8; k >= 0; k--) { gl0 *= x2; gl0 += a[k]; }
        double gl = gl0 / x0 + 0.5 * Math.Log(2 * Math.PI) + (x0 - 0.5) * Math.Log(x0) - x0;
        if (x <= 7.0)
            for (int k = 1; k <= n; k++) { gl -= Math.Log(x0 - 1.0); x0 -= 1.0; }
        return gl;
    }

    /// <summary>The counting window seen through a gain error g: the measured pulse height is (1 + g)·(E + noise), so
    /// L ≤ (1 + g)(E + n) ≤ H is the same event set as the deposit-space window [L/(1 + g), H/(1 + g)] with the noise
    /// evaluated at the true deposit — exact for <see cref="CountingWindow.Acceptance"/>, whose σ is a function of the
    /// deposit.</summary>
    public static CountingWindow ForGain(CountingWindow w, double gain, string name)
    {
        if (w.LowKeV is null || w.HighKeV is null) throw new ArgumentException("An open window has no gain dependence.");
        return new CountingWindow(name, w.LowKeV / (1 + gain), w.HighKeV / (1 + gain), w.FwhmFractionAt662);
    }
}
