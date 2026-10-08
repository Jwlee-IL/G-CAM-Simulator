using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Detector;

/// <summary>One entry of the optical response: probability that a photon emitted in a crystal / depth bin reaches the
/// photosensitive area of <see cref="Sensor"/> (before PDE).</summary>
public readonly record struct SensorShare(int Sensor, double Probability);

/// <summary>Where a traced scintillation photon ended.</summary>
public enum OpticalFate
{
    Detected, WallAbsorbed, TopLost, BulkAbsorbed, ExteriorLost, DeadArea, GuideLost, Trapped,
}

/// <summary>
/// Optical light-spread response of the crystal array, built once per geometry by tracing scintillation photons
/// (isotropic emission, uniform over a crystal's active cross-section within a depth bin) through: side walls and entrance
/// face — reflect with R (Lambertian or specular), cross into the neighbour with T, else absorbed; the exit face —
/// unpolarised Fresnel reflection / total internal reflection at crystal → couplant, refraction otherwise; the couplant
/// and optional light guide — Snell refraction and the lateral shift over their thickness; the sensor plane — a photon
/// that lands on a sensor's photosensitive square reaches it, a photon on a dead border is lost. Every photon ends in
/// exactly one <see cref="OpticalFate"/>, so the table conserves light by construction: Σ sensor probabilities + losses = 1.
/// <para>Approximations (stated, not hidden): the response is averaged over the lateral position inside a crystal (only
/// the depth of interaction is binned); Fresnel reflection is applied at the crystal exit face only (couplant → guide →
/// sensor window are taken as index-matched apart from refraction; TIR at couplant → guide is counted as lost); a photon
/// crossing a wall that would leave the wall outside the neighbour's face is absorbed in the wall; the light guide's
/// lateral edges absorb. Wall / ceramic optical constants are assumptions (no measured data in the repository).</para>
/// With a symmetric layout (sensor grid centred on the array), crystals equivalent under the array's reflections (and
/// transposition for square arrays) share one traced table, mapped by the same symmetry; each traced (crystal class,
/// depth bin) uses its own keyed random stream, so the table does not depend on evaluation order.
/// </summary>
public sealed class OpticalResponse
{
    private readonly SensorShare[][] _table;
    private readonly double[] _collection;
    private readonly ReadoutOpticsConfig _optics;
    private readonly double _rTop;

    public CrystalArrayGeometry Crystals { get; }
    public SensorLayout Sensors { get; }
    public int DepthBins { get; }
    public int PhotonsPerBin { get; }

    /// <summary>Distinct (crystal class) tables actually traced (after symmetry reduction).</summary>
    public int TracedClasses { get; }

    /// <summary>Mean collection (Σ sensor probability) over every crystal and depth bin, before PDE.</summary>
    public double MeanCollection { get; }

    /// <summary>Fraction of all traced photons ending in each <see cref="OpticalFate"/> (sums to 1).</summary>
    public IReadOnlyDictionary<OpticalFate, double> FateFractions { get; }

