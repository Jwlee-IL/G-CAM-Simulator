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
        foreach (bool deployment in new[] { false, true })
        {
            var files = ContentFingerprint.Files(root, deployment);
            var start = new ProcessStartInfo("python") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string argument in new[] { "-B", Path.Combine(RepoPaths.Root(), "tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py"),
                "fingerprint", "--directory", root, "--kind", deployment ? "deployment" : "isolation" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(deadline.Token); // Timeout never force-kills a process.
            Assert.True(process.ExitCode == 0, await stderr);
            using var json = JsonDocument.Parse(await stdout);
            Assert.Equal(ContentFingerprint.Digest(files), json.RootElement.GetProperty("sha256").GetString());
            Assert.Equal(files.Keys.ToArray(), json.RootElement.GetProperty("files").EnumerateObject().Select(p => p.Name).ToArray());
            foreach (var file in files) Assert.Equal(file.Value, json.RootElement.GetProperty("files").GetProperty(file.Key).GetString());
            Assert.DoesNotContain(files.Keys, key => key.Contains("__pycache__", StringComparison.Ordinal) || key.StartsWith("bin/", StringComparison.Ordinal));
        }
    }
}
