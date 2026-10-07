using System.Numerics;
using System.Runtime.InteropServices;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Decoding;

namespace Gcam.Simulation;

/// <summary>The scenario's own cross-correlation decoder as a matrix, plus a background-studentised search statistic.
///
/// The decoder is linear: recon(θ) = Σᵢ nᵢ Gᵢ(θ) with Gᵢ(θ) ∈ {+1, −1, 0} (0 where a non-cyclic ray misses the mask).
/// G is read off the decoder itself, by decoding one-count images, so <see cref="Reconstruct"/> and
/// <see cref="DecoderEstimate"/> reproduce <see cref="CrossCorrelationDecoder.Decode"/> exactly (tested).
///
/// Search statistic (review §7, the correlation-based alternative to a likelihood search): with the background's
/// spatial shape p (Σp = 1, from the transported field) and the acquisition's total count N taken as the background
/// normalisation (the only nuisance parameter; with a source present this over-estimates the background, which only
/// lowers Z), a background-only acquisition is multinomial(N, p), so for every grid point
/// E[recon] = N·Σ G p and Var[recon] = N·(Σ G² p − (Σ G p)²) exactly. Z(θ) is recon(θ) studentised by those moments;
/// the statistic is its maximum over the whole configured search grid. Z is not a Gaussian significance (low counts,
/// a maximum over ~2400 correlated points): its threshold must be calibrated on background-only acquisitions.</summary>
public sealed class CorrelationSearch
{
    private readonly int[][] _columns;      // per pixel: G over the grid (int for a vectorisable multiply-add)
    private readonly SubCellMethod _subCell;
    // Per-count variance below which a grid point carries no background-contrast information (its weights are
    // constant over the background, e.g. a non-cyclic point whose rays all miss the mask): far below any real V₁ ~ 0.1–1.
    private const double MinimumVariance = 1e-12;

    /// <summary>Per-count mean and variance of the reconstruction under one background shape.</summary>
    public sealed record BackgroundModel(double[] Expected, double[] Variance);

    public int Pixels { get; }
    public int GridSize { get; }
    public int GridPoints => GridSize * GridSize;
    public double OriginMm { get; }
    public double StepMm { get; }
    public double SourcePlaneZMm { get; }

    public CorrelationSearch(SimulationConfig config)
    {
        if (config.Decoder.Method != DecoderMethod.CrossCorrelation)
            throw new ArgumentException($"CorrelationSearch is the cross-correlation decoder as a ±1 matrix; Decoder.Method = {config.Decoder.Method} has no such form.", nameof(config));
        var decoder = new DefaultSimulationFactory().CreateDecoder(config) ?? throw new ArgumentException("No decoder configured.");
        var d = config.Detector;
        Pixels = d.PixelsX * d.PixelsY;
        _subCell = config.Decoder.SubCellInterpolation;
        SourcePlaneZMm = config.Geometry.MaskDetectorDistanceMm + config.Geometry.SourceMaskDistanceMm;
        _columns = new int[Pixels][];
        var unit = new DetectorImage(d.PixelsX, d.PixelsY);
        for (int i = 0; i < Pixels; i++)
        {
            int x = i % d.PixelsX, y = i / d.PixelsX;
            unit[x, y] = 1;
            var r = decoder.Decode(unit);
            unit[x, y] = 0;
            if (i == 0) { GridSize = r.Reconstruction.Width; OriginMm = r.ReconOriginMm; StepMm = r.ReconStepMm; }
            // The weights are ±1 / 0 by construction; refuse anything else instead of truncating it silently.
            _columns[i] = r.Reconstruction.Raw.ToArray().Select(v => v == Math.Round(v) ? checked((int)v)
                : throw new InvalidOperationException("Decoder weights are not integral; the matrix form does not apply.")).ToArray();
        }
        // The same weights as bytes, padded to whole SIMD blocks with zeros: a quarter of the memory traffic for the
        // narrow path of Reconstruct (the wide field-of-view grid is ~20 MB of int weights, which bounds its speed).
        _paddedPoints = (GridPoints + Vector<sbyte>.Count - 1) / Vector<sbyte>.Count * Vector<sbyte>.Count;
        _narrow = _columns.Select(col =>
        {
            var b = new sbyte[_paddedPoints];
            for (int k = 0; k < col.Length; k++) b[k] = col[k] is >= -1 and <= 1 ? (sbyte)col[k]
                : throw new InvalidOperationException("Decoder weights outside ±1; the narrow path does not apply.");
            return b;
        }).ToArray();
    }

    private readonly sbyte[][] _narrow;
    private readonly int _paddedPoints;

    /// <summary>recon = G n, accumulated pixel by pixel (exact integers, so the SIMD multiply-add gives the same result as a
    /// scalar loop and as the decoder; counts may be negative, e.g. a mask − antimask difference). Images with at most
    /// 32767 counts in absolute sum go through 8-bit weights and 16-bit sums (exact by the bound), larger ones through
    /// 32-bit.</summary>
    public void Reconstruct(ReadOnlySpan<int> counts, Span<double> recon)
    {
        // |recon(θ)| ≤ Σ|nᵢ| because every weight is ±1 or 0: within ±32767 the 16-bit accumulation is exact too.
        long magnitude = 0;
        for (int i = 0; i < Pixels; i++) magnitude += Math.Abs((long)counts[i]);
        if (magnitude <= short.MaxValue) { ReconstructNarrow(counts, recon); return; }
        Span<int> acc = GridPoints <= 8192 ? stackalloc int[GridPoints] : new int[GridPoints];
        acc.Clear();
        var accV = MemoryMarshal.Cast<int, Vector<int>>(acc);
        int tail = accV.Length * Vector<int>.Count;
        for (int i = 0; i < Pixels; i++)
        {
            int n = counts[i];
            if (n == 0) continue;
            var col = _columns[i];
            var colV = MemoryMarshal.Cast<int, Vector<int>>(col.AsSpan());
            var nV = new Vector<int>(n);
            for (int k = 0; k < accV.Length; k++) accV[k] += nV * colV[k];
            for (int k = tail; k < col.Length; k++) acc[k] += n * col[k];
        }
        for (int k = 0; k < GridPoints; k++) recon[k] = acc[k];
    }

