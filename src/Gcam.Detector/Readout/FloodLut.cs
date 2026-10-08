using Gcam.Configuration;

namespace Gcam.Detector;

/// <summary>
/// Crystal identification from a calibration flood in the raw position plane [−1, 1]². Steps: 2D histogram
/// (BinsPerCrystal × N bins per axis) → Gaussian smoothing (σ in bins; suppresses shot noise, broadens spots) → markers =
/// local maxima (8-neighbourhood, deterministic plateau tie-break toward lower index) above MinPeakFraction of the maximum,
/// non-maximum suppression within MinSeparationFraction of the nominal spot spacing, the Nx·Ny highest kept → ordering
/// into the crystal grid (rows by y, columns by x; every column must then be strictly increasing in y, and neighbours may
/// not step across by half the local spacing or more) → marker-controlled
/// watershed: priority flooding of the smoothed density from the markers, highest density first, ties broken by
/// insertion order (deterministic plateaus); or, as the alternative, nearest-marker (Voronoi) cells. Every bin gets a
/// crystal; points outside [−1, 1)² get −1.
/// <para>Failure is explicit: fewer candidates than crystals, or markers that cannot be ordered into a monotone grid,
/// give <see cref="Succeeded"/> = false with the reason; an unresolved flood is never forced into a grid.</para>
/// </summary>
public sealed class FloodLut
{
    private readonly int[] _labels;

    public bool Succeeded { get; }
    public string? Failure { get; }
    public int PeaksFound { get; }
    public int PeaksExpected { get; }
    public int CountX { get; }
    public int CountY { get; }
    public int BinsX { get; }
    public int BinsY { get; }
    public double SmoothingSigmaBins { get; }

    /// <summary>Marker positions (raw coordinates), index = crystal (iy·Nx + ix); empty on failure.</summary>
    public IReadOnlyList<(double X, double Y)> Peaks { get; }

    /// <summary>Smoothed calibration density, row-major [by·BinsX + bx].</summary>
    public double[] Density { get; }

    private FloodLut(int nx, int ny, int bx, int by, double sigma, double[] density, int found, string? failure,
        (double, double)[] peaks, int[] labels)
    {
        CountX = nx; CountY = ny; BinsX = bx; BinsY = by; SmoothingSigmaBins = sigma;
        Density = density; PeaksFound = found; PeaksExpected = nx * ny; Failure = failure;
        Succeeded = failure is null;
        Peaks = peaks;
        _labels = labels;
    }

    /// <summary>Crystal of a raw position, or −1 (outside the plane, or a failed calibration).</summary>
    public int Lookup(double x, double y)
    {
        if (!Succeeded) return -1;
        int b = Bin(x, y, BinsX, BinsY);
        return b < 0 ? -1 : _labels[b];
    }

    /// <summary>Crystal label of histogram bin (bx, by).</summary>
    public int LabelOfBin(int bx, int by) => Succeeded ? _labels[by * BinsX + bx] : -1;

    public static int Bin(double x, double y, int binsX, int binsY)
    {
        if (!(x >= -1) || !(x < 1) || !(y >= -1) || !(y < 1)) return -1;
        int bx = Math.Min(binsX - 1, (int)((x + 1) / 2 * binsX)), by = Math.Min(binsY - 1, (int)((y + 1) / 2 * binsY));
        return by * binsX + bx;
    }

    /// <summary>Histogram of raw positions over [−1, 1)² (points outside are dropped).</summary>
    public static double[] Histogram(IEnumerable<(double X, double Y)> points, int binsX, int binsY)
    {
        var h = new double[binsX * binsY];
        foreach (var (x, y) in points)
        {
            int b = Bin(x, y, binsX, binsY);
            if (b >= 0) h[b]++;
        }
        return h;
    }

