using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public class MainViewModelTests
{
    [Fact]
    public void WorkspaceActivation_SelectsShell_WhenActiveIsSetWithoutCommand()
    {
        var vm = new MainViewModel(new FakeAcquisition(), new FakeTheme(), new FakeSpectrumService());
        vm.Spectrum.IsActive = true;
        Assert.Same(vm.Spectrum, vm.SelectedWorkspace);
        Assert.False(vm.Imaging.IsActive);
        vm.Imaging.IsActive = true;
        Assert.Same(vm.Imaging, vm.SelectedWorkspace);
        Assert.False(vm.Spectrum.IsActive);
        vm.SelectedWorkspace = vm.Spectrum;
        Assert.True(vm.Spectrum.IsActive);
        Assert.False(vm.Imaging.IsActive);
        Assert.Equal(RunState.Empty, vm.State);
    }

    [Fact]
    public void DetectorInputs_DefaultsAndValidationFallbacks_EditableWithoutData()
    {
        var vm = new MainViewModel(new FakeAcquisition(), new FakeTheme(), new FakeSpectrumService());
        Assert.Equal(new DetectorSettings(), vm.Detector);
        Assert.True(vm.CanEditInputs);
        vm.GainSigmaPercent = 4;
        vm.GainSeed = 5;
        vm.BackgroundToSignalRatio = 1;
        Assert.Equal(0.04, vm.Detector.GainSigma);
        Assert.Equal(5, vm.Detector.GainSeed);
        Assert.Equal(1, vm.BackgroundToSignalRatio);
        vm.Result = Image; // a result alone is not acquired data: nothing locks
        vm.GainSeed = 6;
        Assert.Equal(6, vm.GainSeed);
        vm.GainSigmaPercent = double.NaN;
        vm.BackgroundToSignalRatio = -1;
        Assert.Equal(3, vm.GainSigmaPercent);
        Assert.Equal(0, vm.BackgroundToSignalRatio);
    }
    [Fact]
    public void Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsKeepState()
    {
        var vm = new MainViewModel(new FakeAcquisition(), new FakeTheme(), new FakeSpectrumService());
        Assert.Equal(4, vm.Workspaces.Count);
        Assert.Same(vm.Imaging, vm.Workspaces[0]);
        Assert.Same(vm.Spectrum, vm.Workspaces[1]);
        Assert.Same(vm.Waveform, vm.Workspaces[2]);
        Assert.Same(vm.Imaging, vm.SelectedWorkspace);
        Assert.Equal("Workspace.Imaging", vm.Imaging.AutomationId);
        Assert.True(vm.HasWorkspaceSwitch);
        vm.Result = Image;
        Assert.Same(vm.Imaging, vm.SelectedWorkspace);
        Assert.Same(vm.Result, vm.Imaging.Shared.Result);
        Assert.Equal("peak (1.0, 2.0) mm", vm.Imaging.PeakText);
        vm.Imaging.Measurements.ActiveTool = MeasureTool.Distance;
        Assert.Equal(RunState.Empty, vm.State);
        vm.SelectWorkspaceCommand.Execute("4");
        Assert.Same(vm.Imaging, vm.SelectedWorkspace);
        Assert.True(vm.Imaging.IsActive);
        vm.SelectWorkspaceCommand.Execute("3");
        Assert.Same(vm.DetectorWorkspace, vm.SelectedWorkspace);
        Assert.True(vm.DetectorWorkspace.IsActive);
    }

    private sealed class FakeAcquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
            double liveTimeS, double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
            => throw new NotSupportedException();
    }

    private static ImagingResult Image => new(new DetectorImage(4, 4), -0.9, 0.6, null, 0, 0,
        new SourceEstimate(new Vector3(1, 2, 0), 2.5), 1234, TimeSpan.FromSeconds(1));

    private sealed class FakeTheme : IThemeService
    {
        public AppTheme Current { get; private set; } = AppTheme.Dark;
        public void Apply(AppTheme theme) => Current = theme;
    }

    [Fact]
    public void ThemeToggle_FlipsThemeAndRelabels()
    {
        var theme = new FakeTheme();
        var vm = new MainViewModel(new FakeAcquisition(), theme, new FakeSpectrumService());
        Assert.Equal("Light theme", vm.ThemeToggleLabel);
        vm.ToggleThemeCommand.Execute(null);
        Assert.Equal(AppTheme.Light, theme.Current);
        Assert.Equal("Dark theme", vm.ThemeToggleLabel);
    }

    [Fact]
    public void Startup_HasOneSelectedSource()
    {
        var vm = new MainViewModel(new FakeAcquisition(), new FakeTheme(), new FakeSpectrumService());
        Assert.Single(vm.Sources);
        Assert.Same(vm.Sources[0], vm.SelectedSource);
        Assert.True(vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public void AddRemove_SelectsNewSourceThenNeighbour()
    {
        var vm = new MainViewModel(new FakeAcquisition(), new FakeTheme(), new FakeSpectrumService());
        vm.AddSourceCommand.Execute(null);
        vm.AddSourceCommand.Execute(null);
        Assert.Equal(3, vm.Sources.Count);
        Assert.Same(vm.Sources[2], vm.SelectedSource);
        Assert.NotEqual(vm.Sources[0].X, vm.Sources[1].X);   // new sources are spread, not stacked

        vm.SelectedSource = vm.Sources[1];
        vm.RemoveSourceCommand.Execute(null);
        Assert.Equal(2, vm.Sources.Count);
        Assert.Same(vm.Sources[1], vm.SelectedSource);
    }

    [Fact]
    public void RemoveAll_DisablesRemoveAndStart()
    {
        var vm = new MainViewModel(new FakeAcquisition(), new FakeTheme(), new FakeSpectrumService()) { AmbientDoseRateMicroSvPerHour = 0 };
        vm.RemoveSourceCommand.Execute(null);
        Assert.Empty(vm.Sources);
        Assert.Null(vm.SelectedSource);
        Assert.False(vm.RemoveSourceCommand.CanExecute(null));
        Assert.False(vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public void SourceItem_ClampsValues_LabelFollowsEdits()
    {
        var s = new SourceItemViewModel();
        var changed = new List<string?>();
        s.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        s.DistanceMm = 50;
        s.ActivityUCi = -3;
        s.Isotope = "Unobtainium-1";

        Assert.Equal(200, s.DistanceMm);
        Assert.Equal(1, s.ActivityUCi);
        Assert.Equal("Cs-137", s.Isotope);
        Assert.Contains(nameof(SourceItemViewModel.Label), changed);
    }

    [Fact]
    public void NewResult_RefreshesMeasurements()
    {
        var vm = new MainViewModel(new FakeAcquisition(), new FakeTheme(), new FakeSpectrumService());
        vm.Imaging.Measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Roi,
            [new Gcam.Studio.Core.Imaging.Vec2(-5, -5), new Gcam.Studio.Core.Imaging.Vec2(5, 5)]));
        Assert.Equal("—", vm.Imaging.Measurements.Items[0].Value);

        vm.Result = Image;
        Assert.Equal("Σ 0", vm.Imaging.Measurements.Items[0].Value);   // the fake's flood is all zeros
    }

    [Fact]
    public void IsotopePicker_OffersIr192_AndKeepsCs137AsTheDefault()
    {
        Assert.Contains("Ir-192", SourceItemViewModel.IsotopeNames);
        Assert.Equal("Cs-137", SourceItemViewModel.IsotopeNames[0]);
        var source = new SourceItemViewModel { Isotope = "Ir-192" };
        Assert.Equal("Ir-192", source.ToModel().Isotope);
        source.Isotope = "no such isotope";
        Assert.Equal("Cs-137", source.Isotope);   // unknown → first entry
    }

    [Fact]
    public void SpectrumSummary_NamesTheOverflowAndTheFixedAxisEnd()
    {
        var vm = new MainViewModel(new FakeAcquisition(), new FakeTheme(), new FakeSpectrumService());
        vm.Spectrum.View = new SpectrumView([1], [5], [], 9, 4, .5, .06, 730e-9, "chain", TimeSpan.Zero) { BinEdgesKeV = [0, 2000] };
        Assert.Equal("9 measured pulses · 50.0% in windows · 4 overflow ≥ 2000 keV", vm.Spectrum.Summary);
    }
}
