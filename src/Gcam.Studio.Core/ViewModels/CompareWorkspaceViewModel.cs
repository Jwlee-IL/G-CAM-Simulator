using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Plotting;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>One latest pending request, one worker, and one atomic displayed pair. Acquisition never awaits this VM.</summary>
public sealed partial class CompareWorkspaceViewModel : WorkspaceViewModel
{
    private readonly IReadoutCompareService? _service;
    private Guid _id = Guid.NewGuid();
    private int _revision;
    private CompareSettings? _settings;
    private Request? _pending;
    private CancellationTokenSource? _worker;
    private CompareKey? _running;
    private IReadOnlyList<SceneSource> _scene = [];
    private sealed record Request(CompareKey Key, AcquisitionSnapshot Snapshot, CompareSettings Settings);

    public CompareWorkspaceViewModel(MainViewModel shared, IReadoutCompareService? service) : base("Compare", "Workspace.Compare")
    {
        Shared = shared; _service = service;
        shared.PropertyChanged += SharedChanged;
        shared.Imaging.PropertyChanged += ImagingChanged;
    }

    public MainViewModel Shared { get; }
    public Task WhenUpdated { get; private set; } = Task.CompletedTask;
    [ObservableProperty] private CompareResult? _result;
    [ObservableProperty] private bool _isUpdating;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _showDifference;
    partial void OnResultChanged(CompareResult? value) => Notify();
    partial void OnIsUpdatingChanged(bool value) => OnPropertyChanged(nameof(Updating));
    public bool ShowDirectMode => !Shared.IsPhysicalReadout;
    public bool ShowStart => Shared.IsPhysicalReadout && Shared.Snapshot is null;
    public bool ShowData => Shared.IsPhysicalReadout && Shared.Snapshot is not null;
    public bool ShowPair => ShowData && !ShowDifference;
    public bool ShowAssignment => ShowData && ShowDifference;
    public string Prefix => Result is { } r ? $"Shown prefix {r.Key.LiveTimeS:0.###} s · {r.Key.HitCount:N0} histories · {r.Key.HeldCount:N0} held" : "No comparison yet";
    public string Updating => IsUpdating ? "Updating comparison…" : "";
    public string Selection => Result is { Settings: { } s }
        ? $"Shown: {(s.Window ? "Energy window" : "All")} · {s.Imaging.WindowLowKeV:0.##}–{s.Imaging.WindowHighKeV:0.##} keV · {s.Imaging.Method}"
        : $"Imaging: {Shared.Imaging.SelectedIsotope} · {Shared.WindowLowKeV:0.##}–{Shared.WindowHighKeV:0.##} keV · {Shared.Imaging.Reconstruction}";
    public string? EmptyReason => Error ?? (Result is not { } r ? null : r.Anger.Unavailable ??
        (r.Accounting.Held == 0 ? "No held conversions yet; direct inputs are retained." :
            r.Accounting.Assigned == 0 ? "No assigned conversions; direct image is retained." :
            r.Anger.Image.EffectiveCounts == 0 ? "No Anger events in this window; direct image is retained." : null));
    public string SameShare => Result?.Accounting.SameShare is { } share ? $"Same crystal {share:P2}" : "Same crystal unavailable: no eligible conversions";
    public string AssignmentHelp => "Different / eligible per direct crystal. Eligible = assigned held conversions with one significant contributor; Window gates Anger measured energy. Unknowns and pile-up are separate. Neutral cells have no denominator. Never dominant is not a lost-history count. No recorded contribution counts finalized inputs with no positive noiseless contribution at any recorded hold; unresolved inputs await pulse-support expiry. Threshold and dead-time causes are not separated.";
    public IReadOnlyList<SourceItemViewModel> TruthMarkers => _scene.Select(s => new SourceItemViewModel
        { Isotope = s.Isotope, X = s.X, Y = s.Y, DistanceMm = s.DistanceMm, IsEditable = false }).ToArray();
    public IReadOnlyList<ImagingPeak> DirectPeaks => Peaks(Result?.Direct);
    public IReadOnlyList<ImagingPeak> AngerPeaks => Peaks(Result?.Anger);
    private IReadOnlyList<ImagingPeak> Peaks(ImagingChannel? channel)
    {
        if (channel?.Image.Estimate is not { } estimate) return [];
        // Column headings already identify the path. Only name an isotope when the scene/window identifies it.
        var isotopes = _scene.Where(s => Result?.Settings.Window != true ||
            (channel.CalibrationLineKeV ?? Result?.Spectrum.CalibrationLineKeV) is not { } line ||
            Isotopes.Get(s.Isotope).Lines.Any(l => l.EnergyKeV == line)).Select(s => s.Isotope).Distinct().ToArray();
        return [new(isotopes.Length == 1 ? isotopes[0] : "peak", estimate.Position.X, estimate.Position.Y, estimate.Confidence)];
    }
    public IReadOnlyList<FactRow> Facts
    {
        get
        {
            if (Result is not { } r) return [];
            var a = r.Accounting;
            return [new("Histories / held", $"{a.Histories:N0} / {a.Held:N0}"), new("Assigned / unknown", $"{a.Assigned:N0} / {a.Unknown:N0}"),
                new("Multiple contributors", $"{a.MultiContributor:N0}"), new("No dominant", $"{a.NoDominant:N0}"),
                new("Distinct dominant", $"{a.DistinctDominant:N0}"), new("Never dominant", $"{a.NeverDominant:N0} (not loss)"),
                new("No recorded contribution", a.Participation is { } p ? $"{p.NoRecordedContribution:N0} / {p.FinalizedHistories:N0} finalized · {p.UnresolvedHistories:N0} unresolved" : "Unavailable"),
                new("Same / eligible", $"{a.Same:N0} / {a.Eligible:N0}"),
                new("Direct window", $"{a.DirectWindow:N0}"), new("Anger window", $"{a.AngerWindow:N0}"),
                new("Direct estimate", Estimate(r.Direct)), new("Anger estimate", Estimate(r.Anger)),
                new("Direct truth error", TruthError(r.Direct)), new("Anger truth error", TruthError(r.Anger)),
                new("Path separation", Separation(r)), new("Direct width", "Ideal deposit energy"), new("Anger FWHM", "Not estimated"),
                new("Spectrum overflow", $"Direct {r.Spectrum.DirectOverflow:N0} · Anger {r.Spectrum.AngerOverflow:N0}"),
                new("Worker", $"aggregate {r.Timings.Aggregate.TotalMilliseconds:0.0} · decode {r.Timings.Decode.TotalMilliseconds:0.0} · total {r.Timings.Total.TotalMilliseconds:0.0} ms")];
        }
    }
    private static string Estimate(ImagingChannel channel) => channel.Image.Estimate is { } e ? $"({e.Position.X:0.###}, {e.Position.Y:0.###}) mm" : "Unavailable";
    private string TruthError(ImagingChannel channel)
    {
        if (_scene.Count != 1 || channel.Image.Estimate is not { } e) return "Unavailable (requires one source and estimate)";
        var s = _scene[0]; var truth = new Vector3(s.X, s.Y, s.DistanceMm);
        return $"{Math.Sqrt(Math.Pow(e.Position.X - s.X, 2) + Math.Pow(e.Position.Y - s.Y, 2)):0.###} mm · {Angle(e.Position, truth):0.####}°";
    }
    private static double Angle(Vector3 a, Vector3 b) => Math.Acos(Math.Clamp(a.Normalized().Dot(b.Normalized()), -1, 1)) * 180 / Math.PI;
    private static string Separation(CompareResult r) => r.Direct.Image.Estimate is { } d && r.Anger.Image.Estimate is { } a
        ? $"{(d.Position - a.Position).Length:0.###} mm · {Angle(d.Position, a.Position):0.####}°" : "Unavailable";
    public IReadOnlyList<PlotSeries> Spectrum => Result is { Spectrum.Unavailable: null } r
        ? [new("Direct · solid", r.Spectrum.Direct, Origin: 1, Step: 2),
            new("Anger · dashed", r.Spectrum.Anger, Origin: 1, Step: 2, ColourRole: PlotColourRole.Series2) { Dashed = true }] : [];
    public IReadOnlyList<PlotBand> Bands => Result is { Settings: { } s }
        ? [new(s.Imaging.WindowLowKeV, s.Imaging.WindowHighKeV, "Shared window")] : [];
    public string SpectrumCaption => Result is { } r ? $"Direct solid · Anger dashed · {r.Spectrum.CalibrationLineKeV:0.##} keV calibration · raw counts" : "Raw counts";
    public double FloodMaximum => Result is { } r ? Math.Max(r.Direct.Image.Flood.Raw.ToArray().Max(), r.Anger.Image.Flood.Raw.ToArray().Max()) : 0;
    public double ReconMinimum => ReconRange().Min;
    public double ReconMaximum => ReconRange().Max;
    private (double Min, double Max) ReconRange()
    {
        var values = new[] { Result?.Direct.Image.Reconstruction, Result?.Anger.Image.Reconstruction }
            .OfType<DetectorImage>().SelectMany(i => i.Raw.ToArray()).ToArray();
        return values.Length > 0 ? (values.Min(), values.Max()) : (0, 0);
    }
    public string Reference => Result is { } r ? "Benchmark reference only; not a prediction for this scene.\n" +
        string.Join("\n", new[] { r.Cost.Unavailable }.OfType<string>().Concat(r.Cost.Differences)) : "Reference available after a physical acquisition";
    public string CostHelp => !ShowData ? "" : ReadoutCostHelp.Build(Shared.ReadoutOffers, Result?.Cost.Cell is { } cell && Shared.Snapshot?.Readout is { } readout
        ? readout.Lines.Select(l => l.Preparation.Plan).OfType<ReadoutLinePlan>().Where(p => p.Cell == cell) : []);
    public IReadOnlyList<FactRow> Costs => Result?.Cost.Cell is { } cell ? cell.CostRows.SelectMany(r => new[]
    {
        new FactRow($"{r.TargetDirectCounts:N0} ideal-Direct · {(r.RateCps == 0 ? "isolated" : "100,000 hits/s")}", $"F {Interval(r.F)}"),
        new FactRow("Direct RMS / remote", CostPath(r.Direct)),
        new FactRow("Anger RMS / remote", CostPath(r.Anger)),
    }).ToArray() : [];
    private static string Interval(ReadoutInterval? value) => value is { } v ? $"{v.Estimate:0.####} [{v.Lower95:0.####}, {v.Upper95:0.####}]" : "Unavailable";
    private static string CostPath(ReadoutCostPath path) => path.Available
        ? $"{Interval(path.RmsDeg)}° / {Interval(path.RemoteProbability)}"
        : $"Unavailable · {path.MissingClusters} missing seed clusters";

