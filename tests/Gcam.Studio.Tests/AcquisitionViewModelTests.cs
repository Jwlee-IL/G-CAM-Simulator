using System.Threading.Channels;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public sealed class AcquisitionViewModelTests
{
    private sealed class Theme : IThemeService
    {
        public AppTheme Current { get; private set; }
        public void Apply(AppTheme theme) => Current = theme;
    }

    // A virtual acquisition clock: advancing wall time publishes physical data at the requested speed. Segments
    // continue the same live time and event list, like the real session.
    private sealed class VirtualSession(double preset, double speed, int? seed) : IAcquisitionSession
    {
        private Channel<AcquisitionSnapshot> _channel = Channel.CreateUnbounded<AcquisitionSnapshot>();
        private readonly List<DetectedEvent> _events = [];
        private double _live;
        private readonly DetectorImage _flood = new(4, 4);
        public int Continues { get; private set; }
        public bool Disposed { get; private set; }
        public void AdvanceWallTime(double wallTime, bool mcLimited = false)
        {
            _live = Math.Min(preset, _live + wallTime * (mcLimited ? speed / 2 : speed));
            int count = (int)Math.Round(_live * 10);
            while (_events.Count < count)
            {
                _events.Add(new DetectedEvent(1, 1, 661.7, (_events.Count + 1) / 10.0));
                _flood.Add(1, 1, 1);
            }
            var image = new ImagingResult(_flood.ReadOnlyCopy(), -1.5, 1, null, 0, 0,
                new SourceEstimate(new Vector3(0, 0, 1000), 2), count, TimeSpan.FromSeconds(wallTime));
            _channel.Writer.TryWrite(new AcquisitionSnapshot(_live, count, 10, mcLimited ? speed / 2 : speed,
                mcLimited, image, Array.AsReadOnly(_events.ToArray()), TimeSpan.Zero, _live >= preset) { Seed = seed });
            if (_live >= preset) _channel.Writer.TryComplete();
        }
        public IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync(CancellationToken cancellationToken = default)
            => _channel.Reader.ReadAllAsync(cancellationToken);
        public void Stop() => _channel.Writer.TryComplete();
        public void Continue(double presetLiveTimeS, double speedValue)
        {
            Continues++;
            preset = presetLiveTimeS;
            speed = speedValue;
            _channel = Channel.CreateUnbounded<AcquisitionSnapshot>();
        }
        public ValueTask DisposeAsync() { Disposed = true; Stop(); return ValueTask.CompletedTask; }
        public void Fail(Exception error) => _channel.Writer.TryComplete(error);
    }

    private sealed class Service : IAcquisitionService
    {
        public int Starts { get; private set; }
        public VirtualSession Session { get; private set; } = null!;
        public DetectorSettings? Detector { get; private set; }
        public double BackgroundToSignalRatio { get; private set; }
        public List<int?> Seeds { get; } = [];
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS, double speed,
            DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
        {
            Detector = detector;
            BackgroundToSignalRatio = backgroundToSignalRatio;
            Starts++;
            Seeds.Add(seed);
            return Session = new VirtualSession(liveTimeS, speed, seed);
        }
    }

    private static async Task WaitForSnapshot(MainViewModel vm, double live)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (vm.Snapshot?.LiveTimeS != live) await Task.Delay(1, timeout.Token);
    }

    private static MainViewModel Model(Service service, double liveTime = 60) =>
        new(service, new Theme(), new FakeSpectrumService()) { LiveTimeS = liveTime, SeedText = "42", AmbientDoseRateMicroSvPerHour = 0 };

    [Fact]
    public async Task Start_CapturesDetectorInputs_AndLocksThemWhileDataExist()
    {
        var acquisition = new Service();
        var spectrum = new FakeSpectrumService();
        var vm = new MainViewModel(acquisition, new Theme(), spectrum)
            { LiveTimeS = 5, GainSigmaPercent = 4, GainSeed = 7, BackgroundToSignalRatio = 1, SeedText = "42", AmbientDoseRateMicroSvPerHour = 0 };
        var run = vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(0.04, acquisition.Detector!.GainSigma);
        Assert.Equal(7, acquisition.Detector.GainSeed);
        Assert.Equal(1, acquisition.BackgroundToSignalRatio);
        acquisition.Session.AdvanceWallTime(0.5);
        await run;
        vm.Snapshot = vm.Snapshot! with { Detector = acquisition.Detector };
        await vm.Spectrum.WhenUpdated;
        int calls = spectrum.Calls;
        // Every physical writer is refused while data exist, not only the disabled fields.
        vm.GainSeed = 9; vm.GainSigmaPercent = 8; vm.BackgroundToSignalRatio = 3; vm.ReflectorGapUm = "50";
        vm.Optics = vm.Optics with { CellPitchMm = 1 }; vm.OpticsEditor.CellPitch = "0.9"; vm.SeedText = "7";
        vm.Sources[0].X = 12; vm.Sources[0].Isotope = "Co-60"; vm.Sources[0].DistanceMm = 500; vm.Sources[0].ActivityUCi = 9;
        var scintillator = vm.Scintillator;
        vm.Scintillator = vm.Scintillators.First(s => s != scintillator);
        Assert.Equal(7, vm.GainSeed);
        Assert.Equal(4, vm.GainSigmaPercent);
        Assert.Equal(1, vm.BackgroundToSignalRatio);
        Assert.Equal("100", vm.ReflectorGapUm);
        Assert.Equal(0.7, vm.Optics.CellPitchMm);
        Assert.Equal("0.7", vm.OpticsEditor.CellPitch);
        Assert.Equal("42", vm.SeedText);
        Assert.Equal((0d, "Cs-137", 1000d, 500d), (vm.Sources[0].X, vm.Sources[0].Isotope, vm.Sources[0].DistanceMm, vm.Sources[0].ActivityUCi));
        Assert.Equal(scintillator, vm.Scintillator);
        Assert.False(vm.CanEditInputs);
        Assert.False(vm.OpticsEditor.IsEditable);
        Assert.False(vm.Sources[0].IsEditable);
        Assert.False(vm.AddSourceCommand.CanExecute(null));
        vm.AddSourceCommand.Execute(null);
        Assert.Single(vm.Sources);
        Assert.Equal(calls, spectrum.Calls);
        Assert.Equal(1, acquisition.Starts);
        vm.Spectrum.WindowFwhm = 2; // a view setting stays editable
        await vm.Spectrum.WhenUpdated;
        Assert.Equal(7, spectrum.Settings[^1].Detector!.GainSeed);
        Assert.Equal(0.04, spectrum.Settings[^1].Detector!.GainSigma);
    }

    [Fact]
    public async Task Start_SnapshotsGrow_StopKeepsDataAndLocks_ContinueAccumulates()
    {
        var service = new Service();
        var vm = Model(service);
        vm.Imaging.Measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Roi,
            [new Vec2(-2, -2), new Vec2(2, 2)]));
        Assert.Equal(RunState.Empty, vm.State);
        Assert.Equal("Start", vm.StartLabel);
        var run = vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(RunState.Acquiring, vm.State);
        Assert.True(vm.IsRunning);
        Assert.False(vm.AddSourceCommand.CanExecute(null));
        Assert.False(vm.RemoveSourceCommand.CanExecute(null));
        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.False(vm.ResetCommand.CanExecute(null));
        Assert.False(vm.CanEditLiveTime);
        Assert.False(vm.CanEditSpeed);
        service.Session.AdvanceWallTime(0.25);
        await WaitForSnapshot(vm, 2.5);
        var first = vm.Snapshot!;
        Assert.Equal(25, first.Counts);
        Assert.Equal("Σ 25", vm.Imaging.Measurements.Items[0].Value);
        service.Session.AdvanceWallTime(0.25, mcLimited: true);
        await WaitForSnapshot(vm, 3.75);
        Assert.Contains("MC-limited ×5.00", vm.Status);
        Assert.Contains("seed 42", vm.Status);
        var last = vm.Snapshot!;
        vm.StopCommand.Execute(null);
        await run;
        Assert.Equal(RunState.Stopped, vm.State);
        Assert.StartsWith("Stopped · t = ", vm.Status);
        Assert.Contains($"{last.Counts:N0} counts · {last.ObservedRateCps:F0} cps · seed 42", vm.Status); // observed rate (L-10)
        Assert.Same(last, vm.Snapshot);
        Assert.Same(last.Imaging, vm.Result);
        Assert.False(vm.CanEditInputs);            // data exist: locked (A-2)
        Assert.False(vm.AddSourceCommand.CanExecute(null));
        Assert.True(vm.CanEditLiveTime);
        Assert.True(vm.CanEditSpeed);
        Assert.Equal("Continue", vm.StartLabel);
        Assert.True(vm.StartCommand.CanExecute(null));
        Assert.True(vm.ResetCommand.CanExecute(null));

        vm.Speed = 20;
        var resumed = vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(1, service.Starts);           // Continue, not a new acquisition
        Assert.Equal(1, service.Session.Continues);
        Assert.Equal(RunState.Acquiring, vm.State);
        service.Session.AdvanceWallTime(0.25);
        await WaitForSnapshot(vm, 8.75);
        Assert.True(vm.Snapshot!.Counts > last.Counts);
        Assert.Equal(last.Events, vm.Snapshot.Events.Take(last.Events.Count)); // accumulated onto the same events
        Assert.Equal($"Σ {vm.Snapshot.Counts}", vm.Imaging.Measurements.Items[0].Value);
        vm.StopCommand.Execute(null);
        await resumed;
        Assert.Equal(RunState.Stopped, vm.State);
    }

    [Fact]
    public async Task Completed_StartDisabledUntilPresetRaised_LowerPresetRejected_ProgressFollowsPreset()
    {
        var service = new Service();
        var vm = Model(service, liveTime: 5);
        vm.Speed = 10;
        var run = vm.StartCommand.ExecuteAsync(null);
        service.Session.AdvanceWallTime(0.5);
        await run;
        Assert.Equal(RunState.Completed, vm.State);
        Assert.Equal(1, vm.Progress);
        Assert.Equal(50, vm.Snapshot!.Counts);
        Assert.False(vm.StartCommand.CanExecute(null));
        await vm.StartCommand.ExecuteAsync(null); // the guard inside the command, not only CanExecute
        Assert.Equal(0, service.Session.Continues);
        vm.LiveTimeS = 4;
        Assert.Equal(5, vm.LiveTimeS);
        Assert.NotNull(vm.LiveTimeError);
        vm.LiveTimeS = 10;
        Assert.Null(vm.LiveTimeError);
        Assert.True(vm.StartCommand.CanExecute(null));
        var more = vm.StartCommand.ExecuteAsync(null);
        service.Session.AdvanceWallTime(0.25);
        await WaitForSnapshot(vm, 7.5);
        Assert.Equal(0.75, vm.Progress);           // relative to the current preset
        service.Session.AdvanceWallTime(0.25);
        await more;
        Assert.Equal(RunState.Completed, vm.State);
        Assert.Equal(100, vm.Snapshot!.Counts);
        Assert.Equal(1, service.Starts);
    }

    [Fact]
    public async Task Reset_DiscardsData_UnlocksInputs_NextStartIsANewAcquisitionWithANewSeed()
    {
        var service = new Service();
        var vm = new MainViewModel(service, new Theme(), new FakeSpectrumService()) { LiveTimeS = 5, AmbientDoseRateMicroSvPerHour = 0 };
        vm.Imaging.Measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Roi,
            [new Vec2(-2, -2), new Vec2(2, 2)]));
        Assert.False(vm.ResetCommand.CanExecute(null));
        var run = vm.StartCommand.ExecuteAsync(null);
        var firstSession = service.Session;
        firstSession.AdvanceWallTime(0.5);
        await run;
        int? firstSeed = vm.AcquisitionSeed;
        Assert.NotNull(firstSeed);
        Assert.Equal(firstSeed, service.Seeds[0]);
        await vm.ResetCommand.ExecuteAsync(null);
        Assert.True(firstSession.Disposed);
        Assert.Equal(RunState.Empty, vm.State);
        Assert.Equal("Ready", vm.Status);
        Assert.Null(vm.Snapshot);
        Assert.Null(vm.Result);
        Assert.Null(vm.AcquisitionSeed);
        Assert.Null(vm.Spectrum.View);
        Assert.Null(vm.Imaging.View);
        Assert.Empty(vm.Spectrum.Series);
        Assert.Equal(0, vm.Progress);
        Assert.True(vm.CanEditInputs);
        Assert.True(vm.Sources[0].IsEditable);
        Assert.True(vm.OpticsEditor.IsEditable);
        Assert.Single(vm.Imaging.Measurements.Items); // measurement shapes stay, as across Start
        vm.Sources[0].X = 10;
        Assert.Equal(10, vm.Sources[0].X);
        Assert.Equal("Start", vm.StartLabel);
        var again = vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(2, service.Starts);
        Assert.NotEqual(firstSeed, service.Seeds[1]); // independent measurement (author, E-8)
        service.Session.AdvanceWallTime(0.5);
        await again;
        Assert.Equal(service.Seeds[1], vm.Snapshot!.Seed);
        // A fixed seed reaches the service unchanged.
        await vm.ResetCommand.ExecuteAsync(null);
        vm.SeedText = "123";
        var fixedRun = vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(123, service.Seeds[2]);
        service.Session.AdvanceWallTime(0.5);
        await fixedRun;
        Assert.Equal(123, vm.AcquisitionSeed);
        Assert.Contains("seed 123", vm.Status);
        vm.SeedText = "x";
        Assert.Equal("123", vm.SeedText);           // locked with data
        await vm.ResetCommand.ExecuteAsync(null);
        vm.SeedText = "x";
        Assert.NotNull(vm.SeedError);
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(3, service.Starts);            // an invalid seed does not start
    }

    [Fact]
    public async Task Failure_WithData_KeepsItLocked_ResetOnly()
    {
        var service = new Service();
        var vm = Model(service);
        var run = vm.StartCommand.ExecuteAsync(null);
        service.Session.AdvanceWallTime(0.25);
        await WaitForSnapshot(vm, 2.5);
        var last = vm.Result;
        service.Session.Fail(new InvalidOperationException("MC failed"));
        await run;
        Assert.Equal(RunState.Failed, vm.State);
        Assert.Equal("Failed: MC failed", vm.Status);
        Assert.Same(last, vm.Result);
        Assert.True(service.Session.Disposed);
        Assert.False(vm.CanEditInputs);
        Assert.False(vm.CanEditLiveTime);
        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.True(vm.ResetCommand.CanExecute(null));
        await vm.ResetCommand.ExecuteAsync(null);
        Assert.Equal(RunState.Empty, vm.State);
        Assert.True(vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task Failure_WithoutData_BehavesAsEmpty()
    {
        var service = new Service();
        var vm = Model(service);
        var run = vm.StartCommand.ExecuteAsync(null);
        service.Session.Fail(new InvalidOperationException("no source"));
        await run;
        Assert.Equal(RunState.Failed, vm.State);
        Assert.Null(vm.Snapshot);
        Assert.True(vm.CanEditInputs);
        Assert.True(vm.StartCommand.CanExecute(null));
        Assert.False(vm.ResetCommand.CanExecute(null));
        Assert.Equal("Start", vm.StartLabel);
    }

    [Fact]
    public async Task Spectrum_SnapshotsGrow_ViewSettingsReuseAcquisition_SelectionSurvives()
    {
        var acquisition = new Service();
        var spectrum = new FakeSpectrumService();
        var vm = new MainViewModel(acquisition, new Theme(), spectrum) { LiveTimeS = 5, SeedText = "1", AmbientDoseRateMicroSvPerHour = 0 };
        vm.SelectWorkspaceCommand.Execute("1");
        var run = vm.StartCommand.ExecuteAsync(null);
        acquisition.Session.AdvanceWallTime(0.25);
        await WaitForSnapshot(vm, 2.5);
        await vm.Spectrum.WhenUpdated;
        Assert.Equal(25, vm.Spectrum.View!.TotalCounts);
        vm.Spectrum.SelectedLine = vm.Spectrum.Lines[0];
        var requested = vm.Spectrum.ViewRange;
        Assert.Equal(new Gcam.Studio.Core.Plotting.PlotViewRange(480, 840), requested);
        var series = Assert.Single(vm.Spectrum.Series);
        Assert.Equal(new double[] { 650, 675 }, series.BinEdges);
        var viewport = new Gcam.Studio.Core.Plotting.PlotViewport();
        viewport.Configure(series.EdgeAt(0), series.EdgeAt(series.Y.Length), 0, 25, false);
        viewport.ZoomAt(662, 2);
        var min = viewport.XMin;
        acquisition.Session.AdvanceWallTime(0.25);
        await run;
        Assert.Equal(50, vm.Spectrum.View!.TotalCounts);
        var updated = Assert.Single(vm.Spectrum.Series);
        viewport.Configure(updated.EdgeAt(0), updated.EdgeAt(updated.Y.Length), 0, 50, false);
        Assert.Equal(min, viewport.XMin);
        Assert.Same(requested, vm.Spectrum.ViewRange);
        Assert.Same(vm.Spectrum.Lines[0], vm.Spectrum.SelectedLine);
        Assert.Same(vm.Spectrum, vm.SelectedWorkspace);
        Assert.Equal("Workspace.Spectrum", vm.Spectrum.AutomationId);
        var snapshot = vm.Snapshot;
        int calls = spectrum.Calls;
        vm.Spectrum.WindowFwhm = 2;
        await vm.Spectrum.WhenUpdated;
        vm.Spectrum.PileUp = true;
        await vm.Spectrum.WhenUpdated;
        vm.Spectrum.LogY = false;
        Assert.Equal(calls + 2, spectrum.Calls);
        Assert.Equal(new SpectrumSettings(2, true), spectrum.Settings[^1]);
        Assert.Equal(1, acquisition.Starts);
        Assert.Same(snapshot, vm.Snapshot);
        Assert.Equal(50, vm.Spectrum.View.TotalCounts);
        Assert.Equal(Gcam.Studio.Core.Plotting.PlotKind.Histogram, Assert.Single(vm.Spectrum.Series).Kind);
        Assert.NotEmpty(vm.Spectrum.Bands);
    }

    [Fact]
    public async Task LiveTimeAndSpeed_LockedWhileAcquiring_InvalidValuesFallBack()
    {
        var service = new Service();
        var vm = Model(service, liveTime: 5);
        var run = vm.StartCommand.ExecuteAsync(null);
        vm.LiveTimeS = 30; vm.Speed = 3;
        Assert.Equal(5, vm.LiveTimeS);
        Assert.Equal(10, vm.Speed);
        service.Session.AdvanceWallTime(0.5);
        await run;
        vm.Speed = 20;
        Assert.Equal(20, vm.Speed);
        vm.LiveTimeS = double.NaN;
        vm.Speed = 0;
        Assert.Equal(60, vm.LiveTimeS);
        Assert.Equal(10, vm.Speed);
    }
}
