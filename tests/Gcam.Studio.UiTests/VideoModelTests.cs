using System.Drawing;
using System.Text;
using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

public sealed class VideoModelTests
{
    [Fact]
    public void Timeline_KeepsGaps_AndCaptionsEscapeAndCoalesce()
    {
        Assert.Equal(3_000_000, VideoTimeline.Slot(3_999_999));
        Assert.Equal(0, VideoTimeline.Slot(-1));
        string captions = VideoTimeline.Captions([new(0, "a<&>\n-->b"), new(1, "same slot"), new(30_000_000, "late")], 40_000_000);
        Assert.StartsWith("WEBVTT\n\n", captions);
        Assert.Contains("00:00:00.000 --> 00:00:03.000", captions);
        Assert.Contains("a&lt;&amp;&gt; →b · same slot", captions);
        Assert.Contains("00:00:03.000 --> 00:00:04.000", captions);
        Assert.DoesNotContain("00:00:04.000 --> 00:00:04.000", captions);
    }
    [Fact]
    public void LetterboxAndMask_UseSameTransform_ClipAndRoundConservatively()
    {
        var target = WindowSurvey.Letterbox(200, 100, 100, 100);
        Assert.Equal(new Rectangle(0, 25, 100, 50), target);
        Assert.Equal(new Rectangle(0, 25, 6, 6), WindowSurvey.Mask(new(-20, 10, 31, 11), new(0, 10, 200, 100), target));
        Assert.Equal(Rectangle.Empty, WindowSurvey.Mask(new(300, 300, 1, 1), new(0, 0, 200, 100), target));
        Assert.Equal(new Rectangle(25, 0, 50, 100), WindowSurvey.Letterbox(100, 200, 100, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowSurvey.Letterbox(0, 1, 1, 1));
    }
    [Fact]
    public void Coverage_EveryDesktopMethodIsWrappedOrExplicitlyExcluded_AndTheoryRowsHaveDistinctIds()
    {
        Assert.Equal(new[] { typeof(ObservedRunTests), typeof(PlotViewTests) }.OrderBy(t => t.Name), VideoCoverage.Exclusions.Keys.OrderBy(t => t.Name));
        foreach (var method in VideoCoverage.DesktopMethods)
            Assert.True(VideoCoverage.Exclusions.ContainsKey(method.DeclaringType!) || VideoCoverage.Wrapped(method), method.ToString());
        Assert.Equal(4, new[] { "html", "pdf", "docx", "csv" }.Select(x => VideoEntry.CaseId("Export", "Save", x)).Distinct().Count());
        Assert.NotEqual(VideoEntry.CaseId("X", "a/b"), VideoEntry.CaseId("X", "a_b"));
        Assert.False(VideoRecorder.Enabled("0")); Assert.True(VideoRecorder.Enabled(null));
    }
    private static (string Folder, string Mp4, string Vtt, VideoEntry Entry) Fixture(string id = "case")
    {
        string folder = IsolationPathsTests.Scratch(); Directory.CreateDirectory(folder);
        string mp4 = Path.Combine(folder, "attempt.mp4"), vtt = Path.Combine(folder, "attempt.vtt");
        File.WriteAllBytes(mp4, [1, 2, 3]); File.WriteAllText(vtt, "WEBVTT\n\n");
        string hash = new('a', 64);
        return (folder, mp4, vtt, new("run", DateTimeOffset.UtcNow, new('b', 40), false, hash, hash,
            new(id + ".mp4", IsolationPaths.Hash(File.ReadAllBytes(mp4)), 3),
            new(id + ".vtt", IsolationPaths.Hash(File.ReadAllBytes(vtt)), new FileInfo(vtt).Length), 1_000_000, 64, 48, 1, 1, 0, 0, 0, []));
    }
    [Fact]
    public void Publication_ReplacesMp4ThenVttThenIndex_AndRepairsInterruptedHashes()
    {
        var f = Fixture(); string root = Path.Combine(f.Folder, "latest"); var order = new List<string>();
        VideoPublisher.Publish(root, "case", f.Entry, f.Mp4, f.Vtt, order.Add);
        Assert.Equal(new[] { "mp4", "vtt", "index" }, order);
        Assert.True(VideoPublisher.Matches(root, VideoPublisher.Read(Path.Combine(root, "index.json")).Tests["case"]));
        File.WriteAllBytes(Path.Combine(root, "case.mp4"), [99]); // interruption before index commit
        Assert.False(VideoPublisher.Matches(root, f.Entry));
        VideoPublisher.Publish(root, "case", f.Entry, f.Mp4, f.Vtt);
        Assert.True(VideoPublisher.Matches(root, VideoPublisher.Read(Path.Combine(root, "index.json")).Tests["case"]));
    }
    [Fact]
    public void InterruptedPublication_KeepsOldIndex_AndNextPublicationRemovesOtherStaleEntries()
    {
        var f = Fixture(); string root = Path.Combine(f.Folder, "latest");
        VideoPublisher.Publish(root, "case", f.Entry, f.Mp4, f.Vtt);
        byte[] previous = File.ReadAllBytes(Path.Combine(root, "index.json"));
        File.WriteAllBytes(f.Mp4, [4, 5]);
        var changed = f.Entry with { Video = f.Entry.Video with { Sha256 = IsolationPaths.Hash([4, 5]), Bytes = 2 } };
        Assert.Throws<IOException>(() => VideoPublisher.Publish(root, "case", changed, f.Mp4, f.Vtt, step => { if (step == "mp4") throw new IOException("interrupted"); }));
        Assert.Equal(previous, File.ReadAllBytes(Path.Combine(root, "index.json")));
        VideoPublisher.Publish(root, "other", changed with { Video = changed.Video with { Path = "other.mp4" }, Captions = changed.Captions with { Path = "other.vtt" } }, f.Mp4, f.Vtt);
        Assert.DoesNotContain("case", VideoPublisher.Read(Path.Combine(root, "index.json")).Tests.Keys);
    }
    [Fact]
    public void MetadataOnlyEntry_StaysReadableAsTheOldContract()
    {
        var f = Fixture(); string root = Path.Combine(f.Folder, "latest");
        VideoPublisher.Publish(root, "case", f.Entry, f.Mp4, f.Vtt);
        string json = File.ReadAllText(Path.Combine(root, "index.json"));
        Assert.DoesNotContain("\"evidence\"", json); Assert.DoesNotContain("\"contract\"", json);
        var read = VideoPublisher.Read(Path.Combine(root, "index.json")).Tests["case"];
        Assert.Null(read.Evidence); Assert.Null(read.Check); Assert.Null(read.Contract);
    }
    [Fact]
    public void Index_RejectsUnsafePathsBadHashesVersionsAndMaskBounds()
    {
        var f = Fixture(); f.Entry.Validate();
        Assert.Throws<InvalidDataException>(() => (f.Entry with { Video = f.Entry.Video with { Path = "../bad.mp4" } }).Validate());
        Assert.Throws<InvalidDataException>(() => (f.Entry with { Video = f.Entry.Video with { Sha256 = "bad" } }).Validate());
        Assert.Throws<InvalidDataException>(() => (f.Entry with { WidthPx = 63 }).Validate());
        Assert.Throws<InvalidDataException>(() => (f.Entry with { MaskRanges = [new(1, 1, ["foreign"], [[0, 0, 1, 1]])] }).Validate());
        Assert.Throws<InvalidDataException>(() => (f.Entry with { MaskRanges = [new(0, 0, ["foreign"], [[63, 0, 2, 1]])] }).Validate());
        VideoEntry.MaskRange a = new(0, 0, ["foreign-window"], [[0, 0, 4, 4]]);
        Assert.True(a.SameMask(a with { FirstFrame = 1, LastFrame = 1, RectsPx = [[0, 0, 4, 4]] })); // ranges merge by value
        Assert.False(a.SameMask(a with { RectsPx = [[0, 0, 4, 5]] }));
        Assert.False(a.SameMask(a with { Reasons = ["survey-failed"] }));
        string path = Path.Combine(f.Folder, "index.json"); File.WriteAllText(path, "{\"schema\":\"gcam.ui-videos\",\"schemaVersion\":2,\"tests\":{}}");
        Assert.Throws<InvalidDataException>(() => VideoPublisher.Read(path));
    }
}
