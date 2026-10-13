using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>One in-window modal request at a time. The host can display any content through a data template.</summary>
public sealed partial class ModalService : ObservableObject, IModalService
{
    private Action? _cancelContent;
    private TaskCompletionSource<bool>? _confirmation;
    [ObservableProperty] private object? _activeContent;
    public Task<bool> ConfirmAsync(string message)
    {
        if (ActiveContent is not null) throw new InvalidOperationException("A modal request is already open.");
        _confirmation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ActiveContent = new ConfirmationRequest(message);
        return _confirmation.Task;
    }

    [RelayCommand] private void Confirm() => Close(true);
    public void Show(object content, Action cancel)
    {
        if (ActiveContent is not null) throw new InvalidOperationException("A modal request is already open.");
        _cancelContent = cancel;
        ActiveContent = content;
    }
    public void Dismiss(object content)
    {
        if (!ReferenceEquals(ActiveContent, content)) return;
        _cancelContent = null; ActiveContent = null;
    }
    [RelayCommand] private void Cancel()
    {
        if (_cancelContent is { } cancel) cancel();
        else Close(false);
    }
    private void Close(bool confirmed)
    {
        var pending = _confirmation;
        _confirmation = null;
        ActiveContent = null;
        pending?.TrySetResult(confirmed);
    }
}
