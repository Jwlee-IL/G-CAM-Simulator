using System.Security.Cryptography;
using System.Text.Json;

namespace Gcam.Simulation;

/// <summary>The AB-4 / AB-4d terrestrial source catalog built by samples/ambient/build_catalog.py from evaluated
/// decay data: evaluated photon lines with absolute intensities only, plus the not-included record.</summary>
public sealed class TerrestrialCatalog
{
    public int SchemaVersion { get; set; }
    public string Id { get; set; } = "";
    public int Version { get; set; }
    public TerrestrialChain[] Chains { get; set; } = [];
    public JsonElement Activities { get; set; }

    /// <summary>UNSCEAR activity of a chain in Bq/kg, as written in the catalog.</summary>
    public double ActivityBqPerKg(string chain) => Activities.GetProperty("BqPerKg").GetProperty(chain).GetDouble();

    /// <summary>Load, pin the file bytes when <paramref name="expectedSha256"/> is given, and validate.</summary>
    public static TerrestrialCatalog Load(string path, string? expectedSha256 = null) => Parse(File.ReadAllBytes(path), expectedSha256);

    /// <summary>Parse catalog bytes; a nonempty <paramref name="expectedSha256"/> pins them.</summary>
    public static TerrestrialCatalog Parse(byte[] bytes, string? expectedSha256 = null)
    {
        string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.IsNullOrEmpty(expectedSha256) && !string.Equals(sha, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Catalog hash {sha} differs from the pinned {expectedSha256}.");
        var catalog = JsonSerializer.Deserialize<TerrestrialCatalog>(bytes) ?? throw new InvalidDataException("Empty catalog.");
        catalog.Validate();
        return catalog;
    }

    /// <summary>Reject a malformed catalog: every line must be finite and positive, each chain's stated totals must
    /// equal the sums of its lines (identical summation order, so only rounding of the last bit is allowed), and every
    /// not-included entry must carry a reason.</summary>
    public void Validate()
    {
        if (SchemaVersion != 1 || string.IsNullOrWhiteSpace(Id) || Version < 1 || Chains is not { Length: > 0 })
            throw new InvalidDataException("Catalog requires schema 1, an id, a positive version and at least one chain.");
        if (Chains.Select(c => c.Name).Distinct().Count() != Chains.Length) throw new InvalidDataException("Duplicate chain names.");
        foreach (var chain in Chains)
        {
            if (string.IsNullOrWhiteSpace(chain.Name) || chain.Lines is not { Length: > 0 } || chain.NotIncluded is null)
                throw new InvalidDataException($"Chain '{chain.Name}' requires a name and lines.");
            foreach (var line in chain.Lines)
                if (line is null || !(line.EnergyKeV > 0) || !double.IsFinite(line.EnergyKeV)
                    || !(line.YieldPerChainDecay > 0) || !double.IsFinite(line.YieldPerChainDecay) || string.IsNullOrWhiteSpace(line.Nuclide))
                    throw new InvalidDataException($"Chain '{chain.Name}': every line needs a nuclide, finite positive energy and yield.");
            double photons = 0, energy = 0;
            foreach (var line in chain.Lines) { photons += line.YieldPerChainDecay; energy += line.YieldPerChainDecay * line.EnergyKeV; }
            // Same order of summation as the catalog builder: a few ulps cover cross-language rounding of the products.
            if (Math.Abs(photons - chain.PhotonsPerDecay) > 1e-12 * photons || Math.Abs(energy - chain.PhotonEnergyKeVPerDecay) > 1e-12 * energy)
                throw new InvalidDataException($"Chain '{chain.Name}': stated totals differ from the sum of its lines.");
            if (chain.NotIncluded.Any(n => n is null || string.IsNullOrWhiteSpace(n.Reason) || string.IsNullOrWhiteSpace(n.Nuclide)))
                throw new InvalidDataException($"Chain '{chain.Name}': a not-included entry lacks a nuclide or reason.");
            if (!(ActivityBqPerKg(chain.Name) >= 0)) throw new InvalidDataException($"Chain '{chain.Name}': activity must be nonnegative.");
        }
    }
}
