namespace Gcam.Detector;

/// <summary>Separable marginal initialization followed by one-to-one measured maximum matching. The marginal peaks
/// describe the observed lattice; they are never inserted as watershed markers. Unsupported or ambiguous slots fail.</summary>
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
}