    /// <summary>recon = G m for a real-valued image (an expected map, a model background): the same weights, summed in
    /// double pixel by pixel in the decoder's order.</summary>
    public void ReconstructExpected(ReadOnlySpan<double> map, Span<double> recon)
    {
        if (map.Length != Pixels || recon.Length < GridPoints) throw new ArgumentException("Map or reconstruction size mismatch.");
        recon[..GridPoints].Clear();
        for (int i = 0; i < Pixels; i++)
        {
            double m = map[i];
            if (m == 0) continue;
            var col = _columns[i];
            for (int k = 0; k < col.Length; k++) recon[k] += m * col[k];
        }
    }

    private void ReconstructNarrow(ReadOnlySpan<int> counts, Span<double> recon)
    {
        Span<short> acc = _paddedPoints <= 32768 ? stackalloc short[_paddedPoints] : new short[_paddedPoints];
        acc.Clear();
        var accV = MemoryMarshal.Cast<short, Vector<short>>(acc);    // two short blocks per sbyte block
        for (int i = 0; i < Pixels; i++)
        {
            int n = counts[i];
            if (n == 0) continue;
            var colV = MemoryMarshal.Cast<sbyte, Vector<sbyte>>(_narrow[i].AsSpan());
            var nV = new Vector<short>((short)n);
            for (int k = 0; k < colV.Length; k++)
            {
                Vector.Widen(colV[k], out Vector<short> low, out Vector<short> high);
                accV[2 * k] += low * nV;
                accV[2 * k + 1] += high * nV;
            }
        }
        for (int k = 0; k < GridPoints; k++) recon[k] = acc[k];
    }

    /// <summary>The decoder's own estimate from a reconstruction: first maximum in row-major order, then its sub-cell
    /// interpolation — the same steps as <see cref="CrossCorrelationDecoder.Decode"/>.</summary>
    public (double X, double Y) DecoderEstimate(double[] recon)
    {
        var image = new DetectorImage(GridSize, GridSize);
        for (int k = 0; k < recon.Length; k++) image[k % GridSize, k / GridSize] = recon[k];
        var (bx, by) = PeakInterpolation.Argmax(image);
        var (dx, dy) = PeakInterpolation.Estimate(image, bx, by, _subCell);
        return (OriginMm + (bx + dx) * StepMm, OriginMm + (by + dy) * StepMm);
    }

    /// <summary>Per-count moments of recon under a background of shape p (any non-negative map; normalised here):
    /// E₁(θ) = Σ G p and V₁(θ) = Σ G² p − E₁².</summary>
    public BackgroundModel ModelFor(IReadOnlyList<double> shape)
    {
        double total = shape.Sum();
        if (shape.Count != Pixels || !(total > 0)) throw new ArgumentException("Background shape needs one positive total over all pixels.");
        var expected = new double[GridPoints];
        var second = new double[GridPoints];
        for (int i = 0; i < Pixels; i++)
        {
            double p = shape[i] / total;
            if (p == 0) continue;
            var col = _columns[i];
            for (int k = 0; k < GridPoints; k++) { expected[k] += p * col[k]; second[k] += p * col[k] * col[k]; }
        }
        var variance = new double[GridPoints];
        for (int k = 0; k < GridPoints; k++) variance[k] = second[k] - expected[k] * expected[k];
        return new BackgroundModel(expected, variance);
    }

    /// <summary>Max over the grid of Z(θ) = (recon(θ) − N·E₁(θ)) / √(N·V₁(θ)), and where it is. N = 0, or a grid point
    /// whose correlation weights are constant over the background (V₁ = 0, no information), gives no candidate.</summary>
    public (double Z, int Index, double X, double Y) Search(BackgroundModel model, int total, double[] recon)
    {
        double best = double.NegativeInfinity; int at = -1;
        if (total > 0)
            for (int k = 0; k < GridPoints; k++)
            {
                double v = model.Variance[k];
                if (!(v > MinimumVariance)) continue;
                double z = (recon[k] - total * model.Expected[k]) / Math.Sqrt(total * v);
                if (z > best) { best = z; at = k; }
            }
        return at < 0 ? (double.NegativeInfinity, -1, double.NaN, double.NaN)
            : (best, at, OriginMm + at % GridSize * StepMm, OriginMm + at / GridSize * StepMm);
    }

    /// <summary>Angle (degrees) between the directions from the detector centre to two points of the source plane.</summary>
    public double AngleBetweenDeg(double x1, double y1, double x2, double y2)
    {
        double z = SourcePlaneZMm;
        double dot = x1 * x2 + y1 * y2 + z * z;
        double n1 = Math.Sqrt(x1 * x1 + y1 * y1 + z * z), n2 = Math.Sqrt(x2 * x2 + y2 * y2 + z * z);
        return Math.Acos(Math.Clamp(dot / (n1 * n2), -1, 1)) * 180 / Math.PI;
    }
}
