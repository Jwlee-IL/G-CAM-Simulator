namespace Gcam.Studio.Core.Services;

/// <summary>Contiguous raw half-maximum interval around the sharpest plane, not statistical uncertainty.</summary>
public sealed record FocusInterval(double LoMm, double HiMm, bool NearCensored, bool FarCensored)
{
    public double? WidthMm => NearCensored || FarCensored ? null : HiMm - LoMm;
    public string Description => NearCensored && FarCensored ? "Unresolved across sweep bounds"
        : FarCensored ? $"≥ {LoMm:0} mm (far edge censored)"
        : NearCensored ? $"≤ {HiMm:0} mm (near edge censored)"
        : $"{LoMm:0}–{HiMm:0} mm · width {WidthMm:0} mm";
}
