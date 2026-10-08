using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gcam.Configuration;
using Gcam.Simulation;

namespace Gcam.EvidenceProbe;

/// <summary>TODO-19 stage 1: the readout comparison (ReadoutStudy) for one outer seed. The phase names the request and
/// the seed list the seed must belong to: pilot and selection use RO_SELECTION16, validation RO_VALIDATION32, the pitch
/// confirmation RO_CONFIRMATION32 with its own restricted request (all disjoint, and distinct from every other list in
/// seeds.json); timing is one selection seed with the selection request. The
/// scenario is samples/scenario.json; only its Seed is replaced (the study derives every stream from it).</summary>
internal static class ReadoutRecipe
{
    public static void Run(string phase, int seed, string samplesDir)
    {
        string root = Path.Combine(samplesDir, "evidence", "readout");
        string requestFile = phase switch
        {
            "pilot" => "request-pilot-v1.json",
            "confirmation" => "request-confirmation-v1.json",
            _ => "request-v1.json",
        };
        string seedList = phase switch
        {
            "validation" => "RO_VALIDATION32",
            "confirmation" => "RO_CONFIRMATION32",
            _ => "RO_SELECTION16",
        };
        using (var seeds = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(samplesDir, "evidence", "seeds.json"))))
            if (!seeds.RootElement.GetProperty(seedList).EnumerateArray().Any(s => s.GetInt32() == seed))
                throw new ArgumentException($"Seed {seed} is not in {seedList}.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(root, requestFile));
        var request = JsonSerializer.Deserialize<ReadoutStudyRequest>(bytes,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip })!;
        var scenario = ConfigLoader.Load(Path.Combine(samplesDir, "scenario.json"));
        scenario.Seed = seed;
        var result = ReadoutStudy.Run(scenario, request, seed);
        var output = new JsonObject
        {
            ["Phase"] = phase,
            ["Request"] = requestFile,
            ["RequestSha256"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            ["SeedList"] = seedList,
            ["Result"] = result,
        };
        Console.WriteLine(output.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
    }
}
