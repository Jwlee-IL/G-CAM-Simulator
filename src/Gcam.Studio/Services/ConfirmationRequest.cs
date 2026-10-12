namespace Gcam.Studio.Services;

/// <summary>Content of the discard confirmation; other modal content can use its own template.</summary>
public sealed record ConfirmationRequest(string Message)
{
    public string Title => "Discard acquired data?";
}
