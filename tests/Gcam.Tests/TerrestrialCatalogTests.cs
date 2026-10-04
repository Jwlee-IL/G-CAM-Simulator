using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

public sealed class TerrestrialCatalogTests
{
    private static string CatalogPath => RepoPaths.Sample("ambient/terrestrial-source-catalog-v1.json");

    private static string PinnedSha256()
    {
        using var request = JsonDocument.Parse(File.ReadAllText(RepoPaths.Sample("ambient/terrestrial-generator-v1.json")));
        return request.RootElement.GetProperty("CatalogSha256").GetString()!;
    }

    [Fact]
    public void Load_PinnedCatalogFollowsAb4d()
    {
        var catalog = TerrestrialCatalog.Load(CatalogPath, PinnedSha256());
        Assert.Equal(new[] { "K-40", "U-238 series", "Th-232 series" }, catalog.Chains.Select(c => c.Name));
        Assert.Equal(420, catalog.ActivityBqPerKg("K-40"));
        Assert.Equal(33, catalog.ActivityBqPerKg("U-238 series"));
        Assert.Equal(45, catalog.ActivityBqPerKg("Th-232 series"));
        var u = catalog.Chains[1];
        // AB-4d: the Bi-214 beta transitions without photon intensity are listed, never given a line.
        foreach (double e in new[] { 36.8, 61.0, 71.1, 104.4 })
        {
            Assert.Contains(u.NotIncluded, n => n.Nuclide == "214bi" && n.EnergyKeV == e);
            Assert.DoesNotContain(u.Lines, l => l.Nuclide == "214bi" && l.EnergyKeV == e);
        }
        Assert.Contains(u.NotIncluded, n => n.Nuclide == "218po" && n.EnergyKeV is null); // no evaluated data at all
        Assert.Equal(4, u.Ab4cOmissionsOnRecord.Length);                                 // turn-5 records kept
        // Secular-equilibrium normalisation: Bi-214 609.321 keV keeps its absolute intensity × the Po-218 alpha path.
        var line609 = u.Lines.Single(l => l.Nuclide == "214bi" && l.EnergyKeV == 609.321);
        Assert.InRange(line609.YieldPerChainDecay / (line609.IntensityPercentPerParentDecay / 100), .9999, 1.0);
        // Th-208 is fed only through the Bi-212 alpha branch (35.94 %).
        var tl2614 = catalog.Chains[2].Lines.Single(l => l.Nuclide == "208tl" && l.EnergyKeV == 2614.511);
        Assert.Equal(.3594 * .99754, tl2614.YieldPerChainDecay, 12);
    }

    [Fact]
    public void Load_RejectsWrongHashAndMalformedCatalogs()
    {
        Assert.Throws<InvalidDataException>(() => TerrestrialCatalog.Load(CatalogPath, new string('f', 64)));
        var root = JsonNode.Parse(File.ReadAllText(CatalogPath))!;
        void Rejects(Action<JsonNode> mutate)
        {
            var copy = root.DeepClone();
            mutate(copy);
            Assert.ThrowsAny<Exception>(() => TerrestrialCatalog.Parse(System.Text.Encoding.UTF8.GetBytes(copy.ToJsonString())));
        }
        Rejects(c => c["Chains"]![0]!["Lines"]![0]!["YieldPerChainDecay"] = -0.1);
        Rejects(c => c["Chains"]![0]!["Lines"]![0]!["YieldPerChainDecay"] = 0.0);
        Rejects(c => c["Chains"]![1]!["Lines"]![5]!["EnergyKeV"] = 0.0);
        Rejects(c => c["Chains"]![1]!["Lines"]![7]!["YieldPerChainDecay"] = c["Chains"]![1]!["Lines"]![7]!["YieldPerChainDecay"]!.GetValue<double>() * 1.001);
        Rejects(c => c["Chains"]![2]!["NotIncluded"]![0]!["Reason"] = "");
        Rejects(c => c["Chains"] = new JsonArray());
        Rejects(c => c["SchemaVersion"] = 2);
        Rejects(c => c["Activities"]!["BqPerKg"]!.AsObject().Remove("K-40"));
        Rejects(c => c["Chains"]![1]!["Name"] = "K-40");
    }

    [Fact]
    public void Snapshot_ManifestHashesMatchFiles()
    {
        // Immutable evaluated-data snapshots: every committed file must still hash to its manifest entry.
        string folder = RepoPaths.Sample("ambient/source-data/v1");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "fetch-manifest.json")));
        foreach (var entry in manifest.RootElement.EnumerateArray())
        {
            string actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder, entry.GetProperty("File").GetString()!)))).ToLowerInvariant();
            Assert.Equal(entry.GetProperty("Sha256").GetString(), actual);
        }
        using var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "materials-sources.json")));
        foreach (var entry in sources.RootElement.GetProperty("Files").EnumerateArray())
        {
            string actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder, entry.GetProperty("File").GetString()!)))).ToLowerInvariant();
            Assert.Equal(entry.GetProperty("Sha256").GetString(), actual);
        }
    }
}
