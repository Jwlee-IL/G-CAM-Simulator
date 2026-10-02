using System.Diagnostics;
using System.Globalization;
using Gcam.Core;
using Gcam.Simulation;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var mode = args.FirstOrDefault() ?? "time";
var outDir = AppContext.BaseDirectory;

if (mode == "time")
{
    var h = new Histories(int.Parse(args[1]), 1);
    var m = new Model(h); var a = new double[900]; var w = new double[900];
    m.Eval(0, 0, 500, a, w);
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < 3; i++) m.Eval(3, -2, 500 + i, a, w);
    Console.WriteLine($"N={h.N} per-eval {sw.Elapsed.TotalMilliseconds / 3:F1} ms; sumAll={a.Sum():G6} sumWin={w.Sum():G6}");
}

if (mode == "maskcheck")
{   // Exact expected transmission vs the engine's Transmit by bisection on the uniform it compares against.
    var h = new Histories(900, 3); var m = new Model(h);
    var mask = new DefaultSimulationFactory().CreateMask(G.Base);
    var r = new Random(5); double worst = 0; int n = 0;
    foreach (double z in new[] { 150.0, 300, 700, 2000 })
        for (int k = 0; k < 5000; k++)
        {
            var s = new Vector3((r.NextDouble() - .5) * z * 0.12, (r.NextDouble() - .5) * z * 0.12, z);
            var p = new Vector3((r.NextDouble() - .5) * 18, (r.NextDouble() - .5) * 18, 0);
            var d = p - s; d = d * (1 / d.Length);
            foreach (int l in new[] { 0, 1 })
            {
                double lo = 0, hi = 1;
                for (int b = 0; b < 60; b++) { double mid = (lo + hi) / 2; if (mask.Transmit(new Ray(s, d), h.E[l], new Const(mid))) lo = mid; else hi = mid; }
                worst = Math.Max(worst, Math.Abs(lo - m.MaskT(s, d, h.MuRel[l]))); n++;
            }
        }
    Console.WriteLine($"MASKCHECK rays={n} max|T_model - T_engine|={worst:E3}");
}

if (mode == "validate") Modes.Validate(args);
if (mode == "scan") Modes.Scan(args);
if (mode == "fit") Runs.Fit(args);
if (mode == "rough") Rough.Run(args);
if (mode == "interp") Interp.Run(args);
if (mode == "v3") V3.Run(args);
if (mode == "refit") Refit.Run(args);
if (mode == "cost")
{   // single-thread cost of complete fits (both channels) on a quiet core: histories transported and wall time
    int N = int.Parse(args[1]); var h = new Histories(N, 1); var m = new Model(h);
    foreach (var (z, a) in new[] { (300.0, 0.0), (1000.0, 30.0) })
        foreach (double T in new[] { 10.0, 60.0 })
        {
            double x = z * Math.Tan(a / 1000); int seed = 777001 + (int)z + (int)T;
            var (all, win, _) = G.Acquire(x, 0, z, seed, T);
            long h0 = m.HistoriesTransported; var sw = Stopwatch.StartNew();
            var lines = Runs.FitFlood(m, new Runs.Cond(z, a, T), seed, all, win, 90000, 140, true, "cost");
            Console.WriteLine($"COST,{z},{a},{T},histories={m.HistoriesTransported - h0},seconds={sw.Elapsed.TotalSeconds:F1},us_per_history={sw.Elapsed.TotalMilliseconds * 1000 / (m.HistoriesTransported - h0):F3}");
        }
}

sealed class Const(double u) : IRandom
{
    public double NextDouble() => u;
    public Vector3 NextOnUnitSphere() => new(0, 0, 1);
}

