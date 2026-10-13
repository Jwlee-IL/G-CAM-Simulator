using System.Globalization;
using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>Per-module, per-line readout policy (TODO-41, SD-A1…A3, A9, A10): which calibration lines a module may prepare,
/// at which budget and with which method, decided only from the shipped record and the module fingerprint. Imaging cost
/// values never gate anything here.</summary>
public static class ReadoutPolicy
{
    public const string FieldOnlyMessage = "Physical readout cannot start a field-only scene: no field calibration line is offered. Add a source with a preparable calibration line, or select DirectCrystal.";
    public static string? AcquisitionError(int sourceCount)
        => sourceCount == 0 ? FieldOnlyMessage : null;

    /// <summary>Module constants the confirmation recipe fixed for every geometry (Studio forces GAGG and matched
    /// virtual sensors on the physical path).</summary>
    public const double RecordGapMm = 0.1, RecordEntranceAbsorberMm = 0.15, RecordBackingMm = 2;

    /// <summary>SD-A10: an unmatched (Custom) module calibrates locally at this budget, with LatticeGrowth / adjoint —
    /// a declared, unvalidated policy, not a cut to a confirmed budget.</summary>
    public const int LocalBudget = 2000;

    /// <summary>The reference line: broadband readings (All channel, spectrum, waveform reading) use its calibration
    /// when it is captured, labelled as such.</summary>
    public const double ReferenceLineKeV = 661.7;

    /// <summary>The calibration lines the confirmation measured. An engine emission is mapped to a line only when the
    /// energies are equal; rounded labels never match (Co-57's 122.1 keV emission is not the 122.06 keV row).</summary>
    public static IReadOnlyList<double> AnalysisLines { get; } = [122.06, 661.7, 1332.5];

    private const int LegacyCount = 12;

    /// <summary>Line-independent module key: crystal grid, pitch, gap, entrance absorber and backing.</summary>
    public static string Key(OpticsSettings optics, DetectorSettings detector)
        => FormattableString.Invariant($"readout-v1|{optics.DetectorPixels}|{optics.PixelPitchMm:R}|{detector.ReflectorGapMm:R}|{detector.EntranceAbsorberMm:R}|{detector.BackingScatterMm:R}");

    /// <summary>Full per-line cache identity: module, line, budget, method, solve and the seed scheme.</summary>
    public static string CalibrationKey(string moduleKey, ReadoutLinePlan plan)
        => FormattableString.Invariant($"{moduleKey}|line {plan.LineKeV:R}|{plan.Budget}|{plan.Method}|{plan.Solve}|{(plan.IsLegacy ? "legacy-seeds-402" : "line-seeds-v1")}");

    /// <summary>The confirmed geometry this module is (crystal count, pitch and the recipe's gap / absorber / backing),
    /// irrespective of the Custom label; null when none matches or the record is unavailable.</summary>
    public static ReadoutOfferGeometry? MatchModule(OpticsSettings optics, DetectorSettings detector, ReadoutOfferRecord record)
        => record.IsAvailable
            ? record.Geometries.FirstOrDefault(g => g.Count == optics.DetectorPixels && g.PitchMm == optics.PixelPitchMm
                && detector.ReflectorGapMm == RecordGapMm && detector.EntranceAbsorberMm == RecordEntranceAbsorberMm
                && detector.BackingScatterMm == RecordBackingMm)
            : null;

