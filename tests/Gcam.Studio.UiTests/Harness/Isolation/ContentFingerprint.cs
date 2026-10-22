using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Spec 1: raw bytes; relative slash paths; UTF-16 ordinal order; LF separators, no final LF.</summary>
public static class ContentFingerprint
{
    public sealed record DataFile(string Path, string Sha256);
    public sealed record EmbeddedData(string Assembly, string Resource, string Sha256);
    public sealed record TypedScenes(string Id, string Kind, string Generator);
    public sealed record DataList(string Contract, bool UserImportSlot, string[] DeploymentFiles, DataFile[] DataFiles,
        EmbeddedData[] EmbeddedData, TypedScenes TypedScenes, OwnedStudioProcess.PeerBoundary Peers);
    public sealed record DataCheck(string Contract, string ListSha256, string DeploymentSha256, string[] FingerprintCoverage,
        string[] DeploymentFiles, bool UserImportSlot, TypedScenes TypedScenes, string ScenarioId, string GeneratorAssemblySha256);

    /// <summary>Data input integrity is checked before launch. PDBs are diagnostics, outside fingerprint spec 1.</summary>
    public static (DataCheck Data, OwnedStudioProcess.PeerBoundary Peers) CheckData(string deployment, string listPath,
        string scenario, string? expectedDeployment = null)
    {
        byte[] bytes = File.ReadAllBytes(listPath);
        var list = JsonSerializer.Deserialize<DataList>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Missing test-data list");
        if (list.Contract != "v4" || list.UserImportSlot || string.IsNullOrWhiteSpace(scenario)
            || list.TypedScenes.Id != "harness-typed-scenes" || list.TypedScenes.Kind != "synthetic"
            || string.IsNullOrWhiteSpace(list.TypedScenes.Generator)) throw new InvalidDataException("Unsupported test-data contract");
        // Walk without following links, and check the file set before reading any payload.
        var paths = new List<string>();
        var pending = new Stack<string>([deployment]);
        _ = IsolationPaths.Inside(deployment, "preflight-path-check");
        while (pending.TryPop(out string? directory))
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                string relative = Path.GetRelativePath(deployment, path).Replace('\\', '/');
                _ = IsolationPaths.Inside(deployment, relative);
                if (File.GetAttributes(path).HasFlag(FileAttributes.Directory)) pending.Push(path);
                else if (Path.GetExtension(path) != ".pdb") paths.Add(relative);
            }
        if (list.DeploymentFiles.Distinct(StringComparer.Ordinal).Count() != list.DeploymentFiles.Length
            || !paths.Order(StringComparer.Ordinal).SequenceEqual(list.DeploymentFiles.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Deployment file set differs from the test-data list");
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string relative in paths)
        {
            if (Path.GetExtension(relative) is not (".dll" or ".exe" or ".json"))
                throw new InvalidDataException("Unexpected deployment data file: " + relative);
            using var stream = File.OpenRead(IsolationPaths.Inside(deployment, relative));
            files.Add(relative, Convert.ToHexStringLower(SHA256.HashData(stream)));
        }
        string fingerprint = Digest(files);
        if (expectedDeployment is not null && fingerprint != expectedDeployment)
            throw new InvalidDataException("Deployment differs from the approved fingerprint");
        foreach (var data in list.DataFiles)
            if (!files.TryGetValue(data.Path, out var hash) || hash != data.Sha256)
                throw new InvalidDataException("Test-data hash differs: " + data.Path);
        foreach (var data in list.EmbeddedData)
        {
            using var stream = File.OpenRead(IsolationPaths.Inside(deployment, data.Assembly));
            using var pe = new PEReader(stream);
            var reader = pe.GetMetadataReader();
            var resource = reader.ManifestResources.Select(reader.GetManifestResource)
                .Single(r => reader.GetString(r.Name) == data.Resource && r.Implementation.IsNil);
            int rva = pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress;
            var content = pe.GetSectionData(rva).GetReader((int)resource.Offset, pe.GetSectionData(rva).Length - (int)resource.Offset);
            byte[] payload = content.ReadBytes(content.ReadInt32());
            if (IsolationPaths.Hash(payload) != data.Sha256) throw new InvalidDataException("Embedded test-data hash differs: " + data.Resource);
        }
        return (new("v4", IsolationPaths.Hash(bytes), fingerprint, [".dll", ".exe", ".json"], files.Keys.ToArray(),
            false, list.TypedScenes, scenario, IsolationPaths.Hash(File.ReadAllBytes(typeof(ContentFingerprint).Assembly.Location))), list.Peers);
    }

    public static SortedDictionary<string, string> Files(string root, bool deployment = false)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative.Split('/').Any(part => part is "bin" or "obj" or "__pycache__")) continue;
            if (deployment && Path.GetExtension(path) is not (".dll" or ".exe" or ".json")) continue;
            _ = IsolationPaths.Inside(root, relative);
            using var stream = File.OpenRead(path);
            files.Add(relative, Convert.ToHexStringLower(SHA256.HashData(stream)));
        }
        return files;
    }

    public static string Digest(IEnumerable<KeyValuePair<string, string>> files) => IsolationPaths.Hash(Encoding.UTF8.GetBytes(
        string.Join('\n', files.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + ":" + pair.Value))));
}
