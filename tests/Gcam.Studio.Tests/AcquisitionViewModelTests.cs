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

    // A virtual acquisition clock: advancing wall time publishes physical data at the requested speed.
    private sealed class VirtualSession(double preset, double speed) : IAcquisitionSession
    {
        private readonly Channel<AcquisitionSnapshot> _channel = Channel.CreateUnbounded<AcquisitionSnapshot>();
        private readonly List<DetectedEvent> _events = [];
        private double _live;
        private readonly DetectorImage _flood = new(4, 4);
        public void AdvanceWallTime(double wallTime, bool mcLimited = false)
        {
            _live = Math.Min(preset, _live + wallTime * (mcLimited ? speed / 2 : speed));
            int count = (int)(_live * 10);
            while (_events.Count < count)
            {
                _events.Add(new DetectedEvent(1, 1, 661.7, (_events.Count + 1) / 10.0));
                _flood.Add(1, 1, 1);
            }
            var image = new ImagingResult(_flood.ReadOnlyCopy(), -1.5, 1, null, 0, 0,
                new SourceEstimate(new Vector3(0, 0, 1000), 2), count, TimeSpan.FromSeconds(wallTime));
            _channel.Writer.TryWrite(new AcquisitionSnapshot(_live, count, 10, mcLimited ? speed / 2 : speed,
                mcLimited, image, Array.AsReadOnly(_events.ToArray()), TimeSpan.Zero, _live >= preset));
            if (_live >= preset) _channel.Writer.TryComplete();
        }
        public IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync(CancellationToken cancellationToken = default)
            => _channel.Reader.ReadAllAsync(cancellationToken);
        public void Stop() => _channel.Writer.TryComplete();
        public ValueTask DisposeAsync() { Stop(); return ValueTask.CompletedTask; }
        public void Fail(Exception error) => _channel.Writer.TryComplete(error);
    }

    private sealed class Service : IAcquisitionService
    {
        public int Starts { get; private set; }
        public VirtualSession Session { get; private set; } = null!;
        public DetectorSettings? Detector { get; private set; }
        public double BackgroundToSignalRatio { get; private set; }
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS, double speed,
            DetectorSettings? detector = null, double backgroundToSignalRatio = 0)
        {
            Detector = detector;
            BackgroundToSignalRatio = backgroundToSignalRatio;
            Starts++;
            return Session = new VirtualSession(liveTimeS, speed);
        }
    }

    [Fact]
    public async Task Start_CapturesDetectorInputs_AndEditingMarksOnlyTheResultStale()
    {
        var acquisition = new Service();
        var spectrum = new FakeSpectrumService();
        var vm = new MainViewModel(acquisition, new Theme(), spectrum)
            { LiveTimeS = 5, GainSigmaPercent = 4, GainSeed = 7, BackgroundToSignalRatio = 1 };
        var run = vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(0.04, acquisition.Detector!.GainSigma);
        Assert.Equal(7, acquisition.Detector.GainSeed);
        Assert.Equal(1, acquisition.BackgroundToSignalRatio);
        acquisition.Session.AdvanceWallTime(0.5);
        await run;
        vm.Snapshot = vm.Snapshot! with { Detector = acquisition.Detector };
        await vm.Spectrum.WhenUpdated;
        int calls = spectrum.Calls;
        vm.GainSeed = 9;
        Assert.True(vm.IsResultStale);
        Assert.Equal(7, acquisition.Detector.GainSeed);
        Assert.Equal(calls, spectrum.Calls);
        Assert.Equal(1, acquisition.Starts);
        vm.Spectrum.WindowFwhm = 2;
        await vm.Spectrum.WhenUpdated;
        Assert.Equal(7, spectrum.Settings[^1].Detector!.GainSeed);
        Assert.Equal(0.04, spectrum.Settings[^1].Detector!.GainSigma);
    }

    private static async Task WaitForSnapshot(MainViewModel vm, double live)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (vm.Snapshot?.LiveTimeS != live) await Task.Delay(1, timeout.Token);
    }

    [Fact]
    public async Task Start_SnapshotsGrow_StopKeepsData_UnlocksAndEditMarksStale()
    {
        var service = new Service();
        var vm = new MainViewModel(service, new Theme(), new FakeSpectrumService());
        vm.Imaging.Measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Roi,
            [new Vec2(-2, -2), new Vec2(2, 2)]));
        var run = vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(RunState.Acquiring, vm.State);
        Assert.True(vm.IsRunning);
        Assert.False(vm.AddSourceCommand.CanExecute(null));
        Assert.False(vm.RemoveSourceCommand.CanExecute(null));
        Assert.False(vm.StartCommand.CanExecute(null));
        service.Session.AdvanceWallTime(0.25);
        await WaitForSnapshot(vm, 2.5);
        var first = vm.Snapshot!;
        Assert.Equal(25, first.Counts);
        Assert.Equal("Σ 25", vm.Imaging.Measurements.Items[0].Value);
        service.Session.AdvanceWallTime(0.25, mcLimited: true);
        await WaitForSnapshot(vm, 3.75);
        Assert.True(vm.Snapshot!.Counts >= first.Counts);
        Assert.Contains("MC-limited ×5.00", vm.Status);
        var last = vm.Snapshot;
        vm.StopCommand.Execute(null);
        await run;
        Assert.Equal(RunState.Stopped, vm.State);
        Assert.Same(last, vm.Snapshot);
        Assert.Same(last.Imaging, vm.Result);
        Assert.True(vm.IsIdle);
        Assert.True(vm.AddSourceCommand.CanExecute(null));
        Assert.False(vm.IsResultStale);
        vm.Sources[0].X = 10;
        Assert.True(vm.IsResultStale);
        Assert.Equal(25, first.Counts);
        Assert.Equal(25, first.Imaging.Flood.Raw.ToArray().Sum());
    }

    [Fact]
    public async Task Preset_Completes_NewStartClears_ResultAndEvents()
    {
        var service = new Service();
        var vm = new MainViewModel(service, new Theme(), new FakeSpectrumService()) { LiveTimeS = 5, Speed = 10 };
        var run = vm.StartCommand.ExecuteAsync(null);
        service.Session.AdvanceWallTime(0.5);
        await run;
        Assert.Equal(RunState.Completed, vm.State);
        Assert.Equal(1, vm.Progress);
        Assert.Equal(50, vm.Snapshot!.Counts);
        var again = vm.StartCommand.ExecuteAsync(null);
        Assert.Null(vm.Result);
        Assert.Null(vm.Snapshot);
        Assert.False(vm.IsResultStale);
        vm.StopCommand.Execute(null);
        await again;
        Assert.Equal(RunState.Stopped, vm.State);
        Assert.Null(vm.Snapshot);
    }

    [Fact]
    public async Task Failure_ReportsMessage_KeepsAcquiredData()
    {
        var service = new Service();
        var vm = new MainViewModel(service, new Theme(), new FakeSpectrumService());
        var run = vm.StartCommand.ExecuteAsync(null);
        service.Session.AdvanceWallTime(0.25);
        await WaitForSnapshot(vm, 2.5);
        var last = vm.Result;
        service.Session.Fail(new InvalidOperationException("MC failed"));
        await run;
        Assert.Equal(RunState.Failed, vm.State);
        Assert.Equal("Failed: MC failed", vm.Status);
        Assert.Same(last, vm.Result);
        Assert.True(vm.IsIdle);
    }

    [Fact]
    public async Task Spectrum_SnapshotsGrow_ViewSettingsReuseAcquisition_SelectionSurvives()
    {
        var acquisition = new Service();
        var spectrum = new FakeSpectrumService();
        var vm = new MainViewModel(acquisition, new Theme(), spectrum) { LiveTimeS = 5 };
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
        Assert.False(vm.IsResultStale);
        Assert.Equal(50, vm.Spectrum.View.TotalCounts);
        Assert.Equal(Gcam.Studio.Core.Plotting.PlotKind.Histogram, Assert.Single(vm.Spectrum.Series).Kind);
        Assert.NotEmpty(vm.Spectrum.Bands);
    }

    [Fact]
    public async Task LiveTimeAndSpeed_MarkStale_WorkspaceAndMeasurementsDoNot()
    {
        var service = new Service();
        var vm = new MainViewModel(service, new Theme(), new FakeSpectrumService()) { LiveTimeS = 5 };
        var run = vm.StartCommand.ExecuteAsync(null);
        service.Session.AdvanceWallTime(0.5);
        await run;
        vm.SelectWorkspaceCommand.Execute("0");
        vm.Imaging.Measurements.ActiveTool = MeasureTool.Distance;
        Assert.False(vm.IsResultStale);
        vm.LiveTimeS = 10;
        Assert.True(vm.IsResultStale);
        vm.IsResultStale = false;
        vm.Speed = 20;
        Assert.True(vm.IsResultStale);
        vm.LiveTimeS = double.NaN;
        vm.Speed = 0;
        Assert.Equal(60, vm.LiveTimeS);
        Assert.Equal(10, vm.Speed);
    }
}
