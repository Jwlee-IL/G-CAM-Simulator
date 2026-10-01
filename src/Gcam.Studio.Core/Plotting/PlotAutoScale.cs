namespace Gcam.Studio.Core.Plotting;

/// <summary>Readable headroom; a live update can grow the top but cannot shrink it.</summary>
public static class PlotAutoScale
{
    private const double LinearHeadroom = 1.08;
    public static (double Min, double Max) Calculate(double min, double max, bool floor, bool log, double? previousTop = null)
    {
        double bottom = log ? PlotViewport.LogFloor : floor ? Math.Min(0, min) : min;
        double top = log ? Math.Pow(10, (Math.Floor(Math.Log10(Math.Max(PlotViewport.LogFloor, max)) * 2) + 1) / 2)
            : bottom + Math.Max(1, max - bottom) * LinearHeadroom;
        return (bottom, Math.Max(top, previousTop ?? top));
    }
}
