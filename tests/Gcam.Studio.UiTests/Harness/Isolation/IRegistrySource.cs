namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Read-only view of the current user's hive for the MR8 snapshot; headless tests inject a fake.</summary>
public interface IRegistrySource
{
    /// <summary>One value as stored: name, registry type and the raw data bytes.</summary>
    public sealed record Value(string Name, int Kind, byte[] Data);

    /// <summary>One key as read: last-write time, its values and the names of its subkeys.</summary>
    public sealed record Key(DateTime LastWriteUtc, IReadOnlyList<Value> Values, IReadOnlyList<string> SubKeys);

    /// <summary>Reads the key at <paramref name="path"/> below HKCU ("" is the hive root). Throws when it cannot be read.</summary>
    Key Read(string path);
}
