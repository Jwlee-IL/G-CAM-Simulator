using System.Text.Json;
using Gcam.Configuration;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

/// <summary>Configs are the experiment record: every shipped scenario must load into a usable config, and saving
/// or cloning one must not drop a field (a Codex review once found hand-written clones that reverted mask
/// attenuation and crystal material to defaults — AGENTS.md "Cloning configs").</summary>
public class ConfigLoaderTests
{
    public static TheoryData<string> Scenarios()
    {
        var data = new TheoryData<string>();
        foreach (var f in Directory.GetFiles(RepoPaths.Sample(""), "*.json")
                     .Concat(Directory.GetFiles(RepoPaths.Sample("materials"), "*.json")))
            data.Add(Path.GetRelativePath(RepoPaths.Sample(""), f));
        return data;
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void EveryShippedScenario_LoadsIntoAUsableConfig(string file)
    {
        var cfg = ConfigLoader.Load(RepoPaths.Sample(file));

        Assert.True(IsPrime(cfg.Mask.Rank), $"{file}: rank {cfg.Mask.Rank} is not prime");
        Assert.True(cfg.Mask.CellPitchMm > 0 && cfg.Mask.ThicknessMm > 0, $"{file}: mask geometry");
        Assert.True(cfg.Detector.PixelsX > 0 && cfg.Detector.PixelsY > 0 && cfg.Detector.PixelPitchMm > 0, $"{file}: detector");
        Assert.True(cfg.Geometry.MaskDetectorDistanceMm > 0 && cfg.Geometry.SourceMaskDistanceMm > 0, $"{file}: geometry");
        Assert.True(cfg.PhotonCount > 0, $"{file}: photon count");
        foreach (var s in cfg.Sources ?? [cfg.Source])
        {
            Assert.True(s.Position.Length >= 2, $"{file}: source position");
            foreach (var l in s.Lines ?? [])
                Assert.True(l.EnergyKeV > 0 && l.Intensity > 0, $"{file}: emission line {l.EnergyKeV} keV × {l.Intensity}");
        }
    }

    [Fact]
    public void SaveThenLoad_KeepsEverySection()
    {
        var cfg = FullyPopulated();
        string path = Path.Combine(Path.GetTempPath(), $"gcam-roundtrip-{Guid.NewGuid():N}.json");
        try
        {
            ConfigLoader.Save(cfg, path);
            Assert.Equal(Json(cfg), Json(ConfigLoader.Load(path)));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Clone_IsADeepEqualCopy()
    {
        var cfg = FullyPopulated();
        var clone = cfg.Clone();
        Assert.Equal(Json(cfg), Json(clone));

        clone.Mask.LinearAttenuationPerMm = 99.0;
        clone.Sources![0].Lines![0].EnergyKeV = 1.0;
        Assert.Equal(0.178, cfg.Mask.LinearAttenuationPerMm);          // deep: the original is untouched
        Assert.Equal(661.7, cfg.Sources![0].Lines![0].EnergyKeV);
    }

    [Fact]
    public void Loader_AcceptsCommentsTrailingCommasAndAnyCase()
    {
        string path = Path.Combine(Path.GetTempPath(), $"gcam-lenient-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
                {
                  // a comment
                  "PHOTONCOUNT": 1234,
                  "mask": { "rank": 11, },
                }
                """);
            var cfg = ConfigLoader.Load(path);
            Assert.Equal(1234, cfg.PhotonCount);
            Assert.Equal(11, cfg.Mask.Rank);
        }
        finally { File.Delete(path); }
    }

    // Every optional section set to a non-default value, so a dropped field changes the JSON.
    private static SimulationConfig FullyPopulated()
    {
        var cfg = Rigs.Handheld(photons: 777, seed: 4242);
        cfg.Sources =
        [
            new SourceConfig { Isotope = "Cs-137", Position = [1, 2, 900], ActivityBq = 3e5,
                               Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 0.851 }] },
            new SourceConfig { Isotope = "Co-60", Position = [-3, 4, 0], ActivityBq = 1e5,
                               Lines = [new EmissionLine { EnergyKeV = 1173.2, Intensity = 0.999, CascadeCoincident = true }] },
        ];
        cfg.Background = new BackgroundConfig { BackgroundToSignalRatio = 0.5, EnergyKeV = 250, DarkCountRateKcps = 12 };
        cfg.Detector.FrontEnd = new FrontEndConfig();
        cfg.Decoder.Cyclic = false;
        cfg.Decoder.ReconHalfExtentMm = 12.5;
        cfg.Mask.HolePositionJitterMm = 0.02;
        cfg.Mask.MaskRollDeg = 0.3;
        return cfg;
    }

    private static string Json(SimulationConfig c) => JsonSerializer.Serialize(c);

    private static bool IsPrime(int n)
    {
        if (n < 2) return false;
        for (int i = 2; i * i <= n; i++) if (n % i == 0) return false;
        return true;
    }
}
