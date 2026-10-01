using System.Diagnostics;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Where the app binary and the repository are, relative to this test assembly.</summary>
public static class RepoPaths
{
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gcam.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Gcam.sln not found above the test assembly");
    }

    /// <summary>The app built in the same configuration as the tests (the project reference builds it first).</summary>
    public static string StudioExe()
    {
        // …/tests/Gcam.Studio.UiTests/bin/<Configuration>/net9.0-windows/
        // BaseDirectory ends with a separator; trim it or DirectoryInfo treats the TFM folder as the leaf's parent.
        string tfmDir = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        string configuration = new DirectoryInfo(tfmDir).Parent!.Name;
        string exe = Path.Combine(Root(), "src", "Gcam.Studio", "bin", configuration, "net9.0-windows", "Gcam.Studio.exe");
        return File.Exists(exe) ? exe : throw new FileNotFoundException("build src/Gcam.Studio first", exe);
    }

    /// <summary>Number of uncommitted changes, so a manifest can say whether the binary's commit stamp is the whole story.</summary>
    public static int DirtyFileCount()
    {
        try
        {
            using var git = Process.Start(new ProcessStartInfo("git", "status --porcelain")
            {
                WorkingDirectory = Root(),
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;
            string output = git.StandardOutput.ReadToEnd();
            git.WaitForExit(10_000);
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        }
        catch (Exception)
        {
            return -1;   // git unavailable: recorded as unknown
        }
    }
}
