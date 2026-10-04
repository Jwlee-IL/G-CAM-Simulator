namespace Gcam.Simulation;

/// <summary>Raw sums of one soil/air transport run, per Bq/kg of the chain. Fluence tallies are photons/(cm² s) summed
/// over histories (divide by <see cref="Histories"/>); "kerma" tallies are Σ Φ·E·(μ_en/ρ)_air in keV/(g s) summed the
/// same way (× 0.5767835882 → nGy/h). Uncollided cells are indexed line × zenith bin; each history crosses the slab
/// at most once uncollided, so their squares are per-history second moments.</summary>
public sealed class SoilAirTally
{
    public required string Chain { get; init; }
    public required long Histories { get; init; }
    public required int ZenithBins { get; init; }
    public required double[] LineEnergiesKeV { get; init; }
    public required double[] UncollidedSum { get; init; }
    public required double[] UncollidedSumSq { get; init; }
    /// <summary>Number of scored uncollided slab crossings per cell.</summary>
    public required long[] UncollidedCount { get; init; }
    /// <summary>Uncollided 511-keV annihilation photons per zenith bin.</summary>
    public required double[] AnnihilationSum { get; init; }
    /// <summary>Scattered photons per continuum energy bin × zenith bin.</summary>
    public required double[] ContinuumSum { get; init; }
    public double ScatteredFluenceBelowFirstEdge { get; init; }
    public double ScatteredFluenceAboveLastEdge { get; init; }
    public double FluenceUncollided { get; init; }
    public double FluenceAnnihilation { get; init; }
    public double FluenceScattered { get; init; }
    public double KermaUncollided { get; init; }
    public double KermaAnnihilation { get; init; }
    public double KermaScattered { get; init; }
    public double KermaSumSq { get; init; }
    public double KermaAbove1332KeV { get; init; }
    public double KermaBelow10KeV { get; init; }
    public double KermaBelowFirstEdge { get; init; }
    /// <summary>Upper bound on the air kerma of K-fluorescence photons that the transport does not emit.</summary>
    public double FluorescenceKermaBound { get; init; }
    public double FluorescenceKermaBoundSumSq { get; init; }
    /// <summary>Part of the bound from photoabsorptions in the soil (the rest is from the air).</summary>
    public double FluorescenceKermaBoundFromSoil { get; init; }
    public long Interactions { get; init; }
    public long PairEvents { get; init; }
    public long CutoffTerminations { get; init; }
    public double KermaTotal => KermaUncollided + KermaAnnihilation + KermaScattered;
}
