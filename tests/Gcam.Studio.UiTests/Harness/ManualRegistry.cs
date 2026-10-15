using System.Text.Json;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>
/// The manual's chapter registry and notice list (<c>docs/manual/</c>), read by the manual scenarios: chapter numbers
/// and titles come from the registry, never typed in scenario code; a step may only place a notice the list allows
/// in its chapter. The generator (<c>samples/manual/manual_records.py</c>) validates the same files.
/// </summary>
public sealed class ManualRegistry
{
    public sealed record Chapter(string Number, string Title, string Scenario);

    private readonly Dictionary<string, Chapter> _byScenario;
    private readonly Dictionary<string, string[]> _noticeChapters;

    public ManualRegistry(byte[] registry, byte[] notices)
    {
        using var r = JsonDocument.Parse(registry);
        using var n = JsonDocument.Parse(notices);
        if (r.RootElement.GetProperty("schema").GetString() != "gcam.manual-registry" || r.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unknown chapter registry schema");
        if (n.RootElement.GetProperty("schema").GetString() != "gcam.manual-notices" || n.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unknown notice list schema");
        _byScenario = r.RootElement.GetProperty("chapters").EnumerateArray()
            .Where(c => c.GetProperty("source").GetString() == "generated")
            .Select(c => new Chapter(c.GetProperty("number").GetString()!, c.GetProperty("title").GetString()!, c.GetProperty("scenario").GetString()!))
            .ToDictionary(c => c.Scenario, StringComparer.Ordinal);
        _noticeChapters = n.RootElement.GetProperty("notices").EnumerateArray().ToDictionary(
            e => e.GetProperty("id").GetString()!, e => e.GetProperty("chapters").EnumerateArray().Select(c => c.GetString()!).ToArray(), StringComparer.Ordinal);
        RegistrySha256 = IsolationPaths.Hash(registry);
        NoticesSha256 = IsolationPaths.Hash(notices);
    }

    public string RegistrySha256 { get; }
    public string NoticesSha256 { get; }
    public IReadOnlyCollection<string> Scenarios => _byScenario.Keys;

    public static ManualRegistry Load()
    {
        string root = Path.Combine(RepoPaths.Root(), "docs", "manual");
        return new(File.ReadAllBytes(Path.Combine(root, "chapters.json")), File.ReadAllBytes(Path.Combine(root, "notices.json")));
    }

    public Chapter ForScenario(string scenario) => _byScenario.TryGetValue(scenario, out var chapter)
        ? chapter : throw new InvalidDataException("No generated chapter for scenario " + scenario);

    /// <summary>A placement the notice list does not allow is a harness error, never a silently added warning.</summary>
    public void RequireNotice(string id, string chapter)
    {
        if (!_noticeChapters.TryGetValue(id, out var chapters)) throw new InvalidDataException("Unknown notice " + id);
        if (!chapters.Contains(chapter)) throw new InvalidDataException($"Notice {id} is not allowed in chapter {chapter}");
    }
}
