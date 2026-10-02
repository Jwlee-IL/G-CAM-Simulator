using System.Diagnostics;

/// <summary>Joint (φx, φy, v) fit; φ = lateral / (z − D) (bearing seen from the mask), v = 1 / (z − D) (shadow
/// magnification − 1 = D·v). Intensity and flat background profiled analytically inside Lik.Profile.</summary>
sealed class Fitter
{
    public const double SPhi = 1e-4, SV = 2e-5;              // optimizer scaling only (not tolerances on results)
    public static readonly double VLo = 1 / (3000 - G.D), VHi = 1 / (110 - G.D);   // Studio's sweep range 110…3000 mm
    readonly Model _m; readonly int _nCoarse, _kCoarse; readonly bool _bg;
    readonly double[] _a = new double[900], _w = new double[900];
    public Fitter(Model m, int nCoarse, int kCoarse, bool background) { _m = m; _nCoarse = nCoarse; _kCoarse = kCoarse; _bg = background; }

    public double LL(double[] n, bool win, double px, double py, double v, int nUse = int.MaxValue)
    {
        if (!(v > 0) || v > 1 / 20.0) return double.NegativeInfinity;
        _m.Eval(px / v, py / v, G.D + 1 / v, _a, _w, nUse);
        return Lik.Profile(n, win ? _w : _a, _bg, out _, out _);
    }
    public (double A, double B) Nuisance(double[] n, bool win, double px, double py, double v)
    {
        _m.Eval(px / v, py / v, G.D + 1 / v, _a, _w);
        Lik.Profile(n, win ? _w : _a, _bg, out double A, out double B); return (A, B);
    }

    public const double CoarseMargin = 25;   // log-likelihood units on the coarse model
    public double CoarseGap, CoarseAltV;
    public sealed record Result(double Px, double Py, double V, double LL, int Evals, double CoarseV, double Alt, double AltLL)
    { public double[,]? Cov { get; init; } public double Rms { get; init; } public bool Ok { get; init; } }

