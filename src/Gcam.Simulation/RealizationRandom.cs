using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>
/// The random stream for a study's Poisson realisations of a mean map. It has its own seed offset so the realisation
/// noise never re-uses the uniforms of the transport stream that built the mean map (before TODO-26 the studies
/// called <c>CreateRandom(cfg)</c> again and restarted that very stream). An unseeded config stays unseeded.
/// </summary>
public static class RealizationRandom
{
    /// <summary>Seed offset of the realisation stream (distinct from the other stream offsets: 555 … 8181).</summary>
    public const int SeedOffset = 90_001;

    public static IRandom For(SimulationConfig config)
        => config.Seed is int seed ? new DefaultRandom(unchecked(seed + SeedOffset)) : new DefaultRandom();
}
