namespace Gcam.Studio.Core.Services;

/// <summary>Confirmed crystal-level diagnostics of one cell at one rate (32 seeds): paired G per axis (rad²),
/// single-crystal photopeak mis-identification, multi-crystal share and the weakest / median sensitivity ratio.
/// Diagnostics only — they never gate an offer.</summary>
public sealed record ReadoutCrystalDiagnostics(double RateCps, bool Available, ReadoutInterval? GXRad2,
    ReadoutInterval? GYRad2, ReadoutInterval? SingleMisId, ReadoutInterval? MultiCrystalShare,
    ReadoutInterval? WeakestMedianSensitivity, string? SensitivitySource);
