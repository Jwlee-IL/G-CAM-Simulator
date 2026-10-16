using System.Text.Json;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>
/// The observation scope of an observed run as the UI-automation procedure v3.3 defines it (TODO-52): every write
/// surface's scope entries (declared default, resolved value, isolation location; <c>surface-list.json</c>), the shell
/// record locations (<c>classification-rules.json</c>), the run's harness-owned locations and the registered images.
/// A change outside it that carries no harness marker is unattributed, not a candidate. <c>stage0.py diff-classify</c>
/// resolves the same entries against the snapshots; this copy is what the run manifest's <c>writeAudit</c> records
/// before classification.
/// </summary>
public static class ObservationScope
{
    public sealed record Location(string Id, string Surface, string Channel, string Where, string Basis, string Source);

    public const string ControlWindowRule = "changed-in-null-window";

    public static IReadOnlyList<Location> Describe(string isolationFolder, IReadOnlyList<string> harnessFiles,
        IReadOnlyList<string> harnessKeys, IReadOnlyList<string> registeredImages)
    {
        var locations = new List<Location>();
        locations.AddRange(harnessFiles.Select(f => new Location("harness-owned", "harness", "file", f, "harness-owned (owner marker)", "run")));
        locations.AddRange(harnessKeys.Select(k => new Location("harness-owned", "harness", "registry", k, "harness-owned (registry marker)", "run")));
        using (var surfaces = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(isolationFolder, "surface-list.json"))))
            foreach (var surface in surfaces.RootElement.GetProperty("surfaces").EnumerateArray())
            {
                string id = surface.GetProperty("id").GetString()!;
                if (!surface.TryGetProperty("scope", out var scope)) continue;
                foreach (var entry in scope.EnumerateArray())
                    locations.Add(new(id, id, entry.GetProperty("channel").GetString()!, Where(entry), entry.GetProperty("basis").GetString()!,
                        "surface-list.json#" + id));
            }
        using (var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(isolationFolder, "classification-rules.json"))))
            foreach (var entry in rules.RootElement.GetProperty("shellRecordLocations").EnumerateArray())
            {
                if (entry.TryGetProperty("blindSpot", out _)) continue;
                string id = entry.GetProperty("id").GetString()!;
                string surface = entry.GetProperty("surface").GetString()!.Split('#')[^1];
                locations.Add(new(id, surface, entry.GetProperty("channel").GetString()!, Where(entry), "shell record location",
                    "classification-rules.json#" + id));
            }
        locations.AddRange(registeredImages.Select(i => new Location("owned-processes", "harness", "process", i,
            "registered image and its descendants (process ledger)", "run")));
        return locations;
    }

    /// <summary>The surfaces and shell record locations without a scope of their own, each with its residual risk (D2).</summary>
    public static IReadOnlyList<string> BlindSpots(string isolationFolder)
    {
        var spots = new List<string>();
        using (var surfaces = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(isolationFolder, "surface-list.json"))))
            foreach (var surface in surfaces.RootElement.GetProperty("surfaces").EnumerateArray())
                if (surface.GetProperty("status").GetString() == "blind-spot")
                    spots.Add(surface.GetProperty("id").GetString() + ": " + surface.GetProperty("residualRisk").GetString());
        using (var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(isolationFolder, "classification-rules.json"))))
            foreach (var entry in rules.RootElement.GetProperty("shellRecordLocations").EnumerateArray())
                if (entry.TryGetProperty("blindSpot", out var spot))
                    spots.Add("shell record " + entry.GetProperty("id").GetString() + ": " + spot.GetString());
        return spots;
    }

    /// <summary>What the manifest holds before <c>stage0.py diff-classify</c> has counted: the list location and the rule.</summary>
    public static object PendingUnattributed() => new
    {
        count = (int?)null, list = (string?)null,
        status = "counted by stage0.py diff-classify; the list stays local beside its result (<out>.unattributed.jsonl, A4)",
        definition = "changes outside the observation scope with no harness marker and no owned process: not candidates, never blocking",
    };

    public static object ControlWindow(TimeSpan nullWindow, TimeSpan appWindow) => new
    {
        rule = ControlWindowRule, aToBSeconds = nullWindow.TotalSeconds, appWindowSeconds = appWindow.TotalSeconds,
        shorterThanAppWindow = nullWindow < appWindow, classifiedByRule = (int?)null,
        status = "items classified by the control-window rule are counted by stage0.py diff-classify",
    };

    private static string Where(JsonElement entry)
    {
        if (entry.GetProperty("channel").GetString() == "registry")
            return "HKCU\\" + entry.GetProperty("keyPattern").GetString()
                + (entry.TryGetProperty("valueNamePattern", out var names) ? " / " + names.GetString() : "");
        if (entry.TryGetProperty("location", out var location)) return "owned " + location.GetString();
        string root = "<" + entry.GetProperty("root").GetString() + ">\\";
        return root + (entry.TryGetProperty("relativePath", out var path) ? path.GetString() : entry.GetProperty("relativePattern").GetString());
    }
}
