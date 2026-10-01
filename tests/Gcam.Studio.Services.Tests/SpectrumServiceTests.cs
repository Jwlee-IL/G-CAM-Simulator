using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Services.Tests;

public sealed class SpectrumServiceTests(ITestOutputHelper output)
{
    private static readonly SpectrumLine[] CsLines = Isotopes.Get("Cs-137").Lines
        .Select(l => new SpectrumLine("Cs-137", l.EnergyKeV)).ToArray();

    private static DetectedEvent[] Acquire(int count, params SceneSource[] scene)
    {
        var config = SceneConfigBuilder.Build(scene, new OpticsSettings(), 1, seed: 12345);
        using var source = new ListModeSource(config);
        var events = new List<DetectedEvent>(count);
        while (events.Count < count)
            if (source.Advance(CancellationToken.None) is { } ev) events.Add(ev);
        return events.ToArray();
    }

    [Fact]
    public async Task CsAcquisition_PhotopeakBinAndFwhmMatchChain()
    {
        var events = Acquire(100_000, new SceneSource());
        var view = await new SpectrumService().ProcessAsync(Guid.NewGuid(), events, CsLines, new());
        double width = view.CentresKeV[1] - view.CentresKeV[0];
        int peak = Enumerable.Range(0, view.Counts.Length)
            .Where(i => view.CentresKeV[i] > 478)
            .MaxBy(i => view.Counts[i]);
        int expectedBin = (int)(661.7 / width);
        Assert.Equal(expectedBin, peak);
        // Real Ba K X-rays are almost fully absorbed and form a narrower, taller peak.
        // Keep that global maximum rather than suppressing low-energy engine events.
        int globalPeak = Array.IndexOf(view.Counts, view.Counts.Max());
        var baBand = view.Bands.Single(b => b.Lines.Count == 2);
        Assert.InRange(view.CentresKeV[globalPeak], baBand.LoKeV, baBand.HiKeV);
        Assert.True(view.Counts[globalPeak] > view.Counts[peak]);
        output.WriteLine($"Ba K global peak: bin={globalPeak}, centre={view.CentresKeV[globalPeak]:F4} keV, band={baBand.LoKeV:F4}–{baBand.HiKeV:F4} keV");
        double half = view.Counts[peak] / 2;
        int left = peak, right = peak;
        while (left > 0 && view.Counts[left] >= half) left--;
        while (right + 1 < view.Counts.Length && view.Counts[right] >= half) right++;
        double lo = view.CentresKeV[left] + width * (half - view.Counts[left]) / (view.Counts[left + 1] - view.Counts[left]);
        double hi = view.CentresKeV[right - 1] + width * (view.Counts[right - 1] - half) / (view.Counts[right - 1] - view.Counts[right]);
        double measured = hi - lo;
        var model = new FrontEndModel(FrontEndParts.Default.BuildConfig());
        double expected = 661.7 * model.FwhmFraction(661.7);
        int n = events.Count(e => Math.Abs(e.DepositKeV - 661.7) < 1e-6);
        // Two bins cover crossing quantization / histogram half-height noise; 5σ width sampling
        // uses the Gaussian width estimator's σ = FWHM / sqrt(2(N−1)). No fitted extra tail.
        double tolerance = 2 * width + 5 * expected / Math.Sqrt(2 * (n - 1));
        output.WriteLine($"Cs MC: N={events.Length}, full-energy N={n}, peak bin={peak}, expected={expectedBin}, bin width={width:F4} keV; FWHM={measured:F4}, chain={expected:F4} keV, tolerance=2 bins + 5σ={tolerance:F4} keV; R662={view.Resolution662:P4}");
        Assert.InRange(Math.Abs(measured - expected), 0, tolerance);
        Assert.Equal(events.Length, view.TotalCounts);
        Assert.Equal(view.TotalCounts, (long)view.Counts.Sum() + view.OverflowCounts);
    }

    [Fact]
    public async Task HighRate_PileUpLosesPulsesAndMovesCountsAbovePhotopeak()
    {
        var events = Acquire(100_000, new SceneSource { ActivityUCi = 1e6 });
        var service = new SpectrumService();
        var id = Guid.NewGuid();
        var singles = await service.ProcessAsync(id, events, CsLines, new());
        var piled = await service.ProcessAsync(id, events, CsLines, new(PileUp: true));
        long high = (long)piled.Counts.Where((_, i) => piled.CentresKeV[i] > 1.2 * 661.7).Sum();
        long singleHigh = (long)singles.Counts.Where((_, i) => singles.CentresKeV[i] > 1.2 * 661.7).Sum();
        output.WriteLine($"Pile-up at MC rate: {singles.TotalCounts} → {piled.TotalCounts} pulses; above 794.04 keV {singleHigh} → {high}; overflow={piled.OverflowCounts}; resolving={piled.ResolvingTimeS * 1e9:F0} ns");
        Assert.True(piled.TotalCounts < singles.TotalCounts);
        Assert.True(high > singleHigh);
        // Independently replay the existing paralyzable engine operation at a fine time grid.
        const double fs = 1e12; // 1 ps rounding, negligible relative to the 730 ns window.
        var reference = EventStreamStudy.ApplyPileUp(events.Select(e => new StreamEvent(
            (long)Math.Round(e.ArrivalTimeS * fs), e.DepositKeV)).ToArray(), piled.ResolvingTimeS * fs);
        Assert.Equal(reference.Count, piled.TotalCounts);
    }

