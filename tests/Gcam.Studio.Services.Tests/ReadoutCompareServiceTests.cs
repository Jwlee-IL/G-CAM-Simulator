using System.Runtime.CompilerServices;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Studio.Core.Optics;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Services.Tests;

/// <summary>Exact independent retained-input oracles, population denominators and line-specific projections.</summary>
public sealed class ReadoutCompareServiceTests(ITestOutputHelper output)
{
    internal static readonly OpticsSettings Head = OpticsPreset.All.Single(p => p.Name == "Baseline").Settings!;
    internal static ReadoutPreparation Preparation(double line = 661.7)
    {
        var offers = ReadoutOfferRecordLoader.LoadEmbedded();
        return new(Guid.NewGuid(), ReadoutPolicy.Key(Head, new()), true, null, new DetectorImage(2, 2), [], [], [],
            0, 0, 0, 0, TimeSpan.Zero, 1, 2, 1, 10, 50)
        { LineKeV = line, Plan = ReadoutPolicy.Plan(Head, new(), line, offers), CorrectionHistogram = Enumerable.Repeat(1, 144).ToArray() };
    }
    internal static AcquisitionSnapshot Fixture()
    {
        RealisedReadoutHit[] hits = [new(.1, new(), 0, 0, 600), new(.2, new(), 1, 0, 720), new(.3, new(), 2, 0, 800),
            new(.4, new(), 0, 1, 650), new(.5, new(), 1, 1, 30), new(.6, new(), 2, 1, 2000)];
        var records = new[] { (0, 1, 0, 600d), (1, 1, 2, 720d), (2, 2, 2, 800d), (3, 1, -1, double.NaN), (-1, 0, 3, 650d), (0, 1, 0, 600d) }
            .Select((a, i) => new MeasuredReadoutRecord(i, .1 + .1 * i, .1 + .1 * i, new(), new(), 0, 0, a.Item3, a.Item4, a.Item1, a.Item2, 1)).ToArray();
        var prep = Preparation(); var co = Preparation(1332.5); var flood = new DetectorImage(12, 12);
        foreach (var r in records.Where(r => r.Crystal >= 0)) flood.Add(r.Crystal % 12, r.Crystal / 12, 1);
        return new(1, 5, 6, 1, false, new(flood.ReadOnlyCopy(), -5.5, 1, null, 0, 0, null, 5, TimeSpan.Zero), [], TimeSpan.Zero, true)
        { Optics = Head, Detector = new() { ReadoutMode = ReadoutMode.FourOutputAnger }, Readout = new(prep, records, hits, 1, false)
            { Lines = [new(prep, records.Select(r => new ReadoutLineAssignment(r.Crystal, r.EnergyKeV)).ToArray(), 5, 1),
                new(co, records.Select(_ => new ReadoutLineAssignment(9, 1200)).ToArray(), 6, 0)] } };
    }
    private static CompareKey Key(AcquisitionSnapshot s, Guid? id = null) => new(id ?? Guid.NewGuid(), 1, s.Readout!.Hits.Count, s.Readout.Records.Count, s.LiveTimeS);

    [Fact]
    public async Task RetainedInputs_ExactlyMatchIndependentFloodAndDecoder_WithoutLegacyEnergyResponse()
    {
        var snapshot = Fixture(); var settings = new CompareSettings(new(), false);
        var result = await new ReadoutCompareService().ProcessAsync(Key(snapshot), snapshot, [new()], settings);
        var expected = new DetectorImage(12, 12);
        foreach (var h in snapshot.Readout!.Hits) expected.Add(h.PixelX, h.PixelY, 1);
        Assert.Equal(expected.Raw.ToArray(), result.Direct.Image.Flood.Raw.ToArray());
        var decoder = new Gcam.Simulation.DefaultSimulationFactory().CreateDecoder(SimulationService.BuildConfig([new()], Head, new()))!;
        var decoded = decoder.Decode(expected);
        Assert.Equal(decoded.Reconstruction.Raw.ToArray(), result.Direct.Image.Reconstruction!.Raw.ToArray());
        Assert.Equal(decoded.Estimate, result.Direct.Image.Estimate);
        Assert.Equal(1, result.Spectrum.Direct[300]); // exactly 600 keV, not legacy smeared
        Assert.Equal(1, result.Spectrum.DirectOverflow);
        Assert.Empty(snapshot.Events);
        Assert.Equal(56, Unsafe.SizeOf<RealisedReadoutHit>());
    }

