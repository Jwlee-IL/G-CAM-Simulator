namespace Gcam.Studio.Core.Services;

/// <summary>The shipped, hash-pinned offer and cost record. Malformed or missing data makes the whole record
/// unavailable with a reason — never a fallback offer.</summary>
public sealed record ReadoutOfferRecord(bool IsAvailable, string? UnavailableReason, string Sha256,
    IReadOnlyList<ReadoutOfferCell> Cells, IReadOnlyList<ReadoutOfferGeometry> Geometries, ReadoutBenchmark? Benchmark,
    string FMeaning, string IntervalMethod, string IntervalCaution)
{
    public static ReadoutOfferRecord Unavailable(string reason, string sha256 = "") =>
        new(false, reason, sha256, [], [], null, "", "", "");

    public ReadoutOfferCell? Cell(string geometry, double energyKeV) =>
        Cells.FirstOrDefault(c => c.Geometry == geometry && c.EnergyKeV == energyKeV);

    /// <summary>The calibration lines the record covers, in ascending energy.</summary>
    public IReadOnlyList<double> Lines => Cells.Select(c => c.EnergyKeV).Distinct().Order().ToArray();
}
