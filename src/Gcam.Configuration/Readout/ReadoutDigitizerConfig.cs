namespace Gcam.Configuration;

/// <summary>Per-channel electronics and conversion. The analogue gain is set so that a deposit of
/// <see cref="FullScaleKeV"/> whose whole light reaches ONE channel reaches the ADC's positive full scale — the same
/// keV full-scale convention as the engine's AD9648 preset (14 bit signed, 2000 keV, ENOB 11.8). Codes are signed and
/// pedestal-subtracted (negative noise excursions are kept, not clamped to zero, which would bias low-energy ratios).</summary>
public sealed class ReadoutDigitizerConfig
{
    public int Bits { get; set; } = 14;

    public double FullScaleKeV { get; set; } = 2000;

    /// <summary>Effective number of bits: the ADC's own input-referred noise (AD9648 preset 11.8).</summary>
    public double Enob { get; set; } = 11.8;

    /// <summary>Analogue noise per output channel, RMS, keV-equivalent of one channel (keV of a deposit whose whole light
    /// reaches that channel). Default 3 keV = the summed energy channel's value (Waveform.NoiseKev), used here only as a
    /// labelled sensitivity value; no derived four-output network noise exists.</summary>
    public double NoiseKeV { get; set; } = 3.0;

    /// <summary>Channels whose held code is below this many electronic-noise σ are set to zero before position and
    /// energy (zero suppression, usual with many independent channels); 0 = off, the default.</summary>
    public double ZeroSuppressionSigma { get; set; }
}
