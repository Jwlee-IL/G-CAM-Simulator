using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.RenderTests;

public sealed partial class PlotViewRenderTests
{
    private void RenderAmbientInputs(string theme)
    {
        var model = new MainViewModel(new FixtureAcquisition(), new FixtureTheme(Enum.Parse<AppTheme>(theme)), new FixtureSpectrum(false))
        {
            IsChainExpanded = false, IsOpticsExpanded = false, IsDetectorExpanded = true
        };
        // AB-15 default: validated terrestrial preset at 0.10 µSv/h, front-only bound — rendered as Studio starts.
        Assert.Equal(.1, model.AmbientDoseRateMicroSvPerHour);
        Assert.Equal(AmbientGeometry.FrontOnlyThroughMask, model.AmbientGeometry);
        model.RemoveSourceCommand.Execute(null);
        Assert.True(model.StartCommand.CanExecute(null));
        foreach (string state in new[] { "", "-invalid" })
        foreach (var size in new[] { new Size(1280, 800), new Size(1440, 900) })
        {
            // AB-16: an entry outside the pattern is refused; the previous value stays and the warning shows.
            if (state.Length > 0) { model.AmbientDoseText = "-0.1"; Assert.Equal(.1, model.AmbientDoseRateMicroSvPerHour); Assert.NotNull(model.AmbientDoseError); }
            var (root, _) = DetachMainWindow(model, size);
            var dose = Assert.Single(Descendants(root).OfType<TextBox>(),
                box => AutomationProperties.GetAutomationId(box) == "Acquisition.AmbientDose");
            var bound = Assert.Single(Descendants(root).OfType<ComboBox>(),
                box => AutomationProperties.GetAutomationId(box) == "Acquisition.AmbientBound");
            Assert.True(dose.IsEnabled); Assert.True(bound.IsEnabled);
            Assert.Equal(model.AmbientGeometry, bound.SelectedItem);
            // Scroll the existing offscreen panel to expose the absolute-field input, preset and both-bound selector.
            for (DependencyObject? parent = System.Windows.Media.VisualTreeHelper.GetParent(dose); parent is not null;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent))
                if (parent is ScrollViewer scroll) { scroll.ScrollToBottom(); root.UpdateLayout(); break; }
            var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string directory = SnapshotDirectory("studio-render"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"ambient-inputs{state}-{theme.ToLowerInvariant()}-{size.Width:0}x{size.Height:0}.png");
            using var stream = File.Create(path); encoder.Save(stream); output.WriteLine(path);
        }
    }
}
