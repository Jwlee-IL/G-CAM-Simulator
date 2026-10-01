using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Studio.Core.Plotting;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>Visible-only scope requests with held-event reuse and latest-request publication.</summary>
public sealed partial class WaveformWorkspaceViewModel(MainViewModel shared, IWaveformService? service)
    : WorkspaceViewModel("Waveform", "Workspace.Waveform")
{
    private CancellationTokenSource? _refresh;
    private int _revision;
    private (WaveformSettings Settings, double CoveredUntil, DetectorSettings? Detector)? _last;
    private bool _updating;
    public MainViewModel Shared { get; } = shared;
    public Task WhenUpdated { get; private set; } = Task.CompletedTask;
    [ObservableProperty] private int _triggerIndex;
    [ObservableProperty] private bool _followLatest = true;
    [ObservableProperty] private double _windowUs = 10;
    [ObservableProperty] private bool _rateStudy;
    [ObservableProperty] private double _rateKcps = 50;
    [ObservableProperty] private bool _ideal;
    [ObservableProperty] private WaveformView? _view;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private PlotViewRange? _viewRange;
    public IReadOnlyList<PlotSeries> AdcSeries => View is { } v ? new[] { v.Adc } : [];
    public IReadOnlyList<PlotSeries> ShapedSeries => View is { } v ? new[] { v.Shaped } : [];
    public IReadOnlyList<PlotMarker> Markers => View?.Events.Select(e => new PlotMarker(e.RelativeTimeUs, e.Label)).ToArray() ?? [];
    public IReadOnlyList<WaveformEvent> Events => View?.Events ?? [];
    public string Summary => View is { } v ? $"{v.Events.Count} events · {v.WindowUs:0.###} µs · worker {v.ProcessingTime.TotalMilliseconds:0.##} ms" : "Select an acquired event";
    partial void OnTriggerIndexChanged(int value) { if (!_updating) { FollowLatest = false; Refresh(); } }
    partial void OnFollowLatestChanged(bool value) => NotifySnapshot();
    partial void OnWindowUsChanged(double value) { ViewRange = null; Refresh(); }
    partial void OnRateStudyChanged(bool value) => Refresh();
    partial void OnRateKcpsChanged(double value) => Refresh();
    partial void OnIdealChanged(bool value) => Refresh();
    partial void OnViewChanged(WaveformView? value)
    {
        if (value is not null && ViewRange is null && value.Adc.Y.Length > 0)
            ViewRange = new(value.Adc.Origin, value.Adc.XAt(value.Adc.Y.Length - 1));
        foreach (string name in new[] { nameof(AdcSeries), nameof(ShapedSeries), nameof(Markers), nameof(Events), nameof(Summary) })
            OnPropertyChanged(name);
    }

    [RelayCommand]
    private void Latest() { FollowLatest = true; NotifySnapshot(); }
    [RelayCommand]
    private void Next()
    {
        FollowLatest = false;
        if (Shared.Snapshot is { } s && TriggerIndex + 1 < s.Events.Count) TriggerIndex++;
    }
    internal void Begin()
    {
        _refresh?.Cancel(); _revision++; _last = null; View = null; Error = null; ViewRange = null;
        _updating = true; TriggerIndex = 0; _updating = false;
    }
    internal void NotifySnapshot()
    {
        if (FollowLatest && Shared.Snapshot is { Events.Count: > 0 } s)
        {
            _updating = true; TriggerIndex = s.Events.Count - 1; _updating = false;
        }
        Refresh();
    }
    public void Refresh()
    {
        if (!IsActive) { _refresh?.Cancel(); _revision++; _last = null; return; }
        if (service is null || Shared.Snapshot is not { } snapshot) return;
        var settings = new WaveformSettings(TriggerIndex, WindowUs, RateStudy, RateKcps, Ideal);
        double trigger = snapshot.Events.Count > 0 && TriggerIndex >= 0 && TriggerIndex < snapshot.Events.Count
            ? snapshot.Events[TriggerIndex].ArrivalTimeS : snapshot.LiveTimeS;
        double covered = RateStudy ? snapshot.Events.Count : Math.Min(snapshot.LiveTimeS, trigger + WindowUs * 0.8e-6);
        var key = (settings, covered, snapshot.Detector);
        if (_last == key && Error is null) return;
        _last = key;
        _refresh?.Cancel(); _refresh?.Dispose(); _refresh = new CancellationTokenSource();
        int revision = ++_revision;
        WhenUpdated = UpdateAsync(snapshot, settings, revision, _refresh.Token);
    }
    private async Task UpdateAsync(AcquisitionSnapshot snapshot, WaveformSettings settings, int revision, CancellationToken token)
    {
        try
        {
            var view = await service!.ProcessAsync(snapshot, settings, token);
            if (revision != _revision || token.IsCancellationRequested) return;
            View = view; Error = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { if (revision == _revision) { Error = $"Waveform failed: {ex.Message}"; _last = null; } }
    }
}
