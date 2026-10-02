using Gcam.Configuration;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

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

    [Fact]
    public void SideLeakProfile_IsMeanOneAndEdgeWeighted()
    {
        int W = 12, H = 12;

        // sideFraction 0 = flat uniform pedestal (the unchanged model).
        var flat = Background.SideLeakProfile(W, H, 0.0);
        foreach (var v in flat) Assert.Equal(1.0, v, 9);

        // Directional: mean stays exactly 1 (same TOTAL leak), but edges exceed the centre.
        var prof = Background.SideLeakProfile(W, H, 0.8);
        double mean = 0.0; foreach (var v in prof) mean += v;
        mean /= prof.Length;
        Assert.Equal(1.0, mean, 6);

        double corner = prof[0];                       // (0,0) — nearest two walls
        double centre = prof[(H / 2) * W + (W / 2)];   // middle — farthest from all walls
        Assert.True(corner > centre * 1.2,
            $"edge leak should exceed centre: corner {corner:F3} vs centre {centre:F3}");
    }

    [Fact]
    public void GradientProfile_IsMeanOneAndRampsAlongDirection()
    {
        int W = 12, H = 12;

        // contrast 0 = flat uniform pedestal (the unchanged model).
        var flat = Background.GradientProfile(W, H, 0.0, 0.0);
        foreach (var v in flat) Assert.Equal(1.0, v, 9);

        // +x ramp, contrast 0.6: mean stays exactly 1 (same total leak), left column < right column,
        // and the swing hits ±contrast at the extreme columns.
        var prof = Background.GradientProfile(W, H, 0.0, 0.6);
        double mean = 0.0; foreach (var v in prof) mean += v;
        mean /= prof.Length;
        Assert.Equal(1.0, mean, 6);

        int midRow = (H / 2) * W;
        Assert.True(prof[midRow + (W - 1)] > prof[midRow + 0],
            $"+x gradient should rise left->right: {prof[midRow + 0]:F3} -> {prof[midRow + (W - 1)]:F3}");
        Assert.Equal(1.0 - 0.6, prof[midRow + 0], 6);          // leftmost column = 1 - contrast
        Assert.Equal(1.0 + 0.6, prof[midRow + (W - 1)], 6);    // rightmost column = 1 + contrast

        // A +y ramp is constant across each row (no x-dependence), rising bottom->top instead.
        var profY = Background.GradientProfile(W, H, 90.0, 0.6);
        Assert.Equal(profY[midRow + 0], profY[midRow + (W - 1)], 6);
        Assert.True(profY[(H - 1) * W] > profY[0]);
    }

    [Fact]
    public void GradedBackground_IsHarmlessWhileSourceWins_ThenBiasesAtTheKnee()
    {
        // The honest, sim-revealed behaviour: a flat pedestal and a same-level gradient are IDENTICAL while the
        // source peak still wins the argmax (the gradient's low-frequency residual doesn't move it) — then, once
        // the background competes (heavy BSR), the gradient drags the estimate hard toward its strong side while
        // a flat pedestal only fails randomly. BiasMm is the decode of the MC mean map, so it depends on the seed only
        // through that map's residual noise (measured below).
        double[] bsr = [1.0, 4.0];
        var flat = new BackgroundStudy().RunSweep(Base(), bsr, detectedBudget: 400.0, repeats: 30, failThrMm: 3.0);
        var grad = new BackgroundStudy().RunSweep(Base(), bsr, detectedBudget: 400.0, repeats: 30, failThrMm: 3.0,
                                                  gradientContrast: 0.6, gradientAngleDeg: 0.0);

        // While the source wins (BSR 1), the gradient adds no MEANINGFUL bias over flat. (With the integer argmax
        // both snapped to the same recon cell → bit-identical; sub-cell interpolation now resolves the gradient's
        // ~0.001 mm perturbation of the peak neighbours, so they are near-identical rather than exactly equal.)
        Assert.True(System.Math.Abs(flat[0].BiasMm - grad[0].BiasMm) < 0.01,
            $"gradient should not bias while the source wins: flat {flat[0].BiasMm:F4} vs grad {grad[0].BiasMm:F4} mm");

        // At the knee (BSR 4) the gradient biases far more than the flat pedestal. Measured over 64 seeds (TODO-26,
        // 2001–2064): grad 11.18 mm in all; the flat mean map has two competing maxima, 7.45 mm (49 seeds) or 9.39 mm
        // (15), so the excess is 3.73 or 1.78–1.79 mm. The old +2 mm bound failed the second mode; +1 mm sits below
        // both modes and is still 2000× the BSR-1 difference.
        Assert.True(grad[1].BiasMm > flat[1].BiasMm + 1.0,
            $"gradient should dominate the bias at the knee: flat {flat[1].BiasMm:F2} mm vs grad {grad[1].BiasMm:F2} mm");
    }
}