    /// <summary>Two rounds of a 3^3 quadratic response surface in scaled coords (round 2 sized from round 1's Hessian).</summary>
    (double[] x, double f, double[,]? cov, double rms, bool ok, int evals) Surface(double[] n, bool win, double[] start)
    {
        Func<double[], double> F = q => LL(n, win, q[0] * SPhi, q[1] * SPhi, q[2] * SV);
        var h = new[] { 2.0, 2.0, 2.0 }; var c = start; int ev = 0;
        (double[] x, double f, double[,] H, double rms, bool ok) r = default;
        for (int round = 0; round < 4; round++)
        {
            r = Rsm.Fit(F, c, h); ev += 27;
            if (!r.ok) { h = h.Select(v => v * 2).ToArray(); continue; }
            var cov = Rsm.Inverse(r.H, 3)!; var sig = new double[3];
            for (int j = 0; j < 3; j++) sig[j] = Math.Sqrt(-cov[j, j]);
            bool inside = true; for (int j = 0; j < 3; j++) inside &= Math.Abs(r.x[j] - c[j]) <= h[j];
            var hn = sig.Select(v => Math.Clamp(2.5 * v, 0.3, 60)).ToArray();
            bool sized = Enumerable.Range(0, 3).All(j => h[j] / hn[j] is > 0.6 and < 1.7);
            c = r.x; h = hn;
            if (round >= 1 && inside && sized) break;
        }
        if (!r.ok)
        {   // fallback: full-model Nelder–Mead from the coarse-model optimum (flagged surf_ok = 0)
            var (pn, fn, en) = NelderMead(F, start, [1, 1, 1], 200);
            return (pn, fn, null, double.NaN, false, ev + en);
        }
        var cv = Rsm.Inverse(r.H, 3)!; var covOut = new double[3, 3];
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) covOut[i, j] = -cv[i, j];   // covariance in scaled units
        return (r.x, r.f, covOut, r.rms, true, ev);
    }

    /// <summary>Bearing-profiled LL at fixed v by a 3^2 quadratic surface (one to three rounds).</summary>
    public (double px, double py, double ll, bool ok) ProfileSurface(double[] n, bool win, double v, double px0, double py0, double hphi)
    {
        Func<double[], double> F = q => LL(n, win, q[0] * SPhi, q[1] * SPhi, v);
        var c = new[] { px0 / SPhi, py0 / SPhi }; var h = new[] { hphi, hphi };
        (double[] x, double f, double[,] H, double rms, bool ok) r = default;
        for (int round = 0; round < 3; round++)
        {
            r = Rsm.Fit(F, c, h);
            if (!r.ok) { h = h.Select(x => x * 2).ToArray(); continue; }
            bool inside = Math.Abs(r.x[0] - c[0]) <= h[0] && Math.Abs(r.x[1] - c[1]) <= h[1];
            c = r.x; if (inside) break;
        }
        if (!r.ok) { var (pn, fn, _) = NelderMead(F, [px0 / SPhi, py0 / SPhi], [1, 1], 120); return (pn[0] * SPhi, pn[1] * SPhi, fn, false); }
        return (c[0] * SPhi, c[1] * SPhi, r.f, r.ok);
    }

    public double Px1, Py1, ZC0;
    public Result Fit(double[] n, bool win, double px0, double py0, double gridOffset)
    {
        // Coarse: uniform in v over the whole sweep range at the bearing Studio's decoder finds at 1000 mm (no truth),
        // grid origin offset per data set.
        double dv = (VHi - VLo) / _kCoarse; var L = new double[_kCoarse]; var V = new double[_kCoarse];
        for (int k = 0; k < _kCoarse; k++) { V[k] = VLo + (k + gridOffset) * dv; L[k] = LL(n, win, px0, py0, V[k], _nCoarse); }
        ZC0 = G.D + 1 / V[Array.IndexOf(L, L.Max())];
        // v2: the bearing seed from a wrong plane can be off by several mrad, and at a wrong bearing the coarse depth
        // ranking is unreliable. So each of the top three coarse depth maxima gets its own bearing (Studio's decode at
        // that plane, no truth) and a coarse-model 3-D Nelder–Mead; the margin is applied after that optimisation.
        var top = Enumerable.Range(0, _kCoarse).Where(k => (k == 0 || L[k] >= L[k - 1]) && (k == _kCoarse - 1 || L[k] >= L[k + 1]))
            .OrderByDescending(k => L[k]).Take(3).ToArray();
        var starts = new List<(double[] p, double ll, double v)>(); int evC = 0;
        foreach (int k in top)
        {
            var (dx, dy) = G.DecodeBearing(n, Math.Clamp(Math.Round(G.D + 1 / V[k]), 120, 2900));
            var (pc, fc, ec) = NelderMead(q => LL(n, win, q[0] * SPhi, q[1] * SPhi, q[2] * SV, _nCoarse), [dx / SPhi, dy / SPhi, V[k] / SV], [3, 3, 3], 300);
            starts.Add((pc, fc, V[k])); evC += ec;
            if (k == top[0]) { Px1 = dx; Py1 = dy; }
        }
        starts = starts.OrderByDescending(t => t.ll).ToList();
        CoarseGap = starts.Count > 1 ? starts[0].ll - starts[1].ll : double.NaN;
        CoarseAltV = starts.Count > 1 ? starts[1].p[2] * SV : double.NaN;
        var refine = starts.Where(t => starts[0].ll - t.ll <= CoarseMargin).Take(2).ToList();
        Result? best = null; double altV = double.NaN, altLL = double.NaN; int evals = 0;
        foreach (var st in refine)
        {
            var (p, f, cov, rms, ok, e) = Surface(n, win, st.p);
            evals += e;
            var r = new Result(p[0] * SPhi, p[1] * SPhi, p[2] * SV, f, 0, st.v, 0, 0) { Cov = cov, Rms = rms, Ok = ok };
            if (best is null || r.LL > best.LL) { if (best is not null) { altV = best.V; altLL = best.LL; } best = r; }
            else { altV = r.V; altLL = r.LL; }
        }
        evals += evC;
        return best! with { Evals = evals + _kCoarse, Alt = altV, AltLL = altLL };
    }

    /// <summary>Full-model response-surface refine from a coarse optimum (used by search v3).</summary>
    public Result RefineFrom(double[] n, bool win, double[] start)
    {
        var (p, f, cov, rms, ok, _) = Surface(n, win, start);
        Px1 = Py1 = ZC0 = CoarseGap = CoarseAltV = double.NaN;
        return new Result(p[0] * SPhi, p[1] * SPhi, p[2] * SV, f, 0, start[2] * SV, double.NaN, double.NaN) { Cov = cov, Rms = rms, Ok = ok };
    }

    /// <summary>Profile over bearing at fixed v.</summary>
    public (double px, double py, double ll) ProfileAt(double[] n, bool win, double v, double px0, double py0)
    {
        var (p, f, _) = NelderMead(q => LL(n, win, q[0] * SPhi, q[1] * SPhi, v), [px0 / SPhi, py0 / SPhi], [1, 1], 100);
        return (p[0] * SPhi, p[1] * SPhi, f);
    }

    public static (double[] x, double f, int evals) NelderMead(Func<double[], double> f, double[] x0, double[] step, int maxEval)
    {
        int d = x0.Length; var pts = new double[d + 1][]; var val = new double[d + 1]; int ev = 0;
        double F(double[] x) { ev++; return -f(x); }      // minimise −LL
        for (int i = 0; i <= d; i++) { pts[i] = (double[])x0.Clone(); if (i > 0) pts[i][i - 1] += step[i - 1]; val[i] = F(pts[i]); }
        while (ev < maxEval)
        {
            var idx = Enumerable.Range(0, d + 1).OrderBy(i => val[i]).ToArray();
            pts = idx.Select(i => pts[i]).ToArray(); val = idx.Select(i => val[i]).ToArray();
            double size = 0; for (int i = 1; i <= d; i++) for (int j = 0; j < d; j++) size = Math.Max(size, Math.Abs(pts[i][j] - pts[0][j]));
            if (val[d] - val[0] < 3e-3 && size < 0.05) break;
            var c = new double[d]; for (int i = 0; i < d; i++) for (int j = 0; j < d; j++) c[j] += pts[i][j] / d;
            double[] At(double t) { var r = new double[d]; for (int j = 0; j < d; j++) r[j] = c[j] + t * (pts[d][j] - c[j]); return r; }
            var xr = At(-1); double fr = F(xr);
            if (fr < val[0]) { var xe = At(-2); double fe = F(xe); if (fe < fr) { pts[d] = xe; val[d] = fe; } else { pts[d] = xr; val[d] = fr; } }
            else if (fr < val[d - 1]) { pts[d] = xr; val[d] = fr; }
            else
            {
                var xc = fr < val[d] ? At(-0.5) : At(0.5); double fc = F(xc);
                if (fc < Math.Min(fr, val[d])) { pts[d] = xc; val[d] = fc; }
                else for (int i = 1; i <= d; i++) { for (int j = 0; j < d; j++) pts[i][j] = pts[0][j] + 0.5 * (pts[i][j] - pts[0][j]); val[i] = F(pts[i]); }
            }
        }
        int b = Array.IndexOf(val, val.Min());
        return (pts[b], -val[b], ev);
    }
}

