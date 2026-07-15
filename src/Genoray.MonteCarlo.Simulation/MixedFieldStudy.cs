using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One localized peak of the mixed-field reconstruction (source-plane mm + strength).</summary>
public sealed record FoundSource(double Xmm, double Ymm, double Value);

/// <summary>Multi-source imaging result: the K strongest reconstruction peaks vs the true sources.</summary>
public sealed record MixedFieldResult(
    FoundSource[] Found, double[][] TruthXY, DetectorImage Recon, double OriginMm, double StepMm);

/// <summary>
/// Images a MIXED-isotope field (several sources, via <see cref="SimulationConfig.Sources"/>) through
/// the coded aperture in one run, then extracts the K strongest source peaks by non-maximum
/// suppression on the reconstruction. Demonstrates that the mixed field localizes ALL sources at once
/// (no energy window yet — that separates isotopes; this separates POSITIONS).
/// </summary>
public sealed class MixedFieldStudy
{
    private readonly ISimulationFactory _factory;

    public MixedFieldStudy(ISimulationFactory factory) => _factory = factory;

    public MixedFieldResult LocalizeMultiple(SimulationConfig config, int k, double minSeparationMm)
    {
        if (k < 1) throw new ArgumentOutOfRangeException(nameof(k));
        if (!(minSeparationMm > 0.0)) throw new ArgumentOutOfRangeException(nameof(minSeparationMm));

        var res = new SimulationRunner(_factory).Run(config);
        var recon = res.Reconstruction
                    ?? throw new InvalidOperationException("no reconstruction (decoder not wired)");
        var found = TopPeaks(recon, res.ReconOriginMm, res.ReconStepMm, k, minSeparationMm);

        // Match the factory's rule: a non-empty Sources list is the scene, otherwise the single Source.
        var scene = config.Sources is { Length: > 0 } ss ? ss : [config.Source];
        var truth = scene.Select(s => new[] { s.Position[0], s.Position[1] }).ToArray();
        return new MixedFieldResult(found, truth, recon, res.ReconOriginMm, res.ReconStepMm);
    }

    /// <summary>One-to-one greedy assignment of found peaks to true sources (each found peak is used at
    /// most once), so the "all localized" check can't reuse a single peak for several truths.</summary>
    public sealed record Match(double TruthX, double TruthY, double FoundX, double FoundY, double ErrorMm);

    public static Match[] MatchOneToOne(double[][] truth, IReadOnlyList<FoundSource> found)
    {
        var remaining = found.ToList();
        var matches = new List<Match>(truth.Length);
        foreach (var t in truth)
        {
            int bi = -1; double best = double.PositiveInfinity;
            for (int i = 0; i < remaining.Count; i++)
            {
                double d = Math.Sqrt((remaining[i].Xmm - t[0]) * (remaining[i].Xmm - t[0]) +
                                     (remaining[i].Ymm - t[1]) * (remaining[i].Ymm - t[1]));
                if (d < best) { best = d; bi = i; }
            }
            if (bi >= 0)
            {
                matches.Add(new Match(t[0], t[1], remaining[bi].Xmm, remaining[bi].Ymm, best));
                remaining.RemoveAt(bi);
            }
            else matches.Add(new Match(t[0], t[1], double.NaN, double.NaN, double.PositiveInfinity));
        }
        return matches.ToArray();
    }

    /// <summary>The K strongest reconstruction peaks, each ≥ minSeparation apart (greedy non-max
    /// suppression: take the global max, blank a disk around it, repeat). For a clean MURA decode each
    /// source is a sharp peak, so this recovers well-separated multiple sources.</summary>
    public static FoundSource[] TopPeaks(DetectorImage recon, double origin, double step,
                                         int k, double minSeparationMm)
    {
        int w = recon.Width, h = recon.Height;
        var work = new double[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                work[x, y] = recon[x, y];

        double blankR = minSeparationMm / step;
        double blankR2 = blankR * blankR;
        var peaks = new List<FoundSource>(k);
        for (int i = 0; i < k; i++)
        {
            double best = double.NegativeInfinity; int bx = -1, by = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (work[x, y] > best) { best = work[x, y]; bx = x; by = y; }
            if (bx < 0) break;

            peaks.Add(new FoundSource(origin + bx * step, origin + by * step, best));
            // Blank a disk of radius minSeparation so the next peak is a DISTINCT source.
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double dx = x - bx, dy = y - by;
                    if (dx * dx + dy * dy <= blankR2) work[x, y] = double.NegativeInfinity;
                }
        }
        return peaks.ToArray();
    }

    /// <summary>One source localized in 3D: lateral (mm) + estimated distance from the detector (mm).</summary>
    public sealed record DepthPeak(double Xmm, double Ymm, double Zmm, double Value);

