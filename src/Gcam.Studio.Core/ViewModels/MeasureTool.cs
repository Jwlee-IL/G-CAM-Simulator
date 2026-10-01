namespace Gcam.Studio.Core.ViewModels;

/// <summary>What a pointer gesture on an image does.</summary>
public enum MeasureTool
{
    /// <summary>Drag pans the image; drag on a source marker moves the source.</summary>
    Pan,
    Distance,
    Angle,
    Roi,
}
