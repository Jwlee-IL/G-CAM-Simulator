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
            if (SetProperty(ref _readoutMode, value)) { ReadoutMessage = null; NotifyReadout(); }
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
    public string ReadoutAssumptions => IsPhysicalReadout
        ? "Experimental GAGG · DPC 0.01 · sum trigger 50 keV-equivalent · specular 0.98 · virtual matched SiPMs · unlimited cells; no RC, skew or jitter. Legacy gain and chain are inactive. Ambient and BSR must be zero: their interaction records are unavailable. Each calibration line present in the scene is prepared separately."
        : "Direct crystal assignment; legacy detector gain and chain response.";

    /// <summary>The shipped offer / cost record (unavailable without a preparation service or when malformed).</summary>
    public ReadoutOfferRecord ReadoutOffers => _readoutPreparation?.Offers
        ?? ReadoutOfferRecord.Unavailable("No readout preparation service is available.");

    [ObservableProperty] private string? _readoutMessage;
    [ObservableProperty] private bool _isPreparingReadout;
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
            return ReadoutPlans.Select(plan => new ReadoutLineRow(plan, present.Contains(plan.LineKeV), plan.Reason, LocalState(plan))).ToArray();
        }
    }

    private string LocalState(ReadoutLinePlan plan) => !plan.CanPrepare ? "not preparable"
        : Failure(plan) is { } failure ? string.Create(CultureInfo.InvariantCulture, $"Failed: {plan.LineKeV:0.##} keV: {failure}")
        : Prepared(plan) is not { } p ? "not prepared"
        : p.Succeeded ? string.Create(CultureInfo.InvariantCulture, $"ready · {p.Cost.TotalSeconds:0.00} s · {p.Peaks.Count:N0} / {p.Module?.Crystals.ToString("N0", CultureInfo.InvariantCulture) ?? "?"} peaks")
        : $"failed: {p.Failure}";

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
            NotifyReadout();
            return;
        }
        IsPreparingReadout = true; ReadoutMessage = null; NotifyRunState();
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
                    OnPropertyChanged(nameof(ReadoutState));
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
        Spectrum.Refresh(); Imaging.RefreshChannels(); DetectorWorkspace.Refresh();
    }

    /// <summary>The single calibration line inside the measured window, or the reason there is none.</summary>
    public string WindowLine => ReadoutPolicy.LineForWindow(WindowLowKeV, WindowHighKeV, out string? reason) is { } line
        ? string.Create(CultureInfo.InvariantCulture, $"Window line {line:0.##} keV") : reason!;

    /// <summary>Sets the window to the benchmark's ±fraction of the line inside the current window (exactly the
    /// arithmetic the imaging-profile comparison uses).</summary>
    [RelayCommand]
    private void UseBenchmarkWindow()
    {
        double fraction = ReadoutOffers.Benchmark?.WindowFraction ?? 0.15;
        double line = ReadoutPolicy.LineForWindow(WindowLowKeV, WindowHighKeV, out _) ?? ReadoutPolicy.ReferenceLineKeV;
        var (low, high) = ReadoutPolicy.BenchmarkWindow(line, fraction);
        SetWindow(low, high);
    }

    /// <summary>SD-A4: opt-in flood correction of the physical window image from the calibration's own correction
    /// histogram. A view setting (re-projection), never a transport or calibration input.</summary>
    [ObservableProperty] private bool _applyFloodCorrection;
    partial void OnApplyFloodCorrectionChanged(bool value) => Imaging.RefreshChannels();

    private void NotifyReadout()
    {
        foreach (string name in new[] { nameof(IsPhysicalReadout), nameof(CanUseLegacy), nameof(CanEditLegacy), nameof(ReadoutAssumptions), nameof(ReadoutModeSummary),
            nameof(ReadoutState), nameof(Detector), nameof(ReadoutLines), nameof(ReadoutPlans), nameof(RequiredReadoutLines),
            nameof(MatchingBank), nameof(MatchingPreparation), nameof(PreparedReadout), nameof(IsReadoutBankReady), nameof(ReadoutOffers) })
            OnPropertyChanged(name);
        if (IsPhysicalReadout) { Spectrum.PileUp = false; Imaging.Strip = false; Waveform.RateStudy = false; Waveform.Ideal = false; }
        PrepareReadoutCommand.NotifyCanExecuteChanged();
        DetectorWorkspace.Refresh();
        Imaging.NotifyReadoutCost();
    }
}
