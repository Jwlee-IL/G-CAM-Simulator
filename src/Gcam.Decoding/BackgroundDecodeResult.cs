using Gcam.Core;

namespace Gcam.Decoding;

/// <summary>The fitted amplitude is an image component, not ambient dose. Calibration counts are a cross-check;
/// the reconstruction confidence is not the independent, unchanged ambient gate statistic.</summary>
public sealed record BackgroundDecodeResult(DecodeResult Reconstruction, double BackgroundCounts,
    double CalibrationExpectedCounts, double CalibrationStandardUncertaintyCounts);
