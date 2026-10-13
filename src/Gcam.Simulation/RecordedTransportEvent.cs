using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>An event and its owned pre-optical sites. Pending producers transfer this immutable pair.</summary>
public sealed class RecordedTransportEvent
{
    public DetectedEvent Event { get; }
    public IReadOnlyList<InteractionSite> Sites { get; }
    public TransportOrigin Origin { get; }

    public RecordedTransportEvent(DetectedEvent @event, IEnumerable<InteractionSite> sites, TransportOrigin origin)
    {
        Event = @event;
        Sites = Array.AsReadOnly(sites.ToArray());
        Origin = origin;
    }
}
