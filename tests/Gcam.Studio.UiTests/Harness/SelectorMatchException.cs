namespace Gcam.Studio.UiTests.Harness;

/// <summary>An exact-id selector with zero or multiple matches. Only zero matches are retryable inside a polling wait.</summary>
public sealed class SelectorMatchException(string automationId, int matches)
    : InvalidOperationException($"selector '{automationId}' matched {matches} elements (expected exactly 1)")
{
    public int Matches { get; } = matches;
}
