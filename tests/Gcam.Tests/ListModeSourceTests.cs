using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Tests.Harness;
using Xunit.Abstractions;

namespace Gcam.Tests;

public sealed class ListModeSourceTests(ITestOutputHelper output)
{
    private static DetectedEvent[] Collect(ListModeSource source, int count)
    {
        var events = new List<DetectedEvent>(count);
        while (events.Count < count)
            if (source.Advance() is { } item) events.Add(item);
        return events.ToArray();
    }

    [Fact]
    public void PixelHistogram_MatchesIndependentWeightedComptonFlood()
    {
        var config = Rigs.Lab(1_000_000, 321);
        using var source = new ListModeSource(config);
        var events = Collect(source, 100_000);
        var reference = config.Clone();
        reference.Seed = 456;
        var weighted = new SimulationRunner(new ComptonFactory(ComptonStrategy.Argmax, 661.7, 1)).Run(reference);
        var pixels = events.GroupBy(e => (e.PixelX, e.PixelY)).ToDictionary(g => g.Key, g => g.Count());
        double effectiveN = weighted.DetectedWeight / source.WeightBound;
        double maxZ = 0;
        for (int y = 0; y < config.Detector.PixelsY; y++)
            for (int x = 0; x < config.Detector.PixelsX; x++)
            {
                double p = weighted.DetectorImage[x, y] / weighted.DetectedWeight;
                double observed = pixels.GetValueOrDefault((x, y)) / (double)events.Length;
                // w^2 <= w_max*w bounds the weighted-reference variance. 5σ covers 144 comparisons.
                double sigma = Math.Sqrt(Stat.BinomialSigma(p, events.Length) * Stat.BinomialSigma(p, events.Length)
                    + p * (1 - p) / effectiveN);
                Stat.Within(observed, p, sigma, 5, $"pixel ({x},{y})");
                maxZ = Math.Max(maxZ, Math.Abs(observed - p) / sigma);
            }
        output.WriteLine($"144 pixel fractions: max |z|={maxZ:F3}, limit 5σ; accepted={events.Length}");
    }

    [Fact]
    public void Deposits_MatchIndependentWeightResampledEventStream()
    {
        var config = Rigs.Lab(1_000_000, 123);
        using var source = new ListModeSource(config);
        var events = Collect(source, 100_000);
        var reference = config.Clone();
        reference.Seed = 987;
        var stream = new EventStreamStudy().Generate(reference, 1000, 125e6, 150_000);
        double maxZ = 0;
        for (int bin = 0; bin < 8; bin++)
        {
            int Bin(double energy) => Math.Min(7, (int)(energy / 661.7 * 8));
            double p = stream.Count(e => Bin(e.EnergyKeV) == bin) / (double)stream.Count;
            double observed = events.Count(e => Bin(e.DepositKeV) == bin) / (double)events.Length;
            // Systematic resampling shares ancestors; n/2 conservatively bounds its near-unity-weight pool.
            double sigma = Math.Sqrt(p * (1 - p) * (1.0 / events.Length + 2.0 / stream.Count));
            Stat.Within(observed, p, sigma, 4, $"deposit bin {bin}");
            maxZ = Math.Max(maxZ, Math.Abs(observed - p) / sigma);
        }
        output.WriteLine($"8 deposit bins: max |z|={maxZ:F3}, limit 4σ; reference={stream.Count}");
    }

