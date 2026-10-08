using Gcam.Configuration;

namespace Gcam.Simulation;

/// <summary>One trigger setting of the readout study (logic, threshold, unit).</summary>
public sealed class ReadoutStudyTrigger
{
    public string Name { get; set; } = "";
    public ReadoutTriggerConfig Trigger { get; set; } = new();
}
