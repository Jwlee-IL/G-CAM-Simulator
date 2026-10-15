using System.Diagnostics;
using System.Text.Json;
using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

public sealed class ContentFingerprintTests
{
    [Fact]
    public async Task CanonicalFixture_AgreesAcrossLanguages_WithCaseNestedUnicodeAndExcludedPaths()
    {
        string root = IsolationPathsTests.Scratch(); Directory.CreateDirectory(root);
        foreach (string name in new[] { "a.dll", "a/b.dll", "B.json", "z.exe", "nested/x.txt", "\uE000.dll", "\U00010000.dll",
            "bin/skip.dll", "obj/skip.json", "__pycache__/skip.pyc", "nested/__pycache__/skip.dll" })
        {
            string path = IsolationPaths.Inside(root, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, [0, 13, 10, 255]);
        }
        string interpreter = PythonInterpreter.Resolve(PythonInterpreter.Execute, Environment.GetEnvironmentVariable("GCAM_TEST_PYTHON"));
        foreach (bool deployment in new[] { false, true })
        {
            var files = ContentFingerprint.Files(root, deployment);
            var result = await Task.Run(() => PythonInterpreter.Execute(interpreter, ["-B", Path.Combine(RepoPaths.Root(), "tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py"),
                "fingerprint", "--directory", root, "--kind", deployment ? "deployment" : "isolation"]));
            Assert.True(result.ExitCode == 0, result.Error);
            using var json = JsonDocument.Parse(result.Output);
            Assert.Equal(ContentFingerprint.Digest(files), json.RootElement.GetProperty("sha256").GetString());
            Assert.Equal(files.Keys.ToArray(), json.RootElement.GetProperty("files").EnumerateObject().Select(p => p.Name).ToArray());
            foreach (var file in files) Assert.Equal(file.Value, json.RootElement.GetProperty("files").GetProperty(file.Key).GetString());
            Assert.DoesNotContain(files.Keys, key => key.Contains("__pycache__", StringComparison.Ordinal) || key.StartsWith("bin/", StringComparison.Ordinal));
        }
    }
}
