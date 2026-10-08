namespace Gcam.Detector;

/// <summary>Separable marginal initialization followed by one-to-one measured maximum matching. The marginal peaks
/// describe the observed lattice; they are never inserted as watershed markers. Unsupported or ambiguous slots fail.
/// <see cref="Grow"/> is the v2 matcher, which follows curved columns and rows.</summary>
internal static class FloodTopology
{
    internal static int[]? Match(double[] density, IReadOnlyList<int> candidates, int nx, int ny, int bx, int by,
        out string? failure)
    {
        failure = null;
        var px = new double[bx]; var py = new double[by];
        for (int y = 0; y < by; y++)
            for (int x = 0; x < bx; x++) { px[x] += density[y * bx + x]; py[y] += density[y * bx + x]; }
        int[] Axis(double[] p, int count) => Enumerable.Range(1, p.Length - 2)
            .Where(i => p[i] > p[i - 1] && p[i] >= p[i + 1])
            .OrderByDescending(i => p[i]).ThenBy(i => i).Take(count).Order().ToArray();
        var xs = Axis(px, nx); var ys = Axis(py, ny);
        if (xs.Length != nx || ys.Length != ny)
        { failure = "unsupported marginal lattice"; return null; }
        double Radius(int[] axis, int i, int length)
        {
            double left = i > 0 ? axis[i] - axis[i - 1] : i + 1 < axis.Length ? axis[i + 1] - axis[i] : length;
            double right = i + 1 < axis.Length ? axis[i + 1] - axis[i] : left;
            // Strictly disjoint slots: no maximum can support two crystals. Half-spacing is a topology boundary,
            // not a noise suppression radius. Equality at a boundary is refused.
            return .5 * Math.Min(left, right);
        }
        var grid = new int[nx * ny]; var used = new HashSet<int>();
        for (int r = 0; r < ny; r++)
            for (int c = 0; c < nx; c++)
            {
                double rx = Radius(xs, c, bx), ry = Radius(ys, r, by);
                int marker = candidates.Where(p => Math.Abs(p % bx - xs[c]) < rx && Math.Abs(p / bx - ys[r]) < ry)
                    .OrderByDescending(p => density[p]).ThenBy(p => p).DefaultIfEmpty(-1).First();
                if (marker < 0 || !used.Add(marker))
                { failure = $"unsupported lattice slot ({c}, {r}); no distinct measured maximum"; return null; }
                grid[r * nx + c] = marker;
            }
        // A resolved pair must exhibit a valley below BOTH measured maxima. A merged peak or a continuum ridge
        // cannot be turned into a second marker. One histogram quantum is enough: no empirical contrast cut.
        bool Valley(int a, int b)
        {
            int ax = a % bx, ay = a / bx, dx = b % bx - ax, dy = b / bx - ay;
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (steps < 2) return false;
            double low = double.PositiveInfinity;
            for (int k = 1; k < steps; k++)
            {
                int x = ax + (int)Math.Round((double)dx * k / steps);
                int y = ay + (int)Math.Round((double)dy * k / steps);
                low = Math.Min(low, density[y * bx + x]);
            }
            return low < Math.Min(density[a], density[b]);
        }
        for (int r = 0; r < ny; r++)
            for (int c = 0; c < nx; c++)
            {
                int p = grid[r * nx + c];
                if ((c + 1 < nx && !Valley(p, grid[r * nx + c + 1])) ||
                    (r + 1 < ny && !Valley(p, grid[(r + 1) * nx + c])))
                { failure = $"unresolved lattice neighbours at ({c}, {r})"; return null; }
            }
        return grid;
    }

    /// <summary>v2 detection scale: Gaussian σ = this fraction of the nominal spot spacing (bins per crystal). Fixed from
    /// the geometry before any validation: well below the measured spot widths (FWHM / spacing 0.5 – 0.76), so adjacent
    /// spots keep a valley, and wide enough to remove the shot-noise maxima of a ~1 count / bin flood.</summary>
    internal const double SigmaFraction = 0.125;

    /// <summary>A lattice slot accepts a measured maximum within this fraction of the local step length of its
    /// prediction (below 0.5: neighbouring slots cannot share a maximum).</summary>
    internal const double SearchFraction = 0.4;

    /// <summary>One-sided counting-statistics significance (standard deviations) of the valley between two neighbours.</summary>
    internal const double ValleySigmas = 3.0;

