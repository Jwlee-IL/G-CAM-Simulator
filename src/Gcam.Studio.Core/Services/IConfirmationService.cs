namespace Gcam.Studio.Core.Services;

/// <summary>Requests a deliberate confirmation before discarding acquired data.</summary>
public interface IConfirmationService
{
    Task<bool> ConfirmAsync(string message);
}
