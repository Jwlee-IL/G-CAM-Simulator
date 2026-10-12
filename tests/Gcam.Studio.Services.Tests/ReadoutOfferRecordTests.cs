using System.Security.Cryptography;
using System.Text;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services.Tests;

/// <summary>TODO-41 (SD-A13): the embedded record is the generated, hash-pinned file; malformed or missing data is
/// "unavailable", never a fallback; the published outcome (8 offers, 4 refusals at 122.06 keV, the two declared repair
/// cells at 8 k / 64 k) is read verbatim and turned into per-line plans.</summary>
public sealed class ReadoutOfferRecordTests
{
    private static string RepositoryFile(string relative)
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "Gcam.sln"))) dir = Directory.GetParent(dir)!.FullName;
        return Path.Combine(dir, relative);
    }

    [Fact]
    public void EmbeddedRecord_IsTheRepositoryFile_AndMatchesTheCompiledPin()
    {
        byte[] file = File.ReadAllBytes(RepositoryFile("samples/evidence/studio-readout/offer-cost-v1.json"));
        Assert.Equal(ReadoutOfferPin.Sha256, Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant());
        // LoadEmbedded hashes the embedded bytes against the same pin, so the build carries exactly this file.
        var record = ReadoutOfferRecordLoader.LoadEmbedded();
        Assert.True(record.IsAvailable, record.UnavailableReason);
        Assert.Equal(ReadoutOfferPin.Sha256, record.Sha256);
    }

    [Fact]
    public void PublishedOutcome_IsReadVerbatim()
    {
        var record = ReadoutOfferRecordLoader.LoadEmbedded();
        Assert.Equal(new[] { "Baseline", "Sharp", "Wide", "High" }, record.Geometries.Select(g => g.Id));
        Assert.Equal(12, record.Cells.Count);
        Assert.Equal(8, record.Cells.Count(c => c.Offered));
        Assert.All(record.Cells.Where(c => c.EnergyKeV == 122.06), c => Assert.True(c is { Offered: false, Label: "refused" } && c.Failures.Count == 32));
        var wide = record.Cell("Wide", 661.7)!; var high = record.Cell("High", 661.7)!;
        Assert.True(wide is { Offered: true, Budget: 8000, SourceRecord: "repair-v5.json", SourcePhase: "repair" });
        Assert.True(high is { Offered: true, Budget: 64000, SourceRecord: "repair-v5.json" });
        Assert.True(wide.Superseded is { Offered: false, Budget: 2000 } && wide.Superseded.Failures.Count == 4);
        Assert.True(high.Superseded is { Offered: false, Budget: 32000 } && high.Superseded.Failures.Count == 1);
        // Wide 662 / 4 k / isolated (Turn 7 §3): Direct RMS 0.51664°, bias 0.51108°, F 0.82946 [0.82312, 0.83584].
        var row = wide.CostRows.Single(r => r.TargetDirectCounts == 4000 && r.RateCps == 0);
        Assert.Equal(0.51664, row.Direct.RmsDeg!.Estimate, 5);
        Assert.Equal(0.51108, row.Direct.BiasDeg!.Estimate, 5);
        Assert.Equal(0.82946, row.F!.Estimate, 5);
        Assert.Equal(0.82312, row.F.Lower95, 5);
        Assert.Equal(0.83584, row.F.Upper95, 5);
        Assert.All(record.Cells.SelectMany(c => c.CostRows), r => Assert.Equal(r.F is not null, r.Direct.Available && r.Anger.Available));
        Assert.Contains("not a measurement-time factor", record.FMeaning);
        Assert.Equal(0.15, record.Benchmark!.WindowFraction);
    }

    [Fact]
    public void ChangedOrMalformedBytes_AreUnavailable_NeverAFallback()
    {
        byte[] file = File.ReadAllBytes(RepositoryFile("samples/evidence/studio-readout/offer-cost-v1.json"));
        var tampered = (byte[])file.Clone(); tampered[^2] ^= 1;
        var changed = ReadoutOfferRecordLoader.Parse(tampered, ReadoutOfferPin.Sha256);
        Assert.False(changed.IsAvailable); Assert.Contains("compiled SHA-256 pin", changed.UnavailableReason); Assert.Empty(changed.Cells);
        byte[] garbage = Encoding.UTF8.GetBytes("{\"SchemaVersion\":1,\"Kind\":\"gcam.studio.readout-offer-cost\"}");
        var malformed = ReadoutOfferRecordLoader.Parse(garbage, Convert.ToHexString(SHA256.HashData(garbage)).ToLowerInvariant());
        Assert.False(malformed.IsAvailable); Assert.Contains("malformed", malformed.UnavailableReason);
        // A corrupt population (offered with a failure) is refused even when the pin is re-computed for the corrupt bytes.
        string text = Encoding.UTF8.GetString(file);
        int at = text.IndexOf("\"Offered\": true", StringComparison.Ordinal);
        byte[] corrupt = Encoding.UTF8.GetBytes(text.Remove(at, "\"Offered\": true".Length).Insert(at, "\"Offered\": false"));
        var refused = ReadoutOfferRecordLoader.Parse(corrupt, Convert.ToHexString(SHA256.HashData(corrupt)).ToLowerInvariant());
        Assert.False(refused.IsAvailable); Assert.Contains("corrupt population", refused.UnavailableReason);
    }

    [Fact]
    public void Plans_FollowTheRecordAndTheModuleFingerprint()
    {
        var record = ReadoutOfferRecordLoader.LoadEmbedded();
        var detector = new DetectorSettings { ReadoutMode = ReadoutMode.FourOutputAnger };
        var sharp = new OpticsSettings();
        var refused = ReadoutPolicy.Plan(sharp, detector, 122.06, record);
        Assert.Equal(ReadoutLineStatus.Refused, refused.Status);
        Assert.Contains("32 / 32 seeds", refused.Reason);
        Assert.Contains(record.Cell("Sharp", 122.06)!.Failures[0].Failure, refused.Reason);
        var high = new OpticsSettings { DetectorPixels = 44, PixelPitchMm = .4, MuraRank = 13, CellPitchMm = .5, MaskDetectorDistanceMm = 100 };
        Assert.True(ReadoutPolicy.Plan(high, detector, 661.7, record) is { Status: ReadoutLineStatus.Offered, Budget: 64000, IsLegacy: false, Method: FloodCalibrationMethod.LatticeGrowth, Solve: ChargeNetworkSolveMethod.Adjoint });
        var baseline = new OpticsSettings { DetectorPixels = 12, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
        Assert.True(ReadoutPolicy.Plan(baseline, detector, 661.7, record) is { IsLegacy: true, Budget: 2000, Method: FloodCalibrationMethod.Legacy });
        Assert.True(ReadoutPolicy.Plan(baseline, detector with { ReflectorGapMm = .09 }, 661.7, record) is { Status: ReadoutLineStatus.Local, Budget: 2000, Cell: null });
        Assert.Equal(new[] { 661.7, 1332.5 }, ReadoutPolicy.LinesInScene([new() { Isotope = "Cs-137" }, new() { Isotope = "Co-60" }, new() { Isotope = "Co-57" }]));
    }
}
