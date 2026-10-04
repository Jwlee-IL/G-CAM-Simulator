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
    private void RenderWindows(string theme, bool mixed = false, bool? opticsExpanded = null, bool crowded = false)
    {
        // Share the existing STA/Application lifetime: WPF permits only one Application per AppDomain.
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries.Clear();
        foreach (string file in new[] { $"Tokens.{theme}", "Metrics", "Typography", "Controls" })
            dictionaries.Add(new ResourceDictionary
            { Source = new Uri($"/Gcam.Studio;component/Themes/{file}.xaml", UriKind.Relative) });

        var acquisition = new FixtureAcquisition();
        var model = new MainViewModel(acquisition, new FixtureTheme(Enum.Parse<AppTheme>(theme)), new FixtureSpectrum(mixed),
            mixed ? new FixtureImaging() : null, detectorFace: new Gcam.Studio.Services.DetectorFaceService())
            { SeedText = "12345", AmbientDoseRateMicroSvPerHour = 0 }; // fixed seed: the status line and the PNGs are reproducible (E-8)
        if (mixed)
        {
            model.Sources[0].X = crowded ? -20 : 15; model.Sources[0].Y = crowded ? 0 : 8;
            model.AddSourceCommand.Execute(null);
            model.Sources[1].Isotope = "Co-60";
            model.Sources[1].X = crowded ? 14 : -15; model.Sources[1].Y = crowded ? 0 : -8;
            model.Imaging.Strip = true;
        }
        // The fake publishes synchronously: no MC, timers, worker thread or dispatcher wait.
        model.StartCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.Equal(RunState.Completed, model.State);
        Assert.Same(acquisition.Snapshot, model.Snapshot);
        Assert.NotEmpty(model.Spectrum.Series);

        if (opticsExpanded is { } expanded)
        {
            model.IsOpticsExpanded = model.IsDetectorExpanded = expanded;
            model.Imaging.FocalPlane = "800";
            model.Imaging.WhenUpdated.GetAwaiter().GetResult();
        }
        foreach (var size in new[] { new Size(1280, 800), new Size(1440, 900) })
        foreach (var workspace in model.Workspaces)
        foreach (string isotope in mixed ? (workspace == model.Imaging ? (crowded ? ["All"] : new[] { "All", "Cs-137" })
            : workspace == model.Spectrum && opticsExpanded is null && !crowded ? ["All"] : Array.Empty<string>()) : new[] { "All" })
        {
            model.SelectedWorkspace = workspace;
            model.Imaging.SelectedIsotope = isotope;
            // Construct XAML only. Never Show(), Run(), create an HWND, or send desktop input.
            var (root, content) = DetachMainWindow(model, size);
            // A collapsed Expander has no input controls in the visual tree. Check the expanded fixtures.
            if (model.IsDetectorExpanded)
            {
                var ambientDose = Assert.Single(Descendants(root).OfType<TextBox>(),
                    box => AutomationProperties.GetAutomationId(box) == "Acquisition.AmbientDose");
                var ambientBound = Assert.Single(Descendants(root).OfType<ComboBox>(),
                    box => AutomationProperties.GetAutomationId(box) == "Acquisition.AmbientBound");
                Assert.Equal(model.CanEditInputs, ambientDose.IsEnabled);
                Assert.Equal(model.CanEditInputs, ambientBound.IsEnabled);
                Assert.Equal(model.AmbientGeometry, ambientBound.SelectedItem);
            }

            if (mixed && workspace == model.Imaging)
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

            if (workspace == model.Spectrum) VerifyEmissionTable(root, model.Spectrum);
            var seed = Assert.Single(Descendants(root).OfType<TextBox>(),
                t => AutomationProperties.GetAutomationId(t) == "AcquisitionSeed");
            Assert.False(seed.IsEnabled);
            Assert.Equal(((SolidColorBrush)Application.Current.FindResource("Brush.Text.Disabled")).Color,
                ((SolidColorBrush)seed.Foreground).Color);
            Assert.Equal(((SolidColorBrush)Application.Current.FindResource("Brush.Bg.Surface")).Color,
                ((SolidColorBrush)Descendants(seed).OfType<Border>().First().Background).Color);

            if (workspace == model.Imaging)
            {
                var geometry = Assert.Single(Descendants(root).OfType<TextBlock>(),
                    t => AutomationProperties.GetAutomationId(t) == "Imaging.Geometry");
                Assert.Equal(model.Imaging.GeometryText, geometry.Text);
                var focus = Assert.Single(Descendants(root).OfType<TextBox>(),
                    t => AutomationProperties.GetAutomationId(t) == "Imaging.FocalPlane");
                Assert.Equal(model.Imaging.FocalPlane, focus.Text);
            }
            var scene = Descendants(root).OfType<ScrollViewer>().First(v => v.Content is StackPanel p
                && Descendants(p).OfType<Expander>().Any(e => AutomationProperties.GetAutomationId(e) == "Optics.Section"));
            output.WriteLine($"scene panel overflow {scene.ScrollableHeight:F0} px ({workspace.Title}, {size.Width:0}x{size.Height:0}, optics expanded {model.IsOpticsExpanded})");
            // Default state (sections as the app starts them) fits the minimum window without a scroll bar.
            if (opticsExpanded is null) Assert.Equal(0, scene.ScrollableHeight);
            var section = Assert.Single(Descendants(root).OfType<Expander>(),
                e => AutomationProperties.GetAutomationId(e) == "Optics.Section");
            Assert.Equal(model.IsOpticsExpanded, section.IsExpanded);
            var pickers = Descendants(root).OfType<RadioButton>().Where(
                b => AutomationProperties.GetAutomationId(b).StartsWith("Workspace.", StringComparison.Ordinal)).ToArray();
            Assert.Equal(model.Workspaces.Count, pickers.Length);
            foreach (var picker in pickers)
                Assert.Same(model.ActivateWorkspaceCommand, picker.Command);
            var sourceEditor = Assert.Single(Descendants(root).OfType<StackPanel>(),
                p => ReferenceEquals(p.DataContext, model.SelectedSource));
            Assert.Equal(model.CanEditInputs, sourceEditor.IsEnabled); // locked while data exist (A-2)
            Assert.False(sourceEditor.IsEnabled);
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
            foreach (string id in new[] { "AcquisitionLiveTime", "AcquisitionSpeed", "AcquisitionSeed" })
            {
                var input = Assert.Single(Descendants(root).OfType<TextBox>(),
                    t => AutomationProperties.GetAutomationId(t) == id);
                Assert.Equal(80, input.ActualWidth);
                Assert.Equal(28, input.ActualHeight);
            }
            root.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            if (mixed && workspace == model.Imaging) VerifyOverlayChips(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string directory = SnapshotDirectory("studio-render");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,
                $"{workspace.Title.ToLowerInvariant()}{(crowded ? "-crowded" : "")}{(opticsExpanded.HasValue ? "-optics-" + (opticsExpanded.Value ? "expanded" : "collapsed") + "-focus800" : "")}{(mixed ? "-mixed-" + isotope.ToLowerInvariant() : "")}-{theme.ToLowerInvariant()}-{size.Width:0}x{size.Height:0}.png");
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

    private static void VerifyEmissionTable(DependencyObject root, SpectrumWorkspaceViewModel model)
    {
        var view = Assert.Single(Descendants(root).OfType<Gcam.Studio.Views.SpectrumView>());
        var grids = Descendants(view).OfType<Grid>().Where(g => g.ColumnDefinitions.Count == 5).ToArray();
        Assert.Equal(model.Lines.Count + 1, grids.Length);
        var header = grids.Single(g => g.Children.OfType<TextBlock>().Any(t => t.Text == "Emission"));
        var rightEdges = header.ColumnDefinitions.Skip(1).Select(c => c.Offset + c.ActualWidth).ToArray();
        double gap = ((Thickness)Application.Current.FindResource("Gap.Inline")).Right;
        foreach (var grid in grids)
        {
            for (int i = 1; i < 5; i++) Assert.Equal(rightEdges[i - 1], grid.ColumnDefinitions[i].Offset + grid.ColumnDefinitions[i].ActualWidth, 6);
            var cells = grid.Children.OfType<TextBlock>().OrderBy(Grid.GetColumn).ToArray();
            for (int i = 1; i < cells.Length; i++)
            {
                var left = cells[i - 1].TranslatePoint(new Point(cells[i - 1].ActualWidth, 0), view).X;
                var right = cells[i].TranslatePoint(new Point(0, 0), view).X;
                Assert.True(right - left >= gap - 0.01, $"{cells[i - 1].Text} / {cells[i].Text}: gap {right - left:F3}, required {gap}");
                var text = new FormattedText(cells[i].Text, System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight, new Typeface(cells[i].FontFamily, cells[i].FontStyle, cells[i].FontWeight, cells[i].FontStretch),
                    cells[i].FontSize, Brushes.Black, null, TextFormattingMode.Display, 1);
                Assert.True(cells[i].ActualWidth >= text.Width, $"Truncated table value: {cells[i].Text}");
            }
        }
    }

    private static void VerifyOverlayChips(DependencyObject root)
    {
        var adorner = Assert.Single(Descendants(root).OfType<MeasurementAdorner>(),
            a => a.AdornedElement is HeatmapView { Name: "Recon" });
        var view = (HeatmapView)adorner.AdornedElement;
        var extent = view.ExtentMm;
        var visible = new Rect(view.MmToScreen(extent.Min), view.MmToScreen(extent.Max));
        visible.Intersect(new Rect(view.RenderSize));
        var drawing = VisualTreeHelper.GetDrawing(adorner);
        Assert.NotNull(drawing);
        var chips = Drawings(drawing).OfType<GeometryDrawing>()
            .Where(d => d.Geometry is RectangleGeometry { RadiusX: 3, RadiusY: 3 })
            .Select(d =>
            {
                var rect = ((RectangleGeometry)d.Geometry).Rect;
                if (d.Pen is { } pen) rect.Inflate(pen.Thickness / 2, pen.Thickness / 2);
                return new ScreenRect(rect.X, rect.Y, rect.Width, rect.Height);
            }).ToArray();
        int expected = MeasurementOverlay.GetMarkers(view)!.Cast<IPlaneMarker>().Count()
            + MeasurementOverlay.GetFoundPeaks(view)!.Cast<ImagingPeak>().Count();
        Assert.Equal(expected, chips.Length);
        var bounds = new ScreenRect(visible.X, visible.Y, visible.Width, visible.Height);
        double gap = (double)Application.Current.FindResource("Space.Imaging.LabelGap");
        Assert.All(chips, c => Assert.True(bounds.Contains(c), $"Chip outside visible image: {c}"));
        for (int i = 0; i < chips.Length; i++)
            for (int j = i + 1; j < chips.Length; j++) Assert.True(chips[i].IsSeparatedFrom(chips[j], gap));

        static IEnumerable<Drawing> Drawings(DrawingGroup group)
        {
            foreach (var child in group.Children)
            {
                yield return child;
                if (child is DrawingGroup nested) foreach (var drawing in Drawings(nested)) yield return drawing;
            }
        }
    }

    /// <summary>
    /// The main window's content, detached and laid out at <paramref name="size"/> with the window's own text and
    /// pixel settings (Display text formatting, layout rounding, device-pixel snapping), so every render matches the
    /// app's text metrics. No HWND is created; the window is never shown.
    /// </summary>
    private static (Border Root, FrameworkElement Content) DetachMainWindow(MainViewModel model, Size size)
    {
        var window = new MainWindow();
        var content = (FrameworkElement)window.Content;
        window.Content = null;
        content.Resources.MergedDictionaries.Add(window.Resources);
        content.DataContext = model;
        TextElement.SetFontFamily(content, (FontFamily)Application.Current.FindResource("Font.UI"));
        content.UseLayoutRounding = window.UseLayoutRounding;
        content.SnapsToDevicePixels = window.SnapsToDevicePixels;
        TextOptions.SetTextFormattingMode(content, TextOptions.GetTextFormattingMode(window));
        var root = new Border
        {
            Background = (Brush)Application.Current.FindResource("Brush.Bg.Canvas"),
            Child = new AdornerDecorator { Child = content }
        };
        root.Measure(size);
        root.Arrange(new Rect(size));
        root.UpdateLayout();
        return (root, content);
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
            double liveTimeS, double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
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
                { Detector = new DetectorSettings(), Seed = 12345 };
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

    private sealed class FixtureSpectrum(bool mixed = false) : ISpectrumService
    {
        public double Resolution662 => 0.0457;
        public double ResolvingTimeS => 730e-9;
        public Task<Gcam.Studio.Core.Services.SpectrumView> ProcessAsync(Guid acquisitionId,
            IReadOnlyList<DetectedEvent> events, IReadOnlyList<SpectrumLine> lines, SpectrumSettings settings,
            int seed = 909, CancellationToken cancellationToken = default)
        {
            // Analytic drawing fixture, not a physics result. Peaks exercise the 10,000 log tick.
            int bins = mixed ? 512 : 256;
            const double width = 3;
            var centres = Enumerable.Range(0, bins).Select(i => (i + 0.5) * width).ToArray();
            var counts = centres.Select(x => Math.Round(11000 * Gaussian(x, 34, 5)
                + 1600 * Gaussian(x, 661.7, 14) + (mixed ? 750 * Gaussian(x, 1173.2, 20) + 650 * Gaussian(x, 1332.5, 21) : 0)
                + (x < 478 ? 80 * Math.Exp(-x / 300) : 0))).ToArray();
            SpectrumBand[] bands =
            [
                new([new("Cs-137", 32.1, EmissionKind.XRay, "Ba K"), new("Cs-137", 36.4, EmissionKind.XRay, "Ba K")], 25.5, 43.5, 1094, 0.169),
                new([new("Cs-137", 661.7)], 616.3, 707.1, 1966, 0.304)
            ];
            if (mixed) bands = [.. bands,
                new([new("Co-60", 1173.2)], 1100.4, 1246.0, 42, 0.0065),
                new([new("Co-60", 1332.5)], 1251.2, 1413.8, 38, 0.0059)];
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
                double scale = (settings.FocalDistanceMm ?? optics.FocalDistanceMm) / 1000;
                var recon = new DetectorImage(41, 41);
                for (int y = 0; y < 41; y++)
                for (int x = 0; x < 41; x++)
                    recon[x, y] = sources.Sum(s => 3000 * Gaussian((-56 + x * 2.8) * scale, s.X * scale, 5 * scale)
                        * Gaussian((-56 + y * 2.8) * scale, s.Y * scale, 5 * scale));
                // The estimate is this reconstruction's argmax, as the decoder reports it (never the snapshot's).
                int best = 0;
                for (int i = 1; i < 41 * 41; i++) if (recon[i % 41, i / 41] > recon[best % 41, best / 41]) best = i;
                var estimate = new SourceEstimate(new Gcam.Core.Vector3((-56 + best % 41 * 2.8) * scale,
                    (-56 + best / 41 * 2.8) * scale, settings.FocalDistanceMm ?? optics.FocalDistanceMm), 1);
                return snapshot.Imaging with { Reconstruction = recon.ReadOnlyCopy(), ReconOriginMm = -56 * scale,
                    ReconStepMm = 2.8 * scale, Estimate = estimate };
            }
            channels.Add(new("All", double.NaN, double.NaN, Image(scene), peaks));
            foreach (var s in scene)
                channels.Add(new(s.Isotope, s.Isotope == "Cs-137" ? 616 : 1110,
                    s.Isotope == "Cs-137" ? 707 : 1236, Image([s]), peaks.Where(p => p.Isotope == s.Isotope).ToArray()));
            // The strip ratio exists only when stripping is on and a Co-60 source contaminates Cs-137.
            bool coPair = settings.Strip && scene.Any(s => s.Isotope == "Co-60") && scene.Any(s => s.Isotope == "Cs-137");
            return Task.FromResult(new ImagingView(channels,
                coPair ? [new StripRatio("Cs-137", "Co-60", 100000, 11000, 40000)] : [],
                TimeSpan.FromMilliseconds(12), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(35)));
        }
    }
}
