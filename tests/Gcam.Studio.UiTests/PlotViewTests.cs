using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Threading;
using Gcam.Studio.Controls;
using Gcam.Studio.Core.Plotting;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

public sealed class PlotViewTests(ITestOutputHelper output)
{
    [DesktopFact]
    public void TenMillionSamples_ZoomAndResizeRedraw_Within16Milliseconds()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            Application? application = null;
            try
            {
                application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                // A real visible WPF host, production style and DPI, no render mocks or offscreen bitmap.
                var resources = new ResourceDictionary();
                foreach (string dictionary in new[] { "Tokens.Dark", "Controls" })
                    resources.MergedDictionaries.Add(new ResourceDictionary
                    { Source = new Uri($"/Gcam.Studio;component/Themes/{dictionary}.xaml", UriKind.Relative) });
                double[] samples = new double[10_000_000];
                for (int i = 0; i < samples.Length; i++) samples[i] = Math.Sin(i * 0.003) + (i % 997 == 0 ? 10 : 0);
                var plot = new PlotView { XLabel = "Sample", YLabel = "ADC", Series = [new PlotSeries("ADC", samples)] };
                AutomationProperties.SetAutomationId(plot, "PerformancePlot");
                AutomationProperties.SetName(plot, "ADC performance trace");
                AutomationProperties.SetHelpText(plot, "Plus and minus zoom; arrows pan; 0 resets.");
                window = new Window { Title = "GCAM PlotView performance gate", Width = 1280, Height = 800,
                    Resources = resources, Content = plot, Background = (System.Windows.Media.Brush)resources["Brush.Bg.Canvas"] };
                window.Show();
                PumpRender(plot, () => plot.InvalidateVisual());
                // JIT / font setup are warmed before measuring interactive redraws.
                for (int i = 0; i < 3; i++) PumpRender(plot, () => plot.ZoomAt(0.5, true));
                var zoom = new List<double>();
                var resize = new List<double>();
                var latency = new List<double>();
                for (int i = 0; i < 5; i++)
                {
                    latency.Add(PumpRender(plot, () => plot.ZoomAt(0.5, i % 2 == 0)));
                    zoom.Add(plot.LastRedrawMilliseconds);
                    latency.Add(PumpRender(plot, () => window.Width = i % 2 == 0 ? 1440 : 1280));
                    resize.Add(plot.LastRedrawMilliseconds);
                }
                var record = new { machine = Environment.MachineName, os = Environment.OSVersion.ToString(),
                    processors = Environment.ProcessorCount, date = DateTimeOffset.Now, samples = samples.Length,
                    dpi = System.Windows.Media.VisualTreeHelper.GetDpi(plot).PixelsPerDip,
                    zoomRedrawMs = zoom, resizeRedrawMs = resize, eventToRenderMs = latency,
                    metric = "CPU OnRender: axes, query, frozen geometry and drawing commands; excludes compositor / refresh wait" };
                string directory = Path.Combine(AppContext.BaseDirectory, "ui-runs", "plot-performance");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "performance.json");
                File.WriteAllText(path, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
                output.WriteLine(File.ReadAllText(path));
                output.WriteLine($"performance record: {path}");
                Assert.All(zoom, ms => Assert.True(ms <= 16, $"zoom redraw {ms:F3} ms > 16 ms"));
                Assert.All(resize, ms => Assert.True(ms <= 16, $"resize redraw {ms:F3} ms > 16 ms"));

                var peer = UIElementAutomationPeer.CreatePeerForElement(plot);
                Assert.NotNull(peer);
                Assert.Equal("PlotView", peer.GetClassName());
                Assert.Equal("ADC performance trace", peer.GetName());
                plot.Focus();
                double before = plot.Zoom;
                PumpRender(plot, () => plot.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(plot), 0, Key.Add) { RoutedEvent = Keyboard.KeyDownEvent }));
                Assert.True(plot.Zoom > before);
                PumpRender(plot, plot.ResetView);
                Assert.Equal(1, plot.Zoom);
            }
            catch (Exception e) { failure = e; }
            finally { window?.Close(); application?.Shutdown(); Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "plot desktop thread did not finish");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static double PumpRender(PlotView plot, Action change)
    {
        var frame = new DispatcherFrame();
        long start = Stopwatch.GetTimestamp();
        double elapsed = 0;
        void Rendered(object? sender, EventArgs e)
        {
            elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            frame.Continue = false;
        }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) => frame.Continue = false;
        long before = plot.RenderCount;
        plot.Redrawn += Rendered;
        timer.Start();
        try { change(); Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); plot.Redrawn -= Rendered; }
        Assert.True(plot.RenderCount > before, "no redraw within 5 seconds");
        return elapsed;
    }
}
