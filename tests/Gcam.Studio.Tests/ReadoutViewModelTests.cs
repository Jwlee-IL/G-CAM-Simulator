using Gcam.Configuration;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

/// <summary>TODO-41 per-line policy in the ViewModel: offers and refusals come only from the record and the module
/// fingerprint; the scene decides which lines an acquisition needs; refused and incidental lines never block; Custom
/// inherits by module; the cost card carries the exact F wording and the data's own bias explanation.</summary>
public sealed class ReadoutViewModelTests
{
    [Fact]
    public void WideFovLocalisationHelp_ShippedRecordSupportsDesktopWording_AndRejectsBrokenVerdict()
    {
        var record = Gcam.Studio.Services.ReadoutOfferRecordLoader.LoadEmbedded();
        Assert.True(record.IsAvailable, record.UnavailableReason);
        var optics = Gcam.Studio.Core.Optics.OpticsPreset.All.Single(p => p.Name == "Wide FOV").Settings!;
        var detector = new DetectorSettings { ReadoutMode = ReadoutMode.FourOutputAnger };
        var plans = ReadoutPolicy.Plans(optics, detector, record);
        Assert.Contains(plans, p => p.Status == ReadoutLineStatus.Offered && p.Cell!.CostRows.Any(r =>
            r.F is { Estimate: < 1 } && r.Direct.RmsDeg is not null && r.Direct.BiasDeg is not null));
        string help = ReadoutCostHelp.Build(record, plans);
        Assert.Contains("F is an equal-fluence MSE ratio, not a time factor.", help);
        Assert.Contains("F below 1 is chiefly direct-reference bias", help);
        Assert.Contains("Benchmark: one mono-line point source", help);
        Assert.DoesNotContain("wrong benchmark wording", help);
    }

