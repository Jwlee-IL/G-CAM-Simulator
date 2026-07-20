namespace Gcam.Masks;

/// <summary>
/// A fixed, seeded per-cell fabrication error stamped on a coded-aperture mask: one specific MANUFACTURED
/// mask rather than the ideal MURA the decoder assumes. Tungsten is hard, brittle and high-melting, so a
/// deployed thick fine-pitch mask cannot hold the best-case machining spec — the holes are slightly
/// mis-placed and mis-sized, some fail to open, and (through a thick slab) the high-aspect-ratio bore
/// wanders with depth instead of being a clean straight channel. All errors live here, on the mask's
/// Transmit only; the decoder keeps decoding the ideal pattern, so this is a forward-model mismatch.
///
/// Everything is stored in units of the CELL PITCH (fraction) so <see cref="CodedApertureMask"/> can compare
/// directly against a ray's in-cell coordinate.
/// </summary>
public sealed class MaskFabrication
{
    public int Width { get; }
    public int Height { get; }

    private readonly double _baseHalf;       // nominal hole half-width (fraction of a cell)
    private readonly double[] _dx, _dy;      // in-plane hole-centre offset (fraction of pitch)
    private readonly double[] _ds;           // hole half-width error (fraction of pitch)
    private readonly double[] _wx, _wy;      // front→back depth wander of the centre (fraction of pitch)
    private readonly bool[] _blocked;        // open cell that failed to open

    public MaskFabrication(int width, int height, double cellPitchMm, double nominalHoleFraction,
                           double positionJitterMm, double sizeJitterMm, double blockedProbability,
                           double wanderMm, int seed)
    {
        Width = width;
        Height = height;
        _baseHalf = (nominalHoleFraction <= 0.0 ? 1.0 : Math.Min(nominalHoleFraction, 1.0)) / 2.0;

        int n = width * height;
        _dx = new double[n]; _dy = new double[n]; _ds = new double[n];
        _wx = new double[n]; _wy = new double[n]; _blocked = new bool[n];

        double posF = positionJitterMm / cellPitchMm;
        double sizeF = sizeJitterMm / cellPitchMm;
        double wanderF = wanderMm / cellPitchMm;

        var rng = new Random(seed);
        for (int i = 0; i < n; i++)
        {
            _dx[i] = posF * Gaussian(rng);
            _dy[i] = posF * Gaussian(rng);
            _ds[i] = sizeF * Gaussian(rng);
            _wx[i] = wanderF * Gaussian(rng);
            _wy[i] = wanderF * Gaussian(rng);
            _blocked[i] = blockedProbability > 0.0 && rng.NextDouble() < blockedProbability;
        }
    }

    /// <summary>Does this (open) cell fail to open — chipped shut / not drilled through / web breakout?</summary>
    public bool Blocked(int cx, int cy) => _blocked[cy * Width + cx];

    /// <summary>Hole centre (cx0, cy0) and half-width (hw), all in CELL-FRACTION units, for this cell at the
    /// given depth fraction (0 = front face, 1 = back face). The centre drifts with depth (drill wander); the
    /// half-width carries the per-hole size error. Clamped so a hole neither vanishes nor exceeds its cell.</summary>
    public (double cx0, double cy0, double hw) Hole(int cx, int cy, double depthFrac)
    {
        int i = cy * Width + cx;
        double d = depthFrac - 0.5;                       // ±0.5 across the slab, 0 at mid-plane
        double x0 = 0.5 + _dx[i] + _wx[i] * d;
        double y0 = 0.5 + _dy[i] + _wy[i] * d;
        double hw = Math.Clamp(_baseHalf + _ds[i], 0.02, 0.5);
        return (x0, y0, hw);
    }

    private static double Gaussian(Random r)
    {
        double u1 = 1.0 - r.NextDouble();
        double u2 = r.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
