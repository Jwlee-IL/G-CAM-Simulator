using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Gcam.Studio.UiTests;

internal static class ObservationFixtures
{
    /// <summary>A fresh temporary folder owned by one test, removed with everything the test created in it.</summary>
    public sealed class ScratchFolder : IDisposable
    {
        public string Path { get; } = IsolationPathsTests.Scratch();
        public ScratchFolder() => Directory.CreateDirectory(Path);
        public static implicit operator string(ScratchFolder folder) => folder.Path;
        public void Dispose()
        {
            if (!Directory.Exists(Path)) return;
            // Remove links first so the recursive delete can never reach a link target outside this folder.
            foreach (string link in Directory.EnumerateDirectories(Path, "*", SearchOption.AllDirectories)
                .Where(d => (File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0).ToArray())
                Directory.Delete(link);
            Directory.Delete(Path, recursive: true);
        }
    }
    public static ScratchFolder Scratch() => new();
    public static Process Helper(int exitCode, string setup = "")
    {
        var info = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command",
            (setup.Length == 0 ? "" : setup + "; ") + "[Console]::WriteLine('ready'); [Console]::ReadLine() | Out-Null; exit " + exitCode }) info.ArgumentList.Add(arg);
        var process = Process.Start(info)!;
        var ready = process.StandardOutput.ReadLineAsync();
        if (!ready.Wait(TimeSpan.FromSeconds(10)) || ready.Result != "ready")
        {
            Exit(process); process.Dispose(); throw new InvalidOperationException("Helper startup did not acknowledge readiness");
        }
        return process;
    }
    /// <summary>Asks the test's own helper to exit; a helper that does not exit in time is killed (only this helper).</summary>
    public static void Exit(Process process)
    {
        if (process.HasExited) return;
        try { process.StandardInput.WriteLine(); process.StandardInput.Flush(); } catch (IOException) { }
        if (process.WaitForExit(5000)) return;
        process.Kill(entireProcessTree: true); process.WaitForExit(5000);
        Assert.Fail("Owned helper did not exit within 5 s and was killed");
    }
    /// <summary>A directory junction at <paramref name="link"/> (created here) to <paramref name="target"/>; no privilege
    /// is needed, unlike a directory symbolic link.</summary>
    public static void Junction(string link, string target)
    {
        Directory.CreateDirectory(link);
        byte[] substitute = Encoding.Unicode.GetBytes(@"\??\" + Path.GetFullPath(target).TrimEnd('\\') + @"\");
        byte[] print = Encoding.Unicode.GetBytes(Path.GetFullPath(target));
        int pathBytes = substitute.Length + 2 + print.Length + 2;
        var buffer = new byte[8 + 8 + pathBytes];
        BitConverter.GetBytes(0xA0000003u).CopyTo(buffer, 0);             // IO_REPARSE_TAG_MOUNT_POINT
        BitConverter.GetBytes((ushort)(8 + pathBytes)).CopyTo(buffer, 4);  // ReparseDataLength
        BitConverter.GetBytes((ushort)0).CopyTo(buffer, 8);                // SubstituteNameOffset
        BitConverter.GetBytes((ushort)substitute.Length).CopyTo(buffer, 10);
        BitConverter.GetBytes((ushort)(substitute.Length + 2)).CopyTo(buffer, 12); // PrintNameOffset
        BitConverter.GetBytes((ushort)print.Length).CopyTo(buffer, 14);
        substitute.CopyTo(buffer, 16); print.CopyTo(buffer, 16 + substitute.Length + 2);
        using var handle = CreateFile(link, 0x40000000, 0, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!DeviceIoControl(handle, 0x000900A4, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero)) // FSCTL_SET_REPARSE_POINT
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public static string ExplainVideo(string path)
    {
        var facts = new List<object>();
        var decoded = Harness.Video.MediaFoundationDecoder.Decode(path, (sample, rgb) =>
        {
            int[] lo = [255, 255, 255], hi = [0, 0, 0];
            for (int y = 2; y < sample.HeightPx - 2; y++) for (int x = 2; x < sample.WidthPx - 2; x++) for (int c = 0; c < 3; c++)
            { int v = rgb[(y * sample.WidthPx + x) * 4 + c]; lo[c] = Math.Min(lo[c], v); hi[c] = Math.Max(hi[c], v); }
            facts.Add(new { sample.Time100ns, sample.SingleColour, sample.Black, lo, hi });
        });
        return System.Text.Json.JsonSerializer.Serialize(new { decoded.Error, facts });
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string name,
        uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool DeviceIoControl(SafeFileHandle handle, uint code,
        byte[] input, int inputSize, IntPtr output, int outputSize, out int returned, IntPtr overlapped);
}

/// <summary>Helper-process tests need PowerShell 7 on PATH; without it they skip with that reason instead of failing.</summary>
public sealed class PwshFactAttribute : FactAttribute
{
    public PwshFactAttribute() => Skip = PwshProbe.Unavailable.Value;
}
public sealed class PwshTheoryAttribute : TheoryAttribute
{
    public PwshTheoryAttribute() => Skip = PwshProbe.Unavailable.Value;
}
internal static class PwshProbe
{
    public static readonly Lazy<string?> Unavailable = new(() =>
    {
        try
        {
            var info = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            foreach (string arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "exit 0" }) info.ArgumentList.Add(arg);
            using var probe = Process.Start(info)!;
            if (probe.WaitForExit(15000) && probe.ExitCode == 0) return null;
            if (!probe.HasExited) { probe.Kill(entireProcessTree: true); probe.WaitForExit(5000); }
            return "pwsh did not answer the readiness probe";
        }
        catch (Win32Exception e) { return "pwsh is not on PATH: " + e.Message; }
    });
}
