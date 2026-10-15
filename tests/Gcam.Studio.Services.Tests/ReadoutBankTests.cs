using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Services.Tests;

/// <summary>TODO-41 per-line banks on the real engine (12 × 12 Baseline, 661.7 + 1332.5 keV, about 9 s per line): held ADC
/// facts are acquired once and every line derives its own assignment from them — exactly what a single-line acquisition of
/// that line produces; windows read only their own line; the opt-in flood correction applies the calibration's histogram
/// exactly and an empty bin makes the corrected image unavailable; the session cache pins captured entries.</summary>
public sealed class ReadoutBankTests(ITestOutputHelper output)
{
    private static readonly OpticsSettings Head = new() { DetectorPixels = 12, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
    private static readonly DetectorSettings Physical = new() { ReadoutMode = ReadoutMode.FourOutputAnger };
    private static readonly SceneSource[] Scene = [new() { ActivityUCi = 500 }, new() { Isotope = "Co-60", X = 20, ActivityUCi = 300 }];
    private static readonly ReadoutPreparationService Service = new();
    private static readonly Lazy<Task<(ReadoutPreparation Cs, ReadoutPreparation Co)>> Ready = new(async () =>
    {
        var cs = await Service.PrepareAsync(Head, Physical, ReadoutPolicy.Plan(Head, Physical, 661.7, Service.Offers));
        var co = await Service.PrepareAsync(Head, Physical, ReadoutPolicy.Plan(Head, Physical, 1332.5, Service.Offers));
        return (cs, co);
    });

    private static async Task<AcquisitionSnapshot> Acquire(ReadoutPreparationService service, IReadOnlyList<ReadoutPreparation> bank)
    {
        // Single-line oracle lives below Studio's complete-bank Start guard: replay the SAME full scene and held facts.
        if (bank.Count == 1) return AcquireSingleLineOracle(service, bank[0]);
        var clock = new ManualTimeProvider();
        var detector = Physical with { PreparedReadoutLines = new(bank.Select(p => p.Id).ToArray()) };
        await using var session = new SimulationService(clock, service).Start(Scene, Head, 2, 4, detector, seed: 4711);
        AcquisitionSnapshot? last = null;
        await foreach (var s in session.ReadSnapshotsAsync())
        {
            last = s;
            if (s.IsCompleted) continue;
            await clock.WaitForTimerAsync(); clock.Advance(TimeSpan.FromMilliseconds(250));
        }
        return last!;
    }

    private static AcquisitionSnapshot AcquireSingleLineOracle(ReadoutPreparationService service, ReadoutPreparation preparation)
    {
        var artifact = service.Get(preparation.Id);
        var config = SimulationService.BuildConfig(Scene, Head, Physical);
        config.Seed = 4711; config.Detector.Readout = ReadoutPreparationService.Policy(preparation.Plan!);
        using var source = new Gcam.Simulation.PhysicalListModeSource(config, artifact.Device, artifact.Calibration, 4711);
        var records = new List<MeasuredReadoutRecord>(); var hits = new List<RealisedReadoutHit>();
        var image = new DetectorImage(12, 12);
        static ReadoutCharges Charges(IReadOnlyList<double> q) => new(q[0], q[1], q[2], q[3]);
        while (source.ObservedTimeS < 2)
        {
            foreach (var measured in source.AdvanceUntil(2))
            {
                var e = measured.Conversion;
                records.Add(new(records.Count, e.HoldTimeNs * 1e-9, e.TriggerTimeNs * 1e-9, Charges(e.Codes), Charges(e.AnalogAtHold),
                    measured.RawX, measured.RawY, measured.Crystal, measured.EnergyKeV, e.DominantHit, e.ContributingHits, e.DominantShare));
                if (measured.Crystal >= 0) image.Add(measured.Crystal % 12, measured.Crystal / 12, 1);
            }
            if (source.LastDirectEvent is { } direct)
                hits.AddRange(source.LastHits.Select(h => new RealisedReadoutHit(h.TimeNs * 1e-9, Charges(h.Channels), direct.PixelX, direct.PixelY, direct.DepositKeV)));
        }
        int assigned = records.Count(r => r.Crystal >= 0);
        return new(2, assigned, source.InputRateCps, 1, false, new(image.ReadOnlyCopy(), -5.5, 1, null, 0, 0, null, assigned, TimeSpan.Zero), [], TimeSpan.Zero, true)
        { Detector = Physical, Optics = Head, Readout = new(preparation, records.AsReadOnly(), hits.AsReadOnly(), records.Count - assigned, source.HasPendingHold)
            { Lines = [new(preparation, records.Select(r => new ReadoutLineAssignment(r.Crystal, r.EnergyKeV)).ToArray(), assigned, records.Count - assigned)] } };
    }

    [Fact]
    public async Task LinePreparations_CaptureTheModuleAndDeclaredStreams()
    {
        var (cs, co) = await Ready.Value;
        Assert.True(cs.Succeeded, cs.Failure); Assert.True(co.Succeeded, co.Failure);
        Assert.True(cs.Plan is { IsLegacy: true }); Assert.True(co.Plan is { IsLegacy: false, Budget: 2000 });
        Assert.Equal(new ReadoutModuleGeometry(12, 12, 1, .1, 12, 12, 1, .9), co.Module);
        Assert.Equal(144, co.Peaks.Count); Assert.Equal(144, co.Diagnostics.Count);
        Assert.Equal(144 * 2000, co.Scored);
        // 402 000 000 + 1 000 · line index (3 = 1332.5 keV) + purpose (3 flood, 4 response, 5 ADC, 6–8 validation).
        Assert.Equal("optical 402; transport 402003003; response 402003004; ADC 402003005; independent validation 402003006/402003007/402003008", co.Seeds);
        Assert.Equal("optical 402; transport 403; response 404; ADC 405; independent validation 406/407/408", cs.Seeds);
        Assert.Equal(144, co.CorrectionHistogram!.Count);
        Assert.Same(co, await Service.PrepareAsync(Head, Physical with { GainSigma = .5 }, co.Plan!)); // same-session hit
        output.WriteLine($"661.7 keV: {cs.Cost.TotalSeconds:R} s, 1332.5 keV: {co.Cost.TotalSeconds:R} s; windowed {cs.InWindow} / {co.InWindow}; accounted cache {Service.CachedBytes} bytes in {Service.CachedEntries} entries.");
    }

    [Fact]
    public async Task HeldFactsOnce_EachLineEqualsItsOwnSingleLineAcquisition_WindowsReadOnlyTheirLine()
    {
        var (cs, co) = await Ready.Value;
        var both = await Acquire(Service, [cs, co]);
        var alone = await Acquire(Service, [co]);
        var r = both.Readout!; var single = alone.Readout!;
        Assert.Equal(2, r.Lines.Count); Assert.Equal(661.7, r.Preparation.LineKeV);
        Assert.NotEmpty(r.Records);
        Assert.Equal(single.Records.Count, r.Records.Count);
        Assert.Equal(single.Hits, r.Hits);
        for (int i = 0; i < r.Records.Count; i++)
        {
            var a = r.Records[i]; var b = single.Records[i];
            Assert.Equal((a.Index, a.HoldTimeS, a.TriggerTimeS, a.Codes, a.Analog, a.RawX, a.RawY, a.DominantHit, a.Contributors, a.DominantShare),
                (b.Index, b.HoldTimeS, b.TriggerTimeS, b.Codes, b.Analog, b.RawX, b.RawY, b.DominantHit, b.Contributors, b.DominantShare));
            Assert.Equal(new ReadoutLineAssignment(a.Crystal, a.EnergyKeV), r.Lines[0].Assignments[i]);
            Assert.Equal(new ReadoutLineAssignment(b.Crystal, b.EnergyKeV), r.Lines[1].Assignments[i]);
        }
        foreach (var line in r.Lines) Assert.Equal(r.Records.Count, line.Assigned + line.Unknown);
        Assert.Equal(both.Counts, r.Lines[0].Assigned);
        Assert.Equal(12, both.Imaging.Flood.Width);

        var config = SimulationService.BuildConfig(Scene, Head, Physical);
        var (low, high) = ReadoutPolicy.BenchmarkWindow(1332.5, .15);
        var window = new ImagingSettings { WindowLowKeV = low, WindowHighKeV = high };
        var fromBank = PhysicalReadoutProjection.Imaging(both, config, window, default).Channels[1];
        var fromSingle = PhysicalReadoutProjection.Imaging(alone, config, window, default).Channels[1];
        Assert.Equal(1332.5, fromBank.CalibrationLineKeV);
        Assert.Equal(fromSingle.Image.Flood.Raw.ToArray(), fromBank.Image.Flood.Raw.ToArray());
        Assert.Equal(r.Lines[1].Assignments.LongCount(x => x.Crystal >= 0 && x.EnergyKeV >= low && x.EnergyKeV <= high), fromBank.RawCounts);
        var x122 = PhysicalReadoutProjection.Imaging(both, config, new ImagingSettings { WindowLowKeV = 100, WindowHighKeV = 140 }, default).Channels[1];
        Assert.Contains("no 122.06 keV calibration was captured", x122.Unavailable);
        Assert.Equal(0, x122.Image.EffectiveCounts); Assert.Null(x122.Image.Reconstruction);
        var spectrum = PhysicalReadoutProjection.Spectrum(r, new() { Readout = r, WindowLowKeV = low, WindowHighKeV = high }, default);
        Assert.Equal(fromBank.RawCounts, spectrum.Bands[0].Counts);
        output.WriteLine($"Seed 4711, Cs 500 µCi + Co-60 300 µCi, 2 s: triggered {r.Records.Count}; 661.7 keV assigned {r.Lines[0].Assigned}, 1332.5 keV assigned {r.Lines[1].Assigned}; 1332.5 window {fromBank.RawCounts}.");
    }

    [Fact]
    public async Task FloodCorrection_AppliesTheHistogramExactly_EmptyBinMakesItUnavailable()
    {
        var (cs, _) = await Ready.Value;
        var both = await Acquire(Service, [cs]);
        var config = SimulationService.BuildConfig(Scene, Head, Physical);
        var settings = new ImagingSettings { WindowLowKeV = 600, WindowHighKeV = 720 };
        var raw = PhysicalReadoutProjection.Imaging(both, config, settings, default).Channels[1];
        var corrected = PhysicalReadoutProjection.Imaging(both, config, settings with { ApplyFloodCorrection = true }, default).Channels[1];
        var h = cs.CorrectionHistogram!;
        Assert.All(h, v => Assert.True(v > 0));
        double mean = h.Average();
        for (int y = 0; y < 12; y++) for (int x = 0; x < 12; x++)
            Assert.Equal(raw.Image.Flood[x, y] * (mean / h[y * 12 + x]), corrected.Image.Flood[x, y]);
        Assert.True(corrected.FloodCorrected); Assert.Equal(raw.RawCounts, corrected.RawCounts);
        Assert.Equal((double)raw.RawCounts!, corrected.Image.EffectiveCounts); // raw counts stay the reported count
        var holes = h.ToArray(); holes[5] = 0;
        var emptied = both with { Readout = both.Readout! with { Lines = [both.Readout.Lines[0] with { Preparation = cs with { CorrectionHistogram = holes } }] } };
        var unavailable = PhysicalReadoutProjection.Imaging(emptied, config, settings with { ApplyFloodCorrection = true }, default).Channels[1];
        Assert.Contains("1 empty correction bin(s)", unavailable.Unavailable);
        Assert.Null(unavailable.Image.Reconstruction);
        Assert.Equal(raw.Image.Flood.Raw.ToArray(), PhysicalReadoutProjection.Imaging(emptied, config, settings, default).Channels[1].Image.Flood.Raw.ToArray());
    }

    [Fact]
    public async Task SessionCache_PinsCapturedEntries_EvictsOthersByAccountedBytes_ReportsRealProgress()
    {
        var service = new ReadoutPreparationService(ReadoutOfferRecordLoader.LoadEmbedded, accountedByteLimit: 1);
        var reports = new List<ReadoutPrepareProgress>();
        var cs = await service.PrepareAsync(Head, Physical, ReadoutPolicy.Plan(Head, Physical, 661.7, service.Offers), new Inline(reports.Add));
        var stages = reports.Select(r => r.Stage).Distinct().ToArray();
        Assert.Equal(new[] { "Preparing optical table and DC network…", "Transporting independent calibration flood…",
            "Finding calibration peaks and building LUT…", "Calibrating crystal energy gains…", "Counting assigned correction flood…", "Checking independent single-crystal validation flood…" }, stages);
        var flood = reports.Where(r => r.Stage.StartsWith("Transporting", StringComparison.Ordinal)).ToArray();
        Assert.True(flood.Zip(flood.Skip(1)).All(p => p.First.Done <= p.Second.Done));
        Assert.Equal(144L * 2000, flood[^1].Done); Assert.All(flood, r => Assert.Equal(144L * 2000, r.Total));
        var co = await service.PrepareAsync(Head, Physical, ReadoutPolicy.Plan(Head, Physical, 1332.5, service.Offers));
        Assert.Equal(1, service.CachedEntries); // the 661.7 entry was over the limit and unpinned
        var clock = new ManualTimeProvider();
        await using (var session = new SimulationService(clock, service).Start([Scene[1]], Head, 1, 1,
            Physical with { PreparedReadoutLines = new([co.Id]) }, seed: 1))
        {
            var again = await service.PrepareAsync(Head, Physical, ReadoutPolicy.Plan(Head, Physical, 661.7, service.Offers));
            Assert.NotSame(cs, again); // rebuilt after eviction …
            Assert.Equal(ReadoutLegacyIdentityTests.HashPreparation(cs), ReadoutLegacyIdentityTests.HashPreparation(again)); // … identically
            Assert.Equal(2, service.CachedEntries); // the captured 1332.5 entry is pinned
            Assert.Same(co, await service.PrepareAsync(Head, Physical, co.Plan!));
        }
        output.WriteLine($"Progress reports {reports.Count}; accounted bytes per Baseline entry {service.CachedBytes / Math.Max(1, service.CachedEntries)}.");
    }

    [Fact]
    public async Task LocalCustomModule_UsesGrowthAdjointStreams_AndAcquiresOnItsCapturedGeometry()
    {
        // A small unmatched module (8 × 8 at 1 mm) runs the fine-head path — LatticeGrowth, adjoint solve, streamed flood,
        // declared line streams — in seconds; the 64 k High-res preparation stays out of the normal suite.
        var optics = new OpticsSettings { DetectorPixels = 8, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
        var service = new ReadoutPreparationService();
        var plan = ReadoutPolicy.Plan(optics, Physical, 661.7, service.Offers);
        Assert.True(plan is { Status: ReadoutLineStatus.Local, Budget: 2000, Method: FloodCalibrationMethod.LatticeGrowth, Solve: ChargeNetworkSolveMethod.Adjoint });
        var p = await service.PrepareAsync(optics, Physical, plan);
        Assert.True(p.Succeeded, p.Failure);
        Assert.Equal(new ReadoutModuleGeometry(8, 8, 1, .1, 8, 8, 1, .9), p.Module);
        Assert.Equal(64, p.Peaks.Count); Assert.Equal(64, p.Diagnostics.Count); Assert.Equal(64 * 2000, p.Scored);
        Assert.Contains("transport 402002003", p.Seeds);
        Assert.Equal(64, p.CorrectionHistogram!.Count);
        var clock = new ManualTimeProvider();
        await using var session = new SimulationService(clock, service).Start([Scene[0]], optics, 1, 4,
            Physical with { PreparedReadoutLines = new([p.Id]) }, seed: 99);
        AcquisitionSnapshot? last = null;
        await foreach (var s in session.ReadSnapshotsAsync())
        {
            last = s;
            if (s.IsCompleted) continue;
            await clock.WaitForTimerAsync(); clock.Advance(TimeSpan.FromMilliseconds(250));
        }
        Assert.Equal((8, 8), (last!.Imaging.Flood.Width, last.Imaging.Flood.Height));
        Assert.NotEmpty(last.Readout!.Records);
        Assert.All(last.Readout.Records, r => Assert.InRange(r.Crystal, -1, 63));
        Assert.Equal(last.Counts, (long)last.Imaging.Flood.Raw.ToArray().Sum());
        var scope = PhysicalReadoutWaveform.Process(last, new(0, 10, false, 50, false), default);
        Assert.All(scope.Events, e => { Assert.InRange(e.PixelX, -1, 7); Assert.InRange(e.PixelY, -1, 7); });
        output.WriteLine($"Local 8 × 8: {p.Cost.TotalSeconds:R} s, {p.Peaks.Count} peaks, windowed {p.InWindow}; 1 s acquisition {last.Readout.Records.Count} records, {last.Counts} assigned.");
    }

    [Fact]
    public async Task CancellationDuringTheStreamedFlood_PublishesNothing()
    {
        var optics = new OpticsSettings { DetectorPixels = 8, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
        var service = new ReadoutPreparationService();
        var plan = ReadoutPolicy.Plan(optics, Physical, 1332.5, service.Offers);
        using var cancel = new CancellationTokenSource();
        long seen = 0;
        var progress = new Inline(r => { if (r.Stage.StartsWith("Transporting", StringComparison.Ordinal) && r.Done > 1) { seen = r.Done; cancel.Cancel(); } });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PrepareAsync(optics, Physical, plan, progress, cancel.Token));
        Assert.InRange(seen, 2, 64L * 2000 - 1); // cancelled inside the flood, not at a stage boundary
        Assert.Equal(0, service.CachedEntries);
        Assert.Equal(0, service.CachedDevices);
    }

    [Fact]
    public async Task TwoCancelledModules_RetainNoDevices_AndMissingSceneLineIsRefused()
    {
        var service = new ReadoutPreparationService();
        foreach (double pitch in new[] { 1.0, 1.1 })
        {
            var optics = Head with { DetectorPixels = 8, PixelPitchMm = pitch };
            using var cancel = new CancellationTokenSource();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PrepareAsync(optics, Physical,
                ReadoutPolicy.Plan(optics, Physical, 661.7, service.Offers),
                new Inline(r => { if (r.Stage.StartsWith("Transporting", StringComparison.Ordinal)) cancel.Cancel(); }), cancel.Token));
            Assert.Equal(0, service.CachedEntries); Assert.Equal(0, service.CachedDevices); Assert.Equal(0, service.CachedBytes);
        }
        var (cs, _) = await Ready.Value;
        var ex = Assert.Throws<InvalidOperationException>(() => new SimulationService(readoutPreparation: Service).Start(Scene, Head, 1, 1,
            Physical with { PreparedReadoutId = cs.Id }));
        Assert.Contains("every calibration line", ex.Message); Assert.Contains("1332.5", ex.Message);
    }

    private sealed class Inline(Action<ReadoutPrepareProgress> action) : IProgress<ReadoutPrepareProgress>
    {
        public void Report(ReadoutPrepareProgress value) => action(value);
    }

    [Fact]
    public async Task RealCacheEviction_ChangesShellReadiness_AndRecoversTheTwoLineBank()
    {
        // Two 8x8 entries fit; a subsequently prepared Baseline entry evicts them.
        const long twoEntries = 2 * (256L * 256 * 24 + 64 * 12);
        var service = new ReadoutPreparationService(ReadoutOfferRecordLoader.LoadEmbedded, twoEntries);
        var optics = Head with { DetectorPixels = 8 };
        var acquisition = new SimulationService(readoutPreparation: service);
        var vm = new Gcam.Studio.Core.ViewModels.MainViewModel(acquisition, new TestTheme(), new SpectrumService(), readoutPreparation: service);
        vm.Optics = optics; vm.ReadoutMode = ReadoutMode.FourOutputAnger; vm.AmbientDoseRateMicroSvPerHour = 0;
        vm.AddSourceCommand.Execute(null); vm.Sources[^1].Isotope = "Co-60";
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        Assert.True(vm.IsReadoutBankReady); Assert.Equal(2, vm.MatchingBank.Count);
        var ids = vm.MatchingBank.Select(p => p.Id).ToArray();
        await service.PrepareAsync(Head, Physical);
        Assert.False(vm.IsReadoutBankReady); Assert.Equal("Not prepared", vm.ReadoutState);
        Assert.All(ids, id => Assert.Throws<InvalidOperationException>(() => service.Get(id)));
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Contains("Prepare", vm.ReadoutMessage); Assert.Null(vm.Snapshot);
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        Assert.True(vm.IsReadoutBankReady); Assert.Equal(2, service.CachedEntries);
        Assert.True(service.CachedBytes <= twoEntries);
        Assert.All(vm.MatchingBank, p => Assert.DoesNotContain(p.Id, ids));
    }

    private sealed class TestTheme : IThemeService
    {
        public AppTheme Current => AppTheme.Dark;
        public void Apply(AppTheme theme) { }
    }
}