    /// <summary>LatticeGrowth (v2). Seed: the highest measured maximum within one nominal spacing of the flood's centre of
    /// mass. Growth, ring by ring: each empty slot is predicted from every assigned 4-neighbour as that neighbour plus the
    /// local measured step (its own previous step along the same direction, else the parallel steps of the adjacent
    /// assigned pairs, else the nominal spacing), and takes the highest unused measured maximum within
    /// <see cref="SearchFraction"/> of the mean step length; a slot without one is not grown further. The measured lattice
    /// is grown only onto maxima separated from their assigned neighbours by a significant valley; it must span exactly
    /// Nx × Ny with every slot filled, and every pair of lattice neighbours must show a valley below the
    /// lower maximum by <see cref="ValleySigmas"/> counting standard deviations (Poisson variance of the smoothed counts,
    /// Var = Σw² · λ, λ estimated by the smoothed value). Nothing is inserted: every marker is a measured maximum.</summary>
    internal static int[]? Grow(double[] density, IReadOnlyList<int> candidates, int nx, int ny, int bx, int by,
        int binsPerCrystal, double sigma, out string? failure)
    {
        failure = null;
        var at = new int[bx * by];
        Array.Fill(at, -1);
        for (int i = 0; i < candidates.Count; i++) at[candidates[i]] = i;
        var used = new bool[candidates.Count];
        double nominal = binsPerCrystal;
        int Best(double px, double py, double r)
        {
            int best = -1;
            for (int y = Math.Max(0, (int)Math.Ceiling(py - r)); y <= Math.Min(by - 1, (int)Math.Floor(py + r)); y++)
                for (int x = Math.Max(0, (int)Math.Ceiling(px - r)); x <= Math.Min(bx - 1, (int)Math.Floor(px + r)); x++)
                {
                    int i = at[y * bx + x];
                    if (i < 0 || used[i] || (x - px) * (x - px) + (y - py) * (y - py) >= r * r) continue;
                    if (best < 0 || density[candidates[i]] > density[candidates[best]]
                        || density[candidates[i]] == density[candidates[best]] && i < best) best = i;
                }
            return best;
        }
        double total = 0, mx = 0, my = 0;
        for (int y = 0; y < by; y++)
            for (int x = 0; x < bx; x++) { double v = density[y * bx + x]; total += v; mx += v * x; my += v * y; }
        if (!(total > 0)) { failure = "empty calibration flood"; return null; }
        // Σw² of the separable smoothing kernel exactly as FloodLut.Smooth builds it (4σ truncation, unit sum).
        int radius = (int)Math.Ceiling(4 * sigma);
        double ks = 0, k2 = 0;
        for (int i = -radius; i <= radius; i++) ks += Math.Exp(-0.5 * i * i / (sigma * sigma));
        for (int i = -radius; i <= radius; i++) { double w = Math.Exp(-0.5 * i * i / (sigma * sigma)) / ks; k2 += w * w; }
        double w2 = k2 * k2;
        string? Valley(int a, int b, (int, int) p, (int, int) q)
        {
            int ax = a % bx, ay = a / bx, dx = b % bx - ax, dy = b / bx - ay;
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (steps < 2) return $"unresolved lattice neighbours {p} and {q}";
            double low = double.PositiveInfinity;
            for (int t = 1; t < steps; t++)
                low = Math.Min(low, density[(ay + (int)Math.Round((double)dy * t / steps)) * bx + ax + (int)Math.Round((double)dx * t / steps)]);
            double peak = Math.Min(density[a], density[b]);
            return peak - low > ValleySigmas * Math.Sqrt(w2 * (peak + low)) ? null
                : $"no significant valley between lattice neighbours {p} and {q}";
        }
        var lattice = new Dictionary<(int I, int J), int>();
        var dead = new HashSet<(int, int)>();
        int seed = Best(mx / total, my / total, nominal);
        if (seed < 0) { failure = "no measured maximum within one spacing of the flood centre"; return null; }
        lattice[(0, 0)] = seed; used[seed] = true;
        (double X, double Y) Pos((int I, int J) k) { int b = candidates[lattice[k]]; return (b % bx, b / bx); }
        (double X, double Y) Step((int I, int J) m, int di, int dj)
        {
            var back = (m.I - di, m.J - dj);
            if (lattice.ContainsKey(back)) { var p = Pos(m); var q = Pos(back); return (p.X - q.X, p.Y - q.Y); }
            double sx = 0, sy = 0; int n = 0;
            foreach (var (pi, pj) in new[] { (dj, di), (-dj, -di) })
            {
                var a = (m.I + pi, m.J + pj);
                if (!lattice.ContainsKey(a)) continue;
                var b = (a.Item1 - di, a.Item2 - dj);
                if (lattice.ContainsKey(b)) { var p = Pos(a); var q = Pos(b); sx += p.X - q.X; sy += p.Y - q.Y; n++; }
                var c = (a.Item1 + di, a.Item2 + dj);
                if (lattice.ContainsKey(c)) { var p = Pos(c); var q = Pos(a); sx += p.X - q.X; sy += p.Y - q.Y; n++; }
            }
            return n > 0 ? (sx / n, sy / n) : (di * nominal, dj * nominal);
        }
        (int, int)[] directions = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        var queue = new PriorityQueue<(int I, int J), (int, int, int)>();
        void Push(int i, int j)
        {
            foreach (var (di, dj) in directions)
            {
                var k = (i + di, j + dj);
                if (!lattice.ContainsKey(k) && !dead.Contains(k))
                    queue.Enqueue(k, (Math.Abs(k.Item1) + Math.Abs(k.Item2), k.Item2, k.Item1));
            }
        }
        Push(0, 0);
        int limit = 4 * nx * ny;
        while (lattice.Count <= limit && queue.TryDequeue(out var k, out _))
        {
            if (lattice.ContainsKey(k) || dead.Contains(k)) continue;
            double px = 0, py = 0, length = 0; int count = 0;
            foreach (var (di, dj) in directions)
            {
                var m = (k.I - di, k.J - dj);
                if (!lattice.ContainsKey(m)) continue;
                var v = Step(m, di, dj); var p = Pos(m);
                px += p.X + v.X; py += p.Y + v.Y; length += Math.Sqrt(v.X * v.X + v.Y * v.Y); count++;
            }
            int match = Best(px / count, py / count, SearchFraction * length / count);
            // A slot is grown only onto a maximum separated from every assigned neighbour by a significant valley:
            // continuum or shot-noise maxima beyond the lattice edge (or in an empty slot) are not spots.
            if (match < 0 || directions.Any(d => lattice.TryGetValue((k.I - d.Item1, k.J - d.Item2), out int m)
                    && Valley(candidates[m], candidates[match], (k.I - d.Item1, k.J - d.Item2), k) is not null))
            { dead.Add(k); continue; }
            lattice[k] = match; used[match] = true;
            Push(k.I, k.J);
        }
        // A lattice grown through noise stops at the limit and is refused by the span check.
        int i0 = lattice.Keys.Min(k => k.I), i1 = lattice.Keys.Max(k => k.I);
        int j0 = lattice.Keys.Min(k => k.J), j1 = lattice.Keys.Max(k => k.J);
        if (i1 - i0 + 1 != nx || j1 - j0 + 1 != ny || lattice.Count != nx * ny)
        {
            var empty = Enumerable.Range(j0, j1 - j0 + 1).SelectMany(j => Enumerable.Range(i0, i1 - i0 + 1).Select(i => (i, j)))
                .Where(k => !lattice.ContainsKey(k)).Take(3).Select(k => $"({k.i - i0}, {k.j - j0})");
            failure = $"measured lattice spans {i1 - i0 + 1} × {j1 - j0 + 1} with {lattice.Count} maxima, expected {nx} × {ny}"
                + (lattice.Count < (i1 - i0 + 1) * (j1 - j0 + 1) ? $"; unsupported slots {string.Join(", ", empty)}" : "");
            return null;
        }
        var grid = new int[nx * ny];
        foreach (var (k, c) in lattice) grid[(k.J - j0) * nx + (k.I - i0)] = candidates[c];

        for (int r = 0; r < ny; r++)
            for (int c = 0; c < nx; c++)
            {
                if (c + 1 < nx && Valley(grid[r * nx + c], grid[r * nx + c + 1], (c, r), (c + 1, r)) is { } h) { failure = h; return null; }
                if (r + 1 < ny && Valley(grid[r * nx + c], grid[(r + 1) * nx + c], (c, r), (c, r + 1)) is { } v) { failure = v; return null; }
            }
        return grid;
    }
}
