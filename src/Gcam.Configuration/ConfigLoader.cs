using System.Text.Json;

namespace Gcam.Configuration;

/// <summary>Loads/saves <see cref="SimulationConfig"/> as JSON (System.Text.Json).</summary>
public static class ConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public static SimulationConfig Load(string path)
    {
        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<SimulationConfig>(json, Options)
               ?? throw new InvalidDataException($"Failed to parse config: {path}");
        ResolveAmbientSpectrum(config, Path.GetDirectoryName(Path.GetFullPath(path))!);
        return config;
    }

    /// <summary>Replace an ambient spectrum reference (<see cref="AmbientFieldConfig.SpectrumFile"/>) by the hash-checked
    /// spectrum it names; relative paths are taken from <paramref name="baseDirectory"/>. No reference: no change.</summary>
    public static void ResolveAmbientSpectrum(SimulationConfig config, string baseDirectory)
    {
        if (config.Ambient is not { SpectrumFile: { } file } field) return;
        string full = Path.IsPathRooted(file) ? file : Path.Combine(baseDirectory, file);
        field.Spectrum = IncidentSpectrumFile.Load(full, field.SpectrumFileSha256 ?? "");
        field.SpectrumFile = null;
    }

    public static void Save(SimulationConfig config, string path)
        => File.WriteAllText(path, JsonSerializer.Serialize(config, Options));
}
