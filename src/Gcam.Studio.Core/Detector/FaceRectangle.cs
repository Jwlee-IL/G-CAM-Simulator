namespace Gcam.Studio.Core.Detector;

/// <summary>Detector-face rectangle in mm, measured from the lower left edge.</summary>
public sealed record FaceRectangle(double X, double Y, double Width, double Height, double Gain = 0);
