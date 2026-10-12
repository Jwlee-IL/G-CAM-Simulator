namespace Gcam.Studio.Core.Services;

/// <summary>The declared scope of the shipped cost table: one mono-line point source at nine positions (0 and ±
/// <see cref="PositionHalfFraction"/> of the fully coded half field) at <see cref="FocalDistanceMm"/>, the decoder,
/// the per-path flood correction, ±<see cref="WindowFraction"/> windows, equal emitted fluence, the trials per position
/// and the seed populations.</summary>
public sealed record ReadoutBenchmark(double FocalDistanceMm, double PositionHalfFraction, int Positions,
    string Decoder, string FloodCorrection, double WindowFraction, int TrialsPerPosition, string Exposure,
    int CostSeeds, int CalibrationSeeds);
