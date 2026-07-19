using System.Linq;
using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>
/// Finite (extended) source + capsule self-attenuation. A finite source blurs the coded reconstruction (quadrature
/// while small) and, once its size approaches the coded resolution, WASHES the shadow out entirely — the contrast
/// (peak/secondary) collapses toward 1 and localization is lost. A sealed source's capsule attenuates the escaping
/// gammas, killing LOW-energy lines far more than the 662 keV primary.
/// </summary>
public class FiniteSourceTests
{
    [Fact]
    public void SourceSize_BlursTheRecon_ThenWashesItOut()
    {
        var cfg = new SimulationConfig { Seed = 71 };
        var (rows, _, _, _) = new FiniteSourceStudy().Run(cfg, [0.0, 4.0, 14.0], 0.0, 0.0, photonCount: 1_500_000, seed: 71);
        var point = rows[0];
        var small = rows[1];
        var huge = rows[2];

        // A point source reconstructs to the point resolution with a clean peak.
        Assert.InRange(point.ReconPeakFwhmMm, 2.0, 3.5);
        Assert.True(point.ReconConfidence > 1.1, $"point source should give a clean peak, contrast {point.ReconConfidence:F2}");

        // A small (few-mm) source blurs the peak wider and lowers the contrast, but still localizes.
        Assert.True(small.ReconPeakFwhmMm > point.ReconPeakFwhmMm, "a finite source should widen the reconstruction peak");
        Assert.True(small.ReconConfidence < point.ReconConfidence, "a finite source should lower the reconstruction contrast");

        // A source as large as the coded FOV washes the shadow out — contrast → 1 (no peak), localization lost.
        Assert.True(huge.ReconConfidence < 1.05, $"a huge source should wash the shadow out, contrast {huge.ReconConfidence:F2}");
        Assert.True(huge.LocalizationBiasMm > small.LocalizationBiasMm, "washout should destroy localization");
    }

    [Fact]
    public void Capsule_SuppressesTheLowEnergyLineMuchMoreThanThePrimary()
    {
        var rows = new FiniteSourceStudy().CapsuleSweep([0.0, 1.0, 2.0, 4.0], pelletDiameterMm: 3.0,
            muPelletPrimary: 0.030, muCapsulePrimary: 0.057, muPelletLowE: 0.5, muCapsuleLowE: 0.94, samples: 300_000);

        var t2 = rows.Single(r => r.ThicknessMm == 2.0);
        // The 662 keV primary mostly survives a couple mm of steel; the 32 keV line is largely gone.
        Assert.True(t2.TransmissionPrimary > 0.8, $"662 keV should mostly transmit, got {t2.TransmissionPrimary:F2}");
        Assert.True(t2.TransmissionLowE < 0.15, $"32 keV should be heavily attenuated, got {t2.TransmissionLowE:F2}");
        Assert.True(t2.TransmissionLowE < 0.25 * t2.TransmissionPrimary, "low-E must be suppressed far more than the primary");

        // Transmission falls monotonically with wall thickness for both lines.
        for (int i = 1; i < rows.Length; i++)
        {
            Assert.True(rows[i].TransmissionPrimary < rows[i - 1].TransmissionPrimary);
            Assert.True(rows[i].TransmissionLowE < rows[i - 1].TransmissionLowE);
        }
    }
}
