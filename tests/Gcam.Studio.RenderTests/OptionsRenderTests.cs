using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Gcam.Configuration;
using Gcam.Studio.Controls;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;
using Gcam.Studio.Services;

namespace Gcam.Studio.RenderTests;

public sealed class OptionsRenderTests
{
    [Fact]
    public void OptionsTabs_MovedIdsAndLocks_UseTheDraft_AndCloseOnlyWhenLocked()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var editableBounds = new Dictionary<string, (Rect View, Rect Tabs, Point ActionEdge)>();
                foreach (string theme in new[] { "Dark", "Light" })
                foreach (bool locked in new[] { false, true })
                {
                    var vm = new MainViewModel(new UnusedAcquisition(), new Theme(), new SpectrumService());
                    vm.IsRunning = locked;
                    vm.OpenOptionsCommand.Execute(null);
                    var draft = vm.Options!;
                    var view = ReadoutRenderTests.ProductionView("OptionsView", theme); view.DataContext = draft;
                    (Rect View, Rect Action, Rect Cancel, Rect Tabs)? firstBounds = null;
                    foreach (var tab in Enum.GetValues<OptionsTab>())
                    {
                        draft.SelectedTab = tab;
                        view.Measure(new Size(450, 600)); view.Arrange(new Rect(0, 0, 450, 600)); ReadoutRenderTests.Drain(); view.UpdateLayout();
                        var nodes = ReadoutRenderTests.Descendants(view).OfType<FrameworkElement>().ToArray();
                        foreach (string id in new[] { "AcquisitionSeed", "Detector.Gap", "Detector.GainSigma", "Detector.GainSeed", "Acquisition.AmbientDose", "Acquisition.Background" })
                        {
                            var field = Assert.Single(nodes.OfType<TextBox>(), t => AutomationProperties.GetAutomationId(t) == id);
                            Assert.Equal(!locked, field.IsEnabled);
                            var fieldTab = id == "AcquisitionSeed" ? OptionsTab.Acquisition
                                : id.StartsWith("Detector.", StringComparison.Ordinal) ? OptionsTab.Detector : OptionsTab.Environment;
                            if (tab != fieldTab) continue;
                            Assert.True(field.ActualWidth > 0);
                            Assert.Equal((double)view.FindResource("Size.Control"), field.ActualHeight);
                            if (locked)
                            {
                                Assert.Equal(((SolidColorBrush)view.FindResource("Brush.Text.Disabled")).Color,
                                    ((SolidColorBrush)field.Foreground).Color);
                                var chrome = (Border)field.Template.FindName("Chrome", field);
                                Assert.Equal(((SolidColorBrush)view.FindResource("Brush.Line")).Color,
                                    ((SolidColorBrush)chrome.BorderBrush).Color);
                            }
                        }
                        var close = Assert.Single(nodes.OfType<Button>(), b => AutomationProperties.GetAutomationId(b) == "Modal.Close");
                        var apply = Assert.Single(nodes.OfType<Button>(), b => AutomationProperties.GetAutomationId(b) == "Options.Apply");
                        Assert.Equal(locked ? Visibility.Visible : Visibility.Collapsed, close.Visibility);
                        Assert.Equal(locked ? Visibility.Collapsed : Visibility.Visible, apply.Visibility);
                        Rect Bounds(FrameworkElement element) => element.TransformToAncestor(view).TransformBounds(new Rect(element.RenderSize));
                        var cancel = Assert.Single(nodes.OfType<Button>(), b => AutomationProperties.GetAutomationId(b) == "Modal.Cancel");
                        var tabButton = Assert.Single(nodes.OfType<RadioButton>(), b => AutomationProperties.GetAutomationId(b) == "Options.AcquisitionTab");
                        var action = Bounds(locked ? close : apply);
                        var current = (new Rect(view.RenderSize), action, locked ? action : Bounds(cancel), Bounds(tabButton));
                        if (firstBounds is { } first) Assert.Equal(first, current);
                        else firstBounds = current;
                        var anchor = (new Rect(view.RenderSize), Bounds(tabButton), action.BottomRight);
                        if (editableBounds.TryGetValue(theme, out var editable)) Assert.Equal(editable, anchor);
                        else editableBounds.Add(theme, anchor);
                    }
                    if (!locked)
                    {
                        draft.SelectedTab = OptionsTab.Acquisition;
                        var seed = ReadoutRenderTests.Descendants(view).OfType<TextBox>().Single(t => AutomationProperties.GetAutomationId(t) == "AcquisitionSeed");
                        seed.Text = "-1"; seed.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                        draft.ApplyCommand.Execute(null);
                        Assert.True(draft.IsOpen); Assert.NotNull(draft.SeedError); Assert.Equal("", vm.SeedText);
                    }
                    draft.CancelCommand.Execute(null); view.DataContext = null;
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private sealed class Theme : IThemeService { public AppTheme Current => AppTheme.Dark; public void Apply(AppTheme theme) { } }
    private sealed class UnusedAcquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS, double speed,
            DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null) => throw new InvalidOperationException();
    }
}
