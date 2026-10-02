namespace Gcam.Configuration;

/// <summary>The detection chain as selectable REAL parts (same idea as the ADC preset): pick a scintillator, a
/// photosensor and a preamp/shaper, and the pulse shape AND the energy resolution are DERIVED from their specs
/// rather than hand-set. Feeds <see cref="FrontEndConfig"/> (→ <c>FrontEndModel</c> resolution) and the waveform
/// pulse/shaper. Numbers are representative datasheet values; adjust to a specific part as needed.</summary>
public static class FrontEndParts
{
    public const double AdcSampleRateHz = 125e6;

    /// <summary>The chain selected at startup by the original viewer.</summary>
    public static FrontEndChain Default => new(Scintillators[0], Sensors[0], Preamps[1]);

    /// <summary>Bi-exponential scintillation / CSP convolution, with the legacy half-sample guards.</summary>
    public static (double RiseSamples, double TailSamples) PulseSamples(ScintPreset scintillator,
        PreampPreset preamp, double sampleRateHz = AdcSampleRateHz)
    {
        if (!(sampleRateHz > 0) || !double.IsFinite(sampleRateHz))
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz));
        double nsPerSample = 1e9 / sampleRateHz;
        double rise = Math.Max(0.5, Math.Min(scintillator.DecayNs, preamp.PulseTailNs) / nsPerSample);
        return (rise, Math.Max(rise + 0.5, Math.Max(scintillator.DecayNs, preamp.PulseTailNs) / nsPerSample));
    }

    // Light collection efficiency of the crystal→sensor coupling (shared; folded into N_pe with PDE).
    public const double Collection = 0.50;

    public static readonly IReadOnlyList<ScintPreset> Scintillators =
    [
        new("GAGG(Ce)",  50.0,   90.0, 6.63, 0.035),
        new("NaI(Tl)",   38.0,  230.0, 3.67, 0.055),
        new("LYSO",      33.0,   40.0, 7.10, 0.065),   // + intrinsic Lu-176 background (not modelled here)
        new("CsI(Tl)",   54.0, 1000.0, 4.51, 0.045),
        new("BGO",        8.2,  300.0, 7.13, 0.050),
    ];

    public static readonly IReadOnlyList<SensorPreset> Sensors =
    [
        new("Hamamatsu MPPC S13360-3050", 0.40, 1.03, 5.0e5),
        new("Hamamatsu MPPC S13360-6050", 0.40, 1.05, 2.0e6),
        new("PMT (bialkali, R6231)",      0.25, 1.15, 1.0e1),
    ];

    public static readonly IReadOnlyList<PreampPreset> Preamps =
    [
        // T_sum values are a simulation convention, explicitly separate from the DCR integration window.
        new("Fast CSP + CR-RC^4",        100.0, 150.0, true, CrrcShapingTimeNs: 100.0),
        new("CSP + CR-RC (200 ns)",    200.0, 320.0, true, CrrcShapingTimeNs: 200.0),
        new("Slow shaping (high pileup)", 500.0, 800.0, true, CrrcShapingTimeNs: 500.0),
        new("Trapezoid DAQ",             200.0, 320.0, false),
    ];

    /// <summary>Populate a FrontEndConfig from the chosen parts. Resolution then comes out of the FrontEndModel:
    /// N_pe = lightYield·collection·PDE·E gives the 1/√E statistics, the scintillator's non-proportionality is the
    /// floor, and DCR·integration adds the 1/E low-energy term.</summary>
    public static FrontEndConfig BuildConfig(ScintPreset sc, SensorPreset se, PreampPreset pa) => new()
    {
        LightYieldPhPerKeV = sc.LightYieldPhPerKeV,
        CollectionEfficiency = Collection,
        SipmPde = se.Pde,
        ExcessNoiseFactor = se.Enf,
        IntrinsicResolutionFwhm = sc.NonPropFwhm,
        DarkCountRateHz = se.DcrHz,
        IntegrationTimeNs = pa.IntegrationNs,
    };
}
