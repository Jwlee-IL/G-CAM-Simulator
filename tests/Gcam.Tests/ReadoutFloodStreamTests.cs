using Gcam.Configuration;
using Gcam.Simulation;

namespace Gcam.Tests;

/// <summary>The streamed calibration flood (recipe v2's 8k / 32k levels) is the materialised flood, history by history,
/// and a shorter flood of the same seed is its prefix (nested refinement levels).</summary>
public sealed class ReadoutFloodStreamTests
{
    [Fact]
    public void Stream_EqualsTransport_AndShorterFloodIsAPrefix()
    {
        var cfg = new SimulationConfig();
        cfg.Detector.PixelsX = cfg.Detector.PixelsY = 4;
        cfg.Detector.Material = "GAGG";
        var full = ReadoutFlood.Transport(cfg, 661.7, 400, 17);
        var streamed = ReadoutFlood.Stream(cfg, 661.7, 400, 17).ToList();
        var prefix = ReadoutFlood.Transport(cfg, 661.7, 100, 17);
        Assert.Equal(400, streamed.Count);
        for (int h = 0; h < full.Count; h++) Assert.Equal(full[h], streamed[h]);
        for (int h = 0; h < prefix.Count; h++) Assert.Equal(full[h], prefix[h]);
    }

    [Fact]
    public void Stream_ValidatesEagerly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReadoutFlood.Stream(new SimulationConfig(), 0, 10, 1));
        Assert.Throws<ArgumentNullException>(() => ReadoutFlood.Stream(null!, 661.7, 10, 1));
    }
}
