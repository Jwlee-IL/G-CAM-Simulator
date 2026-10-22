using System.Runtime.InteropServices;

namespace Gcam.Studio.UiTests.Harness.Video;

/// <summary>Source-reader RGB32 decode to EOS. Optional frame consumer supports a future still-per-cue annex.</summary>
public static class MediaFoundationDecoder
{
    public static DecodedVideo Decode(string path, Action<DecodedVideo.Sample, byte[]>? consumeFrame = null)
    {
        DecodedVideo? result = null;
        var thread = new Thread(() => result = Read(path, consumeFrame)) { IsBackground = true, Name = "GCAM video decoder" };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start(); thread.Join(); return result!;
    }
    private static DecodedVideo Read(string path, Action<DecodedVideo.Sample, byte[]>? consume)
    {
        IntPtr attributes = default, reader = default, type = default, actualType = default;
        bool com = false, mf = false, eos = false; string? error = null; int changes = 0;
        var samples = new List<DecodedVideo.Sample>();
        const int VideoStream = unchecked((int)0xfffffffc);
        try
        {
            path = Path.GetFullPath(path);
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) throw new InvalidDataException("A local video file is required");
            H(CoInitializeEx(IntPtr.Zero, 0)); com = true; H(MFStartup(0x20070, 0)); mf = true;
            H(MFCreateAttributes(out attributes, 1));
            Guid processing = new("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");
            H(F<SetU>(attributes, 21)(attributes, ref processing, 1));
            H(MFCreateSourceReaderFromURL(path, attributes, out reader));
            H(F<Select>(reader, 4)(reader, unchecked((int)0xfffffffe), 0)); H(F<Select>(reader, 4)(reader, VideoStream, 1));
            H(MFCreateMediaType(out type));
            G(type, "48eba18e-f8c9-4687-bf11-0a74c9f96a8f", "73646976-0000-0010-8000-00aa00389b71");
            G(type, "f7e34c9a-42e8-4714-b74b-cb29d72c35e5", "00000016-0000-0010-8000-00aa00389b71");
            H(F<SetType>(reader, 7)(reader, VideoStream, IntPtr.Zero, type));
            // The decoder may report its real output type with the first sample (CURRENTMEDIATYPECHANGED): an H.264
            // picture whose height is not a multiple of 16 decodes into a padded frame (900 -> 912 rows) with the
            // displayed picture given by its minimum display aperture. Re-read the type then; analyse only that area.
            int width = 0, height = 0, stride = 0, offsetX = 0, offsetY = 0; bool bottomUp = false; long frameBytes = 0;
            void Configure()
            {
                Release(actualType); actualType = IntPtr.Zero;
                H(F<CurrentMediaType>(reader, 6)(reader, VideoStream, out actualType));
                Guid sizeKey = new("1652c33d-d6b2-4012-b834-72030849a37d");
                H(F<GetU64>(actualType, 8)(actualType, ref sizeKey, out long size));
                int frameWidth = (int)(size >> 32), frameHeight = (int)(size & uint.MaxValue);
                Guid strideKey = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
                stride = frameWidth * 4; bottomUp = false;
                if (F<GetU>(actualType, 7)(actualType, ref strideKey, out int configuredStride) >= 0)
                { stride = Math.Abs(configuredStride); bottomUp = configuredStride < 0; }
                (offsetX, offsetY, width, height) = (0, 0, frameWidth, frameHeight);
                if (Aperture(actualType, "d7388766-18fe-48c6-a177-ee894867c8c4") is { } area) // MF_MT_MINIMUM_DISPLAY_APERTURE
                    (offsetX, offsetY, width, height) = area;
                if (width <= 4 || height <= 4 || stride < frameWidth * 4 || offsetX < 0 || offsetY < 0
                    || offsetX + width > frameWidth || offsetY + height > frameHeight) throw new InvalidDataException("Invalid decoded dimensions/stride");
                frameBytes = checked((long)stride * frameHeight);
            }
            Configure();
            while (true)
            {
                H(F<ReadSample>(reader, 9)(reader, VideoStream, 0, out _, out int flags, out long time, out var sample));
                try
                {
                    if ((flags & 1) != 0) throw new InvalidDataException("Source reader error flag");
                    // A native (container) type change never occurs in the harness's single-stream recordings.
                    if ((flags & 0x10) != 0) throw new InvalidDataException("Native media type changed");
                    if ((flags & 0x20) != 0)
                    {
                        if (!MayFollowTypeChange(samples.Count)) throw new InvalidDataException("Decoded media type changed after the first sample");
                        Configure(); changes++;
                    }
                    if (sample != IntPtr.Zero)
                    {
                        H(F<GetTime>(sample, 37)(sample, out long duration));
                        H(F<GetBuffer>(sample, 41)(sample, out var buffer));
                        try
                        {
                            H(F<Lock>(buffer, 3)(buffer, out var bytes, out _, out int length));
                            byte[]? rgb = null; bool single, black;
                            try
                            {
                                if (length < frameBytes) throw new InvalidDataException("Short decoded RGB32 buffer");
                                int frameHeight = (int)(frameBytes / stride);
                                // Display row y of the aperture, top-down, whatever the buffer's row order.
                                IntPtr Row(int y) => bytes + (bottomUp ? frameHeight - 1 - (offsetY + y) : offsetY + y) * stride + offsetX * 4;
                                single = SingleColour(Row, width, height, out black);
                                if (consume is not null)
                                {
                                    rgb = new byte[checked(width * height * 4)];
                                    for (int y = 0; y < height; y++) Marshal.Copy(Row(y), rgb, y * width * 4, width * 4);
                                }
                            }
                            finally { H(F<Plain>(buffer, 4)(buffer)); }
                            var fact = new DecodedVideo.Sample(time, duration, width, height, single, black);
                            samples.Add(fact); consume?.Invoke(fact, rgb!);
                        }
                        finally { Release(buffer); }
                    }
                    if ((flags & 2) != 0) { eos = true; break; }
                }
                finally { Release(sample); }
            }
        }
        catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
        finally
        {
            Release(actualType); Release(type); Release(reader); Release(attributes);
            if (mf) MFShutdown(); if (com) CoUninitialize();
        }
        return new(eos, samples.ToArray(), error, changes);
    }
    /// <summary>The decoder announces its real output type at most once, with or before the first sample. A change
    /// after a delivered sample is not followed: the stream fails decode-to-end.</summary>
    internal static bool MayFollowTypeChange(int deliveredSamples) => deliveredSamples == 0;
    private static bool SingleColour(Func<int, IntPtr> row0, int width, int height, out bool black)
    {
        // Same detector used in the measured review: allow H.264 quantization range 8; omit the fixed neutral ring.
        black = false;
        byte[] row = new byte[width * 4];
        Span<int> lo = stackalloc int[] { 255, 255, 255 }; Span<int> hi = stackalloc int[] { 0, 0, 0 };
        for (int y = VideoCapture.NeutralEdgePx; y < height - VideoCapture.NeutralEdgePx; y++)
        {
            Marshal.Copy(row0(y), row, 0, row.Length);
            for (int x = VideoCapture.NeutralEdgePx; x < width - VideoCapture.NeutralEdgePx; x++)
                for (int c = 0; c < 3; c++)
                {
                    int v = row[x * 4 + c]; lo[c] = Math.Min(lo[c], v); hi[c] = Math.Max(hi[c], v);
                    if (hi[c] - lo[c] > 8) return false;
                }
        }
        black = hi[0] <= 8 && hi[1] <= 8 && hi[2] <= 8;
        return true;
    }
    private static (int X, int Y, int Width, int Height)? Aperture(IntPtr type, string key)
    {
        // MFVideoArea: two MFOffset { WORD fract; short value } then SIZE { LONG cx, cy }.
        Guid k = new(key); byte[] area = new byte[16];
        if (F<GetBlob>(type, 15)(type, ref k, area, area.Length, out int size) < 0 || size != 16) return null;
        int width = BitConverter.ToInt32(area, 8), height = BitConverter.ToInt32(area, 12);
        return width > 0 && height > 0 ? (BitConverter.ToInt16(area, 2), BitConverter.ToInt16(area, 6), width, height) : null;
    }
    private static void H(int hr) => Marshal.ThrowExceptionForHR(hr);
    private static void Release(IntPtr p) { if (p != IntPtr.Zero) Marshal.Release(p); }
    private static T F<T>(IntPtr p, int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(p), slot * IntPtr.Size));
    private static void G(IntPtr p, string key, string value) { Guid k = new(key), v = new(value); H(F<SetG>(p, 24)(p, ref k, ref v)); }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Plain(IntPtr p);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetU(IntPtr p, ref Guid key, int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetU(IntPtr p, ref Guid key, out int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetBlob(IntPtr p, ref Guid key, byte[] buffer, int size, out int actual);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetU64(IntPtr p, ref Guid key, out long value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetG(IntPtr p, ref Guid key, ref Guid value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetType(IntPtr p, int stream, IntPtr reserved, IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CurrentMediaType(IntPtr p, int stream, out IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Select(IntPtr p, int stream, int selected);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ReadSample(IntPtr p, int stream, int flags, out int actual, out int status, out long time, out IntPtr sample);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetBuffer(IntPtr p, out IntPtr buffer);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetTime(IntPtr p, out long time);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Lock(IntPtr p, out IntPtr bytes, out int maximum, out int length);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr p, int mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("mfplat.dll")] private static extern int MFStartup(int version, int flags);
    [DllImport("mfplat.dll")] private static extern int MFShutdown();
    [DllImport("mfplat.dll")] private static extern int MFCreateAttributes(out IntPtr p, int count);
    [DllImport("mfplat.dll")] private static extern int MFCreateMediaType(out IntPtr p);
    [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int MFCreateSourceReaderFromURL(string url, IntPtr attributes, out IntPtr reader);
}
