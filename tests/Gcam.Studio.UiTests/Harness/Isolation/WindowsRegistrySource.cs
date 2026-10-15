using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>
/// HKCU opened read-only (KEY_READ). Values are enumerated with their raw bytes so the snapshot hashes what is stored,
/// not a managed rendering of it; the key last-write time comes from RegQueryInfoKey.
/// </summary>
public sealed class WindowsRegistrySource : IRegistrySource
{
    private const int ErrorMoreData = 234, ErrorNoMoreItems = 259;

    public IRegistrySource.Key Read(string path)
    {
        RegistryKey key = path.Length == 0 ? Registry.CurrentUser
            : Registry.CurrentUser.OpenSubKey(path, writable: false) ?? throw new IOException("Key not found");
        try
        {
            SafeRegistryHandle handle = key.Handle;
            int result = RegQueryInfoKey(handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out _, out _, IntPtr.Zero,
                out int valueCount, out int maxName, out int maxData, IntPtr.Zero, out long lastWrite);
            if (result != 0) throw new Win32Exception(result);
            var values = new List<IRegistrySource.Value>(valueCount);
            char[] name = new char[maxName + 1];
            byte[] data = new byte[Math.Max(maxData, 1)];
            for (int index = 0; ; )
            {
                int nameLength = name.Length, dataLength = data.Length;
                result = RegEnumValue(handle, index, name, ref nameLength, IntPtr.Zero, out int kind, data, ref dataLength);
                if (result == ErrorNoMoreItems) break;
                if (result == ErrorMoreData)
                {
                    // The key changed while it was read; grow the buffers and read the same index again.
                    name = new char[Math.Max(name.Length * 2, 256)];
                    data = new byte[Math.Max(data.Length * 2, dataLength)];
                    continue;
                }
                if (result != 0) throw new Win32Exception(result);
                values.Add(new(new string(name, 0, nameLength), kind, data[..dataLength]));
                index++;
            }
            return new(DateTime.FromFileTimeUtc(lastWrite), values, key.GetSubKeyNames());
        }
        finally
        {
            if (path.Length != 0) key.Dispose();
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryInfoKeyW")]
    private static extern int RegQueryInfoKey(SafeRegistryHandle key, IntPtr keyClass, IntPtr classLength, IntPtr reserved,
        out int subKeys, out int maxSubKeyLength, IntPtr maxClassLength, out int values, out int maxValueNameLength,
        out int maxValueLength, IntPtr securityDescriptorLength, out long lastWriteTime);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegEnumValueW")]
    private static extern int RegEnumValue(SafeRegistryHandle key, int index, [Out] char[] name, ref int nameLength,
        IntPtr reserved, out int type, [Out] byte[] data, ref int dataLength);
}
