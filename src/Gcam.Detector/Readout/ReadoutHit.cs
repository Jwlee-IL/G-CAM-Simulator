namespace Gcam.Detector;

/// <summary>One detected history as the readout outputs see it before the trigger: its arrival time and the analogue
/// charge of every channel (ADC-code units, before electronic noise), i.e. the amplitude its pulse would have alone.</summary>
/// <param name="TimeNs">Arrival time (ns).</param>
/// <param name="Channels">Per-channel analogue amplitude (codes).</param>
/// <param name="Sum">Σ of <paramref name="Channels"/>.</param>
public sealed record ReadoutHit(double TimeNs, double[] Channels, double Sum);
