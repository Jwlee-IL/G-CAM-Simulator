using System.Text.Json;
using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Tests.Harness;
using Xunit;

namespace Gcam.Tests;

/// <summary>TODO-34: the angres evidence family runs end to end on a tiny budget and keeps its geometry: the one-period grid
/// is the fully coded field, positions sit at z·tan(elements·atan(c/D)), counts never exceed the repeats, and the ladder's
/// shadow sampling is (S + D)/S · c / pixel. No Monte Carlo precision is asserted (the budgets are smoke-sized).</summary>
public sealed class AngularResolutionStudyTests
{
    private static AmbientEvidenceRequest Request(AmbientEvidenceRequest.AngularResolutionSpec spec) => new()
    {
        Seed = 3401, RepoRoot = RepoPaths.Root, Family = "angres",
        Windows = [new AmbientGateRequest.WindowSpec { Name = "cs662", LowKeV = 595.53, HighKeV = 727.87 }],
        AngularResolution = spec
    };

    [Fact]
    public void Main_RunsAndKeepsItsGeometry()
    {
        var spec = new AmbientEvidenceRequest.AngularResolutionSpec
        {
            Scenario = "samples/scenario_handheld.json", SourceDetectorMm = 1000, Window = "cs662",
            SeparationsElements = [1.5], CountsPerSource = [500], Ratios = [1, 0.25], Placements = ["axis", "edge"],
            Valleys = [0.25, 0.5], Repeats = 3, MapPhotons = 20_000, PointPhotons = 20_000, PointCounts = [250], PointRepeats = 3,
            Mlem = [new() { Name = "binary", Kind = "binary", Iterations = [3, 6] }, new() { Name = "area", Kind = "area", PixelSubSamples = 2, Iterations = [4] }]
        };
        var output = AngularResolutionStudy.Run(Request(spec));
        var json = JsonDocument.Parse(JsonSerializer.Serialize(output)).RootElement;
        // One period at 1 m: half-width = rank·c·z/D/2 → atan(7·1000/55/2 / 1000) = 3.6412°.
        Assert.Equal(Math.Atan(7.0 / 55 / 2) * 180 / Math.PI, json.GetProperty("Grid").GetProperty("HalfDeg").GetDouble(), 9);
        Assert.Equal(1000.0 / 945, json.GetProperty("ShadowCellPerPixel").GetDouble(), 12);
        var conditions = json.GetProperty("Conditions");
        Assert.Equal(2 * 1 * 2 * 1, conditions.EnumerateObject().Count());   // placements × Δ × ratios × counts, ideal only
        foreach (var c in conditions.EnumerateObject())
            foreach (var d in c.Value.GetProperty("Decoders").EnumerateObject())
            {
                Assert.All(d.Value.GetProperty("Pass").EnumerateArray(), v => Assert.InRange(v.GetInt32(), 0, 3));
                Assert.All(d.Value.GetProperty("Null").EnumerateArray(), v => Assert.InRange(v.GetInt32(), 0, 3));
            }
        Assert.True(conditions.EnumerateObject().First().Value.GetProperty("Decoders").TryGetProperty("area@4", out _));
        Assert.True(json.GetProperty("Point").GetProperty("Mlem").TryGetProperty("binary@6", out _));
    }

    [Fact]
    public void Main_RefusesASourceOutsideTheSearchGrid()
    {
        // Edge placement with the outer source at 4 elements: beyond the one-period grid (3.5 elements).
        var spec = new AmbientEvidenceRequest.AngularResolutionSpec
        {
            Scenario = "samples/scenario_handheld.json", SourceDetectorMm = 1000, Window = "cs662", JitterElements = 0,
            SeparationsElements = [1], CountsPerSource = [100], Ratios = [1], Placements = ["edge"], EdgeOuterElements = 4,
            Valleys = [0.25], Repeats = 1, MapPhotons = 1000
        };
        Assert.Throws<ArgumentException>(() => AngularResolutionStudy.Run(Request(spec)));
    }

    [Fact]
    public void Ladder_RunsEveryStep()
    {
        var spec = new AmbientEvidenceRequest.AngularResolutionSpec
        {
            Ladder = new()
            {
                LabScenario = "samples/scenario.json", HeadScenario = "samples/scenario_handheld.json", HeadSourceDetectorMm = 1000,
                Ev11SeparationsMm = [3], Ev11Photons = 20_000, Ev11Iterations = 5, SeparationsElements = [1.5], CountsPerSource = [0, 500],
                MapPhotons = 20_000, Repeats = 2, Valleys = [0.25], BinaryIterations = 5, AreaIterations = 5, AreaPixelSubSamples = 2
            }
        };
        var json = JsonDocument.Parse(JsonSerializer.Serialize(AngularResolutionStudy.Run(Request(spec)))).RootElement;
        var steps = json.GetProperty("Steps");
        Assert.Equal(5, steps.EnumerateObject().Count());
        // (S + D)/S · c / pixel: lab 160 / 100 = 1.6; hand-held at 1 m 1000 / 945.
        Assert.Equal(1.6, steps.GetProperty("L1-blind").GetProperty("ShadowCellPerPixel").GetDouble(), 12);
        Assert.Equal(1000.0 / 945, steps.GetProperty("L5-head-far").GetProperty("ShadowCellPerPixel").GetDouble(), 12);
        Assert.True(json.GetProperty("Ev11CountsPerSource").GetDouble() > 0);
    }

    [Fact]
    public void SelfConsistentFlood_LandsTheOpenShareOfAimedPhotons()
    {
        // Aim points are uniform over the detector and map linearly onto the mask plane (p → p·(1 − D/z) for a centred
        // source), so the landed share is the open-area fraction of the detector's back-projected patch: here the 12 mm
        // detector → a 7.5 mm square at the mask centre, its open fraction integrated exactly cell by cell. Binomial SE
        // √(p(1−p)/N), 4 SE.
        var lab = ConfigLoader.Load(RepoPaths.Sample("scenario.json"));
        var pattern = Gcam.Masks.MuraGenerator.Mosaic(lab.Mask.Rank, lab.Mask.MosaicX, lab.Mask.MosaicY);
        double d = lab.Geometry.MaskDetectorDistanceMm, z = d + lab.Geometry.SourceMaskDistanceMm;
        double half = lab.Detector.PixelsX * lab.Detector.PixelPitchMm / 2 * (1 - d / z), maskHalf = pattern.Width * lab.Mask.CellPitchMm / 2;
        double open = 0;
        for (int cx = 0; cx < pattern.Width; cx++)
            for (int cy = 0; cy < pattern.Height; cy++)
            {
                if (!pattern[cx, cy]) continue;
                double x0 = cx * lab.Mask.CellPitchMm - maskHalf, y0 = cy * lab.Mask.CellPitchMm - maskHalf;
                double ox = Math.Max(0, Math.Min(x0 + lab.Mask.CellPitchMm, half) - Math.Max(x0, -half));
                double oy = Math.Max(0, Math.Min(y0 + lab.Mask.CellPitchMm, half) - Math.Max(y0, -half));
                open += ox * oy;
            }
        double expected = open / (4 * half * half);
        const long n = 400_000;
        var hits = AngularResolutionStudy.SelfConsistentFlood(lab, (0, 0), n, Gcam.Core.DefaultRandom.FromKey(1), out _).Hits;
        Stat.Within(hits, expected, Math.Sqrt(expected * (1 - expected) / n), what: "landed share");
    }
}
