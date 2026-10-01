using Gcam.Core;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public class MeasurementsViewModelTests
{
    private static ImagingResult ResultWithFlood(double fill)
    {
        var flood = new DetectorImage(4, 4);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                flood[x, y] = fill;
        return new ImagingResult(flood, -1.5, 1, null, 0, 0, null, 16 * fill, TimeSpan.Zero);
    }

    private static MeasurementDraft Roi(ImagePane pane) =>
        new(pane, MeasurementKind.Roi, [new Vec2(-2, -2), new Vec2(2, 2)]);

    [Fact]
    public void Add_NumbersSequentially_AndSelectsTheNewOne()
    {
        var vm = new MeasurementsViewModel();
        vm.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Distance, [new Vec2(0, 0), new Vec2(3, 4)]));
        vm.AddCommand.Execute(new MeasurementDraft(ImagePane.Reconstruction, MeasurementKind.Angle,
            [new Vec2(1, 0), new Vec2(0, 0), new Vec2(0, 1)]));

        Assert.Equal(["M1", "M2"], vm.Items.Select(m => m.Name));
        Assert.Same(vm.Items[1], vm.Selected);
        Assert.Equal(5.0.ToString("F1") + " mm", vm.Items[0].Value);
        Assert.Equal(90.0.ToString("F1") + "°", vm.Items[1].Value);
    }

    [Fact]
    public void Add_WrongNumberOfPoints_Throws()
    {
        var vm = new MeasurementsViewModel();
        Assert.Throws<ArgumentException>(() =>
            vm.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Angle, [new Vec2(0, 0), new Vec2(1, 1)])));
    }

    [Fact]
    public void Roi_HasNoValueBeforeARun_AndFollowsEachNewResult()
    {
        var vm = new MeasurementsViewModel();
        vm.AddCommand.Execute(Roi(ImagePane.Flood));
        var roi = vm.Items[0];
        Assert.Equal("—", roi.Value);

        vm.Refresh(ResultWithFlood(2));
        Assert.Equal("Σ 32", roi.Value);

        vm.Refresh(ResultWithFlood(10));
        Assert.Equal("Σ " + 160.0.ToString("N0"), roi.Value);
    }

    [Fact]
    public void Roi_OnAPaneWithoutData_StaysEmpty()
    {
        var vm = new MeasurementsViewModel();
        vm.Refresh(ResultWithFlood(2));   // flood only, no reconstruction
        vm.AddCommand.Execute(Roi(ImagePane.Reconstruction));
        Assert.Equal("—", vm.Items[0].Value);
    }

    [Fact]
    public void Roi_WithNoPixelCentreInside_HasNoValue()
    {
        var vm = new MeasurementsViewModel();
        vm.Refresh(ResultWithFlood(2));
        // Entirely outside the 4×4 image (centres at -1.5 … 1.5 mm)
        vm.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Roi, [new Vec2(10, 10), new Vec2(12, 12)]));
        Assert.Equal("—", vm.Items[0].Value);
        Assert.Contains("no pixel centres inside", vm.Items[0].Detail);
    }

    [Fact]
    public void Delete_SelectsTheNeighbour_ClearRestartsNumbering()
    {
        var vm = new MeasurementsViewModel();
        for (int i = 0; i < 3; i++) vm.AddCommand.Execute(Roi(ImagePane.Flood));
        vm.Selected = vm.Items[1];

        vm.DeleteCommand.Execute(null);
        Assert.Equal(["M1", "M3"], vm.Items.Select(m => m.Name));
        Assert.Equal("M3", vm.Selected?.Name);

        vm.ClearCommand.Execute(null);
        Assert.False(vm.HasItems);
        Assert.False(vm.DeleteCommand.CanExecute(null));
        Assert.False(vm.ClearCommand.CanExecute(null));
        vm.AddCommand.Execute(Roi(ImagePane.Flood));
        Assert.Equal("M1", vm.Items[0].Name);
    }

    [Fact]
    public void ToolHint_FollowsTheActiveTool()
    {
        var vm = new MeasurementsViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.ActiveTool = MeasureTool.Angle;
        Assert.Contains(nameof(MeasurementsViewModel.ToolHint), changed);
        Assert.Contains("vertex", vm.ToolHint);
    }
}
