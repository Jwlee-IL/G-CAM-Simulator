using Gcam.Configuration;
using Gcam.Studio.Core.Optics;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

/// <summary>TODO-45 review 5 (planner finding): every refusal of Start is visible. The reason is in the status bar
/// ("Start refused: …", or "Options: …" for an Options input, which also sets the cog's warning state without opening
/// the dialog); the left tab holding the offending input is selected; the field messages stay. Inputs that pass let the
/// start reach the acquisition service (a fixture that fails at that point) and clear the refusal.</summary>
public sealed class StartRefusalTests
{
    private static readonly OpticsSettings Head = new() { DetectorPixels = 12, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };

    private static (MainViewModel Vm, CountingAcquisition Acquisition) Model(IReadoutPreparationService? preparation = null)
    {
        var acquisition = new CountingAcquisition();
        var vm = new MainViewModel(acquisition, new Theme(), new FakeSpectrumService(), confirmation: new FakeConfirmation(),
            readoutPreparation: preparation ?? new FakeReadoutPreparation()) { AmbientDoseRateMicroSvPerHour = 0 };
        return (vm, acquisition);
    }

    private static async Task<MainViewModel> Refused(MainViewModel vm, CountingAcquisition acquisition)
    {
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(0, acquisition.Starts);
        Assert.Null(vm.Snapshot);
        Assert.Equal(vm.StartRefusal, vm.Status);
        return vm;
    }

    [Fact]
    public async Task OpticsError_ShowsReason_SelectsOpticsTab()
    {
        var (vm, acquisition) = Model();
        vm.OpticsEditor.SelectedPresetIndex = 0; // Custom
        vm.OpticsEditor.Pixels = "abc";
        await Refused(vm, acquisition);
        Assert.Equal("Start refused: " + vm.OpticsEditor.Error, vm.Status);
        Assert.Equal(MainViewModel.OpticsTab, vm.SelectedLeftTab);
        Assert.False(vm.HasOptionsWarning);
        Assert.Equal(vm.OpticsEditor.Error, vm.ValidationError); // the field message under the left panel stays
    }

    [Fact]
    public async Task GapError_ShowsOptionsReason_SelectsOpticsTab_WarnsOnTheCogUntilFixed()
    {
        var (vm, acquisition) = Model();
        vm.ReflectorGapUm = "700"; // wider than the 0.6 mm pixel pitch
        Assert.NotNull(vm.GapError);
        Assert.False(vm.HasOptionsWarning); // only a refused Start raises the warning
        await Refused(vm, acquisition);
        Assert.Equal("Options: " + vm.GapError, vm.Status);
        Assert.Equal(MainViewModel.OpticsTab, vm.SelectedLeftTab);
        Assert.True(vm.HasOptionsWarning);
        Assert.Equal("Options: " + vm.GapError, vm.OptionsHelpText);
        Assert.Null(vm.Options); // the dialog opens only on the user's click
        vm.ReflectorGapUm = "100";
        Assert.False(vm.HasOptionsWarning);
    }

    [Fact]
    public async Task SceneError_SelectsSceneTab()
    {
        var (vm, acquisition) = Model();
        vm.SelectedLeftTab = MainViewModel.DetectorTab;
        vm.OpticsEditor.SelectedPresetIndex = 0;
        vm.OpticsEditor.Distance = "1000"; // the 1 m source is no longer in front of the mask
        await Refused(vm, acquisition);
        Assert.StartsWith("Start refused: ", vm.Status);
        Assert.Contains("front face", vm.Status);
        Assert.Equal(MainViewModel.SceneTab, vm.SelectedLeftTab);
    }

    [Fact]
    public async Task FocusError_ShowsReason_OpensTheFocalSection()
    {
        var (vm, acquisition) = Model();
        vm.Imaging.IsFocalExpanded = false;
        vm.Imaging.FocalPlane = "NaN";
        await Refused(vm, acquisition);
        Assert.Equal("Start refused: Decoder focal plane: " + vm.Imaging.FocusError, vm.Status);
        Assert.True(vm.Imaging.IsFocalExpanded);
        Assert.Equal(MainViewModel.SceneTab, vm.SelectedLeftTab); // the focal plane is not a left-panel input
    }

    [Fact]
    public async Task ReadoutNotPrepared_ShowsReason_SelectsDetectorTab()
    {
        var (vm, acquisition) = Model();
        vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        await Refused(vm, acquisition);
        Assert.Equal("Start refused: " + vm.ReadoutMessage, vm.Status);
        Assert.Contains("Prepare", vm.Status);
        Assert.Equal(MainViewModel.DetectorTab, vm.SelectedLeftTab);
        Assert.False(vm.HasOptionsWarning);
    }

