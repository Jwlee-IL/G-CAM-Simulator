using System.Diagnostics;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Read-only source identity for the reviewed commit; never creates or changes an index.</summary>
public static class SourceIdentity
{
    public static (string Commit, DateTimeOffset CommittedAt) Read(string repository)
    {
        string Query(params string[] arguments)
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = repository, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Source query unavailable");
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10000)) throw new TimeoutException("Source query timed out");
            if (process.ExitCode != 0) throw new InvalidDataException("Source query failed: " + stderr.GetAwaiter().GetResult());
            return stdout.GetAwaiter().GetResult().Trim();
        }
        string commit = Query("rev-parse", "HEAD");
        if (!string.IsNullOrEmpty(Query("status", "--porcelain")))
            throw new InvalidDataException("Observed qualification requires the reviewed committed working tree");
        return (commit, DateTimeOffset.Parse(Query("show", "-s", "--format=%cI", commit), System.Globalization.CultureInfo.InvariantCulture));
    }
}
