namespace Gcam.Studio.Core.Detector;

/// <summary>Exact geometric active regions and non-overlapping reflector rectangles; no efficiency claim.</summary>
public sealed record DetectorFace(double SizeMm, double ActiveAreaFraction,
    IReadOnlyList<FaceRectangle> Crystals, IReadOnlyList<FaceRectangle> Gaps)
{
    public static DetectorFace Create(int pixels, double pitchMm, double gapMm, IReadOnlyList<double> gains)
    {
        ArgumentNullException.ThrowIfNull(gains);
        if (pixels <= 0 || !double.IsFinite(pitchMm) || pitchMm <= 0 ||
            !double.IsFinite(gapMm) || gapMm < 0 || gapMm >= pitchMm ||
            gains.Count != pixels * pixels || gains.Any(g => !double.IsFinite(g) || g <= 0))
            throw new ArgumentOutOfRangeException(nameof(gapMm));
        double size = pixels * pitchMm, active = pitchMm - gapMm;
        var crystals = new List<FaceRectangle>();
        var gaps = new List<FaceRectangle>();
        for (int y = 0; y < pixels; y++)
        for (int x = 0; x < pixels; x++)
            crystals.Add(new(x * pitchMm + gapMm / 2, y * pitchMm + gapMm / 2,
                active, active, gains[y * pixels + x]));
        if (gapMm > 0)
        {
            // Half gaps at the perimeter, full gaps at every interior boundary, as InReflectorGap scores them.
            for (int i = 0; i <= pixels; i++)
            {
                double start = i == 0 ? 0 : i * pitchMm - gapMm / 2;
                double width = i == 0 || i == pixels ? gapMm / 2 : gapMm;
                gaps.Add(new(start, 0, width, size));
                for (int x = 0; x < pixels; x++)
                    gaps.Add(new(x * pitchMm + gapMm / 2, start, active, width));
            }
        }
        return new(size, Math.Pow(active / pitchMm, 2), crystals.AsReadOnly(), gaps.AsReadOnly());
    }
}
