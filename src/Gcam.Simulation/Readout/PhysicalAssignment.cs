namespace Gcam.Simulation;

/// <summary>One held conversion projected through one line calibration: LUT crystal (−1 = unknown) and calibrated energy.</summary>
public readonly record struct PhysicalAssignment(int Crystal, double EnergyKeV);
