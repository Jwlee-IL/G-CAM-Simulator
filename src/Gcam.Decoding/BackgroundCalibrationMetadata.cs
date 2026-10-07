namespace Gcam.Decoding;

/// <summary>Identity of a background acquisition. Detector/head response and counting-window knowledge must match;
/// the focal source plane and cyclic decoding choice do not change a measured detector background map.</summary>
public sealed record BackgroundCalibrationMetadata(int PixelsX, int PixelsY, double PixelPitchMm,
    string HeadResponseId, string WindowId, string Bound, string FieldDistributionId);
