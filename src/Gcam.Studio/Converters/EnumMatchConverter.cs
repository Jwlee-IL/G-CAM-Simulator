using System.Globalization;
using System.Windows.Data;

namespace Gcam.Studio.Converters;

/// <summary>
/// Binds a group of radio buttons to one enum property: checked when the value equals <c>ConverterParameter</c>,
/// and checking a button writes that value back.
/// </summary>
public sealed class EnumMatchConverter : IValueConverter
{
    public static EnumMatchConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && string.Equals(value.ToString(), parameter as string, StringComparison.Ordinal);

    // Unchecking is a side effect of checking another button in the group, so only "true" writes a value.
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string name ? Enum.Parse(targetType, name) : Binding.DoNothing;
}
