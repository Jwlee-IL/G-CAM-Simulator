using Gcam.Core;

namespace Gcam.Studio.Core.Services;

/// <summary>Resolved experimental preset and independent calibration/validation facts of ONE calibration line,
/// immutable after publication.</summary>
public sealed record ReadoutPreparation(Guid Id, string Key, bool Succeeded, string? Failure,
    DetectorImage Density, IReadOnlyList<int> Labels, IReadOnlyList<(double X, double Y)> Peaks,
    IReadOnlyList<string> Diagnostics, int Scored, int Triggered, int InWindow, int GainFallback,
    TimeSpan Cost, double RiseNs, double TailNs, double Normalization, double SupportNs, double ThresholdCodes)
{
    public ReadoutCircuit? Circuit { get; init; }
    public double SensorActiveWidthMm { get; init; }

    /// <summary>Calibration line (keV) this entry was prepared for.</summary>
    public double LineKeV { get; init; } = 661.7;

    /// <summary>The resolved policy (status, matched cell, budget, method, solve); null only for hand-built test views.</summary>
    public ReadoutLinePlan? Plan { get; init; }

    /// <summary>Full per-line cache identity (module key, line, budget, method, solve, seed scheme).</summary>
    public string CalibrationKey { get; init; } = "";

    /// <summary>The captured module (crystal and sensor grids); null only for hand-built test views.</summary>
    public ReadoutModuleGeometry? Module { get; init; }

    /// <summary>Declared random streams of this entry, as shown on screen.</summary>
    public string Seeds { get; init; } = "";

    /// <summary>Opt-in flood correction (SD-A4): photopeak-assigned counts per crystal from the calibration's own flood
    /// events, after per-crystal energy calibration (null when the calibration failed). Raw counts are never replaced.</summary>
    public IReadOnlyList<int>? CorrectionHistogram { get; init; }

    /// <summary>True when this result was a same-session cache hit.</summary>
    public bool CacheHit { get; init; }
}
