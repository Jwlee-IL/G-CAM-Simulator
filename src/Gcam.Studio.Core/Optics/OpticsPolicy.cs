using Gcam.Configuration;

namespace Gcam.Studio.Core.Optics;

/// <summary>Studio input and allocation policy, separate from engine physics limits.</summary>
public static class OpticsPolicy
{
    public static IReadOnlyList<int> Ranks { get; } = Array.AsReadOnly(new[] { 5, 7, 11, 13, 17, 19, 23 });
    public const int MaxGridSide = 128;
    public const double MaskThicknessMm = 10;

    public static string? Validate(OpticsSettings value, double reflectorGapMm = 0.1)
    {
        if (!Ranks.Contains(value.MuraRank)) return "Choose a supported prime rank.";
        if (value.DetectorPixels is < 4 or > 64) return "Detector pixels must be an integer from 4 to 64.";
        if (!double.IsFinite(value.MaskDetectorDistanceMm) || value.MaskDetectorDistanceMm < 1)
            return "Mask–detector distance must be finite and at least 1 mm.";
        if (!double.IsFinite(value.CellPitchMm) || value.CellPitchMm < 0.05)
            return "Cell pitch must be finite and at least 0.05 mm.";
        if (!double.IsFinite(value.PixelPitchMm) || value.PixelPitchMm < 0.05)
            return "Pixel pitch must be finite and at least 0.05 mm.";
        if (value.PixelPitchMm <= reflectorGapMm) return "Pixel pitch must exceed the reflector gap.";
        if (!double.IsFinite(2 * value.MuraRank * value.CellPitchMm) ||
            !double.IsFinite(value.DetectorPixels * value.PixelPitchMm)) return "Physical dimensions are too large.";
        return null;
    }

    public static string? ValidateFocus(OpticsSettings value, double focalDistanceMm)
    {
        if (!double.IsFinite(focalDistanceMm) || focalDistanceMm <= value.MaskDetectorDistanceMm)
            return "Decoder focal plane must be finite and beyond the acquired mask plane.";
        double r = value.CellPitchMm * (focalDistanceMm / value.MaskDetectorDistanceMm);
        double half = 0.95 * value.MuraRank * r / 2;
        double step = Math.Max(0.2, r / 4);
        double side = Math.Round(2 * half / step) + 1;
        if (!double.IsFinite(r) || !double.IsFinite(half) || !double.IsFinite(step) ||
            !double.IsFinite(side) || side is < 1 or > MaxGridSide)
            return "Decoder grid exceeds the Studio allocation limit (128 per side).";
        var derived = OpticsGeometry.Calculate(value, focalDistanceMm);
        if (!double.IsFinite(derived.SamplesPerCell) || !double.IsFinite(derived.CoveragePeriods))
            return "Projected dimensions are too large.";
        return null;
    }

    public static string? ValidateScene(OpticsSettings value, IEnumerable<SceneSource> scene)
    {
        foreach (var source in scene)
        {
            if (!double.IsFinite(source.X) || !double.IsFinite(source.Y) ||
                !double.IsFinite(source.ActivityUCi) || source.ActivityUCi <= 0)
                return "Source coordinates and positive activity must be finite.";
            if (!double.IsFinite(source.DistanceMm) || source.DistanceMm <= value.MaskDetectorDistanceMm + MaskThicknessMm / 2)
                return "Every source must be in front of the mask's front face (D + 5 mm).";
        }
        return null;
    }
}
