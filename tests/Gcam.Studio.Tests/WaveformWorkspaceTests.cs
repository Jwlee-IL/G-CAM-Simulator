using System.Runtime.CompilerServices;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Plotting;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;
using Xunit;

namespace Gcam.Studio.Tests;

public sealed class WaveformWorkspaceTests
{
    [Fact]
    public async Task CsI_IsOfferedAndRequiresResetForANewAcquisition()
    {
        var model = new MainViewModel(new Acquisition(), new Theme(), new Spectrum()) { AmbientDoseRateMicroSvPerHour = 0 };
        var csi = Assert.Single(model.Scintillators, s => s.Name == "CsI(Tl)");
        var gagg = model.Scintillator;
        Assert.Equal("GAGG(Ce)", gagg.Name);
        await model.StartCommand.ExecuteAsync(null);
        model.Scintillator = csi;
        Assert.Same(gagg, model.Scintillator);
        Assert.Same(gagg, model.Snapshot!.Chain.Scintillator);
        await model.ResetCommand.ExecuteAsync(null);
        model.Scintillator = csi;
        await model.StartCommand.ExecuteAsync(null);
        Assert.Same(csi, model.Snapshot!.Chain.Scintillator);
        model.Scintillator = gagg;
        Assert.Same(csi, model.Scintillator);
    }

    private static AcquisitionSnapshot Snapshot(double live = 2)
    {
        var flood = new DetectorImage(30, 30);
        var image = new ImagingResult(flood.ReadOnlyCopy(), -8.7, .6, null, 0, 0, null, 2, TimeSpan.Zero);
        return new(live, 2, 1, 1, false, image, new[] { new DetectedEvent(1, 2, 662, 1), new DetectedEvent(3, 4, 32, 1.5) }, TimeSpan.Zero, true)
        { Detector = new DetectorSettings() };
    }

    [Theory]
    [InlineData(10, 1250, false)]
    [InlineData(.001, 1, false)]
    [InlineData(100000, 9999000, true)]
    public void Window_CapsTotalWorkAndKeepsPretrigger(double us, int samples, bool clipped)
    {
        var window = ScopeWindow.Create(us, 1000);
        Assert.Equal(samples, window.Samples); Assert.Equal(clipped, window.Clipped);
        Assert.True(window.Samples + window.WarmupSamples <= PlotSeries.MaximumSamples);
        Assert.Equal(samples / 125.0 * .2, window.PretriggerUs);
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(0)] [InlineData(-1)]
    public void Window_RejectsInvalidLength(double us)
        => Assert.Throws<ArgumentOutOfRangeException>(() => ScopeWindow.Create(us, 1000));

    [Fact]
    public void TimeConversion_IsCheckedAndRelative()
    {
        Assert.Equal(125, ScopeWindow.RelativeSample(1e-6, 0));
        Assert.Equal(-125, ScopeWindow.RelativeSample(0, 1e-6));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScopeWindow.RelativeSample(double.PositiveInfinity, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScopeWindow.RelativeSample(1e20, 0));
    }

    [Fact]
    public void PreparedPlot_RejectsAnotherArraysPyramid()
    {
        var y = new double[] { 1, 2 };
        var prepared = new MinMaxPyramid(y);
        (new PlotSeries("Prepared", y) { PreparedPyramid = prepared }).Validate();
        Assert.Throws<ArgumentException>(() => (new PlotSeries("Wrong", [1, 2]) { PreparedPyramid = prepared }).Validate());
    }

    [Fact]
    public async Task Chain_LockedWithData_ScopeUsesAcquiredChain()
    {
        var acquisition = new Acquisition(); var waveform = new Scope();
        var model = new MainViewModel(acquisition, new Theme(), new Spectrum(), waveform: waveform) { AmbientDoseRateMicroSvPerHour = 0 };
        await model.StartCommand.ExecuteAsync(null);
        var acquired = model.Snapshot!.Chain;
        model.Preamp = FrontEndParts.Preamps[3]; // locked while data exist (A-2): refused
        Assert.Equal(acquired, model.Chain);
        Assert.Equal(acquired, model.Snapshot.Chain);
        model.SelectedWorkspace = model.Waveform;
        await model.Waveform.WhenUpdated;
        Assert.Equal(acquired, waveform.Calls.Last().Snapshot.Chain);
        model.Waveform.Ideal = true; model.Waveform.RateStudy = true;
        await model.Waveform.WhenUpdated;
        Assert.Same(acquisition.Published, model.Snapshot);
        await model.ResetCommand.ExecuteAsync(null);
        model.Preamp = FrontEndParts.Preamps[3];
        Assert.Equal(FrontEndParts.Preamps[3], model.Chain.Preamp);
    }

    [Fact]
    public async Task ActiveAcquisition_DisablesPhysicalChainChanges()
    {
        var acquisition = new Acquisition { Hold = true };
        var model = new MainViewModel(acquisition, new Theme(), new Spectrum()) { AmbientDoseRateMicroSvPerHour = 0 };
        var run = model.StartCommand.ExecuteAsync(null);
        Assert.True(model.IsRunning);
        var chain = model.Chain;
        model.Preamp = FrontEndParts.Preamps[3];
        model.Scintillator = FrontEndMaterials.Scintillators.Single(s => s.Name == "CsI(Tl)");
        model.Sensor = FrontEndParts.Sensors[2];
        Assert.Equal(chain, model.Chain);
        acquisition.Release.TrySetResult(); await run;
        Assert.False(model.IsRunning);
    }

