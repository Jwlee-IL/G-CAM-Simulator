namespace Gcam.Studio.Core.Services;

/// <summary>View settings; changing them does not acquire photons.</summary>
public sealed record SpectrumSettings(double WindowFwhm = 1.5, bool PileUp = false);
