namespace Gcam.Studio.Core.Services;

/// <summary>One benchmark condition of a (preset, line) cell: expected ideal-Direct counts and scored input rate
/// (0 = processed isolated). <see cref="F"/> is the paired equal-fluence source-MSE ratio Anger / Direct, null when a
/// path is unavailable. Direct is the ideal largest-deposit / true-energy reference, not Studio's legacy chain.</summary>
public sealed record ReadoutCostRow(int TargetDirectCounts, double RateCps, ReadoutCostPath Direct,
    ReadoutCostPath Anger, ReadoutInterval? F);
