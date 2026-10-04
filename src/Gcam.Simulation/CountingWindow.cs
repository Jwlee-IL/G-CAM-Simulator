namespace Gcam.Simulation;

/// <summary>Which detected events are counted: all deposits (open) or a pulse-height window on the event's total deposit.
/// The measured pulse height is the deposit smeared by a Gaussian whose FWHM scales as √E (photo-statistics), anchored
/// at <paramref name="FwhmFractionAt662"/> of 661.7 keV (the scenario's <c>EnergyResolutionFwhm</c>; 0 = exact deposit).
/// <see cref="Acceptance"/> is the probability that a deposit lands in the window — the expected count, so response maps
/// built with it carry no extra smearing noise. Source and ambient events go through the same window.</summary>
public sealed record CountingWindow(string Name, double? LowKeV, double? HighKeV, double FwhmFractionAt662)
{
    private const double ReferenceKeV = 661.7;
    private const double FwhmPerSigma = 2.3548200450309493;   // 2·√(2 ln 2)

    public static CountingWindow Open(string name = "open") => new(name, null, null, 0);

    public double Acceptance(double depositKeV)
    {
        if (LowKeV is null || HighKeV is null) return depositKeV > 0 ? 1 : 0;
        if (!(FwhmFractionAt662 > 0)) return depositKeV >= LowKeV && depositKeV <= HighKeV ? 1 : 0;
        double sigma = FwhmFractionAt662 * Math.Sqrt(ReferenceKeV * depositKeV) / FwhmPerSigma;
        return NormalCdf((HighKeV.Value - depositKeV) / sigma) - NormalCdf((LowKeV.Value - depositKeV) / sigma);
    }

    /// <summary>Φ(z) through erfc: the Chebyshev fit <c>erfcc</c> of Numerical Recipes (Press et al., 2nd ed., §6.2),
    /// fractional error &lt; 1.2e-7 everywhere — far below the Monte Carlo noise of any map built from it.</summary>
    public static double NormalCdf(double z) => 0.5 * Erfc(-z / Math.Sqrt(2));

    private static double Erfc(double x)
    {
        double z = Math.Abs(x), t = 1 / (1 + 0.5 * z);
        double r = t * Math.Exp(-z * z - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 + t * (-0.18628806
            + t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 + t * (-0.82215223 + t * 0.17087277)))))))));
        return x >= 0 ? r : 2 - r;
    }
}
