using Gcam.Configuration;
using Gcam.Core;
using Gcam.Decoding;
using Gcam.Masks;
using Gcam.Simulation;
using Gcam.Tests.Harness;
using Xunit;

namespace Gcam.Tests;

/// <summary>TODO-34 (DR-6, DR-7): the opt-in MLEM forward models and background term, and the guarantee that the default
/// decoder is unchanged bit for bit (EV-01 / EV-11 / EV-34 rest on it). Every tolerance is derived in place.</summary>
public sealed class MlemOptionTests
{
    private const double FloatUnit = 1.0 / (1 << 24);   // unit roundoff of float (2⁻²⁴)
    private const double DoubleUnit = 1.0 / (1L << 53); // unit roundoff of double (2⁻⁵³)

    private static (MaskPattern Pattern, CodedApertureGeometry Geo, int W, int H) Head(string scenario, double sourceDetectorMm, bool cyclic)
    {
        var c = ConfigLoader.Load(RepoPaths.Sample(scenario));
        if (sourceDetectorMm > 0) c.Geometry.SourceMaskDistanceMm = sourceDetectorMm - c.Geometry.MaskDetectorDistanceMm;
        var p = MuraGenerator.Mosaic(c.Mask.Rank, c.Mask.MosaicX, c.Mask.MosaicY);
        double d = c.Geometry.MaskDetectorDistanceMm, z = d + c.Geometry.SourceMaskDistanceMm, period = c.Mask.Rank * c.Mask.CellPitchMm * z / d;
        return (p, new CodedApertureGeometry(c.Mask.Rank, d, c.Mask.CellPitchMm, p.Width, p.Height, 0, c.Detector.PixelPitchMm, z,
            period / 2, period / 48, cyclic), c.Detector.PixelsX, c.Detector.PixelsY);
    }

    // A deterministic, structured count image (no Monte Carlo): integer counts with a coded-looking modulation.
    private static DetectorImage Counts(int w, int h)
    {
        var img = new DetectorImage(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) img[x, y] = 3 + (x * 7 + y * 13) % 11 + ((x ^ y) & 1) * 5;
        return img;
    }

