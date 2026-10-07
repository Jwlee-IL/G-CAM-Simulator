using System.Runtime.InteropServices;
using VF = System.Numerics.Vector<float>;

namespace Gcam.Decoding;

/// <summary>Poisson EM for mu_i = sum_j A_ij lambda_j + beta p_i, with beta >= 0 and sum p=1. A supplied pixel-area
/// matrix stays separate from the background column. Float SIMD projection, double sensitivities/updates; no detector
/// transport or generating ambient knowledge is read here. Intended for scoped single-source localization.</summary>
public sealed class JointBackgroundMlem
{
    private readonly double[] _matrix;
    private readonly float[] _floatMatrix;
    private readonly double[] _sensitivity;
    public int Pixels { get; }
    public int SourcePoints { get; }

    public JointBackgroundMlem(IReadOnlyList<double> matrix, int pixels)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (pixels < 1 || matrix.Count == 0 || matrix.Count % pixels != 0 || matrix.Any(v => !double.IsFinite(v) || v < 0 || v > float.MaxValue))
            throw new ArgumentException("A non-negative finite source-major matrix is required.");
        Pixels = pixels; SourcePoints = matrix.Count / pixels; _matrix = matrix.ToArray();
        _floatMatrix = _matrix.Select(v => (float)v).ToArray(); _sensitivity = new double[SourcePoints];
        for (int j = 0; j < SourcePoints; j++)
        {
            for (int i = 0; i < Pixels; i++) _sensitivity[j] += _matrix[j * Pixels + i];
            if (!(_sensitivity[j] > 0) || !double.IsFinite(_sensitivity[j])) throw new ArgumentException("Every source column needs positive sensitivity.");
        }
    }

    /// <summary>Initial expected counts are half source / half background by default, independent of source-grid
    /// density. Fraction zero tests the exact beta=0 EM boundary; it is not used to silently bypass fitting.</summary>
    public JointMlemSnapshot[] Snapshots(IReadOnlyList<double> counts, IReadOnlyList<double> shape, IReadOnlyList<int> iterations,
        double initialBackgroundFraction = 0.5, bool diagnostics = false)
    {
        if (counts.Count != Pixels || counts.Any(v => !double.IsFinite(v) || v < 0)) throw new ArgumentException("MLEM needs finite non-negative raw counts.");
        if (shape.Count != Pixels || shape.Any(v => !double.IsFinite(v) || v < 0)) throw new ArgumentException("A finite non-negative background shape is required.");
        double ps = shape.Sum(), total = counts.Sum();
        if (!(ps > 0) || !double.IsFinite(ps) || !double.IsFinite(total)) throw new ArgumentException("Invalid count or shape total.");
        if (iterations.Count == 0 || iterations[0] < 0 || iterations.Zip(iterations.Skip(1), (a, b) => b < a).Any(v => v))
            throw new ArgumentException("Snapshots must be non-negative ascending iteration counts.");
        if (!(initialBackgroundFraction >= 0 && initialBackgroundFraction < 1)) throw new ArgumentOutOfRangeException(nameof(initialBackgroundFraction));
        var p = shape.Select(v => v / ps).ToArray();
        var source = new double[SourcePoints]; double beta = total * initialBackgroundFraction;
        for (int j = 0; j < SourcePoints; j++) source[j] = total * (1 - initialBackgroundFraction) / (SourcePoints * _sensitivity[j]);
        var projected = new float[Pixels]; int whole = Pixels / VF.Count * VF.Count;
        var result = new JointMlemSnapshot[iterations.Count]; int next = 0;
        for (int it = 0; next < iterations.Count; it++)
        {
            while (next < iterations.Count && iterations[next] == it)
            {
                double expected = beta;
                for (int j = 0; j < SourcePoints; j++) expected += source[j] * _sensitivity[j];
                result[next++] = new(it, (double[])source.Clone(), beta, diagnostics ? Likelihood(counts, p, source, beta) : null, expected);
            }
            if (next == iterations.Count) break;
            for (int i = 0; i < Pixels; i++) projected[i] = (float)(beta * p[i]);
            var fv = MemoryMarshal.Cast<float, VF>(projected.AsSpan(0, whole));
            for (int j = 0; j < SourcePoints; j++)
            {
                var av = MemoryMarshal.Cast<float, VF>(_floatMatrix.AsSpan(j * Pixels, whole)); var lv = new VF((float)source[j]);
                for (int k = 0; k < fv.Length; k++) fv[k] += av[k] * lv;
                for (int i = whole; i < Pixels; i++) projected[i] += _floatMatrix[j * Pixels + i] * (float)source[j];
            }
            double correction = 0;
            for (int i = 0; i < Pixels; i++)
            {
                if (counts[i] > 0 && !(projected[i] > 0)) throw new InvalidOperationException("Positive data outside the forward-model support.");
                projected[i] = projected[i] > 0 ? (float)(counts[i] / projected[i]) : 0;
                correction += p[i] * projected[i];
            }
            for (int j = 0; j < SourcePoints; j++)
            {
                var av = MemoryMarshal.Cast<float, VF>(_floatMatrix.AsSpan(j * Pixels, whole)); var acc = VF.Zero;
                for (int k = 0; k < fv.Length; k++) acc += av[k] * fv[k];
                double c = System.Numerics.Vector.Sum(acc);
                for (int i = whole; i < Pixels; i++) c += _floatMatrix[j * Pixels + i] * projected[i];
                source[j] *= c / _sensitivity[j];
            }
            beta *= correction;
        }
        return result;
    }

    private double Likelihood(IReadOnlyList<double> y, double[] p, double[] source, double beta)
    {
        double l = 0;
        for (int i = 0; i < Pixels; i++)
        {
            double mu = beta * p[i];
            for (int j = 0; j < SourcePoints; j++) mu += _matrix[j * Pixels + i] * source[j];
            if (!(mu > 0)) { if (y[i] > 0) return double.NegativeInfinity; continue; }
            l += y[i] * Math.Log(mu) - mu;
        }
        return l;
    }
}
