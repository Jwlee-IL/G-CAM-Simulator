using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

public sealed partial class MainViewModel
{
    private readonly IReadoutPreparationService? _readoutPreparation;
    // Keep only compact failure messages; reusable views belong to the live service cache.
    private readonly Dictionary<string, string> _preparationFailures = [];
    private ReadoutMode _readoutMode;
    public IReadOnlyList<ReadoutMode> ReadoutModes { get; } = [ReadoutMode.DirectCrystal, ReadoutMode.FourOutputAnger];
    public ReadoutMode ReadoutMode
    {
        get => _readoutMode;
        set
        {
            if (!CanEditInputs || !ReadoutModes.Contains(value)) { RefreshRejectedReadoutProperty(nameof(ReadoutMode)); return; }
            if (SetProperty(ref _readoutMode, value))
            {
                ReadoutMessage = null; NotifyReadout();
                OnPropertyChanged(nameof(AmbientEnvironmentLabel));
            }
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
    /// <summary>The inline one-liner of the readout section; <see cref="ReadoutAssumptions"/> is its help text.</summary>
    public string ReadoutModeSummary => IsPhysicalReadout ? "Experimental GAGG four-output readout" : "Direct crystal assignment";
    public string ReadoutLocalisationHelp => ReadoutCostHelp.Build(ReadoutOffers, ReadoutPlans);
    public string ReadoutAssumptions => IsPhysicalReadout
        ? "Experimental GAGG · DPC 0.01 · sum trigger 50 keV-equivalent · specular 0.98 · virtual matched SiPMs · unlimited cells; no RC, skew or jitter. Legacy gain and chain are inactive. Source, BSR and ambient deposits share one measured pulse stream. BSR schedules detected inputs against the running source rate, before trigger or assignment; unmasked front-entry cosine flux uses module gaps and passive proxies, with no random pixel relocation. Only the source's preparable analysis lines are calibrated; positioning other-energy deposits through those calibrations is unvalidated. Field-only scenes remain unavailable. " + AmbientIncidenceDescription.PhysicalHelp
        : "Direct crystal assignment; legacy detector gain and chain response.";

    /// <summary>The shipped offer / cost record (unavailable without a preparation service or when malformed).</summary>
    public ReadoutOfferRecord ReadoutOffers => _readoutPreparation?.Offers
        ?? ReadoutOfferRecord.Unavailable("No readout preparation service is available.");

    [ObservableProperty] private string? _readoutMessage;
    [ObservableProperty] private bool _isPreparingReadout;
    // The state line and the ready table switch to "Preparing…" as soon as a preparation starts.
    partial void OnIsPreparingReadoutChanged(bool value)
    {
        OnPropertyChanged(nameof(ReadoutState));
        OnPropertyChanged(nameof(ReadoutStateLine));
        OnPropertyChanged(nameof(ReadoutReadyRows));
    }

    /// <summary>The Preparation section's own state line: a short "Preparing…" while running (the progress itself is in the
    /// status bar), otherwise <see cref="ReadoutState"/> (ready summary, failure, not prepared). <see cref="ReadoutState"/>
    /// keeps the full text, progress included, as the section's accessible name.</summary>
    public string ReadoutStateLine => IsPreparingReadout ? "Preparing…" : ReadoutState;

    /// <summary>A preparation failure (or refusal) shown in the status bar, in the warning colour, until the next start or Reset.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusWarning))]
    private bool _isPreparationFailed;

    /// <summary>The status text is a warning: a refused Start or a failed readout preparation.</summary>
    public bool IsStatusWarning => IsStartRefused || IsPreparationFailed;

    private void SetPreparationStatus(string text, bool warning)
    {
        Status = text;
        IsPreparationFailed = warning;
    }
    [ObservableProperty] private double _readoutProgress;
    [ObservableProperty] private string? _readoutProgressText;

    /// <summary>The pending detector inputs without prepared ids: what the module fingerprint and plans are built from.</summary>
    private DetectorSettings ModuleDetector => new() { ReadoutMode = ReadoutMode, GainSigma = GainSigmaPercent / 100, GainSeed = GainSeed, Chain = Chain, ReflectorGapMm = PendingGapMm };

    private string ModuleKey => ReadoutPolicy.Key(Optics, ModuleDetector);

    /// <summary>Every analysis line's plan for the pending module.</summary>
    public IReadOnlyList<ReadoutLinePlan> ReadoutPlans => ReadoutPolicy.Plans(Optics, ModuleDetector, ReadoutOffers);

    /// <summary>Lines the next physical acquisition must capture: present in the scene and preparable.</summary>
    public IReadOnlyList<ReadoutLinePlan> RequiredReadoutLines => IsPhysicalReadout
        ? ReadoutPolicy.RequiredLines(Sources.Select(s => s.ToModel()), Optics, ModuleDetector, ReadoutOffers) : [];

    private ReadoutPreparation? Prepared(ReadoutLinePlan plan)
        => _readoutPreparation?.FindCached(ReadoutPolicy.CalibrationKey(ModuleKey, plan));

    /// <summary>Successful preparations of the required lines, reference line first (661.7 keV when captured).</summary>
    public IReadOnlyList<ReadoutPreparation> MatchingBank => RequiredReadoutLines.Where(p => Failure(p) is null).Select(Prepared)
        .Where(p => p is { Succeeded: true }).Cast<ReadoutPreparation>()
        .OrderBy(p => p.LineKeV == ReadoutPolicy.ReferenceLineKeV ? 0 : 1).ThenBy(p => p.LineKeV).ToArray();

    private string? Failure(ReadoutLinePlan plan) => _preparationFailures.GetValueOrDefault(ReadoutPolicy.CalibrationKey(ModuleKey, plan));
    private readonly SynchronizationContext? _readoutContext = SynchronizationContext.Current;
    private void ReadoutCacheChanged(object? sender, EventArgs e)
    {
        if (_readoutContext is { } context && SynchronizationContext.Current != context)
            context.Post(_ => NotifyReadout(), null);
        else NotifyReadout();
    }

    public bool IsReadoutBankReady => RequiredReadoutLines is { Count: > 0 } required && MatchingBank.Count == required.Count;

    /// <summary>The reference preparation of the pending module (the first bank entry, else the most recent prepared
    /// line of this module).</summary>
    public ReadoutPreparation? MatchingPreparation => MatchingBank.FirstOrDefault()
        ?? ReadoutPlans.Select(Prepared).LastOrDefault(p => p is not null);

    /// <summary>Back-compatible single preparation (the reference line).</summary>
    public ReadoutPreparation? PreparedReadout => MatchingPreparation;

    public IReadOnlyList<ReadoutLineRow> ReadoutLines
    {
        get
        {
            var present = ReadoutPolicy.LinesInScene(Sources.Select(s => s.ToModel()));
            return ReadoutPlans.Select(plan =>
            {
                var (local, label, detail) = LocalState(plan);
                return new ReadoutLineRow(plan, present.Contains(plan.LineKeV), plan.Reason, local) { LocalLabel = label, LocalDetail = detail };
            }).ToArray();
        }
    }

    /// <summary>The local state as one line (<see cref="ReadoutLineRow.Local"/>) and as the panel's word + detail cells.</summary>
    private (string Local, string Label, string Detail) LocalState(ReadoutLinePlan plan)
    {
        const char nbsp = ' '; // keeps "11.00 s" and "144 / 144 peaks" whole when the column wraps
        if (!plan.CanPrepare) return ("not preparable", "not preparable", "");
        if (Failure(plan) is { } failure)
            return (string.Create(CultureInfo.InvariantCulture, $"Failed: {plan.LineKeV:0.##} keV: {failure}"), "failed", failure);
        if (Prepared(plan) is not { } p) return ("not prepared", "not prepared", "");
        if (!p.Succeeded) return ($"failed: {p.Failure}", "failed", p.Failure ?? "");
        string seconds = p.Cost.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture);
        string peaks = p.Peaks.Count.ToString("N0", CultureInfo.InvariantCulture);
        string crystals = p.Module?.Crystals.ToString("N0", CultureInfo.InvariantCulture) ?? "?";
        return ($"ready · {seconds} s · {peaks} / {crystals} peaks", "ready",
            $"{seconds}{nbsp}s · {peaks}{nbsp}/{nbsp}{crystals}{nbsp}peaks");
    }

    public string ReadoutState
    {
        get
        {
            if (IsPreparingReadout) return ReadoutProgressText ?? ReadoutMessage ?? "Preparing…";
            var required = RequiredReadoutLines;
            if (required.Count == 0)
                return MatchingPreparation is { Succeeded: true } ? "Ready" : "Not prepared";
            var states = required.Select(p => (Plan: p, Prep: Prepared(p))).ToArray();
            if (required.FirstOrDefault(p => Failure(p) is not null) is { } failedPlan)
                return $"Failed: {failedPlan.LineKeV:0.##} keV: {Failure(failedPlan)}";
            if (states.All(s => s.Prep is null)) return "Not prepared";
            string Line(ReadoutLinePlan p) => p.LineKeV.ToString("0.##", CultureInfo.InvariantCulture) + " keV";
            if (states.FirstOrDefault(s => s.Prep is { Succeeded: false }) is { Prep: { } failed } f)
                return $"Failed: {Line(f.Plan)}: {failed.Failure}";
            if (states.All(s => s.Prep is { Succeeded: true }))
                return "Ready · " + string.Join("; ", states.Select(s => string.Create(CultureInfo.InvariantCulture,
                    $"{Line(s.Plan)} · {s.Prep!.Cost.TotalSeconds:0.00} s · {s.Prep.InWindow:N0} windowed / {s.Prep.Scored:N0} scored")));
            return "Not prepared: " + string.Join(", ", states.Where(s => s.Prep is null).Select(s => Line(s.Plan)))
                + " · ready: " + string.Join(", ", states.Where(s => s.Prep is not null).Select(s => Line(s.Plan)));
        }
    }

    /// <summary>The ready bank as a table (line · time · windowed · scored): one row per required line when every one is
    /// prepared, the same numbers as the "Ready · …" form of <see cref="ReadoutState"/>; empty in every other state.</summary>
    public IReadOnlyList<ReadoutReadyRow> ReadoutReadyRows
    {
        get
        {
            if (IsPreparingReadout) return [];
            var required = RequiredReadoutLines;
            if (required.Count == 0 || required.Any(p => Failure(p) is not null)) return [];
            var prepared = required.Select(Prepared).ToArray();
            if (!prepared.All(p => p is { Succeeded: true })) return [];
            var c = CultureInfo.InvariantCulture;
            return required.Zip(prepared, (plan, p) => new ReadoutReadyRow(plan.LineKeV.ToString("0.##", c),
                p!.Cost.TotalSeconds.ToString("0.00", c) + " s", p.InWindow.ToString("N0", c), p.Scored.ToString("N0", c))).ToArray();
        }
    }

    private bool CanPrepareReadout() => _readoutPreparation is not null && IsPhysicalReadout && CanEditInputs;

    /// <summary>Prepares every required line not yet prepared, one at a time on the service's single worker, with real
    /// stage / history progress; cancellation keeps the lines already finished.</summary>
    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanPrepareReadout))]
    private async Task PrepareReadoutAsync(CancellationToken token)
    {
        var required = RequiredReadoutLines;
        if (required.Count == 0)
        {
            ReadoutMessage = "No preparable calibration line is present in the scene: " + string.Join(" ", ReadoutPlans.Select(p => p.Reason));
            SetPreparationStatus("Readout preparation refused: " + ReadoutMessage, warning: true);
            NotifyReadout();
            return;
        }
        // Preparation and acquisition never run together (Start is refused while preparing), so the status bar's one
        // status text and one progress element serve whichever runs: "Preparing <progress>" with the preparation bar.
        IsPreparingReadout = true; ReadoutMessage = null; NotifyRunState();
        SetPreparationStatus("Preparing…", warning: false);
        var optics = Optics; var detector = ModuleDetector; string module = ModuleKey;
        string? activeKey = null;
        try
        {
            foreach (var plan in required)
            {
                string key = ReadoutPolicy.CalibrationKey(module, plan);
                activeKey = key;
                // A retry acknowledges the previous attempt; cancellation must not retain its stale failure.
                _preparationFailures.Remove(key);
                if (_readoutPreparation!.FindCached(key) is { Succeeded: true }) continue;
                var progress = new Progress<ReadoutPrepareProgress>(p =>
                {
                    if (!IsPreparingReadout) return;
                    ReadoutProgress = p.Fraction; ReadoutProgressText = p.ToString();
                    Status = "Preparing " + ReadoutProgressText;
                    OnPropertyChanged(nameof(ReadoutState)); OnPropertyChanged(nameof(ReadoutStateLine));
                    OnPropertyChanged(nameof(ReadoutReadyRows));
                });
                var result = await _readoutPreparation!.PrepareAsync(optics, detector, plan, progress, token);
                if (result.Succeeded) _preparationFailures.Remove(key);
                else RecordPreparationFailure(key, result.Failure ?? "Calibration failed.");
                if (!result.Succeeded) { ReadoutMessage = result.Failure; break; }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { ReadoutMessage = "Preparation cancelled."; }
        catch (Exception ex)
        {
            if (activeKey is not null) RecordPreparationFailure(activeKey, ex.Message);
            ReadoutMessage = $"Preparation failed: {ex.Message}";
        }
        finally
        {
            IsPreparingReadout = false; ReadoutProgress = 0; ReadoutProgressText = null;
            // The outcome stays in the status bar (failures in the warning colour) until the next acquisition or Reset;
            // the Preparation section keeps its own state word and the failure inline (D1).
            if (ReadoutMessage == "Preparation cancelled.") SetPreparationStatus("Readout preparation cancelled", warning: false);
            else if (ReadoutMessage is { } failure) SetPreparationStatus("Readout preparation failed: " + failure, warning: true);
            else SetPreparationStatus("Readout ready: " + string.Join(", ", required.Select(p => p.LineKeV.ToString("0.##", CultureInfo.InvariantCulture) + " keV")), warning: false);
            NotifyRunState(); NotifyReadout();
        }
    }

    private void RecordPreparationFailure(string key, string message)
    {
        _preparationFailures[key] = message;
        if (_preparationFailures.Count > 64) _preparationFailures.Remove(_preparationFailures.Keys.First());
    }

    private double _windowLowKeV = 600, _windowHighKeV = 720;
    public double WindowLowKeV { get => _windowLowKeV; set => SetWindow(value, WindowHighKeV); }
    public double WindowHighKeV { get => _windowHighKeV; set => SetWindow(WindowLowKeV, value); }
    private void SetWindow(double low, double high)
    {
        if (!double.IsFinite(low) || !double.IsFinite(high) || low < 0 || high <= low)
        { ReadoutMessage = "Energy window needs finite 0 ≤ low < high in keV."; RefreshRejectedReadoutProperty(nameof(WindowLowKeV)); RefreshRejectedReadoutProperty(nameof(WindowHighKeV)); return; }
        _windowLowKeV = low; _windowHighKeV = high; ReadoutMessage = null;
        OnPropertyChanged(nameof(WindowLowKeV)); OnPropertyChanged(nameof(WindowHighKeV)); OnPropertyChanged(nameof(WindowLine));
        OnPropertyChanged(nameof(WindowPresets));
        Spectrum.Refresh(); Imaging.RefreshChannels(); DetectorWorkspace.Refresh();
    }

    /// <summary>The single calibration line inside the measured window, or the reason there is none.</summary>
    public string WindowLine => ReadoutPolicy.LineForWindow(WindowLowKeV, WindowHighKeV, out string? reason) is { } line
        ? string.Create(CultureInfo.InvariantCulture, $"Window line {line:0.##} keV") : reason!;

    /// <summary>Sets the window to the benchmark's ±fraction of the line inside the current window (exactly the
    /// arithmetic the imaging-profile comparison uses).</summary>
    [RelayCommand]
    private void UseBenchmarkWindow() => UseWindowPreset(BenchmarkWindowFraction);

    private double BenchmarkWindowFraction => ReadoutOffers.Benchmark?.WindowFraction ?? 0.15;

    /// <summary>The energy-window presets: ±10, ±15 (the EV-36 benchmark, from the record), ±20 and ±25 % around the line
    /// inside the current window. A preset only writes the low / high fields (they stay editable; a typed window is the
    /// custom case). It is not sticky: the active mark follows the values, and choosing a preset again re-centres it on
    /// the line inside the window now.</summary>
    public IReadOnlyList<ReadoutWindowPreset> WindowPresets
    {
        get
        {
            double benchmark = BenchmarkWindowFraction;
            double? line = ReadoutPolicy.LineForWindow(WindowLowKeV, WindowHighKeV, out _);
            bool Active(double f) => line is { } l && ReadoutPolicy.BenchmarkWindow(l, f) == (WindowLowKeV, WindowHighKeV);
            return new[] { (0.10, "Readout.Window10"), (benchmark, "Readout.BenchmarkWindow"), (0.20, "Readout.Window20"), (0.25, "Readout.Window25") }
                .Select(p => new ReadoutWindowPreset(p.Item1, p.Item1 == benchmark, Active(p.Item1), p.Item2)).ToArray();
        }
    }

    public const string WindowPresetHelp = "Each preset sets the window to the calibration line ± that width (the line inside the current window, "
        + "else 661.7 keV), with the benchmark's exact decimal rounding; the fields stay editable and a typed window is the custom case. "
        + "The localisation cost (EV-36) was measured at ±15 % only: ±10, ±20 and ±25 % have no measured cost.";

    /// <summary>Applies one preset width (the same arithmetic as the benchmark window).</summary>
    [RelayCommand]
    private void UseWindowPreset(double fraction)
    {
        double line = ReadoutPolicy.LineForWindow(WindowLowKeV, WindowHighKeV, out _) ?? ReadoutPolicy.ReferenceLineKeV;
        var (low, high) = ReadoutPolicy.BenchmarkWindow(line, fraction);
        SetWindow(low, high);
    }

    /// <summary>Leads from an empty readout view to the readout choice: selects the left Detector tab and asks the view to
    /// focus the readout combo (<see cref="ReadoutModeFocusRequest"/>).</summary>
    [RelayCommand]
    private void ChooseReadout()
    {
        SelectedLeftTab = DetectorTab;
        ReadoutModeFocusRequest++;
    }

    /// <summary>Incremented to ask the view to focus the readout-mode combo (a view-side focus request; no WPF in Core).</summary>
    [ObservableProperty] private int _readoutModeFocusRequest;

    /// <summary>SD-A4: opt-in flood correction of the physical window image from the calibration's own correction
    /// histogram. A view setting (re-projection), never a transport or calibration input.</summary>
    [ObservableProperty] private bool _applyFloodCorrection;
    partial void OnApplyFloodCorrectionChanged(bool value) => Imaging.RefreshChannels();

    private void NotifyReadout()
    {
        foreach (string name in new[] { nameof(IsPhysicalReadout), nameof(CanUseLegacy), nameof(CanEditLegacy), nameof(ReadoutAssumptions), nameof(ReadoutModeSummary),
            nameof(ReadoutState), nameof(ReadoutStateLine), nameof(ReadoutReadyRows), nameof(Detector), nameof(ReadoutLines), nameof(ReadoutPlans), nameof(RequiredReadoutLines),
            nameof(MatchingBank), nameof(MatchingPreparation), nameof(PreparedReadout), nameof(IsReadoutBankReady), nameof(ReadoutOffers), nameof(WindowPresets) })
            OnPropertyChanged(name);
        Waveform.SyncReadoutMode();
        if (IsPhysicalReadout) { Spectrum.PileUp = false; Imaging.Strip = false; Waveform.RateStudy = false; Waveform.Ideal = false; }
        PrepareReadoutCommand.NotifyCanExecuteChanged();
        DetectorWorkspace.Refresh();
        Spectrum.NotifyReadoutChanged();
        OnPropertyChanged(nameof(ReadoutLocalisationHelp));
        Imaging.NotifyPhysicalChannel();
    }
}
