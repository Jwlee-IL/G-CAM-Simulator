namespace Gcam.Studio.Core.Services;

/// <summary>One published estimate with its two-sided 95 % outer-seed cluster interval, copied verbatim from the
/// shipped record (display rounding is formatting only).</summary>
public sealed record ReadoutInterval(double Estimate, double Lower95, double Upper95, int Clusters);
