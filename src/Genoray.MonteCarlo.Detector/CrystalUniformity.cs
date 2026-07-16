using Genoray.MonteCarlo.Configuration;

namespace Genoray.MonteCarlo.Detector;

/// <summary>
/// Fixed per-crystal non-uniformity of the pixelated scintillator: each crystal has
/// its own gain and energy resolution (FWHM). The per-pixel <see cref="Sensitivity"/>
/// (gain × energy-window acceptance) multiplies the scored signal, distorting the flood
/// map; the same map is what a real system removes via flood correction. Deterministic
/// from <c>UniformitySeed</c> so the "detector" is stable across runs.
/// </summary>
public sealed class CrystalUniformity
{
    public int Width { get; }
    public int Height { get; }

    /// <summary>Per-pixel relative gain (row-major, mean ≈ 1). Light-yield / collection spread.</summary>
    public double[] Gain { get; }

    /// <summary>Per-pixel energy-resolution FWHM (fraction of the photopeak, row-major).</summary>
    public double[] Fwhm { get; }

    /// <summary>Per-pixel relative sensitivity (row-major), mean ≈ 1. Gain × photopeak-window acceptance
    /// with the peak centred in the window (no thermal drift). This is the calibration-time map.</summary>
    public double[] Sensitivity { get; }

    private readonly double _windowFraction;

    public CrystalUniformity(DetectorConfig cfg)
    {
        Width = cfg.PixelsX;
        Height = cfg.PixelsY;
        _windowFraction = cfg.EnergyWindowFraction;
        Gain = new double[Width * Height];
        Fwhm = new double[Width * Height];
        Sensitivity = new double[Width * Height];

        var rng = new Random(cfg.UniformitySeed);
        for (int i = 0; i < Sensitivity.Length; i++)
        {
            int x = i % Width;
            double fx = Width > 1 ? (double)x / (Width - 1) : 0.5;

            // Random (uncorrelated) + structured (gradient across width) gain.
            double gain = (1.0 + cfg.GainSigma * Gaussian(rng))
                        * (1.0 + cfg.GainGradient * (fx - 0.5));
            if (gain < 0.05) gain = 0.05;

            double fwhm = cfg.EnergyResolutionFwhm * (1.0 + cfg.EnergyResolutionFwhmSigma * Gaussian(rng));
            if (fwhm < 0.0) fwhm = 0.0;

            Gain[i] = gain;
            Fwhm[i] = fwhm;
            Sensitivity[i] = gain * PhotopeakAcceptance(fwhm, _windowFraction, 0.0);
        }
    }

    /// <summary>Per-pixel sensitivity when the photopeak centroid has drifted by <paramref name="centroidShift"/>
    /// (relative, e.g. +0.02 = peak 2% high). Row-major, same layout as <see cref="Sensitivity"/>. Used by the
    /// thermal-drift study to build the acquisition-time flood the fixed calibration window actually sees.</summary>
    public double[] SensitivityWithShift(double[] centroidShift)
    {
        var s = new double[Sensitivity.Length];
        for (int i = 0; i < s.Length; i++)
            s[i] = Gain[i] * PhotopeakAcceptance(Fwhm[i], _windowFraction, centroidShift[i]);
        return s;
    }

    /// <summary>Fraction of a Gaussian photopeak (given FWHM) that falls inside the fixed ±window when the peak
    /// centroid is offset from the window centre by <paramref name="centroidShift"/> (relative to the line energy).
    /// Shift 0 reduces to the symmetric erf(w/√2σ); a shift walks the peak out of the window asymmetrically.</summary>
    public static double PhotopeakAcceptance(double fwhmFraction, double windowFraction, double centroidShift)
    {
        if (windowFraction <= 0.0 || fwhmFraction <= 0.0)
            return Math.Abs(centroidShift) <= windowFraction || windowFraction <= 0.0 ? 1.0 : 0.0;
        double sigma = fwhmFraction / 2.355;
        double a = (windowFraction - centroidShift) / (Math.Sqrt(2.0) * sigma);
        double b = (windowFraction + centroidShift) / (Math.Sqrt(2.0) * sigma);
        return 0.5 * (Erf(a) + Erf(b));
    }

    private static double Gaussian(Random r)
    {
        double u1 = 1.0 - r.NextDouble();
        double u2 = r.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    // Abramowitz & Stegun 7.1.26 approximation of erf.
    private static double Erf(double x)
    {
        double t = 1.0 / (1.0 + 0.3275911 * Math.Abs(x));
        double y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t
                           - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return x < 0 ? -y : y;
    }
}
