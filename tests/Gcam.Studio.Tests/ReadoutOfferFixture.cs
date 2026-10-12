using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Tests;

/// <summary>A hand-built offer / cost record with the shipped record's shape (geometry ids, lines, populations, four cost
/// rows, two diagnostics rates). Values are fixtures for hand-derived oracles, not evidence.</summary>
internal static class ReadoutOfferFixture
{
    public static ReadoutInterval I(double estimate, double lower, double upper, int clusters = 8) => new(estimate, lower, upper, clusters);
    private static ReadoutInterval F(double f) => I(f, f * .9, f * 1.1);

    private static ReadoutCostPath Path(double rms, double bias) =>
        new(true, I(rms, rms * .9, rms * 1.1), I(bias, bias * .9, bias * 1.1), I(0, 0, 0), I(1000, 990, 1010), 0);

    private static ReadoutCostRow[] Rows(double directRms, double directBias, double angerRms, double angerBias, bool anger = true) =>
        [.. new[] { (1000, 0.0), (1000, 1e5), (4000, 0.0), (4000, 1e5) }.Select(c => new ReadoutCostRow(c.Item1, c.Item2,
            Path(directRms, directBias), anger ? Path(angerRms, angerBias) : new ReadoutCostPath(false, null, null, null, null, 8),
            anger ? F(angerRms * angerRms / (directRms * directRms)) : null))];

    private static ReadoutCrystalDiagnostics[] Diagnostics(bool available) =>
        [.. new[] { 0.0, 1e5 }.Select(r => available
            ? new ReadoutCrystalDiagnostics(r, true, I(1e-5, 0, 2e-5, 32), I(1e-5, 0, 2e-5, 32), I(.01, .009, .011, 32),
                I(.6, .59, .61, 32), I(.67, .66, .68, 32), "isolated held-out flood")
            : new ReadoutCrystalDiagnostics(r, false, null, null, null, null, null, null))];

    private static ReadoutOfferCell Offered(string geometry, double line, int budget, double directRms = .16, double directBias = .15,
        double angerRms = .6, double angerBias = .3) =>
        new(geometry, line, true, "experimental", 32, [], budget, "confirmation-v4.json", "confirm",
            Rows(directRms, directBias, angerRms, angerBias), Diagnostics(true), null);

    private static ReadoutOfferCell Refused(string geometry, double line) =>
        new(geometry, line, false, "refused", 32, [new(43000001, "fixture: no resolved spot structure")], 2000,
            "confirmation-v4.json", "confirm", Rows(.2, .1, 0, 0, anger: false), Diagnostics(false), null);

    public static ReadoutOfferRecord Record() => new(true, null, new string('a', 64),
        [
            Refused("Baseline", 122.06), Offered("Baseline", 661.7, 2000), Offered("Baseline", 1332.5, 2000),
            Refused("High", 122.06),
            // Direct RMS 0.5° with bias 0.49°: bias share 0.49² / 0.5² = 96.04 %; F = 0.45² / 0.5² = 0.81 < 1.
            Offered("High", 661.7, 64000, directRms: .5, directBias: .49, angerRms: .45, angerBias: .44) with { SourceRecord = "repair-v5.json", SourcePhase = "repair" },
            Offered("High", 1332.5, 2000),
        ],
        [new("Baseline", 12, 1, 60, 7, 1), new("High", 44, .4, 100, 13, .5)],
        new ReadoutBenchmark(1000, .45, 9, "FiniteMaskCrossCorrelation", "OwnCalibrationFloodPerPath", .15, 64, "equal fluence", 8, 32),
        "equal-fluence source MSE ratio, Direct reference = 1; not a measurement-time factor",
        "two-sided 95% Student outer-seed cluster approximation", "not an exact coverage guarantee");
}
