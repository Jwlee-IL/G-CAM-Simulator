using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>Imaging tools and readings over the shell's shared run result.</summary>
public sealed class ImagingWorkspaceViewModel(MainViewModel shared) : WorkspaceViewModel("Imaging", "Workspace.Imaging")
{
    public MainViewModel Shared { get; } = shared;
    public MeasurementsViewModel Measurements { get; } = new();
    public string? PeakText => Shared.Result?.Estimate is { } e
        ? $"peak ({e.Position.X:F1}, {e.Position.Y:F1}) mm" : null;

    internal void Refresh(ImagingResult? result)
    {
        Measurements.Refresh(result);
        OnPropertyChanged(nameof(PeakText));
    }
}
