using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

public sealed partial class MainViewModel
{
    public IModalService? Modal => Confirmation as IModalService;
    [ObservableProperty] private OptionsViewModel? _options;
    /// <summary>The selected left-panel tab (<see cref="SceneTab"/>, <see cref="OpticsTab"/>, <see cref="DetectorTab"/>);
    /// a refused Start selects the tab holding the offending input.</summary>
    [ObservableProperty] private int _selectedLeftTab;
    public const int SceneTab = 0, OpticsTab = 1, DetectorTab = 2;
    public const string PhysicalFieldMessage = ReadoutPolicy.BsrMessage;

    /// <summary>The reason of the last refused Start, as the status bar shows it ("Start refused: …" or "Options: …");
    /// null after a started acquisition or Reset.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStartRefused))]
    [NotifyPropertyChangedFor(nameof(IsStatusWarning))]
    private string? _startRefusal;
    public bool IsStartRefused => StartRefusal is not null;
    /// <summary>The status bar shows the seed message beside the run status unless the refused-Start text already says it.</summary>
    public bool ShowSeedErrorInStatus => SeedError is not null && StartRefusal != "Options: " + SeedError;
    private bool _optionsRefused;

    /// <summary>The Options input that blocks Start now (seed, ambient dose, gap, or a nonzero field with the physical
    /// readout), or null.</summary>
    public string? OptionsProblem => SeedError ?? AmbientDoseError ?? GapError
        ?? (IsPhysicalReadout && BackgroundToSignalRatio != 0 ? ReadoutPolicy.BsrMessage : null);

    /// <summary>The cog's warning state: a Start was refused for an Options input that is still wrong.</summary>
    public bool HasOptionsWarning => _optionsRefused && OptionsProblem is not null;

    private bool RefuseStart(string reason, int? leftTab = null, bool options = false)
    {
        StartRefusal = (options ? "Options: " : "Start refused: ") + reason;
        Status = StartRefusal;
        if (leftTab is { } tab) SelectedLeftTab = tab;
        if (options) _optionsRefused = true;
        NotifyOptions();
        return false;
    }

    private void ClearStartRefusal()
    {
        StartRefusal = null;
        IsPreparationFailed = false;
        _optionsRefused = false;
        NotifyOptions();
    }
    [RelayCommand]
    private void OpenOptions()
    {
        if (Options is { IsOpen: true } || Modal?.ActiveContent is not null) return;
        var draft = new OptionsViewModel(this, () => { if (Options is { } current) Modal?.Dismiss(current); Options = null; });
        Options = draft;
        Modal?.Show(draft, () => draft.CancelCommand.Execute(null));
    }
    public string OptionsSummary
    {
        get
        {
            var d = StudioOptionDefaults.Current;
            var parts = new List<string>();
            if (int.TryParse(SeedText, out int seed)) parts.Add($"seed {seed}");
            if (PendingGapMm * 1000 != d.GapUm) parts.Add($"gap {ReflectorGapUm} µm" + (GapError is null ? "" : " (invalid; open Options)"));
            string inactive = IsPhysicalReadout ? " (inactive)" : "";
            if (Scintillator != d.Chain.Scintillator) parts.Add(Scintillator.Name + inactive);
            if (Sensor != d.Chain.Sensor) parts.Add(Sensor.Name + inactive);
            if (Preamp != d.Chain.Preamp) parts.Add(Preamp.Name + inactive);
            if (GainSigmaPercent != d.GainSigmaPercent) parts.Add($"gain σ {GainSigmaPercent:G}%" + inactive);
            if (GainSeed != d.GainSeed) parts.Add($"gain seed {GainSeed}" + inactive);
            if (BackgroundToSignalRatio != d.BackgroundToSignalRatio) parts.Add($"BSR {BackgroundToSignalRatio:G} × signal");
            if (AmbientDoseRateMicroSvPerHour != d.AmbientDoseMicroSvPerHour)
                parts.Add(AmbientDoseRateMicroSvPerHour == 0 ? "ideal environment" : $"H*(10) {AmbientDoseRateMicroSvPerHour:G} µSv/h");
            if (AmbientGeometry != d.AmbientGeometry) parts.Add(IsPhysicalReadout
                ? AmbientIncidenceDescription.For(AmbientGeometry, true) : "bare-crystal bound");
            string summary = parts.Count == 0 ? "Options: defaults" : "Options: " + string.Join(" · ", parts);
            return HasData ? summary + " · ambient BSR " + AmbientBsrReadout : summary;
        }
    }
    public bool HasNonDefaultOptions => OptionsSummary != "Options: defaults" && OptionsSummary.Split(" · ambient BSR")[0] != "Options: defaults";
    public string OptionsStatusSummary => HasNonDefaultOptions ? OptionsSummary : HasData ? "ambient BSR " + AmbientBsrReadout : "";
    public string OptionsHelpText => HasOptionsWarning ? "Options: " + OptionsProblem
        : HasNonDefaultOptions ? OptionsSummary : "Options: defaults";
    private void NotifyOptions()
    {
        OnPropertyChanged(nameof(OptionsSummary)); OnPropertyChanged(nameof(HasNonDefaultOptions));
        OnPropertyChanged(nameof(OptionsStatusSummary)); OnPropertyChanged(nameof(OptionsHelpText));
        OnPropertyChanged(nameof(OptionsProblem)); OnPropertyChanged(nameof(HasOptionsWarning));
    }
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(SeedError) or nameof(StartRefusal)) base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(ShowSeedErrorInStatus)));
        if (e.PropertyName is nameof(SeedText) or nameof(ReflectorGapUm) or nameof(GapError) or nameof(Scintillator) or nameof(Sensor)
            or nameof(Preamp) or nameof(GainSigmaPercent) or nameof(GainSeed) or nameof(BackgroundToSignalRatio) or nameof(AmbientDoseRateMicroSvPerHour)
            or nameof(AmbientGeometry) or nameof(Snapshot) or nameof(IsPhysicalReadout) or nameof(SeedError) or nameof(AmbientDoseError)
            or nameof(ReadoutMode)) NotifyOptions();
    }
}