    public static ReadoutLinePlan Plan(OpticsSettings optics, DetectorSettings detector, double lineKeV, ReadoutOfferRecord record)
    {
        ArgumentNullException.ThrowIfNull(optics);
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(record);
        string line = lineKeV.ToString("0.##", CultureInfo.InvariantCulture);
        if (!record.IsAvailable)
            return new(lineKeV, ReadoutLineStatus.Unavailable, null, null, 0, FloodCalibrationMethod.Legacy,
                ChargeNetworkSolveMethod.Legacy, false, $"Unavailable: {record.UnavailableReason}");
        var geometry = MatchModule(optics, detector, record);
        if (geometry is null)
            return new(lineKeV, ReadoutLineStatus.Local, null, null, LocalBudget, FloodCalibrationMethod.LatticeGrowth,
                ChargeNetworkSolveMethod.Adjoint, false,
                $"Local calibration · experimental, unvalidated: this module matches no confirmed preset geometry; {line} keV calibrates at {LocalBudget:N0} histories per crystal (LatticeGrowth LUT, adjoint solve). Imaging cost unmeasured for this configuration.");
        bool baseline = geometry.Count == LegacyCount;
        var method = baseline ? FloodCalibrationMethod.Legacy : FloodCalibrationMethod.LatticeGrowth;
        var solve = baseline ? ChargeNetworkSolveMethod.Legacy : ChargeNetworkSolveMethod.Adjoint;
        var cell = record.Cell(geometry.Id, lineKeV);
        if (cell is null)
            return new(lineKeV, ReadoutLineStatus.Unavailable, geometry.Id, null, 0, method, solve, false,
                $"Unavailable: {line} keV is not a confirmed calibration line for {geometry.Id}; unknown lines are not offered.");
        if (!cell.Offered)
            return new(lineKeV, ReadoutLineStatus.Refused, geometry.Id, cell, cell.Budget, method, solve, false,
                $"Refused: {geometry.Id} {line} keV calibration failed on {cell.Failures.Count} / {cell.CalibrationSeeds} seeds at {cell.Budget:N0} histories per crystal ({cell.SourceRecord}); first failure: {cell.Failures[0].Failure}");
        bool legacy = baseline && lineKeV == ReferenceLineKeV;
        return new(lineKeV, ReadoutLineStatus.Offered, geometry.Id, cell, cell.Budget, method, solve, legacy,
            $"Offered (experimental): {geometry.Id} {line} keV calibration succeeded on {cell.CalibrationSeeds} / {cell.CalibrationSeeds} seeds at {cell.Budget:N0} histories per crystal ({cell.SourceRecord}, {cell.SourcePhase}).");
    }

    /// <summary>Every analysis line's plan for this module, ascending in energy.</summary>
    public static IReadOnlyList<ReadoutLinePlan> Plans(OpticsSettings optics, DetectorSettings detector, ReadoutOfferRecord record)
        => AnalysisLines.Select(l => Plan(optics, detector, l, record)).ToArray();

    /// <summary>Analysis lines emitted by the scene (exact energy equality with an isotope emission line).</summary>
    public static IReadOnlyList<double> LinesInScene(IEnumerable<SceneSource> scene)
    {
        var emitted = scene.SelectMany(s => Isotopes.Get(s.Isotope).Lines).Select(l => l.EnergyKeV).ToHashSet();
        return AnalysisLines.Where(emitted.Contains).ToArray();
    }

    /// <summary>The lines an acquisition must capture: present in the scene and preparable (offered, or local for an
    /// unmatched module). Refused, incidental and unknown lines stay in transport and never block these.</summary>
    public static IReadOnlyList<ReadoutLinePlan> RequiredLines(IEnumerable<SceneSource> scene, OpticsSettings optics,
        DetectorSettings detector, ReadoutOfferRecord record)
    {
        var present = LinesInScene(scene);
        return Plans(optics, detector, record).Where(p => p.CanPrepare && present.Contains(p.LineKeV)).ToArray();
    }

    /// <summary>The single analysis line inside an inclusive keV window, or null with the reason.</summary>
    public static double? LineForWindow(double lowKeV, double highKeV, out string? reason)
    {
        var inside = AnalysisLines.Where(l => l >= lowKeV && l <= highKeV).ToArray();
        reason = inside.Length switch
        {
            0 => "No calibration line inside the window: a physical image needs the window to contain one calibrated line.",
            1 => null,
            _ => $"The window spans {inside.Length} calibration lines ({string.Join(", ", inside.Select(l => l.ToString("0.##", CultureInfo.InvariantCulture)))} keV); narrow it to one line.",
        };
        return inside.Length == 1 ? inside[0] : null;
    }

    /// <summary>The benchmark window of a line: plus / minus <paramref name="fraction"/> of it. The edges are computed in
    /// decimal from the record's decimal values (661.7 keV gives 562.445 / 760.955, 1332.5 keV gives 1132.625 / 1532.375), so
    /// the button sets exactly what a user would type.</summary>
    public static (double Low, double High) BenchmarkWindow(double lineKeV, double fraction)
    {
        decimal line = (decimal)lineKeV, f = (decimal)fraction;
        return ((double)(line * (1 - f)), (double)(line * (1 + f)));
    }
}
