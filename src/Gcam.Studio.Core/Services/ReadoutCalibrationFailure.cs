namespace Gcam.Studio.Core.Services;

/// <summary>One literal published calibration failure (seed and the LUT's own message, verbatim).</summary>
public sealed record ReadoutCalibrationFailure(int Seed, string Failure);
