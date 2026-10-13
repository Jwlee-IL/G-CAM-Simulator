using System.Windows;
using System.Windows.Threading;

namespace Gcam.Studio.Controls;

/// <summary>Focuses an element when a view model's request counter changes (Core has no WPF, so it cannot focus): bind
/// <c>c:FocusRequest.Counter</c> to an int the view model increments. A request made while the element is not loaded (its
/// tab not yet shown) is served once on Loaded; an unchanged counter never refocuses.</summary>
public static class FocusRequest
{
    public static readonly DependencyProperty CounterProperty = DependencyProperty.RegisterAttached("Counter", typeof(int),
        typeof(FocusRequest), new PropertyMetadata(0, OnCounterChanged));
    private static readonly DependencyProperty ServedProperty = DependencyProperty.RegisterAttached("Served", typeof(int),
        typeof(FocusRequest), new PropertyMetadata(0));

    public static int GetCounter(DependencyObject element) => (int)element.GetValue(CounterProperty);
    public static void SetCounter(DependencyObject element, int value) => element.SetValue(CounterProperty, value);

    private static void OnCounterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;
        element.Loaded -= Serve; element.Loaded += Serve;
        if (element.IsLoaded) Serve(element, new RoutedEventArgs());
    }

    private static void Serve(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        int counter = GetCounter(element);
        if (counter == (int)element.GetValue(ServedProperty)) return;
        element.SetValue(ServedProperty, counter);
        // After the tab switch has laid the element out.
        element.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => element.Focus()));
    }
}
