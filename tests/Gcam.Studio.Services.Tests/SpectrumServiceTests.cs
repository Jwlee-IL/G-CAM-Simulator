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
        Assert.Equal(view.Counts.Length + 1, view.BinEdgesKeV.Length);
        Assert.Equal(0, view.BinEdgesKeV[0]);
        double width = view.CentresKeV[1] - view.CentresKeV[0];
        Assert.Equal(SpectrumService.AxisMaximumKeV, view.BinEdgesKeV[^1], 8); // fixed axis (AB-16), not the line list
        Assert.Equal(SpectrumService.BinWidthKeV, width, 8);
        for (int i = 0; i < view.Counts.Length; i++)
            Assert.Equal(view.CentresKeV[i], (view.BinEdgesKeV[i] + view.BinEdgesKeV[i + 1]) / 2, 8);
        int peak = Enumerable.Range(0, view.Counts.Length)
            .Where(i => view.CentresKeV[i] > 478)
            .MaxBy(i => view.Counts[i]);
        int expectedBin = (int)(661.7 / width);
        Assert.Equal(expectedBin, peak);
        // Absorber attenuation is verified by DetectorRealismTests, not by assuming a global peak ordering.
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

    [Fact]
    public async Task Axis_IsFixedAt2000keV_ForAnyLinesFieldOrPileUp_AndOverflowKeepsEveryPulse()
    {
        // AB-16: deposits on and far above the axis end (a Co-60 2505 keV cascade sum, a 3 MeV pulse) are overflow,
        // never dropped; the axis and the 662 band do not follow the line list or the field's highest energy.
        var events = new DetectedEvent[] { new(0, 0, 661.7, 0), new(0, 0, 1173.2, 1), new(0, 0, 1332.5, 2),
            new(0, 0, 2505.7, 3), new(0, 0, 3000, 4), new(0, 0, 661.7, 5) };
        var co = Isotopes.Get("Co-60").Lines.Select(l => new SpectrumLine("Co-60", l.EnergyKeV)).ToArray();
        var views = new List<SpectrumView>();
        foreach (var lines in new[] { CsLines, co, CsLines.Concat(co).ToArray() })
        foreach (double? incident in new double?[] { null, 3960.9 })
        foreach (bool pileUp in new[] { false, true })
            views.Add(await new SpectrumService().ProcessAsync(Guid.NewGuid(), events, lines,
                new(PileUp: pileUp) { IncidentMaximumEnergyKeV = incident }));
        foreach (var view in views)
        {
            Assert.Equal(SpectrumService.BinCount, view.Counts.Length);
            Assert.Equal(1000, SpectrumService.BinCount);
            Assert.Equal(0, view.BinEdgesKeV[0]); Assert.Equal(2000, view.BinEdgesKeV[^1], 8);
            Assert.Equal(2, view.BinEdgesKeV[1] - view.BinEdgesKeV[0], 8);
            Assert.Equal(6, view.TotalCounts); // the arrivals are 1 s apart: no pile-up merges here
            Assert.Equal(2, view.OverflowCounts);
            Assert.Equal(view.TotalCounts, (long)view.Counts.Sum() + view.OverflowCounts);
        }
        // Same lines, with and without the field's highest energy: identical histogram and 662 band.
        var cs = await new SpectrumService().ProcessAsync(Guid.NewGuid(), events, CsLines, new());
        var csField = await new SpectrumService().ProcessAsync(Guid.NewGuid(), events, CsLines, new() { IncidentMaximumEnergyKeV = 3960.9 });
        Assert.Equal(cs.Counts, csField.Counts);
        Assert.Equal(cs.Bands.Select(b => b.Counts), csField.Bands.Select(b => b.Counts));
        Assert.Equal(2, cs.Bands.Single(b => b.Lines.Any(l => l.EnergyKeV == 661.657 || Math.Abs(l.EnergyKeV - 661.7) < 0.1)).Counts);
    }
}
