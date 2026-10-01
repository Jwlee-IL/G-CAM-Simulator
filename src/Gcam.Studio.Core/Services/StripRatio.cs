namespace Gcam.Studio.Core.Services;

/// <summary>H-only accepted-event calibration counts and their low / high window ratio.</summary>
public sealed record StripRatio(string LowIsotope, string HighIsotope, long Events,
    long LowCounts, long HighCounts)
{
    public double R => (double)LowCounts / HighCounts;
    public string Description => $"{HighIsotope} → {LowIsotope}: R = {R:F4} ({LowCounts:N0}/{HighCounts:N0})";
}
