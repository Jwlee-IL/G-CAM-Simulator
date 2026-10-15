using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>Projects one retained physical prefix; never acquires or calibrates.</summary>
public interface IReadoutCompareService
{
    Task<CompareResult> ProcessAsync(CompareKey key, AcquisitionSnapshot snapshot,
        IReadOnlyList<SceneSource> scene, CompareSettings settings, CancellationToken cancellationToken = default);
}
