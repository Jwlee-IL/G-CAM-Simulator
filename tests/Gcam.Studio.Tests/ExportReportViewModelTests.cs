using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public sealed class ExportReportViewModelTests
{
    [Fact]
    public void DefaultsToHtml_AndRefusesJsonWithoutRetainedCodes()
    {
        var vm = new ExportReportViewModel(false, _ => throw new InvalidOperationException(), () => { });
        Assert.Equal(ReportFormat.Html, vm.Format);
        Assert.False(vm.HasAdc);
        Assert.Contains("experimental physical readout", vm.JsonReason);
        vm.Format = ReportFormat.Json;
        Assert.Equal(ReportFormat.Html, vm.Format);
    }
    [Fact]
    public async Task Export_ClosesPopupThenSendsExactlyTheSelectedFormat()
    {
        foreach (var format in Enum.GetValues<ReportFormat>())
        {
            bool closed = false; ReportFormat? exported = null;
            var vm = new ExportReportViewModel(true, selected => { Assert.True(closed); exported = selected; return Task.CompletedTask; }, () => closed = true);
            vm.Format = format;
            await vm.ExportCommand.ExecuteAsync(null);
            Assert.Equal(format, exported);
        }
    }
    [Fact]
    public void Cancel_ClosesWithoutExporting()
    {
        bool closed = false;
        var vm = new ExportReportViewModel(true, _ => throw new InvalidOperationException(), () => closed = true);
        vm.CancelCommand.Execute(null);
        Assert.True(closed);
    }
}
