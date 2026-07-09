using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>Mask channel geometry: a smaller hole strictly loses sensitivity, and focused
/// (converging) channels concentrate on the focal point.</summary>
public class MaskGeometryTests
{
    private static SimulationConfig Baseline()
        => new() { PhotonCount = 300_000, Seed = 12345, Source = new SourceConfig { Position = [0, 0, 0.0] } };

    [Fact]
    public void SmallerHole_LosesEfficiency()
    {
        var study = new MaskGeometryStudy(new DefaultSimulationFactory());
        var full = Baseline(); full.Mask.HoleFraction = 1.0;
        var small = Baseline(); small.Mask.HoleFraction = 0.5;
        Assert.True(study.Efficiency(small) < study.Efficiency(full),
            "a smaller hole must reduce geometric efficiency");
    }

    [Fact]
    public void FocusedChannels_ConcentrateOnFocalPoint()
    {
        // At a thick mask, converging channels beat straight ones for an on-axis source at the focal
        // distance (aligned channels, no off-axis collimation), but NOT for an off-axis edge source.
        var study = new MaskGeometryStudy(new DefaultSimulationFactory());
        double focal = 100.0;

        var straightCenter = Baseline(); straightCenter.Mask.ThicknessMm = 25.0; straightCenter.Mask.FocalDistanceMm = 0.0;
        var focusedCenter = Baseline(); focusedCenter.Mask.ThicknessMm = 25.0; focusedCenter.Mask.FocalDistanceMm = focal;
        Assert.True(study.Efficiency(focusedCenter) > study.Efficiency(straightCenter),
            "focused channels should beat straight at the on-axis focal point");

        var straightEdge = Baseline(); straightEdge.Mask.ThicknessMm = 25.0; straightEdge.Mask.FocalDistanceMm = 0.0; straightEdge.Source.Position = [7.5, 0, 0.0];
        var focusedEdge = Baseline(); focusedEdge.Mask.ThicknessMm = 25.0; focusedEdge.Mask.FocalDistanceMm = focal; focusedEdge.Source.Position = [7.5, 0, 0.0];
        double sEdgeRatio = study.Efficiency(straightEdge) / study.Efficiency(straightCenter);
        double fEdgeRatio = study.Efficiency(focusedEdge) / study.Efficiency(focusedCenter);
        Assert.True(fEdgeRatio < sEdgeRatio, "point-focusing should narrow the FOV (worse edge/center)");
    }

    [Fact]
    public void CellSize_HasABoundedOptimum()
    {
        // A mid-band cell pitch (shadow ~1-2 px) localizes far better than a too-coarse one (mask
        // overflows the detector) -> the feature size has a bounded optimum, not "finer is always better".
        var study = new MaskGeometryStudy(new DefaultSimulationFactory());
        var mid = Baseline(); mid.Mask.CellPitchMm = 1.0;
        var coarse = Baseline(); coarse.Mask.CellPitchMm = 3.0;
        double rmsMid = study.EfficiencyAndResolution(mid, budget: 400, repeats: 150, failThrMm: 3.0).rmsMm;
        double rmsCoarse = study.EfficiencyAndResolution(coarse, budget: 400, repeats: 150, failThrMm: 3.0).rmsMm;
        Assert.True(rmsMid < rmsCoarse, $"mid-band pitch RMS {rmsMid:F2} should beat coarse {rmsCoarse:F2}");
    }
}
