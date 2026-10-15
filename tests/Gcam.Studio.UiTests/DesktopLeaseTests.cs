using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

public sealed class DesktopLeaseTests
{
    [Fact]
    public void LiveLease_RefusesAnotherRunnerEvenAfterExpiry_AndReleasesFinally()
    {
        string path = Path.Combine(IsolationPathsTests.Scratch(), "lease.json");
        DateTimeOffset now = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        using (var first = new DesktopLease(path, "first", 1, now, TimeSpan.FromSeconds(1)))
        {
            Assert.Throws<IOException>(() => new DesktopLease(path, "second", 1, now.AddSeconds(2), TimeSpan.FromSeconds(1)));
            Assert.Equal("pending", first.Current.ReleaseStatus);
        }
        using var second = new DesktopLease(path, "second", 1, now.AddSeconds(2), TimeSpan.FromSeconds(1));
        second.Dispose(); Assert.Equal("released", second.Current.ReleaseStatus);
    }
    [Fact]
    public void RetainedUnexpiredLease_RefusesLaunch_ExpiredRetainedLeaseMayRecover()
    {
        string path = Path.Combine(IsolationPathsTests.Scratch(), "lease.json"); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        DateTimeOffset now = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new DesktopLease.State("old", 1, now, now.AddMinutes(1), "pending")));
        Assert.Throws<IOException>(() => new DesktopLease(path, "new", 1, now, TimeSpan.FromMinutes(1)));
        using var recovered = new DesktopLease(path, "new", 1, now.AddMinutes(2), TimeSpan.FromMinutes(1));
        Assert.Equal("new", recovered.Current.RunId);
    }
}
