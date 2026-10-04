using System.Text.Json;
using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

public sealed class AmbientEvidenceTests
{
    /// <summary>A small request on the committed validated spectrum: budgets far below evidence grade, enough to exercise
    /// every branch (both bounds, both windows, one field and one live time).</summary>
    internal static AmbientEvidenceRequest Request(string family) => new()
    {
        Seed = 4101, RepoRoot = RepoPaths.Root, Family = family,
        Spectrum = new() { File = "samples/ambient/terrestrial-unscear2000-v1.json", Sha256 = "4d48bc4272ce4c25ff6ae9b13ed7583e3c8f467afffdb0225956a70cc435b3f4" },
        AmbientHistories = 3000, CalibrationHistories = 3000, SourcePhotons = 20_000,
        FieldsMicroSvPerHour = [0.1], Bounds = [AmbientGeometry.BareCrystalAllFaces, AmbientGeometry.FrontOnlyThroughMask],
        Windows = [new() { Name = "open" }, new() { Name = "cs662", LowKeV = 595.53, HighKeV = 727.87 }],
        ExposuresS = [10], Repeats = 4
    };

    internal static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public void SourceLines_IsTheSumOfItsLinesEachOnItsOwnStream()
    {
        // Definition: line l is GateResponse.Source with energy, intensity and the derived seed of purpose + l; the map is
        // their pixel-wise sum (two terms, so the floating-point sum is the same either way).
        var config = Rigs.Lab(seed: 4102);
        var windows = new[] { CountingWindow.Open(), new CountingWindow("cs", 595.53, 727.87, 0) };
        AmbientEvidenceRequest.SourceLine[] lines = [new() { EnergyKeV = 1173.2, Intensity = 0.999 }, new() { EnergyKeV = 1332.5, Intensity = 0.999 }];
        var sum = AmbientEvidence.SourceLines(config, lines, 5_000, 4103, 700u, windows);
        var parts = lines.Select((l, i) =>
        {
            var c = config.Clone(); c.Source.EnergyKeV = l.EnergyKeV; c.Source.BranchingRatio = l.Intensity;
            return GateResponse.Source(c, 5_000, GateResponse.StreamSeed(4103, 700u + (uint)i), windows);
        }).ToArray();
        for (int w = 0; w < windows.Length; w++)
            Assert.Equal(parts[0].RatePerPixel[w].Zip(parts[1].RatePerPixel[w], (a, b) => a + b).ToArray(), sum.RatePerPixel[w]);
    }

    [Fact]
    public void SourceLines_IsLinearInTheLineIntensity()
    {
        // The intensity only scales the per-photon weight: doubling it doubles every pixel exactly (a power-of-two scale).
        var config = Rigs.Lab(seed: 4104);
        var windows = new[] { CountingWindow.Open() };
        var one = AmbientEvidence.SourceLines(config, [new() { EnergyKeV = 661.7, Intensity = 0.4 }], 5_000, 4105, 710u, windows);
        var two = AmbientEvidence.SourceLines(config, [new() { EnergyKeV = 661.7, Intensity = 0.8 }], 5_000, 4105, 710u, windows);
        Assert.Equal(one.RatePerPixel[0].Select(v => 2 * v).ToArray(), two.RatePerPixel[0]);
    }

    [Fact]
    public void Antimask_IdealCalibratedIsSingle_AndTheBudgetsAreTheDeclaredOnes()
    {
        var q = Request("antimask");
        q.Antimask = new() { Scenario = "samples/scenario.json", SourceCounts = 400 };
        var conditions = Json(AmbientAntimaskStudy.Run(q)["Conditions"]);
        foreach (var w in new[] { "open", "cs662" })
        {
            // Without a field nothing is subtracted: the calibrated image is the single image, decoded by the same decoder.
            var ideal = conditions.GetProperty($"{w}|ideal");
            Assert.Equal(ideal.GetProperty("Single").GetProperty("RmsErrorMm").GetDouble(), ideal.GetProperty("Calibrated").GetProperty("RmsErrorMm").GetDouble());
            Assert.Equal(0, ideal.GetProperty("ExpectedBackgroundCounts").GetDouble());
            // Bare bound: the antimask exposure sees the same field for half the time — exactly half the background.
            var bare = conditions.GetProperty($"{w}|t=10|F=0.1|BareCrystalAllFaces");
            Assert.Equal(bare.GetProperty("ExpectedBackgroundCounts").GetDouble() / 2, bare.GetProperty("ExpectedAntimaskBackgroundCounts").GetDouble(), 9);
            Assert.True(bare.GetProperty("ExpectedBackgroundCounts").GetDouble() > 0);
            Assert.Equal(400, bare.GetProperty("ExpectedSourceCounts").GetDouble());
            Assert.Equal(4, bare.GetProperty("Antimask").GetProperty("Repeats").GetInt32());
        }
    }

