using System.Text.Json;
using System.Windows;
using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Isolation;
using Gcam.Studio.UiTests.Harness.Observation;

namespace Gcam.Studio.UiTests;

/// <summary>The sole post-refusal input exception uses synthetic raw input, never a desktop window.</summary>
public sealed class PointerReleaseTests
{
    private static readonly OwnedProcessIdentity Owned = new(123, DateTimeOffset.Parse("2026-10-22T00:00:00Z"));
    private sealed class Raw : Pointer.IRawInput
    {
        public readonly List<string> Events = [];
        public Pointer.ReleaseState State = new(Owned, true);
        public Action AfterDown = () => { };
        public void Check(Point p) { }
        public void SetCursor(int x, int y) => Events.Add("move");
        public void Button(uint flags) { Events.Add("button-" + flags); if (flags == 2) AfterDown(); }
        public Pointer.ReleaseState ReadReleaseState(IntPtr mainWindow) => State;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GuardRefusesAfterOurDown_OnlyOneMatchingUpIsSent_OriginalFailureAndBothRecordsRemain(bool drag)
    {
        var raw = new Raw(); bool failed = false;
        var original = new ProductLivenessException("synthetic failure");
        raw.AfterDown = () => failed = true;
        var record = new RunRecord("release-fixture");
        Assert.StartsWith("gcam-", record.RunId);
        var manifest = new UiRunManifest(record.RunId, "release-fixture");
        try
        {
            using var scope = PointerBoundary.Enter(null!, new(new(1, "draft", [])),
                () => { if (failed) throw original; }, Owned, new IntPtr(42), outcome => record.RecordPointerRelease(manifest, outcome));
            var error = Assert.Throws<ProductLivenessException>(() =>
            {
                if (drag) Pointer.Drag(new Point(1, 1), new Point(10, 1), 3, raw);
                else Pointer.Click(new Point(1, 1), raw);
            });
            Assert.Same(original, error);
            Assert.Equal(new[] { "move", "button-2", "button-4" }, raw.Events);
            manifest.ProductIssues.Add(new("not_responding", "synthetic failure"));
            manifest.CleanupCompleted([]);
            manifest.Save(record.Directory, manifest.Finalise("failed"));
            using var run = JsonDocument.Parse(File.ReadAllBytes(record.Save("failed")));
            using var ui = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(record.Directory, "ui-manifest.json")));
            var release = run.RootElement.GetProperty("pointerRelease");
            Assert.True(release.GetProperty("sent").GetBoolean());
            Assert.Equal(JsonSerializer.Serialize(release), JsonSerializer.Serialize(ui.RootElement.GetProperty("evidence").GetProperty("pointerRelease")));
            Assert.Equal("failed", ui.RootElement.GetProperty("productStatus").GetString());
            Assert.Empty(ui.RootElement.GetProperty("runIssues").EnumerateArray());
        }
        finally { Directory.Delete(record.Directory, recursive: true); }
    }

    [Theory]
    [InlineData("no-down")]
    [InlineData("already-up")]
    [InlineData("other-pid")]
    [InlineData("reused-pid")]
    [InlineData("outside")]
    [InlineData("no-scope-identity")]
    public void AnyMissingReleaseCondition_SendsNothing_AndRecordsTheResidualRiskInBothPlaces(string defect)
    {
        var raw = new Raw();
        if (defect == "other-pid") raw.State = new(Owned with { Pid = 999 }, true);
        if (defect == "reused-pid") raw.State = new(Owned with { StartedAtUtc = Owned.StartedAtUtc.AddSeconds(1) }, true);
        if (defect == "outside") raw.State = new(Owned, false);
        var record = new RunRecord("withheld-release-fixture");
        var manifest = new UiRunManifest(record.RunId, "withheld-release-fixture");
        try
        {
            using var scope = PointerBoundary.Enter(null!, new(new(1, "draft", [])), () => throw new InvalidOperationException("refused"),
                defect == "no-scope-identity" ? null : Owned, new IntPtr(42), outcome => record.RecordPointerRelease(manifest, outcome));
            Assert.Throws<InvalidOperationException>(PointerBoundary.RequireInputSafe);
            var release = Pointer.ReleaseAfterRefusal(defect != "no-down", defect == "already-up", raw);
            Assert.False(release.Sent); Assert.Empty(raw.Events);
            Assert.Equal(defect is not ("no-down" or "already-up"), release.LeftButtonMayStillBePressed);
            if (release.LeftButtonMayStillBePressed) Assert.Contains("physical click", release.ResidualRisk);
            manifest.ProductIssues.Add(new("not_responding", "synthetic failure"));
            manifest.CleanupCompleted([]);
            manifest.Save(record.Directory, manifest.Finalise("failed"));
            using var ui = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(record.Directory, "ui-manifest.json")));
            using var saved = JsonDocument.Parse(File.ReadAllBytes(record.Save("failed")));
            var field = saved.RootElement.GetProperty("pointerRelease");
            var evidence = ui.RootElement.GetProperty("evidence").GetProperty("pointerRelease");
            Assert.Equal(JsonSerializer.Serialize(field), JsonSerializer.Serialize(evidence));
            Assert.Equal("completed", ui.RootElement.GetProperty("runStatus").GetString());
            Assert.Empty(manifest.RunIssues);
        }
        finally { Directory.Delete(record.Directory, recursive: true); }
    }
}
