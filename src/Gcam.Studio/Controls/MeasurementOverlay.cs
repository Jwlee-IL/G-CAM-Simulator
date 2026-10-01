using System.Collections;
using System.Windows;
using System.Windows.Documents;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Controls;

/// <summary>
/// Attached properties that put a <see cref="MeasurementAdorner"/> on a <see cref="HeatmapView"/> from XAML, so the
/// view stays declarative and its code-behind empty:
/// <code>
/// &lt;c:HeatmapView c:MeasurementOverlay.Session="{Binding Measurements}" c:MeasurementOverlay.Pane="Flood" … /&gt;
/// </code>
/// </summary>
/// <remarks>
/// The adorner lives in the window's adorner layer (above the image, clipped to it) and is created when the heatmap
/// loads and removed when it unloads, so it never outlives the element or keeps the ViewModels alive.
/// </remarks>
public static class MeasurementOverlay
{
    /// <summary>The measurement session (tool, items, selection). Setting it enables the overlay.</summary>
    public static readonly DependencyProperty SessionProperty = DependencyProperty.RegisterAttached(
        "Session", typeof(MeasurementsViewModel), typeof(MeasurementOverlay), new PropertyMetadata(null, OnChanged));

    public static MeasurementsViewModel? GetSession(DependencyObject d) => (MeasurementsViewModel?)d.GetValue(SessionProperty);
    public static void SetSession(DependencyObject d, MeasurementsViewModel? value) => d.SetValue(SessionProperty, value);

    /// <summary>Which image this is — measurements are kept per pane, each in its own mm frame.</summary>
    public static readonly DependencyProperty PaneProperty = DependencyProperty.RegisterAttached(
        "Pane", typeof(ImagePane), typeof(MeasurementOverlay), new PropertyMetadata(ImagePane.Flood, OnChanged));

    public static ImagePane GetPane(DependencyObject d) => (ImagePane)d.GetValue(PaneProperty);
    public static void SetPane(DependencyObject d, ImagePane value) => d.SetValue(PaneProperty, value);

    /// <summary>Draggable markers (<c>IPlaneMarker</c> items, e.g. the scene's sources) in this image's mm frame.</summary>
    public static readonly DependencyProperty MarkersProperty = DependencyProperty.RegisterAttached(
        "Markers", typeof(IEnumerable), typeof(MeasurementOverlay), new PropertyMetadata(null, OnChanged));

    public static IEnumerable? GetMarkers(DependencyObject d) => (IEnumerable?)d.GetValue(MarkersProperty);
    public static void SetMarkers(DependencyObject d, IEnumerable? value) => d.SetValue(MarkersProperty, value);

    /// <summary>The highlighted marker; set by the overlay when a marker is grabbed (binds two-way by default).</summary>
    public static readonly DependencyProperty SelectedMarkerProperty = DependencyProperty.RegisterAttached(
        "SelectedMarker", typeof(object), typeof(MeasurementOverlay),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnChanged));

    public static object? GetSelectedMarker(DependencyObject d) => d.GetValue(SelectedMarkerProperty);
    public static void SetSelectedMarker(DependencyObject d, object? value) => d.SetValue(SelectedMarkerProperty, value);

    /// <summary>Whether markers may be dragged (bind to the ViewModel's idle state).</summary>
    public static readonly DependencyProperty CanMoveMarkersProperty = DependencyProperty.RegisterAttached(
        "CanMoveMarkers", typeof(bool), typeof(MeasurementOverlay), new PropertyMetadata(true, OnChanged));

    public static bool GetCanMoveMarkers(DependencyObject d) => (bool)d.GetValue(CanMoveMarkersProperty);
    public static void SetCanMoveMarkers(DependencyObject d, bool value) => d.SetValue(CanMoveMarkersProperty, value);

    // The adorner instance, kept on the element so it can be found again on unload or property change.
    private static readonly DependencyProperty AdornerProperty = DependencyProperty.RegisterAttached(
        "Adorner", typeof(MeasurementAdorner), typeof(MeasurementOverlay), new PropertyMetadata(null));

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not HeatmapView view) return;
        if (e.Property == SessionProperty)
        {
            view.Loaded -= OnLoaded;
            view.Unloaded -= OnUnloaded;
            if (e.NewValue is not null)
            {
                view.Loaded += OnLoaded;
                view.Unloaded += OnUnloaded;
                if (view.IsLoaded) Attach(view);
            }
            else Detach(view);
        }
        (view.GetValue(AdornerProperty) as MeasurementAdorner)?.Rebind();
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => Attach((HeatmapView)sender);
    private static void OnUnloaded(object sender, RoutedEventArgs e) => Detach((HeatmapView)sender);

    private static void Attach(HeatmapView view)
    {
        if (view.GetValue(AdornerProperty) is not null) return;
        var layer = AdornerLayer.GetAdornerLayer(view);
        if (layer is null) return;   // no AdornerDecorator above (not inside a Window yet)
        var adorner = new MeasurementAdorner(view);
        layer.Add(adorner);
        view.SetValue(AdornerProperty, adorner);
        adorner.Rebind();
    }

    private static void Detach(HeatmapView view)
    {
        if (view.GetValue(AdornerProperty) is not MeasurementAdorner adorner) return;
        adorner.Unbind();
        AdornerLayer.GetAdornerLayer(view)?.Remove(adorner);
        view.ClearValue(AdornerProperty);
    }
}