    [Fact]
    public async Task PopulationsAndMap_UseIndependentConversionCounts_UnknownsPileupAndRepeatedDominantsStaySeparate()
    {
        var snapshot = Fixture(); var r = await new ReadoutCompareService().ProcessAsync(Key(snapshot), snapshot, [new()], new(new(), false));
        var a = r.Accounting;
        Assert.Equal((6, 6, 5, 1, 1, 1, 4, 2), (a.Histories, a.Held, a.Assigned, a.Unknown, a.MultiContributor, a.NoDominant, a.DistinctDominant, a.NeverDominant));
        Assert.Equal(3, a.Eligible); Assert.Equal(2, a.Same); Assert.Equal(2d / 3, a.SameShare);
        Assert.Equal(2, r.Assignment.Eligible[0]); Assert.Equal(1, r.Assignment.Different[1]);
        Assert.Equal(0, r.Assignment.Difference[0, 0]); Assert.Equal(1, r.Assignment.Difference[1, 0]);
        Assert.True(double.IsNaN(r.Assignment.Difference[2, 0]));
        Assert.Equal(3, a.DirectWindow); Assert.Equal(4, a.AngerWindow);
    }

    [Fact]
    public async Task WindowAndSpectrum_UseOnlyTheirCapturedLine_InclusiveEdges_NoFallback()
    {
        var s = Fixture(); var service = new ReadoutCompareService();
        var window = await service.ProcessAsync(Key(s), s, [new()], new(new(), true));
        Assert.Equal(3, window.Direct.Image.EffectiveCounts); Assert.Equal(4, window.Anger.Image.EffectiveCounts);
        Assert.Equal(661.7, window.Spectrum.CalibrationLineKeV);
        var co = await service.ProcessAsync(Key(s), s, [new()], new(new() { WindowLowKeV = 1100, WindowHighKeV = 1400 }, true));
        Assert.Equal(6, co.Anger.Image.EffectiveCounts); Assert.Equal(6, co.Spectrum.Anger[600]);
        Assert.Equal(1332.5, co.Spectrum.CalibrationLineKeV);
        var missing = await service.ProcessAsync(Key(s), s, [new()], new(new() { WindowLowKeV = 100, WindowHighKeV = 140 }, true));
        Assert.Contains("no 122.06", missing.Anger.Unavailable); Assert.NotNull(missing.Spectrum.Unavailable); Assert.Null(missing.Cost.Cell);
        var ambiguous = await service.ProcessAsync(Key(s), s, [new()], new(new() { WindowLowKeV = 100, WindowHighKeV = 1400 }, true));
        Assert.Contains("spans", ambiguous.Anger.Unavailable);
    }