    /// <summary>Separable Gaussian smoothing, kernel truncated at 4σ, zero outside the plane. σ = 0 returns a copy.</summary>
    public static double[] Smooth(double[] h, int binsX, int binsY, double sigma)
    {
        if (!(sigma > 0)) return (double[])h.Clone();
        int r = (int)Math.Ceiling(4 * sigma);
        var k = new double[2 * r + 1];
        for (int i = -r; i <= r; i++) k[i + r] = Math.Exp(-0.5 * i * i / (sigma * sigma));
        double ks = k.Sum();
        for (int i = 0; i < k.Length; i++) k[i] /= ks;
        var tmp = new double[h.Length];
        var outp = new double[h.Length];
        for (int y = 0; y < binsY; y++)
            for (int x = 0; x < binsX; x++)
            {
                double s = 0;
                for (int i = -r; i <= r; i++)
                {
                    int xx = x + i;
                    if (xx >= 0 && xx < binsX) s += k[i + r] * h[y * binsX + xx];
                }
                tmp[y * binsX + x] = s;
            }
        for (int y = 0; y < binsY; y++)
            for (int x = 0; x < binsX; x++)
            {
                double s = 0;
                for (int i = -r; i <= r; i++)
                {
                    int yy = y + i;
                    if (yy >= 0 && yy < binsY) s += k[i + r] * tmp[yy * binsX + x];
                }
                outp[y * binsX + x] = s;
            }
        return outp;
    }

    public static FloodLut Calibrate(IReadOnlyCollection<(double X, double Y)> points, int countX, int countY,
        FloodCalibrationConfig config)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(config);
        if (countX < 1 || countY < 1 || config.BinsPerCrystal < 2 || !(config.SmoothingSigmaBins >= 0)
            || config.MinPeakFraction is < 0 or >= 1 || !(config.MinSeparationFraction >= 0))
            throw new ArgumentException("Invalid flood calibration parameters.");
        int bx = countX * config.BinsPerCrystal, by = countY * config.BinsPerCrystal, n = countX * countY;
        var density = Smooth(Histogram(points, bx, by), bx, by, config.SmoothingSigmaBins);
        double max = density.Max();
        FloodLut Fail(int found, string why) => new(countX, countY, bx, by, config.SmoothingSigmaBins, density, found, why, [], []);
        if (!(max > 0)) return Fail(0, "empty calibration flood");

