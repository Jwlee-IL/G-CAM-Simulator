using Gcam.Configuration;
using Gcam.Core;
using Gcam.Decoding;
using Gcam.Detector;
using Gcam.Masks;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Services;

// Shared geometry/physics constants, all read from Studio's own defaults (BuildConfig), never hand-typed.
static class G
{
    public static readonly OpticsSettings Opt = new();
    public static readonly DetectorSettings DS = new();
    public static readonly SimulationConfig Base = Config(0, 0, 1000, 1);
    public static readonly double D = Base.Geometry.MaskDetectorDistanceMm;
    public static readonly int NPix = Base.Detector.PixelsX;
    public static readonly double Pitch = Base.Detector.PixelPitchMm;
    public static readonly FrontEndModel Fe = new(DS.Chain.BuildConfig());
    public static readonly double HalfWin = 1.5 * 661.7 * Fe.FwhmFraction(661.7);
    public static readonly double[] Gain = new CrystalUniformity(new DetectorConfig
    { PixelsX = NPix, PixelsY = NPix, GainSigma = DS.GainSigma, UniformitySeed = DS.GainSeed }).Gain;

    public static SimulationConfig Config(double x, double y, double z, int seed)
    {
        var c = SimulationService.BuildConfig([new SceneSource { X = x, Y = y, DistanceMm = z, ActivityUCi = 500 }], Opt, DS);
        c.Seed = seed;
        return c;
    }

    /// <summary>One list-mode acquisition exactly as the Studio path (ListModeSource → MeasurementStage).</summary>
    public static (double[] all, double[] win, long hist) Acquire(double x, double y, double z, int seed, double liveS)
    {
        var c = Config(x, y, z, seed);
        using var src = new ListModeSource(c);
        var ms = new MeasurementStage(DS, NPix, NPix);
        var all = new double[NPix * NPix]; var win = new double[NPix * NPix];
        int hits = 0;
        while (src.ArrivalTimeS <= liveS)
        {
            var ev = src.Advance();
            if (ev is not { } e || e.ArrivalTimeS > liveS) continue;
            int i = e.PixelY * NPix + e.PixelX;
            all[i]++;
            if (Math.Abs(ms.Measure(e, hits) - 661.7) <= HalfWin) win[i]++;
            hits++;
        }
        return (all, win, src.HistoriesEmitted);
    }

    /// <summary>Bearing seed: Studio's own non-cyclic decode at the default 1000 mm plane, no scene truth.</summary>
    public static (double px, double py) DecodeBearing(double[] flood, double plane = 1000)
    {
        var cfg = SceneConfigBuilder.Build([new SceneSource { DistanceMm = plane }], Opt, 1);
        cfg.Decoder.Cyclic = false;
        var p = ImagingProjection.AtFocus(cfg, Opt, plane);
        var im = new DetectorImage(NPix, NPix);
        for (int i = 0; i < flood.Length; i++) im[i % NPix, i / NPix] = flood[i];
        var d = new DefaultSimulationFactory().CreateDecoder(p)!.Decode(im);
        return (d.Estimate.Position.X / (plane - D), d.Estimate.Position.Y / (plane - D));
    }

    public static double Phi(double x)
    {   // standard normal CDF via erfc (W. J. Cody-style rational approx, |err| < 1.2e-7 — Numerical Recipes erfcc)
        double z = -x / Math.Sqrt(2), t = 1 / (1 + 0.5 * Math.Abs(z));
        double r = t * Math.Exp(-z * z - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 + t * (-0.18628806 +
                   t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 + t * (-0.82215223 + t * 0.17087277)))))))));
        double erfc = z >= 0 ? r : 2 - r;
        return 0.5 * erfc;
    }

    /// <summary>Expected probability that a deposit lands in the 662 window after gain and the chain's Gaussian smear —
    /// the analytic mean of MeasurementStage.Measure + the window test.</summary>
    public static double PWin(int pixel, double deposit)
    {
        double a = deposit * Gain[pixel];
        if (a <= 0) return 0;
        double s = a * Fe.FwhmFraction(a) / 2.3548;
        return Phi((661.7 + HalfWin - a) / s) - Phi((661.7 - HalfWin - a) / s);
    }
}

