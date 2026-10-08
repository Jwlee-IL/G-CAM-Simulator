using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

public static class ReadoutPolicy
{
    public static string? GeometryError(OpticsSettings optics) => optics.DetectorPixels != 12 || optics.PixelPitchMm != 1 ||
        optics.MuraRank != 7 || optics.CellPitchMm != 1 || optics.MaskDetectorDistanceMm != 60
        ? "Experimental physical readout requires the 12 × 12 Baseline head (rank 7, cell 1 mm, D 60 mm, crystal pitch 1 mm)." : null;
    public static string Key(OpticsSettings optics, DetectorSettings detector)
        => FormattableString.Invariant($"readout-v1|{optics.DetectorPixels}|{optics.PixelPitchMm:R}|{detector.ReflectorGapMm:R}|{detector.EntranceAbsorberMm:R}|{detector.BackingScatterMm:R}");
}
