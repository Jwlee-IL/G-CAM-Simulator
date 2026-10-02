namespace Gcam.Studio.Core.Services;

public sealed record FocusTrack(IReadOnlyList<FocusSample> Curve, FocusSample Sharpest,
    FocusInterval Interval, bool MultipleModes, bool BoundaryMaximum)
{
    public string Description => $"Sharpest plane {Sharpest.PlaneMm:0} mm · ({Sharpest.Xmm:0.0}, {Sharpest.Ymm:0.0}) mm\nHalf-max: {Interval.Description}"
        + (BoundaryMaximum ? " · boundary maximum" : "") + (MultipleModes ? " · multiple modes" : "");
}
