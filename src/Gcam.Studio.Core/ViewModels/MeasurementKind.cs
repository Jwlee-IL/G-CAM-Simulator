namespace Gcam.Studio.Core.ViewModels;

public enum MeasurementKind
{
    /// <summary>Two points; length in mm.</summary>
    Distance,

    /// <summary>Three points, the second is the vertex; angle in degrees.</summary>
    Angle,

    /// <summary>Two opposite corners; sum / mean / max of the pixels inside.</summary>
    Roi,
}