    [Fact]
    public async Task HiddenScope_DoesNotGenerateAndHeldTriggerReusesWindow()
    {
        var acquisition = new Acquisition(); var waveform = new Scope();
        var model = new MainViewModel(acquisition, new Theme(), new Spectrum(), waveform: waveform) { AmbientDoseRateMicroSvPerHour = 0 };
        await model.StartCommand.ExecuteAsync(null);
        Assert.Empty(waveform.Calls);
        model.SelectedWorkspace = model.Waveform; await model.Waveform.WhenUpdated;
        model.Waveform.FollowLatest = false; model.Waveform.TriggerIndex = 0;
        await model.Waveform.WhenUpdated;
        int calls = waveform.Calls.Count;
        model.Snapshot = model.Snapshot! with { LiveTimeS = 3 };
        await model.Waveform.WhenUpdated;
        Assert.Equal(calls, waveform.Calls.Count);
        model.Waveform.NextCommand.Execute(null); await model.Waveform.WhenUpdated;
        Assert.Equal(1, model.Waveform.TriggerIndex);
        model.SelectedWorkspace = model.Imaging;
        model.Waveform.WindowUs = 20;
        Assert.Equal(calls + 1, waveform.Calls.Count);
    }

    [Fact]
    public async Task LatestSelectionWinsAndNewAcquisitionCancelsOldScope()
    {
        var acquisition = new Acquisition(); var scope = new Scope { Delay = true };
        var model = new MainViewModel(acquisition, new Theme(), new Spectrum(), waveform: scope) { AmbientDoseRateMicroSvPerHour = 0 };
        await model.StartCommand.ExecuteAsync(null);
        model.SelectedWorkspace = model.Waveform;
        var oldTask = model.Waveform.WhenUpdated;
        var old = scope.Calls.Last();
        model.Waveform.WindowUs = 20;
        var latest = scope.Calls.Last(); latest.Completion.SetResult(Scope.View("latest"));
        await model.Waveform.WhenUpdated;
        old.Completion.SetResult(Scope.View("old")); await oldTask;
        Assert.Equal("latest", model.Waveform.View!.Note);
        model.Waveform.WindowUs = 30;
        var obsolete = scope.Calls.Last(); var obsoleteTask = model.Waveform.WhenUpdated;
        await model.ResetCommand.ExecuteAsync(null); // a new acquisition needs Reset first
        var run = model.StartCommand.ExecuteAsync(null);
        var current = scope.Calls.Last(); current.Completion.SetResult(Scope.View("new acquisition"));
        await model.Waveform.WhenUpdated; await run;
        obsolete.Completion.SetResult(Scope.View("obsolete acquisition")); await obsoleteTask;
        Assert.True(obsolete.Token.IsCancellationRequested);
        Assert.Equal("new acquisition", model.Waveform.View!.Note);
    }

    [Fact]
    public async Task ScopeFailure_IsVisibleAndLocalControlsKeepTheAcquisition()
    {
        var acquisition = new Acquisition(); var waveform = new Scope { Fail = true };
        var model = new MainViewModel(acquisition, new Theme(), new Spectrum(), waveform: waveform) { AmbientDoseRateMicroSvPerHour = 0 };
        await model.StartCommand.ExecuteAsync(null);
        model.SelectedWorkspace = model.Waveform; await model.Waveform.WhenUpdated;
        Assert.Contains("test failure", model.Waveform.Error);
        model.Waveform.RateStudy = true; model.Waveform.Ideal = true; model.Waveform.WindowUs = 100;
        await model.Waveform.WhenUpdated;
        Assert.Same(acquisition.Published, model.Snapshot);
        Assert.NotEqual(RunState.Failed, model.State); // a scope error is the workspace's, not the acquisition's
    }

    private sealed class Theme : IThemeService
    {
        public AppTheme Current => AppTheme.Dark;
        public void Apply(AppTheme theme) { }
    }
    private sealed class Spectrum : ISpectrumService
    {
        public double Resolution662 => .04;
        public double ResolvingTimeS => 730e-9;
        public Task<SpectrumView> ProcessAsync(Guid id, IReadOnlyList<DetectedEvent> events, IReadOnlyList<SpectrumLine> lines,
            SpectrumSettings settings, int seed = 909, CancellationToken cancellationToken = default)
            => Task.FromResult(new SpectrumView([], [], [], events.Count, 0, 0, .04, 730e-9, "fixture", TimeSpan.Zero));
    }
    private sealed class Acquisition : IAcquisitionService
    {
        public bool Hold { get; init; }
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public AcquisitionSnapshot? Published { get; private set; }
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS, double speed,
            DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
        {
            Published = Snapshot() with { Detector = detector };
            return new Session(Published, Hold ? Release.Task : Task.CompletedTask);
        }
    }
    private sealed class Session(AcquisitionSnapshot snapshot, Task release) : IAcquisitionSession
    {
        public void Stop() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public async IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        { yield return snapshot; await release; }
    }
    private sealed class Scope : IWaveformService
    {
        public bool Delay { get; init; }
        public bool Fail { get; init; }
        public List<(AcquisitionSnapshot Snapshot, WaveformSettings Settings, CancellationToken Token,
            TaskCompletionSource<WaveformView> Completion)> Calls { get; } = [];
        public Task<WaveformView> ProcessAsync(AcquisitionSnapshot snapshot, WaveformSettings settings, CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<WaveformView>(TaskCreationOptions.RunContinuationsAsynchronously);
            Calls.Add((snapshot, settings, cancellationToken, completion));
            if (Fail) completion.SetException(new InvalidOperationException("test failure"));
            else if (!Delay) completion.SetResult(View("fixture"));
            return completion.Task;
        }
        public static WaveformView View(string note) => new(new("ADC", [0, 1]), new("shaped", [0, 1]), [], "chain", "pulse", note, 10, TimeSpan.Zero);
    }
}
