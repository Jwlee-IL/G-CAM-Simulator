using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>Reference rows and scene differences; calibration eligibility never depends on imaging cost.</summary>
public sealed record ReadoutCostApplicability(ReadoutOfferCell? Cell, IReadOnlyList<string> Differences, string? Unavailable)
{
    public bool ReferenceOnly => true;

    public static ReadoutCostApplicability Evaluate(ReadoutOfferRecord record, ReadoutPreparation preparation,
        IReadOnlyList<SceneSource> scene, OpticsSettings optics, DetectorSettings detector,
        CompareSettings settings, double inputRateCps, bool hasAmbient, bool hasBsr)
    {
        if (!record.IsAvailable || record.Benchmark is not { } benchmark)
            return new(null, [], record.UnavailableReason ?? "Benchmark unavailable.");
        var differences = new List<string>();
        var cell = preparation.Plan?.Cell;
        var geometry = record.Geometries.FirstOrDefault(g => g.Id == cell?.Geometry);
        if (geometry is null || ReadoutPolicy.MatchModule(optics, detector, record)?.Id != geometry.Id)
            differences.Add("Module has no matching measured cost profile.");
        else if (geometry.Rank != optics.MuraRank || geometry.CellMm != optics.CellPitchMm || geometry.DistanceMm != optics.MaskDetectorDistanceMm)
            differences.Add("Mask geometry differs from the benchmark profile.");
        if (scene.Count != 1 || scene.Any(s => Isotopes.Get(s.Isotope).Lines.Length != 1 ||
                Isotopes.Get(s.Isotope).Lines[0].EnergyKeV != preparation.LineKeV))
            differences.Add("Isotope / line: benchmark uses one mono-line point source; this scene has different emissions.");
        if (scene.Any(s => s.DistanceMm != benchmark.FocalDistanceMm) ||
            (settings.Imaging.FocalDistanceMm ?? optics.FocalDistanceMm) != benchmark.FocalDistanceMm)
            differences.Add("Distance / focus differs from the 1 m benchmark.");
        // The nine-position ensemble is never a prediction for a single acquired position.
        differences.Add("Position: reference is a nine-position ensemble; this acquisition is one scene.");
        double offset = benchmark.PositionHalfFraction * optics.MuraRank * optics.CellPitchMm * benchmark.FocalDistanceMm / optics.MaskDetectorDistanceMm / 2;
        bool OnGrid(double coordinate) => coordinate == 0 || Math.Abs(coordinate) == offset;
        if (scene.Any(s => !OnGrid(s.X) || !OnGrid(s.Y)))
            differences.Add("Position: at least one source lies outside the nine benchmark grid points.");
        differences.Add($"Rate: {inputRateCps:0.###} input hits/s; reference rows are isolated or 100,000 input hits/s (no interpolation).");
        if (hasAmbient || hasBsr) differences.Add("Ambient / added background differs from the source-only benchmark.");
        if (ReadoutPolicy.BenchmarkWindow(preparation.LineKeV, benchmark.WindowFraction) !=
            (settings.Imaging.WindowLowKeV, settings.Imaging.WindowHighKeV)) differences.Add("Energy window differs from benchmark ±15%.");
        if (settings.Imaging.Method != DecoderMethod.CrossCorrelation) differences.Add("Decoder differs from finite-mask cross-correlation.");
        differences.Add("Correction: reference corrects each path with its own calibration; Compare's ideal direct image is uncorrected.");
        return new(cell, differences.AsReadOnly(), cell is null ? "Imaging cost unmeasured for this module / line." : null);
    }
}
