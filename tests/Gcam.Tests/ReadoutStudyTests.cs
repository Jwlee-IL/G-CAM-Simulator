using System.Text.Json.Nodes;
using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

/// <summary>TODO-19, RD-7: the readout study recipe is reproducible (same seed → identical numbers, timing fields aside),
/// pairs its readouts on shared transport (the direct reference and every readout see the same localisation events), and
/// reports a failed calibration instead of numbers.</summary>
public class ReadoutStudyTests
{
    private static ReadoutStudyRequest Tiny() => new()
    {
        Geometries = [new ReadoutStudyGeometry { Name = "p3.2", PitchMm = 3.2, GapMm = 0.2 }],
        Readouts =
        [
            new ReadoutStudyVariant { Name = "Ideal", Network = new ChargeNetworkConfig { Topology = NetworkTopology.IdealBilinear } },
            new ReadoutStudyVariant { Name = "Independent", Mode = ReadoutMode.IndependentSipm },
        ],
        Triggers =
        [
            new ReadoutStudyTrigger { Name = "Sum50" },
            new ReadoutStudyTrigger { Name = "And50", Trigger = new ReadoutTriggerConfig { Logic = TriggerLogic.And, Threshold = 50 } },
        ],
        EnergiesKeV = [661.7],
        FloodHistoriesPerCrystal = 20,
        CalibrationHistoriesPerCrystal = 600,
        LocalisationHistories = 20_000,
        RatesCps = [1e5],
        RateHits = 500,
        Base = new ReadoutConfig { Optics = new ReadoutOpticsConfig { PhotonsPerBin = 1000, DepthBins = 4 } },
    };

    private static string WithoutTiming(JsonNode node)
    {
        void Strip(JsonNode? n)
        {
            if (n is JsonObject o)
            {
                foreach (var key in o.Select(p => p.Key).Where(k => k.Contains("Seconds") || k.StartsWith("Microseconds")).ToList()) o.Remove(key);
                foreach (var p in o) Strip(p.Value);
            }
            else if (n is JsonArray a) foreach (var x in a) Strip(x);
        }
        var copy = node.DeepClone();
        Strip(copy);
        return copy.ToJsonString();
    }

    [Fact]
    public void SameSeed_GivesIdenticalNumbers_AndPairsTheReadouts()
    {
        var scenario = Rigs.Lab();
        var first = ReadoutStudy.Run(scenario, Tiny(), 6100001);
        var second = ReadoutStudy.Run(scenario, Tiny(), 6100001);
        Assert.Equal(WithoutTiming(first), WithoutTiming(second));
        var geometry = first["Geometries"]![0]!;
        int events = geometry["Direct"]![0]!["Events"]!.GetValue<int>();
        Assert.True(events > 1000);
        foreach (var readout in geometry["Readouts"]!.AsArray())
            foreach (var trigger in readout!["Triggers"]!.AsArray())
                if (trigger!["Energies"] is JsonArray energies)
                    Assert.Equal(events, energies[0]!["Localisation"]!["Events"]!.GetValue<int>());
        var independentAnd = geometry["Readouts"]![1]!["Triggers"]![1]!;
        Assert.NotNull(independentAnd["NotApplicable"]);
        var ideal = geometry["Readouts"]![0]!["Triggers"]![0]!;
        Assert.True(ideal["Calibration"]!["Succeeded"]!.GetValue<bool>());
        Assert.NotNull(ideal["Energies"]![0]!["Rate"]);
    }

    [Fact]
    public void FailedCalibration_ReportsTheReasonAndNoNumbers()
    {
        var request = Tiny();
        request.Readouts = [request.Readouts[0]];
        request.Triggers = [request.Triggers[0]];
        request.Base.Digitizer.NoiseKeV = 500;                                   // σ_X ≈ 2·500/662 ≫ the spot spacing 1/6
        var result = ReadoutStudy.Run(Rigs.Lab(), request, 6100004);
        var trigger = result["Geometries"]![0]!["Readouts"]![0]!["Triggers"]![0]!;
        Assert.False(trigger["Calibration"]!["Succeeded"]!.GetValue<bool>());
        Assert.Contains("flood", trigger["Calibration"]!["Failure"]!.GetValue<string>());
        Assert.Null(trigger["Energies"]);
    }

    [Fact]
    public void InvalidRequests_AreRefused()
    {
        var request = Tiny();
        request.Readouts = [new ReadoutStudyVariant { Name = "direct", Mode = ReadoutMode.DirectCrystal }];
        Assert.Throws<ArgumentException>(() => ReadoutStudy.Validate(request));
        request = Tiny();
        request.EnergiesKeV = [];
        Assert.Throws<ArgumentException>(() => ReadoutStudy.Validate(request));
    }
}
