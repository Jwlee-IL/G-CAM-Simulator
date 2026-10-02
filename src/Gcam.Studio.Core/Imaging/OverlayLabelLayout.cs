namespace Gcam.Studio.Core.Imaging;

/// <summary>Packs measured chips near their anchors without moving markers. Null means no bounded placement fits.</summary>
public static class OverlayLabelLayout
{
    public static IReadOnlyList<ScreenRect?> Arrange(
        IReadOnlyList<(ScreenRect Preferred, Vec2 Anchor)> labels, ScreenRect bounds,
        IReadOnlyList<ScreenRect> obstacles, double gap)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(obstacles);
        Validate(bounds);
        if (!double.IsFinite(gap) || gap < 0) throw new ArgumentOutOfRangeException(nameof(gap));
        foreach (var obstacle in obstacles) Validate(obstacle);
        var occupied = obstacles.ToList();
        var result = new List<ScreenRect?>();
        // Stable input order keeps the same labels prioritized on every resize/zoom.
        foreach (var (preferred, anchor) in labels)
        {
            Validate(preferred);
            if (!double.IsFinite(anchor.X) || !double.IsFinite(anchor.Y))
                throw new ArgumentOutOfRangeException(nameof(labels));
            if (preferred.Width > bounds.Width || preferred.Height > bounds.Height)
            {
                result.Add(null);
                continue;
            }
            var xs = new List<double> { preferred.X, anchor.X - gap - preferred.Width, anchor.X + gap,
                bounds.X, bounds.Right - preferred.Width };
            var ys = new List<double> { preferred.Y, anchor.Y - gap - preferred.Height, anchor.Y + gap,
                bounds.Y, bounds.Bottom - preferred.Height };
            // Obstacle edges give candidates even when several labels share a lane.
            foreach (var rect in occupied)
            {
                xs.Add(rect.X - gap - preferred.Width); xs.Add(rect.Right + gap);
                ys.Add(rect.Y - gap - preferred.Height); ys.Add(rect.Bottom + gap);
            }
            var candidates = (from x in xs.Distinct()
                              from y in ys.Distinct()
                              select new ScreenRect(Math.Clamp(x, bounds.X, bounds.Right - preferred.Width),
                                  Math.Clamp(y, bounds.Y, bounds.Bottom - preferred.Height), preferred.Width, preferred.Height))
                .Distinct().OrderBy(r => Math.Pow(r.X - preferred.X, 2) + Math.Pow(r.Y - preferred.Y, 2));
            ScreenRect? placement = null;
            foreach (var candidate in candidates)
                if (occupied.All(r => candidate.IsSeparatedFrom(r, gap))) { placement = candidate; break; }
            result.Add(placement);
            if (placement is { } placed) occupied.Add(placed);
        }
        return result;
    }

    private static void Validate(ScreenRect rect)
    {
        if (!double.IsFinite(rect.X) || !double.IsFinite(rect.Y) || !double.IsFinite(rect.Right) ||
            !double.IsFinite(rect.Bottom) || rect.Width < 0 || rect.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(rect));
    }
}
