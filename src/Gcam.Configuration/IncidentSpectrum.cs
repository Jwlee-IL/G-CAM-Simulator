using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gcam.Configuration;

/// <summary>Scalar fluence weights. Continuum weights are integrated over uniform-energy bins.</summary>
public sealed class IncidentSpectrum
{
    public string Id { get; set; } = "custom";
    public int Version { get; set; } = 1;
    public string Reference { get; set; } = "";
    public string AngularModel { get; set; } = "Isotropic";
    public bool IsValidated { get; set; }
    public string ContentHash { get; set; } = "";
    public IncidentLine[] Lines { get; set; } = [];
    public IncidentContinuumBin[] Continuum { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IncidentAngularBin[]? EnergyZenith { get; set; }
    /// <summary>Source-term items deliberately left out (AB-4d), part of the hashed payload when present.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IncidentNotIncluded[]? NotIncluded { get; set; }

    /// <summary>Hash the fixed-order JSON payload, excluding the hash itself. Serialized line/bin order is significant.
    /// Spectra without the optional members keep their earlier payload, so existing hashes stay valid.</summary>
    public string ComputeContentHash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        NotIncluded is not null
            ? JsonSerializer.Serialize(new { Id, Version, Reference, AngularModel, IsValidated, Lines, Continuum, EnergyZenith, NotIncluded })
            : EnergyZenith is null
                ? JsonSerializer.Serialize(new { Id, Version, Reference, AngularModel, IsValidated, Lines, Continuum })
                : JsonSerializer.Serialize(new { Id, Version, Reference, AngularModel, IsValidated, Lines, Continuum, EnergyZenith })))).ToLowerInvariant();

    /// <summary>Development fixture only. It is not a terrestrial spectrum and cannot support ambient evidence.</summary>
    public static IncidentSpectrum Placeholder()
    {
        var spectrum = new IncidentSpectrum
        {
            Id = "development-mono662-v1-NOT-VALIDATED",
            Reference = "Synthetic isotropic monoenergetic fixture; no terrestrial-field claim.",
            Lines = [new() { EnergyKeV = 661.7, FluenceWeight = 1 }]
        };
        spectrum.ContentHash = spectrum.ComputeContentHash();
        return spectrum;
    }
}
