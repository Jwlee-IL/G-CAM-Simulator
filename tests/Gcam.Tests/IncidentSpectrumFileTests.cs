using System.Text;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

public sealed class IncidentSpectrumFileTests
{
    private static string Ambient(string name) => RepoPaths.Sample(Path.Combine("ambient", name));

    [Fact]
    public void PinnedAmbientFiles_HashTheBytesGitStores()
    {
        // .gitattributes stores *.json with LF line ends. A sidecar taken over CRLF bytes (a Windows writer) matches the
        // writer's working copy but not any checkout — the turn-4 placeholder sidecar did exactly that. So a pinned file
        // must contain no CR byte, and its sidecar must be the hash of these (LF) bytes.
        var pinned = Directory.GetFiles(RepoPaths.Sample("ambient"), "*.json").Where(f => File.Exists(f + ".sha256")).ToArray();
        Assert.Contains(pinned, f => f.EndsWith("development-mono662-v1-NOT-VALIDATED.json", StringComparison.Ordinal));
        Assert.Contains(pinned, f => f.EndsWith("terrestrial-unscear2000-v1.json", StringComparison.Ordinal));
        foreach (string file in pinned)
        {
            byte[] bytes = File.ReadAllBytes(file);
            Assert.False(bytes.Contains((byte)'\r'), $"{Path.GetFileName(file)} contains CR bytes");
            Assert.Equal(File.ReadAllText(file + ".sha256").Trim(), IncidentSpectrumFile.Sha256Hex(bytes));
        }
    }

    [Fact]
    public void DevelopmentPlaceholder_RegeneratesTheCommittedBytes()
    {
        byte[] committed = File.ReadAllBytes(Ambient("development-mono662-v1-NOT-VALIDATED.json"));
        byte[] generated = new UTF8Encoding(false).GetBytes(IncidentSpectrumFile.SerializeIndented(IncidentSpectrum.Placeholder()));
        Assert.Equal(committed, generated);
        Assert.Equal("10dccd592958db9b911e026c1e43c02d95872285d23d53d6715d318e68d8222b", IncidentSpectrumFile.Sha256Hex(generated));
    }

    [Fact]
    public void WriteWithSidecar_RefusesCarriageReturnsAndOverwrites()
    {
        string dir = Directory.CreateTempSubdirectory("gcam-spectrumfile-").FullName;
        string path = Path.Combine(dir, "x.json");
        Assert.Throws<ArgumentException>(() => IncidentSpectrumFile.WriteWithSidecar(path, "{\r\n}"));
        string hash = IncidentSpectrumFile.WriteWithSidecar(path, "{\n}");
        Assert.Equal(hash, File.ReadAllText(path + ".sha256").Trim());
        Assert.Throws<IOException>(() => IncidentSpectrumFile.WriteWithSidecar(path, "{\n}"));
    }

    [Fact]
    public void SpectrumReference_ResolvesOnlyWithItsPinnedHash()
    {
        string dir = Directory.CreateTempSubdirectory("gcam-spectrumref-").FullName;
        string spectrum = Ambient("development-mono662-v1-NOT-VALIDATED.json");
        string hash = File.ReadAllText(spectrum + ".sha256").Trim();
        string Write(string sha)
        {
            string path = Path.Combine(dir, $"scenario-{sha[..6]}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                Seed = 1,
                Ambient = new { DoseRateMicroSvPerHour = .1, SpectrumFile = spectrum, SpectrumFileSha256 = sha }
            }));
            return path;
        }
        var config = ConfigLoader.Load(Write(hash));
        Assert.Null(config.Ambient!.SpectrumFile);
        Assert.Equal("development-mono662-v1-NOT-VALIDATED", config.Ambient.Spectrum.Id);
        Assert.Throws<InvalidDataException>(() => ConfigLoader.Load(Write(new string('0', 64))));
        // Built in code without the loader: the engine refuses the unresolved reference instead of the placeholder default.
        var unresolved = new SimulationConfig { Ambient = new() { DoseRateMicroSvPerHour = .1, SpectrumFile = spectrum, SpectrumFileSha256 = hash } };
        Assert.Throws<InvalidOperationException>(() => new AmbientPhotonProcess(unresolved));
    }
}
