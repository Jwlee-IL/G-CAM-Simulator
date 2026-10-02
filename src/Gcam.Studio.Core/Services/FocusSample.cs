namespace Gcam.Studio.Core.Services;

/// <summary>Peak prominence at a detector-referenced plane; angular coordinates link tracks.</summary>
public sealed record FocusSample(double PlaneMm, double Xmm, double Ymm, double Prominence)
{
    public double AngleX => Xmm / PlaneMm;
    public double AngleY => Ymm / PlaneMm;
}
