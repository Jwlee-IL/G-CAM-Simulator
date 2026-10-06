using System.Numerics;
using System.Runtime.InteropServices;

namespace Gcam.Simulation;

/// <summary>The stripped trust statistic Z_s (TODO-35, DA-5) on the scenario's own decoder grid.
///
/// Per-pixel stripping with a per-pixel ratio Rᵢ (the calibration map's downscatter ÷ reference count in pixel i; a single
/// global R leaves a residual image because Rᵢ rises towards the array edge): sᵢ = n₆₆₂,ᵢ − Rᵢ·n_ref,ᵢ. The decoder is
/// linear, so recon_s(θ) = Σᵢ sᵢ Gᵢ(θ), with Gᵢ(θ) the ±1 / 0 weights of <see cref="CorrelationSearch"/>. With
/// independent Poisson pixels its variance is Σᵢ Gᵢ(θ)²(λ₆₆₂,ᵢ + Rᵢ²·λ_ref,ᵢ), estimated plug-in from the counts.
/// Under H₀ (no Cs-137) its mean is the stripped background model E₀(θ) = Σᵢ Gᵢ(θ)(b₆₆₂,ᵢ − Rᵢ·b_ref,ᵢ) (zero without an
/// ambient field). Z_s(θ) = (recon_s(θ) − E₀(θ)) / √var(θ); the statistic is its maximum over the grid. Like the gate's
/// Z it is not a Gaussian significance (a maximum over correlated points, low counts): its threshold is calibrated on
/// Cs-free acquisitions.</summary>
public sealed class StrippedSearch
{
    private readonly double[][] _g;      // per pixel: weights over the grid, padded with zeros to whole SIMD blocks
    private readonly int _padded;

    public CorrelationSearch Search { get; }
    /// <summary>True when every weight is ±1 (no grid point a pixel's rays miss): then G² = 1 and the variance is the
    /// same at every grid point.</summary>
    public bool FullSupport { get; }

    public StrippedSearch(CorrelationSearch search)
    {
        Search = search;
        int gp = search.GridPoints;
        _padded = (gp + Vector<double>.Count - 1) / Vector<double>.Count * Vector<double>.Count;
        _g = new double[search.Pixels][];
        var unit = new double[search.Pixels];
        var col = new double[gp];
        bool full = true;
        for (int i = 0; i < search.Pixels; i++)
        {
            unit[i] = 1;
            search.ReconstructExpected(unit, col);
            unit[i] = 0;
            _g[i] = new double[_padded];
            col.CopyTo(_g[i], 0);
            for (int k = 0; k < gp; k++) if (col[k] == 0) full = false;
        }
        FullSupport = full;
    }

    /// <summary>recon(θ) = Σᵢ wᵢ Gᵢ(θ) for any real per-pixel weights (stripped counts, an expected map).</summary>
    public void Reconstruct(ReadOnlySpan<double> weights, double[] recon)
    {
        if (weights.Length != _g.Length || recon.Length < _padded) throw new ArgumentException("Size mismatch (recon must hold PaddedPoints).");
        Array.Clear(recon, 0, _padded);
        var acc = MemoryMarshal.Cast<double, Vector<double>>(recon.AsSpan(0, _padded));
        for (int i = 0; i < _g.Length; i++)
        {
            double w = weights[i];
            if (w == 0) continue;
            var col = MemoryMarshal.Cast<double, Vector<double>>(_g[i].AsSpan());
            var wv = new Vector<double>(w);
            for (int k = 0; k < acc.Length; k++) acc[k] += wv * col[k];
        }
    }

    /// <summary>Σᵢ vᵢ Gᵢ(θ)² (the plug-in variance), scalar when <see cref="FullSupport"/>.</summary>
    public void Variance(ReadOnlySpan<double> v, double[] variance)
    {
        if (FullSupport)
        {
            double s = 0;
            foreach (double x in v) s += x;
            Array.Fill(variance, s, 0, _padded);
            return;
        }
        Array.Clear(variance, 0, _padded);
        for (int i = 0; i < _g.Length; i++)
        {
            double w = v[i];
            if (w == 0) continue;
            var col = _g[i];
            for (int k = 0; k < _padded; k++) variance[k] += w * col[k] * col[k];
        }
    }

    /// <summary>Length of the reconstruction buffers this class fills (grid points rounded up to whole SIMD blocks).</summary>
    public int PaddedPoints => _padded;

    /// <summary>Max over the grid of (recon − e0) / √variance and its grid index; no candidate (−∞, −1) when every
    /// variance is zero (an acquisition without counts).</summary>
    public (double Z, int Index) Max(double[] recon, double[] variance, double[]? e0)
    {
        double best = double.NegativeInfinity; int at = -1;
        for (int k = 0; k < Search.GridPoints; k++)
        {
            double v = variance[k];
            if (!(v > 1e-12)) continue;
            double z = (recon[k] - (e0 is null ? 0 : e0[k])) / Math.Sqrt(v);
            if (z > best) { best = z; at = k; }
        }
        return (best, at);
    }

    /// <summary>Grid index nearest a source-plane point.</summary>
    public int Nearest(double xMm, double yMm)
    {
        var s = Search;
        int gx = Math.Clamp((int)Math.Round((xMm - s.OriginMm) / s.StepMm), 0, s.GridSize - 1);
        int gy = Math.Clamp((int)Math.Round((yMm - s.OriginMm) / s.StepMm), 0, s.GridSize - 1);
        return gy * s.GridSize + gx;
    }

    public (double X, double Y) PointOf(int index)
        => (Search.OriginMm + index % Search.GridSize * Search.StepMm, Search.OriginMm + index / Search.GridSize * Search.StepMm);
}
