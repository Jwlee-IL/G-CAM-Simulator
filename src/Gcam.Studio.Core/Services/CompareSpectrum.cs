namespace Gcam.Studio.Core.Services;

/// <summary>Shared energy bins, raw counts and explicit overflow; no fitted resolution is implied.</summary>
public sealed record CompareSpectrum(double[] EdgesKeV, double[] Direct, double[] Anger,
    long DirectOverflow, long AngerOverflow, double? CalibrationLineKeV, string? Unavailable);
