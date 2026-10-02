namespace Gcam.Studio.Core.ViewModels;

/// <summary>What a pointer gesture on an image does.</summary>
public enum MeasureTool
{
    /// <summary>Drag pans the image (source markers are display-only: positions are physical inputs, edited in the left panel).</summary>
    Pan,
    Distance,
    Angle,
    Roi,
}
