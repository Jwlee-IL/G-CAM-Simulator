namespace Gcam.Studio.Core.Services;

/// <summary>Builds one bounded scope window from a retained acquisition on a worker.</summary>
public interface IWaveformService
{
    Task<WaveformView> ProcessAsync(AcquisitionSnapshot snapshot, WaveformSettings settings,
        CancellationToken cancellationToken = default);
}
