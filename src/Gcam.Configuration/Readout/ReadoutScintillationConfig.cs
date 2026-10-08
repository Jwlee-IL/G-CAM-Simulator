namespace Gcam.Configuration;

/// <summary>Scintillation photons per interaction: mean N = light yield × deposit, Poisson-distributed (a baseline
/// assumption; a measured Fano-like factor could replace it), with one common multiplicative fluctuation per event of
/// relative σ = intrinsic FWHM / 2.3548 for the crystal's non-proportionality floor. The common factor is shared by every
/// site and sensor of the event, so it moves energy, not position.</summary>
public sealed class ReadoutScintillationConfig
{
    /// <summary>Photons per keV; null = the GAGG(Ce) preset (FrontEndParts, 50 ph/keV).</summary>
    public double? LightYieldPhPerKeV { get; set; }

    /// <summary>Intrinsic FWHM fraction; null = the GAGG(Ce) preset's non-proportionality floor (0.035).</summary>
    public double? IntrinsicResolutionFwhm { get; set; }
}