    private static readonly OpticsSettings Head = new() { DetectorPixels = 12, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
    private static readonly OpticsSettings High = new() { DetectorPixels = 44, PixelPitchMm = .4, MuraRank = 13, CellPitchMm = .5, MaskDetectorDistanceMm = 100 };

    private static MainViewModel Model(IReadoutPreparationService? preparation = null, IAcquisitionService? acquisition = null)
        => new(acquisition ?? new RefusingAcquisition(), new Theme(), new FakeSpectrumService(), readoutPreparation: preparation ?? new FakeReadoutPreparation());

    [Fact]
    public void AnyValidGeometry_SelectsPhysicalReadout_WithPerLinePlansAndLegacyLocks()
    {
        var vm = Model(); vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        Assert.True(vm.IsPhysicalReadout); // the default Sharp head is no longer refused by a geometry gate
        // Sharp is absent from this fixture record: an unmatched module calibrates locally (SD-A10).
        Assert.All(vm.ReadoutPlans, p => Assert.Equal(ReadoutLineStatus.Local, p.Status));
        vm.Optics = Head;
        var plans = vm.ReadoutPlans;
        Assert.Equal(new[] { 122.06, 661.7, 1332.5 }, plans.Select(p => p.LineKeV));
        Assert.Equal(ReadoutLineStatus.Refused, plans[0].Status);
        Assert.Contains("fixture: no resolved spot structure", plans[0].Reason);
        Assert.Contains("1 / 32 seeds", plans[0].Reason);
        Assert.True(plans[1] is { Status: ReadoutLineStatus.Offered, IsLegacy: true, Budget: 2000, Method: FloodCalibrationMethod.Legacy, Solve: ChargeNetworkSolveMethod.Legacy });
        Assert.True(plans[2] is { Status: ReadoutLineStatus.Offered, IsLegacy: false, Budget: 2000, Method: FloodCalibrationMethod.Legacy });
        Assert.False(vm.CanEditLegacy);
        var before = vm.Detector; vm.GainSigmaPercent = 9; vm.GainSeed = 999;
        Assert.Equal(before, vm.Detector);
        vm.Spectrum.PileUp = true; vm.Imaging.Strip = true; vm.Waveform.Ideal = true; vm.Waveform.RateStudy = true;
        Assert.False(vm.Spectrum.PileUp); Assert.False(vm.Imaging.Strip); Assert.False(vm.Waveform.Ideal); Assert.False(vm.Waveform.RateStudy);
        vm.Optics = High;
        Assert.Equal(High, vm.Optics); // no preset is refused while physical
        Assert.True(vm.ReadoutPlans[1] is { Status: ReadoutLineStatus.Offered, Budget: 64000, Method: FloodCalibrationMethod.LatticeGrowth, Solve: ChargeNetworkSolveMethod.Adjoint, IsLegacy: false });
    }

    [Fact]
    public async Task StartRefusesFieldAndMissingPreparationWithoutSilentZeroing()
    {
        var vm = Model(); vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        vm.AmbientDoseRateMicroSvPerHour = 1; vm.BackgroundToSignalRatio = .5;
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Contains("zero", vm.ReadoutMessage); Assert.Equal(1, vm.AmbientDoseRateMicroSvPerHour); Assert.Equal(.5, vm.BackgroundToSignalRatio);
        vm.AmbientDoseRateMicroSvPerHour = 0; vm.BackgroundToSignalRatio = 0;
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Contains("Prepare", vm.ReadoutMessage); Assert.Contains("661.7 keV", vm.ReadoutMessage); Assert.Null(vm.Snapshot);
    }

    [Fact]
    public async Task RequiredLinesFollowTheScene_RefusedAndIncidentalLinesNeverBlock_BankIsReferenceFirst()
    {
        var preparation = new FakeReadoutPreparation(); var acquisition = new CapturingAcquisition();
        var vm = Model(preparation, acquisition); vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        vm.AmbientDoseRateMicroSvPerHour = 0; // the Studio default field is nonzero; the physical path needs zero
        Assert.Equal(new[] { 661.7 }, vm.RequiredReadoutLines.Select(p => p.LineKeV)); // Cs-137 (its Ba X-rays are incidental)
        vm.AddSourceCommand.Execute(null); vm.Sources[^1].Isotope = "Co-60";
        vm.AddSourceCommand.Execute(null); vm.Sources[^1].Isotope = "Co-57"; // 122.1 keV emission is not the 122.06 keV row
        Assert.Equal(new[] { 661.7, 1332.5 }, vm.RequiredReadoutLines.Select(p => p.LineKeV));
        Assert.Equal("Not prepared", vm.ReadoutState);
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        Assert.Equal(new[] { 661.7, 1332.5 }, preparation.Requests.Select(p => p.LineKeV)); // one worker, line by line
        Assert.True(vm.IsReadoutBankReady); Assert.StartsWith("Ready · 661.7 keV", vm.ReadoutState);
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        Assert.Equal(2, preparation.Requests.Count); // ready lines are not prepared again
        await vm.StartCommand.ExecuteAsync(null);
        var ids = Assert.Single(acquisition.Detectors).PreparedReadoutIds;
        Assert.Equal(vm.MatchingBank.Select(p => p.Id), ids);
        Assert.Equal(new[] { 661.7, 1332.5 }, vm.MatchingBank.Select(p => p.LineKeV));
        var row = vm.ReadoutLines.Single(r => r.LineKeV == 122.06);
        Assert.False(row.InScene); Assert.Equal("not preparable", row.Local);
    }

    /// <summary>Author review 2 (TODO-45): the Readout panel shows each calibration line as aligned cells (line · record ·
    /// local). The cells carry the same inline facts as the one-line Summary and Local, which stay the row's accessible
    /// name and help text: the experimental label, the refusal and its short reason, local readiness and failures.</summary>
    [Fact]
    public async Task LineRows_SplitIntoLineRecordAndLocalCells_KeepingEveryInlineFact()
    {
        static string Plain(string text) => text.Replace('\u00A0', ' ');
        var unavailable = new MainViewModel(new RefusingAcquisition(), new Theme(), new FakeSpectrumService()) { ReadoutMode = ReadoutMode.FourOutputAnger };
        Assert.All(unavailable.ReadoutLines, r => Assert.Equal(("Unavailable", ""), (r.RecordLabel, r.RecordDetail)));
        var preparation = new FakeReadoutPreparation(); preparation.Failures[1332.5] = "fixture: unsupported slots (0, 17)";
        var vm = Model(preparation); vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        vm.AmbientDoseRateMicroSvPerHour = 0;
        Assert.All(vm.ReadoutLines, r => Assert.Equal(("Local", "experimental, unvalidated"), (r.RecordLabel, r.RecordDetail)));
        vm.Optics = Head;
        vm.AddSourceCommand.Execute(null); vm.Sources[^1].Isotope = "Co-60";
        var refused = vm.ReadoutLines.Single(r => r.LineKeV == 122.06);
        var cell = refused.Plan.Cell!;
        Assert.Equal(("122.06", "", "Refused", "not preparable", ""), (refused.EnergyText, refused.SceneText, refused.RecordLabel, refused.LocalLabel, refused.LocalDetail));
        Assert.Equal($"failed on {cell.Failures.Count} / {cell.CalibrationSeeds} seeds", Plain(refused.RecordDetail));
        Assert.EndsWith("calibration " + Plain(refused.RecordDetail), refused.Summary);
        var cs = vm.ReadoutLines.Single(r => r.LineKeV == 661.7);
        Assert.Equal(("661.7", "in scene", "Offered", "experimental", "not prepared", ""),
            (cs.EnergyText, Plain(cs.SceneText), cs.RecordLabel, cs.RecordDetail, cs.LocalLabel, cs.LocalDetail));
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        var ready = vm.ReadoutLines.Single(r => r.LineKeV == 661.7);
        Assert.Equal("ready", ready.LocalLabel);
        Assert.Equal(ready.Local, "ready · " + Plain(ready.LocalDetail));
        Assert.Contains(" peaks", Plain(ready.LocalDetail));
        var failed = vm.ReadoutLines.Single(r => r.LineKeV == 1332.5);
        Assert.Equal(("failed", "fixture: unsupported slots (0, 17)"), (failed.LocalLabel, failed.LocalDetail));
        Assert.Equal("Failed: 1332.5 keV: fixture: unsupported slots (0, 17)", failed.Local);
    }

    [Fact]
    public async Task PreparationKeyInvalidationAndWindowValidation_AreExplicit()
    {
        var vm = Model(); vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        Assert.NotNull(vm.MatchingPreparation); Assert.StartsWith("Ready", vm.ReadoutState);
        vm.ReflectorGapUm = "90";
        Assert.Null(vm.MatchingPreparation); Assert.Equal("Not prepared", vm.ReadoutState);
        Assert.Equal(ReadoutLineStatus.Local, vm.RequiredReadoutLines.Single().Status); // gap no longer matches the record
        vm.WindowLowKeV = 721; Assert.Equal(600, vm.WindowLowKeV); Assert.Contains("low < high", vm.ReadoutMessage);
        vm.WindowLowKeV = 500; Assert.Equal(500, vm.WindowLowKeV);
        Assert.False(vm.IsPreparingReadout);
        vm.WindowHighKeV = 1400; Assert.Contains("spans 2 calibration lines", vm.WindowLine);
    }

    [Fact]
    public async Task FailureAndCancellation_KeepFinishedLinesAndReportLiterally()
    {
        var preparation = new FakeReadoutPreparation(); preparation.Cancel.Add(1332.5);
        var vm = Model(preparation); vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        vm.AddSourceCommand.Execute(null); vm.Sources[^1].Isotope = "Co-60";
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        Assert.Equal("Preparation failed: The operation was canceled.", vm.ReadoutMessage);
        Assert.Equal("Failed: 1332.5 keV: The operation was canceled.", vm.ReadoutState); // unrequested OCE is an unexpected exception
        preparation.Cancel.Clear(); preparation.Failures[1332.5] = "fixture: unsupported slots (0, 17)";
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        Assert.Equal("fixture: unsupported slots (0, 17)", vm.ReadoutMessage);
        Assert.Equal("Failed: 1332.5 keV: fixture: unsupported slots (0, 17)", vm.ReadoutState);
        Assert.False(vm.IsReadoutBankReady);
        Assert.Equal(3, preparation.Requests.Count); // 661.7 once, 1332.5 cancelled then failed
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnexpectedPreparationException_FailsTheActiveLine_PreservesFinishedLines_AndRetryRecovers(bool afterPublication)
    {
        var service = new FakeReadoutPreparation();
        var exceptions = afterPublication ? service.ExceptionsAfterPublication : service.Exceptions;
        exceptions[1332.5] = new InvalidOperationException("fixture: unexpected transport failure");
        var vm = Model(service); vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        vm.AmbientDoseRateMicroSvPerHour = 0;
        vm.AddSourceCommand.Execute(null); vm.Sources[^1].Isotope = "Co-60";
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        const string failed = "Failed: 1332.5 keV: fixture: unexpected transport failure";
        Assert.Equal(failed, vm.ReadoutState);
        Assert.Equal(failed, vm.ReadoutLines.Single(r => r.LineKeV == 1332.5).Local);
        Assert.Equal(661.7, Assert.Single(vm.MatchingBank).LineKeV);
        Assert.False(vm.IsReadoutBankReady);
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Null(vm.Snapshot); Assert.Contains("Prepare", vm.ReadoutMessage);
        exceptions.Clear();
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        Assert.True(vm.IsReadoutBankReady); Assert.StartsWith("Ready", vm.ReadoutState);
        Assert.Equal(afterPublication ? new[] { 661.7, 1332.5 } : new[] { 661.7, 1332.5, 1332.5 }, service.Requests.Select(p => p.LineKeV));
    }

    [Fact]
    public async Task PendingPreparation_CanShowNotPreparedAndReadyAcquisition_CancellationIsNotFailure()
    {
        var service = new FakeReadoutPreparation(); service.WaitForCancellation.Add(661.7);
        var vm = Model(service); vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        var pending = vm.PrepareReadoutCommand.ExecuteAsync(null);
        Assert.True(vm.IsPreparingReadout); Assert.Equal("Preparing…", vm.ReadoutState);
        Assert.Equal("not prepared", vm.ReadoutLines.Single(r => r.LineKeV == 661.7).Local);
        Assert.Equal("Ready", vm.Status); // acquisition status is not preparation status
        vm.PrepareReadoutCommand.Cancel();
        await pending;
        Assert.Equal("Preparation cancelled.", vm.ReadoutMessage);
        Assert.Equal("Not prepared", vm.ReadoutState);
        Assert.Equal("not prepared", vm.ReadoutLines.Single(r => r.LineKeV == 661.7).Local);
        service.WaitForCancellation.Clear();
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        Assert.True(vm.IsReadoutBankReady);
    }

    [Fact]
    public void HighResCoScene_RequiresOnly1332Line_AndWindowUsesItsPinnedTwoThousandBudget()
    {
        var vm = Model(); vm.Optics = High; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        vm.Sources[0].Isotope = "Co-60";
        var plan = Assert.Single(vm.RequiredReadoutLines);
        Assert.Equal(1332.5, plan.LineKeV); Assert.Equal(2000, plan.Budget);
        Assert.Equal(ReadoutLineStatus.Offered, plan.Status);
        vm.WindowHighKeV = 1532.375; vm.WindowLowKeV = 1132.625;
        Assert.Equal("Window line 1332.5 keV", vm.WindowLine);
        Assert.False(vm.ReadoutLines.Single(r => r.LineKeV == 661.7).InScene);
    }

    [Fact]
    public void Custom_MatchingModuleInheritsOfferAndBudget_UnmatchedIsLocal()
    {
        var vm = Model(); vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        // Same module as High (44 × 44 at 0.4 mm), different mask and distance: Custom cannot bypass the 64 k budget.
        vm.Optics = High with { MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
        var plan = vm.ReadoutPlans.Single(p => p.LineKeV == 661.7);
        Assert.Equal(ReadoutLineStatus.Offered, plan.Status); Assert.Equal(64000, plan.Budget); Assert.Equal("High", plan.GeometryId);
        vm.Optics = new() { DetectorPixels = 20, PixelPitchMm = .5, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
        var local = vm.ReadoutPlans.Single(p => p.LineKeV == 661.7);
        Assert.True(local is { Status: ReadoutLineStatus.Local, Budget: ReadoutPolicy.LocalBudget, Method: FloodCalibrationMethod.LatticeGrowth, Solve: ChargeNetworkSolveMethod.Adjoint, Cell: null });
        Assert.Contains("experimental, unvalidated", local.Reason);
    }

    [Fact]
    public async Task MissingRecord_OffersNothing_AndStartSaysWhy()
    {
        var vm = Model(new FakeReadoutPreparation(ReadoutOfferRecord.Unavailable("fixture: hash mismatch")));
        vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger; vm.AmbientDoseRateMicroSvPerHour = 0;
        Assert.All(vm.ReadoutPlans, p => Assert.Equal(ReadoutLineStatus.Unavailable, p.Status));
        Assert.Empty(vm.RequiredReadoutLines);
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Contains("fixture: hash mismatch", vm.ReadoutMessage);
    }



    [Theory]
    [InlineData(661.7, 562.445, 760.955)]
    [InlineData(1332.5, 1132.625, 1532.375)]
    public void BenchmarkWindow_SetsExactDecimalEdges(double line, double low, double high)
    {
        Assert.Equal((low, high), ReadoutPolicy.BenchmarkWindow(line, .15));
    }



    [Fact]
    public async Task Eviction_RefusesStart_ReleasesViews_AndPrepareRecovers()
    {
        var service = new FakeReadoutPreparation();
        var acquisition = new CapturingAcquisition();
        var vm = Model(service, acquisition); vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        vm.AmbientDoseRateMicroSvPerHour = 0;
        vm.AddSourceCommand.Execute(null); vm.Sources[^1].Isotope = "Co-60";
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        var old = Track(vm);
        Assert.True(vm.IsReadoutBankReady);
        service.EvictAll();
        Assert.Equal("Not prepared", vm.ReadoutState); Assert.Empty(vm.MatchingBank);
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Contains("Prepare", vm.ReadoutMessage); Assert.Empty(acquisition.Detectors);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(old.TryGetTarget(out _));
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        Assert.True(vm.IsReadoutBankReady); Assert.Equal(4, service.Requests.Count);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<ReadoutPreparation> Track(MainViewModel vm) => new(vm.MatchingPreparation!);



    [Fact]
    public void LocalisationHelp_UsesOfferedModuleCellsAndRecordScope_ExplainsDirectBias()
    {
        var vm = Model(); vm.Optics = High; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        string help = vm.ReadoutLocalisationHelp;
        // High's two offered lines have (.45/.5)^2 and (.6/.16)^2; include all offered module cells.
        Assert.Contains("F 0.81–14.0625", help);
        Assert.Contains("(96.0 % of its MSE is bias)", help);
        Assert.Contains("it does not establish a general Anger advantage", help);
        Assert.Contains(ReadoutCostHelp.FWording, help);
        Assert.Contains(vm.ReadoutOffers.Benchmark!.Decoder, help);
        vm.Optics = new() { DetectorPixels = 20, PixelPitchMm = .5 };
        Assert.Contains("No offered cost cells for this module.", vm.ReadoutLocalisationHelp);
    }

    private sealed class Theme : IThemeService
    {
        public AppTheme Current { get; private set; }
        public void Apply(AppTheme theme) => Current = theme;
    }
    private sealed class RefusingAcquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS, double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
            => throw new InvalidOperationException("This service must not be called before validation.");
    }
    private sealed class CapturingAcquisition : IAcquisitionService
    {
        public List<DetectorSettings> Detectors { get; } = [];
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS, double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
        {
            Detectors.Add(detector!);
            return new EmptySession();
        }
    }
    private sealed class EmptySession : IAcquisitionSession
    {
        public async IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        { await Task.CompletedTask; yield break; }
        public void Stop() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
