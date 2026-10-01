using Gcam.Core;

namespace Gcam.Studio.Core.Imaging;

/// <summary>Pixel statistics inside a region of interest.</summary>
/// <param name="Pixels">Number of pixels whose centre lies inside the region.</param>
public readonly record struct RoiStats(int Pixels, double Sum, double Mean, double Max);

/// <summary>Measurements on a mm grid — the maths behind the distance, angle and ROI tools.</summary>
public static class MeasurementMath
{
    public static double Distance(Vec2 a, Vec2 b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    /// <summary>Angle at <paramref name="vertex"/> between the rays to <paramref name="a"/> and <paramref name="b"/>, in degrees (0–180).</summary>
    public static double AngleDeg(Vec2 a, Vec2 vertex, Vec2 b)
    {
        var u = a - vertex;
        var v = b - vertex;
        double nu = Math.Sqrt(u.X * u.X + u.Y * u.Y), nv = Math.Sqrt(v.X * v.X + v.Y * v.Y);
        if (nu == 0 || nv == 0) return 0;
        double cos = Math.Clamp((u.X * v.X + u.Y * v.Y) / (nu * nv), -1, 1);
        return Math.Acos(cos) * 180 / Math.PI;
    }

    /// <summary>
    /// Statistics over the pixels whose centres fall inside the axis-aligned rectangle spanned by two corners
    /// (in mm, any order). Pixel i is centred at <c>originMm + i·stepMm</c>, as in <see cref="HeatmapViewport"/>.
    /// </summary>
    /// <remarks>
    /// Whole pixels by centre rather than fractional area: a flood-map pixel is one crystal, and a crystal is
    /// either counted or not — a partial count would be a number no detector ever read out.
    /// </remarks>
    public static RoiStats Roi(DetectorImage image, Vec2 cornerA, Vec2 cornerB, double originMm, double stepMm)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (stepMm <= 0) return default;
        double x0 = Math.Min(cornerA.X, cornerB.X), x1 = Math.Max(cornerA.X, cornerB.X);
        double y0 = Math.Min(cornerA.Y, cornerB.Y), y1 = Math.Max(cornerA.Y, cornerB.Y);
        int ix0 = Math.Max(0, (int)Math.Ceiling((x0 - originMm) / stepMm));
        int ix1 = Math.Min(image.Width - 1, (int)Math.Floor((x1 - originMm) / stepMm));
        int iy0 = Math.Max(0, (int)Math.Ceiling((y0 - originMm) / stepMm));
        int iy1 = Math.Min(image.Height - 1, (int)Math.Floor((y1 - originMm) / stepMm));

        int n = 0;
        double sum = 0, max = double.MinValue;
        for (int y = iy0; y <= iy1; y++)
            for (int x = ix0; x <= ix1; x++)
            {
                double v = image[x, y];
                sum += v;
                if (v > max) max = v;
                n++;
            }
        return n == 0 ? default : new RoiStats(n, sum, sum / n, max);
    }
}
