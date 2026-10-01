namespace Gcam.Studio.Core.Services;

/// <summary>A read-only found peak, distinct from a draggable true source.</summary>
public sealed record ImagingPeak(string Isotope, double Xmm, double Ymm, double Value)
{
    public string Description => $"Found {Isotope} ({Xmm:F1}, {Ymm:F1}) mm";
}
