using VecF = System.Numerics.Vector<float>;
using System.Runtime.InteropServices;
using Gcam.Core;
using Gcam.Masks;

namespace Gcam.Decoding;

/// <summary>
/// Maximum-Likelihood Expectation-Maximization (MLEM) reconstruction for the coded aperture — a statistical
/// alternative to <see cref="CrossCorrelationDecoder"/>. Cross-correlation is a single linear back-projection with the
/// ±1 decoding array; it is fast but leaves NEGATIVE sidelobes, aliases off-axis sources into a ghost, and merges
/// nearby sources into one blurred peak. MLEM instead iterates the physical FORWARD model to the source distribution
/// λ ≥ 0 that maximizes the Poisson likelihood of the observed detector counts y:
/// <code>
///   λ_j ← (λ_j / s_j) · Σ_i A_ij · y_i / (Σ_j' A_ij' λ_j' + b_i),   s_j = Σ_i A_ij
/// </code>
/// where the system matrix <c>A_ij</c> approximates the probability a photon from source cell j reaches detector
/// pixel i — for a coded aperture, 1 when the ray j→i (pixel CENTRE) crosses an OPEN mask cell, else 0. (It is a
/// binary pixel-centre sample: it does not integrate over pixel area or the cos/r² emission weighting, a good
/// approximation for the compact default geometry.) MLEM is non-negative, has no decoding sidelobes, and DECONVOLVES
/// nearby sources that cross-correlation cannot separate. With the FINITE aperture (<c>Cyclic = false</c>) it also
/// suppresses the partially-coded-FOV ghost that cyclic decoding aliases; it honours the geometry's Cyclic flag. The
/// cost is iteration and the usual MLEM resolution/noise trade-off with iteration count. Opt-in: the pipeline still
/// defaults to cross-correlation.
///
/// Opt-in forward models (TODO-34, DR-6 / DR-7; the default above is unchanged bit for bit): <see cref="MlemSystemModel"/>
/// integrates each pixel's area and lets closed cells and the frame transmit exp(−μt) — sampling pixel centres alone
/// makes a head with one pixel per projected cell split single sources after a few tens of iterations;
/// <see cref="FromColumns"/> takes a supplied matrix (e.g. transported single-source maps); a known background b_i enters
/// the forward model through <see cref="Decode(DetectorImage, IReadOnlyList{double})"/> and <see cref="Snapshots"/>.
/// </summary>
public sealed class MlemDecoder : IDecoder
{
    private readonly MaskPattern? _aperture;   // the physical (finite) mosaic open/closed pattern (null for supplied columns)
    private readonly CodedApertureGeometry _geo;
    private readonly int _iterations;
    private readonly MlemSystemModel? _model;  // null = the engine's binary pixel-centre matrix (the default)
    private readonly IReadOnlyList<double[]>? _columns;   // supplied system matrix (one column per grid point), or null
    private readonly SubCellMethod _subCell;   // opt-in peak refinement of the estimate (None = the original argmax)

    // Cached system matrix A[j*nDet + i] and sensitivity s[j], built once for the image dimensions. The default model keeps
    // its float matrix and scalar loop exactly as before (bit-identical results); the opt-in models keep a double matrix (for the
    // sensitivities and SystemMatrix) and run a single-precision loop vectorised over pixels.
    private float[]? _a;
    private double[]? _ad;
    private float[]? _af;
    private double[]? _sens;
    private int _n, _nDet, _imgW, _imgH;
    private double _origin;

    public MlemDecoder(MaskPattern aperture, CodedApertureGeometry geometry, int iterations = 60)
    {
        _aperture = aperture;
        _geo = geometry;
        _iterations = iterations;
    }

    /// <summary>Opt-in forward model; <paramref name="model"/> null gives exactly the default decoder.</summary>
    public MlemDecoder(MaskPattern aperture, CodedApertureGeometry geometry, int iterations, MlemSystemModel? model)
        : this(aperture, geometry, iterations)
    {
        if (model is { PixelSubSamples: < 1 }) throw new ArgumentOutOfRangeException(nameof(model), "At least one sample per pixel.");
        if (model is not null && !(model.ClosedCellTransmission is >= 0 and <= 1))
            throw new ArgumentOutOfRangeException(nameof(model), "The closed-cell transmission is a fraction.");
        _model = model;
    }

