using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Tests;

internal sealed class FakeConfirmation(bool confirmed = true) : IConfirmationService
{
    public string? Message { get; private set; }
    public Task<bool> ConfirmAsync(string message) { Message = message; return Task.FromResult(confirmed); }
}
