using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Studio.Controls;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;
using Gcam.Studio.Services;
using Gcam.Studio.Views;

namespace Gcam.Studio.RenderTests;

public sealed partial class PlotViewRenderTests
{
    private void RenderWaveforms(string theme)
    {
        // Called by the existing render test's sole STA/Application lifetime. No HWND or Studio startup.
        var resources = Application.Current.Resources.MergedDictionaries;
        resources.Clear();
        foreach (string file in new[] { $"Tokens.{theme}", "Metrics", "Typography", "Controls" })
            resources.Add(new ResourceDictionary { Source = new Uri($"/Gcam.Studio;component/Themes/{file}.xaml", UriKind.Relative) });
        var acquisition = new WaveformAcquisition();
        var model = new MainViewModel(acquisition, new FixtureTheme(Enum.Parse<AppTheme>(theme)), new FixtureSpectrum(),
            waveform: new FixedTimeWaveformService(), confirmation: new ModalService()) { SeedText = "12345", AmbientDoseRateMicroSvPerHour = 0 };
        model.StartCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        model.SelectedWorkspace = model.Waveform;
        CompleteWaveform(model.Waveform.WhenUpdated);
        model.Waveform.FollowLatest = false;
        model.Waveform.TriggerIndex = 64;
        CompleteWaveform(model.Waveform.WhenUpdated);
        // Physical inputs are locked while data exist (A-2): a chain change is refused.
        var acquiredPreamp = model.Preamp;
        model.Preamp = FrontEndParts.Preamps[3];
        Assert.Same(acquiredPreamp, model.Preamp);
        model.OpenOptionsCommand.Execute(null);
        model.Options!.SelectedTab = OptionsTab.Detector;
        var (optionsRoot, optionsContent) = DetachMainWindow(model, new Size(1280, 800));
        var scintillator = Assert.Single(Descendants(optionsRoot).OfType<ComboBox>(),
            c => AutomationProperties.GetAutomationId(c) == "Chain.Scintillator");
        Assert.Same(model.Scintillator, scintillator.SelectedItem);
        Assert.Equal(5, scintillator.Items.Count);
        Assert.Contains(scintillator.Items.Cast<ScintPreset>(), s => s.Name == "CsI(Tl)");
        Assert.False(scintillator.IsEnabled, "chain selectors are locked while data exist");
        model.Options.CancelCommand.Execute(null);
        optionsContent.DataContext = null;
        foreach (string mode in new[] { "real10us", "rate-study", "ideal" })
        {
            model.Waveform.RateStudy = mode == "rate-study";
            model.Waveform.RateKcps = 1000;
            model.Waveform.Ideal = mode == "ideal";
            CompleteWaveform(model.Waveform.WhenUpdated);
            Assert.Null(model.Waveform.Error);
            Assert.NotEmpty(model.Waveform.AdcSeries);
            Assert.Equal(1250, model.Waveform.View!.Adc.Y.Length);
            Assert.Equal(mode == "rate-study", model.Waveform.View.Note.Contains("not the measured rate"));
            foreach (var size in new[] { new Size(1280, 800), new Size(1440, 900) })
            {
                var (root, content) = DetachMainWindow(model, size);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                root.UpdateLayout();
                var plots = Descendants(root).OfType<PlotView>().ToArray();
                Assert.Equal(2, plots.Length);
                Assert.True(plots[0].ShowMarkerLabels);
                Assert.False(plots[1].ShowMarkerLabels);
                Assert.All(plots, p => Assert.NotEmpty(p.Series!));
                Assert.Equal(plots[0].CurrentViewRange, plots[1].CurrentViewRange);
                // Direct control API, no keyboard/mouse/desktop input, exercises shared navigation binding.
                plots[0].ZoomAt(.5, true);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                root.UpdateLayout();
                Assert.Equal(plots[0].CurrentViewRange, plots[1].CurrentViewRange);
                plots[1].ResetView();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Assert.Equal(plots[0].CurrentViewRange, plots[1].CurrentViewRange);
                Assert.DoesNotContain(Descendants(root).OfType<ComboBox>(),
                    c => AutomationProperties.GetAutomationId(c) == "Chain.Scintillator");
                Assert.DoesNotContain(Descendants(root).OfType<TextBlock>(),
                    c => AutomationProperties.GetAutomationId(c) is "Chain.Pending" or "Chain.Acquired");
                var note = Assert.Single(Descendants(root).OfType<TextBlock>(),
                    c => AutomationProperties.GetAutomationId(c) == "Waveform.Note");
                Assert.Equal(model.Waveform.Note, note.Text);
                // Review 5: the acquired chain is label / value rows (the group keeps its AutomationId and is named with the
                // chain readout); the events are a table with one row per event (name = the event's description).
                var chain = Assert.Single(Descendants(root).OfType<SettingsGroup>(), g => AutomationProperties.GetAutomationId(g) == "Waveform.ChainReadout");
                Assert.Equal(model.Waveform.View!.ChainReadout, AutomationProperties.GetName(chain));
                Assert.NotEmpty(model.Waveform.ChainRows);
                foreach (var row in model.Waveform.ChainRows) Assert.Contains(row.Value.Split(' ')[0], model.Waveform.View.ChainReadout);
                var events = Assert.Single(Descendants(root).OfType<ListBox>(), l => AutomationProperties.GetAutomationId(l) == "Waveform.Events");
                Assert.Equal(model.Waveform.Events.Count, events.Items.Count);
                foreach (var cell in Descendants(events).OfType<TextBlock>().Where(t => t.ActualWidth > 0))
                {
                    // No cell text is cut: its laid-out width holds its whole one-line text (the acquired line wraps).
                    var probe = new TextBlock { Text = cell.Text, FontFamily = cell.FontFamily, FontSize = cell.FontSize };
                    TextOptions.SetTextFormattingMode(probe, TextOptions.GetTextFormattingMode(cell));
                    probe.Measure(new Size(cell.TextWrapping == TextWrapping.Wrap ? cell.ActualWidth + .5 : double.PositiveInfinity, double.PositiveInfinity));
                    Assert.True(cell.TextWrapping == TextWrapping.Wrap ? probe.DesiredSize.Width <= cell.ActualWidth + .5 : cell.ActualWidth + .5 >= probe.DesiredSize.Width,
                        $"event cell '{cell.Text}' cut ({cell.ActualWidth:0.#} < {probe.DesiredSize.Width:0.#})");
                }
                root.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                string directory = SnapshotDirectory("studio-render");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, $"waveform-{mode}-{theme.ToLowerInvariant()}-{size.Width:0}x{size.Height:0}.png");
                using (var stream = File.Create(path)) encoder.Save(stream);
            IconRenderChecks.CheckAndInspect(root, bitmap, path);
                output.WriteLine(path);
                content.DataContext = null; root.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            }
        }
    }

    private static void CompleteWaveform(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var dispatcher = Dispatcher.CurrentDispatcher;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }

    private sealed class WaveformAcquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
            double liveTimeS, double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
        {
            detector ??= new DetectorSettings();
            var config = SimulationService.BuildConfig(scene, optics, detector); config.Seed = seed ?? 12345;
            using var source = new ListModeSource(config);
            var events = new List<DetectedEvent>(); var flood = new DetectorImage(30, 30);
            while (events.Count < 128)
            {
                if (source.Advance() is not { } ev) continue;
                events.Add(ev); flood.Add(ev.PixelX, ev.PixelY, 1);
            }
            var image = new ImagingResult(flood.ReadOnlyCopy(), -8.7, .6, null, 0, 0, null, events.Count, TimeSpan.Zero);
            // Live time ends at the last consumed arrival, as AcquisitionSession's MC-limited branch does: counts /
            // live time is then the observed rate. A 128-event prefix of a 60 s preset is a stopped acquisition.
            var snapshot = new AcquisitionSnapshot(events[^1].ArrivalTimeS, events.Count, source.RateCps,
                1, false, image, events.AsReadOnly(), TimeSpan.Zero, false) { Detector = detector, Optics = optics, Seed = config.Seed };
            return new FixtureSession(snapshot);
        }
    }

    /// <summary>The real scope service with a fixed worker time, so the summary line (and the PNG) is reproducible.</summary>
    private sealed class FixedTimeWaveformService : IWaveformService
    {
        private readonly WaveformService _inner = new();
        public async Task<Gcam.Studio.Core.Services.WaveformView> ProcessAsync(AcquisitionSnapshot snapshot,
            WaveformSettings settings, CancellationToken cancellationToken = default)
            => await _inner.ProcessAsync(snapshot, settings, cancellationToken) with { ProcessingTime = TimeSpan.FromMilliseconds(0.4) };
    }
}
