namespace Gcam.Studio.Core.Services;

/// <summary>One measured conversion projected through one line's calibration: its LUT crystal (−1 = unknown) and calibrated
/// energy. Derived from the held ADC facts, never from the generating line or photon truth.</summary>
public readonly record struct ReadoutLineAssignment(int Crystal, double EnergyKeV);
