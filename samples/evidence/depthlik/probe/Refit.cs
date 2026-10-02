using System.Diagnostics;
/// <summary>Budget test without re-searching: regenerate the data, refine from the earlier estimate with a model of N
/// histories (and model stream ms), then the same matched-profile Λ, Hessian width and nuisances.</summary>
static class Refit
{
    // refit <N> <modelSeed> <src.csv> <z filter or *> <tag> [dop]
    public static void Run(string[] a)
    {
        int N = int.Parse(a[1]), ms = int.Parse(a[2]); string src = a[3], zf = a[4], tag = a[5]; int dop = a.Length > 6 ? int.Parse(a[6]) : 8;
        var h = new Histories(N, ms);
        var rows = File.ReadAllLines(src); var hdr = rows[0].Split(',');
        var recs = rows.Skip(1).Select(l => hdr.Zip(l.Split(',')).ToDictionary(p => p.First, p => p.Second)).Where(r => zf == "*" || r["z"] == zf).ToList();
        var outPath = Path.Combine(AppContext.BaseDirectory, $"fit_{tag}.csv");
        bool fresh = !File.Exists(outPath); var lk = new object(); using var w = new StreamWriter(outPath, append: true);
        if (fresh) { w.WriteLine(Runs.Header); w.Flush(); }
        var models = new System.Collections.Concurrent.ConcurrentBag<Model>();
        Parallel.ForEach(recs.GroupBy(r => (r["z"], r["a_mrad"], r["live_s"], r["seed"])), new ParallelOptions { MaxDegreeOfParallelism = dop }, g =>
        {
            if (!models.TryTake(out var m)) m = new Model(h);
            var r0 = g.First(); double z = double.Parse(r0["z"]), an = double.Parse(r0["a_mrad"]), T = double.Parse(r0["live_s"]); int seed = int.Parse(r0["seed"]);
            var (all, win, hist) = G.Acquire(z * Math.Tan(an / 1000), 0, z, seed, T);
            var lines = new List<string>();
            foreach (var r in g)
            {
                var sw = Stopwatch.StartNew(); bool isWin = r["channel"] == "win"; var n = isWin ? win : all;
                var f = new Fitter(m, 90000, 140, true);
                double v = 1 / (double.Parse(r["zhat"]) - G.D);
                var res = f.RefineFrom(n, isWin, [double.Parse(r["phix_mrad"]) / 1000 / Fitter.SPhi, double.Parse(r["phiy_mrad"]) / 1000 / Fitter.SPhi, v / Fitter.SV]);
                lines.Add(Runs.Finish(m, f, new Runs.Cond(z, an, T), seed, n, isWin, r["channel"], res, double.NaN, double.NaN, tag, sw) + $",{hist}");
            }
            lock (lk) { foreach (var l in lines) { w.WriteLine(l); Console.WriteLine(l); } w.Flush(); }
            models.Add(m);
        });
    }
}
