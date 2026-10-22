using System.Diagnostics;
using System.Runtime.InteropServices;
using Gcam.Studio.UiTests.Harness.Observation;

namespace Gcam.Studio.UiTests;

public sealed class LivenessObserverTests
{
    [Theory]
    [InlineData(0.25)]
    [InlineData(10)]
    public void MessageOnlyWindow_AnswersThenBlocks_IndependentSamplerBreachesLimit(double limitSeconds)
    {
        using var window = new MessageWindow();
        using var current = Process.GetCurrentProcess();
        using var observer = new LivenessObserver(window.Hwnd, OwnedProcessIdentity.From(current),
            limitSeconds == 10 ? null : TimeSpan.FromSeconds(limitSeconds), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(30));
        Assert.True(SpinWait.SpinUntil(() => observer.Snapshot().SampleCount >= 3, 3000));
        Assert.True(observer.Snapshot().Passed);
        window.Block();
        Assert.Equal(limitSeconds, observer.Snapshot().LimitSeconds);
        Assert.True(SpinWait.SpinUntil(() => observer.Snapshot().Breached, TimeSpan.FromSeconds(limitSeconds + 3)));
        Assert.True(SpinWait.SpinUntil(() => observer.Failure.IsCompletedSuccessfully, 3000));
        window.Release.Set();
        Assert.True(SpinWait.SpinUntil(() => !observer.Snapshot().CurrentlyNotResponding, 3000));
        var result = observer.Snapshot();
        Assert.True(result.LongestNonResponseSeconds >= limitSeconds);
        Assert.False(result.Passed); Assert.Empty(result.Errors);
    }
    [PwshTheory]
    [InlineData(0, false, true, false)]
    [InlineData(17, false, true, true)]
    [InlineData(0, true, false, false)]
    [InlineData(17, true, false, true)]
    public void HelperExit_ClassifiesEarlyAndAbnormalIndependently(int code, bool mark, bool early, bool abnormal)
    {
        using var process = ObservationFixtures.Helper(code);
        try
        {
            using var observer = new LivenessObserver(IntPtr.Zero, OwnedProcessIdentity.From(process));
            if (mark) observer.MarkIntentionalClose();
            ObservationFixtures.Exit(process);
            if (!mark) observer.MarkIntentionalClose(); // A late mark must never clear an early exit.
            var result = observer.Snapshot();
            Assert.True(result.ExitCode == code, $"Expected {code}, got {result.ExitCode}; errors: {string.Join("; ", result.Errors)}");
            Assert.Equal(early, result.EarlyExit); Assert.Equal(abnormal, result.AbnormalExit);
        }
        finally { ObservationFixtures.Exit(process); }
    }
    [Fact]
    public void IdentityMismatch_IsRejectedBeforeSampling()
    {
        using var current = Process.GetCurrentProcess();
        Assert.Throws<InvalidOperationException>(() => new LivenessObserver(IntPtr.Zero,
            OwnedProcessIdentity.From(current) with { StartedAtUtc = DateTimeOffset.UtcNow.AddDays(-1) }));
    }
    [Fact]
    public void IntentionalClose_WindowCanDisappearBeforeProcess_WithoutAFalseProbeError()
    {
        using var window = new MessageWindow(); using var current = Process.GetCurrentProcess();
        using var observer = new LivenessObserver(window.Hwnd, OwnedProcessIdentity.From(current), samplePeriod: TimeSpan.FromMilliseconds(20));
        Assert.True(SpinWait.SpinUntil(() => observer.Snapshot().SampleCount > 0, 3000));
        observer.MarkIntentionalClose(); window.RequestClose();
        Assert.True(SpinWait.SpinUntil(() => window.Closed, 3000));
        Thread.Sleep(100); Assert.True(observer.Snapshot().Passed); Assert.False(observer.Failure.IsCompleted);
    }

    /// <summary>A message-only window on its own thread that can stop answering on request (also the teardown helper).</summary>
    internal sealed class MessageWindow : IDisposable
    {
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _ready = new();
        private readonly ManualResetEventSlim _blocking = new();
        public readonly ManualResetEventSlim Release = new();
        private readonly WindowProc _proc;
        private Exception? _error;
        public IntPtr Hwnd { get; private set; }
        public bool Closed => !_thread.IsAlive;
        public MessageWindow()
        {
            _proc = Procedure;
            _thread = new(() =>
            {
                string name = "GcamMessageOnly_" + Guid.NewGuid().ToString("N");
                IntPtr instance = GetModuleHandle(null);
                try
                {
                    var type = new WindowClass { Procedure = _proc, ClassName = name, Instance = instance };
                    if (RegisterClass(ref type) == 0) throw new InvalidOperationException("RegisterClass failed");
                    try
                    {
                        Hwnd = CreateWindowEx(0, name, "", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, instance, IntPtr.Zero);
                        if (Hwnd == IntPtr.Zero) throw new InvalidOperationException("Message-only window failed");
                        _ready.Set();
                        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0) DispatchMessage(ref message);
                    }
                    finally { UnregisterClass(name, instance); }
                }
                catch (Exception e) { _error = e; _ready.Set(); }
            }) { IsBackground = true };
            _thread.Start(); Assert.True(_ready.Wait(TimeSpan.FromSeconds(5))); Assert.Null(_error);
        }
        private IntPtr Procedure(IntPtr hwnd, uint message, UIntPtr w, IntPtr l)
        {
            if (message == 0x8001) { _blocking.Set(); Release.Wait(); return IntPtr.Zero; }
            if (message == 0x10) { DestroyWindow(hwnd); PostQuitMessage(0); return IntPtr.Zero; }
            return DefWindowProc(hwnd, message, w, l);
        }
        public void Block() { PostMessage(Hwnd, 0x8001, UIntPtr.Zero, IntPtr.Zero); Assert.True(_blocking.Wait(TimeSpan.FromSeconds(3))); }
        public void RequestClose() => PostMessage(Hwnd, 0x10, UIntPtr.Zero, IntPtr.Zero);
        public void Dispose()
        {
            Release.Set(); PostMessage(Hwnd, 0x10, UIntPtr.Zero, IntPtr.Zero); Assert.True(_thread.Join(TimeSpan.FromSeconds(5)));
            _ready.Dispose(); _blocking.Dispose(); Release.Dispose(); GC.KeepAlive(_proc);
        }
        private delegate IntPtr WindowProc(IntPtr hwnd, uint message, UIntPtr w, IntPtr l);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
        {
            public uint Style; public WindowProc Procedure; public int ClassExtra, WindowExtra;
            public IntPtr Instance, Icon, Cursor, Background; public string? MenuName; public string ClassName;
        }
        [StructLayout(LayoutKind.Sequential)] private struct Message
        {
            public IntPtr Hwnd; public uint Id; public UIntPtr W; public IntPtr L; public uint Time; public int X, Y; public uint Private;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClass(ref WindowClass type);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string name, IntPtr instance);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint exStyle, string name, string title, uint style,
            int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetMessage(out Message message, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DispatchMessage(ref Message message);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, UIntPtr w, IntPtr l);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, UIntPtr w, IntPtr l);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    }
}
