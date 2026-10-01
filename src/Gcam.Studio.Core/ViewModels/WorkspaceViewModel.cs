using CommunityToolkit.Mvvm.ComponentModel;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>A workspace shares the shell's scene and acquisition, and owns its view settings.</summary>
public abstract partial class WorkspaceViewModel(string title, string automationId) : ObservableObject
{
    [ObservableProperty] private bool _isActive;
    public string Title { get; } = title;
    public string AutomationId { get; } = automationId;
}
