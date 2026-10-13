namespace Gcam.Simulation;

/// <summary>Opt-in wall timings of one bounded advance, for headless pilots; never a simulation input.</summary>
public sealed record ReadoutAdvanceTiming(TimeSpan Transport, TimeSpan Response, TimeSpan Pulse, TimeSpan Projection);
