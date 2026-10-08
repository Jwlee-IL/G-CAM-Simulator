using System.Text.Json;
using Gcam.Configuration;
using Gcam.Simulation;

namespace Gcam.Cli;

/// <summary>TODO-19 readout tools.</summary>
internal static class ReadoutCommands
{
    /// <summary>readout-export &lt;scenario.json&gt; &lt;request.json&gt; &lt;geometry&gt; &lt;readout&gt; &lt;trigger&gt; &lt;out-prefix&gt;:
    /// writes &lt;prefix&gt;.config.json (engine-resolved configuration + DC charge fractions) and, for a solved circuit,
    /// &lt;prefix&gt;.cir (SPICE netlist) — the inputs of samples/readout/schematic.py and netlist_check.py.</summary>
    public static int RunExport(string[] args)
    {
        if (args.Length < 7)
        {
            Console.Error.WriteLine("Usage: montecarlo readout-export <scenario.json> <request.json> <geometry> <readout> <trigger> <out-prefix>");
            return 1;
        }
        var scenario = ConfigLoader.Load(args[1]);
        var request = JsonSerializer.Deserialize<ReadoutStudyRequest>(File.ReadAllBytes(args[2]),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip })!;
        var (description, netlist) = ReadoutExport.Build(scenario, request, args[3], args[4], args[5]);
        string prefix = args[6];
        string? dir = Path.GetDirectoryName(Path.GetFullPath(prefix));
        if (dir is not null) Directory.CreateDirectory(dir);
        string json = description.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n";
        File.WriteAllText(prefix + ".config.json", json);
        Console.WriteLine($"wrote {prefix}.config.json");
        if (netlist is not null)
        {
            File.WriteAllText(prefix + ".cir", netlist);
            Console.WriteLine($"wrote {prefix}.cir");
        }
        return 0;
    }
}