    [Fact]
    public void FieldOfView_OnAxisSourceGivesN0_AndFractionsAreProbabilities()
    {
        var q = Request("fov");
        q.FieldOfView = new()
        {
            Scenario = "samples/scenario_handheld.json", SourceMaskDistancesMm = [1000], DirectionsDeg = [0], AnglesDeg = [0, 2, 10],
            OnAxisCounts = [50], GridHalfAngleDeg = 20, GridStepDeg = 1.0, CalibrationRepeats = 3
        };
        var rows = Json(AmbientFieldOfViewStudy.Run(q)["Rows"]).EnumerateArray().ToArray();
        Assert.Equal(2 * 3 * 3, rows.Length);    // windows × (ideal + 2 bounds) × angles
        foreach (var r in rows)
        {
            if (r.GetProperty("Angle").GetDouble() == 0)
                Assert.Equal(50, r.GetProperty("ExpectedSourceCounts").GetDouble(), 9);   // N0 by construction
            if (r.GetProperty("Env").GetString() == "ideal") Assert.Equal(0, r.GetProperty("ExpectedBackgroundCounts").GetDouble());
            foreach (var name in new[] { "LocNonCyclic", "LocCyclic", "OutsidePeak", "FalseInField" })
                Assert.InRange(r.GetProperty(name).GetDouble(), 0, 1);
        }
        // One flag threshold per series (window × environment), shared by its angles.
        Assert.All(rows.GroupBy(r => r.GetProperty("Window").GetString() + r.GetProperty("Env").GetString()),
            g => Assert.Single(g.Select(r => r.GetProperty("FlagThresholdMm").GetDouble()).Distinct()));
    }

    [Fact]
    public void Separation_IdealHasNothingToSubtract_AndCoAddsToThe662Window()
    {
        var q = Request("separation");
        q.Windows = [new() { Name = "cs662", LowKeV = 595.53, HighKeV = 727.87 }, new() { Name = "co1332", LowKeV = 1199.25, HighKeV = 1465.75 }];
        q.Separation = new()
        {
            Scenario = "samples/scenario.json", CsActivityBq = 1e5, CoActivityBq = 8e5, ExposureS = 1,
            CsLines = [new() { EnergyKeV = 661.7, Intensity = 0.851 }],
            CoLines = [new() { EnergyKeV = 1173.2, Intensity = 0.999 }, new() { EnergyKeV = 1332.5, Intensity = 0.999 }],
            Scenes = [new() { Name = "separated", CsMm = [4, 0], CoMm = [-5, 3] }, new() { Name = "co-located", CsMm = [0, 0], CoMm = [0, 0] }],
            CsWindow = "cs662", CoWindow = "co1332", ReconHalfExtentMm = 8.8667, ReconStepMm = 0.4
        };
        var result = AmbientSeparationStudy.Run(q);
        double r = Json(result["Rates"]).GetProperty("R").GetDouble();
        Assert.True(r > 0 && double.IsFinite(r));
        var conditions = Json(result["Conditions"]);
        foreach (var scene in new[] { "separated", "co-located" })
        {
            var ideal = conditions.GetProperty($"{scene}|ideal");
            Assert.Equal(ideal.GetProperty("Unfloored").GetProperty("Mean").GetDouble(), ideal.GetProperty("Subtracted").GetProperty("Mean").GetDouble());
            Assert.True(ideal.GetProperty("ExpectedWindowCounts").GetDouble() > ideal.GetProperty("TrueCsCounts").GetDouble());
        }
        Assert.Equal(JsonValueKind.Null, conditions.GetProperty("co-located|ideal").GetProperty("CsLocatedWithin1Mm").ValueKind);
    }

    [Fact]
    public void Sweep_CountsArePositionTalliesAndThePullIsReportedPerBoundAndWindow()
    {
        var q = Request("sweep");
        q.SourcePhotons = 5_000;
        q.Repeats = 2;
        q.Sweep = new()
        {
            Scenarios = [new() { Name = "lab", Scenario = "samples/scenario.json", PointsMm = [[0, 0], [12, 0]] }],
            HalfExtentMm = 1.5, StepMm = 1.5, ReconStepMm = 0.75, PointRepeats = 3, MlemIterations = 5, MlemRepeats = 1
        };
        var lab = Json(AmbientSweepStudy.Run(q)["Scenarios"]).GetProperty("lab");
        Assert.Equal(9, lab.GetProperty("GridPositions").GetInt32());
        foreach (var s in lab.GetProperty("Sweep").EnumerateObject())
        {
            double expected = s.Value.GetProperty("ExpectedLocalized").GetDouble();
            Assert.InRange(expected, 0, 9);
            Assert.Equal(9, s.Value.GetProperty("Fraction").GetArrayLength());
        }
        Assert.Equal(2 * 2, lab.GetProperty("BackgroundPull").EnumerateObject().Count());
        Assert.Equal(JsonValueKind.Null, lab.GetProperty("Points").GetProperty("(0,0)|open|ideal").GetProperty("GhostFraction").ValueKind);
    }
}
