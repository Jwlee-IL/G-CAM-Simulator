namespace Gcam.Configuration;

/// <summary>A scintillator, by the datasheet numbers that drive the model: light yield (→ photoelectron budget →
/// resolution), decay time (→ the pulse's leading edge), density (informational / efficiency), and the
/// NON-PROPORTIONALITY resolution floor (the part of the intrinsic resolution that is NOT photon-counting
/// statistics — the FrontEndModel adds the 1/√N_pe statistical term on top, so a good crystal with a poor
/// sensor still resolves badly).</summary>
public sealed record ScintPreset(string Name, double LightYieldPhPerKeV, double DecayNs, double Density,
    double NonPropFwhm);
