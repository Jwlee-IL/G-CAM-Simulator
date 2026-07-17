using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Decoding;

/// <summary>
/// Sub-cell refinement of a reconstruction peak. The cross-correlation decoder samples the source plane on a grid
/// of spacing <c>ReconStepMm</c> and reports the integer argmax cell, so the estimate is quantized to one cell — a
/// precision floor of ≈ step/√12 that no amount of counts can beat. These separable 3-point estimators fit the
/// SHAPE of the correlation peak around the argmax to recover a fractional offset δ ∈ [−0.5, +0.5] per axis.
///
/// The coded-aperture point response near the peak is the autocorrelation of the projected mask cell — a TRIANGLE
/// (tent), locally quadratic only right at the apex — sitting on a positive core with NEGATIVE sidelobes from the
/// ±1 decoding array. The <see cref="SubCellMethod.Tent"/> estimator is the matched model (exact for an ideal tent);
/// parabolic is biased toward the integer cell; the log-parabola (Gaussian) needs positive samples so it falls back
/// to parabolic near the sidelobes and is kept only for comparison. All estimators use DIFFERENCES of samples, so
/// (unlike a centroid) they tolerate the negative sidelobes.
/// </summary>
public static class PeakInterpolation
{
    private const double Eps = 1e-12;

    /// <summary>The integer argmax cell of a reconstruction image.</summary>
    public static (int x, int y) Argmax(DetectorImage recon)
    {
        int bx = 0, by = 0;
        double peak = double.NegativeInfinity;
        for (int y = 0; y < recon.Height; y++)
            for (int x = 0; x < recon.Width; x++)
                if (recon[x, y] > peak) { peak = recon[x, y]; bx = x; by = y; }
        return (bx, by);
    }

    /// <summary>Fractional sub-cell offset (δx, δy) ∈ [−0.5, +0.5]² of the true peak relative to the argmax cell
    /// (bx, by), estimated per axis by the chosen method. Returns (0, 0) at grid edges (no neighbour to fit) or on a
    /// flat plateau.</summary>
    public static (double dx, double dy) Estimate(DetectorImage recon, int bx, int by, SubCellMethod method)
    {
        if (method == SubCellMethod.None) return (0.0, 0.0);

        double dx = 0.0, dy = 0.0;
        if (bx > 0 && bx < recon.Width - 1)
            dx = Axis(recon[bx - 1, by], recon[bx, by], recon[bx + 1, by], method);
        if (by > 0 && by < recon.Height - 1)
            dy = Axis(recon[bx, by - 1], recon[bx, by], recon[bx, by + 1], method);
        return (dx, dy);
    }

    /// <summary>One-axis 3-point estimator. f0 is the peak sample; fm, fp are the lower and upper neighbours.</summary>
    private static double Axis(double fm, double f0, double fp, SubCellMethod method)
    {
        switch (method)
        {
            case SubCellMethod.Parabolic:
                return Parabolic(fm, f0, fp);

            case SubCellMethod.Tent:
                // Matched to a tent peak: the arm toward the higher neighbour sets the slope. Exact for an ideal
                // tent — sampling f0−s|u|, fm, fp on the linear arms gives δ = (fp−fm)/(2(f0−fm)) = u.
                if (fp >= fm)
                {
                    double den = f0 - fm;
                    return den > Eps ? Clamp((fp - fm) / (2.0 * den)) : 0.0;
                }
                else
                {
                    double den = f0 - fp;
                    return den > Eps ? Clamp(-(fm - fp) / (2.0 * den)) : 0.0;
                }

            case SubCellMethod.Gaussian:
                // Log-parabola: only defined where all three samples are positive. The ±1 decoding array drives
                // neighbours negative near the peak's sidelobes, so fall back to the plain parabola there.
                if (fm > 0.0 && f0 > 0.0 && fp > 0.0)
                    return Parabolic(Math.Log(fm), Math.Log(f0), Math.Log(fp));
                return Parabolic(fm, f0, fp);

            default:
                return 0.0;
        }
    }

    /// <summary>3-point parabola vertex offset: δ = ½·(fm − fp)/(fm − 2f0 + fp).</summary>
    private static double Parabolic(double fm, double f0, double fp)
    {
        double den = fm - 2.0 * f0 + fp;              // <0 at a real max
        if (den > -Eps) return 0.0;                   // flat / non-concave → no shift
        return Clamp(0.5 * (fm - fp) / den);
    }

    private static double Clamp(double d) => d < -0.5 ? -0.5 : (d > 0.5 ? 0.5 : d);
}