    public OpticalResponse(CrystalArrayGeometry crystals, SensorLayout sensors, ReadoutOpticsConfig optics, int seed)
    {
        ArgumentNullException.ThrowIfNull(crystals);
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(optics);
        if (!(optics.CrystalRefractiveIndex >= 1) || !(optics.CouplantRefractiveIndex >= 1) || !(optics.LightGuideRefractiveIndex >= 1)
            || !(optics.CouplantThicknessMm >= 0) || !(optics.LightGuideThicknessMm >= 0)
            || optics.WallReflectance is < 0 or > 1 || optics.WallTransmittance is < 0 or > 1
            || optics.WallReflectance + optics.WallTransmittance > 1 || optics.TopReflectance is < 0 or > 1
            || !(optics.AbsorptionLengthMm >= 0) || optics.DepthBins < 1 || optics.PhotonsPerBin < 1 || optics.MaxBounces < 1)
            throw new ArgumentException("Invalid optics: indices ≥ 1, thicknesses ≥ 0, 0 ≤ R, T and R + T ≤ 1, positive bins and budgets.");
        Crystals = crystals;
        Sensors = sensors;
        _optics = optics;
        _rTop = optics.TopReflectance ?? optics.WallReflectance;
        DepthBins = optics.DepthBins;
        PhotonsPerBin = optics.PhotonsPerBin;

        bool flips = sensors.OffsetXMm == 0 && sensors.OffsetYMm == 0;
        bool transpose = flips && crystals.CountX == crystals.CountY && sensors.CountX == sensors.CountY;
        int nx = crystals.CountX, ny = crystals.CountY, bins = DepthBins;
        _table = new SensorShare[crystals.Count * bins][];
        _collection = new double[crystals.Count * bins];
        var classes = new Dictionary<(int, int), int>();
        var classTables = new List<SensorShare[][]>();
        var fates = new long[Enum.GetValues<OpticalFate>().Length];
        var counts = new int[sensors.Count];
        for (int iy = 0; iy < ny; iy++)
            for (int ix = 0; ix < nx; ix++)
            {
                int cx = ix, cy = iy;
                bool fx = flips && ix > nx - 1 - ix, fy = flips && iy > ny - 1 - iy;
                if (fx) cx = nx - 1 - ix;
                if (fy) cy = ny - 1 - iy;
                bool tr = transpose && cx > cy;
                if (tr) (cx, cy) = (cy, cx);
                if (!classes.TryGetValue((cx, cy), out int cls))
                {
                    cls = classTables.Count;
                    classes.Add((cx, cy), cls);
                    var perBin = new SensorShare[bins][];
                    for (int b = 0; b < bins; b++)
                    {
                        var rng = DefaultRandom.FromKey(DefaultRandom.Key(seed, (uint)(cls * bins + b)));
                        Array.Clear(counts);
                        double a = crystals.ActiveWidthMm, binH = crystals.DepthMm / bins;
                        for (int p = 0; p < PhotonsPerBin; p++)
                        {
                            double lx = (rng.NextDouble() - 0.5) * a, ly = (rng.NextDouble() - 0.5) * a;
                            double h = (b + rng.NextDouble()) * binH;
                            var (fate, sensor) = Trace(cx, cy, lx, ly, h, rng);
                            fates[(int)fate]++;
                            if (fate == OpticalFate.Detected) counts[sensor]++;
                        }
                        var shares = new List<SensorShare>();
                        for (int k = 0; k < counts.Length; k++)
                            if (counts[k] > 0) shares.Add(new SensorShare(k, (double)counts[k] / PhotonsPerBin));
                        perBin[b] = shares.ToArray();
                    }
                    classTables.Add(perBin);
                }
                int crystal = iy * nx + ix;
                for (int b = 0; b < bins; b++)
                {
                    var src = classTables[cls][b];
                    var mapped = new SensorShare[src.Length];
                    double sum = 0;
                    for (int e = 0; e < src.Length; e++)
                    {
                        int kx = src[e].Sensor % sensors.CountX, ky = src[e].Sensor / sensors.CountX;
                        if (tr) (kx, ky) = (ky, kx);
                        if (fx) kx = sensors.CountX - 1 - kx;
                        if (fy) ky = sensors.CountY - 1 - ky;
                        mapped[e] = new SensorShare(ky * sensors.CountX + kx, src[e].Probability);
                        sum += src[e].Probability;
                    }
                    Array.Sort(mapped, (p, q) => p.Sensor.CompareTo(q.Sensor));
                    _table[crystal * bins + b] = mapped;
                    _collection[crystal * bins + b] = sum;
                }
            }
        TracedClasses = classTables.Count;
        MeanCollection = _collection.Average();
        long traced = fates.Sum();
        FateFractions = Enum.GetValues<OpticalFate>().ToDictionary(f => f, f => (double)fates[(int)f] / traced);
    }

    /// <summary>Sensor probabilities for a photon emitted in <paramref name="crystal"/> (index iy·Nx + ix), depth bin
    /// <paramref name="bin"/> (0 = next to the exit face), sorted by sensor index.</summary>
    public ReadOnlySpan<SensorShare> Response(int crystal, int bin) => _table[crystal * DepthBins + bin];

    /// <summary>Total probability of reaching any sensor (before PDE).</summary>
    public double Collection(int crystal, int bin) => _collection[crystal * DepthBins + bin];

    /// <summary>Depth bin of a point at height <paramref name="zMm"/> (detector frame).</summary>
    public int DepthBin(double zMm)
    {
        int b = (int)(Crystals.HeightAboveExit(zMm) / Crystals.DepthMm * DepthBins);
        return Math.Clamp(b, 0, DepthBins - 1);
    }

    /// <summary>Trace one photon emitted isotropically at local (x, y) of crystal (ix, iy), at
    /// <paramref name="heightMm"/> above the exit face. Public so tests can check the tracer against closed forms.</summary>
    public (OpticalFate Fate, int Sensor) Trace(int ix, int iy, double localX, double localY, double heightMm, IRandom rng)
    {
        var d0 = rng.NextOnUnitSphere();
        return TraceDirection(ix, iy, localX, localY, heightMm, d0.X, d0.Y, d0.Z, rng);
    }

