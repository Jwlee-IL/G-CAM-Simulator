namespace Gcam.Studio.RenderTests;

/// <summary>Independent opt-in: never creates a window or uses desktop input.</summary>
public sealed class RenderSnapshotFactAttribute : FactAttribute
{
    public RenderSnapshotFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("GCAM_RENDER_SNAPSHOTS") != "1")
            Skip = "Set GCAM_RENDER_SNAPSHOTS=1 to render offscreen PNGs on an STA thread.";
    }
}
