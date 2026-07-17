using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Decoding;

/// <summary>
/// Geometry the decoder needs to back-project detector pixels through the mask
/// for each candidate source position. Frame: detector at <see cref="DetectorPlaneZ"/>,
/// mask at <see cref="MaskPlaneZ"/>, candidate sources on the plane <see cref="SourcePlaneZ"/>.
/// </summary>
public sealed record CodedApertureGeometry(
    int Rank,
    double MaskPlaneZ,
    double MaskCellPitchMm,
    int MaskCellsX,
    int MaskCellsY,
    double DetectorPlaneZ,
    double DetectorPitchMm,
    double SourcePlaneZ,
    double ReconHalfExtentMm,
    double ReconStepMm,
    bool Cyclic = true);

/// <summary>
/// Reconstructs the source distribution by cross-correlating the detector image
/// with the MURA decoding array G, evaluated over a grid of candidate source
/// positions (each candidate back-projects every detector pixel through the mask).
///
/// With <c>Cyclic = true</c> the mask is treated as periodic (mod rank) — this is
/// the classical decoder and it aliases off-axis sources into a "ghost" on the
/// opposite side (the partially-coded-FOV artifact). <c>Cyclic = false</c> only
/// counts pixels whose ray actually crosses the physical mask, suppressing it.
/// </summary>
public sealed class CrossCorrelationDecoder : IDecoder
{
    private readonly int[,] _g;               // p×p signed decoding array (+1/-1)
    private readonly CodedApertureGeometry _geo;
    private readonly SubCellMethod _subCell;   // sub-grid peak refinement (None = raw argmax)

    public CrossCorrelationDecoder(int[,] decodingArray, CodedApertureGeometry geometry,
                                   SubCellMethod subCell = SubCellMethod.None)
    {
        _g = decodingArray;
        _geo = geometry;
        _subCell = subCell;
    }

    public DecodeResult Decode(DetectorImage image)
    {
        int n = (int)Math.Round(2.0 * _geo.ReconHalfExtentMm / _geo.ReconStepMm) + 1;
        double origin = -_geo.ReconHalfExtentMm;

        double detHalfW = image.Width * _geo.DetectorPitchMm / 2.0;
        double detHalfH = image.Height * _geo.DetectorPitchMm / 2.0;
        double maskHalfW = _geo.MaskCellsX * _geo.MaskCellPitchMm / 2.0;
        double maskHalfH = _geo.MaskCellsY * _geo.MaskCellPitchMm / 2.0;

        double sz = _geo.SourcePlaneZ;
        double dz = _geo.DetectorPlaneZ;
        double mz = _geo.MaskPlaneZ;
        // Along a ray from source S (z=sz) to a detector pixel P (z=dz), the point on
        // the mask plane is P + (S - P) * (mz - dz) / (sz - dz).
        double frac = (mz - dz) / (sz - dz);

        var recon = new DetectorImage(n, n);
        for (int gy = 0; gy < n; gy++)
        {
            double syp = origin + gy * _geo.ReconStepMm;
            for (int gx = 0; gx < n; gx++)
            {
                double sxp = origin + gx * _geo.ReconStepMm;
                recon[gx, gy] = Correlate(image, sxp, syp, frac, detHalfW, detHalfH, maskHalfW, maskHalfH);
            }
        }

        var (peakX, peakY, peakVal, secondVal) = FindPeak(recon);
        // Refine the peak below one recon cell: the argmax quantizes the estimate to ReconStepMm regardless of
        // counts; fitting the correlation-peak shape recovers a fractional offset (see PeakInterpolation).
        var (dx, dy) = PeakInterpolation.Estimate(recon, peakX, peakY, _subCell);
        double estX = origin + (peakX + dx) * _geo.ReconStepMm;
        double estY = origin + (peakY + dy) * _geo.ReconStepMm;
        double confidence = peakVal / Math.Max(secondVal, 1e-9); // primary/secondary ratio

        var estimate = new SourceEstimate(new Vector3(estX, estY, sz), confidence);
        return new DecodeResult(estimate, recon, origin, _geo.ReconStepMm);
    }

    /// <summary>The decoded correlation at a single candidate source position (sxp, syp) on this
    /// geometry's source plane — used by depth-from-focus to score one assumed distance without a
    /// full grid (no candidate-position aliasing).</summary>
    public double PointResponse(DetectorImage image, double sxp, double syp)
    {
        double detHalfW = image.Width * _geo.DetectorPitchMm / 2.0;
        double detHalfH = image.Height * _geo.DetectorPitchMm / 2.0;
        double maskHalfW = _geo.MaskCellsX * _geo.MaskCellPitchMm / 2.0;
        double maskHalfH = _geo.MaskCellsY * _geo.MaskCellPitchMm / 2.0;
        double frac = (_geo.MaskPlaneZ - _geo.DetectorPlaneZ) / (_geo.SourcePlaneZ - _geo.DetectorPlaneZ);
        return Correlate(image, sxp, syp, frac, detHalfW, detHalfH, maskHalfW, maskHalfH);
    }

    private double Correlate(
        DetectorImage image, double sxp, double syp, double frac,
        double detHalfW, double detHalfH, double maskHalfW, double maskHalfH)
    {
        double sum = 0.0;
        for (int iy = 0; iy < image.Height; iy++)
        {
            double py = (iy + 0.5) * _geo.DetectorPitchMm - detHalfH;
            for (int ix = 0; ix < image.Width; ix++)
            {
                double d = image[ix, iy];
                if (d == 0.0) continue;

                double px = (ix + 0.5) * _geo.DetectorPitchMm - detHalfW;

                // Back-project the pixel through the mask plane for this candidate source.
                double mx = px + (sxp - px) * frac;
                double my = py + (syp - py) * frac;

                int cx = (int)Math.Floor((mx + maskHalfW) / _geo.MaskCellPitchMm);
                int cy = (int)Math.Floor((my + maskHalfH) / _geo.MaskCellPitchMm);

                if (!_geo.Cyclic &&
                    (cx < 0 || cy < 0 || cx >= _geo.MaskCellsX || cy >= _geo.MaskCellsY))
                    continue; // ray misses the physical mask -> no contribution

                int gxc = Mod(cx, _geo.Rank);
                int gyc = Mod(cy, _geo.Rank);
                sum += d * _g[gxc, gyc];
            }
        }
        return sum;
    }

    private static (int x, int y, double peak, double second) FindPeak(DetectorImage recon)
    {
        int bx = 0, by = 0;
        double peak = double.NegativeInfinity;
        for (int y = 0; y < recon.Height; y++)
            for (int x = 0; x < recon.Width; x++)
                if (recon[x, y] > peak) { peak = recon[x, y]; bx = x; by = y; }

        // Secondary peak: the largest value outside a small exclusion window (~3 cells).
        const int excl = 3;
        double second = double.NegativeInfinity;
        for (int y = 0; y < recon.Height; y++)
            for (int x = 0; x < recon.Width; x++)
                if (Math.Abs(x - bx) > excl || Math.Abs(y - by) > excl)
                    if (recon[x, y] > second) second = recon[x, y];

        return (bx, by, peak, second);
    }

    private static int Mod(int a, int m) => ((a % m) + m) % m;
}
