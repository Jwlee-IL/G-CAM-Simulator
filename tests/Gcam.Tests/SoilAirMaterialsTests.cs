using System.Text.Json;
using System.Text.Json.Nodes;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

public sealed class SoilAirMaterialsTests
{
    private static string MaterialsPath => RepoPaths.Sample("ambient/materials-v1.json");

    [Fact]
    public void Load_ComposedAirReproducesNistTotalWithinTableRounding()
    {
        // build_materials.py records XCOM-composed air μ/ρ next to NIST's tabulated value at shared grid energies.
        // NIST prints 4 significant figures, so a faithful composition differs by at most half a unit in the 4th
        // figure: 5e-4 relative. Read the check back and confirm the C# interpolation returns the grid values exactly.
        var m = SoilAirMaterials.Load(MaterialsPath);
        using var doc = JsonDocument.Parse(File.ReadAllBytes(MaterialsPath));
        foreach (var row in doc.RootElement.GetProperty("AirTotalCheck").EnumerateArray())
        {
            double e = row.GetProperty("EnergyKeV").GetDouble();
            Assert.InRange(row.GetProperty("Ratio").GetDouble(), 1 - 5e-4, 1 + 5e-4);
            Assert.Equal(row.GetProperty("ComposedMuCm2PerG").GetDouble(), m.Air.At(e).Total, 12);
        }
        Assert.Equal(1.6, m.Soil.DensityGPerCm3);
        Assert.Equal(1.20479e-3, m.Air.DensityGPerCm3);
        Assert.Contains(m.SoilKEdgesKeV, e => Math.Abs(e - 7.112) < 1e-9); // Fe K edge from the XCOM file
    }

    [Fact]
    public void AirMuEn_ReturnsTabulatedValuesAndTakesTheValueAboveAnEdge()
    {
        var m = SoilAirMaterials.Load(MaterialsPath);
        Assert.Equal(2.789e-2, m.AirMuEnCm2PerG(1000), 15);  // NIST air table, 1 MeV
        Assert.Equal(1.460e2, m.AirMuEnCm2PerG(3.2029), 12); // Ar K edge, above-edge row
        Assert.Throws<ArgumentOutOfRangeException>(() => m.AirMuEnCm2PerG(0.5));
    }

    [Fact]
    public void Load_RejectsTamperedOrMalformedData()
    {
        Assert.Throws<InvalidDataException>(() => SoilAirMaterials.Load(MaterialsPath, new string('0', 64)));
        var root = JsonNode.Parse(File.ReadAllText(MaterialsPath))!;
        static byte[] Write(JsonNode node) => System.Text.Encoding.UTF8.GetBytes(node.ToJsonString());
        var negative = root.DeepClone();
        negative["Soil"]!["IncoherentCm2PerG"]![3] = -1;
        Assert.Throws<ArgumentException>(() => SoilAirMaterials.Parse(Write(negative)));
        var unordered = root.DeepClone();
        unordered["EnergyKeV"]![2] = 0.5;
        Assert.Throws<ArgumentException>(() => SoilAirMaterials.Parse(Write(unordered)));
        var shortened = root.DeepClone();
        shortened["Air"]!["PhotoelectricCm2PerG"]!.AsArray().RemoveAt(0);
        Assert.Throws<ArgumentException>(() => SoilAirMaterials.Parse(Write(shortened)));
        var m = SoilAirMaterials.Load(MaterialsPath);
        Assert.Throws<ArgumentOutOfRangeException>(() => m.Soil.At(0.9));
        Assert.Throws<ArgumentOutOfRangeException>(() => m.Soil.At(25000));
    }
}
