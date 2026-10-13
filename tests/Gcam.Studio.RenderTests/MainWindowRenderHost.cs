using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace Gcam.Studio.RenderTests;

/// <summary>The one STA thread and WPF <see cref="Application"/> of the main-window offscreen renders. WPF allows a single
/// Application per AppDomain, so the split render tests share this host (an xUnit collection fixture) and each runs its
/// captures on its dispatcher, under its own deadlock deadline. Started on first use: skipped tests start nothing. No
/// Studio App, Run(), window, HWND or desktop input.</summary>
public sealed class MainWindowRenderHost : IDisposable
{
    /// <summary>Deadlock deadline of one render test (unchanged from the former single test).</summary>
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);
    private readonly object _gate = new();
    private Thread? _thread;
    private Dispatcher? _dispatcher;

    private Dispatcher Dispatcher()
    {
        lock (_gate)
        {
            if (_dispatcher is { } running) return running;
            using var ready = new ManualResetEventSlim();
            _thread = new Thread(() =>
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
                System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
                // Base WPF resource infrastructure only.
                _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                ready.Set();
                System.Windows.Threading.Dispatcher.Run();
            }) { IsBackground = true };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            ready.Wait();
            return _dispatcher!;
        }
    }

    /// <summary>Runs one test's captures on the render thread; fails when they exceed <see cref="Deadline"/> and rethrows
    /// their own failure on the test thread.</summary>
    public void Run(Action render)
    {
        Exception? failure = null;
        var operation = Dispatcher().InvokeAsync(() =>
        {
            // As on the former plain STA thread: no synchronization context while rendering. The captures block on async
            // commands (GetAwaiter().GetResult()); a dispatcher context would post their continuations to this very
            // thread and deadlock.
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try { render(); }
            catch (Exception ex) { failure = ex; }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        });
        Assert.True(operation.Task.Wait(Deadline), "Offscreen rendering timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public void Dispose()
    {
        if (_dispatcher is not { } dispatcher) return;
        // Never block here: a render stuck past its deadline must not hang the test run (the thread is a background thread).
        dispatcher.InvokeAsync(() => Application.Current?.Shutdown()).Wait(TimeSpan.FromSeconds(10));
        dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        _thread!.Join(TimeSpan.FromSeconds(10));
    }
}

/// <summary>The main-window render tests: one shared <see cref="MainWindowRenderHost"/>, so they run one after the other on
/// its single render thread (the Application is per AppDomain).</summary>
[CollectionDefinition(Name)]
public sealed class MainWindowRenderCollection : ICollectionFixture<MainWindowRenderHost>
{
    public const string Name = "Main window renders";
}
