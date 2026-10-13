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
        var prefill = editor.Effective;
        Assert.False(editor.IsCustom);
        editor.SelectedPreset = OpticsPreset.All[0];
        Assert.True(editor.IsCustom);
        Assert.Equal(prefill, editor.Effective);
        editor.CellPitch = "0.51";
        Assert.Equal("Custom", editor.SelectedPreset.Name);
        Assert.Equal(0.51, editor.Effective.CellPitchMm);
        editor.CellPitch = "0.5";
        Assert.Equal("Custom", editor.SelectedPreset.Name); // matching High-res must not hide fields mid-edit
        Assert.True(editor.IsCustom);
    }

    [Fact]
    public void CustomMode_ChangesOnlyThroughSelector_PresetFieldsCannotBeEdited()
    {
        var editor = new OpticsEditorViewModel();
        var before = editor.Effective;
        editor.CellPitch = "0.51";
        Assert.Equal(before, editor.Effective);
        Assert.Equal("Sharp", editor.SelectedPreset.Name);
        Assert.False(editor.IsCustom);
        editor.Load(before with { FocalDistanceMm = 800 });
        Assert.False(editor.IsCustom);
        editor.SelectedPresetIndex = 0;
        editor.Load(before);
        Assert.True(editor.IsCustom);
    }

    /// <summary>Author review 2 (TODO-45): the numeric fields are always shown. A preset writes its values into them and
    /// disables them; Custom enables them, starting from the current values; the shell's input lock disables them in every
    /// mode.</summary>
    [Fact]
    public void PresetWritesItsValuesAndDisablesFields_CustomEnablesThemFromCurrentValues()
    {
        var editor = new OpticsEditorViewModel();
        // xUnit's synchronization context runs the editor's posted selector refresh concurrently: count atomically.
        int notified = 0;
        editor.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(OpticsEditorViewModel.CanEditFields)) Interlocked.Increment(ref notified); };
        foreach (var preset in OpticsPreset.All.Skip(1))
        {
            editor.SelectedPreset = preset;
            var settings = preset.Settings!;
            Assert.False(editor.CanEditFields);
            Assert.Equal((settings.MuraRank, settings.CellPitchMm, settings.MaskDetectorDistanceMm, settings.DetectorPixels, settings.PixelPitchMm),
                (editor.Rank, double.Parse(editor.CellPitch), double.Parse(editor.Distance), int.Parse(editor.Pixels), double.Parse(editor.PixelPitch)));
        }
        var current = (editor.Rank, editor.CellPitch, editor.Distance, editor.Pixels, editor.PixelPitch);
        int before = Volatile.Read(ref notified);
        editor.SelectedPreset = OpticsPreset.All[0];
        Assert.True(Volatile.Read(ref notified) > before);
        Assert.True(editor.CanEditFields);
        Assert.Equal(current, (editor.Rank, editor.CellPitch, editor.Distance, editor.Pixels, editor.PixelPitch));
        before = Volatile.Read(ref notified);
        editor.IsEditable = false;
        Assert.True(Volatile.Read(ref notified) > before);
        Assert.False(editor.CanEditFields);
        editor.IsEditable = true;
        Assert.True(editor.CanEditFields);
        editor.SelectedPreset = OpticsPreset.All[1];
        Assert.False(editor.CanEditFields);
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
        var editor = new OpticsEditorViewModel { SelectedPreset = OpticsPreset.All[0] };
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
        var editor = new OpticsEditorViewModel { SelectedPreset = OpticsPreset.All[0], Pixels = text };
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
