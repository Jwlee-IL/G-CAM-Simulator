using System.Globalization;
using System.Windows.Data;
using Gcam.Core;

namespace Gcam.Studio.Rendering;

/// <summary>Binds a <see cref="DetectorImage"/> to an <c>Image.Source</c>. Interim view until the heatmap control.</summary>
public sealed class DetectorImageToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DetectorImage img ? Colormap.ToBitmap(img) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
