using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Controls;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;
using Gcam.Studio.Views;

namespace Gcam.Studio.RenderTests;

public sealed partial class PlotViewRenderTests
{
    private void RenderWindows(string theme)
    {
        // Share the existing STA/Application lifetime: WPF permits only one Application per AppDomain.
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries.Clear();
        foreach (string file in new[] { $"Tokens.{theme}", "Metrics", "Typography", "Controls" })
            dictionaries.Add(new ResourceDictionary
            { Source = new Uri($"/Gcam.Studio;component/Themes/{file}.xaml", UriKind.Relative) });

        var acquisition = new FixtureAcquisition();
        var model = new MainViewModel(acquisition, new FixtureTheme(Enum.Parse<AppTheme>(theme)), new FixtureSpectrum());
        // The fake publishes synchronously: no MC, timers, worker thread or dispatcher wait.
        model.StartCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.Equal(RunState.Completed, model.State);
        Assert.Same(acquisition.Snapshot, model.Snapshot);
        Assert.NotEmpty(model.Spectrum.Series);

        foreach (var size in new[] { new Size(1280, 800), new Size(1440, 900) })
        foreach (var workspace in model.Workspaces)
        {
            model.SelectedWorkspace = workspace;
            // Construct XAML only. Never Show(), Run(), create an HWND, or send desktop input.
            var window = new MainWindow();
            var content = (FrameworkElement)window.Content;
            window.Content = null;
            content.Resources.MergedDictionaries.Add(window.Resources);
            content.DataContext = model;
            TextElement.SetFontFamily(content, (FontFamily)Application.Current.FindResource("Font.UI"));
            content.UseLayoutRounding = true;
            content.SnapsToDevicePixels = true;
            System.Windows.Media.TextOptions.SetTextFormattingMode(content, TextFormattingMode.Display);
            var root = new Border
            {
                Background = (Brush)Application.Current.FindResource("Brush.Bg.Canvas"),
                Child = new AdornerDecorator { Child = content }
            };
            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();
            // Flush binding work only; no input is queued or synthesized.
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            root.UpdateLayout();

            foreach (var heatmap in Descendants(root).OfType<HeatmapView>())
            {
                // Exercise the actual readout path without a mouse event (same API used by adorners).
                var point = heatmap.MmToScreen(new Vec2(0, 0));
                typeof(HeatmapView).GetMethod("HoverAt", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(heatmap, [point]);
                Assert.Contains(heatmap.ValueUnit, heatmap.Readout);
                Assert.DoesNotContain("E+", heatmap.Readout);
            }
            foreach (string id in new[] { "AcquisitionLiveTime", "AcquisitionSpeed" })
            {
                var input = Assert.Single(Descendants(root).OfType<TextBox>(),
                    t => AutomationProperties.GetAutomationId(t) == id);
                Assert.Equal(80, input.ActualWidth);
                Assert.Equal(28, input.ActualHeight);
            }
            root.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string directory = Path.Combine(RepositoryRoot(), "docs", "assets", "studio-render");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,
                $"{workspace.Title.ToLowerInvariant()}-{theme.ToLowerInvariant()}-{size.Width:0}x{size.Height:0}.png");
            using var stream = File.Create(path);
            encoder.Save(stream);
            output.WriteLine(path);
            // Detached RadioButtons have no window group scope. Remove old bindings before
            // changing the shared workspace so an earlier tree cannot uncheck the new picker.
            content.DataContext = null;
            root.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class FixtureTheme(AppTheme theme) : IThemeService
    {
        public AppTheme Current { get; private set; } = theme;
        public void Apply(AppTheme value) => Current = value;
    }

    private sealed class FixtureAcquisition : IAcquisitionService
    {
        public AcquisitionSnapshot Snapshot { get; } = CreateSnapshot();
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
            double liveTimeS, double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0)
            => new FixtureSession(Snapshot);

        private static AcquisitionSnapshot CreateSnapshot()
        {
            var flood = new DetectorImage(30, 30);
            var recon = new DetectorImage(41, 41);
            for (int y = 0; y < 30; y++)
            for (int x = 0; x < 30; x++) flood[x, y] = (x * 7 + y * 11) % 26;
            for (int y = 0; y < 41; y++)
            for (int x = 0; x < 41; x++)
                recon[x, y] = Math.Round(3077 * Gaussian(x, 20, 3) * Gaussian(y, 20, 3)
                    - 1027 * Gaussian(x, 9, 5) * Gaussian(y, 30, 5));
            recon[20, 20] = 3077;
            var imaging = new ImagingResult(flood.ReadOnlyCopy(), -8.7, 0.6,
                recon.ReadOnlyCopy(), -56, 2.8, new SourceEstimate(new Gcam.Core.Vector3(0, 0, 1080), 1),
                6463, TimeSpan.FromMilliseconds(25));
            return new(60, 6463, 6463.0 / 60, 10, false, imaging,
                Array.AsReadOnly(new[] { new DetectedEvent(15, 15, 661.7, 1) }), TimeSpan.FromMilliseconds(25), true)
                { Detector = new DetectorSettings() };
        }
    }

    private sealed class FixtureSession(AcquisitionSnapshot snapshot) : IAcquisitionSession
    {
        public async IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            cancellationToken.ThrowIfCancellationRequested();
            yield return snapshot;
        }
        public void Stop() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FixtureSpectrum : ISpectrumService
    {
        public double Resolution662 => 0.0457;
        public double ResolvingTimeS => 730e-9;
        public Task<Gcam.Studio.Core.Services.SpectrumView> ProcessAsync(Guid acquisitionId,
            IReadOnlyList<DetectedEvent> events, IReadOnlyList<SpectrumLine> lines, SpectrumSettings settings,
            int seed = 909, CancellationToken cancellationToken = default)
        {
            // Analytic drawing fixture, not a physics result. Peaks exercise the 10,000 log tick.
            const int bins = 256;
            const double width = 3;
            var centres = Enumerable.Range(0, bins).Select(i => (i + 0.5) * width).ToArray();
            var counts = centres.Select(x => Math.Round(11000 * Gaussian(x, 34, 5)
                + 1600 * Gaussian(x, 661.7, 14) + (x < 478 ? 80 * Math.Exp(-x / 300) : 0))).ToArray();
            SpectrumBand[] bands =
            [
                new([new("Cs-137", 32.1), new("Cs-137", 36.4)], 25.5, 43.5, 1094, 0.169),
                new([new("Cs-137", 661.7)], 616.3, 707.1, 1966, 0.304)
            ];
            return Task.FromResult(new Gcam.Studio.Core.Services.SpectrumView(centres, counts, bands,
                6463, 0, 0.473, Resolution662, ResolvingTimeS, FrontEndParts.Default.ToString(), TimeSpan.Zero)
                { BinEdgesKeV = Enumerable.Range(0, bins + 1).Select(i => i * width).ToArray() });
        }
    }
}
