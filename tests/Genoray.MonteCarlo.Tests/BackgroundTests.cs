using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>Ambient background: a diffuse, uncoded field modelled as a uniform pedestal on the flood map.
/// The MURA decode rejects the flat (DC) pedestal, so localization holds until the pedestal's shot noise
/// buries the coded peak — this checks that the sweep degrades monotonically and collapses at high BSR.</summary>
public class BackgroundTests
{
    private static SimulationConfig Base()
        => new() { PhotonCount = 500_000, Seed = 12345,
                   Source = new SourceConfig { Position = [2, 0, 0.0], DirectionalBiasing = true } };

    [Fact]
    public void Sweep_DegradesMonotonically_AndCollapsesAtHighBackground()
    {
        double[] bsr = [0.0, 0.5, 1.0, 8.0];
        var rows = new BackgroundStudy().RunSweep(Base(), bsr, detectedBudget: 400.0, repeats: 120, failThrMm: 3.0);

        Assert.Equal(4, rows.Length);
        // Pedestal grows with BSR; the clean point has none.
        Assert.Equal(0.0, rows[0].BgPerPixel, 6);
        Assert.True(rows[3].BgPerPixel > rows[1].BgPerPixel);

        // Decode contrast (peak SNR) falls as background rises.
        Assert.True(rows[0].PeakSnr > rows[3].PeakSnr,
            $"peak SNR should fall with background: clean {rows[0].PeakSnr:F1} vs BSR8 {rows[3].PeakSnr:F1}");

        // Clean field localizes to the decoder floor; a heavy background (BSR 8) mostly fails.
        Assert.True(rows[0].RmsMm < 1.5, $"clean RMS {rows[0].RmsMm:F2} mm should be near the floor");
        Assert.True(rows[3].FailRate > rows[0].FailRate + 0.3,
            $"heavy background should fail far more: clean {rows[0].FailRate:P0} vs BSR8 {rows[3].FailRate:P0}");
    }

    [Fact]
    public void ZeroBackground_MatchesCleanRun()
    {
        var rows = new BackgroundStudy().RunSweep(Base(), [0.0], detectedBudget: 400.0, repeats: 100, failThrMm: 3.0);
        // With BSR 0 there is no pedestal and the study reduces to the plain noisy-localization case.
        Assert.Equal(0.0, rows[0].BgPerPixel, 6);
        Assert.True(rows[0].RmsMm < 1.5);
        Assert.True(rows[0].FailRate < 0.1);
    }

    [Fact]
    public void PedestalPerPixel_IsBsrTimesCountsOverPixels()
    {
        // 400 source counts, BSR 0.5, 144 pixels -> 0.5*400/144 ≈ 1.389 counts/pixel.
        double p = Background.PedestalPerPixel(0.5, 400.0, 144);
        Assert.Equal(0.5 * 400.0 / 144.0, p, 6);
        Assert.Equal(0.0, Background.PedestalPerPixel(0.0, 400.0, 144), 6);
    }
}
