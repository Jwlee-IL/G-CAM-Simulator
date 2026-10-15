using System.Threading.Channels;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

/// <summary>Headless scheduling, acquisition identity, empty states, and pure benchmark differences.</summary>
public sealed class CompareWorkspaceTests
{
    private sealed class Acquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS, double speed,
            DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null) => throw new InvalidOperationException("No acquisition in these fixtures.");
    }
    private sealed class Theme : IThemeService { public AppTheme Current => AppTheme.Dark; public void Apply(AppTheme theme) { } }
    private sealed class Deferred : IReadoutCompareService
    {
        public sealed record Call(CompareKey Key, AcquisitionSnapshot Snapshot, CompareSettings Settings, CancellationToken Token,
            TaskCompletionSource<CompareResult> Completion);
        public Channel<Call> Calls { get; } = Channel.CreateUnbounded<Call>();
        public Task<CompareResult> ProcessAsync(CompareKey key, AcquisitionSnapshot snapshot, IReadOnlyList<SceneSource> scene,
            CompareSettings settings, CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<CompareResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Calls.Writer.TryWrite(new(key, snapshot, settings, cancellationToken, completion)); return completion.Task;
        }
        public async Task<Call> Next() => await Calls.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
    private static ReadoutPreparation Preparation() => new(Guid.NewGuid(), "fixture", true, null, new(2, 2), [], [], [],
        0, 0, 0, 0, TimeSpan.Zero, 1, 2, 1, 10, 50);
    private static AcquisitionSnapshot Snapshot(double time = 1)
    {
        var image = new ImagingResult(new(12, 12), -5.5, 1, null, 0, 0, null, 0, TimeSpan.Zero);
        return new(time, 0, 0, 1, false, image, [], TimeSpan.Zero, true)
        { Optics = new(), Detector = new() { ReadoutMode = ReadoutMode.FourOutputAnger }, Readout = new(Preparation(), [], [], 0, false) };
    }
    private static MainViewModel Model(Deferred service) => new(new Acquisition(), new Theme(), new FakeSpectrumService(), compare: service);
    private static CompareResult Result(Deferred.Call call) => new(call.Key, call.Settings,
        new("Direct", 600, 720, call.Snapshot.Imaging, []), new("Anger", 600, 720, call.Snapshot.Imaging, []),
        new(new(12, 12), [], []), new([0, 2], [0], [0], 0, 0, 661.7, null), new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
        new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), new(null, [], "fixture"));

    [Fact]
    public void FifthWorkspace_AndPhysicalEmptyStates_AreExplicit()
    {
        var service = new Deferred(); var model = Model(service);
        Assert.Same(model.Compare, model.Workspaces[4]); model.SelectWorkspaceCommand.Execute("4");
        Assert.Same(model.Compare, model.SelectedWorkspace); Assert.True(model.Compare.ShowDirectMode);
        model.ReadoutMode = ReadoutMode.FourOutputAnger;
        Assert.True(model.Compare.ShowStart); Assert.False(model.Compare.ShowData);
        Assert.False(service.Calls.Reader.TryRead(out _));
        model.ChooseReadoutCommand.Execute(null); Assert.Equal(MainViewModel.DetectorTab, model.SelectedLeftTab);
    }

    [Fact]
    public async Task DifferenceToggle_KeepsEstimates_AndNamesFoundPeaksByIsotope()
    {
        var service = new Deferred(); var model = Model(service);
        model.ReadoutMode = ReadoutMode.FourOutputAnger;
        // The ideal direct channel has no calibration metadata of its own. Both paths name the shared window's line.
        model.Sources.Add(new() { Isotope = "Co-60" });
        var snapshot = Snapshot();
        model.Imaging.View = new([new("All", 0, 0, snapshot.Imaging, []), new("Energy window", 600, 720, snapshot.Imaging, [])],
            [], TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        model.Imaging.SelectedIsotope = "Energy window";
        model.SelectedWorkspace = model.Compare; model.Snapshot = snapshot;
        var call = await service.Next();
        var result = Result(call);
        var image = call.Snapshot.Imaging with { Estimate = new(new(1, 2, 1000), .9) };
        result = result with { Direct = result.Direct with { Image = image }, Anger = result.Anger with { Image = image } };
        call.Completion.SetResult(result); await model.Compare.WhenUpdated;
        Assert.Equal("Cs-137", Assert.Single(model.Compare.DirectPeaks).Isotope);
        Assert.Equal("Cs-137", Assert.Single(model.Compare.AngerPeaks).Isotope);
        model.Compare.ShowDifference = true;
        Assert.True(model.Compare.ShowAssignment); Assert.False(model.Compare.ShowPair);
        Assert.Same(result, model.Compare.Result);
        Assert.Same(image.Estimate, model.Compare.Result!.Direct.Image.Estimate);
        Assert.Same(image.Estimate, model.Compare.Result.Anger.Image.Estimate);
    }

    [Fact]
    public async Task InactiveAndLatestPending_UseOneWorker_AndPublishAtomicPrefixes()
    {
        var service = new Deferred(); var model = Model(service); model.Snapshot = Snapshot();
        Assert.False(service.Calls.Reader.TryRead(out _));
        model.SelectedWorkspace = model.Compare;
        var first = await service.Next();
        model.Snapshot = Snapshot(2); model.Snapshot = Snapshot(3);
        Assert.False(service.Calls.Reader.TryRead(out _));
        first.Completion.SetResult(Result(first));
        var last = await service.Next(); Assert.Equal(3, last.Key.LiveTimeS);
        last.Completion.SetResult(Result(last)); await model.Compare.WhenUpdated;
        Assert.Equal(last.Key, model.Compare.Result!.Key);
        Assert.False(model.Compare.IsUpdating); Assert.Contains("3 s", model.Compare.Prefix);
        Assert.Contains("No held", model.Compare.EmptyReason); Assert.Null(model.Compare.Result.Accounting.SameShare);
        model.SelectedWorkspace = model.Imaging; model.Snapshot = Snapshot(4);
        Assert.False(service.Calls.Reader.TryRead(out _));
    }

    [Fact]
    public async Task SettingsAndReset_RejectOldResults_EvenWhenServiceIgnoresCancellation()
    {
        var service = new Deferred(); var model = Model(service); model.SelectedWorkspace = model.Compare; model.Snapshot = Snapshot();
        var old = await service.Next(); var oldTask = model.Compare.WhenUpdated;
        model.WindowLowKeV = 590; var current = await service.Next();
        Assert.True(old.Token.IsCancellationRequested);
        old.Completion.SetResult(Result(old)); await oldTask; Assert.Null(model.Compare.Result);
        current.Completion.SetResult(Result(current)); await model.Compare.WhenUpdated;
        Assert.Equal(590, model.Compare.Result!.Settings.Imaging.WindowLowKeV);
        var shown = model.Compare.Result;
        model.Snapshot = Snapshot(2); var pending = await service.Next(); var task = model.Compare.WhenUpdated;
        model.Snapshot = null; pending.Completion.SetResult(Result(pending)); await task;
        Assert.Same(shown, model.Compare.Result); Assert.False(model.Compare.ShowData);
        Assert.True(model.Compare.Spectrum[1].Dashed);
        Assert.Equal(590, model.Compare.Bands[0].Lo);
    }

    [Fact]
    public void ReportCapture_KeepsPhysicalDirectFactsOutOfLegacyEvents_AndDeclaresCompareOmission()
    {
        var model = Model(new Deferred()); var snapshot = Snapshot();
        model.Snapshot = snapshot with { ConfigurationJson = "{}", Readout = snapshot.Readout! with
            { Hits = [new(.1, new(), 2, 3, 661.7)] } };
        var report = SimulationReport.Capture(model, DateTimeOffset.UnixEpoch, "fixture");
        Assert.Empty(report.Events); Assert.Null(report.MeasuredKeV);
        Assert.Empty(report.ReadoutRecords); Assert.Single(model.Snapshot.Readout!.Hits);
        Assert.Contains(report.Scene, row => row.Label == "Report scope" && row.Value == "Compare workspace is not included.");
    }

    [Fact]
    public void CostApplicability_UsesCapturedModuleAndListsSceneDifferences_WithoutPromotingReferenceToPrediction()
    {
        var record = ReadoutOfferFixture.Record();
        var optics = new OpticsSettings { DetectorPixels = 12, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
        var prep = Preparation() with { Plan = ReadoutPolicy.Plan(optics, new(), 661.7, record) };
        var settings = new CompareSettings(new() { WindowLowKeV = 562.445, WindowHighKeV = 760.955 }, true);
        var current = ReadoutCostApplicability.Evaluate(record, prep, [new()], optics, new(), settings, 40, false, false);
        Assert.Same(prep.Plan.Cell, current.Cell); Assert.True(current.ReferenceOnly);
        Assert.Contains(current.Differences, d => d.StartsWith("Isotope / line"));
        Assert.Contains(current.Differences, d => d.StartsWith("Position")); Assert.Contains(current.Differences, d => d.StartsWith("Rate"));
        Assert.DoesNotContain(current.Differences, d => d.StartsWith("Distance"));
        var changed = ReadoutCostApplicability.Evaluate(record, prep, [new() { X = 15, DistanceMm = 800 }], optics with { MuraRank = 11 },
            new() { ReflectorGapMm = .2 }, settings, 500, true, true);
        Assert.Contains(changed.Differences, d => d.StartsWith("Module")); Assert.Contains(changed.Differences, d => d.StartsWith("Distance"));
        Assert.Contains(changed.Differences, d => d.StartsWith("Ambient"));
        var local = ReadoutCostApplicability.Evaluate(record, prep with { Plan = null }, [new()], optics, new(), settings, 1, false, false);
        Assert.Null(local.Cell); Assert.Contains("unmeasured", local.Unavailable);
        var missing = ReadoutCostApplicability.Evaluate(ReadoutOfferRecord.Unavailable("missing"), prep, [new()], optics, new(), settings, 0, false, false);
        Assert.Equal("missing", missing.Unavailable);
    }
}
