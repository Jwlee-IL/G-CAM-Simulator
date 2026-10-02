using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Tests.Harness;
using Xunit.Abstractions;

namespace Gcam.Tests;

/// <summary>Per-decay list-mode emission of cascade isotopes (TODO-14): one history per Co-60 decay, both gammas
/// transported, the detected ones summed into ONE event. The biased decay estimator (one gamma aimed, weight
/// n·w_k/(n̄·H)) must reproduce analog 4π decays.</summary>
public sealed class CascadeEmissionTests(ITestOutputHelper output)
{
    /// <summary>A large-solid-angle head (48 mm face, Co-60 at 30 mm, thin 0.5 mm mask at 10 mm) where analog 4π decays
    /// give thousands of coincidences cheaply — the estimator algebra does not depend on the geometry.</summary>
    private static SimulationConfig Near(bool biased, int seed)
    {
        var config = Rigs.Lab(1, seed);
        config.Detector.PixelsX = config.Detector.PixelsY = 24;
        config.Detector.PixelPitchMm = 2;
        config.Geometry.MaskDetectorDistanceMm = 10;
        config.Mask.ThicknessMm = 0.5;
        var co60 = Isotopes.Get("Co-60");
        var source = new SourceConfig
        {
            Isotope = "Co-60", Position = [0, 0, 30], ActivityBq = 1e5, DirectionalBiasing = biased,
            EnergyKeV = co60.Lines[0].EnergyKeV, BranchingRatio = co60.Lines[0].Intensity,
            Lines = co60.Lines.Select(l => new EmissionLine { EnergyKeV = l.EnergyKeV, Intensity = l.Intensity }).ToArray(),
        };
        config.Source = source;
        config.Sources = [source];
        return config;
    }

    private static List<DetectedEvent> Run(ListModeSource source, long histories)
    {
        var events = new List<DetectedEvent>();
        for (long i = 0; i < histories; i++)
            if (source.Advance() is { } e) events.Add(e);
        return events;
    }

    [Fact]
    public void BiasedDecays_ReproduceAnalogDecays_DetectedAndCoincident()
    {
        using var analog = new ListModeSource(Near(false, 31));
        using var biased = new ListModeSource(Near(true, 32));
        const long nA = 1_500_000, nB = 400_000;
        Run(analog, nA);
        Run(biased, nB);
        double nbar = Isotopes.Get("Co-60").Lines.Sum(l => l.Intensity);
        // Per history E[ω·f] = P_decay(f)/n̄ in both modes. Analog: ω = 1/n̄, so a binomial count. Biased: ω² ≤ bound·ω
        // bounds the variance by bound·mean (the same bound the existing list-mode tests use).
        foreach (var (name, a, b) in new[]
                 {
                     ("detected", analog.DetectedWeight, biased.DetectedWeight),
                     ("coincident", analog.CoincidentWeight, biased.CoincidentWeight),
                 })
        {
            double pA = a * nbar / nA, meanA = a / nA, meanB = b / nB;
            double sA = Stat.BinomialSigma(pA, nA) / nbar, sB = Math.Sqrt(biased.WeightBound * meanB / nB);
            Stat.Within(meanB, meanA, Math.Sqrt(sA * sA + sB * sB), 4, $"{name} per history, biased vs analog");
            output.WriteLine($"{name}: P per decay analog {pA:E4}, biased {meanB * nbar:E4}; z = {(meanB - meanA) / Math.Sqrt(sA * sA + sB * sB):F2}");
        }
        Assert.True(analog.CoincidentHistories > 1000, $"{analog.CoincidentHistories} analog coincidences");
        Assert.Equal(1.0, analog.WeightBound);
        // The detected rate is activity × P(≥1 detected per decay) in both modes.
        double rateSigma = 1e5 * nbar * Math.Sqrt(Math.Pow(Stat.BinomialSigma(analog.DetectedWeight * nbar / nA, nA) / nbar, 2)
            + biased.WeightBound * biased.DetectedWeight / nB / nB);
        Stat.Within(biased.SourceRateCps, analog.SourceRateCps, rateSigma, 4, "detected rate");
    }

    [Fact]
    public void OneEventPerDecay_SumsTheGammas_WithOneArrivalTime()
    {
        using var source = new ListModeSource(Near(true, 33));
        var events = Run(source, 300_000);
        const double sum = 1173.2 + 1332.5;
        // Deposits never exceed the full cascade energy; summed events exist (no single gamma deposits above 1332.5 keV)
        // and fully absorbed decays land exactly on the 2505.7 keV sum.
        Assert.All(events, e => Assert.True(e.DepositKeV <= sum + 1e-9));
        int above = events.Count(e => e.DepositKeV > 1332.5 + 1e-6);
        int fullSum = events.Count(e => Math.Abs(e.DepositKeV - sum) < 1e-6);
        Assert.True(above > 0 && fullSum > 0, $"summed events: {above}, of which full sum {fullSum}");
        Assert.True(source.CoincidentHistories >= above);
        Assert.True(events.Zip(events.Skip(1)).All(p => p.Second.ArrivalTimeS > p.First.ArrivalTimeS));
        output.WriteLine($"{events.Count} events, {above} above 1332.5 keV, {fullSum} at the 2505.7 keV sum; coincident decays {source.CoincidentHistories}, acceptance {source.Acceptance:P2}");
    }

    [Fact]
    public void CascadePath_OnlyForTheSchemesOwnLines()
    {
        // A config that names Co-60 but asks for one line keeps that single-line emission (no decay grouping).
        var config = Near(true, 34);
        config.Source.Lines = [new EmissionLine { EnergyKeV = 1173.2, Intensity = 0.999 }];
        config.Sources = [config.Source];
        using var source = new ListModeSource(config);
        var events = Run(source, 200_000);
        Assert.Equal(0, source.CoincidentHistories);
        Assert.All(events, e => Assert.True(e.DepositKeV <= 1173.2 + 1e-9));
    }
}