    /// <summary>As <see cref="MlemDecoder(MaskPattern, CodedApertureGeometry, int, MlemSystemModel?)"/>, with the estimate
    /// refined below one grid step by <paramref name="subCell"/> (TODO-36: on the pixel-area λ the tent or gaussian
    /// estimate is 2–4× more precise than the argmax above ~1000 counts). <see cref="SubCellMethod.None"/> gives exactly
    /// the four-argument decoder; λ itself never depends on it.</summary>
    public MlemDecoder(MaskPattern aperture, CodedApertureGeometry geometry, int iterations, MlemSystemModel? model, SubCellMethod subCell)
        : this(aperture, geometry, iterations, model)
    {
        _subCell = subCell;
    }

    private MlemDecoder(CodedApertureGeometry geometry, int iterations, IReadOnlyList<double[]> columns, int imgW, int imgH)
    {
        _geo = geometry;
        _iterations = iterations;
        _columns = columns;
        _imgW = imgW; _imgH = imgH;
    }

    /// <summary>MLEM with a supplied system matrix: <paramref name="columns"/>[j][i] = expected counts in pixel i per unit
    /// source at grid point j (row-major over this geometry's reconstruction grid), e.g. transported single-source maps —
    /// the "matched" forward model, an upper bound when the data come from the same transport.</summary>
    public static MlemDecoder FromColumns(CodedApertureGeometry geometry, int imgW, int imgH, IReadOnlyList<double[]> columns, int iterations)
    {
        int n = (int)Math.Round(2.0 * geometry.ReconHalfExtentMm / geometry.ReconStepMm) + 1;
        if (columns.Count != n * n) throw new ArgumentException($"Need {n * n} columns for the {n}×{n} grid, got {columns.Count}.");
        if (columns.Any(c => c.Length != imgW * imgH || c.Any(v => !(v >= 0) || double.IsInfinity(v))))
            throw new ArgumentException("Each column needs one finite, non-negative value per pixel.");
        return new MlemDecoder(geometry, iterations, columns, imgW, imgH);
    }

    /// <summary>Reconstruction grid: points per side, origin and step (mm on the source plane).</summary>
    public (int Size, double OriginMm, double StepMm) Grid
        => ((int)Math.Round(2.0 * _geo.ReconHalfExtentMm / _geo.ReconStepMm) + 1, -_geo.ReconHalfExtentMm, _geo.ReconStepMm);

    public DecodeResult Decode(DetectorImage image) => Decode(image, null);

    /// <summary>Decode with an optional known background b_i (expected counts per pixel, row-major) in the forward model
    /// Σ_j A_ij λ_j + b_i. Null = no background term.</summary>
    public DecodeResult Decode(DetectorImage image, IReadOnlyList<double>? background)
    {
        // Flatten the detector counts.
        var y = new double[image.Width * image.Height];
        for (int iy = 0; iy < image.Height; iy++)
            for (int ix = 0; ix < image.Width; ix++)
                y[iy * image.Width + ix] = image[ix, iy];
        var lam = Snapshots(y, image.Width, image.Height, [_iterations], background)[0];

        // Reconstruction image + peak estimate.
        var recon = new DetectorImage(_n, _n);
        int bx = 0, by = 0; double peak = double.NegativeInfinity;
        for (int gy = 0; gy < _n; gy++)
            for (int gx = 0; gx < _n; gx++)
            {
                double v = lam[gy * _n + gx];
                recon[gx, gy] = v;
                if (v > peak) { peak = v; bx = gx; by = gy; }
            }

        const int excl = 3;
        double second = double.NegativeInfinity;
        for (int gy = 0; gy < _n; gy++)
            for (int gx = 0; gx < _n; gx++)
                if (Math.Abs(gx - bx) > excl || Math.Abs(gy - by) > excl)
                    if (recon[gx, gy] > second) second = recon[gx, gy];

        var (dx, dy) = _subCell == SubCellMethod.None ? (0.0, 0.0) : PeakInterpolation.Estimate(recon, bx, by, _subCell);
        double estX = _origin + (bx + dx) * _geo.ReconStepMm;
        double estY = _origin + (by + dy) * _geo.ReconStepMm;
        double confidence = peak / Math.Max(second, 1e-9);
        var estimate = new SourceEstimate(new Vector3(estX, estY, _geo.SourcePlaneZ), confidence);
        return new DecodeResult(estimate, recon, _origin, _geo.ReconStepMm);
    }

