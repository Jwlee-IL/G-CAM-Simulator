using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>LUT assignment and calibrated held energy; unknown assignments never borrow a transport pixel.</summary>
public sealed record PhysicalMeasuredEvent(ObservedReadoutEvent Conversion, double RawX, double RawY,
    int Crystal, double EnergyKeV);
