using System.Security.Cryptography;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

public sealed class TerrestrialSpectrumTests
{
    private const string FileName = "ambient/terrestrial-unscear2000-v1-NOT-VALIDATED.json";

    private static (IncidentSpectrum Spectrum, byte[] Bytes) Committed()
    {
        byte[] bytes = File.ReadAllBytes(RepoPaths.Sample(FileName));
        return (JsonSerializer.Deserialize<IncidentSpectrum>(bytes)!, bytes);
    }

    [Fact]
    public void CommittedSpectrum_HashesVersionAndValidationStateHold()
    {
        var (spectrum, bytes) = Committed();
        string fileHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(File.ReadAllText(RepoPaths.Sample(FileName + ".sha256")).Trim(), fileHash);
        Assert.Equal(spectrum.ComputeContentHash(), spectrum.ContentHash);
        Assert.Equal("terrestrial-unscear2000-v1-NOT-VALIDATED", spectrum.Id);
        Assert.Equal(1, spectrum.Version);
        Assert.False(spectrum.IsValidated);  // AB-4d: not validated until the author accepts the ratios
        Assert.Equal("EnergyZenithTable", spectrum.AngularModel);
        // AB-4d record travels with the spectrum: the Bi-214 beta transitions without intensity and the AB-4c bounds.
        Assert.Contains(spectrum.NotIncluded!, n => n.Nuclide == "214bi" && n.EnergyKeV == 36.8);
        Assert.Contains(spectrum.NotIncluded!, n => n.Reason.StartsWith("AB-4c record", StringComparison.Ordinal));
        // Tampering with any weight breaks the content hash.
        spectrum.Lines[0].FluenceWeight *= 1.000001;
        Assert.NotEqual(spectrum.ContentHash, spectrum.ComputeContentHash());
    }

    [Fact]
    public void CommittedSpectrum_LoadsIntoTheEngineButNotAsEvidence()
    {
        // The engine's own validation (angular marginals within 64 ulps, non-overlapping bins inside the ICRP 74 range,
        // positive fluence) must accept the file; evidence recipes that require a validated spectrum must reject it.
        var (spectrum, _) = Committed();
        var config = Rigs.Lab(seed: 5201);
        config.Ambient = new() { DoseRateMicroSvPerHour = .1, Spectrum = spectrum };
        var process = new AmbientPhotonProcess(config);
        Assert.True(process.IncidentRateCps > 0);
        config.Ambient.RequireValidatedSpectrum = true;
        Assert.Throws<InvalidOperationException>(() => new AmbientPhotonProcess(config));
    }

    [Fact]
    public void CommittedSpectrum_TableReproducesEveryMarginalAndUncollidedLinesPointUpward()
    {
        var (spectrum, _) = Committed();
        var table = spectrum.EnergyZenith!;
        int components = spectrum.Lines.Length + spectrum.Continuum.Length;
        for (int i = 0; i < components; i++)
        {
            double marginal = i < spectrum.Lines.Length ? spectrum.Lines[i].FluenceWeight : spectrum.Continuum[i - spectrum.Lines.Length].FluenceWeight;
            double rows = table.Where(r => r.EnergyIndex == i).OrderBy(r => r.LowCosine).Sum(r => r.FluenceWeight);
            Assert.Equal(marginal, rows); // built as this exact ordered sum
        }
        // Every catalog line row (all but the MC 511-keV annihilation line) is uncollided from the ground: μ ≥ 0.
        var annihilation = spectrum.Lines.Select((l, i) => (l, i)).Where(p => p.l.EnergyKeV == KleinNishina.ElectronRestEnergyKeV).Select(p => p.i).ToHashSet();
        Assert.All(table.Where(r => r.EnergyIndex < spectrum.Lines.Length && !annihilation.Contains(r.EnergyIndex)),
            r => Assert.True(r.LowCosine >= 0));
        Assert.All(spectrum.Lines, l => Assert.InRange(l.EnergyKeV, TerrestrialSpectrumBuilder.FileMinKeV, 10000));
    }
}