    [Fact]
    public async Task IncrementalPrefixesAndViewChanges_NeverDoubleCount_AndPreviouslyPublishedArraysStayFrozen()
    {
        var s = Fixture(); var service = new ReadoutCompareService(); var key = Key(s);
        var first = await service.ProcessAsync(key, s, [new()], new(new(), false));
        var saved = first.Spectrum.Direct.ToArray();
        var same = await service.ProcessAsync(key, s, [new()], new(new(), false));
        Assert.Equal(saved, same.Spectrum.Direct); Assert.Equal(first.Accounting, same.Accounting);
        await service.ProcessAsync(key, s, [new()], new(new(), true));
        Assert.Equal(saved, first.Spectrum.Direct);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ProcessAsync(key, s, [new()], new(new(), false), cancelled.Token));
        var again = await service.ProcessAsync(key, s, [new()], new(new(), false));
        Assert.Equal(saved, again.Spectrum.Direct);
    }

    [Fact]
    public async Task InvalidDominantAndPrefixIdentity_AreRefused_EmptyDenominatorRemainsUnavailable()
    {
        var s = Fixture(); var r = s.Readout!; var records = r.Records.ToArray();
        records[0] = records[0] with { DominantHit = r.Hits.Count };
        var bad = s with { Readout = r with { Records = records } };
        await Assert.ThrowsAsync<ArgumentException>(() => new ReadoutCompareService().ProcessAsync(Key(bad), bad, [new()], new(new(), false)));
        await Assert.ThrowsAsync<ArgumentException>(() => new ReadoutCompareService().ProcessAsync(Key(s) with { HitCount = 0 }, s, [new()], new(new(), false)));
        var empty = s with { Readout = r with { Records = [], Lines = [new(r.Preparation, [], 0, 0)] } };
        var result = await new ReadoutCompareService().ProcessAsync(Key(empty), empty, [new()], new(new(), false));
        Assert.Null(result.Accounting.SameShare); Assert.Equal(6, result.Direct.Image.EffectiveCounts);
        Assert.All(result.Assignment.Difference.Raw.ToArray(), v => Assert.True(double.IsNaN(v)));
    }

    [Fact]
    public async Task PhysicalRetention_MatchesOwnedSites_AndContinuationPreservesNewFactsAndOldPrefixes()
    {
        var preparation = new ReadoutPreparationService(); var p = await preparation.PrepareAsync(Head, new());
        var detector = new DetectorSettings { ReadoutMode = ReadoutMode.FourOutputAnger, PreparedReadoutId = p.Id };
        var clock = new ManualTimeProvider();
        await using var session = new SimulationService(clock, preparation).Start([new()], Head, 1, 4, detector, seed: 47001);
        async Task<AcquisitionSnapshot> Drain()
        {
            AcquisitionSnapshot? last = null;
            await foreach (var s in session.ReadSnapshotsAsync())
            {
                last = s; Assert.Empty(s.Events);
                Assert.All(s.Readout!.Records, e => Assert.InRange(e.DominantHit, -1, s.Readout.Hits.Count - 1));
                if (!s.IsCompleted) { await clock.WaitForTimerAsync(); clock.Advance(TimeSpan.FromMilliseconds(250)); }
            }
            return last!;
        }
        var first = await Drain(); var frozen = first.Readout!.Hits.ToArray();
        session.Continue(2, 4); var final = await Drain();
        Assert.Equal(frozen, first.Readout.Hits); Assert.Equal(frozen, final.Readout!.Hits.Take(frozen.Length));
        foreach (var sample in final.Readout.TruthSamples)
        {
            var h = final.Readout.Hits[sample.HitIndex];
            Assert.Equal(ReadoutDevice.DirectCrystal(sample.Sites, 12), h.PixelY * 12 + h.PixelX);
            double sum = sample.Sites.Sum(s => s.EnergyKeV);
            // Two aggregation orders, at most 2n additions: allow their combined gamma(2n) round-off.
            const double u = 1.1102230246251565e-16;
            double operations = 2 * sample.Sites.Count + 2;
            double bound = 2 * operations * u / (1 - operations * u) * sum;
            Assert.InRange(Math.Abs(sum - h.DepositKeV), 0, bound);
        }
        Assert.NotNull(final.Readout.Participation);
        Assert.Equal(final.Readout.Hits.Count, final.Readout.Participation.FinalizedHistories + final.Readout.Participation.UnresolvedHistories);
        output.WriteLine($"Retained {final.Readout.Hits.Count} direct inputs; {final.Readout.Records.Count} held; 56 bytes/input.");
    }
}
