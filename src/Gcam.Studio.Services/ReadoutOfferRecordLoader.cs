using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>Reads the embedded, hash-pinned offer / cost record. No file system, download or write is involved: the bytes
/// are compiled into this assembly. A missing resource, a hash mismatch or any schema violation yields an unavailable
/// record with its reason; there is no fallback to an older cell or a fabricated offer.</summary>
public static class ReadoutOfferRecordLoader
{
    public const string ResourceName = "Gcam.Studio.Services.ReadoutOfferRecord.json";
    private const string Kind = "gcam.studio.readout-offer-cost";
    private static readonly double[] Lines = [122.06, 661.7, 1332.5];

    public static ReadoutOfferRecord LoadEmbedded()
    {
        using var stream = typeof(ReadoutOfferRecordLoader).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null) return ReadoutOfferRecord.Unavailable("The readout offer record is missing from this build.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Parse(buffer.ToArray(), ReadoutOfferPin.Sha256);
    }

    public static ReadoutOfferRecord Parse(byte[] bytes, string pinnedSha256)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (sha != pinnedSha256)
            return ReadoutOfferRecord.Unavailable($"The readout offer record does not match its compiled SHA-256 pin (found {sha}).", sha);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            return Read(document.RootElement, sha);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException
            or FormatException or InvalidDataException)
        {
            return ReadoutOfferRecord.Unavailable($"The readout offer record is malformed: {ex.Message}", sha);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    private static ReadoutOfferRecord Read(JsonElement root, string sha)
    {
        Require(root.GetProperty("SchemaVersion").GetInt32() == 1 && root.GetProperty("Kind").GetString() == Kind, "unsupported schema");
        var geometries = root.GetProperty("Geometries").EnumerateArray().Select(g => new ReadoutOfferGeometry(
            g.GetProperty("Id").GetString()!, g.GetProperty("Count").GetInt32(), g.GetProperty("PitchMm").GetDouble(),
            g.GetProperty("DistanceMm").GetDouble(), g.GetProperty("Rank").GetInt32(), g.GetProperty("CellMm").GetDouble())).ToArray();
        Require(geometries.Length > 0 && geometries.Select(g => g.Id).Distinct().Count() == geometries.Length, "geometries");
        var b = root.GetProperty("Benchmark");
        var benchmark = new ReadoutBenchmark(b.GetProperty("FocalDistanceMm").GetDouble(), b.GetProperty("PositionHalfFraction").GetDouble(),
            b.GetProperty("Positions").GetArrayLength(), b.GetProperty("Decoder").GetString()!, b.GetProperty("FloodCorrection").GetString()!,
            b.GetProperty("WindowFraction").GetDouble(), b.GetProperty("TrialsPerPosition").GetInt32(), b.GetProperty("Exposure").GetString()!,
            b.GetProperty("CostSeeds").GetInt32(), b.GetProperty("CalibrationSeeds").GetInt32());
        var cells = root.GetProperty("Cells").EnumerateArray().Select(c => Cell(c, benchmark)).ToArray();
        foreach (var g in geometries)
            Require(cells.Where(c => c.Geometry == g.Id).Select(c => c.EnergyKeV).Order().SequenceEqual(Lines), $"{g.Id}: incomplete line matrix");
        Require(cells.Length == geometries.Length * Lines.Length, "cell count");
        return new ReadoutOfferRecord(true, null, sha, Array.AsReadOnly(cells), Array.AsReadOnly(geometries), benchmark,
            root.GetProperty("FMeaning").GetString()!, root.GetProperty("IntervalMethod").GetString()!,
            root.GetProperty("IntervalCaution").GetString()!);
    }

    private static ReadoutCalibrationFailure[] Failures(JsonElement value) => value.EnumerateArray()
        .Select(f => new ReadoutCalibrationFailure(f.GetProperty("Seed").GetInt32(), f.GetProperty("Failure").GetString()!)).ToArray();

    private static ReadoutOfferCell Cell(JsonElement c, ReadoutBenchmark benchmark)
    {
        string geometry = c.GetProperty("Geometry").GetString()!;
        double energy = c.GetProperty("EnergyKeV").GetDouble();
        bool offered = c.GetProperty("Offered").GetBoolean();
        var failures = Failures(c.GetProperty("Failures"));
        int seeds = c.GetProperty("CalibrationSeeds").GetInt32();
        string label = c.GetProperty("Label").GetString()!;
        Require(seeds == benchmark.CalibrationSeeds && offered == (failures.Length == 0)
            && label == (offered ? "experimental" : "refused"), $"{geometry}/{energy.ToString(CultureInfo.InvariantCulture)}: corrupt population");
        int budget = c.GetProperty("Budget").GetInt32();
        Require(budget > 0, "budget");
        var rows = c.GetProperty("CostTable").EnumerateArray().Select(r =>
        {
            var paths = r.GetProperty("Paths");
            var direct = Path(paths.GetProperty("Direct"), benchmark.CostSeeds);
            var anger = Path(paths.GetProperty("Anger"), benchmark.CostSeeds);
            ReadoutInterval? f = r.GetProperty("F").ValueKind == JsonValueKind.Null ? null : Interval(r.GetProperty("F"), benchmark.CostSeeds);
            Require((f is not null) == (direct.Available && anger.Available), "F without both paths");
            return new ReadoutCostRow(r.GetProperty("TargetDirectCounts").GetInt32(), r.GetProperty("RateCps").GetDouble(), direct, anger, f);
        }).OrderBy(r => r.TargetDirectCounts).ThenBy(r => r.RateCps).ToArray();
        Require(rows.Length == 4, "cost rows");
        var diagnostics = c.GetProperty("Diagnostics").EnumerateObject().Select(d =>
        {
            var v = d.Value;
            bool available = v.GetProperty("Available").GetBoolean();
            ReadoutInterval? Get(string name) => available ? Interval(v.GetProperty(name), benchmark.CalibrationSeeds) : null;
            return new ReadoutCrystalDiagnostics(double.Parse(d.Name, CultureInfo.InvariantCulture), available, Get("GXRad2"), Get("GYRad2"),
                Get("SingleMisId"), Get("MultiCrystalShare"), Get("WeakestMedianSensitivity"),
                available ? v.GetProperty("SensitivitySource").GetString() : null);
        }).OrderBy(d => d.RateCps).ToArray();
        ReadoutOfferOutcome? superseded = null;
        if (c.TryGetProperty("Superseded", out var s))
            superseded = new ReadoutOfferOutcome(s.GetProperty("Offered").GetBoolean(), s.GetProperty("Label").GetString()!,
                s.GetProperty("CalibrationSeeds").GetInt32(), Array.AsReadOnly(Failures(s.GetProperty("Failures"))), s.GetProperty("Budget").GetInt32(),
                s.GetProperty("SourceRecord").GetString()!, s.GetProperty("SourcePhase").GetString()!);
        return new ReadoutOfferCell(geometry, energy, offered, label, seeds, Array.AsReadOnly(failures), budget,
            c.GetProperty("SourceRecord").GetString()!, c.GetProperty("SourcePhase").GetString()!, Array.AsReadOnly(rows),
            Array.AsReadOnly(diagnostics), superseded);
    }

    private static ReadoutCostPath Path(JsonElement p, int clusters)
    {
        if (!p.GetProperty("Available").GetBoolean())
            return new ReadoutCostPath(false, null, null, null, null, p.GetProperty("MissingClusters").GetInt32());
        return new ReadoutCostPath(true, Interval(p.GetProperty("RmsDeg"), clusters), Interval(p.GetProperty("BiasDeg"), clusters),
            Interval(p.GetProperty("RemoteProbability"), clusters), Interval(p.GetProperty("AcceptedCounts"), clusters), 0);
    }

    private static ReadoutInterval Interval(JsonElement v, int clusters)
    {
        var i = new ReadoutInterval(v.GetProperty("Estimate").GetDouble(), v.GetProperty("Lower95").GetDouble(),
            v.GetProperty("Upper95").GetDouble(), v.GetProperty("Clusters").GetInt32());
        Require(i.Clusters == clusters && double.IsFinite(i.Estimate) && i.Lower95 <= i.Upper95, "interval");
        return i;
    }
}
