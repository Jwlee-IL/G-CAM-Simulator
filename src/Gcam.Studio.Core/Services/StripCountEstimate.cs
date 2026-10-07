namespace Gcam.Studio.Core.Services;

/// <summary>Signed scalar-strip estimate, distinct from the displayed clipped flood. Variance is the first-order
/// counting plus independent-calibration uncertainty, or null when that model is unsupported. It is not a detection
/// statistic and does not include ambient or scalar-model error; calibration uncertainty is shared across frames.</summary>
public sealed record StripCountEstimate(double NetCounts, double? Variance, bool HasRawCounts)
{
    public double? Sigma => Variance is { } variance ? Math.Sqrt(variance) : null;
}
