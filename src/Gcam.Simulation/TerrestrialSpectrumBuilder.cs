using System.Text.Json;
using Gcam.Configuration;

namespace Gcam.Simulation;

/// <summary>Assemble the AB-4 incident spectrum file from the catalog and the pooled soil/air tallies, at the catalog's
/// UNSCEAR activities. Lines: uncollided point fluence from the analytic half-space kernel with the transport's own
/// coefficients (the MC validates that kernel, validation (a)); the uncollided 511-keV annihilation line and the
/// scattered continuum: MC means. Weights are photons/(cm² s) at 1 m. Marked NOT-VALIDATED until the author accepts
/// the UNSCEAR comparison (AB-4d).</summary>
public static class TerrestrialSpectrumBuilder
{
    /// <summary>Lower limit of the ICRP 74 H*(10)/Φ table that normalises the field in the engine.</summary>
    public const double FileMinKeV = 10;

    public static IncidentSpectrum Build(TerrestrialCatalog catalog, SoilAirMaterials materials, SoilAirTransportOptions options,
        IReadOnlyDictionary<string, SoilAirTally> pooled, string id, string reference)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(pooled);
        int zb = options.ZenithBins, half = zb / 2;
        double[] edges = options.ContinuumEdgesKeV;
        var lines = new List<IncidentLine>();
        var table = new List<IncidentAngularBin>();
        var notIncluded = new List<IncidentNotIncluded>();
        var annihilation = new double[zb];
        var continuum = new double[(edges.Length - 1) * zb];
        foreach (var chain in catalog.Chains)
        {
            double activity = catalog.ActivityBqPerKg(chain.Name);
            var tally = pooled[chain.Name];
            foreach (var line in chain.Lines)
            {
                if (line.EnergyKeV < FileMinKeV)
                {
                    notIncluded.Add(new() { Chain = chain.Name, Nuclide = line.Nuclide, EnergyKeV = line.EnergyKeV,
                        Reason = $"{line.Kind}: below the 10 keV lower limit of the ICRP 74 table that normalises the field; transported by the generator, not written to the file." });
                    continue;
                }
                var rows = Enumerable.Range(half, half).Select(z => new IncidentAngularBin
                {
                    EnergyIndex = lines.Count, LowCosine = Cosine(z, zb), HighCosine = Cosine(z + 1, zb),
                    FluenceWeight = activity * SoilAirTransport.PointUncollided(line, materials, options, Cosine(z, zb), Cosine(z + 1, zb))
                }).ToList();
                lines.Add(new() { EnergyKeV = line.EnergyKeV, FluenceWeight = rows.OrderBy(r => r.LowCosine).Sum(r => r.FluenceWeight) });
                table.AddRange(rows);
            }
            foreach (var n in chain.NotIncluded)
                notIncluded.Add(new() { Chain = chain.Name, Nuclide = n.Nuclide, EnergyKeV = n.EnergyKeV, Reason = $"{n.Kind}, {n.Decay}: {n.Reason}" });
            foreach (var o in chain.Ab4cOmissionsOnRecord)
                notIncluded.Add(new() { Chain = chain.Name, Nuclide = o.GetProperty("ParentNuclide").GetString() ?? "",
                    Reason = $"AB-4c record (turn 5), {o.GetProperty("DecayBranch").GetString()}: emitted photon energy ≤ "
                        + $"{o.GetProperty("EnergyUpperBoundKeVPerChainDecay").GetDouble():G6} keV per chain decay "
                        + $"(ratio ≤ {o.GetProperty("EnergyRatioUpperBound").GetDouble():G4} of the subset denominator); "
                        + "under AB-4d not included and not certified." });
            for (int z = 0; z < zb; z++) annihilation[z] += activity * tally.AnnihilationSum[z] / tally.Histories;
            for (int i = 0; i < continuum.Length; i++) continuum[i] += activity * tally.ContinuumSum[i] / tally.Histories;
        }
        var aRows = Enumerable.Range(0, zb).Where(z => annihilation[z] > 0).Select(z => new IncidentAngularBin
        {
            EnergyIndex = lines.Count, LowCosine = Cosine(z, zb), HighCosine = Cosine(z + 1, zb), FluenceWeight = annihilation[z]
        }).ToList();
        if (aRows.Count > 0)
        {
            lines.Add(new() { EnergyKeV = KleinNishina.ElectronRestEnergyKeV, FluenceWeight = aRows.OrderBy(r => r.LowCosine).Sum(r => r.FluenceWeight) });
            table.AddRange(aRows);
        }
        var bins = new List<IncidentContinuumBin>();
        for (int e = 0; e < edges.Length - 1; e++)
        {
            if (edges[e] < FileMinKeV) continue;
            var rows = Enumerable.Range(0, zb).Where(z => continuum[e * zb + z] > 0).Select(z => new IncidentAngularBin
            {
                EnergyIndex = -1, LowCosine = Cosine(z, zb), HighCosine = Cosine(z + 1, zb), FluenceWeight = continuum[e * zb + z]
            }).ToList();
            if (rows.Count == 0) continue;
            bins.Add(new() { LowKeV = edges[e], HighKeV = edges[e + 1], FluenceWeight = rows.OrderBy(r => r.LowCosine).Sum(r => r.FluenceWeight) });
            int index = lines.Count + bins.Count - 1;
            foreach (var r in rows) r.EnergyIndex = index;
            table.AddRange(rows);
        }
        var spectrum = new IncidentSpectrum
        {
            Id = id, Version = 1, Reference = reference, AngularModel = "EnergyZenithTable", IsValidated = false,
            Lines = lines.ToArray(), Continuum = bins.ToArray(), EnergyZenith = table.ToArray(), NotIncluded = notIncluded.ToArray()
        };
        spectrum.ContentHash = spectrum.ComputeContentHash();
        return spectrum;
    }

    /// <summary>Lower edge of zenith bin <paramref name="z"/> of <paramref name="bins"/> equal bins over [−1, 1].</summary>
    public static double Cosine(int z, int bins) => -1 + 2.0 * z / bins;

    /// <summary>Serialize compactly (the table has ~10⁴ rows) and return the file text.</summary>
    public static string Serialize(IncidentSpectrum spectrum) => JsonSerializer.Serialize(spectrum);
}
