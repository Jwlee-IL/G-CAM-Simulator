using Gcam.Configuration;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>
/// Mask FORWARD-SCATTER folded into the coded image: a primary hitting a closed tungsten cell can Compton-scatter
/// forward and reach the detector as a blurred pedestal. Theme 41 tallied it as an escape spectrum; this measures the
/// imaging impact. It is doubly suppressed — geometrically (wide-angle scatter drifts off the small detector over the
/// mask–detector gap) and by the balanced MURA decoder (which rejects the smooth pedestal) — so it is a MINOR
/// contaminant; the surviving scatter is small-angle, near-662, and mostly inside the photopeak window.
/// </summary>
public class MaskScatterTests
{
    private static (MaskScatterRow[] rows, double[] spec, double bin) Run(double gapMm, long photons = 2_000_000)
    {
        var cfg = new SimulationConfig { Seed = 4242 };
        cfg.Geometry.MaskDetectorDistanceMm = gapMm;
        var (rows, spec, bin, _) = new MaskScatterStudy().Run(cfg, sourceXMm: 6.0, sourceYMm: 0.0,
            photonCount: photons, windowLoKeV: 590.0, windowHiKeV: 730.0, seed: 4242);
        return (rows, spec, bin);
    }

    [Fact]
    public void Contamination_IsSmall_AndFallsAsTheGapWidens()
    {
        var near = Run(15.0).rows;
        var far = Run(55.0).rows;

        // Present but small at a close gap; a real coded camera's balanced decoder + finite detector keep it minor.
        Assert.InRange(near[1].ContaminationPct, 0.5, 8.0);
        // The mask→detector drift throws wide-angle scatter off the detector, so a wider gap contaminates far less.
        Assert.True(near[1].ContaminationPct > 2.0 * far[1].ContaminationPct,
            $"near-gap contamination {near[1].ContaminationPct:F2}% should dwarf far-gap {far[1].ContaminationPct:F2}%");
    }

    [Fact]
    public void EnergyWindow_RemovesTheDownShiftedScatter_ButNotTheForwardTail()
    {
        var (rows, spec, bin) = Run(15.0);
        // The window never adds counts, and at a close gap it removes the down-shifted (larger-angle) part.
        Assert.True(rows[2].ContaminationPct <= rows[1].ContaminationPct);
        Assert.True(rows[2].ContaminationPct < rows[1].ContaminationPct,
            "the photopeak window should reject the down-shifted scatter at a close gap");
        // But most arriving scatter is small-angle forward (near 662, IN the window) — the irreducible part.
        double tot = 0, inWin = 0, eSum = 0;
        for (int i = 0; i < spec.Length; i++)
        {
            double e = (i + 0.5) * bin;
            tot += spec[i]; eSum += e * spec[i];
            if (e >= 590 && e <= 730) inWin += spec[i];
        }
        Assert.True(tot > 0);
        Assert.True(inWin / tot > 0.5, $"most mask scatter reaching the detector is in-window, got {inWin / tot:P0}");
        Assert.InRange(eSum / tot, 500.0, 661.7);   // down-shifted from the primary, but only mildly
    }

    [Fact]
    public void BalancedDecoder_LargelyRejectsTheSmoothPedestal()
    {
        var rows = Run(15.0).rows;
        // The MURA ±1 decoding array correlates a smooth scatter pedestal to ~0, so the coded-image contrast
        // (peak/secondary confidence) barely moves even with a few-% pedestal added.
        double drop = System.Math.Abs(rows[1].Confidence - rows[0].Confidence) / rows[0].Confidence;
        Assert.True(drop < 0.05, $"balanced decoder should reject the pedestal; confidence moved {drop:P1}");
        // The primary flood localizes (the decode is valid).
        Assert.True(rows[0].LocalizationBiasMm < 3.0, $"primary localization {rows[0].LocalizationBiasMm:F2} mm");
    }
}