static class Runs
{
    public sealed record Cond(double Z, double A, double T);

    /// <summary>One flood: fit both channels, profile at truth, curvature width. Returns CSV lines.</summary>
    public static List<string> FitFlood(Model m, Cond c, int seed, double[] all, double[] win, int nCoarse, int kCoarse, bool bg, string tag)
    {
        var lines = new List<string>(); var f = new Fitter(m, nCoarse, kCoarse, bg);
        double xt = c.Z * Math.Tan(c.A / 1000), vt = 1 / (c.Z - G.D), pxt = xt * vt;
        var off = new Random(seed * 31 + 7).NextDouble();
        foreach (var (ch, n, isWin) in new[] { ("all", all, false), ("win", win, true) })
        {
            var sw = Stopwatch.StartNew(); long e0 = m.Evals;
            var (px0, py0) = G.DecodeBearing(n);
            var r = f.Fit(n, isWin, px0, py0, off);
            lines.Add(Finish(m, f, c, seed, n, isWin, ch, r, px0, py0, tag, sw, e0));
        }
        return lines;
    }

    /// <summary>Post-fit: matched bearing profiles at the estimate and at the truth (for Λ), Hessian width, nuisances; one CSV row.</summary>
    public static string Finish(Model m, Fitter f, Cond c, int seed, double[] n, bool isWin, string ch, Fitter.Result r, double px0, double py0, string tag, Stopwatch sw, long e0 = -1)
    {
        if (e0 < 0) e0 = m.Evals;
        double vt = 1 / (c.Z - G.D);
        // conditional bearing at v_t from the 3-D quadratic, then a 2-D surface there
        double sigV = double.NaN, sigPx = 1, wald = double.NaN; double cpx = r.Px, cpy = r.Py;
        if (r.Cov is { } C)
        {
            double dvu = (vt - r.V) / Fitter.SV;
            cpx += C[0, 2] / C[2, 2] * dvu * Fitter.SPhi; cpy += C[1, 2] / C[2, 2] * dvu * Fitter.SPhi;
            sigV = Math.Sqrt(C[2, 2]) * Fitter.SV; sigPx = Math.Sqrt(Math.Max(C[0, 0] - C[0, 2] * C[0, 2] / C[2, 2], 1e-6));
            wald = dvu * dvu / C[2, 2];
        }
        double hp = Math.Clamp(2.5 * sigPx, 0.3, 60);
        var tr = f.ProfileSurface(n, isWin, vt, cpx, cpy, hp);
        var th = f.ProfileSurface(n, isWin, r.V, r.Px, r.Py, hp);      // same smoothing at the estimate → Λ consistent
        double lam = 2 * (th.ll - tr.ll);
        double zh = G.D + 1 / r.V, sz = sigV / (r.V * r.V);
        var (A, B) = f.Nuisance(n, isWin, r.Px, r.Py, r.V);
        return string.Join(",", "FIT", tag, c.Z, c.A, c.T, seed, ch, n.Sum(), (px0 * 1000).ToString("F4"), (py0 * 1000).ToString("F4"),
            (f.Px1 * 1000).ToString("F4"), (f.Py1 * 1000).ToString("F4"), f.ZC0.ToString("F2"), (G.D + 1 / r.CoarseV).ToString("F2"), zh.ToString("F3"), (r.Px / r.V).ToString("F4"), (r.Py / r.V).ToString("F4"),
            (r.Px * 1000).ToString("F5"), (r.Py * 1000).ToString("F5"), r.LL.ToString("F4"), th.ll.ToString("F4"), lam.ToString("F4"), wald.ToString("F4"),
            (tr.px * 1000).ToString("F5"), (tr.py * 1000).ToString("F5"), sz.ToString("F3"), r.Ok ? "1" : "0", tr.ok && th.ok ? "1" : "0", r.Rms.ToString("F4"),
            double.IsNaN(r.Alt) ? "NaN" : (G.D + 1 / r.Alt).ToString("F2"), double.IsNaN(r.AltLL) ? "NaN" : (r.LL - r.AltLL).ToString("F3"),
            double.IsNaN(f.CoarseAltV) ? "NaN" : (G.D + 1 / f.CoarseAltV).ToString("F2"), f.CoarseGap.ToString("F2"),
            A.ToString("G6"), B.ToString("G6"), (m.Evals - e0).ToString(), sw.Elapsed.TotalSeconds.ToString("F1"));
    }
    public const string Header = "FIT,tag,z,a_mrad,live_s,seed,channel,counts,decode_phix_mrad,decode_phiy_mrad,redecode_phix_mrad,redecode_phiy_mrad,coarse0_z,coarse_z,zhat,xhat,yhat,phix_mrad,phiy_mrad,LLhat3d,LLprof_at_hat,lambda_truth,wald_truth,phix_at_truth,phiy_at_truth,sigma_z_hessian,surf_ok,prof_ok,surf_rms,alt_z,LLhat_minus_alt,coarse_alt_z,coarse_gap,Ahat,Bhat,evals,seconds,hist";

