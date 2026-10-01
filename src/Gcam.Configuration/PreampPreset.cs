namespace Gcam.Configuration;

/// <summary>A preamp + shaper: the effective integration time (noise window; also sets DCR-collected variance)
/// and the pulse decay tail the charge-sensitive preamp produces, plus which digital shaper the DAQ runs.</summary>
public sealed record PreampPreset(string Name, double IntegrationNs, double PulseTailNs, bool Crrc);
