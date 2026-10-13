using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Gcam.Studio.Controls;
using Gcam.Studio.Core.ViewModels;
using Gcam.Studio.Services;
using Gcam.Studio.Core.Services;
using Gcam.Configuration;

namespace Gcam.Studio.RenderTests;

public sealed partial class PlotViewRenderTests
{
    private void RenderOptions(string theme)
    {
        var modal = new ModalService();
        var model = new MainViewModel(new FixtureAcquisition(), new FixtureTheme(Enum.Parse<Gcam.Studio.Core.Services.AppTheme>(theme)),
            new FixtureSpectrum(), confirmation: modal);
        foreach (var size in new[] { new Size(1280, 800), new Size(1440, 900) })
        {
            CaptureStage2(model, theme, size, "summary-defaults");
            model.SeedText = "12345"; model.GainSigmaPercent = 4; model.AmbientDoseText = "0";
            CaptureStage2(model, theme, size, "summary-nondefaults");
            model.SeedText = StudioOptionDefaults.Current.SeedText;
            model.GainSigmaPercent = StudioOptionDefaults.Current.GainSigmaPercent;
            model.AmbientDoseRateMicroSvPerHour = StudioOptionDefaults.Current.AmbientDoseMicroSvPerHour;
            model.SelectedLeftTab = 1;
            CaptureStage2(model, theme, size, "optics-preset");
            model.OpticsEditor.SelectedPresetIndex = 0;
            CaptureStage2(model, theme, size, "optics-custom");
            model.OpticsEditor.SelectedPresetIndex = 1; model.SelectedLeftTab = 0;
            model.IsRunning = true;
            CaptureStage2(model, theme, size, "caption-running");
            model.IsRunning = false;
            CaptureStage2(model, theme, size, "caption-maximized", maximized: true);
            foreach (bool locked in new[] { false, true })
            {
                model.IsRunning = locked;
                foreach (int leftTab in new[] { 0, 1, 2 })
                {
                    model.SelectedLeftTab = leftTab;
                    CaptureStage2(model, theme, size, $"left-{new[] { "scene", "optics", "detector" }[leftTab]}-{(locked ? "locked" : "editable")}");
                }
                model.IsRunning = false;
            }
            model.SelectedLeftTab = 2;
            model.ReadoutMode = ReadoutMode.FourOutputAnger;
            model.IsPreparingReadout = true;
            typeof(MainViewModel).GetMethod("NotifyRunState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(model, null);
            CaptureStage2(model, theme, size, "icons-prepare");
            model.IsPreparingReadout = false;
            typeof(MainViewModel).GetMethod("NotifyRunState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(model, null);
            model.ReadoutMode = ReadoutMode.DirectCrystal;
            model.SelectedLeftTab = 0;
            model.Imaging.IsSweepExpanded = true;
            model.Imaging.IsSweeping = true;
            CaptureStage2(model, theme, size, "icons-sweep");
            model.Imaging.IsSweeping = false;
            model.Imaging.IsSweepExpanded = false;
            (Rect Dialog, Rect Action, Rect Tabs)? editableLayout = null;
            foreach (bool locked in new[] { false, true })
            {
                (Rect Dialog, Rect Action, Rect Tabs)? firstLayout = null;
                model.IsPreparingReadout = locked;
                model.OpenOptionsCommand.Execute(null);
                foreach (var tab in Enum.GetValues<OptionsTab>())
                {
                    model.Options!.SelectedTab = tab;
                    var layout = CaptureStage2(model, theme, size, $"options-{tab.ToString().ToLowerInvariant()}-{(locked ? "locked" : "editable")}")
                        ?? throw new InvalidOperationException("Options did not produce modal bounds.");
                    if (firstLayout is { } first) Assert.Equal(first, layout);
                    else firstLayout = layout;
                    if (editableLayout is { } editable)
                    {
                        Assert.Equal(editable.Dialog, layout.Dialog);
                        Assert.Equal(editable.Tabs, layout.Tabs);
                        Assert.Equal(editable.Action.Right, layout.Action.Right);
                        Assert.Equal(editable.Action.Bottom, layout.Action.Bottom);
                    }
                }
                editableLayout ??= firstLayout;
                model.Options!.CancelCommand.Execute(null);
                model.IsPreparingReadout = false;
            }
        }
        // A 1920 × 1080 monitor with a 40 px taskbar has a smaller DIP work area at high DPI.
        // Exercise that client layout detached; native sizing/DPI transitions remain desktop checks.
        CaptureStage2(model, theme, new Size(1536, 832), "caption-maximized-workarea-125", maximized: true);
        CaptureStage2(model, theme, new Size(1280, 693), "caption-maximized-workarea-150", maximized: true);
    }
    private static void AssertTopBar(FrameworkElement root)
    {
        var top = Descendants(root).OfType<Border>().Single(b => AutomationProperties.GetAutomationId(b) == "Window.TopBar");
        Rect Bounds(FrameworkElement e) => e.TransformToAncestor(root).TransformBounds(new Rect(e.RenderSize));
        double center = Bounds(top).Top + Bounds(top).Height / 2;
        var controls = Descendants(top).OfType<FrameworkElement>().Where(e =>
            e is Button or TextBox || AutomationProperties.GetAutomationId(e) == "WorkspaceSwitch");
        Rect? previous = null;
        foreach (var control in controls.Where(e => e.Visibility == Visibility.Visible))
        {
            if (AutomationProperties.GetAutomationId(control).StartsWith("Window.", StringComparison.Ordinal)) continue;
            var bounds = Bounds(control);
            Assert.Equal((double)root.FindResource("Size.Control"), bounds.Height);
            Assert.InRange(Math.Abs(bounds.Top + bounds.Height / 2 - center), 0, 0.5); // hairline and layout rounding
            Assert.True(new Rect(root.RenderSize).Contains(bounds));
        }
        foreach (var bounds in controls.Where(e => e.Visibility == Visibility.Visible).Select(Bounds).OrderBy(b => b.Left))
        {
            if (previous is { } last) Assert.True(bounds.Left >= last.Right, "top-bar controls overlap");
            previous = bounds;
        }
    }

    private static void AssertLeftTab(FrameworkElement root, MainViewModel model)
    {
        var tabs = Descendants(root).OfType<TabControl>().Single();
        Assert.Equal(model.SelectedLeftTab, tabs.SelectedIndex);
        Assert.Equal(new[] { "Scene", "Optics", "Detector" }, tabs.Items.Cast<TabItem>().Select(t => (string)t.Header));
        Assert.Equal(new[] { "Left.SceneTab", "Left.OpticsTab", "Left.DetectorTab" }, tabs.Items.Cast<TabItem>().Select(AutomationProperties.GetAutomationId));
        if (model.SelectedLeftTab != 0)
        {
            var group = Descendants(tabs).OfType<SettingsGroup>().Single();
            var peer = UIElementAutomationPeer.CreatePeerForElement(group);
            Assert.Equal(model.SelectedLeftTab == 1 ? "Optics.Section" : "Detector.Section", peer.GetAutomationId());
            Assert.Equal(AutomationControlType.Group, peer.GetAutomationControlType());
        }
        string id = model.SelectedLeftTab switch { 0 => "SourceX", 1 => "Optics.Preset", _ => "Readout.Mode" };
        var field = Descendants(tabs).OfType<FrameworkElement>().Single(e => AutomationProperties.GetAutomationId(e) == id);
        Assert.Equal(model.CanEditInputs && model.Modal?.ActiveContent is null, field.IsEnabled);
        var scroll = Assert.IsType<ScrollViewer>(tabs.SelectedContent);
        Assert.Equal(0, scroll.ScrollableHeight);
    }

    private (Rect Dialog, Rect Action, Rect Tabs)? CaptureStage2(MainViewModel model, string theme, Size size, string name, bool maximized = false)
    {
        var (root, content) = DetachMainWindow(model, size);
        Thickness inset = new Thickness(0);
        if (maximized)
        {
            var frame = Descendants(root).OfType<WindowFrame>().Single();
            frame.Padding = inset;
            Descendants(root).OfType<CaptionButtons>().Single().IsMaximized = true;
        }
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); root.UpdateLayout();
        (Rect Dialog, Rect Action, Rect Tabs)? layout = null;
        foreach (string id in new[] { "ThemeToggle", "StartAcquisition", "Window.Minimize", "Window.Maximize", "Window.Close" })
        {
            var button = Descendants(root).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == id);
            if (id == "StartAcquisition" && model.IsRunning) continue;
            var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize));
            Assert.True(new Rect(size).Contains(bounds), $"{id} outside {size}");
            Assert.True(button.ActualWidth > 0);
            Assert.True(System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(button));
            if (id.StartsWith("Window.", StringComparison.Ordinal))
            {
                Assert.NotNull(button.ToolTip);
                Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(button)));
                Assert.True(button.Focusable);
                Assert.Equal(inset.Top, bounds.Top);
                // The bar's bottom hairline runs under the caption buttons, so they fill the bar minus that line.
                Assert.Equal(((GridLength)root.FindResource("Size.Bar")).Value - 1, bounds.Height);
                if (id == "Window.Close") Assert.Equal(size.Width - inset.Right, bounds.Right);
                if (id == "Window.Maximize") Assert.Equal(maximized ? "Restore" : "Maximize", AutomationProperties.GetName(button));
                for (DependencyObject? parent = VisualTreeHelper.GetParent(button); parent is not null; parent = VisualTreeHelper.GetParent(parent))
                    Assert.IsNotType<Button>(parent);
                var action = Descendants(root).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b)
                    == (model.IsRunning ? "StopAcquisition" : "StartAcquisition"));
                var actionBounds = action.TransformToAncestor(root).TransformBounds(new Rect(action.RenderSize));
                Assert.True(bounds.Left >= actionBounds.Right, "caption buttons follow the acquisition action");
            }
        }
        if (model.Options is { } options)
        {
            var view = Descendants(root).OfType<Gcam.Studio.Views.OptionsView>().Single();
            Assert.Same(options, view.DataContext);
            var layer = Descendants(root).OfType<ModalLayer>().Single();
            Assert.Equal("Options", AutomationProperties.GetName(layer));
            var close = Descendants(view).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "Modal.Close");
            Assert.Equal(options.IsReadOnly ? Visibility.Visible : Visibility.Collapsed, close.Visibility);
            var action = options.IsReadOnly ? close : Descendants(view).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "Options.Apply");
            var tab = Descendants(view).OfType<RadioButton>().Single(b => AutomationProperties.GetAutomationId(b) == "Options.AcquisitionTab");
            Rect Bounds(FrameworkElement element) => element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
            var panel = (Border)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(view));
            layout = (Bounds(panel), Bounds(action), Bounds(tab));
        }
        if (model.SelectedLeftTab == 1)
        {
            var pitch = Descendants(root).OfType<TextBox>().Single(t => AutomationProperties.GetAutomationId(t) == "Optics.CellPitch");
            var fields = (StackPanel)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(pitch));
            Assert.Equal(model.OpticsEditor.IsCustom ? Visibility.Visible : Visibility.Collapsed, fields.Visibility);
            Assert.Equal(model.OpticsEditor.CellPitch, pitch.Text);
        }
        var summary = Descendants(root).OfType<TextBlock>().Single(t => AutomationProperties.GetAutomationId(t) == "Options.Summary");
        Assert.Equal(model.OptionsStatusSummary, summary.Text);
        Assert.Equal(model.HasNonDefaultOptions ? FontWeights.SemiBold : FontWeights.Normal, summary.FontWeight);
        AssertTopBar(root);
        AssertLeftTab(root, model);
        var cog = Descendants(root).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "OpenOptions");
        Assert.Equal(model.OptionsHelpText, AutomationProperties.GetHelpText(cog));
        var badge = Descendants(root).OfType<System.Windows.Shapes.Ellipse>().Single(b => AutomationProperties.GetAutomationId(b) == "Options.Badge");
        Assert.Equal(model.HasNonDefaultOptions ? Visibility.Visible : Visibility.Collapsed, badge.Visibility);
        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string directory = SnapshotDirectory("studio-render"); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{name}-{theme.ToLowerInvariant()}-{size.Width:0}x{size.Height:0}.png");
        using (var stream = File.Create(path)) encoder.Save(stream);
        IconRenderChecks.CheckAndInspect(root, bitmap, path);
        output.WriteLine(path); content.DataContext = null;
        return layout;
    }
}
