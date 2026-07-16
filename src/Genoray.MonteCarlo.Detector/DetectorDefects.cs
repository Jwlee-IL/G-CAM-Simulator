namespace Genoray.MonteCarlo.Detector;

/// <summary>
/// A fixed, seeded map of BAD PIXELS on the crystal/SiPM array — the spatial defects a real detector carries that
/// per-crystal gain non-uniformity does not capture:
/// <list type="bullet">
/// <item><b>Dead</b> pixels (disconnected / failed channel): no response, a hole in the flood.</item>
/// <item><b>Hot</b> pixels (a channel firing on its own — dark-count / breakdown runaway): a source-INDEPENDENT
/// spurious spike on the flood.</item>
/// </list>
/// Either way the defect imprints a fixed structure that is NOT part of the mask code, so an ideal decoder's
/// correlation is pulled off the true peak. A system with a known bad-pixel map repairs both by interpolating over
/// the flagged pixels — the analogue of flood-field correction for discrete defects.
/// </summary>
public sealed class DetectorDefects
{
    public int Width { get; }
    public int Height { get; }

    /// <summary>Row-major: this pixel is dead (zero response).</summary>
    public bool[] Dead { get; }

    /// <summary>Row-major: this pixel is hot (adds spurious counts).</summary>
    public bool[] Hot { get; }

    /// <summary>Hot-pixel spurious level as a multiple of the mean live-pixel count.</summary>
    public double HotFactor { get; }

    public int DeadCount { get; }
    public int HotCount { get; }

    public DetectorDefects(int width, int height, double deadFraction, double hotFraction,
                           double hotFactor, int seed)
    {
        Width = width;
        Height = height;
        HotFactor = hotFactor;
        int n = width * height;
        Dead = new bool[n];
        Hot = new bool[n];

        var rng = new Random(seed);
        int dead = 0, hot = 0;
        for (int i = 0; i < n; i++)
        {
            // A pixel is at most one kind of defect (dead takes precedence).
            if (deadFraction > 0.0 && rng.NextDouble() < deadFraction) { Dead[i] = true; dead++; }
            else if (hotFraction > 0.0 && rng.NextDouble() < hotFraction) { Hot[i] = true; hot++; }
        }
        DeadCount = dead;
        HotCount = hot;
    }

    public bool IsDefective(int x, int y)
    {
        int i = y * Width + x;
        return Dead[i] || Hot[i];
    }
}
