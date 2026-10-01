using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Tests.Harness;
using Xunit.Abstractions;

namespace Gcam.Tests;

public sealed class ListModeBackgroundTests(ITestOutputHelper output)
{
    private static DetectedEvent[] CollectToTime(ListModeSource source, double horizon)
    {
        var events = new List<DetectedEvent>();
        while (true)
            if (source.Advance() is { } ev)
            {
                if (ev.ArrivalTimeS > horizon) return events.ToArray();
                events.Add(ev);
            }
    }

    [Fact]
    public void ZeroBsr_PreservesSourceStreamBitForBit()
    {
        var config = Rigs.Lab(seed: 623);
        var zero = config.Clone(); zero.Background = new BackgroundConfig();
        using var clean = new ListModeSource(config);
        using var disabled = new ListModeSource(zero);
        Assert.Equal(CollectToTime(clean, 20), CollectToTime(disabled, 20));
        Assert.Equal(clean.HistoriesEmitted, disabled.HistoriesEmitted);
        Assert.Equal(clean.DetectedWeight, disabled.DetectedWeight);
    }

    [Fact]
    public void BsrOne_DoublesRate_PreservesEverySourceEvent_AndMatchesBackgroundModel()
    {
        var config = Rigs.Lab(1_000_000, 623);
        var noisy = config.Clone(); noisy.Background = new BackgroundConfig { BackgroundToSignalRatio = 1, EnergyKeV = 200 };
        using var clean = new ListModeSource(config);
        using var mixed = new ListModeSource(noisy);
        var source = CollectToTime(clean, 1000);
        var combined = CollectToTime(mixed, 1000);
        var sourceSet = source.ToHashSet();
        var combinedSet = combined.ToHashSet();
        Assert.All(source, e => Assert.Contains(e, combinedSet));
        Assert.Equal(combined.Length, combined.Distinct().Count());
        Assert.True(combined.Zip(combined.Skip(1)).All(p => p.First.ArrivalTimeS < p.Second.ArrivalTimeS));
        var background = combined.Where(e => !sourceSet.Contains(e)).ToArray();
        // Ignore the first 100 s while the running source-efficiency estimate settles.
        int signalTail = source.Count(e => e.ArrivalTimeS >= 100);
        int bgTail = background.Count(e => e.ArrivalTimeS >= 100);
        double ratio = (signalTail + bgTail) / (double)signalTail;
        double sigma = Math.Sqrt(bgTail + signalTail) / signalTail;
        Stat.Within(ratio, 2, sigma, 4, "BSR=1 total/source rate");
        output.WriteLine($"BSR1: signal={source.Length}, background={background.Length}, mature rate ratio={ratio:F6}, expected=2, 4σ={4 * sigma:F6}; every source record retained exactly");
        var profile = Background.SideLeakProfile(config.Detector.PixelsX, config.Detector.PixelsY, 0);
        int pixels = profile.Length;
        double maxZ = 0;
        for (int i = 0; i < pixels; i++)
        {
            double observed = background.Count(e => e.PixelY * config.Detector.PixelsX + e.PixelX == i) / (double)background.Length;
            double p = profile[i] / pixels;
            double error = Stat.BinomialSigma(p, background.Length);
            Stat.Within(observed, p, error, 5, $"background pixel {i}");
            maxZ = Math.Max(maxZ, Math.Abs(observed - p) / error);
        }
        var reference = noisy.Clone(); reference.Seed = 987;
        var spectrum = EventStreamStudy.BackgroundDepositSpectrum(reference, 200, 100_000, isotropic: true);
        double maxEnergyZ = 0;
        for (int bin = 0; bin < 8; bin++)
        {
            int Bin(double energy) => Math.Min(7, (int)(energy / 200 * 8));
            int referenceHits = spectrum.Count(e => Bin(e) == bin);
            int observedHits = background.Count(e => Bin(e.DepositKeV) == bin);
            double p = referenceHits / (double)spectrum.Length;
            double observed = observedHits / (double)background.Length;
            double pooled = (referenceHits + observedHits) / (double)(spectrum.Length + background.Length);
            if (pooled is 0 or 1)
            {
                // Both samples have no variance in this bin; agreement is exact.
                Assert.Equal(p, observed);
                continue;
            }
            // Under the equal-distribution null, estimate p from BOTH finite samples. A zero
            // reference count alone is not zero probability. The acceptance remains 4σ.
            double error = Math.Sqrt(pooled * (1 - pooled) * (1.0 / spectrum.Length + 1.0 / background.Length));
            Stat.Within(observed, p, error, 4, $"background deposit bin {bin}");
            maxEnergyZ = Math.Max(maxEnergyZ, Math.Abs(observed - p) / error);
        }
        output.WriteLine($"uniform pedestal max |z|={maxZ:F4} (5σ, {pixels} pixels); crystal response max |z|={maxEnergyZ:F4} (4σ, eight bins); reference=100000 fresh diffuse deposits");
    }

    [Fact]
    public void Background_SeedAndCancellationAreDeterministic()
    {
        var config = Rigs.Lab(); config.Background = new BackgroundConfig { BackgroundToSignalRatio = 1 };
        using var a = new ListModeSource(config);
        using var b = new ListModeSource(config);
        Assert.Equal(CollectToTime(a, 10), CollectToTime(b, 10));
        Assert.Throws<OperationCanceledException>(() => a.Advance(new CancellationToken(true)));
    }
}
