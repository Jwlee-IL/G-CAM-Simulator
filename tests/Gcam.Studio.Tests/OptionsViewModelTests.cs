using Gcam.Configuration;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public sealed class OptionsViewModelTests
{
    private static MainViewModel Model() => new(new UnusedAcquisition(), new Theme(), new FakeSpectrumService());
    private static OptionsViewModel Open(MainViewModel model) { model.OpenOptionsCommand.Execute(null); return model.Options!; }
    [Fact]
    public void Defaults_AreTheInitialValues_AndFallbacks_AndSummary()
    {
        var vm = Model(); var d = StudioOptionDefaults.Current;
        Assert.Equal(d.SeedText, vm.SeedText); Assert.Equal(d.Chain, vm.Chain);
        Assert.Equal(d.GapUm, double.Parse(vm.ReflectorGapUm)); Assert.Equal(d.GainSigmaPercent, vm.GainSigmaPercent);
        Assert.Equal(d.GainSeed, vm.GainSeed); Assert.Equal(d.BackgroundToSignalRatio, vm.BackgroundToSignalRatio);
        Assert.Equal(d.AmbientDoseMicroSvPerHour, vm.AmbientDoseRateMicroSvPerHour); Assert.Equal(d.AmbientGeometry, vm.AmbientGeometry);
        Assert.Equal(d.EntranceAbsorberMm, vm.Detector.EntranceAbsorberMm); Assert.Equal(d.BackingScatterMm, vm.Detector.BackingScatterMm);
        Assert.Equal(d.LiveTimeS, vm.LiveTimeS); Assert.Equal(d.Speed, vm.Speed); Assert.Equal(d.WindowFwhm, vm.WindowFwhm);
        Assert.Equal("Options: defaults", vm.OptionsSummary); Assert.False(vm.HasNonDefaultOptions);
        Assert.Equal("", vm.OptionsStatusSummary);
        Assert.Equal("Options: defaults", vm.OptionsHelpText);
        vm.GainSigmaPercent = double.NaN; vm.BackgroundToSignalRatio = double.PositiveInfinity;
        vm.Speed = -1; vm.LiveTimeS = -1; vm.WindowFwhm = -1;
        Assert.Equal(d.GainSigmaPercent, vm.GainSigmaPercent); Assert.Equal(d.BackgroundToSignalRatio, vm.BackgroundToSignalRatio);
        Assert.Equal(d.Speed, vm.Speed); Assert.Equal(d.LiveTimeS, vm.LiveTimeS); Assert.Equal(d.WindowFwhm, vm.WindowFwhm);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Draft_ChangesOwnerOnlyOnApply(bool apply)
    {
        var vm = Model(); var draft = Open(vm);
        draft.SeedText = "42"; draft.GapUm = "90"; draft.GainSigmaPercent = "4"; draft.GainSeed = "7";
        draft.BackgroundToSignalRatio = "2"; draft.AmbientDoseText = "0"; draft.AmbientGeometry = AmbientGeometry.BareCrystalAllFaces;
        draft.Scintillator = vm.Scintillators[1];
        Assert.Equal("Options: defaults", vm.OptionsSummary);
        if (apply) draft.ApplyCommand.Execute(null); else draft.CancelCommand.Execute(null);
        Assert.False(draft.IsOpen); Assert.Null(vm.Options);
        Assert.Equal(apply ? "42" : "", vm.SeedText); Assert.Equal(apply ? "90" : "100", vm.ReflectorGapUm);
        Assert.Equal(apply ? 4 : 3, vm.GainSigmaPercent); Assert.Equal(apply ? 7 : 1, vm.GainSeed);
        Assert.Equal(apply ? 2 : 0, vm.BackgroundToSignalRatio); Assert.Equal(apply ? 0 : .1, vm.AmbientDoseRateMicroSvPerHour);
        Assert.Equal(apply ? vm.Scintillators[1] : StudioOptionDefaults.Current.Chain.Scintillator, vm.Scintillator);
    }
    [Theory]
    [InlineData("Seed", "-1", OptionsTab.Acquisition)]
    [InlineData("Gap", "NaN", OptionsTab.Detector)]
    [InlineData("Gain", "-1", OptionsTab.Detector)]
    [InlineData("GainSeed", "1.5", OptionsTab.Detector)]
    [InlineData("Dose", "-0.1", OptionsTab.Environment)]
    [InlineData("Background", "Infinity", OptionsTab.Environment)]
    public void InvalidDraft_StaysOpenWithFieldMessage_AndWritesNothing(string field, string value, OptionsTab tab)
    {
        var vm = Model(); var draft = Open(vm); draft.SeedText = "42";
        switch (field)
        {
            case "Seed": draft.SeedText = value; break;
            case "Gap": draft.GapUm = value; break;
            case "Gain": draft.GainSigmaPercent = value; break;
            case "GainSeed": draft.GainSeed = value; break;
            case "Dose": draft.AmbientDoseText = value; break;
            case "Background": draft.BackgroundToSignalRatio = value; break;
        }
        draft.ApplyCommand.Execute(null);
        Assert.True(draft.IsOpen); Assert.Equal(tab, draft.SelectedTab); Assert.Equal("Options: defaults", vm.OptionsSummary);
        Assert.NotNull(field switch { "Seed" => draft.SeedError, "Gap" => draft.GapError, "Gain" => draft.GainSigmaError,
            "GainSeed" => draft.GainSeedError, "Dose" => draft.AmbientDoseError, _ => draft.BackgroundError });
        if (field == "Seed") Assert.Equal(StudioOptionValidation.SeedMessage, draft.SeedError);
        if (field == "Gap") Assert.Equal(StudioOptionValidation.GapMessage, draft.GapError);
        if (field == "Dose") Assert.Equal(StudioOptionValidation.AmbientDoseMessage, draft.AmbientDoseError);
        draft.CancelCommand.Execute(null);
    }
    [Theory]
    [InlineData("Preparing")]
    [InlineData("Running")]
    [InlineData("Data")]
    public async Task LockedOptions_AreCloseOnly_AndDoNotApply(string state)
    {
        var vm = Model();
        if (state == "Preparing") vm.IsPreparingReadout = true;
        if (state == "Running") vm.IsRunning = true;
        if (state == "Data") { var acquired = DetectorWorkspaceTests.Model(); await acquired.StartCommand.ExecuteAsync(null); vm.Snapshot = acquired.Snapshot; }
        var draft = Open(vm);
        Assert.True(draft.IsReadOnly); Assert.False(draft.CanEditInputs); Assert.False(draft.CanEditLegacy);
        Assert.Equal("Acquired settings · locked until Reset", draft.LockCaption);
        draft.SeedText = "42"; draft.ApplyCommand.Execute(null); Assert.Equal("", vm.SeedText); Assert.False(draft.IsOpen);
    }
    [Fact]
    public async Task Summary_ShowsOnlyNondefaults_AndAmbientResultWithData()
    {
        var vm = Model(); vm.ReflectorGapUm = "100.0"; vm.AmbientDoseText = "0.100";
        Assert.Equal("Options: defaults", vm.OptionsSummary);
        vm.SeedText = "42"; vm.Scintillator = vm.Scintillators[1]; vm.AmbientDoseText = "0";
        Assert.Equal("Options: seed 42 · NaI(Tl) · ideal environment", vm.OptionsSummary); Assert.True(vm.HasNonDefaultOptions);
        var acquired = DetectorWorkspaceTests.Model(); await acquired.StartCommand.ExecuteAsync(null);
        vm.Snapshot = acquired.Snapshot! with { SourceRateCps = 2, AmbientRateCps = 5 };
        Assert.EndsWith(" · ambient BSR off", vm.OptionsSummary);
        var defaults = Model(); defaults.Snapshot = acquired.Snapshot! with { SourceRateCps = 2, AmbientRateCps = 5 };
        Assert.Equal("Options: defaults · ambient BSR 2.5 × signal", defaults.OptionsSummary); Assert.False(defaults.HasNonDefaultOptions);
        Assert.Equal("ambient BSR 2.5 × signal", defaults.OptionsStatusSummary);
        Assert.Equal(vm.OptionsSummary, vm.OptionsStatusSummary);
        Assert.Equal(vm.OptionsSummary, vm.OptionsHelpText);
    }
    [Fact]
    public void Summary_ListsEveryChangedOption_WithoutFixedFactsOrTopBarInputs()
    {
        var vm = Model();
        vm.SeedText = "7";
        vm.ReflectorGapUm = "90";
        vm.Scintillator = vm.Scintillators[1];
        vm.Sensor = vm.Sensors[1];
        vm.Preamp = vm.Preamps[0];
        vm.GainSigmaPercent = 4;
        vm.GainSeed = 2;
        vm.BackgroundToSignalRatio = 3;
        vm.AmbientDoseText = "0.2";
        vm.AmbientGeometry = AmbientGeometry.BareCrystalAllFaces;
        vm.Speed = 20;
        vm.LiveTimeS = 120;
        Assert.Equal("Options: seed 7 · gap 90 µm · NaI(Tl) · Hamamatsu MPPC S13360-6050 · Fast CSP + CR-RC^4"
            + " · gain σ 4% · gain seed 2 · BSR 3 × signal · H*(10) 0.2 µSv/h · bare-crystal bound", vm.OptionsSummary);
    }
    [Fact]
    public void ModalHost_ReceivesOneDraft_AndCancelDiscardsIt()
    {
        var modal = new Modal();
        var vm = new MainViewModel(new UnusedAcquisition(), new Theme(), new FakeSpectrumService(), confirmation: modal);
        var draft = Open(vm);
        Assert.Same(draft, modal.ActiveContent);
        draft.SeedText = "42";
        vm.OpenOptionsCommand.Execute(null);
        Assert.Same(draft, vm.Options);
        modal.Cancel!();
        Assert.Null(modal.ActiveContent);
        Assert.Null(vm.Options);
        Assert.False(draft.IsOpen);
        Assert.Equal("Options: defaults", vm.OptionsSummary);
    }
    [Fact]
    public void PhysicalMode_DisablesOnlyLegacyFields_AndMarksLegacyDifferencesInactive()
    {
        var vm = Model(); vm.GainSigmaPercent = 4; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        var draft = Open(vm); Assert.True(draft.CanEditInputs); Assert.False(draft.CanEditLegacy); Assert.False(draft.IsReadOnly);
        Assert.Equal("Options: gain σ 4% (inactive)", vm.OptionsSummary);
        draft.GainSigmaPercent = "invalid inactive field"; draft.SeedText = "42"; draft.ApplyCommand.Execute(null);
        Assert.False(draft.IsOpen); Assert.Equal(4, vm.GainSigmaPercent); Assert.Equal("42", vm.SeedText);
    }
    private sealed class Theme : IThemeService { public AppTheme Current => AppTheme.Dark; public void Apply(AppTheme theme) { } }
    private sealed class Modal : IModalService
    {
        public object? ActiveContent { get; private set; }
        public Action? Cancel { get; private set; }
        public void Show(object content, Action cancel) { ActiveContent = content; Cancel = cancel; }
        public void Dismiss(object content) { Assert.Same(content, ActiveContent); ActiveContent = null; }
        public Task<bool> ConfirmAsync(string message) => throw new InvalidOperationException();
    }
    private sealed class UnusedAcquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS, double speed,
            DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null) => throw new InvalidOperationException();
    }
}
