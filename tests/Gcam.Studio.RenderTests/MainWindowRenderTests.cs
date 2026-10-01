using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Data;
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
    private void RenderWindows(string theme, bool mixed = false)
    {
        // Share the existing STA/Application lifetime: WPF permits only one Application per AppDomain.
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries.Clear();
        foreach (string file in new[] { $"Tokens.{theme}", "Metrics", "Typography", "Controls" })
            dictionaries.Add(new ResourceDictionary
            { Source = new Uri($"/Gcam.Studio;component/Themes/{file}.xaml", UriKind.Relative) });

        var acquisition = new FixtureAcquisition();
        var model = new MainViewModel(acquisition, new FixtureTheme(Enum.Parse<AppTheme>(theme)), new FixtureSpectrum(),
            mixed ? new FixtureImaging() : null);
        if (mixed)
        {
            model.Sources[0].X = 15; model.Sources[0].Y = 8;
            model.AddSourceCommand.Execute(null);
            model.Sources[1].Isotope = "Co-60";
            model.Sources[1].X = -15; model.Sources[1].Y = -8;
            model.Imaging.Strip = true;
        }
        // The fake publishes synchronously: no MC, timers, worker thread or dispatcher wait.
        model.StartCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.Equal(RunState.Completed, model.State);
        Assert.Same(acquisition.Snapshot, model.Snapshot);
        Assert.NotEmpty(model.Spectrum.Series);

        foreach (var size in new[] { new Size(1280, 800), new Size(1440, 900) })
        foreach (var workspace in model.Workspaces)
        foreach (string isotope in mixed ? (workspace == model.Imaging ? new[] { "All", "Cs-137" } : Array.Empty<string>()) : new[] { "All" })
        {
            model.SelectedWorkspace = workspace;
            model.Imaging.SelectedIsotope = isotope;
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

            if (mixed)
            {
                var selector = Assert.Single(Descendants(root).OfType<ComboBox>(),
                    c => AutomationProperties.GetAutomationId(c) == "Imaging.Channel");
                var options = Assert.Single(Descendants(root).OfType<ImagingOptionsPanel>());
                var itemsBinding = BindingOperations.GetBindingExpression(selector, ItemsControl.ItemsSourceProperty);
                Assert.Null(BindingOperations.GetBinding(options, FrameworkElement.DataContextProperty));
                Assert.Same(model.Imaging, options.DataContext);
                Assert.Equal(BindingStatus.Active, itemsBinding!.Status);
                Assert.Same(model.Imaging, itemsBinding.DataItem);
                output.WriteLine($"Imaging.Channel Isotopes source={itemsBinding.DataItem.GetType().FullName}; status={itemsBinding.Status}; options inherit workspace DataContext");
                Assert.Equal(new[] { "All", "Cs-137", "Co-60" }, selector.Items.Cast<string>());
                Assert.Equal(isotope, selector.SelectedItem);
                var recon = Assert.Single(Descendants(root).OfType<HeatmapView>(),
                    h => h.Name == "Recon");
                Assert.Same(model.Imaging.Result!.Reconstruction, recon.Image);
                Assert.Equal(isotope == "All" ? 2 : 1,
                    MeasurementOverlay.GetFoundPeaks(recon)!.Cast<ImagingPeak>().Count());
                // Detached render trees do not get a window Loaded event; attach production adorners explicitly.
                foreach (var heatmap in Descendants(root).OfType<HeatmapView>().ToArray())
                    typeof(MeasurementOverlay).GetMethod("OnLoaded", BindingFlags.Static | BindingFlags.NonPublic)!
                        .Invoke(null, [heatmap, new RoutedEventArgs(FrameworkElement.LoadedEvent)]);
                root.UpdateLayout();
            }
            // Flush binding work only; no input is queued or synthesized.
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            root.UpdateLayout();

            var fcfov = Assert.Single(Descendants(root).OfType<TextBlock>(),
                t => BindingOperations.GetBinding(t, TextBlock.TextProperty)?.Path.Path == "DataContext.FcfovHalfMm");
            Assert.Equal($"± {model.FcfovHalfMm:0.#} mm", fcfov.Text);
            var pickers = Descendants(root).OfType<RadioButton>().Where(
                b => AutomationProperties.GetAutomationId(b).StartsWith("Workspace.", StringComparison.Ordinal)).ToArray();
            Assert.Equal(model.Workspaces.Count, pickers.Length);
            foreach (var picker in pickers)
                Assert.Same(model.ActivateWorkspaceCommand, picker.Command);
            var sourceEditor = Assert.Single(Descendants(root).OfType<StackPanel>(),
                p => ReferenceEquals(p.DataContext, model.SelectedSource));
            Assert.Equal(model.IsIdle, sourceEditor.IsEnabled);
            Assert.Equal(BindingStatus.Active,
                BindingOperations.GetBindingExpression(sourceEditor, UIElement.IsEnabledProperty)!.Status);

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
                $"{workspace.Title.ToLowerInvariant()}{(mixed ? "-mixed-" + isotope.ToLowerInvariant() : "")}-{theme.ToLowerInvariant()}-{size.Width:0}x{size.Height:0}.png");
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

    private sealed class FixtureImaging : IImagingService
    {
        public Task<ImagingView> ProcessAsync(Guid acquisitionId, AcquisitionSnapshot snapshot,
            IReadOnlyList<SceneSource> scene, OpticsSettings optics, ImagingSettings settings,
            CancellationToken cancellationToken = default)
        {
            // Analytic drawing fixture only; the service tests supply all physics evidence.
            var peaks = scene.Select(s => new ImagingPeak(s.Isotope, s.X + 0.5, s.Y + 0.3, 100)).ToArray();
            var channels = new List<ImagingChannel>();
            ImagingResult Image(IEnumerable<SceneSource> sources)
            {
                var recon = new DetectorImage(41, 41);
                for (int y = 0; y < 41; y++)
                for (int x = 0; x < 41; x++)
                    recon[x, y] = sources.Sum(s => 3000 * Gaussian(-56 + x * 2.8, s.X, 5)
                        * Gaussian(-56 + y * 2.8, s.Y, 5));
                return snapshot.Imaging with { Reconstruction = recon.ReadOnlyCopy() };
            }
            channels.Add(new("All", double.NaN, double.NaN, Image(scene), peaks));
            foreach (var s in scene)
                channels.Add(new(s.Isotope, s.Isotope == "Cs-137" ? 616 : 1110,
                    s.Isotope == "Cs-137" ? 707 : 1236, Image([s]), peaks.Where(p => p.Isotope == s.Isotope).ToArray()));
            return Task.FromResult(new ImagingView(channels,
                [new StripRatio("Cs-137", "Co-60", 100000, 11000, 40000)],
                TimeSpan.FromMilliseconds(12), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(35)));
        }
    }
}
