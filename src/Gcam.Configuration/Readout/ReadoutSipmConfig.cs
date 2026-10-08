namespace Gcam.Configuration;

/// <summary>SiPM response. Defaults come from the engine's Hamamatsu S13360-3050 preset (FrontEndParts: PDE 0.40,
/// ENF 1.03, 500 kcps on its 3 × 3 mm area). The PDE already contains the microcell fill factor, so the fill is not
/// multiplied in again (review correction 6); the package dead border is geometric (sensor active width).</summary>
public sealed class ReadoutSipmConfig
{
    /// <summary>Photon detection efficiency; null = 0.40 (S13360-3050 preset).</summary>
    public double? Pde { get; set; }

    /// <summary>Excess noise factor of the avalanche gain; null = 1.03 (S13360-3050 preset). Applied as a Gaussian gain
    /// sum with the exact first two moments: Q = n + √((ENF − 1)·n)·G.</summary>
    public double? ExcessNoiseFactor { get; set; }

    /// <summary>Dark-count rate per mm² of active area (Hz); null = the preset's 500 kcps / 9 mm². Scaling the
    /// datasheet rate by area is an assumption.</summary>
    public double? DarkCountRatePerMm2Hz { get; set; }

    /// <summary>Integration window over which dark counts are collected (ns); null = 200 ns (the default chain's
    /// "CSP + CR-RC (200 ns)" preamp integration window). Dark charge is baseline-subtracted by its mean.</summary>
    public double? IntegrationTimeNs { get; set; }

    /// <summary>Microcells per mm² for the no-recovery saturation limit n_fired = N·(1 − exp(−n/N)); 0 = infinite cells
    /// (no saturation, the default). 400 /mm² is the 50 µm-cell device. Microcell RECOVERY during the scintillation pulse
    /// is not modelled (RD-5), so this formula is a limiting bound, not a linearity prediction.</summary>
    public double MicrocellsPerMm2 { get; set; }
}
