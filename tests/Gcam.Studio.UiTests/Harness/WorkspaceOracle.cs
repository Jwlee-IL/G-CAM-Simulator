namespace Gcam.Studio.UiTests.Harness;

/// <summary>Independent arithmetic over retained product data; never calls workspace processing or decoding.</summary>
public static class WorkspaceOracle
{
    public static long BandCount(double[] centres, double[] counts, double lo, double hi) =>
        (long)Enumerable.Range(0, centres.Length).Where(i => lo <= centres[i] && centres[i] <= hi).Sum(i => counts[i]);

    public static int[] EventsInWindow(double[] times, int trigger, double windowUs) =>
        Enumerable.Range(0, times.Length).Where(i =>
            times[i] >= times[trigger] - windowUs * 0.2e-6 && times[i] < times[trigger] + windowUs * 0.8e-6).ToArray();

    public static (double Lo, double Hi, bool Near, bool Far) HalfMax(double[] x, double[] y)
    {
        int max = Array.IndexOf(y, y.Max());
        double level = y[max] / 2;
        // Walk the contiguous superlevel set surrounding the global maximum, then intersect its boundary segments.
        int a = max, b = max;
        while (a > 0 && y[a - 1] >= level) a--;
        while (b + 1 < y.Length && y[b + 1] >= level) b++;
        double Cross(int p, int q) => (x[p] * (y[q] - level) + x[q] * (level - y[p])) / (y[q] - y[p]);
        return (a == 0 ? x[0] : Cross(a - 1, a), b == y.Length - 1 ? x[^1] : Cross(b, b + 1), a == 0, b == y.Length - 1);
    }
}
