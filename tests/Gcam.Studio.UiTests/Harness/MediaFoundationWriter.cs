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
    private IntPtr _writer;
    private readonly int _stream, _bytes;
    private bool _mf, _com, _finished;
    public MediaFoundationWriter(string path, int width, int height)
    {
        if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("Video encoding requires x64");
        if (width <= 0 || height <= 0 || width % 2 != 0 || height % 2 != 0) throw new ArgumentOutOfRangeException(nameof(width));
        _bytes = checked(width * height * 4);
        IntPtr input = IntPtr.Zero, output = IntPtr.Zero;
        try
        {
            H(CoInitializeEx(IntPtr.Zero, 0)); _com = true;
            H(MFStartup(0x20070, 0)); _mf = true;
            H(MFCreateSinkWriterFromURL(path, IntPtr.Zero, IntPtr.Zero, out _writer));
            output = Type(width, height, "34363248-0000-0010-8000-00aa00389b71");
            U(output, "20332624-fb0d-4d9e-bd0d-cbf6786c102e", 2_000_000);
            H(Fn<AddStream>(_writer, (int)WriterSlot.AddStream)(_writer, output, out _stream));
            input = Type(width, height, "00000016-0000-0010-8000-00aa00389b71");
            U(input, "644b4e48-1e02-4516-b0eb-c01ca9d49ac6", width * 4);
            H(Fn<Input>(_writer, (int)WriterSlot.SetInputMediaType)(_writer, _stream, input, IntPtr.Zero));
            H(Fn<Plain>(_writer, (int)WriterSlot.BeginWriting)(_writer));
        }
        catch { Dispose(); throw; }
        finally { Release(input); Release(output); }
    }
    public void Write(byte[] rgb32, long time100ns, long duration100ns)
    {
        if (_finished || _writer == IntPtr.Zero) throw new ObjectDisposedException(nameof(MediaFoundationWriter));
        if (rgb32.Length != _bytes || time100ns < 0 || duration100ns <= 0) throw new ArgumentException("Invalid RGB32 sample");
        IntPtr buffer = IntPtr.Zero, sample = IntPtr.Zero;
        try
        {
            H(MFCreateMemoryBuffer(_bytes, out buffer));
            H(Fn<Lock>(buffer, (int)BufferSlot.Lock)(buffer, out var pixels, out _, out _));
            try { Marshal.Copy(rgb32, 0, pixels, _bytes); }
            finally { H(Fn<Plain>(buffer, (int)BufferSlot.Unlock)(buffer)); }
            H(Fn<Length>(buffer, (int)BufferSlot.SetCurrentLength)(buffer, _bytes));
            H(MFCreateSample(out sample));
            H(Fn<AddBuffer>(sample, (int)SampleSlot.AddBuffer)(sample, buffer));
            H(Fn<TimeSet>(sample, (int)SampleSlot.SetSampleTime)(sample, time100ns));
            H(Fn<TimeSet>(sample, (int)SampleSlot.SetSampleDuration)(sample, duration100ns));
            H(Fn<WriteSample>(_writer, (int)WriterSlot.WriteSample)(_writer, _stream, sample));
        }
        finally { Release(sample); Release(buffer); }
    }
    public void Finish()
    {
        if (_finished) return;
        H(Fn<Plain>(_writer, (int)WriterSlot.Finalize)(_writer)); _finished = true;
    }
    public void Dispose()
    {
        Release(_writer); _writer = IntPtr.Zero;
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
    [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int MFCreateSinkWriterFromURL(string url, IntPtr stream, IntPtr attributes, out IntPtr writer);
}
