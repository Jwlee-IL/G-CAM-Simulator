using System.Diagnostics;
using System.Text.Json;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Probe executable identity rather than trusting an alias or a machine-specific install directory.</summary>
public static class PythonInterpreter
{
    public sealed record Result(int ExitCode, string Output, string Error);
    /// <summary>An explicit override is the only candidate (a broken one fails by name). Otherwise the launcher comes
    /// first: a Store package's executable (under <c>WindowsApps</c>) passed this probe and then refused the next start
    /// with "access denied" (2026-10-16), so it is used only when nothing else runs.</summary>
    public static string Resolve(Func<string, string[], Result> execute, string? configured = null)
    {
        var failures = new List<string>();
        string? packaged = null;
        var candidates = string.IsNullOrWhiteSpace(configured)
            ? new[] { ("py", new[] { "-3" }), ("python", Array.Empty<string>()) }
            : new[] { (configured, Array.Empty<string>()) };
        foreach (var (command, prefix) in candidates)
        {
            try
            {
                string[] probe = [.. prefix, "-B", "-c", "import sys,json; print(json.dumps({'executable':sys.executable,'major':sys.version_info.major}))"];
                var result = execute(command, probe);
                if (result.ExitCode != 0) throw new InvalidOperationException($"exit {result.ExitCode}: {result.Error}");
                using var json = JsonDocument.Parse(result.Output);
                string path = json.RootElement.GetProperty("executable").GetString() ?? "";
                if (json.RootElement.GetProperty("major").GetInt32() != 3 || !Path.IsPathFullyQualified(path))
                    throw new InvalidDataException("Probe did not identify a Python 3 executable");
                // The launcher result must itself run; the fingerprint process will use this exact executable.
                var validation = execute(path, ["-B", "-c", "import sys; print(sys.version_info.major)"]);
                if (validation.ExitCode != 0 || validation.Output.Trim() != "3") throw new InvalidDataException("Resolved executable did not run Python 3");
                if (!IsPackaged(path) || !string.IsNullOrWhiteSpace(configured)) return path;
                packaged ??= path;
                failures.Add(command + ": Store package executable, kept as the last resort");
            }
            catch (Exception e) { failures.Add(command + ": " + e.GetType().Name + ": " + e.Message); }
        }
        if (packaged is not null) return packaged;
        throw new InvalidOperationException("No working Python 3 interpreter. Set GCAM_TEST_PYTHON to an executable. " + string.Join("; ", failures));
    }
    public static bool IsPackaged(string path) =>
        path.Replace('/', '\\').Split('\\').Contains("WindowsApps", StringComparer.OrdinalIgnoreCase);
    public static Result Execute(string executable, string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in arguments) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Interpreter did not start");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10_000))
        {
            // Only the process just created by this call. No lookup by image name or attachment to user processes.
            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(2_000)) throw new TimeoutException("Interpreter cleanup timed out");
            throw new TimeoutException("Interpreter probe exceeded 10 seconds");
        }
        return new(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }
}
