using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Gcam.Studio.Converters;

/// <summary>true → Collapsed, false → Visible: for empty-state hints shown while a list has no items.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public static InverseBoolToVisibilityConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
