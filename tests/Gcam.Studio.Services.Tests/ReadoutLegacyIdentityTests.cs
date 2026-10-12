using System.Security.Cryptography;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Services.Tests;

/// <summary>TODO-41, SD-A1 / PD-2: the 12 × 12 Baseline head at 661.7 keV keeps its TODO-40 preparation (seeds 402–408,
/// Legacy solve and LUT) and its acquisition stream byte for byte. The SHA-256 fixtures were recorded from the code
/// BEFORE the per-line bank was introduced (branch head 0dc0b68 plus the opt-in correction histogram, which defaults to
/// off); the hashed bytes are every published preparation fact, every measured record, realised hit and truth sample of
/// a fixed-seed two-second acquisition, the assigned flood, and the physical imaging / spectrum projections.</summary>
public sealed class ReadoutLegacyIdentityTests(ITestOutputHelper output)
{
    private const string PreparationHash = "6a8b291c5831fd269c06565d791c5c044e052347b970203675e6749ce724ae82";
    private const string AcquisitionHash = "6b169b2f247c65c8d9e9144e33db6e20b9b616058270f14babf0bdbf0667b16d";
    private const string ProjectionHash = "74a59000c210666006234bed8f741aa98f42b4677e7e62805e57a29502edfa71";

    private static readonly OpticsSettings Head = new() { DetectorPixels = 12, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
    private static readonly SceneSource[] Scene = [new() { ActivityUCi = 500 }];

    private static string Hash(Action<BinaryWriter> write)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) write(w);
        return Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
    }

    private static void Image(BinaryWriter w, Gcam.Core.DetectorImage image)
    {
        w.Write(image.Width); w.Write(image.Height);
        foreach (double v in image.Raw) w.Write(v);
    }

    internal static string HashPreparation(ReadoutPreparation p) => Hash(w =>
    {
        w.Write(p.Succeeded); w.Write(p.Failure ?? "");
        Image(w, p.Density);
        foreach (int l in p.Labels) w.Write(l);
        w.Write(p.Peaks.Count); foreach (var (x, y) in p.Peaks) { w.Write(x); w.Write(y); }
        w.Write(p.Diagnostics.Count); foreach (string d in p.Diagnostics) w.Write(d);
        w.Write(p.Scored); w.Write(p.Triggered); w.Write(p.InWindow); w.Write(p.GainFallback);
        w.Write(p.RiseNs); w.Write(p.TailNs); w.Write(p.Normalization); w.Write(p.SupportNs); w.Write(p.ThresholdCodes);
        w.Write(p.SensorActiveWidthMm);
        var c = p.Circuit!;
        foreach (string n in c.Nodes) w.Write(n);
        foreach (var r in c.Resistors) { w.Write(r.A); w.Write(r.B); w.Write(r.Ohm); }
        w.Write(c.InputOhm);
    });

    [Fact]
    public async Task Baseline661_PreparationAcquisitionAndProjections_MatchPreBankFixtures()
    {
        var service = new ReadoutPreparationService();
        var p = await service.PrepareAsync(Head, new());
        string preparation = HashPreparation(p);
        Assert.Equal("readout-v1|12|1|0.1|0.15|2", p.Key);
        Assert.Equal("readout-v1|12|1|0.1|0.15|2|line 661.7|2000|Legacy|Legacy|legacy-seeds-402", p.CalibrationKey);
        Assert.Equal("optical 402; transport 403; response 404; ADC 405; independent validation 406/407/408", p.Seeds);
        Assert.Equal(new ReadoutModuleGeometry(12, 12, 1, .1, 12, 12, 1, .9), p.Module);
        Assert.True(p.Plan is { IsLegacy: true, LineKeV: 661.7, Budget: 2000, Method: FloodCalibrationMethod.Legacy, Solve: ChargeNetworkSolveMethod.Legacy });
        Assert.NotEqual(Guid.Empty, p.Id);
        Assert.Equal(p.Id, (await service.PrepareAsync(Head, new())).Id);
        Assert.Equal(144, p.Gains.Count);
        // Recorded from the pre-fix Release calibration's entire private gain array; not a sampled acquisition.
        Assert.Equal("ff8cb478d33726d3e532f35c703b6925bcc61dc4b1bc067cc9ada7f8b2952bda",
            Hash(w => { foreach (double gain in p.Gains) w.Write(gain); }));

        var detector = new DetectorSettings { ReadoutMode = ReadoutMode.FourOutputAnger, PreparedReadoutId = p.Id };
        var clock = new ManualTimeProvider();
        await using var session = new SimulationService(clock, service).Start(Scene, Head, 2, 4, detector, seed: 4711);
        AcquisitionSnapshot? final = null;
        await foreach (var s in session.ReadSnapshotsAsync())
        {
            final = s;
            if (s.IsCompleted) continue;
            await clock.WaitForTimerAsync(); clock.Advance(TimeSpan.FromMilliseconds(250));
        }
        var r = final!.Readout!;
        string acquisition = Hash(w =>
        {
            w.Write(final.LiveTimeS); w.Write(final.Counts); w.Write(r.Unknown);
            foreach (var e in r.Records)
            {
                w.Write(e.Index); w.Write(e.HoldTimeS); w.Write(e.TriggerTimeS);
                foreach (var q in new[] { e.Codes, e.Analog }) { w.Write(q.A); w.Write(q.B); w.Write(q.C); w.Write(q.D); }
                w.Write(e.RawX); w.Write(e.RawY); w.Write(e.Crystal); w.Write(e.EnergyKeV);
                w.Write(e.DominantHit); w.Write(e.Contributors); w.Write(e.DominantShare);
            }
            foreach (var h in r.Hits) { w.Write(h.ArrivalTimeS); w.Write(h.Charges.A); w.Write(h.Charges.B); w.Write(h.Charges.C); w.Write(h.Charges.D); }
            foreach (var t in r.TruthSamples)
            {
                w.Write(t.HitIndex);
                foreach (var s in t.Sites) { w.Write(s.XMm); w.Write(s.YMm); w.Write(s.ZMm); w.Write(s.TimeNs); w.Write(s.EnergyKeV); w.Write(s.CrystalX); w.Write(s.CrystalY); }
            }
            Image(w, final.Imaging.Flood);
            Image(w, r.LiveDensity!);
        });

        var config = SimulationService.BuildConfig(Scene, Head, detector);
        var imaging = PhysicalReadoutProjection.Imaging(final, config, new() { WindowLowKeV = 600, WindowHighKeV = 720 }, default);
        var spectrum = PhysicalReadoutProjection.Spectrum(r, new() { Readout = r, WindowLowKeV = 600, WindowHighKeV = 720 }, default);
        string projection = Hash(w =>
        {
            foreach (var channel in imaging.Channels)
            {
                w.Write(channel.Isotope); w.Write(channel.LoKeV); w.Write(channel.HiKeV);
                Image(w, channel.Image.Flood); w.Write(channel.Image.EffectiveCounts);
                if (channel.Image.Reconstruction is { } recon) Image(w, recon);
                if (channel.Image.Estimate is { } est) { w.Write(est.Position.X); w.Write(est.Position.Y); w.Write(est.Confidence); }
            }
            foreach (double v in spectrum.Counts) w.Write(v);
            w.Write(spectrum.TotalCounts); w.Write(spectrum.OverflowCounts); w.Write(spectrum.Bands[0].Counts);
        });
        output.WriteLine($"Preparation {preparation}\nAcquisition {acquisition}\nProjection {projection}\n"
            + $"records {r.Records.Count}, assigned {final.Counts}, unknown {r.Unknown}, hits {r.Hits.Count}");
        Assert.Equal(PreparationHash, preparation);
        Assert.Equal(AcquisitionHash, acquisition);
        Assert.Equal(ProjectionHash, projection);
    }
}
