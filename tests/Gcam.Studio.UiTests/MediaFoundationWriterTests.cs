using System.Runtime.ExceptionServices;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

public sealed class MediaFoundationWriterTests(ITestOutputHelper output)
{
    [MediaFoundationFact]
    public void SyntheticRgb32Sequence_WritesFinalizedH264Container_WithoutScreenAccess()
    {
        string folder = IsolationPathsTests.Scratch(); Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "synthetic.mp4"); Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var writer = new MediaFoundationWriter(path, 64, 48);
                var pixels = new byte[64 * 48 * 4];
                for (int n = 0; n < 20; n++)
                {
                    Array.Fill(pixels, (byte)30);
                    for (int y = 15; y < 30; y++) for (int x = n; x < n + 10; x++) { int i = (y * 64 + x) * 4; pixels[i] = 0; pixels[i + 1] = 200; pixels[i + 2] = 240; }
                    writer.Write(pixels, n * VideoTimeline.Period100ns, VideoTimeline.Period100ns);
                }
                writer.Finish();
            }
            catch (Exception e) { failure = e; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "Encoder did not complete within 5 s");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        byte[] bytes = File.ReadAllBytes(path); string tags = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("ftyp", tags); Assert.Contains("moov", tags); Assert.Contains("mdat", tags); Assert.Contains("avc1", tags);
        // Independent MP4 sample-size box: full-box flags, fixed sample size, then sample count.
        int stsz = tags.IndexOf("stsz", StringComparison.Ordinal); Assert.True(stsz >= 0);
        Assert.Equal(20u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(stsz + 12, 4)));
        output.WriteLine($"H.264 encode: 20 RGB32 frames, 64x48, 10 fps; {bytes.Length} bytes");
    }
}