    [Fact]
    public void RateAndGaps_MatchPhysicalEfficiencyAndExponentialLaw()
    {
        var config = Rigs.Lab(1_000_000, 623);
        using var source = new ListModeSource(config);
        var events = Collect(source, 100_000);
        var reference = config.Clone();
        reference.Seed = 932;
        var weighted = new SimulationRunner(new ComptonFactory(ComptonStrategy.Argmax, 661.7, 1)).Run(reference);
        double rate = config.Source.ActivityBq * config.Source.BranchingRatio * weighted.DetectedWeight / weighted.PhotonsEmitted;
        // Exclude startup estimator transients from the rate measurement (all events remain in the list).
        var tail = events.Skip(10_000).ToArray();
        double duration = tail[^1].ArrivalTimeS - tail[0].ArrivalTimeS;
        double observed = (tail.Length - 1) / duration;
        double relativeReferenceSigma = Math.Sqrt(source.WeightBound / weighted.DetectedWeight);
        double sigma = rate * Math.Sqrt(1.0 / (tail.Length - 1) + relativeReferenceSigma * relativeReferenceSigma);
        Stat.Within(observed, rate, sigma, 4, "physical rate");
        // Normalize by the actual rate used for each gap; unit exponentials have mean 1, variance 1.
        using var normalizedSource = new ListModeSource(config);
        var gaps = new List<double>();
        double previous = 0;
        for (int i = 0; i < 50_000;)
            if (normalizedSource.Advance() is { } e)
            {
                gaps.Add((e.ArrivalTimeS - previous) * normalizedSource.RateCps);
                previous = e.ArrivalTimeS;
                i++;
            }
        var moments = Stat.Moments(gaps);
        Stat.Within(moments.Mean, 1, 1 / Math.Sqrt(gaps.Count), 4, "exponential mean");
        // For Exp(1), fourth central moment is 9: sample-variance SE tends to sqrt(8/n).
        Stat.Within(moments.Variance, 1, Math.Sqrt(8.0 / gaps.Count), 4, "exponential variance");
        double cdf = gaps.Count(g => g <= 1) / (double)gaps.Count;
        Stat.Within(cdf, 1 - Math.Exp(-1), Stat.BinomialSigma(1 - Math.Exp(-1), gaps.Count), 4, "exponential CDF(1)");
        Assert.Equal(events.Length, events.Distinct().Count());
        Assert.True(events.Zip(events.Skip(1)).All(pair => pair.First.ArrivalTimeS < pair.Second.ArrivalTimeS));
        output.WriteLine($"rate={observed:F3} cps, reference={rate:F3}, 4σ tolerance={4 * sigma:F3}; normalized gap mean={moments.Mean:F5}, variance={moments.Variance:F5}, CDF(1)={cdf:F5}; no duplicates");
    }

    [Fact]
    public void MixedDistances_RejectionPreservesWeightedFloodAndTotalEmissionRate()
    {
        var config = Rigs.Lab(2_000_000, 731);
        config.Source.Position = [2, 0, 160];
        var far = config.Clone();
        far.Source.Position = [20, 0, 1000];
        far.Source.ActivityBq *= 40;
        config.Sources = [config.Source, far.Source];
        using var source = new ListModeSource(config);
        var events = Collect(source, 40_000);
        var reference = config.Clone();
        reference.Seed = 845;
        var weighted = new SimulationRunner(new ComptonFactory(ComptonStrategy.Argmax, 661.7, 1)).Run(reference);
        double effectiveN = weighted.DetectedWeight / source.WeightBound;
        var histogram = events.GroupBy(e => (e.PixelX, e.PixelY)).ToDictionary(g => g.Key, g => g.Count());
        double maxZ = 0;
        for (int y = 0; y < config.Detector.PixelsY; y++)
            for (int x = 0; x < config.Detector.PixelsX; x++)
            {
                double p = weighted.DetectorImage[x, y] / weighted.DetectedWeight;
                double observed = histogram.GetValueOrDefault((x, y)) / (double)events.Length;
                double sigma = Math.Sqrt(p * (1 - p) * (1.0 / events.Length + 1.0 / effectiveN));
                Stat.Within(observed, p, sigma, 5, $"mixed pixel ({x},{y})");
                maxZ = Math.Max(maxZ, Math.Abs(observed - p) / sigma);
            }
        double emission = config.Sources.Sum(s => s.ActivityBq * s.BranchingRatio);
        double efficiency = weighted.DetectedWeight / weighted.PhotonsEmitted;
        double rateSigma = emission * Math.Sqrt(source.WeightBound * efficiency *
            (1.0 / weighted.PhotonsEmitted + 1.0 / source.HistoriesEmitted));
        Stat.Within(source.RateCps, emission * efficiency, rateSigma, 4, "mixed physical rate");
        // Unlike the centred lab rig, this stresses rejection: far proposals have ~1/39 of the near weight.
        Assert.InRange(source.Acceptance, 0.0, 0.1);
        output.WriteLine($"mixed 160 mm / 1000 mm (activity 1:40): acceptance={source.Acceptance:P4}, max pixel |z|={maxZ:F3} < 5σ; rate={source.RateCps:F3} vs {emission * efficiency:F3} cps, 4σ tolerance={4 * rateSigma:F3}");
    }