    /// <summary>λ (row-major over the grid) after each iteration count of <paramref name="iterations"/> (ascending, ≥ 0) for
    /// counts y (row-major, imgW × imgH), with an optional background b_i. The default model runs the original scalar
    /// loop, so a snapshot at the decoder's own iteration count equals <see cref="Decode(DetectorImage)"/>'s image bit for
    /// bit.</summary>
    public double[][] Snapshots(IReadOnlyList<double> y, int imgW, int imgH, IReadOnlyList<int> iterations, IReadOnlyList<double>? background = null)
    {
        if (iterations.Count == 0) throw new ArgumentException("No iteration count requested.");
        for (int k = 0; k < iterations.Count; k++)
            if (iterations[k] < 0 || (k > 0 && iterations[k] < iterations[k - 1]))
                throw new ArgumentException("Iteration counts must be ascending and non-negative.");
        if (y.Count != imgW * imgH) throw new ArgumentException("Counts do not match the image size.");
        if (background is not null && background.Count != imgW * imgH) throw new ArgumentException("Background does not match the image size.");
        PrepareMatrix(imgW, imgH);
        int nSrc = _n * _n, nDet = _nDet;
        var sens = _sens!;
        var a = _a;
        var af = _af;
        var fbarF = af is null ? null : new float[nDet];

        // MLEM: start from a flat non-negative estimate and iterate.
        var lam = new double[nSrc];
        Array.Fill(lam, 1.0);
        var fbar = new double[nDet];
        var corr = new double[nSrc];
        var output = new double[iterations.Count][];
        int next = 0;
        while (next < iterations.Count && iterations[next] == 0) output[next++] = (double[])lam.Clone();
        for (int it = 0; next < iterations.Count; it++)
        {
            if (a is not null)
            {
                // The engine's loop, unchanged (float matrix, scalar sums in this order).
                // forward project: fbar_i = Σ_j A_ij λ_j (+ b_i)
                if (background is null) Array.Clear(fbar, 0, nDet);
                else for (int i = 0; i < nDet; i++) fbar[i] = background[i];
                for (int j = 0; j < nSrc; j++)
                {
                    double lj = lam[j];
                    if (lj == 0.0) continue;
                    int baseIdx = j * nDet;
                    for (int i = 0; i < nDet; i++) fbar[i] += a[baseIdx + i] * lj;
                }
                // ratio and back-project: corr_j = Σ_i A_ij (y_i / fbar_i)
                Array.Clear(corr, 0, nSrc);
                for (int i = 0; i < nDet; i++)
                    fbar[i] = fbar[i] > 1e-12 ? y[i] / fbar[i] : 0.0;
                for (int j = 0; j < nSrc; j++)
                {
                    int baseIdx = j * nDet;
                    double c = 0.0;
                    for (int i = 0; i < nDet; i++) c += a[baseIdx + i] * fbar[i];
                    corr[j] = c;
                }
            }
            else
            {
                IterateVector(af!, lam, y, background, fbarF!, corr, nSrc, nDet);
            }
            // multiplicative update
            for (int j = 0; j < nSrc; j++)
                lam[j] = sens[j] > 0.0 ? lam[j] * corr[j] / sens[j] : 0.0;
            while (next < iterations.Count && iterations[next] == it + 1) output[next++] = (double[])lam.Clone();
        }
        return output;
    }

    /// <summary>The system matrix this decoder inverts for an imgW × imgH image, row-major A[j·nDet + i] (a copy).</summary>
    public double[] SystemMatrix(int imgW, int imgH)
    {
        PrepareMatrix(imgW, imgH);
        return _a is not null ? _a.Select(v => (double)v).ToArray() : (double[])_ad!.Clone();
    }

    private void PrepareMatrix(int imgW, int imgH)
    {
        if (_columns is not null)
        {
            if (imgW != _imgW || imgH != _imgH) throw new ArgumentException("Image size differs from the supplied columns.");
            EnsureColumns();
        }
        else if (_model is null) BuildSystemMatrix(imgW, imgH);
        else BuildAreaMatrix(imgW, imgH);
    }