static class Modes
{
    public static void Validate(string[] args)
    {   // model mean vs long independent list-mode acquisitions: Pearson chi2 per channel.
        int N = int.Parse(args[1]);
        var h1 = new Histories(N, 1); var h2 = new Histories(N, 2);
        var pts = new (double z, double a)[] { (300, 0), (500, 15), (1000, 30) };
        Parallel.ForEach(pts, pt =>
        {
            double x = pt.z * Math.Tan(pt.a / 1000);
            var m1 = new Model(h1); var m2 = new Model(h2);
            double[] a1 = new double[900], w1 = new double[900], a2 = new double[900], w2 = new double[900];
            m1.Eval(x, 0, pt.z, a1, w1); m2.Eval(x, 0, pt.z, a2, w2);
            double live = pt.z == 300 ? 300 : pt.z == 500 ? 600 : 1800;
            var (all, win, hist) = G.Acquire(x, 0, pt.z, 990001 + (int)pt.z, live);
            foreach (var (ch, n, m, mm) in new[] { ("all", all, a1, a2), ("win", win, w1, w2) })
            {
                double chi = Chi(n, m), chiModel = ChiModels(m, mm);
                Console.WriteLine($"VALIDATE,{pt.z},{pt.a},{ch},live={live},counts={n.Sum()},hist={hist},chi2/dof_data_vs_model1={chi:F4},chi2/dof_model1_vs_model2={chiModel:F4},model_eff_counts={EffCounts(m, mm):G4}");
            }
        });
    }
    static double Chi(double[] n, double[] m)
    {
        double A = n.Sum() / m.Sum(), c = 0; for (int i = 0; i < n.Length; i++) { double e = A * m[i]; c += (n[i] - e) * (n[i] - e) / e; }
        return c / (n.Length - 1);
    }
    // two independent model streams: Var(m1-m2) ≈ 2 m²/neff_i; returns chi2 assuming each pixel neff from the pair spread
    static double ChiModels(double[] a, double[] b)
    {
        double sa = a.Sum(), sb = b.Sum(), c = 0;
        // treat as weighted-count comparison: scale both to unit sum; this returns mean relative squared difference × total
        for (int i = 0; i < a.Length; i++) { double pa = a[i] / sa, pb = b[i] / sb; c += (pa - pb) * (pa - pb) / ((pa + pb) / 2); }
        return c;
    }
    // effective counts of one model stream: E[sum (pa-pb)^2/p] = 2*(P-1)/neff → neff
    static double EffCounts(double[] a, double[] b) => 2 * (a.Length - 1) / ChiModels(a, b);

    public static void Scan(string[] args)
    {   // LL along depth at the true bearing, both channels, one 60 s acquisition per point.
        int N = int.Parse(args[1]);
        var h = new Histories(N, 1);
        double vlo = 1 / (3000 - G.D), vhi = 1 / (110 - G.D); int K = int.Parse(args[2]);
        var pts = new (double z, double a)[] { (300, 0), (500, 0), (700, 0), (1000, 0), (500, 30), (1000, 30) };
        Parallel.ForEach(pts, new ParallelOptions { MaxDegreeOfParallelism = 6 }, pt =>
        {
            double x = pt.z * Math.Tan(pt.a / 1000);
            var (all, win, _) = G.Acquire(x, 0, pt.z, 43001, 60);
            var md = new Model(h); double[] ma = new double[900], mw = new double[900];
            double phi = x / (pt.z - G.D);
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k <= K; k++)
            {
                double v = vlo + (vhi - vlo) * k / K, z = G.D + 1 / v;
                md.Eval(phi / v, 0, z, ma, mw);
                double la = Lik.Profile(all, ma, true, out _, out double ba), lw = Lik.Profile(win, mw, true, out _, out double bw);
                sb.AppendLine($"SCAN,{pt.z},{pt.a},{v:G6},{z:F2},{la:F3},{lw:F3},{ba:G4},{bw:G4}");
            }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, $"scan_{pt.z}_{pt.a}.csv"), sb.ToString());
            Console.WriteLine($"done {pt.z} {pt.a} counts {all.Sum()} {win.Sum()}");
        });
    }
}
