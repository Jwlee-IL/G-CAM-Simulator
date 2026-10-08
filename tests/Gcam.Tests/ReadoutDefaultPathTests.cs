using System.Security.Cryptography;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

/// <summary>TODO-19, RD-6 "disabled path bit-identical": the direct crystal assignment must be untouched by the physical
/// readout. The SHA-256 fixtures below were recorded from the engine BEFORE the readout was added (commit aeac875, the
/// same seeds and budgets) — the hashed bytes are each event's pixel, deposit and arrival time and the source's counters
/// (list mode), the flood image (runner) and the serialised configuration. Recording the pre-optical interaction sites is
/// pure bookkeeping, so it must reproduce the same stream.</summary>
public class ReadoutDefaultPathTests
{
    private const string LabEvents = "0057f59cd68691b61d179c499bff4f3f201832032ff01269d7c356398e57ceb1";
    private const string Co60Events = "1bd61bb8a5175065b93745f7e721c8a783c5c4da6e823eaaed9bd175f20e8fa9";
    private const string RealismEvents = "f66c6bd1e6caf1cc36ce6e864e59549d86cd568547f662ca88e941649dff0e48";
    private const string CrosstalkImage = "5dbe7eea09ca409f3595811e4ccc8e7ed3d8e2a35f852e8146cca445e1ba083a";
    private const string LabJson = "d833711a64bf423f408d6321a29140e3cc2f25a7df12c3df89a4a0a5d1fbecd1";

    private static SimulationConfig Load(string file)
    {
        var c = ConfigLoader.Load(RepoPaths.Sample(file));
        c.Detector.Material = "GAGG";
        c.Seed = 12345;
        return c;
    }

    private static SimulationConfig Realism()
    {
        var c = Load("scenario.json");
        c.Detector.ReflectorGapMm = 0.1;
        c.Detector.OpticalCrosstalkFraction = 0.4;
        c.Detector.EntranceAbsorberMm = 1;
        c.Detector.BackingScatterMm = 2;
        return c;
    }

    private static string Events(SimulationConfig c, int n, bool record = false, List<(DetectedEvent, InteractionSite[])>? keep = null)
    {
        using var source = new ListModeSource(c, record);
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int got = 0;
        while (got < n)
        {
            if (source.Advance() is not { } e) continue;
            w.Write(e.PixelX); w.Write(e.PixelY); w.Write(e.DepositKeV); w.Write(e.ArrivalTimeS);
            keep?.Add((e, source.LastInteractions.ToArray()));
            got++;
        }
        w.Write(source.HistoriesEmitted); w.Write(source.HistoriesDetected); w.Write(source.DetectedWeight);
        w.Flush();
        return Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
    }

    [Fact]
    public void DefaultListMode_MatchesPreReadoutFixtures()
    {
        Assert.Equal(LabEvents, Events(Load("scenario.json"), 2000));
        Assert.Equal(Co60Events, Events(Load("scenario_co60.json"), 1000));
        Assert.Equal(RealismEvents, Events(Realism(), 1000));
    }

    [Fact]
    public void InteractionRecording_LeavesEveryStreamIdentical()
    {
        Assert.Equal(LabEvents, Events(Load("scenario.json"), 2000, record: true));
        Assert.Equal(Co60Events, Events(Load("scenario_co60.json"), 1000, record: true));
        Assert.Equal(RealismEvents, Events(Realism(), 1000, record: true));
    }

    [Fact]
    public void ExplicitDirectCrystal_IsTheDefault_AndNullIsNotSerialised()
    {
        var c = Load("scenario.json");
        Assert.Equal(LabJson, Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(c))).ToLowerInvariant());
        Assert.DoesNotContain("Readout", JsonSerializer.Serialize(c));
        c.Detector.Readout = new ReadoutConfig { Mode = ReadoutMode.DirectCrystal };
        Assert.Equal(LabEvents, Events(c, 2000));
        Assert.Equal(ReadoutMode.DirectCrystal, c.Clone().Detector.Readout!.Mode);
        Assert.Throws<ArgumentException>(() => new ReadoutDevice(c.Detector, c.Detector.Readout, 1));
    }

    [Fact]
    public void ComptonRunnerImage_MatchesPreReadoutFixture()
    {
        var c = Load("scenario.json");
        c.Detector.OpticalCrosstalkFraction = 0.4;
        c.PhotonCount = 50_000;
        var image = new SimulationRunner(new ComptonFactory(ComptonStrategy.Argmax, 661.7, 0.15)).Run(c).DetectorImage;
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++) w.Write(image[x, y]);
        w.Flush();
        Assert.Equal(CrosstalkImage, Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant());
    }

    /// <summary>The recorded sites carry the whole deposit (the event's DepositKeV, summed in another order: round-off only,
    /// ≤ n·ε·E with n ≤ 32 sites, so 1e-12·E is a safe bound), lie inside the crystal slab, carry the pitch-cell index of
    /// their own XY, and — without the legacy light spread — their arg-max crystal is the event's pixel.</summary>
    [Fact]
    public void RecordedSites_ConserveTheDeposit_AndReproduceTheDirectPixel()
    {
        var c = Load("scenario.json");
        var keep = new List<(DetectedEvent, InteractionSite[])>();
        Events(c, 3000, record: true, keep: keep);
        var d = c.Detector;
        double half = d.PixelsX * d.PixelPitchMm / 2;
        int multi = 0;
        foreach (var (e, sites) in keep)
        {
            Assert.NotEmpty(sites);
            Assert.True(Math.Abs(sites.Sum(s => s.EnergyKeV) - e.DepositKeV) <= 1e-12 * e.DepositKeV);
            foreach (var s in sites)
            {
                Assert.InRange(s.ZMm, -d.CrystalThicknessMm, 0.0);
                Assert.Equal((int)((s.XMm + half) / d.PixelPitchMm), s.CrystalX);
                Assert.Equal((int)((s.YMm + half) / d.PixelPitchMm), s.CrystalY);
                Assert.True(s.TimeNs >= 0 && s.TimeNs < 1.0);                  // < 1 ns: a few cm of flight at c
            }
            Assert.Equal(e.PixelY * d.PixelsX + e.PixelX, ReadoutDevice.DirectCrystal(sites, d.PixelsX));
            if (sites.Select(s => (s.CrystalX, s.CrystalY)).Distinct().Count() > 1) multi++;
        }
        Assert.True(multi > 0, "the sample must contain multi-crystal histories");
    }

    /// <summary>Stage 1 simulates a physical readout in the study only: the direct paths refuse it instead of silently
    /// ignoring it.</summary>
    [Fact]
    public void PhysicalReadoutOnADirectPath_IsRefused()
    {
        var c = Load("scenario.json");
        c.Detector.Readout = new ReadoutConfig { Mode = ReadoutMode.FourOutputAnger };
        Assert.Throws<NotSupportedException>(() => new ListModeSource(c));
        Assert.Throws<NotSupportedException>(() => new SimulationRunner(new DefaultSimulationFactory()).Run(c));
        Assert.Throws<NotSupportedException>(() => new ComptonFactory(ComptonStrategy.Argmax, 661.7, 0.15).CreateDetector(c));
    }

    [Fact]
    public void Recording_IsRefusedWithBackgroundOrAmbient()
    {
        var c = Load("scenario.json");
        c.Background = new BackgroundConfig { BackgroundToSignalRatio = 1 };
        Assert.Throws<NotSupportedException>(() => new ListModeSource(c, recordInteractions: true));
    }
}
