using System.Runtime.InteropServices;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Streaming x64 RGB32 → H.264 sink writer. Create, write and dispose on the same MTA thread.</summary>
public sealed class MediaFoundationWriter : IDisposable
{
    // Complete native vtable layouts, including inherited slots. Delegate calls avoid COM interface-inheritance
    // flattening differences between runtimes. No shortened/shifted IMFAttributes declaration is used.
    private enum AttributesSlot
    {
        QueryInterface, AddRef, Release, GetItem, GetItemType, CompareItem, Compare, GetUINT32, GetUINT64,
        GetDouble, GetGUID, GetStringLength, GetString, GetAllocatedString, GetBlobSize, GetBlob, GetAllocatedBlob,
        GetUnknown, SetItem, DeleteItem, DeleteAllItems, SetUINT32, SetUINT64, SetDouble, SetGUID, SetString,
        SetBlob, SetUnknown, LockStore, UnlockStore, GetCount, GetItemByIndex, CopyAllItems
    }
    private enum MediaTypeSlot { GetMajorType = 33, IsCompressedFormat, IsEqual, GetRepresentation, FreeRepresentation }
    private enum SampleSlot
    {
        GetSampleFlags = 33, SetSampleFlags, GetSampleTime, SetSampleTime, GetSampleDuration, SetSampleDuration,
        GetBufferCount, GetBufferByIndex, ConvertToContiguousBuffer, AddBuffer, RemoveBufferByIndex, RemoveAllBuffers,
        GetTotalLength, CopyToBuffer
    }
    private enum BufferSlot { QueryInterface, AddRef, Release, Lock, Unlock, GetCurrentLength, SetCurrentLength, GetMaxLength }
    private enum WriterSlot
    {
        QueryInterface, AddRef, Release, AddStream, SetInputMediaType, BeginWriting, WriteSample, SendStreamTick,
        PlaceMarker, NotifyEndOfSegment, Flush, Finalize, GetServiceForStream, GetStatistics
    }
    private IntPtr _writer, _codec, _callbackUnknown;
    private readonly Callback _callback = new();
    private bool _previousBlack;
    /// <summary>Sink-writer completion callback: markers say every earlier sample has left the encoder; with this
    /// callback installed Finalize completes asynchronously through <see cref="OnFinalize"/>.</summary>
    [ComImport, Guid("666f76de-33d2-41b9-a458-29ed0a972c58"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISinkWriterCallback { [PreserveSig] int OnFinalize(int status); [PreserveSig] int OnMarker(int stream, IntPtr context); }
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class Callback : ISinkWriterCallback
    {
        public readonly AutoResetEvent Marker = new(false);
        public readonly ManualResetEventSlim Finalized = new();
        public int FinalizeStatus;
        public int OnFinalize(int status) { FinalizeStatus = status; Finalized.Set(); return 0; }
        public int OnMarker(int stream, IntPtr context) { Marker.Set(); return 0; }
    }
    private readonly int _stream, _bytes, _width, _height;
    private bool _mf, _com, _finished;
    public MediaFoundationWriter(string path, int width, int height)
    {
        if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("Video encoding requires x64");
        if (width <= 0 || height <= 0 || width % 2 != 0 || height % 2 != 0) throw new ArgumentOutOfRangeException(nameof(width));
        _bytes = checked(width * height * 4);
        _width = width; _height = height;
        IntPtr input = IntPtr.Zero, output = IntPtr.Zero, attributes = IntPtr.Zero;
        try
        {
            H(CoInitializeEx(IntPtr.Zero, 0)); _com = true;
            H(MFStartup(0x20070, 0)); _mf = true;
            // MF_LOW_LATENCY: the encoder emits each picture as it arrives (no look-ahead / reordering queue), so a key
            // frame forced just before a sample is submitted applies to that sample, not to one still queued inside.
            H(MFCreateAttributes(out attributes, 2));
            U(attributes, "9c27891a-ed7a-40e1-88e8-b22727a024ee", 1);
            _callbackUnknown = Marshal.GetIUnknownForObject(_callback);
            Guid callbackKey = new("48cb183e-7b0b-46f4-822e-5e1d2dda4354"); // MF_SINK_WRITER_ASYNC_CALLBACK
            H(Fn<UnknownSet>(attributes, (int)AttributesSlot.SetUnknown)(attributes, ref callbackKey, _callbackUnknown));
            H(MFCreateSinkWriterFromURL(path, IntPtr.Zero, attributes, out _writer));
            output = Type(width, height, "34363248-0000-0010-8000-00aa00389b71");
            U(output, "20332624-fb0d-4d9e-bd0d-cbf6786c102e", 2_000_000);
            H(Fn<AddStream>(_writer, (int)WriterSlot.AddStream)(_writer, output, out _stream));
            // Native encoder input avoids the sink writer's RGB video processor: it inserts/interpolates frames
            // across gaps and privacy transitions (40 submitted samples became 44 in the gap fixture).
            input = Type(width, height, "3231564e-0000-0010-8000-00aa00389b71"); // NV12
            U(input, "644b4e48-1e02-4516-b0eb-c01ca9d49ac6", width);
            H(Fn<Input>(_writer, (int)WriterSlot.SetInputMediaType)(_writer, _stream, input, IntPtr.Zero));
            H(Fn<Plain>(_writer, (int)WriterSlot.BeginWriting)(_writer));
            // Key-frame forcing is best effort: without ICodecAPI the recording continues and the evidence says so;
            // the checker's decoded-black rule judges privacy samples either way.
            Guid service = Guid.Empty, codecApi = new("901db4c7-31ce-41a2-85dc-8fa0bf41b8da");
            if (Fn<Service>(_writer, (int)WriterSlot.GetServiceForStream)(_writer, _stream, ref service, ref codecApi, out _codec) < 0)
            { _codec = IntPtr.Zero; KeyFrameForcing = "unavailable"; }
            else KeyFrameForcing = EncoderMode() switch
            {
                true => "requested-synchronous-encoder-after-drain-marker",
                false => "requested-asynchronous-encoder-after-drain-marker-placement-unverified",
                null => "requested-encoder-mode-unknown-after-drain-marker-placement-unverified",
            };
        }
        catch { Dispose(); throw; }
        finally { Release(input); Release(output); Release(attributes); }
    }
    /// <summary>How the first black (privacy) sample after visible ones was forced to a key frame. The writer waits for a
    /// drain marker (every earlier sample has left the encoder) and then requests the key frame, so with a synchronous
    /// encoder (<c>requested-synchronous-encoder-after-drain-marker</c>) it applies to that sample. With an asynchronous or
    /// undeterminable encoder mode the value ends in <c>placement-unverified</c>; <c>unavailable</c> means no ICodecAPI,
    /// <c>failed</c> a refused request or a marker that did not return. The checker's decoded-black rule judges the result.</summary>
    public string KeyFrameForcing { get; private set; } = "unavailable";
    /// <summary>True when the encoder transform is synchronous (no MF_TRANSFORM_ASYNC), false when asynchronous, null
    /// when the sink writer does not expose it.</summary>
    private bool? EncoderMode()
    {
        IntPtr extended = IntPtr.Zero, transform = IntPtr.Zero, attributes = IntPtr.Zero;
        try
        {
            Guid writerEx = new("588d72ab-5bc1-496a-8714-b70617141b25");
            if (Marshal.QueryInterface(_writer, in writerEx, out extended) < 0) return null;
            Guid encoder = new("f79eac7d-e545-4387-bdee-d647d7bde42a"); // MFT_CATEGORY_VIDEO_ENCODER
            for (int index = 0; index < 8; index++)
            {
                Release(transform); transform = IntPtr.Zero;
                if (Fn<TransformFor>(extended, 14)(extended, _stream, index, out Guid category, out transform) < 0) return null;
                if (category != encoder) continue;
                if (Fn<GetAttributes>(transform, 8)(transform, out attributes) < 0 || attributes == IntPtr.Zero) return null;
                Guid async = new("f81a699a-649a-497d-8c73-29f8fed6ad7a"); // MF_TRANSFORM_ASYNC
                return Fn<U32Get>(attributes, (int)AttributesSlot.GetUINT32)(attributes, ref async, out int value) < 0 || value == 0;
            }
            return null;
        }
        catch { return null; }
        finally { Release(attributes); Release(transform); Release(extended); }
    }
    public void Write(byte[] rgb32, long time100ns, long duration100ns)
    {
        if (_finished || _writer == IntPtr.Zero) throw new ObjectDisposedException(nameof(MediaFoundationWriter));
        if (rgb32.Length != _bytes || time100ns < 0 || duration100ns <= 0) throw new ArgumentException("Invalid RGB32 sample");
        IntPtr buffer = IntPtr.Zero, sample = IntPtr.Zero;
        try
        {
            // Inter prediction can leave the previous picture's residual at a privacy boundary. A fresh intra
            // picture for black input prevents that residual; the checker still uses the unchanged range-8 rule.
            bool black = true;
            for (int i = 0; i < rgb32.Length && black; i += 4) black = (rgb32[i] | rgb32[i + 1] | rgb32[i + 2]) == 0;
            if (black && !_previousBlack && _codec != IntPtr.Zero)
            {
                // Wait until every earlier sample has left the encoder, so the request below can only apply to this one.
                H(Fn<Marker>(_writer, (int)WriterSlot.PlaceMarker)(_writer, _stream, IntPtr.Zero));
                if (!_callback.Marker.WaitOne(TimeSpan.FromSeconds(5))) KeyFrameForcing = "failed";
            }
            _previousBlack = black;
            if (black && _codec != IntPtr.Zero)
            {
                Guid forceKey = new("398c1b98-8353-475a-9ef2-8f265d260345");
                var value = new CodecValue { Type = 19, Value = 1 }; // VT_UI4
                if (Fn<CodecSet>(_codec, 9)(_codec, ref forceKey, ref value) < 0) KeyFrameForcing = "failed";
            }
            byte[] nv12 = ToNv12(rgb32, _width, _height);
            H(MFCreateMemoryBuffer(nv12.Length, out buffer));
            H(Fn<Lock>(buffer, (int)BufferSlot.Lock)(buffer, out var pixels, out _, out _));
            try { Marshal.Copy(nv12, 0, pixels, nv12.Length); }
            finally { H(Fn<Plain>(buffer, (int)BufferSlot.Unlock)(buffer)); }
            H(Fn<Length>(buffer, (int)BufferSlot.SetCurrentLength)(buffer, nv12.Length));
            H(MFCreateSample(out sample));
            H(Fn<AddBuffer>(sample, (int)SampleSlot.AddBuffer)(sample, buffer));
            H(Fn<TimeSet>(sample, (int)SampleSlot.SetSampleTime)(sample, time100ns));
            H(Fn<TimeSet>(sample, (int)SampleSlot.SetSampleDuration)(sample, duration100ns));
            H(Fn<WriteSample>(_writer, (int)WriterSlot.WriteSample)(_writer, _stream, sample));
        }
        finally { Release(sample); Release(buffer); }
    }
    private static byte[] ToNv12(byte[] rgb, int width, int height)
    {
        // BT.709 studio-range YCbCr, 4:2:0 with each chroma sample averaging its four source pixels. BT.709 is what a
        // decoder assumes for the harness's window-sized (HD) recordings when it ignores the signalled matrix, as the
        // Media Foundation decoder was measured to do; a clip under 576 lines may decode with BT.601 colours (greys exact).
        var yuv = new byte[checked(width * height * 3 / 2)];
        static byte Clip(int value) => (byte)Math.Clamp(value, 0, 255);
        for (int y = 0; y < height; y += 2)
            for (int x = 0; x < width; x += 2)
            {
                int red = 0, green = 0, blue = 0;
                for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
                {
                    int pixel = (y + dy) * width + x + dx, at = pixel * 4;
                    int b = rgb[at], g = rgb[at + 1], r = rgb[at + 2];
                    yuv[pixel] = Clip(((47 * r + 157 * g + 16 * b + 128) >> 8) + 16);
                    red += r; green += g; blue += b;
                }
                int chroma = width * height + y / 2 * width + x;
                yuv[chroma] = Clip(((-26 * red - 87 * green + 113 * blue + 512) >> 10) + 128);
                yuv[chroma + 1] = Clip(((112 * red - 102 * green - 10 * blue + 512) >> 10) + 128);
            }
        return yuv;
    }
    public void Finish()
    {
        if (_finished) return;
        H(Fn<Plain>(_writer, (int)WriterSlot.Finalize)(_writer)); _finished = true;
        if (!_callback.Finalized.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Video finalisation did not complete");
        H(_callback.FinalizeStatus);
    }
    public void Dispose()
    {
        Release(_codec); _codec = IntPtr.Zero;
        // An unfinished writer releases without waiting for its callback.
        Release(_writer); _writer = IntPtr.Zero;
        Release(_callbackUnknown); _callbackUnknown = IntPtr.Zero;
        if (_mf) { MFShutdown(); _mf = false; }
        if (_com) { CoUninitialize(); _com = false; }
    }
    private static IntPtr Type(int width, int height, string subtype)
    {
        H(MFCreateMediaType(out var type));
        try
        {
            G(type, "48eba18e-f8c9-4687-bf11-0a74c9f96a8f", "73646976-0000-0010-8000-00aa00389b71");
            G(type, "f7e34c9a-42e8-4714-b74b-cb29d72c35e5", subtype);
            Pair(type, "1652c33d-d6b2-4012-b834-72030849a37d", width, height);
            Pair(type, "c459a2e8-3d2c-4e44-b132-fee5156c7bb0", 10, 1);
            Pair(type, "c6376a1e-8d0a-4027-be45-6d9a0ad39bb6", 1, 1);
            U(type, "e2724bb8-e676-4806-b4b2-a8d6efb44ccd", 2);
            // ToNv12 converts with BT.709 studio range; signalled on both types for decoders that honour it.
            U(type, "3e23d450-2c75-4d25-a00e-b91670d12327", 1); // MF_MT_YUV_MATRIX = MFVideoTransferMatrix_BT709
            U(type, "c21b8ee5-b956-4071-8daf-325edf5cab11", 2); // MF_MT_VIDEO_NOMINAL_RANGE = MFNominalRange_16_235
            return type;
        }
        catch { Release(type); throw; }
    }
    private static void Release(IntPtr value) { if (value != IntPtr.Zero) Marshal.Release(value); }
    private static void H(int hr) => Marshal.ThrowExceptionForHR(hr);
    private static T Fn<T>(IntPtr p, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(p), slot * IntPtr.Size));
    private static void G(IntPtr p, string key, string value) { Guid k = new(key), v = new(value); H(Fn<GuidSet>(p, (int)AttributesSlot.SetGUID)(p, ref k, ref v)); }
    private static void U(IntPtr p, string key, int value) { Guid k = new(key); H(Fn<U32Set>(p, (int)AttributesSlot.SetUINT32)(p, ref k, value)); }
    private static void Pair(IntPtr p, string key, int a, int b) { Guid k = new(key); H(Fn<U64Set>(p, (int)AttributesSlot.SetUINT64)(p, ref k, ((long)a << 32) | (uint)b)); }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Plain(IntPtr p);
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct CodecValue
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public uint Value;
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Service(IntPtr p, int stream, ref Guid service, ref Guid iid, out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int UnknownSet(IntPtr p, ref Guid key, IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Marker(IntPtr p, int stream, IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int TransformFor(IntPtr p, int stream, int index, out Guid category, out IntPtr transform);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetAttributes(IntPtr p, out IntPtr attributes);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int U32Get(IntPtr p, ref Guid key, out int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CodecSet(IntPtr p, ref Guid key, ref CodecValue value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GuidSet(IntPtr p, ref Guid key, ref Guid value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int U32Set(IntPtr p, ref Guid key, int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int U64Set(IntPtr p, ref Guid key, long value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int AddStream(IntPtr p, IntPtr type, out int stream);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Input(IntPtr p, int stream, IntPtr type, IntPtr attributes);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int WriteSample(IntPtr p, int stream, IntPtr sample);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int TimeSet(IntPtr p, long value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int AddBuffer(IntPtr p, IntPtr buffer);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Lock(IntPtr p, out IntPtr bytes, out int maximum, out int current);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Length(IntPtr p, int length);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, int mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFStartup(int version, int flags);
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFShutdown();
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFCreateMediaType(out IntPtr type);
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFCreateSample(out IntPtr sample);
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFCreateMemoryBuffer(int size, out IntPtr buffer);
    [DllImport("mfplat.dll")] private static extern int MFCreateAttributes(out IntPtr attributes, int count);
    [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int MFCreateSinkWriterFromURL(string url, IntPtr stream, IntPtr attributes, out IntPtr writer);
}