    // fit <N> <nCoarse> <kCoarse> <seedsPerCond> <seedBase> <zlist> <alist> <tlist> <tag> [modelSeed] [bg 1/0] [dop] [seedStart]
    public static void Fit(string[] a)
    {
        int N = int.Parse(a[1]), nc = int.Parse(a[2]), kc = int.Parse(a[3]), S = int.Parse(a[4]), sb = int.Parse(a[5]);
        var zs = a[6].Split(';').Select(double.Parse).ToArray(); var As = a[7].Split(';').Select(double.Parse).ToArray(); var Ts = a[8].Split(';').Select(double.Parse).ToArray();
        string tag = a[9]; int ms = a.Length > 10 ? int.Parse(a[10]) : 1; bool bg = a.Length <= 11 || a[11] == "1";
        int dop = a.Length > 12 ? int.Parse(a[12]) : Environment.ProcessorCount; int s0 = a.Length > 13 ? int.Parse(a[13]) : 0;
        var h = new Histories(N, ms);
        var jobs = (from s in Enumerable.Range(s0, S) from z in zs from an in As from t in Ts select (c: new Cond(z, an, t), s)).ToArray();
        var outPath = Path.Combine(AppContext.BaseDirectory, $"fit_{tag}.csv");
        bool fresh = !File.Exists(outPath);
        var lk = new object(); using var w = new StreamWriter(outPath, append: true);
        if (fresh) { w.WriteLine(Header); w.Flush(); }
        var models = new System.Collections.Concurrent.ConcurrentBag<Model>();
        Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = dop }, j =>
        {
            if (!models.TryTake(out var m)) m = new Model(h);
            // data seed: independent of the model's counter-based streams; distinct per condition, seed index and live time
            int seed = sb + (int)j.c.Z * 1000 + (int)j.c.A * 37 + (int)j.c.T * 7 + j.s * 100003;
            double xt = j.c.Z * Math.Tan(j.c.A / 1000);
            var (all, win, hist) = G.Acquire(xt, 0, j.c.Z, seed, j.c.T);
            var lines = FitFlood(m, j.c, seed, all, win, nc, kc, bg, tag);
            lock (lk) { foreach (var l in lines) { w.WriteLine(l + $",{hist}"); Console.WriteLine(l); } w.Flush(); }
            models.Add(m);
        });
    }
}
