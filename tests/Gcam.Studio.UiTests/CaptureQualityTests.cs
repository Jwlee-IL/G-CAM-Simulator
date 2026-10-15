using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

public sealed class CaptureQualityTests
{
    [Fact]
    public void BadFrames_RejectEachReason_WithoutDesktopAccess()
    {
        var template = new CaptureQuality.Template("fixture", "ready", 2, 1, 96, "dark", "en", ["Known"], "owned-frame", "1");
        var good = new CaptureQuality.Frame(2, 1, true, true, false, true, true, [0, 0, 0, 255, 255, 255]);
        Assert.Empty(CaptureQuality.Check(template, good));
        Assert.Contains("occluded", CaptureQuality.Check(template, good with { Occluded = true }));
        Assert.Contains("not-foreground", CaptureQuality.Check(template, good with { Foreground = false }));
        Assert.Contains("wrong-size", CaptureQuality.Check(template, good with { WidthPx = 1 }));
        Assert.Contains("blank", CaptureQuality.Check(template, good with { Rgb = [0, 0, 0, 0, 0, 0] }));
        Assert.Contains("not-owned", CaptureQuality.Check(template, good with { Owned = false }));
        Assert.Contains("privacy-unknown", CaptureQuality.Check(template, good with { PrivacySafe = false }));
        Assert.Contains("unstable", CaptureQuality.Check(template, good with { Stable = false }));
    }
    [Fact]
    public void TemplateFingerprint_ChangesWithDefinition_AndRejectsMalformedPixels()
    {
        var template = new CaptureQuality.Template("fixture", "ready", 2, 1, 96, "dark", "en", [], "frame", "1");
        Assert.Equal(template.Sha256, template.Sha256);
        Assert.NotEqual(template.Sha256, (template with { Theme = "light" }).Sha256);
        Assert.NotEqual(template.Sha256, (template with { Dpi = 144 }).Sha256);
        Assert.Contains("invalid-pixels", CaptureQuality.Check(template, new(2, 1, true, true, false, true, true, [])));
    }
}
