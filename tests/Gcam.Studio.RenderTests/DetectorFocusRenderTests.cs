using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Gcam.Studio.Controls;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;
using Gcam.Studio.Services;
using Gcam.Studio.Views;

namespace Gcam.Studio.RenderTests;

public sealed partial class PlotViewRenderTests
{
    private void RenderDetectorAndFocus(string theme)
    {
        // Sole existing STA/Application lifetime, detached content only. Curves are binding/layout fixtures,
        // deliberately not MC evidence or an assertion about depth accuracy.
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries.Clear();
        foreach (string file in new[] { $"Tokens.{theme}", "Metrics", "Typography", "Controls" })
            dictionaries.Add(new ResourceDictionary { Source = new Uri($"/Gcam.Studio;component/Themes/{file}.xaml", UriKind.Relative) });
        var model = new MainViewModel(new WaveformAcquisition(), new FixtureTheme(Enum.Parse<AppTheme>(theme)),
            new FixtureSpectrum(), detectorFace: new DetectorFaceService()) { SeedText = "12345", AmbientDoseRateMicroSvPerHour = 0 };
        model.IsOpticsExpanded = model.IsDetectorExpanded = false;
        model.SelectedWorkspace = model.DetectorWorkspace;
        Capture("detector-before");
        model.StartCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        model.ReflectorGapUm = "20"; // locked while data exist (A-2): refused, the face keeps the acquired gap
        Assert.Equal("100", model.ReflectorGapUm);
        Assert.Equal("Acquired settings · locked until Reset", model.DetectorWorkspace.Identity);
        Assert.Equal(Math.Pow(5d / 6, 2), model.DetectorWorkspace.Face!.ActiveAreaFraction, 12);
        Capture("detector-acquired");
        foreach (bool far in new[] { false, true })
        {
            // Each focus scene is its own fresh acquisition whose only source sits where the sweep
            // finds it: 300 mm (resolved near field) or 1000 mm (far edge censored). SYNTHETIC drawing fixture:
            // the snapshot, reconstruction (FixtureImaging) and focus curve are analytic stand-ins, not MC
            // results and not evidence about depth accuracy; FocusSweepMath.Describe still builds the interval.
            double sourceMm = far ? 1000 : 300;
            model = new MainViewModel(new FixtureAcquisition(), new FixtureTheme(Enum.Parse<AppTheme>(theme)),
                new FixtureSpectrum(), new FixtureImaging(), detectorFace: new DetectorFaceService()) { SeedText = "12345", AmbientDoseRateMicroSvPerHour = 0 };
            model.IsOpticsExpanded = model.IsDetectorExpanded = false;
            model.Sources[0].DistanceMm = sourceMm;
            model.StartCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            model.Imaging.WhenUpdated.GetAwaiter().GetResult();
            Assert.True(model.HasData);
            Assert.NotNull(model.Imaging.Result?.Reconstruction);
            model.SelectedWorkspace = model.Imaging;
            // A laser range to the surface in front of the source, a little short of it.
            model.Imaging.ExternalRange = far ? "950" : "280";
            // Planes uniform in 1/z from D+30 = 110 mm to 3000 mm, as the sweep samples them (81 planes).
            double[] planes = Enumerable.Range(0, 81).Select(i => 1 / (1 / 110.0 - i * (1 / 110.0 - 1 / 3000.0) / 80)).ToArray();
            double invSource = 1 / sourceMm, width = far ? 7e-4 : 4e-4;   // depth of focus is roughly constant in 1/z
            var track = FocusSweepMath.Describe(planes.Select(p =>
                new FocusSample(p, 0, 0, 6 * Math.Exp(-0.5 * Math.Pow((1 / p - invSource) / width, 2)))).ToArray());
            Assert.Equal(far, track.Interval.FarCensored);
            Assert.InRange(track.Sharpest.PlaneMm, sourceMm * 0.95, sourceMm * 1.05);
            var snapshot = model.Snapshot!;
            model.Imaging.SweepResult = new(new(Guid.NewGuid(), snapshot.Counts, snapshot.LiveTimeS,
                "All", 1.5, false, snapshot.Optics ?? model.Optics, snapshot.Detector ?? model.Detector, 1), [track], TimeSpan.FromMilliseconds(42));
            Capture(far ? "focus-far-censored" : "focus-near-resolved");
        }
        void Capture(string name)
        {
            foreach (var size in new[] { new Size(1280, 800), new Size(1440, 900) })
            {
                var (root, content) = DetachMainWindow(model, size);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); root.UpdateLayout();
                if (model.SelectedWorkspace == model.DetectorWorkspace)
                {
                    var face = Assert.Single(Descendants(root).OfType<DetectorFaceView>());
                    Assert.Same(model.DetectorWorkspace.Face, face.Face);
                    Assert.True(face.ActualWidth > 0 && face.ActualHeight > 0);
                    var identity = Assert.Single(Descendants(root).OfType<TextBlock>(), t => AutomationProperties.GetAutomationId(t) == "Detector.Identity");
                    Assert.Equal(model.DetectorWorkspace.Identity, identity.Text);
                    var panel = Assert.Single(Descendants(root).OfType<DetectorPanel>());
                    var card = Assert.Single(panel.Content as Border is { } border ? new[] { border } : []);
                    Assert.Equal(VerticalAlignment.Top, card.VerticalAlignment);
                    Assert.True(card.ActualHeight < panel.ActualHeight);
                }
                else
                {
                    var plot = Assert.Single(Descendants(root).OfType<PlotView>(), p => AutomationProperties.GetAutomationId(p) == "Imaging.FocusCurve");
                    Assert.Single(plot.Series!); Assert.Single(plot.Bands!); Assert.Equal(2, plot.Markers!.Count);
                    Assert.True(plot.ActualWidth > 0 && plot.ActualHeight > 0);
                }
                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                string directory = SnapshotDirectory("studio-render"); Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, $"{name}-{theme.ToLowerInvariant()}-{size.Width:0}x{size.Height:0}.png");
                using (var stream = File.Create(path)) encoder.Save(stream);
                output.WriteLine(path);
                content.DataContext = null; root.UpdateLayout();
            }
        }
    }
}
