using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Gcam.Configuration;

/// <summary>Reading and writing hash-pinned spectrum files. The pinned hash is the SHA-256 of the file's bytes as git
/// stores them: the repository normalises <c>*.json</c> to LF (<c>.gitattributes</c>), so every writer here emits LF
/// line ends and UTF-8 without a byte-order mark, and a checkout on any OS reproduces the recorded hash.</summary>
public static class IncidentSpectrumFile
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, NewLine = "\n" };

    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>Indented form (small development fixtures); LF line ends on every OS.</summary>
    public static string SerializeIndented(IncidentSpectrum spectrum) => JsonSerializer.Serialize(spectrum, Indented);

    /// <summary>Compact form (the terrestrial spectrum's table has ~10⁴ rows; no line ends at all).</summary>
    public static string SerializeCompact(IncidentSpectrum spectrum) => JsonSerializer.Serialize(spectrum);

    /// <summary>Write <paramref name="text"/> and its <c>.sha256</c> sidecar (hash + LF). Refuses to overwrite either:
    /// a pinned file is immutable, a new version gets a new name.</summary>
    public static string WriteWithSidecar(string path, string text)
    {
        if (text.Contains('\r')) throw new ArgumentException("Pinned files are written with LF line ends only.", nameof(text));
        if (File.Exists(path) || File.Exists(path + ".sha256"))
            throw new IOException($"Refusing to overwrite the pinned file {Path.GetFileName(path)} or its sidecar.");
        byte[] bytes = new UTF8Encoding(false).GetBytes(text);
        File.WriteAllBytes(path, bytes);
        string hash = Sha256Hex(bytes);
        File.WriteAllText(path + ".sha256", hash + "\n", new UTF8Encoding(false));
        return hash;
    }

    /// <summary>Load a spectrum whose file bytes must hash to <paramref name="expectedSha256"/>, and whose payload must
    /// match its own content hash.</summary>
    public static IncidentSpectrum Load(string path, string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256)) throw new ArgumentException("A spectrum file reference needs its SHA-256.");
        byte[] bytes = File.ReadAllBytes(path);
        string actual = Sha256Hex(bytes);
        if (!string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Spectrum file {Path.GetFileName(path)}: SHA-256 {actual} does not match the pinned {expectedSha256}.");
        var spectrum = JsonSerializer.Deserialize<IncidentSpectrum>(bytes)
            ?? throw new InvalidDataException($"Spectrum file {Path.GetFileName(path)} is empty.");
        if (spectrum.ContentHash != spectrum.ComputeContentHash())
            throw new InvalidDataException($"Spectrum file {Path.GetFileName(path)}: payload does not match its content hash.");
        return spectrum;
    }
}
