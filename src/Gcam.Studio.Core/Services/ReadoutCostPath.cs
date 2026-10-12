namespace Gcam.Studio.Core.Services;

/// <summary>One readout path of a benchmark cost row: absolute source RMS and per-position bias magnitude (degrees),
/// remote-peak probability (fraction) and accepted counts, or unavailable with the number of missing seed clusters.</summary>
public sealed record ReadoutCostPath(bool Available, ReadoutInterval? RmsDeg, ReadoutInterval? BiasDeg,
    ReadoutInterval? RemoteProbability, ReadoutInterval? AcceptedCounts, int MissingClusters);
