using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Atomic create disposition prevents overwriting an existing marker, including a racing creator.</summary>
public sealed class WindowsRegistryMarkerStore : IRegistryMarkerStore
{
    public string NativeUserHive
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return @"\REGISTRY\USER\" + (identity.User?.Value ?? throw new InvalidOperationException("User SID unavailable"));
        }
    }

    public bool TryCreateNew(string relativeKey, string valueName, string valueData)
    {
        // The public backend also refuses paths outside this exact marker convention.
        string runId = relativeKey.StartsWith(RegistryMarker.RelativeRoot + "\\", StringComparison.Ordinal)
            ? relativeKey[(RegistryMarker.RelativeRoot.Length + 1)..] : "";
        _ = IsolationPaths.RunRoot("scratch", runId);
        if (runId.Length > 71) throw new ArgumentException("Probe run ID exceeds the trace wrapper limit", nameof(relativeKey));
        if (valueName != RegistryMarker.ValueName || valueData != runId) throw new InvalidDataException("Invalid registry marker value");
        const int keySetValue = 0x0002, createdNewKey = 1;
        int result = RegCreateKeyEx(new IntPtr(unchecked((int)0x80000001)), relativeKey, 0, null, 0, keySetValue,
            IntPtr.Zero, out var key, out int disposition);
        using (key)
        {
            if (result != 0) throw new Win32Exception(result);
            if (disposition != createdNewKey) return false;
            byte[] data = Encoding.Unicode.GetBytes(valueData + "\0");
            result = RegSetValueEx(key, valueName, 0, 1 /* REG_SZ */, data, data.Length);
            if (result != 0) throw new Win32Exception(result); // Retain even incomplete markers; cleanup is an author act.
            return true;
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegCreateKeyExW")]
    private static extern int RegCreateKeyEx(IntPtr root, string subKey, int reserved, string? keyClass, int options,
        int access, IntPtr security, out SafeRegistryHandle key, out int disposition);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegSetValueExW")]
    private static extern int RegSetValueEx(SafeRegistryHandle key, string name, int reserved, int type, byte[] data, int size);
}
