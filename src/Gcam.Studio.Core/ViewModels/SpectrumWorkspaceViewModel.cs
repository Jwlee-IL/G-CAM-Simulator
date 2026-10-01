using CommunityToolkit.Mvvm.ComponentModel;
using Gcam.Configuration;
using Gcam.Studio.Core.Plotting;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>View-only settings over the shared acquisition; late responses cannot overwrite newer data.</summary>
public sealed partial class SpectrumWorkspaceViewModel : WorkspaceViewModel
{
    private readonly ISpectrumService _service;
    private Guid _acquisitionId;
    private IReadOnlyList<SpectrumLine> _lines = [];
    private CancellationTokenSource? _refresh;
    private int _revision;
    private bool _updatingSelection;

    public SpectrumWorkspaceViewModel(MainViewModel shared, ISpectrumService service)
        : base("Spectrum", "Workspace.Spectrum")
    { Shared = shared; _service = service; }

    public MainViewModel Shared { get; }
    public Task WhenUpdated { get; private set; } = Task.CompletedTask;
    [ObservableProperty] private bool _logY = true;
    public double WindowFwhm { get => Shared.WindowFwhm; set => Shared.WindowFwhm = value; }
    [ObservableProperty] private bool _pileUp;
    [ObservableProperty] private SpectrumView? _view;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private IReadOnlyList<PlotSeries> _series = [];
    [ObservableProperty] private IReadOnlyList<PlotBand> _bands = [];
    [ObservableProperty] private SpectrumBand? _selectedLine;
    [ObservableProperty] private PlotViewRange? _viewRange;

    partial void OnSelectedLineChanged(SpectrumBand? value)
    {
        if (value is null || _updatingSelection) return;
        double width = value.HiKeV - value.LoKeV;
        ViewRange = new(value.LoKeV - width, value.HiKeV + width);
    }
    public IReadOnlyList<SpectrumBand> Lines => View?.Bands ?? [];
    public string Summary => View is { } v
        ? $"{v.TotalCounts:N0} measured pulses · {v.InWindowShare:P1} in windows · {v.OverflowCounts:N0} above plot range" : "No acquired counts";
    public string Chain => Shared.Snapshot?.Chain.ToString() ?? "No acquired chain";
    public string Resolution => $"{(View?.Resolution662 ?? _service.Resolution662):P2} FWHM at 662 keV (single channel)";
    public string ResolvingTime => $"Effective resolving interval {(View?.ResolvingTimeS ?? _service.ResolvingTimeS) * 1e9:F0} ns";

    internal void NotifyWindowChanged()
    {
        OnPropertyChanged(nameof(WindowFwhm));
        Refresh();
    }
    partial void OnPileUpChanged(bool value) => Refresh();

    internal void Begin(IReadOnlyList<SceneSource> scene)
    {
        _refresh?.Cancel();
        _revision++;
        _acquisitionId = Guid.NewGuid();
        _lines = scene.SelectMany(s => Isotopes.Get(s.Isotope).Lines.Select(l => new SpectrumLine(s.Isotope, l.EnergyKeV)))
            .Distinct().ToArray();
        View = null;
        Series = [];
        Bands = [];
        SelectedLine = null;
        ViewRange = null;
        Error = null;
        NotifyReadings();
    }

    internal void Refresh()
    {
        _refresh?.Cancel();
        _refresh?.Dispose();
        _refresh = new CancellationTokenSource();
        int revision = ++_revision;
        WhenUpdated = UpdateAsync(revision, _refresh.Token);
    }

    private async Task UpdateAsync(int revision, CancellationToken token)
    {
        if (Shared.Snapshot is not { } snapshot || _lines.Count == 0) return;
        try
        {
            var view = await _service.ProcessAsync(_acquisitionId, snapshot.Events, _lines,
                new SpectrumSettings(WindowFwhm, PileUp)
                {
                    Detector = snapshot.Detector,
                    PixelsX = snapshot.Detector is null ? 0 : snapshot.Imaging.Flood.Width,
                    PixelsY = snapshot.Detector is null ? 0 : snapshot.Imaging.Flood.Height
                }, cancellationToken: token);
            if (revision != _revision || token.IsCancellationRequested) return;
            var selected = SelectedLine;
            View = view;
            // Replace ItemsSource before assigning a row from the new list; otherwise WPF rejects it.
            NotifyReadings();
            // Snapshot rows are immutable replacements; rebinding selection must not re-zoom a live view.
            _updatingSelection = true;
            SelectedLine = selected is null ? null : view.Bands.FirstOrDefault(b => b.Lines.SequenceEqual(selected.Lines));
            _updatingSelection = false;
            Series = [new PlotSeries("Acquired counts", view.Counts, Kind: PlotKind.Histogram, BinEdges: view.BinEdgesKeV)];
            Bands = view.Bands.Select(b => new PlotBand(b.LoKeV, b.HiKeV, b.Label)).ToArray();
            Error = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (revision == _revision) Error = $"Spectrum failed: {ex.Message}";
        }
    }

    private void NotifyReadings()
    {
        OnPropertyChanged(nameof(Chain));
        OnPropertyChanged(nameof(Lines));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Resolution));
        OnPropertyChanged(nameof(ResolvingTime));
    }
}
