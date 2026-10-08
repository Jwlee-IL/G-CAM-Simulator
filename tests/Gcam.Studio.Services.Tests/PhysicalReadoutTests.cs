using Gcam.Configuration;
using Gcam.Studio.Core.Services;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Gcam.Studio.Services.Tests;

/// <summary>Independent preparation, immutable measured prefixes and virtual-clock continuation; exact oracles.</summary>
public sealed class PhysicalReadoutTests(ITestOutputHelper output)
{
    private static readonly OpticsSettings Head = new() { DetectorPixels = 12, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
    private static readonly SceneSource[] Scene = [new() { ActivityUCi = 500 }];
    private static readonly ReadoutPreparationService Preparation = new();
    private static readonly Lazy<Task<ReadoutPreparation>> Ready = new(() => Preparation.PrepareAsync(Head, new()));

    private static async Task<List<AcquisitionSnapshot>> Segment(IAcquisitionSession session, ManualTimeProvider clock, int? stopAt = null)
    {
        var result = new List<AcquisitionSnapshot>(); int ticks = 0;
        await foreach (var snapshot in session.ReadSnapshotsAsync())
        {
            result.Add(snapshot);
            if (snapshot.IsCompleted) continue;
            if (stopAt is { } n && ticks >= n) { session.Stop(); continue; }
            await clock.WaitForTimerAsync(); clock.Advance(TimeSpan.FromMilliseconds(250)); ticks++;
        }
        return result;
    }

    [Fact]
    public async Task IsolatedScope_TriggerFirstCrossingAndHoldPeakShareThePulseTimeBase()
    {
        var p = await Ready.Value;
        var ro = ReadoutPreparationService.Preset();
        ro.Optics.DepthBins = 1; ro.Optics.PhotonsPerBin = 10;
        ro.Network.Topology = NetworkTopology.IdealBilinear;
        var processor = new Gcam.Detector.ReadoutPulseProcessor(new Gcam.Detector.ReadoutDevice(
            new DetectorConfig { PixelsX=2, PixelsY=2 },ro,1),ro.Pulse,ro.Trigger);
        double amplitude = processor.ThresholdCodes * 10;
        var hit = new Gcam.Detector.ReadoutHit(123.25,[amplitude/4,amplitude/4,amplitude/4,amplitude/4],amplitude);
        var stream = processor.CreateStream(new Gcam.Core.DefaultRandom(2)); stream.Append(hit);
        var conversion = Assert.Single(stream.AdvanceTo(5000));
        // Independent continuous rising-edge root, bracketed by bisection, then one engine period to quantise it.
        double peak = processor.RiseNs*processor.TailNs/(processor.TailNs-processor.RiseNs)
            * Math.Log(processor.TailNs/processor.RiseNs);
        double lo=0,hi=peak;
        double Sum(double ns) => amplitude*(Math.Exp(-ns/p.TailNs)-Math.Exp(-ns/p.RiseNs))/p.Normalization;
        for(int i=0;i<60;i++) { double mid=(lo+hi)/2; if(Sum(mid)<processor.ThresholdCodes) lo=mid; else hi=mid; }
        double trigger = conversion.TriggerTimeNs-hit.TimeNs;
        Assert.InRange(trigger,lo,hi+processor.StepNs);
        Assert.True(Sum(trigger)>=processor.ThresholdCodes);
        Assert.True(Sum(trigger-processor.StepNs)<processor.ThresholdCodes);
        Assert.InRange(conversion.HoldTimeNs-conversion.TriggerTimeNs,0,processor.HoldWindowNs);
        Assert.InRange(Math.Abs(conversion.HoldTimeNs-hit.TimeNs-peak),0,processor.StepNs);
        var record = new MeasuredReadoutRecord(0,conversion.HoldTimeNs*1e-9,conversion.TriggerTimeNs*1e-9,
            new(),new(),0,0,0,661.7,0,1,1);
        var flood = new Gcam.Core.DetectorImage(12,12);
        var snapshot = new AcquisitionSnapshot(5e-6,1,1,1,false,
            new(flood.ReadOnlyCopy(),-5.5,1,null,0,0,null,1,TimeSpan.Zero),[],TimeSpan.Zero,true)
        { Readout=new(p,[record],[new(hit.TimeNs*1e-9,new(amplitude/4,amplitude/4,amplitude/4,amplitude/4))],0,false) };
        var scope = PhysicalReadoutWaveform.Process(snapshot,new(0,10,false,50,false),default);
        var sum=scope.PhysicalLanes[4];
        int crossing=Array.FindIndex(sum.Y,y=>y>=p.ThresholdCodes);
        int maximum=Array.IndexOf(sum.Y,sum.Y.Max());
        Assert.True(crossing>0); Assert.True(sum.Y[crossing-1]<p.ThresholdCodes);
        // Scope and processor phases can differ: each first sampled crossing is at most one period after the root.
        double boundUs=Math.Max(processor.StepNs*1e-3,sum.Step);
        Assert.InRange(Math.Abs(scope.PhysicalMarkers[0].X-sum.XAt(crossing)),0,boundUs);
        Assert.InRange(Math.Abs(scope.PhysicalMarkers[1].X-sum.XAt(maximum)),0,boundUs);
        Assert.InRange(scope.PhysicalMarkers[1].X-scope.PhysicalMarkers[0].X,0,processor.HoldWindowNs*1e-3);
        output.WriteLine($"Isolated pulse: continuous crossing [{lo:R},{hi:R}] ns; trigger {trigger:R} ns; hold {conversion.HoldTimeNs-hit.TimeNs:R} ns; peak {peak:R} ns. Engine bound {processor.StepNs:R} ns; scope bound {boundUs:R} µs; hold window {processor.HoldWindowNs:R} ns.");
    }

    [Fact]
    public async Task Prepare_IsIndependentCachedAndCancellationDoesNotPublish()
    {
        var p = await Ready.Value;
        Assert.True(p.Succeeded, p.Failure);
        Assert.Equal(144, p.Peaks.Count);
        Assert.Equal(144, p.Diagnostics.Count);
        output.WriteLine($"Preparation: {p.Cost.TotalSeconds:R} s, scored {p.Scored}, triggered with valid position {p.Triggered}, photopeak {p.InWindow}, gain fallback {p.GainFallback}, ordered peaks {p.Peaks.Count}.");
        Assert.Equal(p.Density.Width * p.Density.Height, p.Labels.Count);
        Assert.Same(p, await Preparation.PrepareAsync(Head, new() { GainSigma = .5, GainSeed = 999 }));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Preparation.PrepareAsync(Head, new(), cancellationToken: cancelled.Token));
        Assert.Same(p, await Preparation.PrepareAsync(Head, new()));
    }

