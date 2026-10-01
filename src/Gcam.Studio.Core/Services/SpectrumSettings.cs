namespace Gcam.Studio.Core.Services;

/// <summary>View settings; changing them does not acquire photons.</summary>
public sealed record SpectrumSettings(double WindowFwhm = 1.5, bool PileUp = false)
{
    public DetectorSettings? Detector { get; init; }
    public int PixelsX { get; init; }
    public int PixelsY { get; init; }
}