    public static TheoryData<string, double, bool, int> Cases => new()
    {
        { "scenario.json", 0, true, 80 },             // EV-11's lab geometry, cyclic
        { "scenario.json", 0, false, 33 },
        { "scenario_handheld.json", 0, true, 60 },    // the hand-held head at its scenario distance (EV-34's sweep points)
        { "scenario_handheld.json", 1000, false, 60 },
        { "scenario_handheld.json", 5000, false, 17 },
        { "scenario_handheld.json", 1000, false, 0 }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void DefaultDecoder_IsBitIdenticalToThePreChangeAlgorithm(string scenario, double sd, bool cyclic, int iterations)
    {
        // Reference = a verbatim copy of MlemDecoder.Decode / BuildSystemMatrix before TODO-34 (git 5977c23). Exact equality.
        var (p, geo, w, h) = Head(scenario, sd, cyclic);
        var img = Counts(w, h);
        var expected = Reference(p, geo, iterations, img);
        foreach (var r in new[]
                 {
                     new MlemDecoder(p, geo, iterations).Decode(img), new MlemDecoder(p, geo, iterations, null).Decode(img),
                     new MlemDecoder(p, geo, iterations).Decode(img, null)
                 })
        {
            Assert.Equal(expected.Lambda.Length, r.Reconstruction.Raw.Length);
            for (int k = 0; k < expected.Lambda.Length; k++)
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Lambda[k]), BitConverter.DoubleToInt64Bits(r.Reconstruction.Raw[k]));
            Assert.Equal(expected.X, r.Estimate.Position.X);
            Assert.Equal(expected.Y, r.Estimate.Position.Y);
            Assert.Equal(expected.Confidence, r.Estimate.Confidence);
        }
        // A zero background enters as fbar_i = 0 + Σ…, the same sums as a cleared buffer: also bit-identical.
        var zero = new MlemDecoder(p, geo, iterations).Decode(img, new double[w * h]).Reconstruction.Raw;
        for (int k = 0; k < expected.Lambda.Length; k++)
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Lambda[k]), BitConverter.DoubleToInt64Bits(zero[k]));
    }

    [Fact]
    public void Snapshots_OfTheDefaultLoop_EqualDecodeAtEachIterationCount()
    {
        var (p, geo, w, h) = Head("scenario_handheld.json", 1000, false);
        var img = Counts(w, h);
        var y = img.Raw.ToArray();
        int[] its = [0, 5, 20, 40];
        var snaps = new MlemDecoder(p, geo, 40).Snapshots(y, w, h, its);
        for (int k = 0; k < its.Length; k++)
        {
            var d = new MlemDecoder(p, geo, its[k]).Decode(img).Reconstruction.Raw;
            for (int j = 0; j < d.Length; j++) Assert.Equal(BitConverter.DoubleToInt64Bits(d[j]), BitConverter.DoubleToInt64Bits(snaps[k][j]));
        }
    }

    [Fact]
    public void PixelAreaModel_WithOneSampleAndOpaqueCells_IsTheBinaryMatrix()
    {
        // One sample at the pixel centre ((ix + 0.5/1)·pitch, the same expression) and t = 0: identical entries.
        var (p, geo, w, h) = Head("scenario_handheld.json", 1000, false);
        var binary = new MlemDecoder(p, geo, 1).SystemMatrix(w, h);
        var area = new MlemDecoder(p, geo, 1, new MlemSystemModel(1, 0)).SystemMatrix(w, h);
        Assert.Equal(binary, area);
    }

    [Fact]
    public void PixelAreaModel_EntriesAreSampleAveragesOfOneAndT()
    {
        // A_ij = (k + (s² − k)·t) / s² for an integer k of open samples: (A − t)/(1 − t)·s² is an integer. The sum of s² terms
        // and one division carry a rounding error ≤ (s² + 1)·u relative, so the integer test holds to s²·(s² + 2)·u.
        const int sub = 8;
        const double t = 0.1685;
        var (p, geo, w, h) = Head("scenario_handheld.json", 1000, false);
        var a = new MlemDecoder(p, geo, 1, new MlemSystemModel(sub, t)).SystemMatrix(w, h);
        double tol = sub * sub * (sub * sub + 2) * DoubleUnit / (1 - t);
        foreach (double v in a)
        {
            Assert.InRange(v, t - 1e-15, 1 + 1e-15);
            double k = (v - t) / (1 - t) * sub * sub;
            Assert.True(Math.Abs(k - Math.Round(k)) <= tol + sub * sub * DoubleUnit, $"entry {v} is not a sample average");
        }
        // Fully transmitting cells (t = 1) make every entry exactly 1.
        Assert.All(new MlemDecoder(p, geo, 1, new MlemSystemModel(2, 1)).SystemMatrix(w, h), v => Assert.Equal(1.0, v));
    }

    [Fact]
    public void VectorLoop_OnTheBinaryMatrix_AgreesWithTheDefaultLoop()
    {
        // Same matrix, single-precision sums: per iteration the forward sum over nSrc terms and the back sum over nDet terms
        // carry relative errors ≤ (nSrc − 1)·u_f and (nDet − 1)·u_f (recursive summation), plus u_f each for the float casts
        // of λ_j and the ratio; with the EM map not amplifying relative perturbations, K iterations stay within
        // K·(nSrc + nDet + 2)·u_f of the double result, measured against the largest λ.
        var (p, geo, w, h) = Head("scenario_handheld.json", 1000, false);
        var y = Counts(w, h).Raw.ToArray();
        const int k = 8;
        var exact = new MlemDecoder(p, geo, k).Snapshots(y, w, h, [k])[0];
        var vec = new MlemDecoder(p, geo, k, new MlemSystemModel(1, 0)).Snapshots(y, w, h, [k])[0];
        int nSrc = exact.Length, nDet = w * h;
        double tol = k * (nSrc + nDet + 2) * FloatUnit * exact.Max();
        for (int j = 0; j < nSrc; j++) Assert.True(Math.Abs(exact[j] - vec[j]) <= tol, $"λ[{j}] {vec[j]} vs {exact[j]} (tol {tol:E2})");
    }

    [Fact]
    public void SuppliedColumns_ReproduceTheModelTheyCameFrom()
    {
        // FromColumns stores the same doubles and converts them to the same floats: the same loop gives identical results.
        var (p, geo, w, h) = Head("scenario_handheld.json", 1000, false);
        var model = new MlemDecoder(p, geo, 30, new MlemSystemModel(4, 0.17));
        var a = model.SystemMatrix(w, h);
        int nDet = w * h, nSrc = a.Length / nDet;
        var cols = Enumerable.Range(0, nSrc).Select(j => a.AsSpan(j * nDet, nDet).ToArray()).ToArray();
        var y = Counts(w, h).Raw.ToArray();
        var expected = model.Snapshots(y, w, h, [30])[0];
        var got = MlemDecoder.FromColumns(geo, w, h, cols, 30).Snapshots(y, w, h, [30])[0];
        Assert.Equal(expected, got);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Background_DataOfTheModelAtFlatLambda_IsAFixedPoint(bool area)
    {
        // y = A·1 + b: from the flat start every ratio is 1 up to rounding, so λ stays 1. Default loop (double sums of exact
        // 0/1 entries): per iteration ≤ (nSrc + nDet + 2)·u relative; the vector loop: the same count in u_f. K iterations.
        // Without b the same data are not a fixed point (λ moves by far more): the term is used.
        var (p, geo, w, h) = Head("scenario_handheld.json", 1000, false);
        var dec = area ? new MlemDecoder(p, geo, 1, new MlemSystemModel(8, 0.17)) : new MlemDecoder(p, geo, 1);
        var a = dec.SystemMatrix(w, h);
        int nDet = w * h, nSrc = a.Length / nDet;
        var b = Enumerable.Range(0, nDet).Select(i => 1000.0 * (1 + i % 5)).ToArray();   // comparable to Σ_j A_ij (~10³ per pixel)
        var y = new double[nDet];
        for (int i = 0; i < nDet; i++) { y[i] = b[i]; for (int j = 0; j < nSrc; j++) y[i] += a[j * nDet + i]; }
        const int k = 10;
        var lam = dec.Snapshots(y, w, h, [k], b)[0];
        double u = area ? FloatUnit : DoubleUnit;
        double tol = k * (nSrc + nDet + 2) * u;
        for (int j = 0; j < nSrc; j++)
        {
            double s = 0;
            for (int i = 0; i < nDet; i++) s += a[j * nDet + i];
            if (s > 0) Assert.True(Math.Abs(lam[j] - 1) <= tol, $"λ[{j}] {lam[j]} (tol {tol:E2})");
        }
        var without = dec.Snapshots(y, w, h, [k])[0];
        Assert.True(without.Max() - 1 > 100 * tol, "without b the background is pushed into λ");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FarSource_NoiselessModelData_ReconstructAtItsGridPoint(bool area)
    {
        // At 1 m the decoder's grid point k whose column is unique; data = A·e_k (the model's own image of a point at k).
        // MLEM's fixed point reproduces the data; the argmax after 200 iterations is k (exact grid index).
        var (p, geo, w, h) = Head("scenario_handheld.json", 1000, false);
        var dec = area ? new MlemDecoder(p, geo, 200, new MlemSystemModel(8, 0.17)) : new MlemDecoder(p, geo, 200);
        var a = dec.SystemMatrix(w, h);
        int nDet = w * h, nSrc = a.Length / nDet, n = (int)Math.Round(Math.Sqrt(nSrc));
        // The first grid point off axis (scanning outward along a row inside the fully coded field) whose column is unique.
        bool Unique(int k)
        {
            var c = a.AsSpan(k * nDet, nDet);
            for (int j = 0; j < nSrc; j++) if (j != k && a.AsSpan(j * nDet, nDet).SequenceEqual(c)) return false;
            return true;
        }
        int kIndex = Enumerable.Range(3, n / 2 - 6).Select(dx => (n / 2 + 2) * n + n / 2 - dx).First(Unique);
        var col = a.AsSpan(kIndex * nDet, nDet).ToArray();
        var lam = dec.Snapshots(col.Select(v => v * 1000).ToArray(), w, h, [200])[0];
        int arg = 0;
        for (int j = 1; j < nSrc; j++) if (lam[j] > lam[arg]) arg = j;
        Assert.Equal(kIndex, arg);
    }

    [Fact]
    public void ClosedCellTransmission_IsTheSlabsBeerLambertFactor()
    {
        // exp(−μ·μ_rel(E)·t) with μ_rel(662 keV) = 1: exp(−0.178 × 10) for the hand-held mask.
        var c = ConfigLoader.Load(RepoPaths.Sample("scenario_handheld.json"));
        Assert.Equal(Math.Exp(-0.178 * CodedApertureMask.TungstenMuRel(661.7) * 10.0), AngularResolutionStudy.ClosedCellTransmission(c), 15);
        Assert.InRange(AngularResolutionStudy.ClosedCellTransmission(c), 0.1684, 0.1686);
    }

    // ---- the pre-change algorithm, verbatim (MlemDecoder at git 5977c23) ----
    private static (double[] Lambda, double X, double Y, double Confidence) Reference(MaskPattern aperture, CodedApertureGeometry geo, int iterations, DetectorImage image)
    {
        int imgW = image.Width, imgH = image.Height;
        int n = (int)Math.Round(2.0 * geo.ReconHalfExtentMm / geo.ReconStepMm) + 1;
        double origin = -geo.ReconHalfExtentMm;
        int nDet = imgW * imgH, nSrc = n * n;
        double detHalfW = imgW * geo.DetectorPitchMm / 2.0, detHalfH = imgH * geo.DetectorPitchMm / 2.0;
        double maskHalfW = geo.MaskCellsX * geo.MaskCellPitchMm / 2.0, maskHalfH = geo.MaskCellsY * geo.MaskCellPitchMm / 2.0;
        double frac = (geo.MaskPlaneZ - geo.DetectorPlaneZ) / (geo.SourcePlaneZ - geo.DetectorPlaneZ);
        var a = new float[nSrc * nDet];
        var sens = new double[nSrc];
        for (int gy = 0; gy < n; gy++)
            for (int gx = 0; gx < n; gx++)
            {
                int j = gy * n + gx;
                double sxp = origin + gx * geo.ReconStepMm, syp = origin + gy * geo.ReconStepMm;
                int baseIdx = j * nDet;
                double s = 0.0;
                for (int iy = 0; iy < imgH; iy++)
                {
                    double py = (iy + 0.5) * geo.DetectorPitchMm - detHalfH;
                    for (int ix = 0; ix < imgW; ix++)
                    {
                        double px = (ix + 0.5) * geo.DetectorPitchMm - detHalfW;
                        double mx = px + (sxp - px) * frac, my = py + (syp - py) * frac;
                        int cx = (int)Math.Floor((mx + maskHalfW) / geo.MaskCellPitchMm);
                        int cy = (int)Math.Floor((my + maskHalfH) / geo.MaskCellPitchMm);
                        bool open = geo.Cyclic
                            ? aperture[((cx % aperture.Width) + aperture.Width) % aperture.Width, ((cy % aperture.Height) + aperture.Height) % aperture.Height]
                            : cx >= 0 && cy >= 0 && cx < aperture.Width && cy < aperture.Height && aperture[cx, cy];
                        if (open) { a[baseIdx + iy * imgW + ix] = 1f; s += 1.0; }
                    }
                }
                sens[j] = s;
            }
        var y = new double[nDet];
        for (int iy = 0; iy < imgH; iy++)
            for (int ix = 0; ix < imgW; ix++) y[iy * imgW + ix] = image[ix, iy];
        var lam = new double[nSrc];
        Array.Fill(lam, 1.0);
        var fbar = new double[nDet];
        var corr = new double[nSrc];
        for (int it = 0; it < iterations; it++)
        {
            Array.Clear(fbar, 0, nDet);
            for (int j = 0; j < nSrc; j++)
            {
                double lj = lam[j];
                if (lj == 0.0) continue;
                int baseIdx = j * nDet;
                for (int i = 0; i < nDet; i++) fbar[i] += a[baseIdx + i] * lj;
            }
            Array.Clear(corr, 0, nSrc);
            for (int i = 0; i < nDet; i++) fbar[i] = fbar[i] > 1e-12 ? y[i] / fbar[i] : 0.0;
            for (int j = 0; j < nSrc; j++)
            {
                int baseIdx = j * nDet;
                double c = 0.0;
                for (int i = 0; i < nDet; i++) c += a[baseIdx + i] * fbar[i];
                corr[j] = c;
            }
            for (int j = 0; j < nSrc; j++) lam[j] = sens[j] > 0.0 ? lam[j] * corr[j] / sens[j] : 0.0;
        }
        int bx = 0, by = 0; double peak = double.NegativeInfinity;
        for (int gy = 0; gy < n; gy++)
            for (int gx = 0; gx < n; gx++)
                if (lam[gy * n + gx] > peak) { peak = lam[gy * n + gx]; bx = gx; by = gy; }
        double second = double.NegativeInfinity;
        for (int gy = 0; gy < n; gy++)
            for (int gx = 0; gx < n; gx++)
                if (Math.Abs(gx - bx) > 3 || Math.Abs(gy - by) > 3)
                    if (lam[gy * n + gx] > second) second = lam[gy * n + gx];
        return (lam, origin + bx * geo.ReconStepMm, origin + by * geo.ReconStepMm, peak / Math.Max(second, 1e-9));
    }
}
