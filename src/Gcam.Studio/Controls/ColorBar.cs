using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gcam.Studio.Rendering;

namespace Gcam.Studio.Controls;

/// <summary>
/// Horizontal colour scale with numeric ticks, so a heatmap's colours can be read as values.
/// Bind <see cref="Minimum"/>/<see cref="Maximum"/> to <see cref="HeatmapView.DataMin"/>/<see cref="HeatmapView.DataMax"/>.
/// </summary>
public sealed class ColorBar : FrameworkElement
{
    private const double BarHeight = 8;
    private const double LabelGap = 4;
    private static readonly BitmapSource Gradient = BuildGradient();

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(ColorBar), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(ColorBar), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public static readonly DependencyProperty TickCountProperty = DependencyProperty.Register(
        nameof(TickCount), typeof(int), typeof(ColorBar), new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsRender));

    public int TickCount { get => (int)GetValue(TickCountProperty); set => SetValue(TickCountProperty, value); }

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(ColorBar), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public static readonly DependencyProperty FontFamilyProperty = TextElementFontFamily();

    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    private static DependencyProperty TextElementFontFamily() => DependencyProperty.Register(
        nameof(FontFamily), typeof(FontFamily), typeof(ColorBar), new FrameworkPropertyMetadata(new FontFamily("Consolas"), FrameworkPropertyMetadataOptions.AffectsRender));

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width, BarHeight + LabelGap + 14);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        if (w <= 0) return;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Linear);
        dc.DrawImage(Gradient, new Rect(0, 0, w, BarHeight));

        int ticks = Math.Max(2, TickCount);
        double span = Maximum - Minimum;
        string format = Math.Abs(span) >= 100 ? "N0" : Math.Abs(span) >= 1 ? "0.##" : "G3";
        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        for (int i = 0; i < ticks; i++)
        {
            double t = i / (double)(ticks - 1);
            string label = (Minimum + span * t).ToString(format, CultureInfo.InvariantCulture);
            if (label is "-0") label = "0";   // a tiny negative that rounds to zero
            var text = new FormattedText(label,
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                11, Foreground, ppd);
            // First label left-aligned, last right-aligned, the rest centred on their tick.
            double x = i == 0 ? 0 : i == ticks - 1 ? w - text.Width : t * w - text.Width / 2;
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
