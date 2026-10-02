using CommunityToolkit.Mvvm.ComponentModel;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.Optics;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>Selected channel and measurement session over a frozen acquisition scene.</summary>
public sealed partial class ImagingWorkspaceViewModel(MainViewModel shared, IImagingService? service = null, IFocusSweepService? focusService = null)
    : WorkspaceViewModel("Imaging", "Workspace.Imaging")
{
    private Guid _id;
    private IReadOnlyList<SceneSource> _scene = [];
    private OpticsSettings _optics = new();
    private CancellationTokenSource? _refresh;
    private int _revision;
    private bool _updating;
    [ObservableProperty] private string _focalPlane = "1000";
    [ObservableProperty] private string? _focusError;
    [ObservableProperty] private string? _focusNote;
    public double FocalDistanceMm { get; private set; } = 1000;
    public OpticsGeometry? Geometry => OpticsPolicy.ValidateFocus(ProjectionOptics, FocalDistanceMm) is null
        ? OpticsGeometry.Calculate(ProjectionOptics, FocalDistanceMm) : null;
    public string GeometryText => Geometry?.Description ?? "Choose a valid decoder focal plane.";
    public string SamplingEvidence => "Position-dependent precision: a 1 m Sharp scan measured RMS 0.95 mm at 1.27 samples/cell and 0.24 mm at 3.8, at the same detector size. This is conditional evidence, not a pass threshold.";
    private OpticsSettings ProjectionOptics => _scene.Count > 0 ? _optics : Shared.Optics;
    partial void OnFocalPlaneChanged(string value)
    {
        if (!double.TryParse(value, out double focal)) { FocusError = "Enter a numeric decoder focal plane."; return; }
        FocusError = OpticsPolicy.ValidateFocus(ProjectionOptics, focal);
        if (FocusError is not null) return;
        if (FocalDistanceMm == focal) return;
        FocalDistanceMm = focal;
        Measurements.ClearReconstruction();
        FocusNote = "Reconstruction measurements cleared: their mm plane changed. Flood measurements retained.";
        NotifyGeometryChanged();
        RefreshChannels();
    }
    internal void NotifyGeometryChanged()
    {
        FocusError = double.TryParse(FocalPlane, out double focal)
            ? OpticsPolicy.ValidateFocus(ProjectionOptics, focal) : "Enter a numeric decoder focal plane.";
        OnPropertyChanged(nameof(Geometry));
        OnPropertyChanged(nameof(GeometryText));
        Shared.NotifyFocalGeometryChanged();
        UseAsFocusCommand.NotifyCanExecuteChanged();
    }
    public MainViewModel Shared { get; } = shared;
    public MeasurementsViewModel Measurements { get; } = new();
    public Task WhenUpdated { get; private set; } = Task.CompletedTask;
    [ObservableProperty] private ImagingView? _view;
    [ObservableProperty] private string _selectedIsotope = "All";
    [ObservableProperty] private bool _strip;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isProcessing;
    public IReadOnlyList<string> Isotopes => View?.Channels.Select(c => c.Isotope).ToArray()
        ?? new[] { "All" }.Concat(_scene.Select(s => s.Isotope).Distinct()).ToArray();
    public ImagingChannel? SelectedChannel => View?.Channels.FirstOrDefault(c => c.Isotope == SelectedIsotope);
    public ImagingResult? Result => SelectedChannel?.Image ?? (SelectedIsotope == "All" ? Shared.Result : null);
    public IReadOnlyList<ImagingPeak> Peaks => SelectedChannel?.Peaks ?? [];
    public IReadOnlyList<StripRatio> Ratios => View?.Ratios ?? [];
    public string? PeakText => Result?.Estimate is { } e ? $"peak ({e.Position.X:F1}, {e.Position.Y:F1}) mm" : null;
    public string Summary => IsProcessing ? "Building channels / calibrating…" : Result is { } r
        ? $"{SelectedIsotope} · {r.EffectiveCounts:N0} counts · {Peaks.Count} found" : "No acquired counts";
    public string WorkerCosts => View is { } v
        ? $"Worker: channels {v.ChannelTime.TotalMilliseconds:F1} ms · calibration {v.CalibrationTime.TotalMilliseconds:F1} ms · decode {v.DecodeTime.TotalMilliseconds:F1} ms" : "";
    partial void OnSelectedIsotopeChanged(string value)
    {
        if (!Isotopes.Contains(value)) { SelectedIsotope = "All"; return; }
        InvalidateSweep();
        NotifyResult();
    }
    partial void OnStripChanged(bool value) { InvalidateSweep(); RefreshChannels(); }
    partial void OnIsProcessingChanged(bool value) { OnPropertyChanged(nameof(Summary)); SweepCommand.NotifyCanExecuteChanged(); }

    internal void Begin(IReadOnlyList<SceneSource> scene, OpticsSettings optics)
    {
        InvalidateSweep();
        _refresh?.Cancel();
        _refresh = new CancellationTokenSource();
        _updating = false;
        _revision++;
        _id = Guid.NewGuid();
        _scene = scene;
        _optics = optics;
        FocusNote = null;
        NotifyGeometryChanged();
        View = null;
        Error = null;
        IsProcessing = false;
        OnPropertyChanged(nameof(Isotopes));
        OnPropertyChanged(nameof(Ratios));
        OnPropertyChanged(nameof(WorkerCosts));
        if (!Isotopes.Contains(SelectedIsotope)) SelectedIsotope = "All";
        NotifyResult();
    }

    internal void Refresh(ImagingResult? result) => NotifyResult();
    internal void RefreshChannels()
    {
        _revision++;
        if (_updating) return; // A running preparation keeps its caches; one latest request follows it.
        _refresh ??= new CancellationTokenSource();
        WhenUpdated = UpdateAsync(_refresh.Token);
    }

    private async Task UpdateAsync(CancellationToken token)
    {
        if (service is null || Shared.Snapshot is null || _scene.Count == 0 || FocusError is not null) return;
        _updating = true;
        IsProcessing = true;
        int revision = _revision;
        try
        {
            do
            {
                revision = _revision;
                var snapshot = Shared.Snapshot!;
                var view = await service.ProcessAsync(_id, snapshot, _scene, _optics,
                    new(Shared.WindowFwhm, Strip, FocalDistanceMm), token);
                if (token.IsCancellationRequested) return;
                if (revision != _revision) continue;
                View = view;
                OnPropertyChanged(nameof(Ratios));
                OnPropertyChanged(nameof(WorkerCosts));
                NotifyResult();
                Error = null;
            } while (revision != _revision && FocusError is null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { if (revision == _revision) Error = $"Imaging failed: {ex.Message}"; }
        finally
        {
            // A cancelled old acquisition cannot change the new acquisition's worker state.
            if (!token.IsCancellationRequested) { _updating = false; IsProcessing = false; }
        }
    }

    private void NotifyResult()
    {
        SweepCommand.NotifyCanExecuteChanged();
        Measurements.Refresh(Result);
        foreach (string name in new[] { nameof(SelectedChannel), nameof(Result), nameof(Peaks), nameof(PeakText), nameof(Summary) })
            OnPropertyChanged(name);
    }
}
