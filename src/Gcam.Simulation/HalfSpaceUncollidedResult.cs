namespace Gcam.Simulation;

/// <summary>Uncollided scalar fluence per Bq/kg and its MC error. Bin i spans [i/B,(i+1)/B] in upward zenith cosine.</summary>
public sealed record HalfSpaceUncollidedResult(int Histories, long[] ZenithCounts, double FluenceNormalizationPerBqKg,
    double FluencePerBqKg, double StandardErrorPerBqKg);
