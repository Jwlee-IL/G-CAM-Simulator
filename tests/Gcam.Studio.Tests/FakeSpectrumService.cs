using Gcam.Core;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Tests;

internal sealed class FakeSpectrumService : ISpectrumService
{
    public double Resolution662 => 0.06;
    public double ResolvingTimeS => 730e-9;
    public int Calls { get; private set; }
    public List<SpectrumSettings> Settings { get; } = [];
    public List<IReadOnlyList<SpectrumLine>> LineRequests { get; } = [];
    public Task<SpectrumView> ProcessAsync(Guid acquisitionId, IReadOnlyList<DetectedEvent> events,
        IReadOnlyList<SpectrumLine> lines, SpectrumSettings settings, int seed = 909,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        Settings.Add(settings);
        LineRequests.Add(lines);
        return Task.FromResult(new SpectrumView([661.7], [(double)events.Count],
            [new SpectrumBand(lines, 600, 720, events.Count, 1)], events.Count, 0, 1,
            0.06, 730e-9, "test chain", TimeSpan.Zero) { BinEdgesKeV = [650, 675] });
    }
}