    internal void Begin(IReadOnlyList<SceneSource> scene)
    {
        Cancel(); _id = Guid.NewGuid(); _scene = scene; _settings = null; Result = null; Error = null; Notify();
    }
    internal void Reset() => Begin([]);
    private void SharedChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.Snapshot) or nameof(MainViewModel.IsPhysicalReadout)
            or nameof(MainViewModel.WindowLowKeV) or nameof(MainViewModel.WindowHighKeV) or nameof(MainViewModel.ApplyFloodCorrection)) Refresh();
    }
    private void ImagingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImagingWorkspaceViewModel.SelectedIsotope) or nameof(ImagingWorkspaceViewModel.Reconstruction)
            or nameof(ImagingWorkspaceViewModel.FocalPlane) or nameof(ImagingWorkspaceViewModel.FocusError)) Refresh();
    }
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsActive)) Refresh();
    }
    partial void OnShowDifferenceChanged(bool value) => Notify();
    private void Cancel() { _pending = null; _running = null; _worker?.Cancel(); _worker = null; IsUpdating = false; }
    public void Refresh()
    {
        Notify();
        if (!IsActive || !Shared.IsPhysicalReadout || Shared.Snapshot?.Readout is not { } readout || _service is null)
        { Cancel(); return; }
        if (Shared.Imaging.FocusError is not null) { Cancel(); Error = Shared.Imaging.FocusError; Notify(); return; }
        if (_scene.Count == 0) _scene = Shared.Sources.Select(s => s.ToModel()).ToArray();
        var settings = new CompareSettings(new(Shared.WindowFwhm, false, Shared.Imaging.FocalDistanceMm, Shared.Imaging.Reconstruction)
        { WindowLowKeV = Shared.WindowLowKeV, WindowHighKeV = Shared.WindowHighKeV, ApplyFloodCorrection = Shared.ApplyFloodCorrection }, Shared.Imaging.SelectedIsotope != "All");
        if (settings != _settings) { Cancel(); _revision++; _settings = settings; }
        var key = new CompareKey(_id, _revision, readout.Hits.Count, readout.Records.Count, Shared.Snapshot.LiveTimeS);
        // Imaging and shared-setting notifications repeat without a new prefix; the same key is already running or shown.
        if (key == _running || (_worker is null && Result?.Key == key)) return;
        _pending = new(key, Shared.Snapshot, settings);
        if (_worker is not null) return;
        _worker = new(); WhenUpdated = UpdateAsync(_worker);
    }
    private async Task UpdateAsync(CancellationTokenSource worker)
    {
        IsUpdating = true; Notify();
        try
        {
            await Task.Yield();
            while (_pending is not null)
            {
                var request = _pending; _pending = null; _running = request.Key;
                var started = Stopwatch.StartNew();
                var result = await _service!.ProcessAsync(request.Key, request.Snapshot, _scene, request.Settings, worker.Token);
                if (worker.IsCancellationRequested || request.Key.AcquisitionId != _id || request.Key.SettingsRevision != _revision) return;
                Result = result; Error = null; Notify();
                // Snapshot cadence is 4 Hz. Coalesce bursts without cancelling an otherwise valid calculation.
                var wait = TimeSpan.FromMilliseconds(250) - started.Elapsed;
                if (_pending is not null && wait > TimeSpan.Zero) await Task.Delay(wait, worker.Token);
            }
        }
        catch (OperationCanceledException) when (worker.IsCancellationRequested) { }
        catch (Exception ex) { if (!worker.IsCancellationRequested) { Error = $"Comparison failed: {ex.Message}"; Notify(); } }
        finally
        {
            if (ReferenceEquals(worker, _worker)) { _worker = null; _running = null; IsUpdating = false; Notify(); }
            worker.Dispose();
        }
    }
    private void Notify()
    {
        foreach (string name in new[] { nameof(ShowDirectMode), nameof(ShowStart), nameof(ShowData), nameof(ShowPair), nameof(ShowAssignment),
            nameof(Prefix), nameof(Updating), nameof(Selection), nameof(EmptyReason), nameof(SameShare), nameof(Facts), nameof(Spectrum), nameof(Bands),
            nameof(SpectrumCaption), nameof(Reference), nameof(CostHelp), nameof(Costs), nameof(TruthMarkers), nameof(DirectPeaks), nameof(AngerPeaks),
            nameof(FloodMaximum), nameof(ReconMinimum), nameof(ReconMaximum) }) OnPropertyChanged(name);
    }
}
