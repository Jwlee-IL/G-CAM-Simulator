namespace Gcam.Studio.Core.Services;

public interface IFocusSweepService
{
    Task<FocusSweepResult> SweepAsync(FocusSweepRequest request, CancellationToken cancellationToken = default);
}