    /// <summary>As <see cref="Trace"/> with a given initial direction (dz &gt; 0 points to the entrance face).</summary>
    public (OpticalFate Fate, int Sensor) TraceDirection(int ix, int iy, double x, double y, double h,
        double dx, double dy, double dz, IRandom rng)
    {
        double a = Crystals.ActiveWidthMm, half = a / 2.0, depth = Crystals.DepthMm, gap = Crystals.GapMm;
        double r = _optics.WallReflectance, t = _optics.WallTransmittance;
        bool lambert = _optics.Surface == ReflectorSurface.Lambertian;
        double absorb = _optics.AbsorptionLengthMm;
        for (int bounce = 0; bounce <= _optics.MaxBounces; bounce++)
        {
            double tx = dx > 0 ? (half - x) / dx : dx < 0 ? (-half - x) / dx : double.PositiveInfinity;
            double ty = dy > 0 ? (half - y) / dy : dy < 0 ? (-half - y) / dy : double.PositiveInfinity;
            double th = dz > 0 ? (depth - h) / dz : dz < 0 ? -h / dz : double.PositiveInfinity;
            double step = Math.Min(tx, Math.Min(ty, th));
            if (absorb > 0 && -Math.Log(1.0 - rng.NextDouble()) * absorb < step) return (OpticalFate.BulkAbsorbed, -1);
            x += dx * step; y += dy * step; h += dz * step;
            if (th <= tx && th <= ty)
            {
                if (dz > 0)
                {
                    h = depth;
                    if (rng.NextDouble() >= _rTop) return (OpticalFate.TopLost, -1);
                    if (lambert) (dx, dy, dz) = Lambert(rng, 2, -1);
                    else dz = -dz;
                    continue;
                }
                h = 0;
                double n1 = _optics.CrystalRefractiveIndex, n2 = _optics.CouplantRefractiveIndex;
                double cosI = -dz, sinT = n1 / n2 * Math.Sqrt(Math.Max(0, 1 - cosI * cosI));
                if (sinT >= 1.0) { dz = -dz; continue; }                       // total internal reflection
                double cosT = Math.Sqrt(1 - sinT * sinT);
                double rs = (n1 * cosI - n2 * cosT) / (n1 * cosI + n2 * cosT);
                double rp = (n2 * cosI - n1 * cosT) / (n2 * cosI + n1 * cosT);
                if (rng.NextDouble() < 0.5 * (rs * rs + rp * rp)) { dz = -dz; continue; }
                return Land(ix, iy, x, y, dx, dy, n1 / n2);
            }
            bool alongX = tx <= ty;
            double side = alongX ? Math.Sign(dx) : Math.Sign(dy);
            if (alongX) x = half * side; else y = half * side;
            double u = rng.NextDouble();
            if (u < r)
            {
                if (lambert) (dx, dy, dz) = Lambert(rng, alongX ? 0 : 1, -side);
                else if (alongX) dx = -dx; else dy = -dy;
                continue;
            }
            if (u >= r + t) return (OpticalFate.WallAbsorbed, -1);
            int nxIdx = ix + (alongX ? (int)side : 0), nyIdx = iy + (alongX ? 0 : (int)side);
            if (nxIdx < 0 || nyIdx < 0 || nxIdx >= Crystals.CountX || nyIdx >= Crystals.CountY)
                return (OpticalFate.ExteriorLost, -1);
            double cross = gap / Math.Abs(alongX ? dx : dy);                    // straight path through the wall
            if (alongX) { y += dy * cross; x = -half * side; } else { x += dx * cross; y = -half * side; }
            h += dz * cross;
            if (Math.Abs(alongX ? y : x) > half || h < 0 || h > depth) return (OpticalFate.WallAbsorbed, -1);
            ix = nxIdx; iy = nyIdx;
        }
        return (OpticalFate.Trapped, -1);
    }

    // Leave the exit face at local (x, y) of crystal (ix, iy): refract into the couplant (tangential components scale by
    // n1/n2), shift laterally over the couplant and light-guide thicknesses, land on the sensor plane.
    private (OpticalFate, int) Land(int ix, int iy, double x, double y, double dx, double dy, double ratio)
    {
        double gx = Crystals.CenterX(ix) + x, gy = Crystals.CenterY(iy) + y;
        double tX = dx * ratio, tY = dy * ratio;                                // tangential direction in the couplant
        double s2 = tX * tX + tY * tY, cz = Math.Sqrt(Math.Max(1e-300, 1 - s2));
        gx += _optics.CouplantThicknessMm * tX / cz;
        gy += _optics.CouplantThicknessMm * tY / cz;
        if (_optics.LightGuideThicknessMm > 0)
        {
            double g = _optics.CouplantRefractiveIndex / _optics.LightGuideRefractiveIndex;
            double uX = tX * g, uY = tY * g, u2 = uX * uX + uY * uY;
            if (u2 >= 1.0) return (OpticalFate.GuideLost, -1);
            double uz = Math.Sqrt(1 - u2);
            gx += _optics.LightGuideThicknessMm * uX / uz;
            gy += _optics.LightGuideThicknessMm * uY / uz;
            if (Math.Abs(gx) > Crystals.HalfWidthMm || Math.Abs(gy) > Crystals.HalfHeightMm) return (OpticalFate.GuideLost, -1);
        }
        int sensor = Sensors.SensorAt(gx, gy);
        return sensor < 0 ? (OpticalFate.DeadArea, -1) : (OpticalFate.Detected, sensor);
    }

    // Cosine-weighted direction about the inward normal of a face perpendicular to axis (0 = x, 1 = y, 2 = z), the normal
    // pointing along sign · axis.
    private static (double, double, double) Lambert(IRandom rng, int axis, double sign)
    {
        double u1 = rng.NextDouble(), u2 = rng.NextDouble();
        double cos = Math.Sqrt(1 - u1), sin = Math.Sqrt(u1), phi = 2 * Math.PI * u2;
        double n = sign * cos, p = sin * Math.Cos(phi), q = sin * Math.Sin(phi);
        return axis switch { 0 => (n, p, q), 1 => (p, n, q), _ => (p, q, n) };
    }
}
