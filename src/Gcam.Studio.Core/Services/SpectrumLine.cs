namespace Gcam.Studio.Core.Services;

/// <summary>A source emission to annotate; never an added histogram count. <paramref name="Kind"/> and
/// <paramref name="XRayOrigin"/> come from the engine's isotope table (gamma unless the table says X-ray).</summary>
public sealed record SpectrumLine(string Isotope, double EnergyKeV,
    Gcam.Configuration.EmissionKind Kind = Gcam.Configuration.EmissionKind.Gamma, string? XRayOrigin = null);
