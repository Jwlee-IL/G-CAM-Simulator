using System.Numerics;

namespace Gcam.Studio.Core.Plotting;

/// <summary>O(N) construction, exact extrema using the coarsest aligned blocks within each column.</summary>
public sealed class MinMaxPyramid
{
    private const int BaseBlockShift = 6; // 64-sample blocks bound stored extrema to about N/16 doubles.
    private const int BaseBlockSize = 1 << BaseBlockShift;
    private readonly double[] _samples;
    private readonly List<(double[] Min, double[] Max)> _levels = [];

    /// <summary>Extra doubles stored for extrema, excluding the caller-owned samples.</summary>
    public long StoredDoubleCount { get; private set; }

    public MinMaxPyramid(double[] samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Length > PlotSeries.MaximumSamples) throw new ArgumentOutOfRangeException(nameof(samples));
        for (int i = 0; i < samples.Length; i++)
            if (!double.IsFinite(samples[i])) throw new ArgumentException("Samples must be finite.");
        _samples = samples;
        // Store only complete blocks; an incomplete final block is scanned by Range.
        int blocks = samples.Length / BaseBlockSize;
        if (blocks == 0) return;
        var min = new double[blocks];
        var max = new double[blocks];
        for (int b = 0; b < blocks; b++)
        {
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            for (int i = b * BaseBlockSize; i < (b + 1) * BaseBlockSize; i++)
            {
                lo = Math.Min(lo, samples[i]);
                hi = Math.Max(hi, samples[i]);
            }
            min[b] = lo;
            max[b] = hi;
        }
        _levels.Add((min, max));
        StoredDoubleCount = 2L * blocks;
        while (min.Length > 1)
        {
            var nextMin = new double[min.Length / 2];
            var nextMax = new double[nextMin.Length];
            for (int i = 0; i < nextMin.Length; i++)
            {
                int a = i * 2, b = Math.Min(a + 1, min.Length - 1);
                nextMin[i] = Math.Min(min[a], min[b]);
                nextMax[i] = Math.Max(max[a], max[b]);
            }
            _levels.Add((nextMin, nextMax));
            StoredDoubleCount += 2L * nextMin.Length;
            min = nextMin;
            max = nextMax;
        }
    }

    public PlotRange Range(int start, int end)
    {
        start = Math.Clamp(start, 0, _samples.Length);
        end = Math.Clamp(end, start, _samples.Length);
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        for (int i = start; i < end;)
        {
            int level = BitOperations.Log2((uint)(end - i));
            if (i != 0) level = Math.Min(level, BitOperations.TrailingZeroCount((uint)i));
            if (level < BaseBlockShift)
            {
                int edgeEnd = Math.Min(end, ((i / BaseBlockSize) + 1) * BaseBlockSize);
                for (; i < edgeEnd; i++)
                {
                    min = Math.Min(min, _samples[i]);
                    max = Math.Max(max, _samples[i]);
                }
                continue;
            }
            else
            {
                var block = _levels[level - BaseBlockShift];
                min = Math.Min(min, block.Min[i >> level]);
                max = Math.Max(max, block.Max[i >> level]);
            }
            i += 1 << level;
        }
        return new(start, end, min, max);
    }

    /// <summary>Sample-coordinate range [x0,x1), split into pixel columns; each sample belongs to one column.</summary>
    public IReadOnlyList<PlotRange> Query(double x0, double x1, int columns)
    {
        if (!double.IsFinite(x0) || !double.IsFinite(x1) || x1 < x0 || columns < 1)
            throw new ArgumentOutOfRangeException(nameof(columns));
        var result = new PlotRange[columns];
        for (int c = 0; c < columns; c++)
            result[c] = Range((int)Math.Clamp(Math.Ceiling(x0 + (x1 - x0) * c / columns), 0, _samples.Length),
                (int)Math.Clamp(Math.Ceiling(x0 + (x1 - x0) * (c + 1) / columns), 0, _samples.Length));
        return result;
    }
}
