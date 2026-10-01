namespace Gcam.Studio.Core.Plotting;

/// <summary>X navigation and invertible linear / log10 Y mapping. Non-positive Y clamps to LogFloor.</summary>
public sealed class PlotViewport
{
    public const double LogFloor = 1;
    private double _fullMin, _fullMax = 1;
    public double XMin { get; private set; }
    public double XMax { get; private set; } = 1;
    public double YMin { get; private set; }
    public double YMax { get; private set; } = 1;
    public bool LogY { get; private set; }
    public double Zoom => (_fullMax - _fullMin) / (XMax - XMin);

    public void Configure(double xMin, double xMax, double yMin, double yMax, bool logY)
    {
        if (!double.IsFinite(xMin) || !double.IsFinite(xMax) || !double.IsFinite(yMin) || !double.IsFinite(yMax))
            throw new ArgumentException("Bounds must be finite.");
        _fullMin = xMin; _fullMax = xMax > xMin ? xMax : xMin + 1;
        LogY = logY;
        YMin = logY ? Math.Max(LogFloor, yMin) : yMin;
        YMax = yMax > YMin ? yMax : (logY ? YMin * 10 : YMin + 1);
        Reset();
    }

    public double XToPixel(double x, double width) => (x - XMin) / (XMax - XMin) * width;
    public double PixelToX(double pixel, double width) => XMin + pixel / width * (XMax - XMin);
    private double Transform(double y) => LogY ? Math.Log10(Math.Max(LogFloor, y)) : y;
    public double YToPixel(double y, double height) => height * (1 - (Transform(y) - Transform(YMin)) / (Transform(YMax) - Transform(YMin)));
    public double PixelToY(double pixel, double height)
    {
        double y = Transform(YMin) + (1 - pixel / height) * (Transform(YMax) - Transform(YMin));
        return LogY ? Math.Pow(10, y) : y;
    }

    public void ZoomAt(double x, double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor));
        double span = Math.Clamp((XMax - XMin) / factor, (_fullMax - _fullMin) / 1_000_000, _fullMax - _fullMin);
        double ratio = Math.Clamp((x - XMin) / (XMax - XMin), 0, 1);
        XMin = Math.Clamp(x - span * ratio, _fullMin, _fullMax - span);
        XMax = XMin + span;
    }

    public void Pan(double delta)
    {
        double span = XMax - XMin;
        XMin = Math.Clamp(XMin + delta, _fullMin, _fullMax - span);
        XMax = XMin + span;
    }
    public void Reset() { XMin = _fullMin; XMax = _fullMax; }
}
