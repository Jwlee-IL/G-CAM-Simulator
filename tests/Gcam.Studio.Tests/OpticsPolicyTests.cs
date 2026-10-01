using Gcam.Configuration;
using Gcam.Studio.Core.Optics;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public sealed class OpticsPolicyTests
{
    [Fact]
    public void Presets_ApplyAtomically_MatchPhysicalFieldsAndShowCustom()
    {
        var editor = new OpticsEditorViewModel();
        int changes = 0;
        editor.Changed += (_, _) => changes++;
        foreach (var preset in OpticsPreset.All.Skip(1))
        {
            int before = changes;
            editor.SelectedPreset = preset;
            Assert.Equal(before + (editor.Effective == preset.Settings && preset.Name == "Sharp" ? 0 : 1), changes);
            Assert.Equal(preset.Settings, editor.Effective);
            Assert.Same(preset, OpticsPreset.Match(editor.Effective with { FocalDistanceMm = 800 }));
            Assert.Null(OpticsPolicy.Validate(editor.Effective));
        }
        editor.CellPitch = "0.51";
        Assert.Equal("Custom", editor.SelectedPreset.Name);
        Assert.Equal(0.51, editor.Effective.CellPitchMm);
    }

    [Theory]
    [InlineData(1000, 56.875, 1.268115942, 1.81978022)]
    [InlineData(160, 9.1, 2.333333333, 0.989010989)]
    public void Geometry_UsesEffectiveFieldsAndHasNoPerformanceBand(double focal, double half, double samples, double coverage)
    {
        var optics = new OpticsSettings();
        var geometry = OpticsGeometry.Calculate(optics, focal);
        Assert.Equal(half, geometry.NominalHalfFieldMm, 6);
        Assert.Equal(samples, geometry.SamplesPerCell, 6);
        Assert.Equal(coverage, geometry.CoveragePeriods, 6);
        Assert.Equal(18.2, geometry.MaskWidthMm, 6);
        Assert.Equal(optics.MuraRank, 2 * half / geometry.ResolutionElementMm, 6);
        var config = SceneConfigBuilder.Build([new SceneSource()], optics with { FocalDistanceMm = focal }, 1);
        Assert.Equal(0.95 * half, config.Decoder.ReconHalfExtentMm!.Value, 6);
        Assert.DoesNotContain("✓", geometry.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("0.01")]
    public void InvalidText_IsRetainedAndCannotBecomeEffective(string text)
    {
        var editor = new OpticsEditorViewModel();
        var before = editor.Effective;
        editor.CellPitch = text;
        Assert.NotNull(editor.Error);
        Assert.Equal(text, editor.CellPitch);
        Assert.Equal(before, editor.Effective);
        Assert.Equal("Custom", editor.SelectedPreset.Name);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("65")]
    [InlineData("30.5")]
    public void PixelCount_RequiresSupportedInteger(string text)
    {
        var editor = new OpticsEditorViewModel { Pixels = text };
        Assert.NotNull(editor.Error);
        Assert.Equal(30, editor.Effective.DetectorPixels);
    }

    [Fact]
    public void Validation_ChecksRankGapSourceFaceFocusAndOverflow()
    {
        var optics = new OpticsSettings();
        Assert.NotNull(OpticsPolicy.Validate(optics with { MuraRank = 2 }));
        Assert.NotNull(OpticsPolicy.Validate(optics with { MuraRank = 12 }));
        Assert.NotNull(OpticsPolicy.Validate(optics with { PixelPitchMm = 0.1 }));
        Assert.NotNull(OpticsPolicy.Validate(optics with { MaskDetectorDistanceMm = double.NaN }));
        Assert.NotNull(OpticsPolicy.ValidateScene(optics, [new SceneSource { DistanceMm = 85 }]));
        Assert.Null(OpticsPolicy.ValidateScene(optics, [new SceneSource { DistanceMm = 86 }]));
        Assert.NotNull(OpticsPolicy.ValidateFocus(optics, 80));
        Assert.Null(OpticsPolicy.ValidateFocus(optics, 80.5));
        Assert.NotNull(OpticsPolicy.ValidateFocus(optics with { CellPitchMm = double.MaxValue }, double.MaxValue));
        Assert.Equal(new[] { 5, 7, 11, 13, 17, 19, 23 }, OpticsPolicy.Ranks);
    }
}
