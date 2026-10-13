using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;
using System.Security.Cryptography;

namespace Gcam.Studio.Services.Tests;

public sealed class ReadoutFieldAcquisitionTests
{
    private static readonly OpticsSettings Head = new() { DetectorPixels = 12, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
    private static readonly DetectorSettings Physical = new() { ReadoutMode = ReadoutMode.FourOutputAnger };
    private static readonly SceneSource[] Scene = [new() { ActivityUCi = 500 }, new() { Isotope = "Co-60", X = 20, ActivityUCi = 300 }];
    private static readonly ReadoutPreparationService Preparation = new();
    private static readonly Lazy<Task<DetectorSettings>> Ready = new(async () =>
    {
        var cs = await Preparation.PrepareAsync(Head, Physical, ReadoutPolicy.Plan(Head, Physical, 661.7, Preparation.Offers));
        var co = await Preparation.PrepareAsync(Head, Physical, ReadoutPolicy.Plan(Head, Physical, 1332.5, Preparation.Offers));
        return Physical with { PreparedReadoutLines = new([cs.Id, co.Id]) };
    });

    private static async Task<AcquisitionSnapshot> Segment(IAcquisitionSession session, ManualTimeProvider clock, bool stop = false)
    {
        AcquisitionSnapshot? last = null; int ticks = 0;
        await foreach (var s in session.ReadSnapshotsAsync())
        {
            last = s;
            Assert.Equal(s.Readout!.Records.Count, s.Counts + s.Readout.Unknown);
            Assert.All(s.Readout.Records, e => Assert.True(e.HoldTimeS <= s.LiveTimeS));
            Assert.InRange(s.Readout.TruthSamples.Count, 0, 256);
            if (s.IsCompleted) continue;
            if (stop && ticks++ >= 1) { session.Stop(); continue; }
            await clock.WaitForTimerAsync(); clock.Advance(TimeSpan.FromMilliseconds(250));
        }
        return last!;
    }

    [Theory]
    [InlineData(AmbientGeometry.FrontOnlyThroughMask, 0, true)]
    [InlineData(AmbientGeometry.BareCrystalAllFaces, 0, true)]
    [InlineData(AmbientGeometry.FrontOnlyThroughMask, .5, true)]
    [InlineData(AmbientGeometry.BareCrystalAllFaces, .5, true)]
    [InlineData(AmbientGeometry.FrontOnlyThroughMask, 1, true)]
    [InlineData(AmbientGeometry.BareCrystalAllFaces, 1, true)]
    [InlineData(AmbientGeometry.FrontOnlyThroughMask, .5, false)]
    [InlineData(AmbientGeometry.FrontOnlyThroughMask, 1, false)]
    public async Task BsrAndAmbient_ContinueKeepsFactsAndEveryLine_AndPublishesComponentRates(AmbientGeometry geometry, double bsr, bool ambient)
    {
        var detector = await Ready.Value;
        var clock = new ManualTimeProvider(); var service = new SimulationService(clock, Preparation);
        var field = new AmbientFieldConfig { DoseRateMicroSvPerHour = 1, Geometry = geometry };
        await using var whole = ambient ? service.StartAmbient(Scene, Head, 2, 4, field, detector, bsr, seed: 4711)
            : service.Start(Scene, Head, 2, 4, detector, bsr, seed: 4711);
        var expected = await Segment(whole, clock);
        await using var paused = ambient ? service.StartAmbient(Scene, Head, 2, 4, field, detector, bsr, seed: 4711)
            : service.Start(Scene, Head, 2, 4, detector, bsr, seed: 4711);
        var stopped = await Segment(paused, clock, true);
        var frozen = stopped.Readout!.Records.ToArray();
        clock.Advance(TimeSpan.FromSeconds(100)); paused.Continue(2, 4);
        var actual = await Segment(paused, clock);
        Assert.Equal(expected.Readout!.Records, actual.Readout!.Records);
        Assert.Equal(expected.Readout.Hits, actual.Readout.Hits);
        Assert.Equal(frozen, stopped.Readout.Records);
        Assert.Equal(expected.SourceRateCps, actual.SourceRateCps);
        Assert.Equal(expected.AmbientRateCps, actual.AmbientRateCps);
        if (ambient)
        {
            Assert.True(actual.SourceRateCps > 0); Assert.True(actual.AmbientRateCps > 0);
            Assert.Equal(AmbientIncidenceDescription.For(geometry, true), actual.AmbientIncidence);
        }
        else { Assert.Equal(0, actual.AmbientRateCps); Assert.Null(actual.AmbientIncidence); }
        for (int i = 0; i < 2; i++)
        {
            var a = actual.Readout.Lines[i]; var e = expected.Readout.Lines[i];
            Assert.Equal(e.Assignments, a.Assignments);
            Assert.Equal(actual.Readout.Records.Count, a.Assigned + a.Unknown);
        }
        var settings = new ImagingSettings { WindowLowKeV = 600, WindowHighKeV = 720 };
        var config = SimulationService.BuildConfig(Scene, Head, detector, bsr, ambient: ambient ? field : null);
        var image = PhysicalReadoutProjection.Imaging(actual, config, settings, default).Channels[1];
        var spectrum = PhysicalReadoutProjection.Spectrum(actual.Readout,
            new() { WindowLowKeV = 600, WindowHighKeV = 720 }, default);
        Assert.Equal(image.RawCounts, spectrum.Bands[0].Counts);
        Assert.Equal(new[] { 661.7, 1332.5 }, actual.Readout.Lines.Select(l => l.LineKeV));
    }

    [Fact]
    public async Task FieldOnly_RefusesBeforeCapture_AndMissingAmbientBsrBankDoesNotStart()
    {
        var service = new SimulationService(readoutPreparation: Preparation);
        Assert.Equal(ReadoutPolicy.FieldOnlyMessage, Assert.Throws<NotSupportedException>(() =>
            service.StartAmbient([], Head, 1, 1, new(), Physical)).Message);
        Assert.Throws<InvalidOperationException>(() => service.StartAmbient(Scene, Head, 1, 1, new(), Physical, 1));
        Assert.Throws<InvalidOperationException>(() => service.StartAmbient(Scene, Head, 1, 1, new(), Physical));
        // The refused calls do not poison reuse of either prepared entry.
        var detector = await Ready.Value;
        Assert.Equal(2, detector.PreparedReadoutIds.Count);
    }

    [Fact]
    public async Task PhysicalGuards_KeepDarkAndLegacyCrosstalkExplicit()
    {
        var detector = await Ready.Value;
        var bank = Preparation.Capture(detector.PreparedReadoutIds);
        try
        {
            var config = SimulationService.BuildConfig(Scene, Head, detector);
            config.Background = new() { DarkCountRateKcps = .1 };
            Assert.Contains("electronic dark arrivals", Assert.Throws<NotSupportedException>(() =>
                new PhysicalListModeSource(config, bank.Reference.Device, bank.Reference.Calibration, 4711)).Message);
            config.Background = null; config.Detector.OpticalCrosstalkFraction = .1;
            Assert.Contains("legacy optical crosstalk = 0", Assert.Throws<NotSupportedException>(() =>
                new PhysicalListModeSource(config, bank.Reference.Device, bank.Reference.Calibration, 4711)).Message);
        }
        finally { Preparation.Release(bank); }
    }

    [Fact]
    public async Task AmbientEmptyInterval_CompletesPendingHoldWithoutFutureInput()
    {
        var detector = await Ready.Value;
        var bank = Preparation.Capture(detector.PreparedReadoutIds);
        try
        {
            var config = SimulationService.BuildConfig(Scene, Head, detector, ambient: new());
            config.Seed = 4711;
            // Obtain the actual first pre-trigger event time without drawing readout fluctuations.
            var direct = config.Clone(); direct.Detector.Readout = null;
            using var transport = new ListModeSource(direct, true, true);
            RecordedTransportEvent? first = null;
            while (first is null) { transport.AdvanceUntil(2); first = transport.LastRecordedEvent; }
            double arrival = first.Event.ArrivalTimeS;
            using var producer = new PhysicalListModeSource(config, bank.Reference.Device,
                bank.Entries.Select(e => e.Calibration).ToArray(), 4711);
            while (producer.ObservedTimeS < arrival) Assert.Empty(producer.AdvanceUntil(arrival));
            Assert.Equal(1, producer.HitsObserved);
            // With a deliberately long hold search, a trigger is pending through a proved empty interval.
            var heldConfig = config.Clone(); heldConfig.Detector.Readout!.Pulse.HoldWindowNs = 100000;
            using var held = new PhysicalListModeSource(heldConfig, bank.Reference.Device,
                bank.Entries.Select(e => e.Calibration).ToArray(), 4711);
            while (held.ObservedTimeS < arrival + 10000e-9) Assert.Empty(held.AdvanceUntil(arrival + 10000e-9));
            Assert.True(held.HasPendingHold);
            var events = new List<PhysicalMeasuredEvent>();
            while (held.ObservedTimeS < arrival + 120000e-9) events.AddRange(held.AdvanceUntil(arrival + 120000e-9));
            Assert.NotEmpty(events);
            Assert.Equal(1, held.HitsObserved); // future look-ahead was never realised
        }
        finally { Preparation.Release(bank); }
    }

    [Fact]
    public async Task ZeroFieldTwoLineHeldFacts_MatchBaselineProducerHash()
    {
        var detector = await Ready.Value;
        var bank = Preparation.Capture(detector.PreparedReadoutIds);
        try
        {
            var config = SimulationService.BuildConfig(Scene, Head, detector); config.Seed = 4711;
            using var source = new PhysicalListModeSource(config, bank.Reference.Device,
                bank.Entries.Select(e => e.Calibration).ToArray(), 4711);
            using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
            while (source.ObservedTimeS < 2)
                foreach (var e in source.AdvanceUntil(2))
                {
                    writer.Write(e.Conversion.TriggerTimeNs); writer.Write(e.Conversion.HoldTimeNs);
                    foreach (var code in e.Conversion.Codes) writer.Write(code);
                    foreach (var assignment in e.Assignments) { writer.Write(assignment.Crystal); writer.Write(assignment.EnergyKeV); }
                }
            writer.Flush();
            // Captured with the unmodified 0d21524 producer compiled in task scratch, not the new implementation.
            Assert.Equal("3B64F2131DF4540747CE19BA5BBFC9799CE5F0D8E0A35C9381EAC9A6AF93AB6D",
                Convert.ToHexString(SHA256.HashData(memory.ToArray())));
        }
        finally { Preparation.Release(bank); }
    }

    [Fact]
    public async Task EmptyPhysicalIntervals_CompleteWithoutInventedHits_AndCancellationConsumesNothing()
    {
        var detector = await Ready.Value;
        var bank = Preparation.Capture(detector.PreparedReadoutIds);
        try
        {
            var config = SimulationService.BuildConfig(Scene, Head, detector, ambient: new());
            config.Sources = []; config.Source.ActivityBq = 0;
            using var producer = new PhysicalListModeSource(config, bank.Reference.Device,
                bank.Entries.Select(e => e.Calibration).ToArray(), 4711);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => producer.AdvanceUntil(1, cancelled.Token));
            Assert.Equal(0, producer.ObservedTimeS);
            Assert.Empty(producer.AdvanceUntil(1)); Assert.Equal(1, producer.ObservedTimeS);
            Assert.Empty(producer.AdvanceUntil(2)); Assert.Equal(2, producer.ObservedTimeS);
            Assert.Empty(producer.LastHits); Assert.Equal(0, producer.HitsObserved);
        }
        finally { Preparation.Release(bank); }
    }
}
