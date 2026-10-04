using System.Security.Cryptography;
using System.Text.Json;

namespace Gcam.Simulation;

/// <summary>Soil and dry air for the AB-4 generator: XCOM partial coefficients, NIST air μ_en/ρ and the K edges
/// of each material's elements, read from samples/ambient/materials-v1.json (built by build_materials.py).</summary>
public sealed class SoilAirMaterials
{
    private readonly double[] _muEnKeV, _muEn;

    public string Id { get; }
    public PhotonMaterial Soil { get; }
    public PhotonMaterial Air { get; }
    /// <summary>K-shell edge energies (keV) of the elements present, per material; used only by the fluorescence bound.</summary>
    public IReadOnlyList<double> SoilKEdgesKeV { get; }
    public IReadOnlyList<double> AirKEdgesKeV { get; }
    public string FileSha256 { get; }

    public SoilAirMaterials(string id, PhotonMaterial soil, PhotonMaterial air, double[] muEnKeV, double[] airMuEnCm2PerG,
        IReadOnlyList<double> soilKEdgesKeV, IReadOnlyList<double> airKEdgesKeV, string fileSha256 = "")
    {
        ArgumentNullException.ThrowIfNull(soil); ArgumentNullException.ThrowIfNull(air);
        ArgumentNullException.ThrowIfNull(muEnKeV); ArgumentNullException.ThrowIfNull(airMuEnCm2PerG);
        if (muEnKeV.Length < 2 || muEnKeV.Length != airMuEnCm2PerG.Length) throw new ArgumentException("μ_en table needs matching arrays.");
        for (int i = 0; i < muEnKeV.Length; i++)
            if (!(muEnKeV[i] > 0) || !double.IsFinite(muEnKeV[i]) || i > 0 && muEnKeV[i] < muEnKeV[i - 1]
                || !(airMuEnCm2PerG[i] > 0) || !double.IsFinite(airMuEnCm2PerG[i]))
                throw new ArgumentException("μ_en table must be finite, positive and nondecreasing in energy.");
        Id = id; Soil = soil; Air = air; FileSha256 = fileSha256;
        _muEnKeV = (double[])muEnKeV.Clone(); _muEn = (double[])airMuEnCm2PerG.Clone();
        SoilKEdgesKeV = soilKEdgesKeV.ToArray(); AirKEdgesKeV = airKEdgesKeV.ToArray();
    }

    /// <summary>Air μ_en/ρ, cm²/g, log-log linear in the NIST table. At a tabulated absorption edge the table lists the
    /// energy twice (below and above); an energy exactly at the edge takes the value above it.</summary>
    public double AirMuEnCm2PerG(double energyKeV)
    {
        if (!(energyKeV >= _muEnKeV[0] && energyKeV <= _muEnKeV[^1])) throw new ArgumentOutOfRangeException(nameof(energyKeV));
        int hi = 1;
        while (hi < _muEnKeV.Length - 1 && _muEnKeV[hi] < energyKeV) hi++;
        while (hi < _muEnKeV.Length - 1 && _muEnKeV[hi] == _muEnKeV[hi + 1] && energyKeV >= _muEnKeV[hi]) hi++;
        int lo = hi - 1;
        if (_muEnKeV[hi] == _muEnKeV[lo]) return _muEn[hi];
        double t = Math.Log(energyKeV / _muEnKeV[lo]) / Math.Log(_muEnKeV[hi] / _muEnKeV[lo]);
        return Math.Exp(Math.Log(_muEn[lo]) + t * Math.Log(_muEn[hi] / _muEn[lo]));
    }

    /// <summary>Load and validate; a nonempty <paramref name="expectedSha256"/> pins the exact file bytes.</summary>
    public static SoilAirMaterials Load(string path, string? expectedSha256 = null) => Parse(File.ReadAllBytes(path), expectedSha256);

    /// <summary>Parse material-file bytes; a nonempty <paramref name="expectedSha256"/> pins them.</summary>
    public static SoilAirMaterials Parse(byte[] bytes, string? expectedSha256 = null)
    {
        string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.IsNullOrEmpty(expectedSha256) && !string.Equals(sha, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Material file hash {sha} differs from the pinned {expectedSha256}.");
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.GetProperty("SchemaVersion").GetInt32() != 1) throw new InvalidDataException("Unsupported material schema.");
        double[] grid = Doubles(root.GetProperty("EnergyKeV"));
        PhotonMaterial Material(string name)
        {
            var m = root.GetProperty(name);
            return new PhotonMaterial(name, m.GetProperty("DensityGPerCm3").GetDouble(), grid,
                Doubles(m.GetProperty("CoherentCm2PerG")), Doubles(m.GetProperty("IncoherentCm2PerG")),
                Doubles(m.GetProperty("PhotoelectricCm2PerG")), Doubles(m.GetProperty("PairNuclearCm2PerG")),
                Doubles(m.GetProperty("PairElectronCm2PerG")));
        }
        double[] Edges(string name) => root.GetProperty("KEdges").GetProperty(name).EnumerateArray()
            .Select(e => e.GetProperty("EdgeKeV").GetDouble()).ToArray();
        return new SoilAirMaterials(root.GetProperty("Id").GetString() ?? "", Material("Soil"), Material("Air"),
            Doubles(root.GetProperty("AirMuEnKeV")), Doubles(root.GetProperty("AirMuEnCm2PerG")), Edges("Soil"), Edges("Air"), sha);
    }

    private static double[] Doubles(JsonElement array) => array.EnumerateArray().Select(e => e.GetDouble()).ToArray();
}
