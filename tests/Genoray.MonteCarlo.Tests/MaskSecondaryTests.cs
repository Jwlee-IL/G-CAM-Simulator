using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Detector;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>
/// Mask tungsten secondaries a pure-attenuation mask omits: Compton scatter (dominant at 662 keV) and W
/// K-fluorescence (59/67 keV, only above the K-edge, heavily self-absorbed). The secondary is real physics —
/// Klein-Nishina scatter + the true W characteristic X-ray energies — not a hand-added line.
/// </summary>
public class MaskSecondaryTests
{
    // --- Cross-section helpers ---

    [Fact]
    public void PhotoFraction_DecreasesWithEnergy()
    {
        Assert.True(MaskSecondary.PhotoFraction(100) > 0.9);        // low E: photoelectric dominates in W
        Assert.InRange(MaskSecondary.PhotoFraction(662), 0.05, 0.15);
        Assert.True(MaskSecondary.PhotoFraction(100) > MaskSecondary.PhotoFraction(662));
        Assert.True(MaskSecondary.PhotoFraction(662) > MaskSecondary.PhotoFraction(1332));
    }

    [Fact]
    public void MuRel_AnchoredAt662_HugeForFluorescence()
    {
        Assert.Equal(1.0, MaskSecondary.MuRel(662), 3);
        Assert.True(MaskSecondary.MuRel(59.3) > 30, "59 keV must be strongly attenuated in W (self-absorption)");
        Assert.True(MaskSecondary.MuRel(1332) < 1.0);
    }

    // --- Interaction: Compton dominates at 662; fluorescence only above the K-edge ---

    [Fact]
    public void At662_MostlyComptonScatter_LowerEnergy()
    {
        var sec = new MaskSecondary();
        var rng = new DefaultRandom(1);
        var down = new Vector3(0, 0, -1);
        int compton = 0, fluor = 0, absorbed = 0;
        for (int i = 0; i < 20000; i++)
        {
            var (produced, e, _, isFluor) = sec.Interact(661.7, down, rng);
            if (!produced) { absorbed++; continue; }
            if (isFluor) { fluor++; Assert.True(e is MaskSecondary.KaKeV or MaskSecondary.KbKeV); }
            else { compton++; Assert.True(e < 661.7 && e > 100.0); }   // scattered photon is degraded
        }
        Assert.True(compton > 0.8 * (compton + fluor + absorbed), "Compton should dominate at 662 keV");
        Assert.True(fluor > 0, "some K-fluorescence should occur above the edge");
    }

    [Fact]
    public void BelowKEdge_NoFluorescence()
    {
        var sec = new MaskSecondary();
        var rng = new DefaultRandom(2);
        var down = new Vector3(0, 0, -1);
        for (int i = 0; i < 10000; i++)
        {
            var (_, _, _, isFluor) = sec.Interact(60.0, down, rng);   // below the 69.5 keV K-edge
            Assert.False(isFluor);
        }
    }

    // --- Study: the mask emits an escaping secondary background, dominated by forward Compton scatter ---

    [Fact]
    public void Study_ProducesForwardScatterBackground_FluorescenceNegligible()
    {
        var cfg = new SimulationConfig();
        var r = new MaskSecondaryStudy().Run(cfg, 661.7, samples: 1_000_000, bins: 350, maxEnergyKeV: 700.0);

        Assert.InRange(r.OpenFraction, 0.4, 0.6);                    // MURA ≈ half open
        Assert.True(r.SecondaryPerPrimaryPct > 1.0, $"a real background should escape, got {r.SecondaryPerPrimaryPct}%");
        Assert.True(r.FluorPct < 5.0, $"K X-rays are self-absorbed → a tiny share, got {r.FluorPct}%");

        // Arriving scatter is forward (backscatter heads away): counts live above ~250 keV, essentially none at 150.
        double bw = 700.0 / 350;
        double lowBand = 0.0, highBand = 0.0;
        for (int i = 0; i < r.SecondaryHist.Length; i++)
        {
            double e = r.BinCenters[i];
            if (e is > 140 and < 160) lowBand += r.SecondaryHist[i];
            if (e is > 550 and < 650) highBand += r.SecondaryHist[i];
        }
        Assert.True(highBand > lowBand * 5.0, "forward scatter peaks near the primary, not at low energy");
    }
}
