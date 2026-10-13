using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Owned destinations and launch values. No fallback to a user-selected folder.</summary>
public static partial class IsolationPaths
{
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,95}$")]
    private static partial Regex RunName();

    public static string RunRoot(string temporaryRoot, string runId)
    {
        if (!RunName().IsMatch(runId)) throw new ArgumentException("Invalid run ID", nameof(runId));
        return Path.Combine(Path.GetFullPath(temporaryRoot), "gcam-uia-" + runId);
    }

    public static string Inside(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/', '\\').Any(p => p is ".." or "."))
            throw new ArgumentException("Destination must be a confined relative path", nameof(relative));
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Destination escaped root");
        if ((File.Exists(path) || Directory.Exists(path)) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("Reparse point at owned destination");
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(path)!); dir is not null && dir.FullName.Length >= fullRoot.Length - 1; dir = dir.Parent)
            if (dir.Exists && dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Reparse point in owned destination");
        return path;
    }

    public static IReadOnlyDictionary<string, string> LaunchValues(string root, string runId) => new Dictionary<string, string>
    {
        ["APPDATA"] = Inside(root, "Roaming"), ["LOCALAPPDATA"] = Inside(root, "Local"),
        ["TEMP"] = Inside(root, "Temp"), ["TMP"] = Inside(root, "Temp"), ["GCAM_UIA_RUN"] = runId,
    };

    public static void ValidateLaunchValues(string root, string runId, IReadOnlyDictionary<string, string> values)
    {
        var expected = LaunchValues(root, runId);
        if (expected.Count != values.Count || expected.Any(v => !values.TryGetValue(v.Key, out string? actual) || actual != v.Value))
            throw new ArgumentException("Launch values differ from the isolation allow-list");
    }

    public static string Fingerprint(string directory)
    {
        return ContentFingerprint.Digest(ContentFingerprint.Files(directory));
    }

    public static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
