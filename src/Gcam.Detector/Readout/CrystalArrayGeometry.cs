using Gcam.Configuration;

namespace Gcam.Detector;

/// <summary>The pixelated crystal array as the optical readout sees it: Nx × Ny crystals on a pitch, separated by a
/// shared reflector gap (active width = pitch − gap, the engine's existing convention), depth along z with the gamma
/// entrance face at <see cref="PlaneZ"/> and the exit (readout) face at <see cref="PlaneZ"/> − depth.</summary>
public sealed record CrystalArrayGeometry(int CountX, int CountY, double PitchMm, double GapMm, double DepthMm,
    double PlaneZ = 0.0)
{
    public double ActiveWidthMm => PitchMm - GapMm;
    public double HalfWidthMm => CountX * PitchMm / 2.0;
    public double HalfHeightMm => CountY * PitchMm / 2.0;
    public int Count => CountX * CountY;

    public double CenterX(int ix) => (ix + 0.5) * PitchMm - HalfWidthMm;
    public double CenterY(int iy) => (iy + 0.5) * PitchMm - HalfHeightMm;

    /// <summary>Height above the exit face of a point at <paramref name="zMm"/>, clamped into the crystal.</summary>
    public double HeightAboveExit(double zMm) => Math.Clamp(zMm - (PlaneZ - DepthMm), 0.0, DepthMm);

    public static CrystalArrayGeometry From(DetectorConfig detector)
    {
        ArgumentNullException.ThrowIfNull(detector);
        var g = new CrystalArrayGeometry(detector.PixelsX, detector.PixelsY, detector.PixelPitchMm,
            detector.ReflectorGapMm, detector.CrystalThicknessMm);
        if (g.CountX < 1 || g.CountY < 1 || !(g.PitchMm > 0) || !(g.GapMm >= 0) || !(g.ActiveWidthMm > 0) || !(g.DepthMm > 0))
            throw new ArgumentException("Crystal array needs positive counts, pitch, depth and active width (pitch − gap).");
        return g;
    }
}
