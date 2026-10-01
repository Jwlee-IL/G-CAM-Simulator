namespace Gcam.Studio.Rendering;

/// <summary>Perceptually uniform colormaps as 256-entry BGRA lookup tables.</summary>
public static class Colormap
{
    // Viridis control points (matplotlib), linearly interpolated: perceptually uniform and colour-blind safe.
    private static readonly (double R, double G, double B)[] ViridisStops =
    [
        (68, 1, 84), (59, 82, 139), (33, 145, 140), (94, 201, 98), (253, 231, 37),
    ];

    /// <summary>Viridis as packed BGRA32 (alpha 255), index 0 = minimum.</summary>
    public static IReadOnlyList<uint> Viridis { get; } = Build(ViridisStops);

    private static uint[] Build((double R, double G, double B)[] stops)
    {
        var lut = new uint[256];
        for (int i = 0; i < lut.Length; i++)
        {
            double t = i / 255.0 * (stops.Length - 1);
            int k = Math.Min((int)t, stops.Length - 2);
            double f = t - k;
            var (a, b) = (stops[k], stops[k + 1]);
            uint r = (uint)Math.Round(a.R + (b.R - a.R) * f);
            uint g = (uint)Math.Round(a.G + (b.G - a.G) * f);
            uint bl = (uint)Math.Round(a.B + (b.B - a.B) * f);
            lut[i] = 0xFF000000u | (r << 16) | (g << 8) | bl;
        }
        return lut;
    }
}
