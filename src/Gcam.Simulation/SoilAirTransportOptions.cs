namespace Gcam.Simulation;

/// <summary>Recipe of one soil/air transport run. Every field is part of the reported recipe.</summary>
public sealed class SoilAirTransportOptions
{
    /// <summary>Detector-point height above the soil surface, cm (UNSCEAR / AB-4: 1 m).</summary>
    public double HeightCm { get; init; } = 100;
    /// <summary>Half-width of the track-length scoring slab around the height. The slab average differs from the point
    /// value by about (δ²/6)·Φ''/Φ; for the uncollided kernel that is ~μ_a δ²/(6h) ≈ 2e-5 at δ = 10 cm (reported, and the
    /// uncollided oracle compares slab average with slab average).</summary>
    public double SlabHalfWidthCm { get; init; } = 10;
    /// <summary>Source depths are sampled from λe^(−λd) with λ = factor × μ_soil(E₀), weight e^(λd)/λ. Any factor in (0,1)
    /// is unbiased with finite variance (the photon's attenuation never falls below μ_soil(E₀): energy only decreases and
    /// μ rises as energy falls below 20 MeV); the factor changes efficiency only.</summary>
    public double DepthSamplingFactor { get; init; } = .5;
    /// <summary>Photons below this energy end their history (lower limit of the XCOM data).</summary>
    public double TransportCutoffKeV { get; init; } = 1;
    /// <summary>Scattered-photon energy bin edges, keV, strictly increasing.</summary>
    public double[] ContinuumEdgesKeV { get; init; } = [];
    /// <summary>Equal zenith-cosine bins over [−1, 1]; must be even so the upward half has its own bins.</summary>
    public int ZenithBins { get; init; } = 20;
    /// <summary>Energy grid points (keV) of the fluorescence-bound table, log-spaced from the cutoff to each K edge.</summary>
    public int FluorescenceEnergyPoints { get; init; } = 200;

    public void Validate()
    {
        if (!(HeightCm > 0 && SlabHalfWidthCm > 0 && SlabHalfWidthCm < HeightCm) || !double.IsFinite(HeightCm + SlabHalfWidthCm))
            throw new ArgumentOutOfRangeException(nameof(HeightCm), "Need 0 < slab half-width < height.");
        if (!(DepthSamplingFactor > 0 && DepthSamplingFactor < 1)) throw new ArgumentOutOfRangeException(nameof(DepthSamplingFactor));
        if (!(TransportCutoffKeV > 0) || !double.IsFinite(TransportCutoffKeV)) throw new ArgumentOutOfRangeException(nameof(TransportCutoffKeV));
        if (ZenithBins < 2 || ZenithBins % 2 != 0) throw new ArgumentOutOfRangeException(nameof(ZenithBins));
        if (ContinuumEdgesKeV is not { Length: >= 2 } e || e.Zip(e.Skip(1)).Any(p => !(p.Second > p.First)) || !(e[0] >= TransportCutoffKeV))
            throw new ArgumentException("Continuum edges must be strictly increasing and start at or above the cutoff.");
        if (FluorescenceEnergyPoints < 2) throw new ArgumentOutOfRangeException(nameof(FluorescenceEnergyPoints));
    }
}
