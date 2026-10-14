using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public sealed class ReportExportCommandTests
{
    [Fact]
    public void ExportReport_EnabledOnlyWithSnapshot_AndNotifies()
    {
        var vm = Create(new FakeExport());
        int changes = 0;
        vm.ExportReportCommand.CanExecuteChanged += (_, _) => changes++;
        Assert.False(vm.ExportReportCommand.CanExecute(null));
        vm.Snapshot = Snapshot();
        Assert.True(vm.ExportReportCommand.CanExecute(null));
        vm.Snapshot = null;
        Assert.False(vm.ExportReportCommand.CanExecute(null));
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task ExportReport_CancelledDialogWritesNothing()
    {
        var export = new FakeExport();
        var vm = Create(export); vm.Snapshot = Snapshot();
        await vm.ExportReportCommand.ExecuteAsync(null);
        Assert.Equal("gcam-report-20261014-120000.html", export.DefaultName);
        Assert.Equal(0, export.Writes);
        Assert.Null(export.Error);
    }

    [Fact]
    public async Task ExportReport_FailedWriteReportsFailure_WithoutChangingAcquisition()
    {
        var export = new FakeExport { Path = "report.html", Fail = true };
        var vm = Create(export); vm.Snapshot = Snapshot();
        var snapshot = vm.Snapshot;
        await vm.ExportReportCommand.ExecuteAsync(null);
        Assert.Contains("disk failure", export.Error);
        Assert.False(export.FileExists);
        Assert.Same(snapshot, vm.Snapshot);
        Assert.Equal(RunState.Empty, vm.State);
    }

    [Fact]
    public async Task ExportReport_CapturesBeforeDialogPumpsNewSnapshots()
    {
        var export = new FakeExport { Path = "report.html" };
        var vm = Create(export); vm.Snapshot = Snapshot(); vm.Result = vm.Snapshot.Imaging;
        vm.Imaging.Measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Distance,
            [new(0, 0), new(3, 4)]));
        export.OnDialog = () => { vm.Snapshot = Snapshot() with { Counts = 999 }; vm.Imaging.Measurements.Items.Clear(); };
        await vm.ExportReportCommand.ExecuteAsync(null);
        string html = System.Text.Encoding.UTF8.GetString(export.Files!.Html);
        Assert.Contains("<td>10</td>", html);
        Assert.DoesNotContain("999", html);
        Assert.Contains("5.0 mm", html);
        Assert.Equal(ConfigLoader.Serialize(new SimulationConfig { Seed = 73 }), System.Text.Encoding.UTF8.GetString(export.Files.SceneJson));
    }

    private static MainViewModel Create(FakeExport export) => new(new FakeAcquisition(), new FakeTheme(), new FakeSpectrumService(),
        reportExport: export, reportClock: new FixedClock());

    private static AcquisitionSnapshot Snapshot() => new(1, 10, 10, 1, false,
        new(new DetectorImage(2, 2), -1, 2, null, 0, 0, null, 10, TimeSpan.Zero), [], TimeSpan.Zero, true)
        { ConfigurationJson = ConfigLoader.Serialize(new SimulationConfig { Seed = 73 }), Seed = 73, Optics = new(), Detector = new() };

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 14, 3, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("ReportTest", TimeSpan.FromHours(9), "ReportTest", "ReportTest");
    }
    private sealed class FakeExport : IReportExportService
    {
        public string Version => "1.0.0+test";
        public string? Path { get; init; }
        public bool Fail { get; init; }
        public Action? OnDialog { get; set; }
        public string? DefaultName { get; private set; }
        public int Writes { get; private set; }
        public bool FileExists { get; private set; }
        public string? Error { get; private set; }
        public ReportFiles? Files { get; private set; }
        public string? ChoosePath(string defaultName) { DefaultName = defaultName; OnDialog?.Invoke(); return Path; }
        public Task WriteAsync(string path, ReportFiles files)
        {
            Writes++; Files = files;
            if (Fail) throw new IOException("disk failure");
            FileExists = true; return Task.CompletedTask;
        }
        public void ShowError(string message) => Error = message;
    }
    private sealed class FakeAcquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS,
            double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null) => throw new NotSupportedException();
    }
    private sealed class FakeTheme : IThemeService
    {
        public AppTheme Current => AppTheme.Dark;
        public void Apply(AppTheme theme) { }
    }
}
