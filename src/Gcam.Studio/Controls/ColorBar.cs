using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Rendering;

namespace Gcam.Studio.Controls;

/// <summary>
/// Horizontal colour scale with numeric ticks, so a heatmap's colours can be read as values.
/// Bind <see cref="Minimum"/>/<see cref="Maximum"/> to <see cref="HeatmapView.DataMin"/>/<see cref="HeatmapView.DataMax"/>.
/// </summary>
/// <remarks>Tick text comes from <see cref="TickFormatter"/>: one shared ×10ⁿ and one decimal count per scale.</remarks>
public sealed class ColorBar : FrameworkElement
{
    private const double LabelGap = 4;
    private const double LabelSize = 11;
    private static readonly BitmapSource Gradient = BuildGradient();

    public ColorBar()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Linear);
    }

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(ColorBar), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(ColorBar), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public static readonly DependencyProperty TickCountProperty = DependencyProperty.Register(
        nameof(TickCount), typeof(int), typeof(ColorBar), new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsRender));

    public int TickCount { get => (int)GetValue(TickCountProperty); set => SetValue(TickCountProperty, value); }

    public static readonly DependencyProperty BarHeightProperty = DependencyProperty.Register(
        nameof(BarHeight), typeof(double), typeof(ColorBar),
        new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Height of the gradient strip (set from <c>Size.ColorBar</c>).</summary>
    public double BarHeight { get => (double)GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(ColorBar), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register(
        nameof(FontFamily), typeof(FontFamily), typeof(ColorBar),
        new FrameworkPropertyMetadata(new FontFamily("Consolas"), FrameworkPropertyMetadataOptions.AffectsRender));

    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width, BarHeight + LabelGap + LabelSize + 3);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        if (w <= 0) return;
        dc.DrawImage(Gradient, new Rect(0, 0, w, BarHeight));

        var labels = TickFormatter.Labels(Minimum, Maximum, TickCount);
        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        for (int i = 0; i < labels.Count; i++)
        {
            double t = i / (double)(labels.Count - 1);
            var text = new FormattedText(labels[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, LabelSize, Foreground, ppd);
            // First label left-aligned, last right-aligned, the rest centred on their tick.
            double x = i == 0 ? 0 : i == labels.Count - 1 ? w - text.Width : t * w - text.Width / 2;
            dc.DrawText(text, new Point(x, BarHeight + LabelGap));
        }
    }

    private static BitmapSource BuildGradient()
    {
        var lut = Colormap.Viridis.ToArray();
        var bmp = BitmapSource.Create(lut.Length, 1, 96, 96, PixelFormats.Bgra32, null, lut, lut.Length * 4);
        bmp.Freeze();
        return bmp;
    }
}
