namespace Gcam.Studio.Core.Services;

/// <summary>H-only accepted-event calibration counts and their low / high window ratio.</summary>
public sealed record StripRatio(string LowIsotope, string HighIsotope, long Events,
    long LowCounts, long HighCounts)
{
    /// <summary>Accepted calibration events in both windows. Null means overlap covariance was not retained.</summary>
    public long? OverlapCounts { get; init; }
    public double R => (double)LowCounts / HighCounts;
    public string Description => $"{HighIsotope} → {LowIsotope}: R = {Gcam.Studio.Core.Imaging.NumberFormat.Significant(R)} ({LowCounts:N0}/{HighCounts:N0})";
}
