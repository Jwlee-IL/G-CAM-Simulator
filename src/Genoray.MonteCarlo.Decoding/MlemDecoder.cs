using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Masks;

namespace Genoray.MonteCarlo.Decoding;

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
/// </summary>
public sealed class MlemDecoder : IDecoder
{
    private readonly MaskPattern _aperture;    // the physical (finite) mosaic open/closed pattern
    private readonly CodedApertureGeometry _geo;
    private readonly int _iterations;

    // Cached system matrix A[j*nDet + i] and sensitivity s[j], built once for the image dimensions.
    private float[]? _a;
    private double[]? _sens;
    private int _n, _nDet, _imgW, _imgH;
    private double _origin;

    public MlemDecoder(MaskPattern aperture, CodedApertureGeometry geometry, int iterations = 60)
    {
        _aperture = aperture;
        _geo = geometry;
        _iterations = iterations;
    }

    public DecodeResult Decode(DetectorImage image)
    {
        BuildSystemMatrix(image.Width, image.Height);
        int nSrc = _n * _n, nDet = _nDet;
        var a = _a!;
        var sens = _sens!;

        // Flatten the detector counts.
        var y = new double[nDet];
        for (int iy = 0; iy < image.Height; iy++)
            for (int ix = 0; ix < image.Width; ix++)
                y[iy * image.Width + ix] = image[ix, iy];

        // MLEM: start from a flat non-negative estimate and iterate.
        var lam = new double[nSrc];
        Array.Fill(lam, 1.0);
        var fbar = new double[nDet];
        var corr = new double[nSrc];
        for (int it = 0; it < _iterations; it++)
        {
            // forward project: fbar_i = Σ_j A_ij λ_j
            Array.Clear(fbar, 0, nDet);
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
            // multiplicative update
            for (int j = 0; j < nSrc; j++)
                lam[j] = sens[j] > 0.0 ? lam[j] * corr[j] / sens[j] : 0.0;
        }

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

        double estX = _origin + bx * _geo.ReconStepMm;
        double estY = _origin + by * _geo.ReconStepMm;
        double confidence = peak / Math.Max(second, 1e-9);
        var estimate = new SourceEstimate(new Vector3(estX, estY, _geo.SourcePlaneZ), confidence);
        return new DecodeResult(estimate, recon, _origin, _geo.ReconStepMm);
    }

    private void BuildSystemMatrix(int imgW, int imgH)
    {
        if (_a != null && imgW == _imgW && imgH == _imgH) return;
        _imgW = imgW; _imgH = imgH;
        _n = (int)Math.Round(2.0 * _geo.ReconHalfExtentMm / _geo.ReconStepMm) + 1;
        _origin = -_geo.ReconHalfExtentMm;
        _nDet = imgW * imgH;
        int nSrc = _n * _n;

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
                            open = _aperture[Mod(cx, _aperture.Width), Mod(cy, _aperture.Height)];
                        else
                            open = cx >= 0 && cy >= 0 && cx < _aperture.Width && cy < _aperture.Height && _aperture[cx, cy];
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
