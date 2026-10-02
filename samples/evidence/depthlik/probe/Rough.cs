static class Rough
{
    // rough <Nmax>: LL along v (bearing fixed at truth) over ±4σ around truth for prefixes N/4, N/2, N; one 60 s flood
    public static void Run(string[] a)
    {
        int N = int.Parse(a[1]); var h = new Histories(N, 1);
        var pts = new (double z, double sz)[] { (500, 2.5), (1000, 13) };
        Parallel.ForEach(pts, pt =>
        {
            var (all, win, _) = G.Acquire(0, 0, pt.z, 4242, 60);
            var m = new Model(h); var f = new Fitter(m, 1, 1, true);
            double vt = 1 / (pt.z - G.D), sv = pt.sz * vt * vt;
            var sb = new System.Text.StringBuilder();
            foreach (int frac in new[] { 4, 2, 1 })
                for (int k = -40; k <= 40; k++)
                {
                    double v = vt + k * 0.1 * sv;
                    sb.AppendLine($"ROUGH,{pt.z},{N / frac},{k},{G.D + 1 / v:F4},{f.LL(all, false, 0, 0, v, h.N / frac):F5},{f.LL(win, true, 0, 0, v, h.N / frac):F5}");
                }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, $"rough_{pt.z}.csv"), sb.ToString());
        });
    }
}