    [Fact]
    public async Task Continuation_ConservesMeasuredRecordsAndImmutablePrefixesExactly()
    {
        var p = await Ready.Value; Assert.True(p.Succeeded, p.Failure);
        var detector = new DetectorSettings { ReadoutMode = ReadoutMode.FourOutputAnger, PreparedReadoutId = p.Id };
        var clock = new ManualTimeProvider(); var service = new SimulationService(clock, Preparation);
        await using var whole = service.Start(Scene, Head, 2, 4, detector, seed: 4711);
        var reference = (await Segment(whole, clock))[^1];
        await using var paused = service.Start(Scene, Head, 2, 4, detector, seed: 4711);
        var first = await Segment(paused, clock, 1);
        var frozen = first[^1].Readout!.Records.ToArray();
        clock.Advance(TimeSpan.FromSeconds(100)); paused.Continue(2, 3);
        var second = await Segment(paused, clock); var final = second[^1];
        Assert.Equal(first[^1].LiveTimeS, second[0].LiveTimeS);
        Assert.Equal(reference.Readout!.Records, final.Readout!.Records);
        Assert.Equal(reference.Readout.Hits, final.Readout.Hits);
        await using var changedGain = service.Start(Scene, Head, 2, 4, detector with { GainSigma = .5, GainSeed = 888 }, seed: 4711);
        Assert.Equal(reference.Readout.Records, (await Segment(changedGain, clock))[^1].Readout!.Records);
        output.WriteLine($"Seed 4711, 500 µCi, 1 m, 2 s: assigned {final.Counts}, unknown {final.Readout.Unknown}, triggered {final.Readout.Records.Count}, realised hits {final.Readout.Hits.Count}; exact uninterrupted/continued/changed-legacy-gain equality.");
        Assert.Equal(frozen, first[^1].Readout!.Records);
        Assert.NotEmpty(final.Readout.Records);
        Assert.Equal(Math.Min(256, final.Readout.Hits.Count), final.Readout.TruthSamples.Count);
        foreach (var s in first.Concat(second))
        {
            Assert.Empty(s.Events);
            Assert.Equal(s.Readout!.Records.Count, s.Counts + s.Readout.Unknown);
            Assert.Equal(s.Counts, (long)s.Imaging.Flood.Raw.ToArray().Sum());
            Assert.All(s.Readout.Records, e => Assert.True(e.HoldTimeS <= s.LiveTimeS));
        }
        var settings = new SpectrumSettings { Readout = final.Readout, WindowLowKeV = 600, WindowHighKeV = 720 };
        var spectrum = PhysicalReadoutProjection.Spectrum(final.Readout, settings, default);
        Assert.Equal(final.Counts, spectrum.TotalCounts);
        Assert.Equal(spectrum.TotalCounts, (long)spectrum.Counts.Sum() + spectrum.OverflowCounts);
        Assert.Equal(final.Readout.Records.Count(e => e.Crystal >= 0 && e.EnergyKeV >= 600 && e.EnergyKeV <= 720), spectrum.Bands[0].Counts);
        var scope = PhysicalReadoutWaveform.Process(final, new(0, 10, false, 50, false), default);
        Assert.Equal(5, scope.PhysicalLanes.Count);
        for (int i = 0; i < scope.PhysicalLanes[4].Y.Length; i++)
            Assert.Equal(scope.PhysicalLanes.Take(4).Sum(l => l.Y[i]), scope.PhysicalLanes[4].Y[i]);
        Assert.Equal("Hold", scope.PhysicalMarkers[1].Label);
        Assert.Equal(0, scope.PhysicalMarkers[1].X);
        Assert.All(scope.Events, e => Assert.True(e.IsMeasured));
        // The replay's absolute seconds lose at most 8u·t in subtraction/conversion. The derivative of
        // (exp(-t/tail)-exp(-t/rise))/norm is bounded by (1/tail+1/rise)/norm. Add summation round-off.
        const double u = 1.1102230246251565e-16;
        var selected = final.Readout.Records[0];
        for (int c = 0; c < 4; c++)
        {
            double amplitude = final.Readout.Hits.Sum(h => Math.Abs(h.Charges.Channel(c)));
            double dtNs = 8 * u * Math.Max(1, final.LiveTimeS) * 1e9;
            var profile = p;
            double m = 8.0 * final.Readout.Hits.Count + 20;
            double tolerance = amplitude * (dtNs * (1 / profile.RiseNs + 1 / profile.TailNs) / profile.Normalization + m * u / (1 - m * u));
            Assert.InRange(Math.Abs(scope.PhysicalLanes[c].Y[250] - selected.Analog.Channel(c)), 0, tolerance);
            output.WriteLine($"Lane {c} held analogue replay absolute error {Math.Abs(scope.PhysicalLanes[c].Y[250] - selected.Analog.Channel(c)):R} codes; derived bound {tolerance:R} codes.");
        }
        Assert.Throws<NotSupportedException>(() => PhysicalReadoutWaveform.Process(final, new(0, 10, true, 50, false), default));
    }

