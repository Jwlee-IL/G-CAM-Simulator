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
    private readonly double _focalMm;     // channels converge toward a source at this distance (0 = straight)
    private readonly double _holeFraction;// LINEAR open fraction of a cell (open AREA = _holeFraction²)
    private readonly double _tanTaper;    // bevel of the channel walls (0 = straight, collimating)

    public MaskPattern Pattern { get; }
    public double PlaneZ { get; }

    public CodedApertureMask(MaskPattern pattern, double planeZ, double cellPitchMm,
                             double thicknessMm, double muPerMm,
                             double focalDistanceMm = 0.0, double holeFraction = 1.0,
                             double taperAngleDeg = 0.0)
    {
        Pattern = pattern;
        PlaneZ = planeZ;
        _cellPitchMm = cellPitchMm;
        _thicknessMm = thicknessMm;
        _muPerMm = muPerMm;
        _halfWidth = pattern.Width * cellPitchMm / 2.0;
        _halfHeight = pattern.Height * cellPitchMm / 2.0;
        // Focused channels need the focal point outside the slab; focal <= thickness would make the
        // per-depth remap denominator (focal + PlaneZ - z) hit zero/flip sign inside the slab. Reject
        // that unphysical geometry by falling back to straight channels.
        _focalMm = focalDistanceMm > thicknessMm ? focalDistanceMm : 0.0;
        _holeFraction = holeFraction <= 0.0 ? 1.0 : Math.Min(holeFraction, 1.0);
        // Bevelled (hourglass) channel walls: the code is defined at the slab MID-PLANE and the walls
        // flare by this angle toward both faces, so an off-axis ray within the taper cone is not
        // clipped. Taper is a wide-FOV device for THICK masks; it is mutually exclusive with focusing.
        _tanTaper = (taperAngleDeg > 0.0 && _focalMm == 0.0) ? Math.Tan(taperAngleDeg * Math.PI / 180.0) : 0.0;
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

        // Bevelled walls: the code is sampled at the slab mid-plane; toward each face the wall recedes
        // by |z - mid|·tan(taper), so a ray is de-sheared back toward its mid-plane crossing before the
        // pattern lookup (an off-axis ray within the taper cone then sees its own open cell, uncollimated).
        double uMid = 0.0, vMid = 0.0;
        if (_tanTaper > 0.0)
        {
            double tMid = (PlaneZ - ray.Origin.Z) / dz;
            var hitMid = ray.At(tMid);
            uMid = hitMid.X + _halfWidth;
            vMid = hitMid.Y + _halfHeight;
        }

        int nTungsten = 0;
        for (int i = 0; i < SlabSteps; i++)
        {
            double frac = (i + 0.5) / SlabSteps;
            double z = zFront + (zBack - zFront) * frac;
            double tRay = (z - ray.Origin.Z) / dz;
            if (tRay <= 0.0) return false;              // slab behind the photon

            var hit = ray.At(tRay);
            // Focused channels: map the hit back to the mask-centre frame along a channel that
            // converges toward a source at z = PlaneZ + focal. A ray FROM that focal point keeps a
            // constant mapped position across the slab -> a clear channel with no oblique clipping.
            double scale = _focalMm > 0.0 ? _focalMm / (_focalMm + PlaneZ - z) : 1.0;
            double u = hit.X * scale + _halfWidth;
            double v = hit.Y * scale + _halfHeight;
            bool inFrame = u >= 0.0 && v >= 0.0 && u < 2.0 * _halfWidth && v < 2.0 * _halfHeight;
            if (!inFrame)
            {
                nTungsten++;                            // surrounding shield = opaque
                continue;
            }
            // De-shear toward the mid-plane crossing by the wall bevel at this depth.
            double us = u, vs = v;
            if (_tanTaper > 0.0)
            {
                double bevel = Math.Abs(z - PlaneZ) * _tanTaper;
                us = DeShear(u, uMid, bevel);
                vs = DeShear(v, vMid, bevel);
            }
            // Guard on us/vs BEFORE the cast: (int) truncates toward zero, so a slightly negative
            // de-sheared coordinate (us = -0.1) would wrongly land in cell 0 instead of being rejected.
            if (us < 0.0 || vs < 0.0) { nTungsten++; continue; }
            int cx = (int)(us / _cellPitchMm);
            int cy = (int)(vs / _cellPitchMm);
            if (cx >= Pattern.Width || cy >= Pattern.Height) { nTungsten++; continue; }
            if (!Pattern[cx, cy]) { nTungsten++; continue; }   // closed cell = tungsten
            // Finite hole: only the central holeFraction of an open cell is drilled; the rest is
            // a tungsten border (sharpens the shadow at the cost of open area / sensitivity).
            if (_holeFraction < 1.0)
            {
                double fx = us / _cellPitchMm - cx, fy = vs / _cellPitchMm - cy;
                double b = (1.0 - _holeFraction) / 2.0;
                if (fx < b || fx > 1.0 - b || fy < b || fy > 1.0 - b) nTungsten++;
            }
        }

        if (nTungsten == 0) return true;                // clear open channel

        // Tungsten path = (fraction of slab in tungsten) × slant thickness.
        double slant = _thicknessMm / Math.Abs(dz);
        double tungstenPath = (double)nTungsten / SlabSteps * slant;
        // Energy-dependent attenuation: _muPerMm is anchored at 662 keV; scale it by the tungsten μ(E)/μ(662)
        // so low-energy lines (e.g. Co-57 122) are heavily blocked while high-energy lines (Co-60 1332) leak
        // more through closed cells. For a mixed field the lines now attenuate differently in the mask.
        double transmission = Math.Exp(-_muPerMm * TungstenMuRel(energyKeV) * tungstenPath);
        return rng.NextDouble() < transmission;
    }

    // Tungsten linear-attenuation ratio μ(E)/μ(662 keV), from NIST-XCOM total-with-coherent points
    // (photoelectric-steep below ~200 keV, Compton-flat above), log–log interpolated and end-clamped.
    // Anchored at 662 keV = 1.0 so the config's LinearAttenuationPerMm keeps its meaning.
    private static readonly double[] _muE = { 122.0, 250.0, 400.0, 662.0, 1000.0, 1332.0 };
    private static readonly double[] _muR = { 28.6, 2.82, 1.60, 1.00, 0.72, 0.61 };

    private static double TungstenMuRel(double energyKeV)
    {
        if (energyKeV <= _muE[0]) return _muR[0];
        if (energyKeV >= _muE[^1]) return _muR[^1];
        int i = 1; while (energyKeV > _muE[i]) i++;
        double t = (Math.Log(energyKeV) - Math.Log(_muE[i - 1])) / (Math.Log(_muE[i]) - Math.Log(_muE[i - 1]));
        return Math.Exp(Math.Log(_muR[i - 1]) + t * (Math.Log(_muR[i]) - Math.Log(_muR[i - 1])));
    }

    // Pull a sample position toward the mid-plane crossing by the wall bevel: if the ray's excursion
    // from the mid-plane is within the bevel it collapses to the mid-plane cell (uncollimated),
    // otherwise only the residual excursion is kept (still collimated beyond the taper cone).
    private static double DeShear(double u, double uMid, double bevel)
    {
        double d = u - uMid;
        double residual = Math.Abs(d) - bevel;
        return residual <= 0.0 ? uMid : uMid + Math.Sign(d) * residual;
    }
}
