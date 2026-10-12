using System.Collections;
using Gcam.Core;

namespace Gcam.Studio.Services;

/// <summary>A calibration flood produced history by history (never materialised): a High-res 661.7 keV flood at the
/// pinned 64 000 histories per crystal would need about 12 GiB as a list (Turn 7). Calibration enumerates it once;
/// indexing is refused.</summary>
internal sealed class StreamedFlood(Func<IEnumerable<InteractionSite[]>> source, int count) : IReadOnlyList<InteractionSite[]>
{
    public int Count => count;
    public InteractionSite[] this[int index] => throw new NotSupportedException("A streamed flood has no random access.");
    public IEnumerator<InteractionSite[]> GetEnumerator() => source().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
