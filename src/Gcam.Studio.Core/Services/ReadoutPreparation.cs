using Gcam.Core;

namespace Gcam.Studio.Core.Services;

/// <summary>Resolved experimental preset and independent calibration/validation facts, immutable after publication.</summary>
public sealed record ReadoutPreparation(Guid Id, string Key, bool Succeeded, string? Failure,
    DetectorImage Density, IReadOnlyList<int> Labels, IReadOnlyList<(double X, double Y)> Peaks,
    IReadOnlyList<string> Diagnostics, int Scored, int Triggered, int InWindow, int GainFallback,
    TimeSpan Cost, double RiseNs, double TailNs, double Normalization, double SupportNs, double ThresholdCodes)
{
    public ReadoutCircuit? Circuit { get; init; }
    public double SensorActiveWidthMm { get; init; }
}
