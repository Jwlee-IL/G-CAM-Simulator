using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>The physical SiPM front-end model (port of rtl/frontend_model.py) folded into the C# pipeline:
/// energy resolution from the photoelectron budget, energy-dependent (1/√E), and its effect on the
/// crystal-Compton detector's energy discrimination.</summary>
public class FrontEndTests
{
    // CeBr3 on a good MPPC — matches frontend_model.py's constants.
    private static FrontEndConfig CeBr3() => new()
    {
        LightYieldPhPerKeV = 45, CollectionEfficiency = 0.60, SipmPde = 0.45,
        ExcessNoiseFactor = 1.20, IntrinsicResolutionFwhm = 0.032,
    };

    [Fact]
    public void Fwhm_MatchesPhotoelectronBudget_At662()
    {
        var m = new FrontEndModel(CeBr3());
        // N_pe = 45·662·0.6·0.45 = 8043; R_stat = 2.355·√(1.2/8043) = 2.88%; R_tot = √(2.88² + 3.2²) = 4.30%.
        Assert.Equal(8043.3, m.Photoelectrons(662.0), 0);
        Assert.Equal(0.0430, m.FwhmFraction(662.0), 3);
    }

    [Fact]
    public void Fwhm_ImprovesWithEnergy_AsOneOverSqrtE()
    {
        var m = new FrontEndModel(CeBr3());
        // The statistical part scales 1/√E, so higher-energy lines resolve BETTER (smaller FWHM fraction).
        Assert.True(m.FwhmFraction(1332.0) < m.FwhmFraction(662.0));
        Assert.True(m.FwhmFraction(122.0) > m.FwhmFraction(662.0));
        // ...but never below the intrinsic floor.
        Assert.True(m.FwhmFraction(1_000_000.0) >= 0.032 - 1e-9);
    }

    [Fact]
    public void Measure_IsUnbiased_WithFwhmSpread()
    {
        var m = new FrontEndModel(CeBr3());
        var rng = new DefaultRandom(7);
        double sum = 0, sumSq = 0; int n = 40_000;
        for (int i = 0; i < n; i++) { double e = m.Measure(662.0, rng); sum += e; sumSq += e * e; }
        double mean = sum / n, sd = System.Math.Sqrt(sumSq / n - mean * mean);
        Assert.Equal(662.0, mean, 0);                                   // unbiased
        Assert.Equal(0.0430, 2.3548 * sd / mean, 2);                    // spread ≈ the FWHM
    }

    [Fact]
    public void Dcr_IsParallelNoise_ScalingOneOverE()
    {
        var noDcr = new FrontEndModel(CeBr3());
        Assert.Equal(0.0, noDcr.DcrFwhmFraction(662.0), 12);            // 0 by default (backward compatible)

        var cfg = CeBr3(); cfg.DarkCountRateHz = 1.0e6; cfg.IntegrationTimeNs = 200.0;
        var m = new FrontEndModel(cfg);
        // Parallel-noise term scales 1/E, so the 122→662 ratio equals 662/122 (not √ of it).
        double ratio = m.DcrFwhmFraction(122.0) / m.DcrFwhmFraction(662.0);
        Assert.Equal(662.0 / 122.0, ratio, 2);
    }

    [Fact]
    public void Dcr_TermConcentratesAtLowEnergy_AndRaisesTotal()
    {
        // A stressed DCR (10 Mcps, 1 µs window) makes the DCR term measurable at low energy — where it
        // concentrates (1/E) — while adding in quadrature it only nudges the total (dominated by the intrinsic
        // + statistical terms). So DCR is a low-energy / weak-source concern, not a photopeak one.
        var clean = new FrontEndModel(CeBr3());
        var cfg = CeBr3(); cfg.DarkCountRateHz = 10.0e6; cfg.IntegrationTimeNs = 1000.0;
        var m = new FrontEndModel(cfg);
        Assert.True(m.DcrFwhmFraction(122.0) > 0.005, $"DCR term should be measurable at 122 keV, got {m.DcrFwhmFraction(122.0):P2}");
        Assert.True(m.DcrFwhmFraction(122.0) > 5.0 * m.DcrFwhmFraction(1332.0), "DCR term far larger at low energy");
        Assert.True(m.FwhmFraction(122.0) > clean.FwhmFraction(122.0), "DCR raises the total FWHM (monotone)");
    }

    [Fact]
    public void FrontEnd_BroadensPhotopeak_ShrinkingTightWindowCounts()
    {
        // A tight ±2% window around 662: with the front-end resolution (~4.3% FWHM) many photopeak events
        // scatter out of the window, so fewer counts are accepted than the perfect-energy (no front-end) case.
        var baseCfg = new SimulationConfig
        {
            PhotonCount = 300_000, Seed = 4242,
            Source = new SourceConfig { Position = [0, 0, 0.0], EnergyKeV = 661.7, DirectionalBiasing = true },
        };

        double Detected(bool withFrontEnd)
        {
            var cfg = baseCfg.Clone();
            if (withFrontEnd) cfg.Detector.FrontEnd = CeBr3();
            return new SimulationRunner(new ComptonFactory(ComptonStrategy.Argmax, 661.7, 0.02)).Run(cfg).DetectedWeight;
        }

        double ideal = Detected(false), real = Detected(true);
        Assert.True(real < 0.9 * ideal,
            $"front-end smearing should drop tight-window counts: ideal {ideal:F1} vs front-end {real:F1}");
    }
}
