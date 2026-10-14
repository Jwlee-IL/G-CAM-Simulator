namespace Gcam.Studio.Rendering;

/// <summary>Perceptually uniform colormaps as 256-entry BGRA lookup tables.</summary>
public static class Colormap
{
    /// <summary>Viridis as packed BGRA32 (alpha 255), index 0 = minimum.</summary>
    public static IReadOnlyList<uint> Viridis => Core.Rendering.Colormap.Viridis;
}
