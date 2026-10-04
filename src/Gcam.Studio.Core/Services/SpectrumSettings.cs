namespace Gcam.Studio.Core.Services;

/// <summary>View settings; changing them does not acquire photons.</summary>
public sealed record SpectrumSettings(double WindowFwhm = 1.5, bool PileUp = false)
{
    public DetectorSettings? Detector { get; init; }
    public int PixelsX { get; init; }
    public int PixelsY { get; init; }
    /// <summary>Frozen incident-spectrum maximum, including an absolute field when no source lines are present. It only
    /// admits a source-free (field-only) spectrum; the axis is fixed at 0–2000 keV and does not follow it (AB-16).</summary>
    public double? IncidentMaximumEnergyKeV { get; init; }
}
