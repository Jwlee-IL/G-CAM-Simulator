using System.Globalization;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>The measured imaging cost of the physical readout (TODO-41, SD-A9, A11, A12, A16): a compact reference card and
/// the full shipped table for the window's calibration line, with the declared benchmark scope. Descriptive only — it
/// never gates preparation or acquisition.</summary>
public sealed partial class ImagingWorkspaceViewModel
{
    /// <summary>The exact wording the records require beside every F value.</summary>
    public const string FWording = "F is an equal-fluence MSE ratio, not a time factor.";

    private static string Num(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    private static string Interval(ReadoutInterval? i, double scale = 1, string unit = "")
        => i is null ? "unavailable" : $"{Num(i.Estimate * scale)}{unit} [{Num(i.Lower95 * scale)}, {Num(i.Upper95 * scale)}]";
    private static string Rate(double cps) => cps == 0 ? "isolated" : cps.ToString("N0", CultureInfo.InvariantCulture) + " input hits/s";

    private OpticsSettings CostOptics => (Shared.Snapshot?.Optics ?? Shared.Optics) with { FocalDistanceMm = FocalDistanceMm };
    private DetectorSettings CostDetector => Shared.Snapshot?.Detector ?? Shared.Detector;

    /// <summary>The (preset, line) cell of the window's calibration line for the module in view, or null.</summary>
    public ReadoutOfferCell? ReadoutCostCell => ReadoutCostPlan?.Cell;

    private ReadoutLinePlan? ReadoutCostPlan => Shared.IsPhysicalReadout
        && ReadoutPolicy.LineForWindow(Shared.WindowLowKeV, Shared.WindowHighKeV, out _) is { } line
            ? ReadoutPolicy.Plan(CostOptics, CostDetector, line, Shared.ReadoutOffers) : null;

    /// <summary>Whether the full imaging profile matches the confirmed geometry's benchmark (the only case in which the
    /// reference table is inherited for this configuration).</summary>
    public bool ReadoutCostInherited => ReadoutCostPlan is { Cell: not null, GeometryId: { } id } plan
        && Shared.ReadoutOffers.Geometries.FirstOrDefault(g => g.Id == id) is { } geometry
        && Shared.ReadoutOffers.Benchmark is { } benchmark
        && ReadoutPolicy.MatchesImagingProfile(CostOptics, geometry, benchmark, Reconstruction, plan.LineKeV,
            Shared.WindowLowKeV, Shared.WindowHighKeV, Shared.ApplyFloodCorrection);

    /// <summary>Short status above the card (inline): inherited, unmeasured, not offered, local or unavailable. The reasons
    /// are in <see cref="ReadoutCostStatusDetails"/> (help).</summary>
    public string ReadoutCostStatus
    {
        get
        {
            if (!Shared.IsPhysicalReadout) return "";
            if (ReadoutPolicy.LineForWindow(Shared.WindowLowKeV, Shared.WindowHighKeV, out _) is null) return "No reference cost row applies";
            var plan = ReadoutCostPlan!;
            return plan.Status switch
            {
                ReadoutLineStatus.Unavailable => "Imaging cost unavailable",
                ReadoutLineStatus.Local => "Imaging cost unmeasured for this configuration (local, experimental, unvalidated)",
                ReadoutLineStatus.Refused => "Not offered (refused)",
                _ when ReadoutCostInherited => $"Reference benchmark inherited ({plan.GeometryId})",
                _ => "Imaging cost unmeasured for this configuration",
            };
        }
    }

    /// <summary>The literal reasons behind <see cref="ReadoutCostStatus"/> (help text).</summary>
    public string ReadoutCostStatusDetails
    {
        get
        {
            if (!Shared.IsPhysicalReadout) return "";
            if (ReadoutPolicy.LineForWindow(Shared.WindowLowKeV, Shared.WindowHighKeV, out string? reason) is null) return reason!;
            var plan = ReadoutCostPlan!;
            return plan.Status switch
            {
                ReadoutLineStatus.Unavailable => plan.Reason,
                ReadoutLineStatus.Local => "This module matches no confirmed preset geometry; it is calibrated locally, and no imaging cost was measured for it.",
                ReadoutLineStatus.Refused => $"{plan.Reason} Direct-reference rows are shown for context.",
                _ when ReadoutCostInherited => $"Full imaging profile matches {plan.GeometryId} (mask, distance, focus, decoder, window, flood correction). A benchmark, not a prediction for this scene.",
                _ => $"Unmeasured because: {ProfileDifferences(plan)}. The {plan.GeometryId} reference table is shown for reference only (benchmark not applicable).",
            };
        }
    }

    private string ProfileDifferences(ReadoutLinePlan plan)
    {
        var record = Shared.ReadoutOffers;
        if (record.Geometries.FirstOrDefault(g => g.Id == plan.GeometryId) is not { } g || record.Benchmark is not { } b) return "no benchmark";
        var optics = CostOptics;
        var differences = new List<string>();
        if (optics.MuraRank != g.Rank || optics.CellPitchMm != g.CellMm) differences.Add("mask differs");
        if (optics.MaskDetectorDistanceMm != g.DistanceMm) differences.Add("mask-detector distance differs");
        if (optics.FocalDistanceMm != b.FocalDistanceMm) differences.Add(string.Create(CultureInfo.InvariantCulture, $"focus is not {b.FocalDistanceMm:0} mm"));
        if (Reconstruction != DecoderMethod.CrossCorrelation) differences.Add("MLEM selected (benchmark: finite-mask cross-correlation)");
        if (!ReadoutPolicy.IsBenchmarkWindow(Shared.WindowLowKeV, Shared.WindowHighKeV, plan.LineKeV, b.WindowFraction)) differences.Add(string.Create(CultureInfo.InvariantCulture, $"window is not ±{b.WindowFraction:P0} of {plan.LineKeV:0.##} keV"));
        if (!Shared.ApplyFloodCorrection) differences.Add("flood correction off (benchmark: per-path correction)");
        return differences.Count == 0 ? "no difference found" : string.Join("; ", differences);
    }

    /// <summary>The full table of the cell (1 000 / 4 000 expected ideal-Direct counts × isolated / 100 000 input hits/s).</summary>
    public IReadOnlyList<ReadoutCostRowView> ReadoutCostRows => ReadoutCostCell is { } cell
        ? cell.CostRows.Select(r => new ReadoutCostRowView(
            r.TargetDirectCounts.ToString("N0", CultureInfo.InvariantCulture) + " ideal-Direct counts", Rate(r.RateCps),
            Interval(r.Direct.RmsDeg, unit: "°"), Interval(r.Direct.BiasDeg, unit: "°"), Interval(r.Direct.RemoteProbability, 100, " %"),
            Interval(r.Direct.AcceptedCounts),
            Interval(r.Anger.RmsDeg, unit: "°"), Interval(r.Anger.BiasDeg, unit: "°"), Interval(r.Anger.RemoteProbability, 100, " %"),
            Interval(r.Anger.AcceptedCounts), r.F is null ? "unavailable" : Interval(r.F))).ToArray()
        : [];

    /// <summary>Compact card (inline): the cell and its label, the isolated rows' key numbers (RMS, F) at both exposures and
    /// the F sentence. Provenance, F definition, benchmark conditions and the F-below-1 explanation are in
    /// <see cref="ReadoutCostHelp"/>.</summary>
    public string ReadoutCostCard
    {
        get
        {
            if (ReadoutCostCell is not { } cell) return "";
            var lines = new List<string> { string.Create(CultureInfo.InvariantCulture, $"{cell.Geometry} {cell.EnergyKeV:0.##} keV · {cell.Label}") };
            foreach (var r in cell.CostRows.Where(r => r.RateCps == 0))
                lines.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{r.TargetDirectCounts:N0} counts: Direct RMS {Short(r.Direct.RmsDeg)}° · Anger RMS {Short(r.Anger.RmsDeg)}° · F {(r.F is null ? "unavailable" : Interval(r.F))}"));
            lines.Add(FWording);
            return string.Join("\n", lines);
        }
    }

    private static string Short(ReadoutInterval? i) => i is null ? "unavailable" : Num(i.Estimate);

    /// <summary>Help text of the cost card: status reasons, provenance, the F-below-1 explanation and the benchmark scope.</summary>
    public string ReadoutCostHelp
    {
        get
        {
            if (!Shared.IsPhysicalReadout) return "";
            var lines = new List<string> { ReadoutCostStatusDetails };
            if (ReadoutCostCell is { } cell)
            {
                lines.Add(string.Create(CultureInfo.InvariantCulture,
                    $"Calibration {cell.CalibrationSeeds - cell.Failures.Count} / {cell.CalibrationSeeds} seeds at {cell.Budget:N0} histories per crystal ({cell.SourceRecord}, {cell.SourcePhase}). Card rows are isolated; the full table adds 100 000 input hits/s."));
                lines.AddRange(BiasExplanations(cell));
            }
            lines.Add(ReadoutCostScope);
            return string.Join("\n\n", lines.Where(l => l.Length > 0));
        }
    }

    /// <summary>For every row with F below 1: the Direct path's own bias share of its MSE, from the record.</summary>
    public static IReadOnlyList<string> BiasExplanations(ReadoutOfferCell cell) => cell.CostRows
        .Where(r => r.F is { Estimate: < 1 } && r.Direct.RmsDeg is not null && r.Direct.BiasDeg is not null)
        .Select(r =>
        {
            double rms = r.Direct.RmsDeg!.Estimate, bias = r.Direct.BiasDeg!.Estimate;
            return string.Create(CultureInfo.InvariantCulture,
                $"F below 1 at {r.TargetDirectCounts:N0} counts, {Rate(r.RateCps)}: the ideal Direct reference has RMS {Num(rms)}° with per-position bias {Num(bias)}° ({bias * bias / (rms * rms):P1} of its MSE is bias). F below 1 is chiefly direct-reference bias under this decoder; it does not establish a general Anger advantage.");
        }).ToArray();

    /// <summary>The declared benchmark scope and interval method, from the record.</summary>
    public string ReadoutCostScope => Shared.ReadoutOffers is { IsAvailable: true, Benchmark: { } b } record
        ? string.Create(CultureInfo.InvariantCulture,
            $"Benchmark: one mono-line point source at {b.Positions} positions (0 and ±{b.PositionHalfFraction:0.##} of the fully coded half field) at {b.FocalDistanceMm:0} mm; {b.Decoder}; ±{b.WindowFraction:P0} windows; flood correction {b.FloodCorrection}; equal emitted fluence; {b.TrialsPerPosition} trials per position; {b.CostSeeds} cost seeds, {b.CalibrationSeeds} calibration seeds. ")
          + "Direct = the ideal largest-deposit / true-energy reference, not Studio's legacy chain. Remote = radial angular error beyond cell / D radians; remote peaks are included in the RMS. "
          + $"F meaning: {record.FMeaning}. Intervals: {record.IntervalMethod}; {record.IntervalCaution}. "
          + "This acquisition's counts are shown separately; the two benchmark exposures and rates are not interpolated and Anger counts are not mapped to Direct counts."
        : $"Imaging cost unavailable: {Shared.ReadoutOffers.UnavailableReason}";

    /// <summary>The current acquisition's window reading, kept apart from the benchmark exposures.</summary>
    public string ReadoutCurrentCounts => SelectedChannel is { } channel && Shared.IsPhysicalReadout
        ? channel.Unavailable ?? string.Create(CultureInfo.InvariantCulture,
            $"This acquisition: {channel.RawCounts ?? (long)channel.Image.EffectiveCounts:N0} raw counts in the window")
            + (channel.CalibrationLineKeV is { } l ? string.Create(CultureInfo.InvariantCulture, $" ({l:0.##} keV calibration)") : "")
            + (channel.FloodCorrected ? " · display flood-corrected (opt-in)" : "")
        : "";

    internal void NotifyReadoutCost()
    {
        foreach (string name in new[] { nameof(ReadoutCostCell), nameof(ReadoutCostInherited), nameof(ReadoutCostStatus), nameof(ReadoutCostRows),
            nameof(ReadoutCostCard), nameof(ReadoutCostScope), nameof(ReadoutCurrentCounts), nameof(SamplingCaption), nameof(SamplingEvidence),
            nameof(ReadoutCostStatusDetails), nameof(ReadoutCostHelp) })
            OnPropertyChanged(name);
    }
}