        // Local maxima: strictly greater than the neighbours after it in scan order, ≥ those before (plateau → first bin).
        var candidates = new List<int>();
        for (int y = 0; y < by; y++)
            for (int x = 0; x < bx; x++)
            {
                double v = density[y * bx + x];
                if (v < config.MinPeakFraction * max || v <= 0) continue;
                bool peak = true;
                for (int dy = -1; dy <= 1 && peak; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= bx || yy >= by) continue;
                        double w = density[yy * bx + xx];
                        bool after = dy > 0 || dy == 0 && dx > 0;
                        if (after ? w >= v : w > v) { peak = false; break; }
                    }
                if (peak) candidates.Add(y * bx + x);
            }
        candidates.Sort((p, q) => density[q] != density[p] ? density[q].CompareTo(density[p]) : p.CompareTo(q));
        double radius = config.MinSeparationFraction * config.BinsPerCrystal;
        var kept = new List<int>();
        foreach (int c in candidates)
        {
            int cx = c % bx, cy = c / bx;
            bool near = false;
            foreach (int m in kept)
            {
                double ddx = m % bx - cx, ddy = m / bx - cy;
                if (ddx * ddx + ddy * ddy < radius * radius) { near = true; break; }
            }
            if (!near) kept.Add(c);
        }
        if (kept.Count < n) return Fail(kept.Count, $"found {kept.Count} of {n} flood peaks");
        var markers = kept.Take(n).ToList();

        // Order: sort by y, take rows of Nx, sort each row by x; then every column must increase strictly in y.
        markers.Sort((p, q) => p / bx != q / bx ? (p / bx).CompareTo(q / bx) : (p % bx).CompareTo(q % bx));
        var grid = new int[n];
        for (int r = 0; r < countY; r++)
        {
            var row = markers.Skip(r * countX).Take(countX).OrderBy(m => m % bx).ThenBy(m => m / bx).ToArray();
            for (int c = 0; c < countX; c++) grid[r * countX + c] = row[c];
        }
        for (int r = 0; r + 1 < countY; r++)
            for (int c = 0; c < countX; c++)
                if (grid[(r + 1) * countX + c] / bx <= grid[r * countX + c] / bx)
                    return Fail(kept.Count, $"flood peaks cannot be ordered into a {countX} × {countY} grid (column {c}, rows {r}/{r + 1})");
        for (int r = 0; r < countY; r++)
            for (int c = 0; c + 1 < countX; c++)
                if (grid[r * countX + c + 1] % bx <= grid[r * countX + c] % bx)
                    return Fail(kept.Count, $"flood peaks cannot be ordered into a {countX} × {countY} grid (row {r}, columns {c}/{c + 1})");
        // Local regularity: neighbours along a row may not step in y by half the local row spacing or more (and likewise
        // columns in x). A smoothly distorted flood passes; markers that merely sort into a grid by chance (noise peaks
        // in an unresolved flood) zig-zag and fail.
        if (Irregular(grid, countX, countY, bx) is { } where)
            return Fail(kept.Count, $"flood peaks do not form a regular {countX} × {countY} grid ({where})");

        var labels = Enumerable.Repeat(-1, bx * by).ToArray();
        if (config.Segmentation == FloodSegmentation.NearestPeak)
        {
            // Voronoi: each bin to the nearest marker (squared bin distance; ties to the lower crystal index).
            for (int y = 0; y < by; y++)
                for (int x = 0; x < bx; x++)
                {
                    int best = 0;
                    double bd = double.PositiveInfinity;
                    for (int crystal = 0; crystal < n; crystal++)
                    {
                        double ddx = grid[crystal] % bx - x, ddy = grid[crystal] / bx - y, d2 = ddx * ddx + ddy * ddy;
                        if (d2 < bd) { bd = d2; best = crystal; }
                    }
                    labels[y * bx + x] = best;
                }
            var voronoiPeaks = grid.Select(g => (ToRaw(g % bx, bx), ToRaw(g / bx, by))).ToArray();
            return new FloodLut(countX, countY, bx, by, config.SmoothingSigmaBins, density, kept.Count, null, voronoiPeaks, labels);
        }

        // Marker-controlled watershed by priority flooding.
        var queue = new PriorityQueue<int, (double, long)>();
        long order = 0;
        for (int crystal = 0; crystal < n; crystal++)
        {
            labels[grid[crystal]] = crystal;
            queue.Enqueue(grid[crystal], (-density[grid[crystal]], order++));
        }
        Span<(int, int)> nb = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        while (queue.TryDequeue(out int b, out _))
        {
            int x = b % bx, y = b / bx;
            foreach (var (dx, dy) in nb)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= bx || yy >= by) continue;
                int t = yy * bx + xx;
                if (labels[t] >= 0) continue;
                labels[t] = labels[b];
                queue.Enqueue(t, (-density[t], order++));
            }
        }
        var peaks = grid.Select(g => (ToRaw(g % bx, bx), ToRaw(g / bx, by))).ToArray();
        return new FloodLut(countX, countY, bx, by, config.SmoothingSigmaBins, density, kept.Count, null, peaks, labels);
    }

    private static string? Irregular(int[] grid, int nx, int ny, int bx)
    {
        double X(int c, int r) => grid[r * nx + c] % bx;
        double Y(int c, int r) => grid[r * nx + c] / bx;
        double RowSpacing(int c, int r)
        {
            double s = 0; int k = 0;
            if (r + 1 < ny) { s += Y(c, r + 1) - Y(c, r); k++; }
            if (r > 0) { s += Y(c, r) - Y(c, r - 1); k++; }
            return k == 0 ? double.PositiveInfinity : s / k;
        }
        double ColumnSpacing(int c, int r)
        {
            double s = 0; int k = 0;
            if (c + 1 < nx) { s += X(c + 1, r) - X(c, r); k++; }
            if (c > 0) { s += X(c, r) - X(c - 1, r); k++; }
            return k == 0 ? double.PositiveInfinity : s / k;
        }
        for (int r = 0; r < ny; r++)
            for (int c = 0; c + 1 < nx; c++)
                if (Math.Abs(Y(c + 1, r) - Y(c, r)) >= 0.5 * Math.Min(RowSpacing(c, r), RowSpacing(c + 1, r)))
                    return $"row {r}, columns {c}/{c + 1}";
        for (int c = 0; c < nx; c++)
            for (int r = 0; r + 1 < ny; r++)
                if (Math.Abs(X(c, r + 1) - X(c, r)) >= 0.5 * Math.Min(ColumnSpacing(c, r), ColumnSpacing(c, r + 1)))
                    return $"column {c}, rows {r}/{r + 1}";
        return null;
    }

    private static double ToRaw(int bin, int bins) => (bin + 0.5) / bins * 2 - 1;

    /// <summary>Peak-to-valley of a (smoothed) density along the straight segments joining this LUT's markers of
    /// horizontally and vertically adjacent crystals: the profile maxima in each half are the peaks, the minimum between
    /// them the valley, P/V = mean(peaks) / valley. A valley of zero density is censored (infinite at this resolution);
    /// a half whose maximum sits on the midpoint is counted as unresolved.</summary>
    public PeakToValleyResult PeakToValley(double[] density)
    {
        ArgumentNullException.ThrowIfNull(density);
        if (!Succeeded) return new PeakToValleyResult(0, 0, 0, double.NaN, double.NaN);
        var values = new List<double>();
        int censored = 0, unresolved = 0, pairs = 0;
        void Pair(int a, int b)
        {
            pairs++;
            double x0 = (Peaks[a].X + 1) / 2 * BinsX - 0.5, y0 = (Peaks[a].Y + 1) / 2 * BinsY - 0.5;
            double x1 = (Peaks[b].X + 1) / 2 * BinsX - 0.5, y1 = (Peaks[b].Y + 1) / 2 * BinsY - 0.5;
            int m = Math.Max(4, (int)Math.Ceiling(2 * Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0))));
            if (m % 2 == 1) m++;
            var p = new double[m + 1];
            for (int i = 0; i <= m; i++) p[i] = Bilinear(density, x0 + (x1 - x0) * i / m, y0 + (y1 - y0) * i / m);
            int half = m / 2, i1 = 0, i2 = m;
            for (int i = 0; i <= half; i++) if (p[i] > p[i1]) i1 = i;
            for (int i = m; i >= half; i--) if (p[i] > p[i2]) i2 = i;
            if (i1 == half || i2 == half) { unresolved++; return; }
            double valley = double.PositiveInfinity;
            for (int i = i1; i <= i2; i++) valley = Math.Min(valley, p[i]);
            if (!(valley > 0)) { censored++; return; }
            values.Add(0.5 * (p[i1] + p[i2]) / valley);
        }
        for (int y = 0; y < CountY; y++)
            for (int x = 0; x < CountX; x++)
            {
                if (x + 1 < CountX) Pair(y * CountX + x, y * CountX + x + 1);
                if (y + 1 < CountY) Pair(y * CountX + x, (y + 1) * CountX + x);
            }
        values.Sort();
        // Censored valleys are larger than every finite ratio; the median counts them as +∞.
        var all = values.Concat(Enumerable.Repeat(double.PositiveInfinity, censored)).ToList();
        double median = all.Count == 0 ? double.NaN : all[(all.Count - 1) / 2] * 0.5 + all[all.Count / 2] * 0.5;
        double min = unresolved > 0 ? 1.0 : all.Count == 0 ? double.NaN : all[0];
        return new PeakToValleyResult(pairs, censored, unresolved, median, min);
    }

    private double Bilinear(double[] d, double x, double y)
    {
        x = Math.Clamp(x, 0, BinsX - 1); y = Math.Clamp(y, 0, BinsY - 1);
        int x0 = Math.Min((int)x, BinsX - 2 < 0 ? 0 : BinsX - 2), y0 = Math.Min((int)y, BinsY - 2 < 0 ? 0 : BinsY - 2);
        double fx = x - x0, fy = y - y0;
        double v00 = d[y0 * BinsX + x0], v10 = d[y0 * BinsX + x0 + 1], v01 = d[(y0 + 1) * BinsX + x0], v11 = d[(y0 + 1) * BinsX + x0 + 1];
        return (1 - fx) * (1 - fy) * v00 + fx * (1 - fy) * v10 + (1 - fx) * fy * v01 + fx * fy * v11;
    }
}