    [Fact]
    public async Task CancellationAtTransportBoundary_PublishesNoPartialArtifact()
    {
        using var cancelled = new CancellationTokenSource();
        var progress = new InlineProgress(text => { if (text.StartsWith("Transporting", StringComparison.Ordinal)) cancelled.Cancel(); });
        var clock = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Preparation.PrepareAsync(Head,
            new() { ReflectorGapMm = .09 }, progress, cancelled.Token));
        output.WriteLine($"Cancellation at calibration transport boundary after optical table: {clock.Elapsed.TotalSeconds:R} s overall; no partial artifact returned.");
        Assert.Same(await Ready.Value, await Preparation.PrepareAsync(Head, new()));
    }
    private sealed class InlineProgress(Action<string> action) : IProgress<string> { public void Report(string value) => action(value); }

    [Fact]
    public async Task ManualWindowBoundariesAndUnknowns_AgreeAcrossSpectrumAndImaging()
    {
        var p = await Ready.Value;
        double[] energies = [599,600,720,721,650];
        var records = energies.Select((e,i)=>new MeasuredReadoutRecord(i,.1+i*.1,.1+i*.1,
            new(),new(),0,0,i==4?-1:0,e,i,1,1)).ToArray();
        var readout = new ReadoutSnapshot(p, Array.AsReadOnly(records),[],1,false);
        var spectrum = PhysicalReadoutProjection.Spectrum(readout,new() { WindowLowKeV=600,WindowHighKeV=720 },default);
        Assert.Equal(4,spectrum.TotalCounts); Assert.Equal(2,spectrum.Bands[0].Counts);
        var flood = new Gcam.Core.DetectorImage(12,12); flood.Add(0,0,4);
        var image = new ImagingResult(flood.ReadOnlyCopy(),-5.5,1,null,0,0,null,4,TimeSpan.Zero);
        var snapshot = new AcquisitionSnapshot(1,4,4,1,false,image,[],TimeSpan.Zero,true) { Readout=readout };
        var config = SimulationService.BuildConfig(Scene,Head,new());
        var projection = PhysicalReadoutProjection.Imaging(snapshot,config,new() { WindowLowKeV=600,WindowHighKeV=720 },default);
        Assert.Equal(4,projection.Channels[0].Image.EffectiveCounts);
        Assert.Equal(2,projection.Channels[1].Image.EffectiveCounts);
        Assert.Throws<NotSupportedException>(()=>PhysicalReadoutProjection.Imaging(snapshot,config,new(Strip:true),default));
    }

    [Fact]
    public void UnsupportedInputs_AreRefusedWithoutMutatingThem()
    {
        var d = new DetectorSettings { ReadoutMode = ReadoutMode.FourOutputAnger };
        Assert.Throws<ArgumentException>(() => SimulationService.BuildConfig(Scene, new(), d));
        Assert.Throws<NotSupportedException>(() => SimulationService.BuildConfig(Scene, Head, d, .1));
        Assert.Throws<NotSupportedException>(() => SimulationService.BuildConfig(Scene, Head, d, ambient: new()));
        Assert.Throws<InvalidOperationException>(() => new SimulationService().Start(Scene, Head, 1, 1, d));
        Assert.Equal(ReadoutMode.DirectCrystal, new DetectorSettings().ReadoutMode);
    }
}
