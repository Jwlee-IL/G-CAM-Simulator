using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Gcam.Studio.Core.Detector;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>Static response pattern: pending detector and optics in Empty, the acquired (locked) ones once data exist.
/// On the physical readout it shows, per calibration line, the confirmed offer or refusal (shipped record) separately
/// from this session's local preparation, and draws the selected line's captured module, LUT and circuit.</summary>
public sealed partial class DetectorWorkspaceViewModel(MainViewModel shared, IDetectorFaceService? service)
    : WorkspaceViewModel("Detector", "Workspace.Detector")
{
    private Gcam.Configuration.OpticsSettings? _faceOptics;
    private DetectorSettings? _faceDetector;
    public MainViewModel Shared { get; } = shared;
    [ObservableProperty] private DetectorFace? _face;
    [ObservableProperty] private ReadoutDetectorTab _readoutTab;
    /// <summary>The calibration line whose preparation the map, facts and diagnostics show (null = the reference line).</summary>
    [ObservableProperty] private double? _selectedLineKeV;
    partial void OnSelectedLineKeVChanged(double? value) => Refresh();

    // View switch (Face · SiPM · Anger · Calibration · Diagnostics): always shown. The Face view is the detector face of
    // either readout; the readout views show the physical readout, or an empty state with "Choose readout…" when the
    // direct crystal assignment is active. A readout-mode change selects that mode's first view.
    private bool _wasPhysical;
    public int ReadoutTabIndex
    {
        get => (int)ReadoutTab;
        set { if (Enum.IsDefined((ReadoutDetectorTab)value)) ReadoutTab = (ReadoutDetectorTab)value; }
    }
    public bool IsFaceView => ReadoutTab == ReadoutDetectorTab.Face;
    public bool ShowReadoutView => !IsFaceView && Shared.IsPhysicalReadout;
    public bool ShowReadoutEmpty => !IsFaceView && !Shared.IsPhysicalReadout;
    /// <summary>The centre heading follows the view and the mode: the face; the physical head; or the mode-neutral
    /// "Detector" over the empty state of a readout view under the direct readout.</summary>
    public string CentreTitle => IsFaceView ? "Detector face · gain pattern" : Shared.IsPhysicalReadout ? "Experimental four-output head" : "Detector";
    partial void OnReadoutTabChanged(ReadoutDetectorTab value)
    {
        foreach (string name in new[] { nameof(ReadoutTabIndex), nameof(IsFaceView), nameof(ShowReadoutView), nameof(ShowReadoutEmpty), nameof(CentreTitle) })
            OnPropertyChanged(name);
    }

    /// <summary>The local preparation (<see cref="CalibrationState"/>) as label / value rows; empty without a preparation,
    /// where the panel shows the readout state line.</summary>
    public IReadOnlyList<FactRow> CalibrationRows
    {
        get
        {
            if (Preparation is not { } p) return [];
            var c = CultureInfo.InvariantCulture;
            var rows = new List<FactRow>
            {
                new("State", p.Succeeded ? "Ready" : "Failed: " + p.Failure), new("Line", $"{Kev(p.LineKeV)} keV"),
                new("Peaks", $"{p.Peaks.Count} / {Crystals(p)}"),
            };
            if (p.Plan is { } plan)
            {
                rows.Add(new("Method", plan.MethodLabel)); rows.Add(new("Solve", plan.SolveLabel));
                rows.Add(new("Histories / crystal", plan.Budget.ToString("N0", c)));
            }
            rows.Add(new("Seeds", p.Seeds));
            rows.Add(new("Scored", p.Scored.ToString("N0", c))); rows.Add(new("Triggered", p.Triggered.ToString("N0", c)));
            rows.Add(new("Windowed", p.InWindow.ToString("N0", c)));
            rows.Add(new("Gain fallback", $"{p.GainFallback} / {Crystals(p)} crystals"));
            rows.Add(new("Cost", p.Cost.TotalSeconds.ToString("0.00", c) + " s"));
            rows.Add(new("LUT bins", $"{p.Density.Width} × {p.Density.Height}"));
            if (p.Module is { } m)
            {
                rows.Add(new("Sensors", string.Create(c, $"{m.SensorsX} × {m.SensorsY} virtual matched SiPMs")));
                rows.Add(new("Sensor pitch", string.Create(c, $"{m.SensorPitchMm:0.###} mm, active {m.SensorActiveWidthMm:0.###} mm")));
                rows.Add(new("Microcells", "Unlimited"));
            }
            rows.Add(new("Flood correction", p.CorrectionHistogram is { } h
                ? string.Create(c, $"{h.Sum():N0} photopeak events; {h.Count(v => v <= 0)} empty bins") : "Unavailable"));
            rows.Add(new("Calibration id", p.Id.ToString()));
            return rows;
        }
    }

    /// <summary>The local per-crystal check as table rows (crystal · assigned · wrong); lines that are not crystal checks
    /// (hand-built views) stay as notes across the table.</summary>
    public IReadOnlyList<ReadoutDiagnosticRow> DiagnosticRows => Preparation is not { } p ? []
        : p.CrystalChecks.Count > 0
            ? p.CrystalChecks.Select(k => new ReadoutDiagnosticRow(k.Crystal.ToString(CultureInfo.InvariantCulture),
                $"{k.Assigned} / {k.Total}", $"{k.Wrong} / {k.Assigned}")).ToArray()
            : p.Diagnostics.Select(d => new ReadoutDiagnosticRow("", "", "", d)).ToArray();

    /// <summary>What the per-crystal check covers (line, energies, validation seeds), shown above the table.</summary>
    public string DiagnosticScope => Preparation is { CheckScope: { Length: > 0 } scope } ? scope : "";

    private static string Kev(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Successful line preparations in view: the acquisition's captured bank once data exist, else every line of
    /// the pending module prepared in this session.</summary>
    public IReadOnlyList<ReadoutPreparation> Bank => Shared.Snapshot?.Readout is { } r
        ? (r.Lines.Count > 0 ? r.Lines.Select(l => l.Preparation).ToArray() : [r.Preparation])
        : Shared.MatchingBank.Count > 0 ? Shared.MatchingBank
        : Shared.MatchingPreparation is { } p ? [p] : [];

    public IReadOnlyList<double> BankLines => Bank.Select(p => p.LineKeV).ToArray();

    /// <summary>The line shown by the selector: the explicit selection, else the reference preparation's line.</summary>
    public double? ShownLineKeV
    {
        get => Preparation?.LineKeV;
        set { if (value is { } line && BankLines.Contains(line)) SelectedLineKeV = line; }
    }

    public ReadoutPreparation? Preparation => (SelectedLineKeV is { } line ? Bank.FirstOrDefault(p => p.LineKeV == line) : null)
        ?? Shared.Snapshot?.Readout?.Preparation ?? Shared.MatchingPreparation;
    public ReadoutSnapshot? PhysicalSnapshot => Shared.Snapshot?.Readout;
    public IReadOnlyList<string> Diagnostics => Preparation?.Diagnostics ?? [];

    /// <summary>Help text of the Diagnostics tab: what the per-crystal rows are, and what they are not.</summary>
    public string DiagnosticsHelp => DiagnosticsHelpText;
    private const string DiagnosticsHelpText = "Local check of this preparation: an independent flood of 200 histories per crystal at the line energy, all deposited energies, single-crystal histories only; per crystal, the share assigned by the LUT and the share assigned to a wrong crystal. It is not the confirmed 32-seed record (see the Calibration tab's offer help) and never gates an offer.";

    /// <summary>Help text of the map: what the SiPM and Anger views draw, and how to zoom and inspect.</summary>
    public string MapHelp => MapHelpText;
    private const string MapHelpText = "SiPM: the resolved DPC resistor edges of the captured module, row 1000 Ω / column 10 Ω, virtual-ground outputs A–D; sensor squares at the actual sensor pitch and active width (virtual matched sensors, illustrative circuit). Anger: raw X/Y from −1 to 1, density in viridis from a cached raster, LUT boundaries and peak marks; unknown conversions are retained. Wheel or + / − zooms, drag or arrow keys pan, 0 resets; hover to inspect a LUT crystal.";

    /// <summary>The confirmed offer / refusal rows of the pending module, kept apart from local preparation state.</summary>
    public IReadOnlyList<ReadoutLineRow> OfferRows => Shared.IsPhysicalReadout ? Shared.ReadoutLines : [];
    public Gcam.Core.DetectorImage? SensitivitySummary => SensitivityCell?.SensitivitySummary;
    public double SensitivityStepMm => Shared.ReadoutOffers.Geometries.FirstOrDefault(g => g.Id == SensitivityCell?.Geometry)?.PitchMm ?? 1;
    public double SensitivityOriginMm => -((SensitivitySummary?.Width ?? 1) - 1) * SensitivityStepMm / 2;
    private ReadoutOfferCell? SensitivityCell => Shared.ReadoutPlans.FirstOrDefault(p => p.LineKeV ==
        (ShownLineKeV ?? ReadoutPolicy.LineForWindow(Shared.WindowLowKeV, Shared.WindowHighKeV, out _) ?? ReadoutPolicy.ReferenceLineKeV))?.Cell;
    public string SensitivityCaption => SensitivityCell is { SensitivitySummary: not null } cell
        ? $"{Kev(cell.EnergyKeV)} keV · mean sensitivity map (32 seeds) · weakest / median {cell.Diagnostics.First(d => d.RateCps == 0).WeakestMedianSensitivity!.Estimate:0.###} (recorded seed statistic)" : "Sensitivity summary unavailable for this line";
    public string SensitivityHelp => SensitivityCell?.SensitivitySummaryMeaning ?? "No offered-cell sensitivity summary.";

    /// <summary>Literal published failures and the confirmed 32-seed crystal diagnostics of each line (record values,
    /// distinct from the local 200-history check in Diagnostics).</summary>
    public string OfferDetails
    {
        get
        {
            if (!Shared.IsPhysicalReadout) return "";
            var record = Shared.ReadoutOffers;
            if (!record.IsAvailable) return $"Readout offer record unavailable: {record.UnavailableReason}";
            var lines = new List<string> { $"Shipped record SHA-256 {record.Sha256}" };
            foreach (var plan in Shared.ReadoutPlans)
            {
                if (plan.Cell is not { } cell) { lines.Add($"{Kev(plan.LineKeV)} keV: {plan.Reason}"); continue; }
                lines.Add($"{cell.Geometry} {Kev(cell.EnergyKeV)} keV · {cell.Label} · {cell.CalibrationSeeds} seeds · budget {cell.Budget.ToString("N0", CultureInfo.InvariantCulture)} histories per crystal · {cell.SourceRecord} ({cell.SourcePhase})");
                foreach (var f in cell.Failures) lines.Add($"  seed {f.Seed}: {f.Failure}");
                if (cell.Superseded is { } old)
                    lines.Add($"  superseded: {old.SourceRecord} {old.Label}, {old.Failures.Count} / {old.CalibrationSeeds} failures at {old.Budget.ToString("N0", CultureInfo.InvariantCulture)} histories per crystal"
                        + string.Concat(old.Failures.Select(f => $"\n    seed {f.Seed}: {f.Failure}")));
                foreach (var d in cell.Diagnostics.Where(d => d.Available))
                    lines.Add(string.Create(CultureInfo.InvariantCulture,
                        $"  confirmed diagnostics ({(d.RateCps == 0 ? "isolated" : $"{d.RateCps:N0} input hits/s")}, 32 seeds): single-crystal mis-ID {d.SingleMisId!.Estimate:P2}; multi-crystal share {d.MultiCrystalShare!.Estimate:0.###}; weakest / median sensitivity {d.WeakestMedianSensitivity!.Estimate:0.###}; G x / y {d.GXRad2!.Estimate:0.###E+0} / {d.GYRad2!.Estimate:0.###E+0} rad²"));
            }
            lines.Add("Crystal diagnostics are descriptive; only complete calibration success gates an offer.");
            return string.Join("\n", lines);
        }
    }

    public string CalibrationState => Preparation is { } p
        ? string.Join("\n", new[]
        {
            $"{(p.Succeeded ? "Ready" : "Failed: " + p.Failure)} · {Kev(p.LineKeV)} keV · {p.Peaks.Count} / {Crystals(p)} peaks",
            $"Calibration {p.Id}" + (p.Plan is { } plan ? $" · {plan.MethodLabel}, {plan.SolveLabel}, {plan.Budget.ToString("N0", CultureInfo.InvariantCulture)} histories per crystal" : ""),
            $"Seeds: {p.Seeds}",
            $"Scored {p.Scored:N0} · triggered {p.Triggered:N0} · windowed {p.InWindow:N0}",
            $"Global energy gain fallback {p.GainFallback} / {Crystals(p)} crystals",
            $"Cost {p.Cost.TotalSeconds:0.00} s; {p.Density.Width} × {p.Density.Height} LUT bins",
            p.Module is { } m
                ? string.Create(CultureInfo.InvariantCulture, $"Module {m.CrystalsX} × {m.CrystalsY} crystals · pitch {m.PitchMm:0.###} mm · gap {m.GapMm * 1000:0.###} µm · {m.SensorsX} × {m.SensorsY} virtual matched SiPMs at {m.SensorPitchMm:0.###} mm pitch, active {m.SensorActiveWidthMm:0.###} mm, unlimited microcells")
                : "Module geometry not captured",
            p.CorrectionHistogram is { } h
                ? $"Flood-correction histogram: {h.Sum():N0} photopeak-assigned flood events; {h.Count(v => v <= 0)} empty bins (opt-in in Imaging)"
                : "Flood-correction histogram unavailable",
        })
        : Shared.ReadoutState;

    private static string Crystals(ReadoutPreparation p) => p.Module?.Crystals.ToString(CultureInfo.InvariantCulture) ?? "?";

    public string PhysicalCounts => PhysicalSnapshot is { } p
        ? $"Assigned {Shared.Snapshot!.Counts:N0} · unknown {p.Unknown:N0} · triggered {p.Records.Count:N0} · realised hits {p.Hits.Count:N0} · diagnostic truth {p.TruthSamples.Count} / 256 first hits{(p.PendingHold ? " · hold pending observed horizon" : "")} · reference {Kev(p.Preparation.LineKeV)} keV calibration"
          + string.Concat(p.Lines.Skip(1).Select(l => $"\n{Kev(l.LineKeV)} keV calibration: assigned {l.Assigned:N0} · unknown {l.Unknown:N0} (same held records; not additional counts)"))
        : "No measured conversions";
    /// <summary>The facts of <see cref="PhysicalCounts"/> as label / value rows (the panel's Measured conversions
    /// section); empty before measured conversions exist, where the panel shows <see cref="PhysicalCounts"/>.</summary>
    public IReadOnlyList<FactRow> ConversionRows
    {
        get
        {
            if (PhysicalSnapshot is not { } p) return [];
            var c = CultureInfo.InvariantCulture;
            var rows = new List<FactRow>
            {
                new("Assigned", Shared.Snapshot!.Counts.ToString("N0", c)), new("Unknown", p.Unknown.ToString("N0", c)),
                new("Triggered", p.Records.Count.ToString("N0", c)), new("Realised hits", p.Hits.Count.ToString("N0", c)),
                new("Diagnostic truth", string.Create(c, $"{p.TruthSamples.Count} / 256 first hits")),
            };
            if (p.PendingHold) rows.Add(new("Hold", "Pending observed horizon"));
            rows.Add(new("Reference", $"{Kev(p.Preparation.LineKeV)} keV calibration"));
            foreach (var l in p.Lines.Skip(1))
            {
                rows.Add(new($"{Kev(l.LineKeV)} keV assigned", l.Assigned.ToString("N0", c)));
                rows.Add(new($"{Kev(l.LineKeV)} keV unknown", l.Unknown.ToString("N0", c)));
            }
            return rows;
        }
    }

    /// <summary>Shown under the rows when other line calibrations project the same held records.</summary>
    public string? ConversionNote => PhysicalSnapshot is { Lines.Count: > 1 }
        ? "Other line calibrations project the same held records (not additional counts)." : null;

    public double MinimumGain => Face?.Crystals.Min(c => c.Gain) ?? 0;
    public double MaximumGain => Face?.Crystals.Max(c => c.Gain) ?? 0;
    public string GainLegend => Face is { } f ? $"Relative gain {f.Crystals.Min(c => c.Gain):0.000}–{f.Crystals.Max(c => c.Gain):0.000} · face {f.SizeMm:0.###} mm square" : "Enter valid detector settings";
    /// <summary>Whose settings the face shows: the next acquisition's (Empty) or the acquired, locked ones.</summary>
    public string Identity => Shared.Snapshot is null ? "Settings for the next acquisition" : "Acquired settings · locked until Reset";
    public string Readout
    {
        get
        {
            var detector = Shared.Snapshot?.Detector ?? Shared.Detector;
            var optics = Shared.Snapshot?.Optics ?? Shared.Optics;
            if (Shared.IsPhysicalReadout)
            {
                // The captured module once prepared or acquired; the pending optics before that (labelled).
                string module = Preparation?.Module is { } m
                    ? string.Create(CultureInfo.InvariantCulture, $"{m.CrystalsX} × {m.CrystalsY} matched virtual SiPMs (captured)\nPitch {m.PitchMm:0.###} mm · gap {m.GapMm * 1000:0.###} µm")
                    : string.Create(CultureInfo.InvariantCulture, $"{optics.DetectorPixels} × {optics.DetectorPixels} matched virtual SiPMs (pending)\nPitch {optics.PixelPitchMm:0.###} mm · gap {detector.ReflectorGapMm * 1000:0.###} µm");
                return $"Experimental GAGG · {module}\nDPC row 1000 Ω / column 10 Ω; A–D virtual grounds\nSum trigger 50 keV-equivalent; common hold; specular 0.98\n{PhysicalCounts}";
            }
            return $"{detector.Chain.Scintillator.Name}\n{optics.DetectorPixels} × {optics.DetectorPixels} crystals · pitch {optics.PixelPitchMm:0.###} mm\n"
                + $"Gap {detector.ReflectorGapMm * 1000:0.###} µm · active width {optics.PixelPitchMm - detector.ReflectorGapMm:0.###} mm\n"
                + $"Gain σ {detector.GainSigma * 100:0.#}% · seed {detector.GainSeed}\nGeometric active-area fraction: {Face?.ActiveAreaFraction:P2}";
        }
    }
    // The right panel shows the facts of Readout as label / value rows in two sections (geometry; gain or readout network);
    // Readout stays the same facts as one text (the rows container's accessible name).

    /// <summary>Geometry section rows: material or readout, module / crystals, pitch, gap, active width.</summary>
    public IReadOnlyList<FactRow> GeometryRows
    {
        get
        {
            var detector = Shared.Snapshot?.Detector ?? Shared.Detector;
            var optics = Shared.Snapshot?.Optics ?? Shared.Optics;
            var c = CultureInfo.InvariantCulture;
            if (Shared.IsPhysicalReadout)
                return Preparation?.Module is { } m
                    ? [new("Readout", "Experimental GAGG"), new("Module", string.Create(c, $"{m.CrystalsX} × {m.CrystalsY} (captured)")),
                        new("SiPMs", "Matched, virtual"),
                        new("Pitch", string.Create(c, $"{m.PitchMm:0.###} mm")), new("Reflector gap", string.Create(c, $"{m.GapMm * 1000:0.###} µm"))]
                    : [new("Readout", "Experimental GAGG"), new("Module", string.Create(c, $"{optics.DetectorPixels} × {optics.DetectorPixels} (pending)")),
                        new("SiPMs", "Matched, virtual"),
                        new("Pitch", string.Create(c, $"{optics.PixelPitchMm:0.###} mm")), new("Reflector gap", string.Create(c, $"{detector.ReflectorGapMm * 1000:0.###} µm"))];
            return [new("Scintillator", detector.Chain.Scintillator.Name), new("Crystals", $"{optics.DetectorPixels} × {optics.DetectorPixels}"),
                new("Pitch", $"{optics.PixelPitchMm:0.###} mm"), new("Reflector gap", $"{detector.ReflectorGapMm * 1000:0.###} µm"),
                new("Active width", $"{optics.PixelPitchMm - detector.ReflectorGapMm:0.###} mm")];
        }
    }

    /// <summary>The geometric active-area fraction (the row that carries the area help mark).</summary>
    public string ActiveAreaText => Face is { } f ? $"{f.ActiveAreaFraction:P2}" : "—";

    /// <summary>Title of the second section: the legacy gain pattern, or the physical readout network.</summary>
    public string ResponseTitle => Shared.IsPhysicalReadout ? "Readout network" : "Gain pattern";

    /// <summary>Second section rows (same facts as <see cref="Readout"/>).</summary>
    public IReadOnlyList<FactRow> ResponseRows
    {
        get
        {
            var detector = Shared.Snapshot?.Detector ?? Shared.Detector;
            return Shared.IsPhysicalReadout
                ? [new("Network", "DPC"), new("Row / column", "1000 Ω / 10 Ω"), new("Outputs", "A–D virtual grounds"),
                    new("Trigger", "Sum, 50 keV-eq."), new("Hold", "Common"), new("Reflector", "Specular 0.98")]
                : [new("Gain σ", $"{detector.GainSigma * 100:0.#}%"), new("Gain seed", $"{detector.GainSeed}"), .. GainMapRows];
        }
    }

    /// <summary>The face legend (<see cref="GainLegend"/>) as rows: the relative-gain range and the face size, or the
    /// settings message when no face can be built.</summary>
    public IReadOnlyList<FactRow> GainMapRows => Face is { } f
        ? [new("Relative gain", $"{f.Crystals.Min(c => c.Gain):0.000}–{f.Crystals.Max(c => c.Gain):0.000}"), new("Face", $"{f.SizeMm:0.###} mm square")]
        : [new("Gain map", "Enter valid detector settings")];

    /// <summary>The acquired counts and rate (<see cref="Counts"/>) as rows; empty before an acquisition, where the panel
    /// shows <see cref="Counts"/>' message.</summary>
    public IReadOnlyList<FactRow> CountRows => Shared.Snapshot is { } s
        ? [new("Counts", $"{s.Counts:N0}"), new("Live-time rate", $"{s.ObservedRateCps:0.0} cps")] // rate = counts / live time
        : [];

    public string Counts => Shared.Snapshot is { } s
        ? $"Acquired {s.Counts:N0} counts · {s.ObservedRateCps:0.0} cps (counts / live time)"
        : "No acquired counts";
    internal void Refresh()
    {
        if (Shared.IsPhysicalReadout != _wasPhysical)
        {
            _wasPhysical = Shared.IsPhysicalReadout;
            ReadoutTab = _wasPhysical ? ReadoutDetectorTab.Sipm : ReadoutDetectorTab.Face;
        }
        foreach (string name in new[] { nameof(ShowReadoutView), nameof(ShowReadoutEmpty), nameof(CentreTitle) }) OnPropertyChanged(name);
        var optics = Shared.Snapshot?.Optics ?? Shared.Optics;
        var detector = Shared.Snapshot?.Detector ?? Shared.Detector;
        if (Shared.Snapshot is not null || Shared.GapError is null)
        {
            if (Face is null || optics != _faceOptics || detector != _faceDetector)
            {
                Face = service?.Build(optics, Shared.IsPhysicalReadout ? detector with { GainSigma = 0 } : detector);
                _faceOptics = optics; _faceDetector = detector;
            }
        }
        else { Face = null; _faceOptics = null; _faceDetector = null; }
        foreach (string name in new[] { nameof(Identity), nameof(Readout), nameof(Counts), nameof(GainLegend), nameof(MinimumGain), nameof(MaximumGain),
            nameof(GeometryRows), nameof(ActiveAreaText), nameof(ResponseTitle), nameof(ResponseRows), nameof(GainMapRows), nameof(CountRows) }) OnPropertyChanged(name);
        foreach (string name in new[] { nameof(Bank), nameof(BankLines), nameof(ShownLineKeV), nameof(Preparation), nameof(PhysicalSnapshot), nameof(Diagnostics), nameof(CalibrationState),
            nameof(PhysicalCounts), nameof(ConversionRows), nameof(ConversionNote), nameof(CalibrationRows), nameof(DiagnosticRows), nameof(DiagnosticScope), nameof(OfferRows), nameof(OfferDetails), nameof(SensitivitySummary), nameof(SensitivityStepMm), nameof(SensitivityOriginMm), nameof(SensitivityCaption), nameof(SensitivityHelp) }) OnPropertyChanged(name);
    }
}
