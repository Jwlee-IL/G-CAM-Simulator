using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.Imaging;

/// <summary>Sampling, one-to-one angular linking and descriptive half-maximum intervals.</summary>
public static class FocusSweepMath
{
    public static double[] Planes(double maskDistanceMm, int count = 81)
    {
        double near = maskDistanceMm + 30;
        const double far = 3000;
        if (!double.IsFinite(near) || maskDistanceMm <= 0 || near >= far || count < 2 || count > 256)
            throw new ArgumentOutOfRangeException(nameof(maskDistanceMm));
        var planes = Enumerable.Range(0, count).Select(i =>
            1 / (1 / near + (1 / far - 1 / near) * i / (count - 1))).ToArray();
        planes[0] = near; planes[^1] = far;
        return planes;
    }

    /// <summary>Exact minimum-cost assignment for at most four candidates; no mm gate or scene-derived K.</summary>
    public static int[] Link(IReadOnlyList<FocusSample> heads, IReadOnlyList<FocusSample> candidates)
    {
        if (heads.Count > 4 || candidates.Count > heads.Count) throw new ArgumentOutOfRangeException(nameof(candidates));
        int[] best = [], current = new int[candidates.Count];
        double bestCost = double.PositiveInfinity;
        Search(0, 0, 0);
        return best;
        void Search(int index, int used, double cost)
        {
            if (index == candidates.Count)
            {
                if (cost < bestCost) { bestCost = cost; best = (int[])current.Clone(); }
                return;
            }
            for (int h = 0; h < heads.Count; h++)
            {
                if ((used & (1 << h)) != 0) continue;
                double dx = heads[h].AngleX - candidates[index].AngleX;
                double dy = heads[h].AngleY - candidates[index].AngleY;
                current[index] = h;
                Search(index + 1, used | (1 << h), cost + dx * dx + dy * dy);
            }
        }
    }

    public static FocusTrack Describe(IReadOnlyList<FocusSample> curve)
    {
        if (curve.Count < 2 || curve.Any(p => !double.IsFinite(p.Prominence) || p.Prominence < 0 ||
            !double.IsFinite(p.PlaneMm) || p.PlaneMm <= 0) ||
            curve.Zip(curve.Skip(1)).Any(p => p.First.PlaneMm >= p.Second.PlaneMm))
            throw new ArgumentException("A focus curve needs finite increasing planes and nonnegative prominence.", nameof(curve));
        int peak = 0;
        for (int i = 1; i < curve.Count; i++) if (curve[i].Prominence > curve[peak].Prominence) peak = i;
        double half = curve[peak].Prominence / 2;
        int left = peak, right = peak;
        while (left > 0 && curve[left - 1].Prominence >= half) left--;
        while (right < curve.Count - 1 && curve[right + 1].Prominence >= half) right++;
        double lo = left == 0 ? curve[0].PlaneMm : Crossing(curve[left - 1], curve[left], half);
        double hi = right == curve.Count - 1 ? curve[^1].PlaneMm : Crossing(curve[right], curve[right + 1], half);
        bool multiple = curve.Take(left).Concat(curve.Skip(right + 1)).Any(p => p.Prominence >= half);
        return new(Array.AsReadOnly(curve.ToArray()), curve[peak], new(lo, hi, left == 0, right == curve.Count - 1),
            multiple, peak == 0 || peak == curve.Count - 1);
    }

    private static double Crossing(FocusSample a, FocusSample b, double half)
        => a.PlaneMm + (b.PlaneMm - a.PlaneMm) * (half - a.Prominence) / (b.Prominence - a.Prominence);
}
