using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Observation;

namespace Gcam.Studio.UiTests;

public sealed class DocumentsWatcherTests
{
    private static string SurfaceList => Path.Combine(RepoPaths.Root(), "tests", "Gcam.Studio.UiTests", "Harness", "Isolation", "surface-list.json");
    // Recorded paths are final (long) paths; a CI temporary folder can be an 8.3 name, so expectations use the same form.
    private static string Final(string path) => DocumentsWatcher.ResolveRoot(path);
    [Fact]
    public void TemporaryRoot_RecordsTransientCreateChangeRenameDelete_AndControl()
    {
        using var scratch = ObservationFixtures.Scratch(); string root = Final(scratch);
        using var watcher = new DocumentsWatcher(scratch, SurfaceList);
        Assert.True(watcher.PositiveControl("gcam-report-20261022-120000.html"));
        string child = Path.Combine(root, "child"); Directory.CreateDirectory(child);
        string original = Path.Combine(child, "gcam-events-20261022-120001.csv");
        string renamed = Path.Combine(child, "gcam-events-20261022-120002.csv");
        File.WriteAllText(original, "first");
        Assert.True(SpinWait.SpinUntil(() => watcher.Snapshot().Events.Any(e => e.Path == original && e.Operation == "create"), 3000));
        File.AppendAllText(original, "second");
        File.Move(original, renamed);
        File.Delete(renamed); // Only this fixture's own transient file.
        File.WriteAllText(Path.Combine(root, "unrelated.txt"), "ignore");
        Assert.True(SpinWait.SpinUntil(() => watcher.Snapshot().Events.Any(e => e.Path == renamed && e.Operation == "delete"), 3000));
        var result = watcher.Snapshot();
        Assert.True(result.Passed); Assert.Equal(root, result.ResolvedRoot); Assert.Empty(result.BlindSpots);
        Assert.Contains(result.Events, e => e.Operation == "change" && e.Path == original);
        Assert.Contains(result.Events, e => e.Operation == "rename" && e.Path == renamed && e.OldPath == original);
        Assert.Contains(result.Events, e => e.HarnessControl);
        Assert.DoesNotContain(result.Events, e => e.Path.EndsWith("unrelated.txt", StringComparison.Ordinal));
    }
    [Fact]
    public void MissingRootAndOverflow_AreFailedCoverage()
    {
        using var scratch = ObservationFixtures.Scratch();
        using var absent = new DocumentsWatcher(Path.Combine(scratch, "absent"), SurfaceList);
        Assert.False(absent.Snapshot().CoversRoot); Assert.NotEmpty(absent.Snapshot().Errors);
        using var watcher = new DocumentsWatcher(scratch, SurfaceList);
        watcher.ReportError(new InternalBufferOverflowException("fixture overflow"));
        Assert.False(watcher.Snapshot().Passed); Assert.False(watcher.Snapshot().CoversRoot);
        Assert.Contains(watcher.Snapshot().Errors, e => e.Contains("InternalBufferOverflowException", StringComparison.Ordinal));
    }
    [Fact]
    public void Control_RefusesUnmatchedAndOutsidePaths()
    {
        using var scratch = ObservationFixtures.Scratch();
        using var watcher = new DocumentsWatcher(scratch, SurfaceList);
        Assert.Throws<ArgumentException>(() => watcher.PositiveControl("other.txt"));
        Assert.Throws<ArgumentException>(() => watcher.PositiveControl("../gcam-report-20261022-120000.html"));
        Assert.False(watcher.Snapshot().Passed);
    }
    [Fact]
    public void ParentDirectoryRenameAndDeletion_RetainMatchingChildNames()
    {
        using var scratch = ObservationFixtures.Scratch(); string root = Final(scratch);
        string old = Path.Combine(root, "before"), target = Path.Combine(root, "after");
        Directory.CreateDirectory(old);
        string name = "gcam-report-20261022-121000.html"; File.WriteAllText(Path.Combine(old, name), "fixture");
        using var watcher = new DocumentsWatcher(scratch, SurfaceList);
        Directory.Move(old, target); // Fresh destination, both directories owned by this fixture.
        Assert.True(SpinWait.SpinUntil(() => watcher.Snapshot().Events.Any(e => e.Operation == "rename" && e.Path == Path.Combine(target, name)), 3000));
        Directory.Delete(target, recursive: true); // Only this fixture's own temporary subtree.
        Assert.True(SpinWait.SpinUntil(() => watcher.Snapshot().Events.Any(e => e.Operation == "delete" && e.Path == Path.Combine(target, name)), 3000));
    }
    [Fact]
    public void JunctionUnderRoot_IsANamedBlindSpot_NotAFailedRoot()
    {
        // A real Documents folder holds hidden legacy junctions (and OneDrive placeholders): never followed, never fatal.
        using var scratch = ObservationFixtures.Scratch();
        string root = Path.Combine(scratch, "documents"), outside = Path.Combine(scratch, "outside");
        Directory.CreateDirectory(root); Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "gcam-report-20261022-122000.html"), "behind the link");
        string link = Path.Combine(root, "My Pictures");
        ObservationFixtures.Junction(link, outside);
        using var watcher = new DocumentsWatcher(root, SurfaceList);
        Assert.True(watcher.PositiveControl("gcam-report-20261022-122001.html"));
        // A junction moved in during the run is also reported, not followed (made outside first, so it arrives complete).
        string staged = Path.Combine(scratch, "staged-link");
        ObservationFixtures.Junction(staged, outside);
        Directory.Move(staged, Path.Combine(root, "My Videos"));
        Assert.True(SpinWait.SpinUntil(() => watcher.Snapshot().BlindSpots.Any(b => b.Path == Path.Combine(Final(root), "My Videos")), 3000));
        var result = watcher.Snapshot();
        Assert.True(result.CoversRoot); Assert.True(result.Passed, string.Join("; ", result.Errors));
        Assert.Contains(result.BlindSpots, b => b.Path == Path.Combine(Final(root), "My Pictures") && b.Kind == "link");
        Assert.DoesNotContain(result.Events, e => e.Path.Contains("122000", StringComparison.Ordinal)); // the target is not inventoried
    }
}