    [Fact]
    public async Task BsrWithPhysicalReadout_RequiresBank_ThenStartsWithoutOptionsWarning()
    {
        var (vm, acquisition) = Model();
        vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        vm.BackgroundToSignalRatio = .5;
        await Refused(vm, acquisition);
        Assert.Contains("Prepare", vm.Status);
        Assert.Equal(MainViewModel.DetectorTab, vm.SelectedLeftTab);
        Assert.False(vm.HasOptionsWarning);
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(1, acquisition.Starts);
        Assert.Equal(.5, vm.BackgroundToSignalRatio);
        Assert.Null(vm.StartRefusal);
    }

    [Fact]
    public async Task PhysicalFieldOnly_ShowsOwnReason_SelectsSceneTab_WithoutOptionsWarning()
    {
        var (vm, acquisition) = Model();
        vm.Sources.Clear(); vm.AmbientDoseRateMicroSvPerHour = .1;
        vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        await Refused(vm, acquisition);
        Assert.Equal("Start refused: " + ReadoutPolicy.FieldOnlyMessage, vm.Status);
        Assert.Equal(MainViewModel.SceneTab, vm.SelectedLeftTab);
        Assert.False(vm.HasOptionsWarning); Assert.Equal(.1, vm.AmbientDoseRateMicroSvPerHour);
    }

    [Fact]
    public async Task SourceWithAmbient_RequiresBank_ThenRoutesStartAmbientAndClearsRefusal()
    {
        var preparation = new FakeReadoutPreparation();
        var (vm, acquisition) = Model(preparation);
        vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        vm.AmbientDoseRateMicroSvPerHour = .1;
        await Refused(vm, acquisition);
        Assert.Contains("Prepare", vm.Status); Assert.False(vm.HasOptionsWarning);
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(1, acquisition.Starts); Assert.Equal(1, acquisition.AmbientStarts);
        Assert.Null(vm.StartRefusal); Assert.False(vm.HasOptionsWarning);
        Assert.Equal(.1, vm.AmbientDoseRateMicroSvPerHour);
    }

    [Fact]
    public async Task SeedError_ShowsOptionsReason_WarnsOnTheCog()
    {
        var (vm, acquisition) = Model();
        vm.SeedText = "x";
        Assert.True(vm.ShowSeedErrorInStatus); // before Start: the seed message beside the run status
        await Refused(vm, acquisition);
        Assert.Equal("Options: " + vm.SeedError, vm.Status);
        Assert.False(vm.ShowSeedErrorInStatus); // the status line already says it: no second copy in the one-line bar
        Assert.True(vm.HasOptionsWarning);
        Assert.Equal(MainViewModel.SceneTab, vm.SelectedLeftTab);
        vm.SeedText = "42";
        Assert.False(vm.HasOptionsWarning);
    }

    [Fact]
    public async Task AmbientDoseError_ShowsOptionsReason_WarnsOnTheCog_PassingInputsClearTheRefusal()
    {
        var (vm, acquisition) = Model();
        vm.AmbientDoseText = "-1";
        await Refused(vm, acquisition);
        Assert.Equal("Options: " + vm.AmbientDoseError, vm.Status);
        Assert.True(vm.HasOptionsWarning);
        vm.AmbientDoseText = "0";
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(1, acquisition.Starts); // reached the service (which fails in this fixture)
        Assert.Null(vm.StartRefusal);
        Assert.False(vm.HasOptionsWarning);
        Assert.StartsWith("Failed: fixture", vm.Status);
    }

    private sealed class Theme : IThemeService
    {
        public AppTheme Current => AppTheme.Dark;
        public void Apply(AppTheme theme) { }
    }

    /// <summary>Counts the starts that pass validation, then fails them (no transport in these tests).</summary>
    private sealed class CountingAcquisition : IAcquisitionService
    {
        public int Starts { get; private set; }
        public int AmbientStarts { get; private set; }
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS, double speed,
            DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
        { Starts++; throw new InvalidOperationException("fixture: start reached the service"); }
        public IAcquisitionSession StartAmbient(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS,
            double speed, AmbientFieldConfig ambient, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
        { Starts++; AmbientStarts++; throw new InvalidOperationException("fixture: ambient start reached the service"); }
    }
}