    [Fact]
    public async Task MixedCsCo_HasFourBandsAndUnionCountsEachBinOnce()
    {
        var scene = new[] { new SceneSource(), new SceneSource { Isotope = "Co-60" } };
        var events = Acquire(20_000, scene);
        var lines = scene.SelectMany(s => Isotopes.Get(s.Isotope).Lines.Select(l => new SpectrumLine(s.Isotope, l.EnergyKeV))).ToArray();
        var view = await new SpectrumService().ProcessAsync(Guid.NewGuid(), events, lines, new());
        Assert.Equal(4, view.Bands.Count);
        Assert.Equal("32.1 + 36.4 keV", view.Bands[0].Label);
        Assert.Equal(new[] { "661.7 keV", "1173.2 keV", "1332.5 keV" }, view.Bands.Skip(1).Select(b => b.Label));
        double union = view.Counts.Where((_, i) => view.Bands.Any(b => view.CentresKeV[i] >= b.LoKeV && view.CentresKeV[i] <= b.HiKeV)).Sum();
        Assert.Equal(union / view.TotalCounts, view.InWindowShare);
        Assert.True(view.Bands.Sum(b => b.Counts) >= union);
        output.WriteLine($"Cs + Co: {string.Join(", ", view.Bands.Select(b => b.Label))}; union share={view.InWindowShare:P4}");
    }

    [Theory]
    [InlineData(32.1, 36.4, 1)]
    [InlineData(1173.2, 1332.5, 2)]
    public void Merge_UsesResolutionRatherThanWindowOverlap(double first, double second, int bands)
    {
        // Inject 10% constant FWHM for the resolved pair: separation 159.3 keV
        // exceeds FWHM(mean)=125.285 keV, while N=1.5 windows overlap.
        // This tests grouping, not a claim about the default physical chain.
        var model = bands == 2
            ? new FrontEndModel(new FrontEndConfig { LightYieldPhPerKeV = 0, IntrinsicResolutionFwhm = 0.10, DarkCountRateHz = 0 })
            : new FrontEndModel(FrontEndParts.Default.BuildConfig());
        var result = SpectrumService.BuildBands([new("test", first), new("test", second)], 1.5, model);
        Assert.Equal(bands, result.Count);
        if (bands == 2) Assert.True(result[0].HiKeV > result[1].LoKeV);
    }

    [Fact]
    public void Merge_SingleLineGivesOneBand()
        => Assert.Single(SpectrumService.BuildBands([new("Cs-137", 661.7)], 1.5,
            new FrontEndModel(FrontEndParts.Default.BuildConfig())));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SeedAndSnapshotPartition_AreDeterministic_ToggleReplaysExactly(bool pileUp)
    {
        var events = Acquire(10_000, new SceneSource { ActivityUCi = 1e6 });
        var service = new SpectrumService();
        var id = Guid.NewGuid();
        var settings = new SpectrumSettings(PileUp: pileUp);
        var first = await service.ProcessAsync(id, events.Take(503).ToArray(), CsLines, settings);
        var incremental = await service.ProcessAsync(id, events, CsLines, settings);
        var batch = await new SpectrumService().ProcessAsync(Guid.NewGuid(), events, CsLines, settings);
        Assert.Equal(batch.Counts, incremental.Counts);
        Assert.Equal(batch.OverflowCounts, incremental.OverflowCounts);
        await service.ProcessAsync(id, events, CsLines, new(PileUp: !pileUp));
        var replay = await service.ProcessAsync(id, events, CsLines, settings);
        Assert.Equal(batch.Counts, replay.Counts);
        if (pileUp) Assert.InRange(first.TotalCounts, 1, 503);
        else Assert.Equal(503, first.TotalCounts);
    }

    [Fact]
    public async Task Processing_MeasuresFullAndIncrementalWorkAt100000Events()
    {
        var events = Acquire(100_000, new SceneSource());
        // Warm JIT before recording worker CPU time (excludes Task scheduling / gate waiting).
        await new SpectrumService().ProcessAsync(Guid.NewGuid(), events.Take(1000).ToArray(), CsLines, new());
        var full = new List<double>();
        var incremental = new List<double>();
        var toggled = new List<double>();
        for (int run = 0; run < 5; run++)
        {
            var service = new SpectrumService();
            var id = Guid.NewGuid();
            full.Add((await service.ProcessAsync(id, events, CsLines, new())).ProcessingTime.TotalMilliseconds);
            // Start an independent cache with a known prefix, then add the last 1000 events.
            id = Guid.NewGuid();
            await service.ProcessAsync(id, events.Take(99_000).ToArray(), CsLines, new());
            incremental.Add((await service.ProcessAsync(id, events, CsLines, new())).ProcessingTime.TotalMilliseconds);
            toggled.Add((await service.ProcessAsync(id, events, CsLines, new(PileUp: true))).ProcessingTime.TotalMilliseconds);
        }
        output.WriteLine($"Worker processing ms (5 warmed runs): smear+bin 100000 [{string.Join(", ", full.Select(t => t.ToString("F3")))}]; increment 1000 [{string.Join(", ", incremental.Select(t => t.ToString("F3")))}]; pile-up toggle 100000 [{string.Join(", ", toggled.Select(t => t.ToString("F3")))}]. All run in service Task.Run.");
    }
}
