namespace Gcam.Studio.RenderTests;

/// <summary>The long offscreen render tests (PlotViewRenderTests, ReadoutRenderTests) share this xUnit collection, so they run
/// one after the other instead of in parallel: each keeps its own deadlock deadline without CPU contention.</summary>
[CollectionDefinition(Name)]
public sealed class OffscreenRenderCollection
{
    public const string Name = "Offscreen renders";
}
