using CommunityToolkit.Mvvm.ComponentModel;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>Selected channel and measurement session over a frozen acquisition scene.</summary>
public sealed partial class ImagingWorkspaceViewModel(MainViewModel shared, IImagingService? service = null)
    : WorkspaceViewModel("Imaging", "Workspace.Imaging")
{
    private Guid _id;
    private IReadOnlyList<SceneSource> _scene = [];
    private OpticsSettings _optics = new();
    private CancellationTokenSource? _refresh;
    private int _revision;
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
        NotifyResult();
    }
    partial void OnStripChanged(bool value) => RefreshChannels();
    partial void OnIsProcessingChanged(bool value) => OnPropertyChanged(nameof(Summary));

    internal void Begin(IReadOnlyList<SceneSource> scene, OpticsSettings optics)
    {
        _refresh?.Cancel();
        _revision++;
        _id = Guid.NewGuid();
        _scene = scene;
        _optics = optics;
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
        _refresh?.Cancel();
        _refresh?.Dispose();
        _refresh = new CancellationTokenSource();
        WhenUpdated = UpdateAsync(++_revision, _refresh.Token);
    }

    private async Task UpdateAsync(int revision, CancellationToken token)
    {
        // Optional service preserves the original raw-result harness; production DI always supplies it.
        if (service is null || Shared.Snapshot is not { } snapshot || _scene.Count == 0) return;
        IsProcessing = true;
        try
        {
            var view = await service.ProcessAsync(_id, snapshot, _scene, _optics,
                new(Shared.WindowFwhm, Strip), token);
            if (revision != _revision || token.IsCancellationRequested) return;
            View = view;
            OnPropertyChanged(nameof(Ratios));
            OnPropertyChanged(nameof(WorkerCosts));
            NotifyResult();
            Error = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { if (revision == _revision) Error = $"Imaging failed: {ex.Message}"; }
        finally { if (revision == _revision) IsProcessing = false; }
    }

    private void NotifyResult()
    {
        Measurements.Refresh(Result);
        foreach (string name in new[] { nameof(SelectedChannel), nameof(Result), nameof(Peaks), nameof(PeakText), nameof(Summary) })
            OnPropertyChanged(name);
    }
}