    // One forward and one back projection with the opt-in matrix, in single precision vectorised over pixels (each source's
    // row is contiguous): the loop is memory-bound, and float halves the traffic. λ, the sensitivities and the update stay
    // double; a sum of nDet float products carries a relative error ≤ (nDet − 1)·2⁻²⁴ (Higham's recursive-summation bound).
    private static void IterateVector(float[] af, double[] lam, IReadOnlyList<double> y, IReadOnlyList<double>? background,
        float[] fbar, double[] corr, int nSrc, int nDet)
    {
        if (background is null) Array.Clear(fbar, 0, nDet);
        else for (int i = 0; i < nDet; i++) fbar[i] = (float)background[i];
        int whole = nDet / VecF.Count * VecF.Count;
        var fv = MemoryMarshal.Cast<float, VecF>(fbar.AsSpan(0, whole));
        for (int j = 0; j < nSrc; j++)
        {
            float lj = (float)lam[j];
            if (lj == 0f) continue;
            var row = af.AsSpan(j * nDet, nDet);
            var rv = MemoryMarshal.Cast<float, VecF>(row[..whole]);
            var l = new VecF(lj);
            for (int k = 0; k < fv.Length; k++) fv[k] += rv[k] * l;
            for (int i = whole; i < nDet; i++) fbar[i] += row[i] * lj;
        }
        for (int i = 0; i < nDet; i++)
            fbar[i] = fbar[i] > 1e-12f ? (float)(y[i] / fbar[i]) : 0f;
        var qv = MemoryMarshal.Cast<float, VecF>(fbar.AsSpan(0, whole));
        for (int j = 0; j < nSrc; j++)
        {
            var row = af.AsSpan(j * nDet, nDet);
            var rv = MemoryMarshal.Cast<float, VecF>(row[..whole]);
            var acc = VecF.Zero;
            for (int k = 0; k < qv.Length; k++) acc += rv[k] * qv[k];
            double c = System.Numerics.Vector.Sum(acc);
            for (int i = whole; i < nDet; i++) c += row[i] * fbar[i];
            corr[j] = c;
        }
    }

    private void EnsureColumns()
    {
        if (_ad != null) return;
        _n = (int)Math.Round(2.0 * _geo.ReconHalfExtentMm / _geo.ReconStepMm) + 1;
        _origin = -_geo.ReconHalfExtentMm;
        _nDet = _imgW * _imgH;
        var ad = new double[_n * _n * _nDet];
        var sens = new double[_n * _n];
        for (int j = 0; j < _n * _n; j++)
        {
            Array.Copy(_columns![j], 0, ad, j * _nDet, _nDet);
            sens[j] = _columns[j].Sum();
        }
        _ad = ad; _af = Array.ConvertAll(ad, v => (float)v); _sens = sens;
    }

    /// <summary>Analytic pixel-area matrix: A_ij = mean over PixelSubSamples² points of pixel i (a regular grid at the
    /// sub-cell centres) of the thin-mask transmission along the ray to grid point j — 1 through an open cell,
    /// ClosedCellTransmission through a closed cell or the surrounding frame (no oblique channel clipping, no crystal
    /// response). With one sample and zero transmission it is the default binary matrix.</summary>
    private void BuildAreaMatrix(int imgW, int imgH)
    {
        if (_ad != null && imgW == _imgW && imgH == _imgH) return;
        var model = _model!;
        var aperture = _aperture!;
        _imgW = imgW; _imgH = imgH;
        _n = (int)Math.Round(2.0 * _geo.ReconHalfExtentMm / _geo.ReconStepMm) + 1;
        _origin = -_geo.ReconHalfExtentMm;
        _nDet = imgW * imgH;
        int nSrc = _n * _n, nDet = _nDet, sub = model.PixelSubSamples, n = _n;
        double t = model.ClosedCellTransmission, origin = _origin, step = _geo.ReconStepMm, pitch = _geo.DetectorPitchMm;
        double cell = _geo.MaskCellPitchMm;
        bool cyclic = _geo.Cyclic;
        double detHalfW = imgW * pitch / 2.0;
        double detHalfH = imgH * pitch / 2.0;
        double maskHalfW = _geo.MaskCellsX * cell / 2.0;
        double maskHalfH = _geo.MaskCellsY * cell / 2.0;
        double frac = (_geo.MaskPlaneZ - _geo.DetectorPlaneZ) / (_geo.SourcePlaneZ - _geo.DetectorPlaneZ);
        var ad = new double[(long)nSrc * nDet <= int.MaxValue ? nSrc * nDet : 0];
        var sens = new double[nSrc];
        Parallel.For(0, nSrc, j =>
        {
            double sxp = origin + j % n * step;
            double syp = origin + j / n * step;
            int baseIdx = j * nDet;
            double s = 0.0;
            for (int iy = 0; iy < imgH; iy++)
                for (int ix = 0; ix < imgW; ix++)
                {
                    double acc = 0.0;
                    for (int b = 0; b < sub; b++)
                    {
                        double py = (iy + (b + 0.5) / sub) * pitch - detHalfH;
                        for (int a = 0; a < sub; a++)
                        {
                            double px = (ix + (a + 0.5) / sub) * pitch - detHalfW;
                            double mx = px + (sxp - px) * frac;
                            double my = py + (syp - py) * frac;
                            int cx = (int)Math.Floor((mx + maskHalfW) / cell);
                            int cy = (int)Math.Floor((my + maskHalfH) / cell);
                            bool open = cyclic
                                ? aperture[Mod(cx, aperture.Width), Mod(cy, aperture.Height)]
                                : cx >= 0 && cy >= 0 && cx < aperture.Width && cy < aperture.Height && aperture[cx, cy];
                            acc += open ? 1.0 : t;
                        }
                    }
                    double v = acc / (sub * sub);
                    ad[baseIdx + iy * imgW + ix] = v;
                    s += v;
                }
            sens[j] = s;
        });
        _ad = ad;
        _af = Array.ConvertAll(ad, v => (float)v);
        _sens = sens;
    }

