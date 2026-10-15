using System.Text.Json;
using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

public sealed class SurfaceSurveyTests
{
    [Fact]
    public void DeployedMetadata_RecordsManagedAndNativeReferences_WithoutExecutingProduct()
    {
        string binary = Path.ChangeExtension(RepoPaths.StudioExe(), ".dll");
        string[] references = SurfaceSurvey.BinaryReferences(binary);
        Assert.Contains(references, r => r.StartsWith("native:", StringComparison.Ordinal));
        Assert.DoesNotContain(references, r => r.StartsWith("System.Net.", StringComparison.Ordinal) || r == "System.Diagnostics.Process.Start");
        // File writes are allowed only as the declared report export (D-58), and only from its declared writer types.
        // Positive control for the attribution: this test's own write below is found and attributed to this type.
        Assert.Contains(typeof(SurfaceSurveyTests).FullName!,
            SurfaceSurvey.ReferencingTypes(typeof(SurfaceSurveyTests).Assembly.Location)["System.IO.File.WriteAllText"]);
        string[] writes = references.Where(SurfaceSurvey.IsFileWrite).ToArray();
        if (writes.Length != 0)
        {
            var writers = DeclaredReportExportWriters();
            var users = SurfaceSurvey.ReferencingTypes(binary);
            foreach (string write in writes)
            {
                // A reference with no attributed method body would mean the IL scan missed it: fail rather than pass.
                Assert.True(users.TryGetValue(write, out var types) && types.Length != 0, "Unattributed file-write reference: " + write);
                Assert.All(types!, type => Assert.True(writers.Contains(type), $"{write} referenced outside the declared export: {type}"));
            }
        }
        string? destination = Environment.GetEnvironmentVariable("GCAM_SURVEY_OUTPUT");
        if (destination is not null)
        {
            string scratch = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "gcam-todo43")) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(destination).StartsWith(scratch, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Survey output root");
            Directory.CreateDirectory(destination);
            var inventory = Directory.EnumerateFiles(Path.GetDirectoryName(binary)!, "Gcam.*.dll")
                .ToDictionary(p => Path.GetFileName(p), p => new { sha256 = IsolationPaths.Hash(File.ReadAllBytes(p)), references = SurfaceSurvey.BinaryReferences(p) });
            File.WriteAllText(IsolationPaths.Inside(destination, "binary-references.json"), JsonSerializer.Serialize(inventory) + "\n");
        }
    }

    /// <summary>The writer types of the confirmed Studio file-write surface app-report-export in surface-list.json.</summary>
    private static HashSet<string> DeclaredReportExportWriters()
    {
        using var list = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(RepoPaths.Root(), "tests/Gcam.Studio.UiTests/Harness/Isolation/surface-list.json")));
        var surface = Assert.Single(list.RootElement.GetProperty("surfaces").EnumerateArray(),
            s => s.GetProperty("id").GetString() == "app-report-export");
        Assert.Equal("Studio", surface.GetProperty("actor").GetString());
        Assert.Equal("file write", surface.GetProperty("kind").GetString());
        Assert.Equal("confirmed", surface.GetProperty("status").GetString());
        var writers = surface.GetProperty("writerTypes").EnumerateArray().Select(t => t.GetString()!).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(writers);
        return writers;
    }
}
