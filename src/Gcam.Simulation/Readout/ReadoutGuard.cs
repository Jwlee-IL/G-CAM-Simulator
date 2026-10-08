using Gcam.Configuration;

namespace Gcam.Simulation;

/// <summary>Stage 1 of TODO-19 runs the physical readout only in <see cref="ReadoutStudy"/>; every other engine path
/// assigns events directly to crystals. A scenario that asks for a physical readout on such a path is refused rather
/// than silently simulated with the direct assignment. Pure check: no random number is drawn.</summary>
public static class ReadoutGuard
{
    public static void RequireDirect(SimulationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Detector.Readout is { Mode: not ReadoutMode.DirectCrystal } readout)
            throw new NotSupportedException(
                $"Detector.Readout.Mode = {readout.Mode} is simulated by ReadoutStudy only (TODO-19 stage 1); this path assigns crystals directly.");
    }
}
