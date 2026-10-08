using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

public sealed partial class MainViewModel
{
    private readonly IReadoutPreparationService? _readoutPreparation;
    private ReadoutMode _readoutMode;
    public IReadOnlyList<ReadoutMode> ReadoutModes { get; } = [ReadoutMode.DirectCrystal, ReadoutMode.FourOutputAnger];
    public ReadoutMode ReadoutMode
    {
        get => _readoutMode;
        set
        {
            if (!CanEditInputs || !ReadoutModes.Contains(value)) { RefreshRejectedReadoutProperty(nameof(ReadoutMode)); return; }
            if (value != ReadoutMode.DirectCrystal && ReadoutPolicy.GeometryError(Optics) is { } error)
            { ReadoutMessage = error; RefreshRejectedReadoutProperty(nameof(ReadoutMode)); return; }
            if (SetProperty(ref _readoutMode, value)) NotifyReadout();
        }
    }
    private void RefreshRejectedReadoutProperty(string name)
    {
        // A Selector can still be committing the rejected selection. Refresh after its source update completes.
        // With no UI context the immediate notification preserves the headless property contract.
        if (SynchronizationContext.Current is { } context)
            context.Post(_ => OnPropertyChanged(name), null);
        else
            OnPropertyChanged(name);
    }
    public bool IsPhysicalReadout => (Snapshot?.Detector?.ReadoutMode ?? ReadoutMode) != ReadoutMode.DirectCrystal;
    public bool CanUseLegacy => !IsPhysicalReadout;
    public bool CanEditLegacy => CanEditInputs && CanUseLegacy;
    public int ScopeEventCount => Snapshot?.Readout?.Records.Count ?? Snapshot?.Events.Count ?? 0;
    public string ReadoutAssumptions => IsPhysicalReadout
        ? "Experimental GAGG · DPC 0.01 · sum trigger 50 keV-equivalent · specular 0.98 · virtual matched SiPMs · unlimited cells; no RC, skew or jitter. Legacy gain and chain are inactive. Ambient and BSR must be zero: their interaction records are unavailable."
        : "Direct crystal assignment; legacy detector gain and chain response.";
    [ObservableProperty] private ReadoutPreparation? _preparedReadout;
    [ObservableProperty] private string? _readoutMessage;
    [ObservableProperty] private bool _isPreparingReadout;
    public string ReadoutState => IsPreparingReadout ? ReadoutMessage ?? "Preparing…"
        : MatchingPreparation is { } p ? p.Succeeded ? $"Ready · calibration seed 403 · {p.Cost.TotalSeconds:0.00} s · {p.InWindow:N0} windowed / {p.Scored:N0} scored" : $"Failed: {p.Failure}"
        : "Not prepared";
    public ReadoutPreparation? MatchingPreparation => PreparedReadout?.Key == ReadoutPolicy.Key(Optics, Detector) ? PreparedReadout : null;
    private bool CanPrepareReadout() => _readoutPreparation is not null && IsPhysicalReadout && CanEditInputs;
    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanPrepareReadout))]
    private async Task PrepareReadoutAsync(CancellationToken token)
    {
        IsPreparingReadout = true; NotifyRunState();
        try
        {
            PreparedReadout = await _readoutPreparation!.PrepareAsync(Optics, Detector,
                new Progress<string>(text => { if (IsPreparingReadout) { ReadoutMessage = text; OnPropertyChanged(nameof(ReadoutState)); } }), token);
            ReadoutMessage = PreparedReadout.Succeeded ? null : PreparedReadout.Failure;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { ReadoutMessage = "Preparation cancelled."; }
        catch (Exception ex) { ReadoutMessage = $"Preparation failed: {ex.Message}"; }
        finally { IsPreparingReadout = false; NotifyRunState(); NotifyReadout(); }
    }
    private double _windowLowKeV = 600, _windowHighKeV = 720;
    public double WindowLowKeV { get => _windowLowKeV; set => SetWindow(value, WindowHighKeV); }
    public double WindowHighKeV { get => _windowHighKeV; set => SetWindow(WindowLowKeV, value); }
    private void SetWindow(double low, double high)
    {
        if (!double.IsFinite(low) || !double.IsFinite(high) || low < 0 || high <= low)
        { ReadoutMessage = "Energy window needs finite 0 ≤ low < high in keV."; RefreshRejectedReadoutProperty(nameof(WindowLowKeV)); RefreshRejectedReadoutProperty(nameof(WindowHighKeV)); return; }
        _windowLowKeV = low; _windowHighKeV = high; ReadoutMessage = null;
        OnPropertyChanged(nameof(WindowLowKeV)); OnPropertyChanged(nameof(WindowHighKeV));
        Spectrum.Refresh(); Imaging.RefreshChannels();
    }
    private void NotifyReadout()
    {
        foreach (string name in new[] { nameof(IsPhysicalReadout), nameof(CanUseLegacy), nameof(CanEditLegacy), nameof(ReadoutAssumptions), nameof(ReadoutState), nameof(Detector) }) OnPropertyChanged(name);
        if (IsPhysicalReadout) { Spectrum.PileUp = false; Imaging.Strip = false; Waveform.RateStudy = false; Waveform.Ideal = false; }
        PrepareReadoutCommand.NotifyCanExecuteChanged();
        DetectorWorkspace.Refresh();
    }
}
