namespace Gcam.Studio.Core.Plotting;

/// <summary>
/// X navigation and invertible linear / log10 mapping on both axes. Non-positive Y clamps to LogFloor on a log Y
/// axis. A log X axis needs a positive data range; otherwise the X axis stays linear. Zoom, pan and the minimum
/// span all work in the transformed X coordinate, so a log axis zooms and pans by equal screen fractions.
/// </summary>
public sealed class PlotViewport
{
    public const double LogFloor = 1;
    private double _fullMin, _fullMax = 1;
    public double XMin { get; private set; }
    public double XMax { get; private set; } = 1;
    public double YMin { get; private set; }
    public double YMax { get; private set; } = 1;
    public bool LogY { get; private set; }
    public bool LogX { get; private set; }
    public double Zoom => (U(_fullMax) - U(_fullMin)) / (U(XMax) - U(XMin));

    public void Configure(double xMin, double xMax, double yMin, double yMax, bool logY, bool logX = false)
    {
        if (!double.IsFinite(xMin) || !double.IsFinite(xMax) || !double.IsFinite(yMin) || !double.IsFinite(yMax))
            throw new ArgumentException("Bounds must be finite.");
        bool useLogX = logX && xMin > 0;
        double fullMax = xMax > xMin ? xMax : (useLogX ? xMin * 10 : xMin + 1);
        bool changed = _fullMin != xMin || _fullMax != fullMax || LogX != useLogX;
        _fullMin = xMin; _fullMax = fullMax; LogX = useLogX;
        LogY = logY;
        YMin = logY ? Math.Max(LogFloor, yMin) : yMin;
        YMax = yMax > YMin ? yMax : (logY ? YMin * 10 : YMin + 1);
        if (changed) Reset();
    }

    public void SetY(double min, double max) { YMin = min; YMax = max; }

    public void SetRange(PlotViewRange range)
    {
        if (!double.IsFinite(range.Lo) || !double.IsFinite(range.Hi) || range.Hi <= range.Lo || (LogX && range.Lo <= 0))
            throw new ArgumentException("View range must increase and be finite (and positive on a log axis).");
        SetSpan(U(range.Lo), U(range.Hi) - U(range.Lo));
    }

    public double XToPixel(double x, double width) => (U(x) - U(XMin)) / (U(XMax) - U(XMin)) * width;
    public double PixelToX(double pixel, double width) => X(U(XMin) + pixel / width * (U(XMax) - U(XMin)));
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
        double lo = U(XMin), hi = U(XMax), span = (hi - lo) / factor;
        double ratio = Math.Clamp((U(x) - lo) / (hi - lo), 0, 1);
        SetSpan(U(x) - ClampSpan(span) * ratio, span);
    }

    /// <summary>Pans by a data-unit delta on a linear axis; on a log axis use <see cref="PanFraction"/>.</summary>
    public void Pan(double delta)
    {
        if (LogX) { PanFraction(delta / (XMax - XMin)); return; }
        SetSpan(XMin + delta, XMax - XMin);
    }

    /// <summary>Pans by a fraction of the visible width (positive moves the view toward larger X).</summary>
    public void PanFraction(double fraction)
    {
        double lo = U(XMin), span = U(XMax) - lo;
        SetSpan(lo + fraction * span, span);
    }

    public void Reset() { XMin = _fullMin; XMax = _fullMax; }

    private double U(double x) => LogX ? Math.Log10(x) : x;
    private double X(double u) => LogX ? Math.Pow(10, u) : u;
    private double ClampSpan(double span)
    {
        double full = U(_fullMax) - U(_fullMin);
        return Math.Clamp(span, full / 1_000_000, full);
    }
    private void SetSpan(double lo, double span)
    {
        span = ClampSpan(span);
        double fullLo = U(_fullMin), fullHi = U(_fullMax);
        lo = Math.Clamp(lo, fullLo, fullHi - span);
        // Snap the edges to the exact full bounds so Reset/clamp compare equal on a log axis too.
        XMin = lo <= fullLo ? _fullMin : X(lo);
        XMax = lo + span >= fullHi ? _fullMax : X(lo + span);
    }
}
