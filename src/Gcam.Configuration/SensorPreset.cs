namespace Gcam.Configuration;

/// <summary>A photosensor, by datasheet: photon-detection efficiency (→ N_pe), excess-noise factor, and dark
/// count rate (→ low-energy resolution and a dark-count background).</summary>
public sealed record SensorPreset(string Name, double Pde, double Enf, double DcrHz);