/// <summary>Counter-based random stream so every history replays identically for every candidate source (CRN).</summary>
sealed class Crn : IRandom
{
    public ulong S;
    public double NextDouble()
    {
        S += 0x9E3779B97F4A7C15UL;
        ulong z = S;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (z >> 11) * (1.0 / 9007199254740992.0);
    }
    public Vector3 NextOnUnitSphere()
    {
        double z = 2 * NextDouble() - 1, p = 2 * Math.PI * NextDouble(), r = Math.Sqrt(1 - z * z);
        return new Vector3(r * Math.Cos(p), r * Math.Sin(p), z);
    }
    public static ulong Mix(ulong a, ulong b)
    {
        ulong z = a * 0x9E3779B97F4A7C15UL + b + 0x632BE59BD9B4E019UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}

/// <summary>Shared per-history common random numbers: entry point (pixel-stratified), line, crystal stream key.</summary>
sealed class Histories
{
    public readonly int N; public readonly double[] Ex, Ey; public readonly int[] Line; public readonly ulong[] Key;
    public readonly double[] E, MuRel; public readonly int ModelSeed;
    public Histories(int n, int modelSeed)
    {
        ModelSeed = modelSeed;
        int np = G.NPix * G.NPix; N = (n + np - 1) / np * np;
        Ex = new double[N]; Ey = new double[N]; Line = new int[N]; Key = new ulong[N];
        var src = G.Base.Sources![0];
        E = src.Lines!.Select(l => l.EnergyKeV).ToArray();
        var w = src.Lines!.Select(l => l.Intensity).ToArray();
        MuRel = E.Select(CodedApertureMask.TungstenMuRel).ToArray();
        double tot = w.Sum();
        var r = new Crn { S = Crn.Mix((ulong)modelSeed, 0xABCDEF) };
        double half = G.NPix * G.Pitch / 2;
        for (int k = 0; k < N; k++)
        {
            int j = k % np;
            Ex[k] = (j % G.NPix + r.NextDouble()) * G.Pitch - half;
            Ey[k] = (j / G.NPix + r.NextDouble()) * G.Pitch - half;
            double u = r.NextDouble() * tot, acc = 0; int l = 0;
            for (; l < w.Length - 1; l++) { acc += w[l]; if (u < acc) break; }
            Line[k] = l;
            Key[k] = Crn.Mix((ulong)modelSeed * 7919UL + 1, (ulong)k);
        }
    }
}

/// <summary>Forward model: expected per-pixel detections per emitted photon for a point source at (x, y, z), all events and
/// the 662 window. Mask = exact expected transmission of the engine's 12-step slab march; entrance absorber, crystal
/// Compton cascade, backing and gap = the engine's ComptonCrystalDetector itself, replayed with CRN; window = analytic
/// mean of gain + Gaussian smear. One instance per thread.</summary>
sealed class Model
{
    readonly Histories _h;
    readonly Crn _rng = new();
    readonly ComptonCrystalDetector _det;
    readonly MaskPattern _pat;
    readonly double _cell, _half, _t, _mu;
    (int x, int y, double dep, double w)? _hit;
    public long Evals, HistoriesTransported;

    public Model(Histories h)
    {
        _h = h;
        var c = G.Base; var d = c.Detector;
        _pat = MuraGenerator.Mosaic(c.Mask.Rank, c.Mask.MosaicX, c.Mask.MosaicY);
        _cell = c.Mask.CellPitchMm; _half = _pat.Width * _cell / 2; _t = c.Mask.ThicknessMm; _mu = c.Mask.LinearAttenuationPerMm;
        if (c.Mask.Invert || c.Mask.FocalDistanceMm > 0 || c.Mask.TaperAngleDeg > 0 || c.Mask.HoleFraction is > 0 and < 1 ||
            c.Mask.HolePositionJitterMm > 0 || c.Mask.MaskOffsetXMm != 0 || c.Mask.MaskRollDeg != 0 || _pat.Width != _pat.Height)
            throw new InvalidOperationException("model assumes the ideal straight-channel mask");
        _det = new ComptonCrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm, 1, double.MaxValue, ComptonStrategy.Argmax, _rng,
            d.CrystalAttenuationPerMm > 0 ? d.CrystalAttenuationPerMm : null, d.CrystalThicknessMm,
            entranceAbsorber: d.EntranceAbsorberMm > 0 ? new EntranceAbsorber(d.EntranceAbsorberMm) : null,
            backingScatterer: d.BackingScatterMm > 0 ? new EntranceAbsorber(d.BackingScatterMm) : null,
            reflectorGapMm: d.ReflectorGapMm, opticalCrosstalk: d.OpticalCrosstalkFraction,
            material: CrystalMaterial.ForConfig(d.Material),
            pixelEventSink: (x, y, dep, w) => _hit = (x, y, dep, w));
    }

    /// <summary>Expected transmission of the engine's slab march (CodedApertureMask.Transmit without the Bernoulli draw).</summary>
    public double MaskT(Vector3 o, Vector3 dir, double muRel)
    {
        double dz = dir.Z, zf = G.D + _t / 2, zb = G.D - _t / 2; int n = 0;
        for (int i = 0; i < 12; i++)
        {
            double z = zf + (zb - zf) * ((i + 0.5) / 12);
            double tr = (z - o.Z) / dz;
            double u = o.X + dir.X * tr + _half, v = o.Y + dir.Y * tr + _half;
            if (!(u >= 0 && v >= 0 && u < 2 * _half && v < 2 * _half)) { n++; continue; }
            int cx = (int)(u / _cell), cy = (int)(v / _cell);
            if (cx >= _pat.Width || cy >= _pat.Height || !_pat[cx, cy]) n++;
        }
        if (n == 0) return 1;
        return Math.Exp(-_mu * muRel * ((double)n / 12 * _t / Math.Abs(dz)));
    }

    /// <summary>Fill all/win (length NPix²) with expected detections per emitted photon (× N for convenience).</summary>
    public void Eval(double x, double y, double z, double[] all, double[] win, int nUse = int.MaxValue)
    {
        Evals++;
        Array.Clear(all); Array.Clear(win);
        var s = new Vector3(x, y, z);
        double norm = 4 * G.NPix * G.NPix * G.Pitch * G.Pitch / 4 / (4 * Math.PI);
        int n = Math.Min(nUse, _h.N);
        HistoriesTransported += n;
        for (int k = 0; k < n; k++)
        {
            var delta = new Vector3(_h.Ex[k] - x, _h.Ey[k] - y, -z);
            double r = delta.Length;
            var dir = delta * (1 / r);
            double T = MaskT(s, dir, _h.MuRel[_h.Line[k]]);
            if (T < 1e-12) continue;
            double w = norm * Math.Abs(dir.Z) / (r * r) * T;
            _rng.S = _h.Key[k]; _hit = null;
            _det.Score(new Photon { Ray = new Ray(s, dir), EnergyKeV = _h.E[_h.Line[k]], Weight = w });
            if (_hit is not { } hit) continue;
            int p = hit.y * G.NPix + hit.x;
            all[p] += hit.w;
            win[p] += hit.w * G.PWin(p, hit.dep);
        }
    }
}
