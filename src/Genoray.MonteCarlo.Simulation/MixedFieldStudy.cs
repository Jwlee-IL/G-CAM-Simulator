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
    private static FoundSource[] TopPeaks(DetectorImage recon, double origin, double step,
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
