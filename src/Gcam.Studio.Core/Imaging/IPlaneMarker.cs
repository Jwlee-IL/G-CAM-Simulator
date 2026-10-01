namespace Gcam.Studio.Core.Imaging;

/// <summary>
/// A point an image overlay can show and let the user drag, in the image's mm frame. Implemented by ViewModels
/// (a scene source) so the overlay control can move them without knowing what they are.
/// </summary>
public interface IPlaneMarker
{
    string MarkerLabel { get; }
    double X { get; set; }
    double Y { get; set; }
}
