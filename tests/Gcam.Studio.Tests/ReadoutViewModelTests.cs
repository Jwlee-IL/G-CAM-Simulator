using Gcam.Configuration;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

/// <summary>TODO-41 per-line policy in the ViewModel: offers and refusals come only from the record and the module
/// fingerprint; the scene decides which lines an acquisition needs; refused and incidental lines never block; Custom
/// inherits by module; the cost card carries the exact F wording and the data's own bias explanation.</summary>
public sealed class ReadoutViewModelTests
{
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
        Assert.StartsWith("Not prepared: 1332.5 keV · ready: 661.7 keV", vm.ReadoutState);
        preparation.Cancel.Clear(); preparation.Failures[1332.5] = "fixture: unsupported slots (0, 17)";
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        Assert.Equal("fixture: unsupported slots (0, 17)", vm.ReadoutMessage);
        Assert.Equal("Failed: 1332.5 keV: fixture: unsupported slots (0, 17)", vm.ReadoutState);
        Assert.False(vm.IsReadoutBankReady);
        Assert.Equal(3, preparation.Requests.Count); // 661.7 once, 1332.5 cancelled then failed
    }

    [Fact]
    public void Custom_MatchingModuleInheritsOfferAndBudget_ButNotTheCost_UnmatchedIsLocal()
    {
        var vm = Model(); vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        // Same module as High (44 × 44 at 0.4 mm), different mask and distance: Custom cannot bypass the 64 k budget.
        vm.Optics = High with { MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
        var plan = vm.ReadoutPlans.Single(p => p.LineKeV == 661.7);
        Assert.Equal(ReadoutLineStatus.Offered, plan.Status); Assert.Equal(64000, plan.Budget); Assert.Equal("High", plan.GeometryId);
        Assert.False(vm.Imaging.ReadoutCostInherited);
        Assert.Contains("Imaging cost unmeasured for this configuration", vm.Imaging.ReadoutCostStatus);
        Assert.Contains("mask differs", vm.Imaging.ReadoutCostStatusDetails); // the reasons live in the help text
        Assert.Contains("mask-detector distance differs", vm.Imaging.ReadoutCostHelp);
        vm.Optics = new() { DetectorPixels = 20, PixelPitchMm = .5, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
        var local = vm.ReadoutPlans.Single(p => p.LineKeV == 661.7);
        Assert.True(local is { Status: ReadoutLineStatus.Local, Budget: ReadoutPolicy.LocalBudget, Method: FloodCalibrationMethod.LatticeGrowth, Solve: ChargeNetworkSolveMethod.Adjoint, Cell: null });
        Assert.Contains("experimental, unvalidated", local.Reason);
        Assert.StartsWith("Imaging cost unmeasured for this configuration", vm.Imaging.ReadoutCostStatus);
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
        Assert.Contains("Imaging cost unavailable", vm.Imaging.ReadoutCostStatus);
    }

    [Fact]
    public void CostCard_InheritsOnlyOnFullProfile_StatesFWording_AndExplainsFBelowOneFromTheData()
    {
        var vm = Model(); vm.Optics = High; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        Assert.False(vm.Imaging.ReadoutCostInherited); // window 600–720 is not ±15 % of 661.7, correction off
        vm.UseBenchmarkWindowCommand.Execute(null);
        Assert.Equal(562.445, vm.WindowLowKeV); Assert.Equal(760.955, vm.WindowHighKeV); // exact decimal edges
        Assert.False(vm.Imaging.ReadoutCostInherited);
        vm.ApplyFloodCorrection = true;
        Assert.True(vm.Imaging.ReadoutCostInherited);
        Assert.StartsWith("Reference benchmark inherited", vm.Imaging.ReadoutCostStatus);
        vm.Imaging.Reconstruction = DecoderMethod.Mlem;
        Assert.False(vm.Imaging.ReadoutCostInherited);
        Assert.Contains("MLEM selected", vm.Imaging.ReadoutCostStatusDetails);
        string card = vm.Imaging.ReadoutCostCard;
        Assert.Contains(ImagingWorkspaceViewModel.FWording, card);
        Assert.Contains("0.5° · Anger RMS 0.45° · F 0.81", card); // key numbers stay inline
        string help = vm.Imaging.ReadoutCostHelp;
        // Hand-derived oracle: Direct RMS 0.5°, bias 0.49° -> 0.49² / 0.5² = 96.0 % of the Direct MSE is bias.
        Assert.Contains("(96.0 % of its MSE is bias)", help);
        Assert.Contains("it does not establish a general Anger advantage", help);
        Assert.Contains("64,000 histories per crystal (repair-v5.json, repair)", help);
        Assert.DoesNotContain("of its MSE is bias", card);
        Assert.Equal(4, vm.Imaging.ReadoutCostRows.Count);
        Assert.Equal("0.81 [0.729, 0.891]", vm.Imaging.ReadoutCostRows[0].F);
        Assert.Equal("0.5° [0.45, 0.55]", vm.Imaging.ReadoutCostRows[0].DirectRms);
    }

    [Theory]
    // Typed edges as a user enters them, for each record line (exact decimals 661.7 x 0.85 / 1.15 and 1332.5 x 0.85 / 1.15).
    [InlineData(661.7, 562.445, 760.955, true)]
    [InlineData(1332.5, 1132.625, 1532.375, true)]
    // One unit in the third decimal off: not the benchmark window (the tolerance is half a unit, 0.0005 keV).
    [InlineData(661.7, 562.446, 760.955, false)]
    [InlineData(1332.5, 1132.625, 1532.374, false)]
    [InlineData(1332.5, 1132.62, 1532.38, false)]
    public void BenchmarkWindow_MatchesTypedEdgesAtTheirDefinedPrecision(double line, double low, double high, bool expected)
    {
        Assert.Equal(expected, ReadoutPolicy.IsBenchmarkWindow(low, high, line, .15));
        var (bl, bh) = ReadoutPolicy.BenchmarkWindow(line, .15); // the button's values pass the same check …
        Assert.True(ReadoutPolicy.IsBenchmarkWindow(bl, bh, line, .15));
        if (expected) Assert.Equal((low, high), (bl, bh)); // … and are exactly what a user types
        Assert.NotEqual(1532.375, 1332.5 * 1.15); // binary 1532.3749999999998: why the exact comparison failed
    }

    [Fact]
    public void CostCard_InheritsWithHandTypedBenchmarkWindow()
    {
        var vm = Model(); vm.Optics = High; vm.ReadoutMode = ReadoutMode.FourOutputAnger; vm.ApplyFloodCorrection = true;
        vm.WindowHighKeV = 760.955; vm.WindowLowKeV = 562.445;
        Assert.True(vm.Imaging.ReadoutCostInherited);
        Assert.StartsWith("Reference benchmark inherited", vm.Imaging.ReadoutCostStatus);
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
