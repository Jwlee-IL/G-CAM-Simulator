using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Tests;

/// <summary>Records every per-line request and returns a successful (or scripted) preparation carrying the plan's keys.</summary>
internal sealed class FakeReadoutPreparation(ReadoutOfferRecord? offers = null) : IReadoutPreparationService
{
    public ReadoutOfferRecord Offers { get; } = offers ?? ReadoutOfferFixture.Record();
    public List<ReadoutLinePlan> Requests { get; } = [];
    /// <summary>Lines whose preparation fails with this literal message.</summary>
    public Dictionary<double, string> Failures { get; } = [];
    /// <summary>Lines whose preparation is cancelled (throws as a cancelled token would).</summary>
    public HashSet<double> Cancel { get; } = [];

    public Task<ReadoutPreparation> PrepareAsync(OpticsSettings optics, DetectorSettings detector, ReadoutLinePlan line,
        IProgress<ReadoutPrepareProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(line);
        if (Cancel.Contains(line.LineKeV)) throw new OperationCanceledException(cancellationToken);
        string module = ReadoutPolicy.Key(optics, detector);
        bool ok = !Failures.TryGetValue(line.LineKeV, out string? failure);
        int n = optics.DetectorPixels;
        return Task.FromResult(new ReadoutPreparation(Guid.NewGuid(), module, ok, failure,
            new DetectorImage(1, 1).ReadOnlyCopy(), [-1], [], [], 0, 0, 0, 0, TimeSpan.Zero, 8, 100, 1, 1000, 100)
        {
            LineKeV = line.LineKeV, Plan = line, CalibrationKey = ReadoutPolicy.CalibrationKey(module, line),
            Module = new(n, n, optics.PixelPitchMm, detector.ReflectorGapMm, n, n, optics.PixelPitchMm, optics.PixelPitchMm - detector.ReflectorGapMm),
            CorrectionHistogram = Enumerable.Repeat(10, n * n).ToArray(),
        });
    }
}