    private void BuildSystemMatrix(int imgW, int imgH)
    {
        if (_a != null && imgW == _imgW && imgH == _imgH) return;
        _imgW = imgW; _imgH = imgH;
        _n = (int)Math.Round(2.0 * _geo.ReconHalfExtentMm / _geo.ReconStepMm) + 1;
        _origin = -_geo.ReconHalfExtentMm;
        _nDet = imgW * imgH;
        int nSrc = _n * _n;
        var aperture = _aperture!;

        double detHalfW = imgW * _geo.DetectorPitchMm / 2.0;
        double detHalfH = imgH * _geo.DetectorPitchMm / 2.0;
        double maskHalfW = _geo.MaskCellsX * _geo.MaskCellPitchMm / 2.0;
        double maskHalfH = _geo.MaskCellsY * _geo.MaskCellPitchMm / 2.0;
        double frac = (_geo.MaskPlaneZ - _geo.DetectorPlaneZ) / (_geo.SourcePlaneZ - _geo.DetectorPlaneZ);

        var a = new float[(long)nSrc * _nDet <= int.MaxValue ? nSrc * _nDet : 0];
        var sens = new double[nSrc];
        for (int gy = 0; gy < _n; gy++)
            for (int gx = 0; gx < _n; gx++)
            {
                int j = gy * _n + gx;
                double sxp = _origin + gx * _geo.ReconStepMm;
                double syp = _origin + gy * _geo.ReconStepMm;
                int baseIdx = j * _nDet;
                double s = 0.0;
                for (int iy = 0; iy < imgH; iy++)
                {
                    double py = (iy + 0.5) * _geo.DetectorPitchMm - detHalfH;
                    for (int ix = 0; ix < imgW; ix++)
                    {
                        double px = (ix + 0.5) * _geo.DetectorPitchMm - detHalfW;
                        // Back-project this pixel through the mask plane for source (sxp, syp).
                        double mx = px + (sxp - px) * frac;
                        double my = py + (syp - py) * frac;
                        int cx = (int)Math.Floor((mx + maskHalfW) / _geo.MaskCellPitchMm);
                        int cy = (int)Math.Floor((my + maskHalfH) / _geo.MaskCellPitchMm);
                        bool open;
                        if (_geo.Cyclic)
                            open = aperture[Mod(cx, aperture.Width), Mod(cy, aperture.Height)];
                        else
                            open = cx >= 0 && cy >= 0 && cx < aperture.Width && cy < aperture.Height && aperture[cx, cy];
                        if (open) { a[baseIdx + iy * imgW + ix] = 1f; s += 1.0; }
                    }
                }
                sens[j] = s;
            }
        _a = a;
        _sens = sens;
    }

    private static int Mod(int a, int m) => ((a % m) + m) % m;
}

/// <summary>Opt-in MLEM forward model (<see cref="MlemDecoder"/>): <paramref name="PixelSubSamples"/>² sample points per
/// pixel (area integration; 1 = the pixel centre) and the transmission of a closed cell or the frame (0 = opaque;
/// exp(−μt) of the slab at the line energy).</summary>
public sealed record MlemSystemModel(int PixelSubSamples, double ClosedCellTransmission);
