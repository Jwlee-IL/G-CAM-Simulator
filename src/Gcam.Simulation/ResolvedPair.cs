namespace Gcam.Simulation;

/// <summary>TODO-34 (DR-2): the blind resolved-pair test on a reconstruction sampled on a square grid of the source plane.
///
/// Peaks are the local maxima of the image (8-connected; equal neighbours broken by raster order, so a plateau gives one
/// peak) with their topographic prominence: processing the grid points from the highest down and merging 8-connected
/// regions, a peak's saddle is the level at which its region first joins a region with a higher peak (the highest col on
/// any path to higher ground); the global maximum's saddle is the image minimum. Prominence is measured above a baseline b
/// (the image median for cross-correlation, whose image carries a pedestal and negative sidelobes; 0 for MLEM):
/// relative prominence = (P − max(saddle, b)) / (P − b). The test takes P1 = the global maximum and P2 = the highest other
/// peak whose relative prominence is ≥ v, and calls the pair resolved when P1 and P2 lie within
/// r = min(Δ / 2, one element) of different true sources (angles seen from the detector centre). v = 0.25 is a
/// Rayleigh-like valley (for two equal tents of FWHM w the relative prominence is Δ / w − 1, so v = 0.25 ↔ Δ = 1.25 w).</summary>
public static class ResolvedPair
{
    /// <summary>A peak: grid index, value and saddle level.</summary>
    public readonly record struct Peak(int Index, double Value, double Saddle);

