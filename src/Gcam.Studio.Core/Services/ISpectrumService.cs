using Gcam.Core;

namespace Gcam.Studio.Core.Services;

/// <summary>Processes a cumulative, ordered immutable acquisition prefix off the caller thread.</summary>
public interface ISpectrumService
{
    double Resolution662 { get; }
    double ResolvingTimeS { get; }
    Task<SpectrumView> ProcessAsync(Guid acquisitionId, IReadOnlyList<DetectedEvent> events,
        IReadOnlyList<SpectrumLine> lines, SpectrumSettings settings, int seed = 909,
        CancellationToken cancellationToken = default);

    /// <summary>Per event, the measured energy (keV) <see cref="ProcessAsync"/> histograms for the same events, settings
    /// and seed: the same index-keyed draw, no new randomness. Under pile-up a group's measured sum sits on its first
    /// event and absorbed events are NaN. Null when this service does not expose it or for the physical readout.</summary>
    IReadOnlyList<double>? MeasuredEnergies(IReadOnlyList<DetectedEvent> events, SpectrumSettings settings, int seed = 909) => null;
}
