using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>Uncollided monoenergetic uniform half-space kernel, not a collided-spectrum generator.
/// Beck, DeCampo and Gogolak, HASL-258 (1972), equations (4)-(5), reprinted in Appendix I of
/// https://lmpublicsearch.lm.doe.gov/sitedocs/sw-a-000582.pdf . Zenith is upward propagation (+y).
/// Backward ray integration gives dPhi/dmu = q/(2*mu_soil)*exp(-mu_air*h/mu), 0 &lt; mu &lt;= 1.
/// Here q = density/1000 * photon yield for one Bq/kg. Soil and air coefficients must be provided
/// from cited material data by the caller; this kernel makes no choice of material or nuclear line data.</summary>
public static class HalfSpaceUncollided
{
    /// <summary>Scalar line fluence, photons/(cm² s) per Bq/kg, integrated over a zenith interval.</summary>
    public static double FluencePerBqKg(double photonYieldPerDecay, double soilDensityGPerCm3,
        double soilMuPerCm, double airMuPerCm, double heightCm, double lowCosine = 0, double highCosine = 1)
    {
        Check(photonYieldPerDecay, soilDensityGPerCm3, soilMuPerCm, airMuPerCm, heightCm);
        if (!double.IsFinite(lowCosine) || !double.IsFinite(highCosine) || lowCosine < 0 || highCosine > 1 || lowCosine >= highCosine)
            throw new ArgumentOutOfRangeException(nameof(lowCosine));
        double prefactor = photonYieldPerDecay * soilDensityGPerCm3 / (2000 * soilMuPerCm);
        double a = airMuPerCm * heightCm;
        if (a == 0) return prefactor * (highCosine - lowCosine);
        double Function(double mu) => mu == 0 ? 0 : Math.Exp(-a / mu);
        double mid = (lowCosine + highCosine) / 2;
        double fa = Function(lowCosine), fm = Function(mid), fb = Function(highCosine);
        double whole = (highCosine - lowCosine) * (fa + 4 * fm + fb) / 6;
        // Deterministic integration accuracy is separate from MC acceptance. 1e-13 absolute angular integral
        // error is far below the statistical errors of the validation ensembles, not a physics tolerance.
        return prefactor * Integrate(Function, lowCosine, highCosine, fa, fm, fb, whole, 1e-13, 30);
    }

    /// <summary>Importance-integrate source positions along soil rays, then analog-sample uncollided air survival.
    /// Every uniform zenith proposal carries q/(2*mu_soil)/N scalar fluence; misses contribute zero.
    /// Thus the per-bin counts have binomial MC errors. This estimates only the uncollided part.</summary>
    public static HalfSpaceUncollidedResult Measure(double photonYieldPerDecay, double soilDensityGPerCm3,
        double soilMuPerCm, double airMuPerCm, double heightCm, int histories, int zenithBins, IRandom random)
    {
        Check(photonYieldPerDecay, soilDensityGPerCm3, soilMuPerCm, airMuPerCm, heightCm);
        ArgumentNullException.ThrowIfNull(random);
        if (histories < 2 || zenithBins < 1) throw new ArgumentOutOfRangeException(nameof(histories));
        var counts = new long[zenithBins];
        for (int i = 0; i < histories; i++)
        {
            double mu = random.NextDouble();
            bool survives = airMuPerCm == 0 || mu > 0 && -Math.Log(1 - random.NextDouble()) >= airMuPerCm * heightCm / mu;
            if (survives) counts[Math.Min(zenithBins - 1, (int)(mu * zenithBins))]++;
        }
        double prefactor = photonYieldPerDecay * soilDensityGPerCm3 / (2000 * soilMuPerCm);
        double p = (double)counts.Sum() / histories;
        return new(histories, counts, prefactor, prefactor * p, prefactor * Math.Sqrt(p * (1 - p) / (histories - 1)));
    }

    private static void Check(double yield, double density, double soilMu, double airMu, double height)
    {
        if (!(yield >= 0 && density > 0 && soilMu > 0 && airMu >= 0 && height >= 0)
            || !double.IsFinite(yield + density + soilMu + airMu + height) || !double.IsFinite(airMu * height))
            throw new ArgumentOutOfRangeException(nameof(yield));
    }

    private static double Integrate(Func<double, double> f, double a, double b, double fa, double fm, double fb,
        double whole, double tolerance, int remaining)
    {
        double mid = (a + b) / 2;
        double leftMid = (a + mid) / 2, rightMid = (mid + b) / 2;
        double fl = f(leftMid), fr = f(rightMid);
        double left = (mid - a) * (fa + 4 * fl + fm) / 6, right = (b - mid) * (fm + 4 * fr + fb) / 6;
        double delta = left + right - whole;
        if (Math.Abs(delta) <= 15 * tolerance) return left + right + delta / 15;
        if (remaining == 0) throw new InvalidOperationException("Half-space deterministic integral did not converge.");
        return Integrate(f, a, mid, fa, fl, fm, left, tolerance / 2, remaining - 1)
            + Integrate(f, mid, b, fm, fr, fb, right, tolerance / 2, remaining - 1);
    }
}
