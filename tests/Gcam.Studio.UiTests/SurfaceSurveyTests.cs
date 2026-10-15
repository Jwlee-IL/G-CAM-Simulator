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
        Assert.DoesNotContain(references, r => r.StartsWith("System.IO.File.Write", StringComparison.Ordinal)
            || r.StartsWith("System.Net.", StringComparison.Ordinal) || r == "System.Diagnostics.Process.Start");
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
}
