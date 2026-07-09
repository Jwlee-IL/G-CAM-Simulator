using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Masks;

/// <summary>
/// A physical coded-aperture mask (e.g. 10 mm tungsten) carrying a
/// <see cref="MaskPattern"/>, sitting on the plane z = <see cref="PlaneZ"/> and
/// centered on the optical axis. Decides whether an incoming photon reaches the detector.
/// </summary>
public sealed class CodedApertureMask : IMask
{
    private readonly double _cellPitchMm;
    private readonly double _thicknessMm;
    private readonly double _muPerMm;
    private readonly double _halfWidth;   // physical half-extent along x (mm)
    private readonly double _halfHeight;  // physical half-extent along y (mm)

    public MaskPattern Pattern { get; }
    public double PlaneZ { get; }

    public CodedApertureMask(MaskPattern pattern, double planeZ, double cellPitchMm,
                             double thicknessMm, double muPerMm)
    {
        Pattern = pattern;
        PlaneZ = planeZ;
        _cellPitchMm = cellPitchMm;
        _thicknessMm = thicknessMm;
        _muPerMm = muPerMm;
        _halfWidth = pattern.Width * cellPitchMm / 2.0;
        _halfHeight = pattern.Height * cellPitchMm / 2.0;
    }

    // Sub-steps used to ray-march the ray through the finite-thickness slab. Higher
    // = finer resolution of oblique open-channel clipping (collimation).
    private const int SlabSteps = 12;

    public bool Transmit(Ray ray, double energyKeV, IRandom rng)
    {
        // The mask is a slab of thickness t centered on z = PlaneZ. March the ray
        // from the front face (z = PlaneZ + t/2) to the back face (z = PlaneZ - t/2),
        // counting how many sub-steps fall in tungsten (closed cells, mask edges, or
        // the surrounding shield). This makes thick masks collimate off-axis rays
        // through the open channels, and thin masks leak through closed cells.
        double dz = ray.Direction.Z;
        if (dz == 0.0) return false;                    // parallel to the slab

        double zFront = PlaneZ + _thicknessMm / 2.0;
        double zBack = PlaneZ - _thicknessMm / 2.0;

        int nTungsten = 0;
        for (int i = 0; i < SlabSteps; i++)
        {
            double frac = (i + 0.5) / SlabSteps;
            double z = zFront + (zBack - zFront) * frac;
            double tRay = (z - ray.Origin.Z) / dz;
            if (tRay <= 0.0) return false;              // slab behind the photon

            var hit = ray.At(tRay);
            double u = hit.X + _halfWidth;
            double v = hit.Y + _halfHeight;
            bool inFrame = u >= 0.0 && v >= 0.0 && u < 2.0 * _halfWidth && v < 2.0 * _halfHeight;
            if (!inFrame)
            {
                nTungsten++;                            // surrounding shield = opaque
                continue;
            }
            int cx = (int)(u / _cellPitchMm);
            int cy = (int)(v / _cellPitchMm);
            if (!Pattern[cx, cy]) nTungsten++;          // closed cell = tungsten
        }

        if (nTungsten == 0) return true;                // clear open channel

        // Tungsten path = (fraction of slab in tungsten) × slant thickness.
        double slant = _thicknessMm / Math.Abs(dz);
        double tungstenPath = (double)nTungsten / SlabSteps * slant;
        double transmission = Math.Exp(-_muPerMm * tungstenPath);
        return rng.NextDouble() < transmission;
    }
}
