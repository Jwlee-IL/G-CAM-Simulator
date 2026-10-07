using System.Text.Json;

namespace Gcam.Cli;

/// <summary>TODO-36 / MD-2: only the single run honours <c>Decoder.Method</c>. Study commands decode through the factory in
/// many places that assume cross-correlation (frame subtraction feeds negative counts, <c>CorrelationSearch</c> reads the
/// ±1 weights, per-configuration loops would rebuild an MLEM matrix each time), so a scenario that selects another method
/// is refused before the study starts instead of silently changing its decoder.</summary>
internal static class StudyDecoderGuard
{
    /// <summary>The first JSON argument whose <c>decoder.method</c> (any letter case) names something other than
    /// cross-correlation, or null. Files that are not scenarios (evidence requests, spectra) carry no decoder section.</summary>
    public static string? FirstNonCorrelationScenario(IEnumerable<string> arguments)
    {
        foreach (string path in arguments)
        {
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) continue;
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(File.ReadAllText(path),
                    new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            }
            catch (JsonException) { continue; }
            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object) continue;
                foreach (var section in document.RootElement.EnumerateObject())
                {
                    if (!section.NameEquals("decoder") && !string.Equals(section.Name, "decoder", StringComparison.OrdinalIgnoreCase)) continue;
                    if (section.Value.ValueKind != JsonValueKind.Object) continue;
                    foreach (var field in section.Value.EnumerateObject())
                        if (string.Equals(field.Name, "method", StringComparison.OrdinalIgnoreCase)
                            && !(field.Value.ValueKind == JsonValueKind.String
                                 && string.Equals(field.Value.GetString(), "CrossCorrelation", StringComparison.OrdinalIgnoreCase)))
                            return path;
                }
            }
        }
        return null;
    }
}
