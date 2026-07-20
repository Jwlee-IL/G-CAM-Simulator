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
        return JsonSerializer.Deserialize<SimulationConfig>(json, Options)
               ?? throw new InvalidDataException($"Failed to parse config: {path}");
    }

    public static void Save(SimulationConfig config, string path)
        => File.WriteAllText(path, JsonSerializer.Serialize(config, Options));
}
