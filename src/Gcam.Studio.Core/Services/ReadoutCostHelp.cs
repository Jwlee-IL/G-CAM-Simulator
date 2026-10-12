using System.Globalization;

namespace Gcam.Studio.Core.Services;

/// <summary>Descriptive evidence for offered cells of one module; never a prediction for an acquisition.</summary>
public static class ReadoutCostHelp
{
    public const string FWording = "F is an equal-fluence MSE ratio, not a time factor.";
    private static string Num(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    public static string Build(ReadoutOfferRecord record, IEnumerable<ReadoutLinePlan> plans)
    {
        if (!record.IsAvailable || record.Benchmark is not { } b)
            return $"Localisation cost unavailable: {record.UnavailableReason}\n\n{FWording}";
        var cells = plans.Where(p => p.Status == ReadoutLineStatus.Offered && p.Cell is not null)
            .Select(p => p.Cell!).Distinct().ToArray();
        var ratios = cells.SelectMany(c => c.CostRows).Where(r => r.F is not null).Select(r => r.F!.Estimate).ToArray();
        var text = new List<string>
        {
            "Benchmark reference only; not a prediction for this scene.",
            string.Create(CultureInfo.InvariantCulture,
                $"Benchmark: one mono-line point source at {b.Positions} positions (0 and ±{b.PositionHalfFraction:0.##} of the fully coded half field) at {b.FocalDistanceMm:0} mm; {b.Decoder}; ±{b.WindowFraction:P0} windows; flood correction {b.FloodCorrection}; equal emitted fluence; {b.TrialsPerPosition} trials per position; {b.CostSeeds} cost seeds, {b.CalibrationSeeds} calibration seeds."),
            ratios.Length == 0 ? "No offered cost cells for this module." : $"Module's offered cells: F {Num(ratios.Min())}–{Num(ratios.Max())} (point estimates across recorded exposures and rates).",
            "Direct = the ideal largest-deposit / true-energy reference, not Studio's legacy chain.",
            $"F meaning: {record.FMeaning}. Intervals: {record.IntervalMethod}; {record.IntervalCaution}.",
        };
        foreach (var cell in cells)
        foreach (var row in cell.CostRows.Where(r => r.F is { Estimate: < 1 } && r.Direct.RmsDeg is not null && r.Direct.BiasDeg is not null))
        {
            double rms = row.Direct.RmsDeg!.Estimate, bias = row.Direct.BiasDeg!.Estimate;
            text.Add(string.Create(CultureInfo.InvariantCulture,
                $"F below 1 at {cell.EnergyKeV:0.##} keV, {row.TargetDirectCounts:N0} counts, {row.RateCps:N0} input hits/s: ideal Direct RMS {Num(rms)}°, per-position bias {Num(bias)}° ({bias * bias / (rms * rms):P1} of its MSE is bias). F below 1 is chiefly direct-reference bias under this decoder; it does not establish a general Anger advantage."));
        }
        text.Add(FWording);
        return string.Join("\n\n", text);
    }
}
