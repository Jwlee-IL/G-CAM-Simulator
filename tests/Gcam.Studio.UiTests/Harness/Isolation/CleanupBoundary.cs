using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Cleanup/record errors add issues without replacing the original product/assertion failure.</summary>
public static class CleanupBoundary
{
    public static Exception? Attempt(Exception? existing, Action action, List<UiRunManifest.Issue> issues, string issueType)
    {
        try { action(); }
        catch (Exception e) { issues.Add(new(issueType, e.GetType().Name + ": " + e.Message)); return existing ?? e; }
        return existing;
    }
}