    [Fact]
    public void PixelSink_PreservesLegacyEnergySinkAndTransport_WithOpticalCrosstalk()
    {
        var config = Rigs.Lab(20_000).Clone();
        config.Detector.OpticalCrosstalkFraction = 0.2;
        var factory = new DefaultSimulationFactory();
        var rng = factory.CreateRandom(config);
        var mask = factory.CreateMask(config);
        var legacy = new List<(double Deposit, double Weight)>();
        var richer = new List<(double Deposit, double Weight)>();
        var pixels = new List<(int X, int Y, double Deposit, double Weight)>();
        var d = config.Detector;
        ComptonCrystalDetector Detector(List<(double, double)> sink, bool rich) => new(
            d.PixelsX, d.PixelsY, d.PixelPitchMm, 661.7, 1, ComptonStrategy.Argmax,
            new DefaultRandom(config.Seed + 777), opticalCrosstalk: d.OpticalCrosstalkFraction,
            eventSink: (deposit, weight) => sink.Add((deposit, weight)),
            pixelEventSink: rich ? (x, y, deposit, weight) => pixels.Add((x, y, deposit, weight)) : null);
        var before = Detector(legacy, false);
        var after = Detector(richer, true);
        foreach (var photon in factory.CreateSource(config).Emit(rng, config.PhotonCount))
            if (mask.Transmit(photon.Ray, photon.EnergyKeV, rng))
            {
                Assert.Equal(before.Score(photon), after.Score(photon));
            }
        Assert.NotEmpty(legacy);
        Assert.Equal(legacy, richer);
        Assert.Equal(legacy, pixels.Select(p => (p.Deposit, p.Weight)).ToList());
        Assert.Equal(before.Readout().Raw.ToArray(), after.Readout().Raw.ToArray());
        Assert.All(pixels, p => { Assert.InRange(p.X, 0, d.PixelsX - 1); Assert.InRange(p.Y, 0, d.PixelsY - 1); });
    }

    [Fact]
    public void SeedAndCancellation_AreDeterministicAndPrompt()
    {
        using var a = new ListModeSource(Rigs.Lab());
        using var b = new ListModeSource(Rigs.Lab());
        Assert.Equal(Collect(a, 1000), Collect(b, 1000));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => a.Advance(cancellation.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Performance_RecordAcceptedThroughputAcceptanceAndDecode(bool handheld)
    {
        var config = handheld ? Rigs.Handheld() : Rigs.Lab();
        if (handheld) config.Geometry.SourceMaskDistanceMm = 1000 - config.Geometry.MaskDetectorDistanceMm;
        using (var warmup = new ListModeSource(config)) Collect(warmup, 2000);
        using var source = new ListModeSource(config);
        var watch = Stopwatch.StartNew();
        var events = Collect(source, 100_000);
        watch.Stop();
        var image = new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY);
        foreach (var e in events) image.Add(e.PixelX, e.PixelY, 1);
        var decoder = new DefaultSimulationFactory().CreateDecoder(config)!;
        decoder.Decode(image);
        var times = new List<double>();
        for (int i = 0; i < 20; i++)
        {
            var decode = Stopwatch.StartNew();
            decoder.Decode(image);
            times.Add(decode.Elapsed.TotalMilliseconds);
        }
        output.WriteLine($"{(handheld ? "handheld Cs-137 1 m" : "samples/scenario.json")}: accepted={events.Length}, wall={watch.Elapsed.TotalSeconds:F4} s, throughput={events.Length / watch.Elapsed.TotalSeconds:F0} events/s, acceptance={source.Acceptance:P4} ({source.EventsAccepted}/{source.HistoriesDetected}), histories={source.HistoriesEmitted}, decode mean={times.Average():F3} ms, max={times.Max():F3} ms");
    }
}
