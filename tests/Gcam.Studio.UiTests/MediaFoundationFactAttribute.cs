using System.Runtime.InteropServices;
using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

/// <summary>xUnit 2 discovers an unavailable system encoder as skipped, rather than treating a runtime skip as failure.</summary>
public sealed class MediaFoundationFactAttribute : FactAttribute
{
    private static readonly Lazy<string?> Unavailable = new(Probe);
    public MediaFoundationFactAttribute() => Skip = Unavailable.Value;
    private static string? Probe()
    {
        string? reason = null;
        var thread = new Thread(() =>
        {
            try
            {
                string folder = IsolationPathsTests.Scratch(); Directory.CreateDirectory(folder);
                using var writer = new MediaFoundationWriter(Path.Combine(folder, "encoder-probe.mp4"), 64, 48);
                writer.Write(new byte[64 * 48 * 4], 0, VideoTimeline.Period100ns); writer.Finish();
            }
            catch (Exception e) when (e is COMException or DllNotFoundException or PlatformNotSupportedException or EntryPointNotFoundException)
            { reason = $"Media Foundation H.264 encoder unavailable ({e.GetType().Name}, 0x{e.HResult:x8})"; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        return thread.Join(TimeSpan.FromSeconds(5)) ? reason : "Media Foundation encoder availability timed out";
    }
}
