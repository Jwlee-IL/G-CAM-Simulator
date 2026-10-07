using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>First-order net uncertainty for a pair of potentially overlapping Poisson windows and a separate
/// fixed-total H-only calibration. Multiple contaminants require a calibration covariance model and are unsupported.</summary>
public static class StripCountEstimator
{
    public static StripCountEstimate ForPair(double lowCounts, double highCounts, double overlapCounts, StripRatio ratio)
    {
        ArgumentNullException.ThrowIfNull(ratio);
        if (ratio.HighCounts <= 0 || ratio.LowCounts < 0)
            throw new ArgumentOutOfRangeException(nameof(ratio), "A ratio needs a nonempty calibration high window.");
        if (!double.IsFinite(lowCounts) || !double.IsFinite(highCounts) || !double.IsFinite(overlapCounts) ||
            lowCounts < 0 || highCounts < 0 || overlapCounts < 0 || overlapCounts > Math.Min(lowCounts, highCounts))
            throw new ArgumentOutOfRangeException(nameof(overlapCounts));
        double r = ratio.R;
        double net = lowCounts - r * highCounts;
        bool hasData = lowCounts > 0 || highCounts > 0;
        // An empty calibration low bin cannot establish zero uncertainty in its unknown probability.
        if (ratio.LowCounts == 0 || ratio.OverlapCounts is not { } overlap ||
            overlap < 0 || overlap > Math.Min(ratio.LowCounts, ratio.HighCounts))
            return new(net, null, hasData);
        // Disjoint categories make both covariance expressions nonnegative without clipping numerical residuals:
        // Var(L-RH) = L + R²H - 2RC. For fixed calibration T, multinomial -1/T terms cancel in the ratio gradient.
        double varianceR = (ratio.LowCounts - overlap + r * r * (ratio.HighCounts - overlap) +
            (1 - r) * (1 - r) * overlap) / ((double)ratio.HighCounts * ratio.HighCounts);
        double variance = lowCounts - overlapCounts + r * r * (highCounts - overlapCounts) +
            (1 - r) * (1 - r) * overlapCounts + highCounts * highCounts * varianceR;
        return new(net, double.IsFinite(variance) ? variance : null, hasData);
    }
}
