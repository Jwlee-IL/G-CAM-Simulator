using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;
using Gcam.Studio.Services;

namespace Gcam.Studio.RenderTests;

public sealed partial class PlotViewRenderTests
{
    /// <summary>Author decision (TODO-45 review 3): the Measurements panel with the tools as icon toggles in its header and
    /// a delete button per row. Captures no measurements, several rows with one selected under each tool, and the half
    /// width beside a focus curve; asserts the checked toggle's frame, the row button on the selected row only, and that
    /// no Value cell is cut. Synthetic drawing fixture (FixtureImaging), not MC evidence.</summary>
    private void RenderMeasurements(string theme)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries.Clear();
        foreach (string file in new[] { $"Tokens.{theme}", "Metrics", "Typography", "Controls" })
            dictionaries.Add(new ResourceDictionary { Source = new Uri($"/Gcam.Studio;component/Themes/{file}.xaml", UriKind.Relative) });
        var model = new MainViewModel(new FixtureAcquisition(), new FixtureTheme(Enum.Parse<AppTheme>(theme)),
            new FixtureSpectrum(), new FixtureImaging(), detectorFace: new DetectorFaceService()) { SeedText = "12345", AmbientDoseRateMicroSvPerHour = 0 };
        model.Sources[0].DistanceMm = 300;
        model.StartCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        model.Imaging.WhenUpdated.GetAwaiter().GetResult();
        model.SelectedWorkspace = model.Imaging;
        var measurements = model.Imaging.Measurements;
        Capture("measurements-empty");
        measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Distance, [new Vec2(-4, -3), new Vec2(5, 4)]));
        measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Reconstruction, MeasurementKind.Angle, [new Vec2(10, 0), new Vec2(0, 0), new Vec2(6, 8)]));
        measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Roi, [new Vec2(-8, -8), new Vec2(8, 8)]));
        measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Reconstruction, MeasurementKind.Roi, [new Vec2(-30, -30), new Vec2(30, 30)]));
        measurements.Selected = measurements.Items[1];
        foreach (var tool in Enum.GetValues<MeasureTool>())
        {
            measurements.ActiveTool = tool;
            Capture($"measurements-{tool.ToString().ToLowerInvariant()}");
        }
        measurements.ActiveTool = MeasureTool.Pan;
        // Half width beside a focus curve: the narrowest Value column.
        double[] planes = Enumerable.Range(0, 81).Select(i => 1 / (1 / 110.0 - i * (1 / 110.0 - 1 / 3000.0) / 80)).ToArray();
        var track = FocusSweepMath.Describe(planes.Select(p => new FocusSample(p, 0, 0, 6 * Math.Exp(-0.5 * Math.Pow((1 / p - 1 / 300.0) / 4e-4, 2)))).ToArray());
        var snapshot = model.Snapshot!;
        model.Imaging.SweepResult = new(new(Guid.NewGuid(), snapshot.Counts, snapshot.LiveTimeS,
            "All", 1.5, false, snapshot.Optics ?? model.Optics, snapshot.Detector ?? model.Detector, 1), [track], TimeSpan.FromMilliseconds(42));
        Capture("measurements-sweep");

        void Capture(string name)
        {
            foreach (var size in new[] { new Size(1280, 800), new Size(1440, 900) })
            {
                var (root, content) = DetachMainWindow(model, size);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); root.UpdateLayout();
                var accent = (Brush)Application.Current.FindResource("Brush.Accent");
                var frame = (Brush)Application.Current.FindResource("Brush.Line.Control");
                foreach (var (id, tool, key) in new[] { ("ToolPan", MeasureTool.Pan, "Alt+P"), ("ToolDistance", MeasureTool.Distance, "Alt+D"),
                    ("ToolAngle", MeasureTool.Angle, "Alt+A"), ("ToolRoi", MeasureTool.Roi, "Alt+R") })
                {
                    var toggle = Assert.Single(Descendants(root).OfType<RadioButton>(), r => AutomationProperties.GetAutomationId(r) == id);
                    // The window's access key (SR-A11Y-03) reaches assistive technology through the toggle's peer (IconToggle:
                    // the stock peer drops AutomationProperties.AccessKey); the class name stays RadioButton.
                    var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(toggle);
                    Assert.Equal(key, peer.GetAccessKey());
                    Assert.Equal(nameof(RadioButton), peer.GetClassName());
                    bool active = measurements.ActiveTool == tool;
                    Assert.Equal(active, toggle.IsChecked);
                    var chrome = (Border)toggle.Template.FindName("Chrome", toggle);
                    Assert.Same(active ? accent : frame, chrome.BorderBrush);
                    Assert.Same(active ? accent : Application.Current.FindResource("Brush.Text.Primary"), toggle.Foreground);
                    Assert.Equal((double)Application.Current.FindResource("Size.Control"), toggle.ActualHeight);
                    Assert.EndsWith($"({key})", Assert.IsType<string>(toggle.ToolTip));
                }
                Assert.DoesNotContain(Descendants(root).OfType<Button>(), b => AutomationProperties.GetAutomationId(b) == "DeleteMeasurement");
                var list = Assert.Single(Descendants(root).OfType<ListBox>(), l => AutomationProperties.GetAutomationId(l) == "MeasurementList");
                foreach (var item in measurements.Items)
                {
                    var container = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(item);
                    var delete = Assert.Single(Descendants(container).OfType<Button>());
                    Assert.Equal(item.DeleteAutomationId, AutomationProperties.GetAutomationId(delete));
                    Assert.Same(measurements.RemoveCommand, delete.Command);
                    Assert.Same(item, delete.CommandParameter);
                    bool selected = ReferenceEquals(item, measurements.Selected);
                    Assert.Equal(selected ? Visibility.Visible : Visibility.Hidden, delete.Visibility);
                    Assert.Equal(selected, container.IsTabStop);
                    Assert.True(delete.ActualWidth > 0); // the column is reserved on every row
                    // The Value cell shows its whole text: at full width on one line; where the column is narrower (half
                    // width) it wraps, so the laid-out text at the cell's width fits the cell in both directions.
                    var value = Descendants(container).OfType<TextBlock>().Single(t => t.Text == item.Value);
                    Assert.Equal(TextTrimming.None, value.TextTrimming);
                    var probe = new TextBlock { Text = value.Text, FontFamily = value.FontFamily, FontSize = value.FontSize, FontWeight = value.FontWeight };
                    TextOptions.SetTextFormattingMode(probe, TextOptions.GetTextFormattingMode(value));
                    probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    bool fullWidth = !model.Imaging.HasSweepResult;
                    if (fullWidth)
                        Assert.True(value.ActualWidth + 0.5 >= probe.DesiredSize.Width, $"{name} {size}: Value '{value.Text}' cut ({value.ActualWidth:0.#} < {probe.DesiredSize.Width:0.#})");
                    var wrapped = new TextBlock { Text = value.Text, FontFamily = value.FontFamily, FontSize = value.FontSize, FontWeight = value.FontWeight, TextWrapping = TextWrapping.Wrap };
                    TextOptions.SetTextFormattingMode(wrapped, TextOptions.GetTextFormattingMode(value));
                    wrapped.Measure(new Size(value.ActualWidth + 0.5, double.PositiveInfinity));
                    Assert.True(wrapped.DesiredSize.Width <= value.ActualWidth + 0.5 && wrapped.DesiredSize.Height <= value.ActualHeight + 0.5,
                        $"{name} {size}: Value '{value.Text}' does not fit its cell ({value.ActualWidth:0.#} x {value.ActualHeight:0.#})");
                    output.WriteLine($"VALUE {name} {size}: '{value.Text}' cell {value.ActualWidth:0.#} x {value.ActualHeight:0.#}, one line needs {probe.DesiredSize.Width:0.#}");
                    var deleteBounds = delete.TransformToAncestor(container).TransformBounds(new Rect(delete.RenderSize));
                    var valueBounds = value.TransformToAncestor(container).TransformBounds(new Rect(value.RenderSize));
                    Assert.True(valueBounds.Right <= deleteBounds.Left, $"{name} {size}: Value overlaps the row's delete button");
                }
                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                string directory = SnapshotDirectory("studio-render"); Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, $"{name}-{theme.ToLowerInvariant()}-{size.Width:0}x{size.Height:0}.png");
                using (var stream = File.Create(path)) encoder.Save(stream);
                // Every visible icon button and icon toggle: complete, centred glyph ink (the selected row's delete included).
                IconRenderChecks.CheckAndInspect(root, bitmap, path);
                output.WriteLine(path);
                content.DataContext = null; root.UpdateLayout();
            }
        }
    }
}
