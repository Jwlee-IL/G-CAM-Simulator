using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Services;

/// <summary>Search v3 = v2 ∪ {candidate seeded by Studio's focus sweep}. Run as an incremental pass over v2 rows: the data
/// are regenerated from the seed (deterministic), the sweep-seeded candidate is optimised on the coarse model, and if it is
/// a distinct optimum from v2's answer it is refined on the full model; the larger likelihood wins. No truth is used.</summary>
static class V3
{
    /// <summary>Studio's sweep: 81 inverse-distance planes 110…3000 mm, non-cyclic decode, K = 1, prominence = (peak − mean)/std.</summary>
    public static (double plane, double px, double py) Sweep(double[] flood)
    {
        var planes = FocusSweepMath.Planes(G.D);
        var cfg = SceneConfigBuilder.Build([new SceneSource { DistanceMm = planes[0] }], G.Opt, 1);
        cfg.Decoder.Cyclic = false;
        var im = new DetectorImage(G.NPix, G.NPix);
        for (int i = 0; i < flood.Length; i++) im[i % G.NPix, i / G.NPix] = flood[i];
        double best = double.NegativeInfinity, bp = planes[0], bx = 0, by = 0;
        foreach (double plane in planes)
        {
            var p = ImagingProjection.AtFocus(cfg, G.Opt, plane);
            var d = new DefaultSimulationFactory().CreateDecoder(p)!.Decode(im);
            var v = d.Reconstruction.Raw.ToArray(); double mean = v.Average(), sd = Math.Sqrt(v.Select(x => (x - mean) * (x - mean)).Average());
            if (sd == 0) continue;
            double prom = (v.Max() - mean) / sd;
            if (prom > best) { best = prom; bp = plane; bx = d.Estimate.Position.X / (plane - G.D); by = d.Estimate.Position.Y / (plane - G.D); }
        }
        return (bp, bx, by);
    }

    // v3 <N> <nCoarse> <kCoarse> <v2csv> <tag> [dop]
    public static void Run(string[] a)
    {
        int N = int.Parse(a[1]), nc = int.Parse(a[2]), kc = int.Parse(a[3]); string src = a[4], tag = a[5];
        int dop = a.Length > 6 ? int.Parse(a[6]) : 8;
        var h = new Histories(N, 1);
        var rows = File.ReadAllLines(src); var hdr = rows[0].Split(',');
        var recs = rows.Skip(1).Where(l => l.StartsWith("FIT,")).Select(l => hdr.Zip(l.Split(',')).ToDictionary(p => p.First, p => p.Second)).ToList();
        var floods = recs.GroupBy(r => (r["z"], r["a_mrad"], r["live_s"], r["seed"])).ToList();
        var outPath = Path.Combine(AppContext.BaseDirectory, $"fit_{tag}.csv");
        var done = File.Exists(outPath) ? File.ReadAllLines(outPath).Skip(1).Select(l => string.Join(",", l.Split(',').Skip(2).Take(5))).ToHashSet() : [];
        bool fresh = !File.Exists(outPath);
        var lk = new object(); using var w = new StreamWriter(outPath, append: true);
        if (fresh) { w.WriteLine(Runs.Header + ",v3_sweep_plane,v3_sweep_phix_mrad,v3_sweep_phiy_mrad,v3_cand_z,v3_cand_phix,v3_cand_phiy,v3_distinct,v3_refined_LL,v3_switched,v3_seconds"); w.Flush(); }
        var models = new System.Collections.Concurrent.ConcurrentBag<Model>();
        Parallel.ForEach(floods, new ParallelOptions { MaxDegreeOfParallelism = dop }, g =>
        {
            var r0 = g.First();
            double z = double.Parse(r0["z"]), an = double.Parse(r0["a_mrad"]), T = double.Parse(r0["live_s"]); int seed = int.Parse(r0["seed"]);
            if (done.Contains(string.Join(",", r0["z"], r0["a_mrad"], r0["live_s"], r0["seed"], g.First()["channel"]))) return;
            if (!models.TryTake(out var m)) m = new Model(h);
            var c = new Runs.Cond(z, an, T);
            double xt = z * Math.Tan(an / 1000);
            var (all, win, hist) = G.Acquire(xt, 0, z, seed, T);
            var lines = new List<string>();
            foreach (var r in g)
            {
                var sw = Stopwatch.StartNew();
                bool isWin = r["channel"] == "win"; var n = isWin ? win : all;
                if (Math.Abs(n.Sum() - double.Parse(r["counts"])) > 0.5) throw new InvalidOperationException("data regeneration mismatch");
                var f = new Fitter(m, nc, kc, true);
                var (plane, sx, sy) = Sweep(n);
                // coarse depth scan at the sweep bearing (same grid origin rule as v2), then a coarse-model 3-D Nelder–Mead
                double off = new Random(seed * 31 + 7).NextDouble(), dv = (Fitter.VHi - Fitter.VLo) / kc; double bestL = double.NegativeInfinity, bestV = 0;
                for (int k = 0; k < kc; k++) { double v = Fitter.VLo + (k + off) * dv, l = f.LL(n, isWin, sx, sy, v, nc); if (l > bestL) { bestL = l; bestV = v; } }
                var (pc, _, _) = Fitter.NelderMead(q => f.LL(n, isWin, q[0] * Fitter.SPhi, q[1] * Fitter.SPhi, q[2] * Fitter.SV, nc), [sx / Fitter.SPhi, sy / Fitter.SPhi, bestV / Fitter.SV], [3, 3, 3], 300);
                double v2px = double.Parse(r["phix_mrad"]) / 1000, v2py = double.Parse(r["phiy_mrad"]) / 1000, v2v = 1 / (double.Parse(r["zhat"]) - G.D);
                double cpx = pc[0] * Fitter.SPhi, cpy = pc[1] * Fitter.SPhi, cv = pc[2] * Fitter.SV;
                bool distinct = Math.Abs(cpx - v2px) > 1e-3 || Math.Abs(cpy - v2py) > 1e-3 || Math.Abs(cv - v2v) > 2e-4;
                string line = string.Join(",", r.Values.Take(hdr.Length)); double refinedLL = double.NaN; bool switched = false;
                if (distinct)
                {
                    var res = f.RefineFrom(n, isWin, pc);
                    refinedLL = res.LL;
                    if (res.LL > double.Parse(r["LLhat3d"]))
                    {
                        switched = true;
                        line = Runs.Finish(m, f, c, seed, n, isWin, r["channel"], res, double.Parse(r["decode_phix_mrad"]) / 1000, double.Parse(r["decode_phiy_mrad"]) / 1000, tag, sw) + $",{hist}";
                    }
                }
                lines.Add(line + $",{plane:F2},{sx * 1000:F4},{sy * 1000:F4},{G.D + 1 / cv:F2},{cpx * 1000:F4},{cpy * 1000:F4},{(distinct ? 1 : 0)},{refinedLL:F4},{(switched ? 1 : 0)},{sw.Elapsed.TotalSeconds:F1}");
            }
            lock (lk) { foreach (var l in lines) { w.WriteLine(l); Console.WriteLine(l); } w.Flush(); }
            models.Add(m);
        });
    }
}
