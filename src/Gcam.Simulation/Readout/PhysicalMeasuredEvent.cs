using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>LUT assignment and calibrated held energy; unknown assignments never borrow a transport pixel.
/// <see cref="Crystal"/> / <see cref="EnergyKeV"/> are the reference calibration's; <see cref="Assignments"/> holds every
/// calibration's projection of the same held conversion, in calibration order.</summary>
public sealed record PhysicalMeasuredEvent(ObservedReadoutEvent Conversion, double RawX, double RawY,
    int Crystal, double EnergyKeV)
{
    public IReadOnlyList<PhysicalAssignment> Assignments { get; init; } = [];
}
