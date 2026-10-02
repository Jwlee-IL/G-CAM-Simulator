namespace Gcam.Studio.Core.Imaging;

/// <summary>UI-free rectangle in screen DIPs, used for overlay label packing.</summary>
public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool Contains(Vec2 point) => point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;
    public bool Contains(ScreenRect rect) => rect.X >= X && rect.Right <= Right && rect.Y >= Y && rect.Bottom <= Bottom;
    public bool IsSeparatedFrom(ScreenRect rect, double gap) =>
        Right + gap <= rect.X || rect.Right + gap <= X || Bottom + gap <= rect.Y || rect.Bottom + gap <= Y;
}
