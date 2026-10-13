namespace Gcam.Studio.Core.Services;

/// <summary>Hosts transient content inside the existing window, without any persistence.</summary>
public interface IModalService : IConfirmationService
{
    object? ActiveContent { get; }
    void Show(object content, Action cancel);
    void Dismiss(object content);
}
