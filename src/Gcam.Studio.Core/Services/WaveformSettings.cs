namespace Gcam.Studio.Core.Services;

/// <summary>Local scope settings. Rate study never modifies acquired timestamps.</summary>
public sealed record WaveformSettings(int TriggerIndex = 0, double WindowUs = 10,
    bool RateStudy = false, double RateKcps = 50, bool Ideal = false, int Seed = 555);
