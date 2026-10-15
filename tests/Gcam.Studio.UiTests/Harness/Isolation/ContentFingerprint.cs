using System.Security.Cryptography;
using System.Text;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Spec 1: raw bytes; relative slash paths; UTF-16 ordinal order; LF separators, no final LF.</summary>
public static class ContentFingerprint
{
    public static SortedDictionary<string, string> Files(string root, bool deployment = false)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative.Split('/').Any(part => part is "bin" or "obj" or "__pycache__")) continue;
            if (deployment && Path.GetExtension(path) is not (".dll" or ".exe" or ".json")) continue;
            using var stream = File.OpenRead(path);
            files.Add(relative, Convert.ToHexStringLower(SHA256.HashData(stream)));
        }
        return files;
    }

    public static string Digest(IEnumerable<KeyValuePair<string, string>> files) => IsolationPaths.Hash(Encoding.UTF8.GetBytes(
        string.Join('\n', files.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + ":" + pair.Value))));
}
