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
}
