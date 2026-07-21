using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

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
    public void MixedField_ThroughEnergyWindow_SeparatesIsotopesSpatially()
    {
        // Cs-137 (662) + a strong Co-60 (1173+1332) imaged in ONE run through a 662 keV window +
        // crystal Compton. The window admits both (Cs photopeak AND Co downscatter), but the coded
        // decode puts them at different positions -> both localize near their true sources.
        var cfg = Base();
        cfg.PhotonCount = 3_000_000;
        cfg.Decoder.Cyclic = false;
        double frac = cfg.Geometry.MaskDetectorDistanceMm /
                      (cfg.Geometry.MaskDetectorDistanceMm + cfg.Geometry.SourceMaskDistanceMm);
        cfg.Decoder.ReconHalfExtentMm = 0.95 * cfg.Mask.Rank * cfg.Mask.CellPitchMm / frac / 2.0;
        cfg.Decoder.ReconStepMm = 0.4;
        cfg.Sources =
        [
            new SourceConfig { Position = [4, 0, 0.0], ActivityBq = 1.0, Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 0.851 }] },
            new SourceConfig { Position = [-5, 3, 0.0], ActivityBq = 8.0, Lines = [new EmissionLine { EnergyKeV = 1173.2, Intensity = 0.999 }, new EmissionLine { EnergyKeV = 1332.5, Intensity = 0.999 }] },
        ];
        var factory = new ComptonFactory(ComptonStrategy.PerPixelWindow, 661.7, 0.10);
        var r = new MixedFieldStudy(factory).LocalizeMultiple(cfg, k: 2, minSeparationMm: 3.0);

        foreach (var m in MixedFieldStudy.MatchOneToOne(r.TruthXY, r.Found))
            Assert.True(m.ErrorMm < 2.5, $"({m.TruthX},{m.TruthY}) matched peak {m.ErrorMm:F2} mm");
    }

    [Fact]
    public void PerNuclideWindow_SeparatesCsFromNa_InTheImage()
    {
        // The image-domain nuclide separation the WPF viewer uses: a Cs-137 source and a Na-22 source are
        // co-measured, then the SAME scene is imaged twice, each through a DIFFERENT nuclide's photopeak window.
        // The 662 keV window's reconstruction is dominated by the Cs source (Cs emits 662 directly; Na reaches
        // 662 only via weak downscatter), and the 511 keV window's by the Na source — so which nuclide appears is
        // selected by the window. That is what makes co-measured isotopes separate in the reconstructed image.
        SimulationConfig Scene()
        {
            var cfg = Base();
            cfg.PhotonCount = 4_000_000;
            cfg.Decoder.Cyclic = false;
            double frac = cfg.Geometry.MaskDetectorDistanceMm /
                          (cfg.Geometry.MaskDetectorDistanceMm + cfg.Geometry.SourceMaskDistanceMm);
            cfg.Decoder.ReconHalfExtentMm = 0.95 * cfg.Mask.Rank * cfg.Mask.CellPitchMm / frac / 2.0;
            cfg.Decoder.ReconStepMm = 0.4;
            cfg.Sources =
            [
                new SourceConfig { Isotope = "Cs-137", Position = [5, 0, 0.0], ActivityBq = 1.0,
                    Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 0.851 }] },
                new SourceConfig { Isotope = "Na-22", Position = [-4, 4, 0.0], ActivityBq = 1.0,
                    Lines = [new EmissionLine { EnergyKeV = 511.0, Intensity = 1.798 },
                             new EmissionLine { EnergyKeV = 1274.5, Intensity = 0.999 }] },
            ];
            return cfg;
        }
        double[] cs = [5, 0], na = [-4, 4];
        double Dist(FoundSource p, double[] q) => System.Math.Sqrt((p.Xmm - q[0]) * (p.Xmm - q[0]) + (p.Ymm - q[1]) * (p.Ymm - q[1]));

        // Each nuclide's window is its PRIMARY line (Lines[0]) — the same rule the WPF channel builder uses
        // (Isotopes.Get(iso).Lines[0]). Derive the centers from the scene so the test tracks that rule.
        var scene0 = Scene();
        double csCenter = scene0.Sources![0].Lines![0].EnergyKeV;   // 661.7 (Cs primary)
        double naCenter = scene0.Sources![1].Lines![0].EnergyKeV;   // 511.0 (Na primary)

        // Cs window -> the top peak is the Cs source, not the Na source.
        var csWin = new MixedFieldStudy(new ComptonFactory(ComptonStrategy.PerPixelWindow, csCenter, 0.10))
            .LocalizeMultiple(Scene(), k: 1, minSeparationMm: 3.0).Found[0];
        Assert.True(Dist(csWin, cs) < 2.5, $"Cs window should localize Cs @(5,0); got ({csWin.Xmm:F1},{csWin.Ymm:F1})");
        Assert.True(Dist(csWin, na) > 4.0, $"Cs window peak should NOT sit on Na; got ({csWin.Xmm:F1},{csWin.Ymm:F1})");

        // Na window -> the top peak is the Na source, not the Cs source. Switching the window switches the nuclide.
        var naWin = new MixedFieldStudy(new ComptonFactory(ComptonStrategy.PerPixelWindow, naCenter, 0.10))
            .LocalizeMultiple(Scene(), k: 1, minSeparationMm: 3.0).Found[0];
        Assert.True(Dist(naWin, na) < 2.5, $"511 window should localize Na @(-4,4); got ({naWin.Xmm:F1},{naWin.Ymm:F1})");
        Assert.True(Dist(naWin, cs) > 4.0, $"511 window peak should NOT sit on Cs; got ({naWin.Xmm:F1},{naWin.Ymm:F1})");
    }

    [Fact]
    public void ComptonStripping_RecoversCoLocatedCsCount()
    {
        // Cs-137 and a strong Co-60 CO-LOCATED at the origin. The 662 keV window flood OVER-counts Cs because
        // Co-60 downscatters into 662 at the SAME position — the spatial decode cannot split them. Per-pixel
        // Compton stripping (subtract R × the Co 1332 window, with R = Co-only downscatter-into-662 / Co
        // photopeak) removes the contamination and recovers the Cs count. This is the mechanism the WPF
        // Compton-strip toggle and the CLI `mixedstrip` use (themes 16-17).
        // coCenter = Co-60's PRIMARY line (Lines[0]), matching the WPF channel centre — not 1332.5.
        const double wf = 0.10, csLine = 661.7, coCenter = 1173.2;
        const long budget = 4_000_000;
        SourceConfig Cs() => new() { Isotope = "Cs-137", Position = [0, 0, 0.0], ActivityBq = 1.0,
            Lines = [new EmissionLine { EnergyKeV = csLine, Intensity = 0.851 }] };
        SourceConfig Co() => new() { Isotope = "Co-60", Position = [0, 0, 0.0], ActivityBq = 8.0,
            Lines = [new EmissionLine { EnergyKeV = 1173.2, Intensity = 0.999 }, new EmissionLine { EnergyKeV = 1332.5, Intensity = 0.999 }] };
        static double Sum(DetectorImage m) { double s = 0; foreach (var v in m.Raw) s += v; return s; }
        DetectorImage Flood(SourceConfig[] s, double center, long photons)
        {
            var c = Base(); c.Sources = s; c.PhotonCount = photons;
            return new SimulationRunner(new ComptonFactory(ComptonStrategy.PerPixelWindow, center, wf)).Run(c).DetectorImage;
        }

        // "true Cs" reference: Cs at its share of the mixed photon budget (weak source is diluted in a mixed field).
        double wCsE = 1.0 * 0.851, wCoE = 8.0 * (0.999 + 0.999);
        long csPhotons = (long)(budget * (wCsE / (wCsE + wCoE)));
        var trueCs = Flood([Cs()], csLine, csPhotons);
        var raw662 = Flood([Cs(), Co()], csLine, budget);
        var coWin = Flood([Cs(), Co()], coCenter, budget);

        // R calibrated from a Co-ONLY run: downscatter-into-662 per Co photopeak (budget-independent ratio).
        double R = Sum(Flood([Co()], coCenter, budget)) is var hi && hi > 0
            ? Sum(Flood([Co()], csLine, budget)) / hi : 0.0;

        var stripped = new DetectorImage(raw662.Width, raw662.Height);
        for (int y = 0; y < raw662.Height; y++)
            for (int x = 0; x < raw662.Width; x++)
                stripped[x, y] = System.Math.Max(0.0, raw662[x, y] - R * coWin[x, y]);

        double t = Sum(trueCs), rawErr = System.Math.Abs(Sum(raw662) - t), stripErr = System.Math.Abs(Sum(stripped) - t);
        Assert.True(R > 0, $"R should be positive: {R:F3}");
        Assert.True(Sum(raw662) > t * 1.2, $"raw 662 should OVER-count Cs (Co contamination): raw {Sum(raw662):F0} vs true {t:F0}");
        // Stripping should remove MOST of the contamination (at least half the raw error) and land near the true Cs.
        Assert.True(stripErr < 0.5 * rawErr, $"stripping should remove >=half the Cs count error: raw err {rawErr:F0} vs stripped err {stripErr:F0}");
        Assert.InRange(Sum(stripped), 0.5 * t, 1.5 * t);
    }

    [Fact]
    public void MultiLine_SplitsByIntensity()
    {
        // A single source emitting two equal-intensity lines at the SAME energy splits its photons
        // ~50/50 between them; the total weight equals a single-line source of the same summed
        // intensity (same energy so the mask attenuates them identically — isolates the intensity split
        // from the energy-dependent mask μ).
        var oneLine = Base();
        oneLine.Sources = [new SourceConfig { Position = [0, 0, 0.0], ActivityBq = 2.0,
            Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 1.0 }] }];

        var twoLine = Base();
        twoLine.Sources = [new SourceConfig { Position = [0, 0, 0.0], ActivityBq = 1.0,
            Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 1.0 },
                     new EmissionLine { EnergyKeV = 661.7, Intensity = 1.0 }] }];

        double w1 = Weight(oneLine), w2 = Weight(twoLine);
        Assert.True(System.Math.Abs(w2 - w1) / w1 < 0.05, $"two-line {w2:F1} vs one-line {w1:F1}");
    }

    [Fact]
    public void MaskAttenuation_IsEnergyDependent()
    {
        // Low-energy Co-57 (122 keV) is far more attenuated by the closed tungsten cells than
        // high-energy Co-60 (1332 keV), which leaks through — so, at fixed emission weight and
        // position, the 1332 line detects MORE than the 122 line (energy-dependent mask μ).
        SimulationConfig One(double e) { var c = Base(); c.Sources = [new SourceConfig { Position = [0, 0, 0.0],
            ActivityBq = 1.0, Lines = [new EmissionLine { EnergyKeV = e, Intensity = 1.0 }] }]; return c; }
        double wLow = Weight(One(122.1)), wHigh = Weight(One(1332.5));
        Assert.True(wHigh > wLow, $"1332 keV weight {wHigh:F1} should exceed 122 keV {wLow:F1} (more leak)");
    }
}
