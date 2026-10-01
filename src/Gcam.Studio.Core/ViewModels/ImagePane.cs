namespace Gcam.Studio.Core.ViewModels;

/// <summary>Which image a measurement was drawn on — each has its own mm frame.</summary>
public enum ImagePane
{
    /// <summary>Detector plane (flood map), mm from the optical axis.</summary>
    Flood,

    /// <summary>Source plane (reconstruction), mm from the optical axis at the focal distance.</summary>
    Reconstruction,
}
