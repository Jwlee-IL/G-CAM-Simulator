namespace Gcam.Studio.RenderTests;

/// <summary>The readout offscreen render (ReadoutRenderTests). It parses its views detached on its own STA thread and needs
/// no Application, so its collection runs in parallel with the main-window renders (MainWindowRenderCollection), which stay
/// one after the other on their shared render thread.</summary>
[CollectionDefinition(Name)]
public sealed class OffscreenRenderCollection
{
    public const string Name = "Offscreen renders";
}
