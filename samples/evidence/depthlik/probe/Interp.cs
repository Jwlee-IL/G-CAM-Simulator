/// <summary>What a node library + linear interpolation would cost, measured against the exact (CRN) model so finite-MC
/// noise cannot masquerade as interpolation error (the H-7 confound). Asimov data = expected flood at the truth.</summary>
static class Interp
{
    public static void Run(string[] a)
    {
        int N = int.Parse(a[1]); var h = new Histories(N, 1);
        var jobs = new List<(double z, double a, string axis, double step, double frac)>();
        foreach (double z in new[] { 300.0, 500, 700, 1000 })
        {
            foreach (double dz in new[] { 5.0, 10, 20, 40 }) foreach (double fr in new[] { 0.25, 0.5 }) jobs.Add((z, 0, "z", dz, fr));
            foreach (double dp in new[] { 0.25, 0.5, 1, 2 }) foreach (double fr in new[] { 0.25, 0.5 }) jobs.Add((z, 0, "phi_mrad", dp, fr));
        }
        var lines = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.ForEach(jobs, j =>
        {
            var m = new Model(h); double[] ta = new double[900], tw = new double[900], aa = new double[900], aw = new double[900], ba = new double[900], bw = new double[900];
            double vt = 1 / (j.z - G.D);
            m.Eval(0, 0, j.z, ta, tw);
            double za, zb, pa, pb;
            if (j.axis == "z") { za = j.z - j.frac * j.step; zb = za + j.step; pa = pb = 0; m.Eval(0, 0, za, aa, aw); m.Eval(0, 0, zb, ba, bw); }
            else { za = zb = j.z; pa = -j.frac * j.step / 1000; pb = pa + j.step / 1000; m.Eval(pa * (j.z - G.D), 0, j.z, aa, aw); m.Eval(pb * (j.z - G.D), 0, j.z, ba, bw); }
            foreach (var (ch, t, A, B, counts) in new[] { ("all", ta, aa, ba, j.z switch { 300 => 46600.0, 500 => 17300, 700 => 8800, _ => 4400 }),
                                                           ("win", tw, aw, bw, j.z switch { 300 => 14300.0, 500 => 5260, 700 => 2650, _ => 1340 }) })
            {
                var pt = Norm(t); var p0 = Norm(A); var p1 = Norm(B);
                double exact = 0; for (int i = 0; i < 900; i++) if (pt[i] > 0) exact += pt[i] * Math.Log(pt[i]);
                double best = double.NegativeInfinity, bf = 0;
                for (int k = 0; k <= 2000; k++)
                {
                    double f = k / 2000.0, s = 0;
                    for (int i = 0; i < 900; i++) if (pt[i] > 0) s += pt[i] * Math.Log((1 - f) * p0[i] + f * p1[i]);
                    if (s > best) { best = s; bf = f; }
                }
                // bias of the interpolated-library estimate and the expected LL lost at the 60 s count (Asimov)
                string bias = j.axis == "z" ? $"{za + bf * j.step - j.z:F3} mm" : $"{(pa + bf * j.step / 1000) * 1000:F4} mrad";
                lines.Add($"INTERP,{j.z},{j.axis},{j.step},{j.frac},{ch},fhat={bf:F4},bias={bias},LLloss60s={counts * (exact - best):F4}");
            }
        });
        foreach (var l in lines.OrderBy(x => x)) Console.WriteLine(l);
    }
    static double[] Norm(double[] v) { double s = v.Sum(); return v.Select(x => x / s).ToArray(); }
}
