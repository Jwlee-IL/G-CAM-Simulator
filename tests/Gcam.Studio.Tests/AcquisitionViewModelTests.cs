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

    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = [];
        public override void Post(SendOrPostCallback callback, object? state) => _callbacks.Enqueue((callback, state));
        public void Drain()
        {
            while (_callbacks.TryDequeue(out var item)) item.Callback(item.State);
        }
    }

    private sealed class BatchService : ISimulationService
    {
        private int _runs;
        public static ImagingResult Image => new(new DetectorImage(4, 4), -1.5, 1, null, 0, 0, null, 0, TimeSpan.Zero);
        public TaskCompletionSource<ImagingResult> NextResult { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ImagingResult> RunAsync(IReadOnlyList<SceneSource> scene, OpticsSettings optics, long photons,
            IProgress<double>? progress, CancellationToken cancellationToken)
        {
            if (_runs++ != 0) return NextResult.Task;
            progress?.Report(0.5);
            return Task.FromResult(Image);
        }
    }

    [Fact]
    public async Task LegacyBatch_LateReportCannotOverwriteANewSessionProgress()
    {
        var service = new BatchService();
        var vm = new MainViewModel(service, new Theme());
        var queue = new QueuedContext();
        var original = SynchronizationContext.Current;
        Task first;
        try
        {
            SynchronizationContext.SetSynchronizationContext(queue);
            first = vm.RunCommand.ExecuteAsync(null);
        }
        finally { SynchronizationContext.SetSynchronizationContext(original); }
        await first;
        Assert.Equal(1, vm.Progress);
        var second = vm.RunCommand.ExecuteAsync(null);
        Assert.True(vm.IsRunning);
        Assert.Equal(0, vm.Progress);
        queue.Drain(); // Deliver the previous run's report while the next run is active.
        Assert.Equal(0, vm.Progress);
        service.NextResult.SetResult(BatchService.Image);
        await second;
        Assert.Equal(1, vm.Progress);
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

    private sealed class Service : ISimulationService, IAcquisitionService
    {
        public VirtualSession Session { get; private set; } = null!;
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS, double speed)
            => Session = new VirtualSession(liveTimeS, speed);
        public Task<ImagingResult> RunAsync(IReadOnlyList<SceneSource> scene, OpticsSettings optics, long photons,
            IProgress<double>? progress, CancellationToken cancellationToken) => throw new NotSupportedException();
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
        var vm = new MainViewModel(service, new Theme());
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
        var vm = new MainViewModel(service, new Theme()) { LiveTimeS = 5, Speed = 10 };
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
        var vm = new MainViewModel(service, new Theme());
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
    public async Task LiveTimeAndSpeed_MarkStale_WorkspaceAndMeasurementsDoNot()
    {
        var service = new Service();
        var vm = new MainViewModel(service, new Theme()) { LiveTimeS = 5 };
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
