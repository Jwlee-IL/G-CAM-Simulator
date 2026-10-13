using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

public sealed partial class MainViewModel
{
    public IModalService? Modal => Confirmation as IModalService;
    [ObservableProperty] private OptionsViewModel? _options;
    [ObservableProperty] private int _selectedLeftTab;
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
            if (AmbientGeometry != d.AmbientGeometry) parts.Add("bare-crystal bound");
            string summary = parts.Count == 0 ? "Options: defaults" : "Options: " + string.Join(" · ", parts);
            return HasData ? summary + " · ambient BSR " + AmbientBsrReadout : summary;
        }
    }
    public bool HasNonDefaultOptions => OptionsSummary != "Options: defaults" && OptionsSummary.Split(" · ambient BSR")[0] != "Options: defaults";
    public string OptionsStatusSummary => HasNonDefaultOptions ? OptionsSummary : HasData ? "ambient BSR " + AmbientBsrReadout : "";
    public string OptionsHelpText => HasNonDefaultOptions ? OptionsSummary : "Options: defaults";
    private void NotifyOptions()
    {
        OnPropertyChanged(nameof(OptionsSummary)); OnPropertyChanged(nameof(HasNonDefaultOptions));
        OnPropertyChanged(nameof(OptionsStatusSummary)); OnPropertyChanged(nameof(OptionsHelpText));
    }
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(SeedText) or nameof(ReflectorGapUm) or nameof(GapError) or nameof(Scintillator) or nameof(Sensor)
            or nameof(Preamp) or nameof(GainSigmaPercent) or nameof(GainSeed) or nameof(BackgroundToSignalRatio) or nameof(AmbientDoseRateMicroSvPerHour)
            or nameof(AmbientGeometry) or nameof(Snapshot) or nameof(IsPhysicalReadout)) NotifyOptions();
    }
}
