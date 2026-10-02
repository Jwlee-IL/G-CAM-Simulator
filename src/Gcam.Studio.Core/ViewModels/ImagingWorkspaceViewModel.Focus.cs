using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Studio.Core.Optics;
using Gcam.Studio.Core.Plotting;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

public sealed partial class ImagingWorkspaceViewModel
{
    private CancellationTokenSource? _sweepCancellation;
    private int _sweepRevision;
    [ObservableProperty] private int _peakCount = 1;
    [ObservableProperty] private FocusSweepResult? _sweepResult;
    [ObservableProperty] private bool _isSweeping;
    [ObservableProperty] private string? _sweepError;
    [ObservableProperty] private string _externalRange = "";
    [ObservableProperty] private string? _externalRangeError;
    public IReadOnlyList<int> PeakCounts { get; } = Array.AsReadOnly(new[] { 1, 2, 3, 4 });
    public double? ExternalRangeMm { get; private set; }
    public bool HasSweepResult => SweepResult is not null;
    public string SweepNote => "Experimental focus analysis: reproducible near-field bias depends on lateral position. At default optics, intervals reach the far sweep edge from about 700 mm. Half-max width is not uncertainty; stripped floods are fractional, clipped values, not independent Poisson counts. External range measures a surface, not necessarily the gamma source.";
    public string SweepSummary => IsSweeping ? "Sweeping retained flood…" : SweepResult is { } r
        // U+00A0 keeps each value with its unit when the side panel wraps the line.
        ? $"{r.Identity.Channel} · retained at {r.Identity.LiveTimeS:0.0} s · {r.Identity.Counts:N0} acquired counts · worker {r.ProcessingTime.TotalMilliseconds:0} ms"
            + (r.Tracks.Count == 0 ? " · unresolved (no complete peak track)" : "")
        : "Sweep selected channel's retained flood";
    public IReadOnlyList<PlotSeries> FocusSeries => SweepResult?.Tracks.Select((t, i) =>
        new PlotSeries($"Peak {i + 1}", t.Curve.Select(p => p.Prominence).ToArray(),
            t.Curve.Select(p => p.PlaneMm).ToArray(), ColourRole: (PlotColourRole)(i % 3))).ToArray() ?? [];
    public IReadOnlyList<PlotBand> FocusBands => SweepResult?.Tracks.Select((t, i) =>
        new PlotBand(t.Interval.LoMm, t.Interval.HiMm, $"{i + 1}: half-max {t.Interval.Description}")).ToArray() ?? [];
    public IReadOnlyList<PlotMarker> FocusMarkers => (SweepResult?.Tracks.Select((t, i) =>
        new PlotMarker(t.Sharpest.PlaneMm, $"{i + 1}: sharpest plane")) ?? []).Concat(
            ExternalRangeMm is { } range ? new[] { new PlotMarker(range, "External surface range") } : []).ToArray();
    partial void OnPeakCountChanged(int value)
    {
        if (value is < 1 or > 4) { PeakCount = Math.Clamp(value, 1, 4); return; }
        InvalidateSweep();
    }
    partial void OnExternalRangeChanged(string value)
    {
        ExternalRangeMm = null; ExternalRangeError = null;
        if (!string.IsNullOrWhiteSpace(value))
        {
            if (!double.TryParse(value, out double range) || !double.IsFinite(range) || range <= 0)
                ExternalRangeError = "Enter a finite positive external surface range in mm, or leave blank.";
            else ExternalRangeMm = range;
        }
        OnPropertyChanged(nameof(FocusMarkers));
        UseAsFocusCommand.NotifyCanExecuteChanged();
    }
    private bool CanUseAsFocus() => ExternalRangeMm is { } range && OpticsPolicy.ValidateFocus(ProjectionOptics, range) is null;
    [RelayCommand(CanExecute = nameof(CanUseAsFocus))]
    private void UseAsFocus() => FocalPlane = ExternalRangeMm!.Value.ToString("G", System.Globalization.CultureInfo.CurrentCulture);
    partial void OnIsSweepingChanged(bool value)
    {
        SweepCommand.NotifyCanExecuteChanged(); CancelSweepCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SweepSummary));
    }
    partial void OnSweepResultChanged(FocusSweepResult? value)
    {
        foreach (string name in new[] { nameof(FocusSeries), nameof(FocusBands), nameof(FocusMarkers), nameof(SweepSummary), nameof(HasSweepResult) }) OnPropertyChanged(name);
        if (value is not null) IsSweepExpanded = true; // a result opens its section so its tracks and caveats are read
    }
    internal void InvalidateSweep()
    {
        _sweepRevision++; _sweepCancellation?.Cancel();
        SweepResult = null; SweepError = null; IsSweeping = false;
        SweepCommand.NotifyCanExecuteChanged(); UseAsFocusCommand.NotifyCanExecuteChanged();
    }
    private bool CanSweep() => focusService is not null && !IsSweeping && !IsProcessing && Shared.Snapshot is not null && Result is { EffectiveCounts: > 0 };
    [RelayCommand(CanExecute = nameof(IsSweeping))]
    private void CancelSweep() => InvalidateSweep();
    [RelayCommand(CanExecute = nameof(CanSweep))]
    private async Task SweepAsync()
    {
        if (!CanSweep()) return;
        var snapshot = Shared.Snapshot!;
        var identity = new FocusSweepIdentity(_id, snapshot.Counts, snapshot.LiveTimeS, SelectedIsotope,
            Shared.WindowFwhm, Strip, snapshot.Optics ?? _optics, snapshot.Detector ?? new DetectorSettings(), PeakCount);
        var request = new FocusSweepRequest(identity, Result!);
        _sweepCancellation?.Dispose(); _sweepCancellation = new CancellationTokenSource();
        var token = _sweepCancellation.Token;
        int revision = ++_sweepRevision;
        SweepResult = null; SweepError = null; IsSweeping = true;
        try
        {
            var result = await focusService!.SweepAsync(request, token);
            if (revision == _sweepRevision && !token.IsCancellationRequested && result.Identity == identity) SweepResult = result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { if (revision == _sweepRevision) SweepError = $"Focus sweep failed: {ex.Message}"; }
        finally { if (revision == _sweepRevision) IsSweeping = false; }
    }
}
