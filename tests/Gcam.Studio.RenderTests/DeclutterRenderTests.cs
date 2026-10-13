using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using Gcam.Configuration;
using Gcam.Studio.Controls;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;
using Gcam.Studio.Services;

namespace Gcam.Studio.RenderTests;

public sealed class DeclutterRenderTests
{
    [Fact]
    public void HelpMarks_ContainMovedText_AndInlineLabelsAndPhysicalChannelRemain()
        => Sta(() =>
        {
            foreach (string theme in new[] { "Dark", "Light" })
            {
                var vm = Model();
                vm.Imaging.Reconstruction = DecoderMethod.Mlem;
                vm.Imaging.IsSweepExpanded = true;
                var imaging = View("ImagingOptionsPanel", theme, vm.Imaging);
                string Inline(FrameworkElement root) => string.Join("\n", Nodes(root).OfType<TextBlock>().Select(t => t.Text));
                HelpMark Mark(FrameworkElement root, string id) => Nodes(root).OfType<HelpMark>().Single(m => AutomationProperties.GetAutomationId(m) == id);
                Assert.Contains("retained floods", Mark(imaging, "Imaging.ReconstructionHelp").Text);
                Assert.Contains("One-pass scalar", Mark(imaging, "Imaging.StripHelp").Text);
                Assert.Contains("counting and calibration", Mark(imaging, "Imaging.UncertaintyHelp").Text);
                Assert.Contains("iterations", Mark(imaging, "Imaging.MlemHelp").Text);
                Assert.Contains("default optics", Mark(imaging, "Imaging.SweepHelp").Text);
                Assert.Contains("MLEM: low-count false peaks", Inline(imaging));
                Assert.Contains("Experimental · near-field bias", Inline(imaging));
                Assert.DoesNotContain("chosen for pair resolution", Inline(imaging));
                Assert.DoesNotContain("One-pass scalar estimate", Inline(imaging));
                Assert.DoesNotContain("Physical readout cost", Inline(imaging));
                Assert.DoesNotContain(Nodes(imaging).OfType<Expander>(), e => AutomationProperties.GetAutomationId(e).StartsWith("Imaging.ReadoutCost"));
                vm.ReadoutMode = ReadoutMode.FourOutputAnger;
                ReadoutRenderTests.Drain();
                imaging.UpdateLayout();
                var correction = Nodes(imaging).OfType<CheckBox>().Single(c => AutomationProperties.GetAutomationId(c) == "Imaging.FloodCorrection");
                var correctionLabel = Assert.IsType<TextBlock>(correction.Content);
                Assert.Equal("Flood correction from the line calibration (opt-in)", correctionLabel.Text);
                Assert.Equal(TextWrapping.Wrap, correctionLabel.TextWrapping);
                // IsVisible is false without a presentation source; inspect the bound physical-mode container.
                Assert.Equal(Visibility.Visible, ((StackPanel)correction.Parent).Visibility);
                var readout = View("ReadoutPanel", theme, vm);
                Assert.Contains(ReadoutCostHelp.FWording, Mark(readout, "Readout.LocalisationHelp").Text);
                Assert.Contains("experimental readout · localisation cost (EV-36)", Inline(readout));
                var detector = View("DetectorPanel", theme, vm.DetectorWorkspace);
                Assert.Contains("not detection efficiency", Mark(detector, "Detector.AreaHelp").Text);
                Assert.DoesNotContain("not detection efficiency", Inline(detector));
                vm.ReadoutMode = ReadoutMode.DirectCrystal;
                var spectrum = View("SpectrumPanel", theme, vm.Spectrum);
                vm.ReadoutMode = ReadoutMode.FourOutputAnger;
                ReadoutRenderTests.Drain(); spectrum.UpdateLayout();
                Assert.Contains("virtual matched SiPMs", Mark(spectrum, "Spectrum.ChainHelp").Text);
                Assert.Contains("Experimental GAGG four-output readout", Inline(spectrum));
                Assert.DoesNotContain("virtual matched SiPMs", Inline(spectrum));
                vm.Waveform.View = new(new("ADC", [0, 1]), new("shaped", [0, 1]), [], "chain", "pulse",
                    "Real acquired arrival times. Summed energy channel; four position channels are not modelled. ADC simulation: shaped heights are not the analytic MCA spectrum. Finite filter warm-up; uncorrected shaped baseline. Window clipped. Partial acquisition window: no future deposits; pulse response and baseline beyond acquired live time are simulated.", 10, TimeSpan.Zero);
                Assert.Contains("four position channels", vm.Waveform.NoteHelp);
                Assert.DoesNotContain("four position channels", vm.Waveform.Note);
                Assert.Contains("Real acquired arrival times. ADC simulation: shaped heights ≠ analytic MCA. Window clipped. Partial acquisition window: no acquired future.", vm.Waveform.Note);
            }
        });

