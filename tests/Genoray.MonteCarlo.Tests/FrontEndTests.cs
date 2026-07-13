using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Detector;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

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
