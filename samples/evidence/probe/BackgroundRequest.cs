using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Gcam.EvidenceProbe;

/// <summary>Version two changes sampling budgets only; the selected physics request and pin remain immutable.</summary>
internal static class BackgroundRequest
{
    public static string PathFor(string root, int version) => Path.Combine(root, $"request-v{version}.json");

    public static void Validate(string root, int version)
    {
        if (version == 1) return;
        if (version != 2) throw new ArgumentOutOfRangeException(nameof(version));
        var request = JsonNode.Parse(File.ReadAllBytes(PathFor(root, version)))!.AsObject();
        var baseline = JsonNode.Parse(File.ReadAllBytes(PathFor(root, 1)))!.AsObject();
        var budget = request["BudgetRevision"]!.AsObject();
        string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        if (budget["BaseRequestSha256"]!.GetValue<string>() != Hash(PathFor(root, 1))
            || budget["PinnedSha256"]!.GetValue<string>() != Hash(Path.Combine(root, "pinned-v1.json"))
            || request["ValidationRepeats"]!.GetValue<int>() != 100
            || request["PilotRepeats"]!.GetValue<int>() != 4
            || budget["Workers"]!.GetValue<int>() != 16)
            throw new InvalidDataException("Version-two sampling budget or immutable references differ from the author decision.");
        request.Remove("BudgetRevision");
        request["ValidationRepeats"] = baseline["ValidationRepeats"]!.DeepClone();
        request["PilotRepeats"] = baseline["PilotRepeats"]!.DeepClone();
        if (!JsonNode.DeepEquals(request, baseline))
            throw new InvalidDataException("Sampling-budget revision changes the selected physics request.");
    }
}