    /// <summary>Per-source DEPTH from a single flood map by refocusing: decode the SAME flood at a sweep of
    /// focal planes, track each source's peak across planes (it drifts laterally with magnification), and take
    /// the plane where its peak is SHARPEST (highest) as that source's distance — a source is in focus only at
    /// its own depth. No new MC: only the decode changes per plane. Returns the K strongest 3D peaks.</summary>
    public static DepthPeak[] LocalizeDepths(DetectorImage flood, SimulationConfig baseCfg,
        ISimulationFactory factory, int k, double zMin, double zMax, int steps,
        double gateMm = 4.0, double minSeparationMm = 2.5)
    {
        double d = baseCfg.Geometry.MaskDetectorDistanceMm;
        double gate2 = gateMm * gateMm;

        // Peaks at each focal plane. The metric is peak SNR = (peak − mean)/std of the reconstruction, which is
        // comparable across planes (raw peak height is NOT — the recon grid and scale change with the plane).
        // A source is a SHARP (high-SNR) peak only at its own depth; off-focus it smears to a low-SNR blob.
        var perFocal = new List<(double z, List<DepthPeak> peaks)>(steps);
        for (int s = 0; s < steps; s++)
        {
            double z = steps > 1 ? zMin + (zMax - zMin) * s / (steps - 1) : zMin;
            var cfg = baseCfg.Clone();
            cfg.Geometry.SourceMaskDistanceMm = Math.Max(1.0, z - d);
            double frac = d / Math.Max(d + 1.0, z);
            cfg.Decoder.ReconHalfExtentMm = 0.95 * cfg.Mask.Rank * cfg.Mask.CellPitchMm / frac / 2.0;
            cfg.Decoder.ReconStepMm = Math.Max(0.2, cfg.Mask.CellPitchMm / frac / 4.0);
            var dec = factory.CreateDecoder(cfg)!.Decode(flood);
            var recon = dec.Reconstruction;

            double sum = 0, sum2 = 0;
            int n = recon.Width * recon.Height;
            foreach (var v in recon.Raw) { sum += v; sum2 += v * v; }
            double mean = sum / n;
            double std = Math.Sqrt(Math.Max(1e-9, sum2 / n - mean * mean));

            var dps = TopPeaks(recon, dec.ReconOriginMm, dec.ReconStepMm, k, minSeparationMm)
                .Select(p => new DepthPeak(p.Xmm, p.Ymm, z, (p.Value - mean) / std))
                .ToList();
            perFocal.Add((z, dps));
        }

        // Greedy tracking: link each plane's peaks to the nearest open track head (sharpest peaks claim first).
        var tracks = new List<List<DepthPeak>>();
        foreach (var (z, peaks) in perFocal)
        {
            var used = new HashSet<List<DepthPeak>>();
            foreach (var cand in peaks.OrderByDescending(q => q.Value))
            {
                List<DepthPeak>? best = null;
                double bestD = gate2;
                foreach (var t in tracks)
                {
                    if (used.Contains(t)) continue;
                    var h = t[^1];
                    double dd = (h.Xmm - cand.Xmm) * (h.Xmm - cand.Xmm) + (h.Ymm - cand.Ymm) * (h.Ymm - cand.Ymm);
                    if (dd < bestD) { bestD = dd; best = t; }
                }
                if (best != null) { best.Add(cand); used.Add(best); }
                else tracks.Add([cand]);
            }
        }

        // Each track's sharpest plane is that source's depth; keep the K strongest tracks.
        return tracks
            .Select(t => t.Aggregate((a, b) => b.Value > a.Value ? b : a))
            .OrderByDescending(p => p.Value)
            .Take(k)
            .ToArray();
    }

    /// <summary>The single focal plane that best focuses the STRONGEST source in a flood: decode across a
    /// sweep and return the plane where the top peak's SNR is highest. Used for auto-focus — a source at an
    /// unknown distance is brought into focus without the user knowing its depth.</summary>
    public static double BestFocalMm(DetectorImage flood, SimulationConfig baseCfg, ISimulationFactory factory,
                                     double zMin, double zMax, int steps, double minSeparationMm = 2.5)
    {
        double d = baseCfg.Geometry.MaskDetectorDistanceMm;
        double bestFocal = zMin, bestSnr = double.NegativeInfinity;
        for (int i = 0; i < steps; i++)
        {
            double z = steps > 1 ? zMin + (zMax - zMin) * i / (steps - 1) : zMin;
            var cfg = baseCfg.Clone();
            cfg.Geometry.SourceMaskDistanceMm = Math.Max(1.0, z - d);
            double frac = d / Math.Max(d + 1.0, z);
            cfg.Decoder.ReconHalfExtentMm = 0.95 * cfg.Mask.Rank * cfg.Mask.CellPitchMm / frac / 2.0;
            cfg.Decoder.ReconStepMm = Math.Max(0.2, cfg.Mask.CellPitchMm / frac / 4.0);
            var dec = factory.CreateDecoder(cfg)!.Decode(flood);
            var recon = dec.Reconstruction;
            double sum = 0, sum2 = 0;
            int n = recon.Width * recon.Height;
            foreach (var v in recon.Raw) { sum += v; sum2 += v * v; }
            double mean = sum / n, std = Math.Sqrt(Math.Max(1e-9, sum2 / n - mean * mean));
            var pk = TopPeaks(recon, dec.ReconOriginMm, dec.ReconStepMm, 1, minSeparationMm);
            double snr = pk.Length > 0 ? (pk[0].Value - mean) / std : 0.0;
            if (snr > bestSnr) { bestSnr = snr; bestFocal = z; }
        }
        return bestFocal;
    }