    [Fact]
    public void ModalLayer_UsesSingleTree_CancelDefault_EscapeCancels_AndIconsAreNamed()
        => Sta(() =>
        {
            foreach (string theme in new[] { "Dark", "Light" })
            {
                var service = new ModalService();
                var vm = Model(service);
                var shell = View("MainWindow", theme, vm);
                var host = Nodes(shell).OfType<ModalHost>().Single();
                CheckIconDeclarations();
                var request = service.ConfirmAsync("Discard the acquired data (1.0 live s, 20 counts)? Inputs unlock for the next acquisition.");
                ReadoutRenderTests.Drain(); shell.UpdateLayout();
                var layer = Nodes(shell).OfType<ModalLayer>().Single();
                Assert.Equal(Visibility.Visible, layer.Visibility);
                Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(layer));
                var peer = UIElementAutomationPeer.CreatePeerForElement(layer);
                Assert.Equal("Modal.Layer", peer.GetAutomationId());
                Assert.Equal("Discard acquired data?", peer.GetName());
                host.DialogName = "Hosted content fixture";
                Assert.Equal("Hosted content fixture", peer.GetName());
                var cancel = Nodes(layer).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "Modal.Cancel");
                Assert.True(cancel.IsDefault);
                Assert.False(Nodes(shell).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "UnderlyingInput").IsEnabled);
                // Routed Escape without a presentation source or real keyboard input.
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, new DetachedSource(), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                host.RaiseEvent(key);
                Assert.True(key.Handled);
                Assert.False(request.GetAwaiter().GetResult());
                ReadoutRenderTests.Drain(); shell.UpdateLayout();
                Assert.Equal(Visibility.Collapsed, layer.Visibility);
                request = service.ConfirmAsync("confirm fixture");
                service.ConfirmCommand.Execute(null);
                Assert.True(request.GetAwaiter().GetResult());
                shell.DataContext = null;
            }
        });

    private static IEnumerable<DependencyObject> Nodes(DependencyObject root) => ReadoutRenderTests.Descendants(root);
    private static void CheckIconDeclarations()
    {
        string repo = AppContext.BaseDirectory;
        while (!System.IO.File.Exists(System.IO.Path.Combine(repo, "Gcam.sln"))) repo = System.IO.Directory.GetParent(repo)!.FullName;
        var icons = new[] { "ThemeToggle", "ResetAcquisition", "AddSource", "RemoveSource", "ClearMeasurements", "DeleteMeasurement", "Readout.Cancel", "Imaging.CancelSweep", "Waveform.Latest", "Waveform.Next" };
        var buttons = new[] { "MainWindow", "ReadoutPanel", "ImagingOptionsPanel", "WaveformPanel" }
            .SelectMany(name => System.Xml.Linq.XDocument.Load(System.IO.Path.Combine(repo, "src/Gcam.Studio/Views", name + ".xaml")).Descendants())
            .Where(e => e.Name.LocalName == "Button").ToArray();
        foreach (string id in icons)
        {
            var button = Assert.Single(buttons, b => (string?)b.Attribute("AutomationProperties.AutomationId") == id);
            Assert.NotNull(button.Attribute("ToolTip")); Assert.NotNull(button.Attribute("AutomationProperties.Name"));
            Assert.Equal(id == "ThemeToggle" ? "{StaticResource Button.Theme}" : "{StaticResource Button.Icon}", (string?)button.Attribute("Style"));
            if (id == "DeleteMeasurement")
            {
                Assert.Equal("{StaticResource Icon.Remove}", (string?)button.Attribute("Content"));
                Assert.Equal("Delete selected measurement (Delete)", (string?)button.Attribute("ToolTip"));
            }
        }
    }

    [Fact]
    public void HelpMarks_HaveNamedRows_AndIconFramesShareEnabledDisabledAndFocusStyles()
        => Sta(() =>
        {
            string repo = AppContext.BaseDirectory;
            while (!System.IO.File.Exists(System.IO.Path.Combine(repo, "Gcam.sln"))) repo = System.IO.Directory.GetParent(repo)!.FullName;
            foreach (string path in System.IO.Directory.EnumerateFiles(System.IO.Path.Combine(repo, "src/Gcam.Studio/Views"), "*.xaml"))
            foreach (var mark in System.Xml.Linq.XDocument.Load(path).Descendants().Where(e => e.Name.LocalName == "HelpMark"))
            {
                Assert.Equal("DockPanel", mark.Parent!.Name.LocalName);
                Assert.Equal("Right", (string?)mark.Attribute("DockPanel.Dock"));
                Assert.Contains(mark.Parent.Elements(), e => e != mark && e.Name.LocalName != "HelpMark");
            }
            foreach (string theme in new[] { "Dark", "Light" })
            {
                var shell = View("MainWindow", theme, Model(new ModalService()));
                var style = (Style)shell.FindResource("Button.Icon");
                Assert.Same(shell.FindResource(typeof(Button)), style.BasedOn);
                var button = new Button { Style = style, Content = shell.FindResource("Icon.Remove"), Resources = shell.Resources };
                button.Measure(new Size(100, 100)); button.Arrange(new Rect(0, 0, 100, 100));
                var chrome = (Border)button.Template.FindName("Chrome", button);
                Assert.Same(shell.FindResource("Brush.Bg.Raised"), chrome.Background);
                Assert.Same(shell.FindResource("Brush.Line.Control"), chrome.BorderBrush);
                Assert.Contains(button.Template.Triggers.OfType<Trigger>(), t => t.Property == UIElement.IsMouseOverProperty);
                Assert.Contains(button.Template.Triggers.OfType<Trigger>(), t => t.Property == UIElement.IsKeyboardFocusedProperty);
                button.IsEnabled = false;
                ReadoutRenderTests.Drain();
                Assert.Same(shell.FindResource("Brush.Bg.Surface"), chrome.Background);
                Assert.Same(shell.FindResource("Brush.Line"), chrome.BorderBrush);
                Assert.Same(shell.FindResource("Brush.Text.Disabled"), button.Foreground);
            }
        });
    private static FrameworkElement View(string name, string theme, object model)
    {
        var root = ReadoutRenderTests.ProductionView(name, theme);
        root.DataContext = model; root.Measure(new Size(1280, 800)); root.Arrange(new Rect(0, 0, 1280, 800));
        ReadoutRenderTests.Drain(); root.UpdateLayout(); return root;
    }
    private static MainViewModel Model(IConfirmationService? confirmation = null) => new(new NoAcquisition(), new Theme(), new SpectrumService(), confirmation: confirmation);
    private sealed class Theme : IThemeService { public AppTheme Current => AppTheme.Dark; public void Apply(AppTheme theme) { } }
    private sealed class NoAcquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS, double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null) => throw new InvalidOperationException();
    }
    private sealed class DetachedSource : PresentationSource
    {
        public override System.Windows.Media.Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override System.Windows.Media.CompositionTarget GetCompositionTargetCore() => null!;
    }
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
