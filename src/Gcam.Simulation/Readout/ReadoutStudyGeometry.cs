using Gcam.Configuration;

namespace Gcam.Simulation;

/// <summary>One crystal-array geometry of the readout study: pitch and shared reflector gap (active width = pitch − gap),
/// optionally its own optics, which then REPLACE the request's base optics entirely (unset fields take the class
/// defaults, not the base's values).</summary>
public sealed class ReadoutStudyGeometry
{
    public string Name { get; set; } = "";
    public double PitchMm { get; set; }
    public double GapMm { get; set; }
    public ReadoutOpticsConfig? Optics { get; set; }
}