    /// <summary>Every local maximum with its saddle (order unspecified).</summary>
    public static List<Peak> Peaks(ReadOnlySpan<double> image, int n)
    {
        int count = n * n;
        if (image.Length < count) throw new ArgumentException("Image smaller than the grid.");
        var order = new int[count];
        var keys = new double[count];
        for (int k = 0; k < count; k++) { order[k] = k; keys[k] = -image[k]; }
        // Descending value, ties by ascending index (a stable order makes the plateau rule deterministic).
        Array.Sort(keys, order, Comparer<double>.Create((p, q) => p.CompareTo(q)));
        StableTies(keys, order);
        var parent = new int[count];
        Array.Fill(parent, -1);
        var peakOf = new int[count];
        var peaks = new List<Peak>();
        Span<int> roots = stackalloc int[8];
        foreach (int k in order)
        {
            int x = k % n, y = k / n, nr = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int xx = x + dx, yy = y + dy;
                    if (xx < 0 || yy < 0 || xx >= n || yy >= n) continue;
                    int kk = yy * n + xx;
                    if (parent[kk] < 0) continue;
                    int r = Find(parent, kk);
                    bool seen = false;
                    for (int q = 0; q < nr; q++) if (roots[q] == r) { seen = true; break; }
                    if (!seen) roots[nr++] = r;
                }
            parent[k] = k; peakOf[k] = k;
            if (nr == 0) continue;
            // The region whose peak is highest (ties: the peak processed first, i.e. the lower index) survives.
            int best = roots[0];
            for (int q = 1; q < nr; q++) if (Higher(image, peakOf[roots[q]], peakOf[best])) best = roots[q];
            for (int q = 0; q < nr; q++)
                if (roots[q] != best) { peaks.Add(new Peak(peakOf[roots[q]], image[peakOf[roots[q]]], image[k])); parent[roots[q]] = best; }
            parent[k] = best;
        }
        int top = order[0];
        peaks.Add(new Peak(top, image[top], image[order[^1]]));
        return peaks;
    }

    private static bool Higher(ReadOnlySpan<double> image, int a, int b) => image[a] > image[b] || (image[a] == image[b] && a < b);

    private static void StableTies(double[] keys, int[] order)
    {
        int start = 0;
        for (int k = 1; k <= keys.Length; k++)
            if (k == keys.Length || keys[k] != keys[start]) { if (k - start > 1) Array.Sort(order, start, k - start); start = k; }
    }

    private static int Find(int[] parent, int x)
    {
        while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
        return x;
    }

    /// <summary>The image median (the cross-correlation baseline).</summary>
    public static double Median(ReadOnlySpan<double> image, int count)
    {
        // The element of rank count / 2 of the sorted values, by quickselect (Hoare partition), without a full sort.
        var a = image[..count].ToArray();
        int lo = 0, hi = count - 1, k = count / 2;
        while (lo < hi)
        {
            double pivot = a[(lo + hi) >>> 1];
            int i = lo, j = hi;
            while (i <= j)
            {
                while (a[i] < pivot) i++;
                while (a[j] > pivot) j--;
                if (i <= j) { (a[i], a[j]) = (a[j], a[i]); i++; j--; }
            }
            if (k <= j) hi = j; else if (k >= i) lo = i; else break;
        }
        return a[k];
    }

    /// <summary>The second peak for each valley threshold v: the highest peak other than the global maximum whose relative
    /// prominence is ≥ v (index −1 when none). Returns the global maximum's index.</summary>
    public static int SecondPeaks(List<Peak> peaks, double baseline, IReadOnlyList<double> valleys, Span<int> second)
        => SecondPeaks(peaks, baseline, valleys, second, stackalloc double[valleys.Count]);

    /// <summary>As <see cref="SecondPeaks(List{Peak}, double, IReadOnlyList{double}, Span{int})"/>, also returning each second
    /// peak's absolute prominence P − max(saddle, baseline) in the image's own units (0 when there is none) — the statistic
    /// of the turn-3 significance floor.</summary>
    public static int SecondPeaks(List<Peak> peaks, double baseline, IReadOnlyList<double> valleys, Span<int> second, Span<double> prominence)
    {
        peaks.Sort((p, q) => q.Value != p.Value ? q.Value.CompareTo(p.Value) : p.Index.CompareTo(q.Index));
        int top = peaks[0].Index;
        second.Fill(-1);
        prominence.Fill(0);
        int open = valleys.Count;
        for (int k = 1; k < peaks.Count && open > 0; k++)
        {
            double h = peaks[k].Value - baseline;
            if (!(h > 0)) break;
            double abs = peaks[k].Value - Math.Max(peaks[k].Saddle, baseline);
            double rel = abs / h;
            for (int v = 0; v < valleys.Count; v++)
                if (second[v] < 0 && rel >= valleys[v]) { second[v] = peaks[k].Index; prominence[v] = abs; open--; }
        }
        return top;
    }

    /// <summary>Angle (degrees) between the directions from the detector centre to two points of the source plane z.</summary>
    public static double AngleDeg(double z, double x1, double y1, double x2, double y2)
    {
        double dot = x1 * x2 + y1 * y2 + z * z;
        double n1 = Math.Sqrt(x1 * x1 + y1 * y1 + z * z), n2 = Math.Sqrt(x2 * x2 + y2 * y2 + z * z);
        return Math.Acos(Math.Clamp(dot / (n1 * n2), -1, 1)) * 180 / Math.PI;
    }

    /// <summary>Grid geometry of a reconstruction: size, origin and step (mm) on the source plane at z (mm from the detector).</summary>
    public readonly record struct GridFrame(int Size, double OriginMm, double StepMm, double PlaneZMm)
    {
        public (double X, double Y) Point(int index) => (OriginMm + index % Size * StepMm, OriginMm + index / Size * StepMm);
    }

    /// <summary>Whether grid points p1, p2 (indices) are within r degrees of different true sources s1, s2.</summary>
    public static bool Assigned(GridFrame g, int p1, int p2, (double X, double Y) s1, (double X, double Y) s2, double radiusDeg)
    {
        if (p1 < 0 || p2 < 0) return false;
        var a = g.Point(p1); var b = g.Point(p2);
        double Ang((double X, double Y) u, (double X, double Y) w) => AngleDeg(g.PlaneZMm, u.X, u.Y, w.X, w.Y);
        return (Ang(a, s1) < radiusDeg && Ang(b, s2) < radiusDeg) || (Ang(a, s2) < radiusDeg && Ang(b, s1) < radiusDeg);
    }

    /// <summary>The full test for each v: resolved[v] for the hypothesised pair (s1, s2) and assignment radius r (degrees).</summary>
    public static void Test(ReadOnlySpan<double> image, GridFrame g, bool medianBaseline, (double X, double Y) s1, (double X, double Y) s2,
        double radiusDeg, IReadOnlyList<double> valleys, Span<bool> resolved)
        => Test(image, g, medianBaseline, s1, s2, radiusDeg, valleys, resolved, stackalloc double[valleys.Count]);

    /// <summary>The shape test, also returning the second peak's absolute prominence for each v (0 when there is none). The
    /// turn-3 criterion "DR-2 + significance floor" is resolved[v] ∧ prominence[v] ≥ F (<see cref="PassesFloor"/>), with F
    /// calibrated on single-source acquisitions; the shape result itself is unchanged.</summary>
    public static void Test(ReadOnlySpan<double> image, GridFrame g, bool medianBaseline, (double X, double Y) s1, (double X, double Y) s2,
        double radiusDeg, IReadOnlyList<double> valleys, Span<bool> resolved, Span<double> prominence)
    {
        int count = g.Size * g.Size;
        double b = medianBaseline ? Median(image, count) : 0.0;
        var peaks = Peaks(image, g.Size);
        Span<int> second = stackalloc int[valleys.Count];
        int top = SecondPeaks(peaks, b, valleys, second, prominence);
        for (int v = 0; v < valleys.Count; v++) resolved[v] = Assigned(g, top, second[v], s1, s2, radiusDeg);
    }

    /// <summary>Turn 3: the shape test plus a significance floor on the second peak's absolute prominence.</summary>
    public static bool PassesFloor(bool shapeResolved, double prominence, double floor) => shapeResolved && prominence >= floor;

    /// <summary>Turn 3 selection rule: the smallest floor F among the observed null statistics (and 0) such that the share of
    /// single-source acquisitions whose second peak has prominence ≥ F is ≤ alpha — the hypothesis-free false second-peak
    /// rate, which bounds the assigned false split for any hypothesised pair. Inclusive: a statistic equal to F counts as
    /// exceeding. Statistics are the absolute prominences (0 = no second peak, never exceeding a positive F).</summary>
    public static double SelectFloor(IReadOnlyList<double> nullProminences, double alpha)
    {
        var s = nullProminences.Where(v => v > 0).OrderBy(v => v).ToArray();
        int n = nullProminences.Count;
        if (n == 0) throw new ArgumentException("No null acquisitions.");
        // Exceedance of F = s[k] is (count of values ≥ s[k]) / n; the smallest admissible F is just above the
        // (largest allowed count + 1)-th largest value.
        int allowed = (int)Math.Floor(alpha * n + 1e-9);
        if (s.Length <= allowed) return 0;
        double kth = s[s.Length - allowed - 1];          // the largest value that must be excluded
        return BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(kth) + 1);   // next double above it
    }

    /// <summary>FWHM (degrees) along x and y through the global maximum, linear interpolation between grid points, half
    /// height above the baseline; NaN when an edge is not reached inside the grid.</summary>
    public static (double X, double Y) Fwhm(ReadOnlySpan<double> image, GridFrame g, double baseline)
    {
        int n = g.Size, b = 0;
        for (int k = 1; k < n * n; k++) if (image[k] > image[b]) b = k;
        int bx = b % n, by = b / n;
        double half = baseline + 0.5 * (image[b] - baseline);
        var img = image[..(n * n)].ToArray();
        double Edge(Func<int, double> f, int c, int dir)
        {
            for (int k = c; k + dir >= 0 && k + dir < n; k += dir)
                if (f(k) >= half && f(k + dir) < half) return k + dir * (f(k) - half) / (f(k) - f(k + dir));
            return double.NaN;
        }
        double Deg(double idx) => Math.Atan((g.OriginMm + idx * g.StepMm) / g.PlaneZMm) * 180 / Math.PI;
        double Fx(int k) => img[by * n + k];
        double Fy(int k) => img[k * n + bx];
        return (Deg(Edge(Fx, bx, 1)) - Deg(Edge(Fx, bx, -1)), Deg(Edge(Fy, by, 1)) - Deg(Edge(Fy, by, -1)));
    }

    /// <summary>Solid angle (deg²) of the grid points above half height (above the baseline), as the diameter (degrees) of
    /// the circle of equal area — one robust number for a lumpy peak. Each grid point counts its own cell.</summary>
    public static double HalfMaxDiameterDeg(ReadOnlySpan<double> image, GridFrame g, double baseline)
    {
        int n = g.Size, b = 0;
        for (int k = 1; k < n * n; k++) if (image[k] > image[b]) b = k;
        double half = baseline + 0.5 * (image[b] - baseline), area = 0;
        for (int k = 0; k < n * n; k++)
            if (image[k] >= half)
            {
                var (x, y) = g.Point(k);
                double r2 = x * x + y * y + g.PlaneZMm * g.PlaneZMm;
                // cell solid angle ≈ step² · cos θ / r² (sr), cos θ = z / r
                area += g.StepMm * g.StepMm * g.PlaneZMm / (r2 * Math.Sqrt(r2));
            }
        double deg2 = area * Math.Pow(180 / Math.PI, 2);
        return 2 * Math.Sqrt(deg2 / Math.PI);
    }
}
