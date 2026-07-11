using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>The mixed-isotope field source: multiple (position, line) emitters imaged in one run,
/// with contributions weighted by activity × line intensity. Validates the emission weighting by
/// superposition against the single-source path.</summary>
public class MixedFieldTests
{
    private static SimulationConfig Base()
        => new() { PhotonCount = 400_000, Seed = 12345,
                   Source = new SourceConfig { Position = [0, 0, 0.0], DirectionalBiasing = true } };

    private static double Weight(SimulationConfig c)
        => new SimulationRunner(new DefaultSimulationFactory()).Run(c).DetectedWeight;

    [Fact]
    public void SingleSourceScene_MatchesSinglePath()
    {
        // A one-source mixed field must reproduce the classic single-source detected weight.
        var single = Base();
        single.Source.Position = [3, 0, 0.0]; single.Source.EnergyKeV = 661.7; single.Source.BranchingRatio = 1.0;

        var scene = Base();
        scene.Sources = [new SourceConfig { Position = [3, 0, 0.0], EnergyKeV = 661.7, BranchingRatio = 1.0, ActivityBq = 1.0 }];

        double ws = Weight(single), wm = Weight(scene);
        Assert.True(System.Math.Abs(wm - ws) / ws < 0.03, $"mixed one-source {wm:F1} vs single {ws:F1}");
    }

    [Fact]
    public void Superposition_IsActivityWeighted()
    {
        // mixed(A activity 1 + B activity 3) total detected weight ~ 0.25·single(A) + 0.75·single(B):
        // the photon count is split 1:3 by activity, and each keeps its own geometric-efficiency weight.
        var a = Base(); a.Source.Position = [6, 0, 0.0]; a.Source.BranchingRatio = 1.0;
        var b = Base(); b.Source.Position = [-6, 4, 0.0]; b.Source.BranchingRatio = 1.0;
        double wa = Weight(a), wb = Weight(b);

        var mix = Base();
        mix.Sources =
        [
            new SourceConfig { Position = [6, 0, 0.0], BranchingRatio = 1.0, ActivityBq = 1.0 },
            new SourceConfig { Position = [-6, 4, 0.0], BranchingRatio = 1.0, ActivityBq = 3.0 },
        ];
        double wm = Weight(mix);
        double expected = 0.25 * wa + 0.75 * wb;
        Assert.True(System.Math.Abs(wm - expected) / expected < 0.03,
            $"mixed {wm:F1} vs expected 0.25·A+0.75·B {expected:F1}");
    }

    [Fact]
    public void MultiSource_AllLocalized_InOneRun()
    {
        // Three sources at three positions imaged in ONE mixed-field run; multi-peak extraction
        // recovers all of them (the recon grid is kept inside the FCFOV to avoid edge artifacts).
        var cfg = Base();
        cfg.PhotonCount = 1_500_000;
        cfg.Decoder.Cyclic = false;
        double frac = cfg.Geometry.MaskDetectorDistanceMm /
                      (cfg.Geometry.MaskDetectorDistanceMm + cfg.Geometry.SourceMaskDistanceMm);
        cfg.Decoder.ReconHalfExtentMm = 0.95 * cfg.Mask.Rank * cfg.Mask.CellPitchMm / frac / 2.0;
        cfg.Decoder.ReconStepMm = 0.4;
        cfg.Sources =
        [
            new SourceConfig { Position = [5, 1, 0.0], ActivityBq = 1.0 },
            new SourceConfig { Position = [-6, 3, 0.0], ActivityBq = 1.0 },
            new SourceConfig { Position = [0, -6, 0.0], ActivityBq = 1.0 },
        ];
        var r = new MixedFieldStudy(new DefaultSimulationFactory())
            .LocalizeMultiple(cfg, k: 3, minSeparationMm: 3.0);

        // One-to-one matching so a single found peak can't be reused to "cover" several truths.
        foreach (var m in MixedFieldStudy.MatchOneToOne(r.TruthXY, r.Found))
            Assert.True(m.ErrorMm < 2.0, $"source ({m.TruthX},{m.TruthY}) matched peak {m.ErrorMm:F2} mm");
    }

    [Fact]
    public void MultiLine_SplitsByIntensity()
    {
        // A single source emitting two equal-intensity lines splits its photons ~50/50 between them;
        // the total weight equals a single-line source of the same summed intensity (same geometry).
        var oneLine = Base();
        oneLine.Sources = [new SourceConfig { Position = [0, 0, 0.0], ActivityBq = 2.0,
            Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 1.0 }] }];

        var twoLine = Base();
        twoLine.Sources = [new SourceConfig { Position = [0, 0, 0.0], ActivityBq = 1.0,
            Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 1.0 },
                     new EmissionLine { EnergyKeV = 1332.5, Intensity = 1.0 }] }];

        // Both have total emission weight 2.0 from the same position, so the detected weight matches
        // (the mask mu is energy-independent, so the two energies detect alike here).
        double w1 = Weight(oneLine), w2 = Weight(twoLine);
        Assert.True(System.Math.Abs(w2 - w1) / w1 < 0.05, $"two-line {w2:F1} vs one-line {w1:F1}");
    }
}