    /// <summary>Result of comparing the external rangefinder range against depth-from-focus: the image-sharpest
    /// plane, the peak SNR at the laser plane vs the sharpest plane, and the sharpness-curve FWHM (depth of
    /// field = how trustworthy the focus estimate is — narrow near, huge far).</summary>
    public sealed record FocusCheck(double LaserMm, double BestFocalMm, double LaserSnr, double BestSnr, double FwhmMm);

    /// <summary>Cross-check the laser range with depth-from-focus. The laser gives an absolute range but to the
    /// surface it HIT; the coded-aperture sharpness measures the SOURCE's own distance. If the image is clearly
    /// sharper at a different plane, the source is not on the laser surface — near, the focus refines it; far,
    /// the depth of field is too wide to refine (only flag it).</summary>
    public static FocusCheck CheckFocus(DetectorImage flood, SimulationConfig baseCfg, ISimulationFactory factory,
                                        double laserMm, double zMin, double zMax, int steps)
    {
        double d = baseCfg.Geometry.MaskDetectorDistanceMm;
        var focal = new double[steps];
        var snr = new double[steps];
        for (int i = 0; i < steps; i++)
        {
            double z = steps > 1 ? zMin + (zMax - zMin) * i / (steps - 1) : zMin;
            focal[i] = z;
            var cfg = baseCfg.Clone();
            cfg.Geometry.SourceMaskDistanceMm = Math.Max(1.0, z - d);
            double frac = d / Math.Max(d + 1.0, z);
            cfg.Decoder.ReconHalfExtentMm = 0.95 * cfg.Mask.Rank * cfg.Mask.CellPitchMm / frac / 2.0;
            cfg.Decoder.ReconStepMm = Math.Max(0.2, cfg.Mask.CellPitchMm / frac / 4.0);
            var dec = factory.CreateDecoder(cfg)!.Decode(flood);
            var recon = dec.Reconstruction;
            double sum = 0, s2 = 0;
            int n = recon.Width * recon.Height;
            foreach (var v in recon.Raw) { sum += v; s2 += v * v; }
            double mean = sum / n, std = Math.Sqrt(Math.Max(1e-9, s2 / n - mean * mean));
            var pk = TopPeaks(recon, dec.ReconOriginMm, dec.ReconStepMm, 1, 2.5);
            snr[i] = pk.Length > 0 ? (pk[0].Value - mean) / std : 0.0;
        }
        int ib = 0;
        for (int i = 1; i < steps; i++) if (snr[i] > snr[ib]) ib = i;
        int il = 0;
        double bd = double.MaxValue;
        for (int i = 0; i < steps; i++) { double dd = Math.Abs(focal[i] - laserMm); if (dd < bd) { bd = dd; il = i; } }
        return new FocusCheck(laserMm, focal[ib], snr[il], snr[ib], FwhmOf(focal, snr));
    }

    private static double FwhmOf(double[] x, double[] y)
    {
        int im = 0;
        for (int i = 1; i < y.Length; i++) if (y[i] > y[im]) im = i;
        double max = y[im], baseline = y.Min(), half = baseline + (max - baseline) / 2.0;
        if (max <= baseline) return x[^1] - x[0];
        double xl = x[0];
        for (int i = im; i > 0; i--) if (y[i - 1] <= half) { xl = Lerp(y[i - 1], x[i - 1], y[i], x[i], half); break; }
        double xr = x[^1];
        for (int i = im; i < y.Length - 1; i++) if (y[i + 1] <= half) { xr = Lerp(y[i + 1], x[i + 1], y[i], x[i], half); break; }
        return Math.Max(0.0, xr - xl);
    }

    private static double Lerp(double y0, double x0, double y1, double x1, double yt) =>
        y1 == y0 ? x0 : x0 + (x1 - x0) * (yt - y0) / (y1 - y0);

    public static string ToCsv(MixedFieldResult r)
    {
        var sb = new System.Text.StringBuilder("kind,index,x_mm,y_mm,value\n");
        for (int i = 0; i < r.TruthXY.Length; i++)
            sb.Append($"truth,{i},{r.TruthXY[i][0]:F2},{r.TruthXY[i][1]:F2},\n");
        for (int i = 0; i < r.Found.Length; i++)
            sb.Append($"found,{i},{r.Found[i].Xmm:F2},{r.Found[i].Ymm:F2},{r.Found[i].Value:F4}\n");
        return sb.ToString();
    }
}
