using Gcam.Configuration;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>Coded-aperture depth (z) estimation by refocusing: the near-field distance is
/// recovered from the shadow magnification, and depth resolution degrades with distance.</summary>
public class DepthTests
{
    private static SimulationConfig Baseline()
        => new() { PhotonCount = 300_000, Seed = 12345, Source = new SourceConfig { Position = [0, 0, 0.0] } };

    private static double[] Assumed()
    {
        var a = new System.Collections.Generic.List<double>();
        for (double s = 20; s <= 260; s += 5) a.Add(s);
        return a.ToArray();
    }

    [Fact]
    public void NearFieldDistance_IsRecovered()
    {
        var study = new DepthStudy(new DefaultSimulationFactory());
        var r = study.Run(Baseline(), trueSmm: 60.0, Assumed());
        Assert.True(System.Math.Abs(r.ErrorMm) < 20.0, $"estimated {r.EstimatedSmm:F1} vs true 60 (err {r.ErrorMm:F1})");
    }

    [Fact]
    public void DepthResolution_DegradesWithDistance()
    {
        // The focus curve broadens with distance -> the far-field estimate is no better than the near.
        var study = new DepthStudy(new DefaultSimulationFactory());
        double near = System.Math.Abs(study.Run(Baseline(), 50.0, Assumed()).ErrorMm);
        double far = FocusWidth(study.Run(Baseline(), 200.0, Assumed()).Curve);
        double nearW = FocusWidth(study.Run(Baseline(), 50.0, Assumed()).Curve);
        Assert.True(far > nearW, $"far-field focus width {far:F0} should exceed near-field {nearW:F0}");
    }

    [Fact]
    public void NearFieldDepth_ConvergesWithCounts()
    {
        // With enough counts the near-field depth estimate is sub-few-mm.
        var study = new DepthStudy(new DefaultSimulationFactory());
        var r = study.RunNoisyDepth(Baseline(), "near", trueSmm: 60.0, Assumed(), counts: 3000, repeats: 40);
        Assert.True(r.DepthRmsMm < 5.0, $"near-field depth RMS {r.DepthRmsMm:F1} mm should be small at 3000 counts");
    }

    [Fact]
    public void Joint_LocalizesLateralBetterThanDepth()
    {
        // The coded aperture constrains (x,y) much better than z: lateral RMS << depth RMS.
        var study = new DepthStudy(new DefaultSimulationFactory());
        var r = study.RunNoisyJoint(Baseline(), "offaxis", sx: 5.0, sy: 0.0, trueSmm: 150.0,
                                    Assumed(), nominalSmm: 100.0, counts: 3000, repeats: 40);
        Assert.True(r.LateralRmsMm < r.DepthRmsMm, $"lateral {r.LateralRmsMm:F1} should beat depth {r.DepthRmsMm:F1}");
        Assert.True(r.LateralRmsMm < 5.0, $"lateral RMS {r.LateralRmsMm:F1} mm should be ~mm");
    }

    [Fact]
    public void Joint3D_BeatsAlternating_OnNearFieldCoupling()
    {
        // The alternating iteration with a FIXED nominal seed (100) stalls on near-field off-axis
        // coupling (true S=60, so the seed is far off). The seed-free 3D search removes that trap.
        // NOTE (Codex round 5): a well-SEEDED alternating iteration converges too — the trap was the
        // seed, not the alternation; the 3D search's value is being seed-free. See docs/AGENTS.Findings.md, theme 24.
        var study = new DepthStudy(new DefaultSimulationFactory());
        var alt = study.RunNoisyJoint(Baseline(), "near", sx: 5.0, sy: 0.0, trueSmm: 60.0,
                                      Assumed(), nominalSmm: 100.0, counts: 1000, repeats: 40);
        var d3 = study.RunNoisyJoint3D(Baseline(), "near", sx: 5.0, sy: 0.0, trueSmm: 60.0,
                                       Assumed(), counts: 1000, repeats: 40);
        Assert.True(d3.LateralRmsMm < alt.LateralRmsMm,
            $"3D lateral RMS {d3.LateralRmsMm:F2} should beat alternating {alt.LateralRmsMm:F2}");
        Assert.True(d3.DepthRmsMm < alt.DepthRmsMm,
            $"3D depth RMS {d3.DepthRmsMm:F1} should beat alternating {alt.DepthRmsMm:F1} near-field");
    }

    private static double FocusWidth(DepthRow[] curve)
    {
        double max = double.NegativeInfinity, min = double.PositiveInfinity;
        foreach (var r in curve) { if (r.Focus > max) max = r.Focus; if (r.Focus < min) min = r.Focus; }
        double thr = min + 0.5 * (max - min);
        double lo = double.NaN, hi = double.NaN;
        foreach (var r in curve)
            if (r.Focus > thr) { if (double.IsNaN(lo)) lo = r.AssumedSmm; hi = r.AssumedSmm; }
        return hi - lo;
    }
}
