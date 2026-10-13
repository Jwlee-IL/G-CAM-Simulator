using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Gcam.Configuration;
using Gcam.Studio.Controls;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;
using Gcam.Studio.Services;

namespace Gcam.Studio.RenderTests;

/// <summary>The modal focus boundary and popups owned by dialog controls (desktop run 2026-10-13: the Options dialog's
/// combo could not keep its drop-down, because the boundary walked the visual tree only and a popup's root has no visual
/// parent). Offscreen and windowless: a drop-down cannot be opened without creating its popup window, and real keyboard
/// focus needs a presentation source, so the drop-down's content is laid out detached under its Popup (the same break in the
/// visual tree as an open popup root) and the focus request is raised as the routed preview event the keyboard raises.</summary>
public sealed class ModalFocusRenderTests
{
    [Fact]
    public void OptionsComboDropDown_KeepsFocusInsideTheDialog_FocusOutsideIsPulledBack()
        => Sta(() =>
        {
            foreach (string theme in new[] { "Dark", "Light" })
            {
                var vm = new MainViewModel(new NoAcquisition(), new Theme(), new SpectrumService());
                var shell = ReadoutRenderTests.ProductionView("MainWindow", theme);
                shell.DataContext = vm;
                Layout(shell);
                vm.OpenOptionsCommand.Execute(null);
                var draft = vm.Options!;
                draft.SelectedTab = OptionsTab.Environment;
                var options = ReadoutRenderTests.ProductionView("OptionsView", theme);
                options.DataContext = draft;
                var host = Nodes(shell).OfType<ModalHost>().Single();
                host.DialogContent = options;
                Layout(shell);
                var layer = Nodes(shell).OfType<ModalLayer>().Single();
                Assert.Equal(Visibility.Visible, layer.Visibility);

                var combo = Nodes(options).OfType<ComboBox>().Single(c => AutomationProperties.GetAutomationId(c) == "Acquisition.AmbientBound");
                var popup = Assert.IsType<Popup>(combo.Template.FindName("PART_Popup", combo));
                var dropDown = Assert.IsAssignableFrom<FrameworkElement>(popup.Child);
                dropDown.Measure(new Size(400, 300)); dropDown.Arrange(new Rect(dropDown.DesiredSize)); ReadoutRenderTests.Drain();
                var items = Nodes(dropDown).OfType<ComboBoxItem>().ToArray();
                Assert.Equal(Enum.GetValues<AmbientGeometry>().Length, items.Length);
                var item = items.Single(i => Equals(i.Content, AmbientGeometry.BareCrystalAllFaces));
                Assert.Null(System.Windows.Media.VisualTreeHelper.GetParent(dropDown)); // the break an open popup root has

                // The drop-down's item is inside the dialog: the boundary lets the combo focus it.
                Assert.False(RequestFocus(host, item), $"{theme}: focus on the dialog's drop-down item was pulled back");
                Assert.True(host.IsInsideDialog(item));
                // Focus outside the dialog (the disabled underlying content) is still refused and pulled back.
                var underlying = Nodes(shell).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "UnderlyingInput");
                Assert.True(RequestFocus(host, underlying), $"{theme}: focus outside the dialog was not pulled back");
                Assert.False(host.IsInsideDialog(underlying));

                // Popups placed on a control (tooltips such as a HelpMark's, context menus) follow their placement target.
                var mark = Nodes(options).OfType<HelpMark>().Single(m => AutomationProperties.GetAutomationId(m) == "Acquisition.BackgroundHelp");
                var placedInside = new Popup { PlacementTarget = mark, Child = new Button() };
                Assert.True(host.IsInsideDialog(placedInside.Child));
                var placedOutside = new Popup { PlacementTarget = underlying, Child = new Button() };
                Assert.False(host.IsInsideDialog(placedOutside.Child));
                var tip = Assert.IsType<ToolTip>(mark.ToolTip);
                Assert.Same(mark, tip.PlacementTarget);

                draft.CancelCommand.Execute(null);
                host.ClearValue(ModalHost.DialogContentProperty);
                shell.DataContext = null;
            }
        });

    /// <summary>Raises the keyboard's preview focus event for <paramref name="target"/>; true when the boundary refused it.
    /// Also checks that the event reached the host, so a refusal-free result is not a route that missed the boundary.</summary>
    private static bool RequestFocus(ModalHost host, UIElement target)
    {
        bool reached = false;
        KeyboardFocusChangedEventHandler seen = (_, _) => reached = true;
        host.AddHandler(Keyboard.PreviewGotKeyboardFocusEvent, seen, handledEventsToo: true);
        var request = new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, null, target) { RoutedEvent = Keyboard.PreviewGotKeyboardFocusEvent };
        target.RaiseEvent(request);
        host.RemoveHandler(Keyboard.PreviewGotKeyboardFocusEvent, seen);
        Assert.True(reached, "the focus request did not route to the modal host");
        return request.Handled;
    }

    private static void Layout(FrameworkElement root)
    {
        root.Measure(new Size(1280, 800)); root.Arrange(new Rect(0, 0, 1280, 800));
        ReadoutRenderTests.Drain(); root.UpdateLayout();
    }
    private static IEnumerable<DependencyObject> Nodes(DependencyObject root) => ReadoutRenderTests.Descendants(root);
    private sealed class Theme : IThemeService { public AppTheme Current => AppTheme.Dark; public void Apply(AppTheme theme) { } }
    private sealed class NoAcquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS, double speed,
            DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null) => throw new InvalidOperationException();
    }
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
