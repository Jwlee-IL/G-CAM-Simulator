using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public sealed class ExportReportViewModelTests
{
    [Fact]
    public void DefaultsToHtml_AndEventCsvIsAlwaysAvailable()
    {
        var vm = new ExportReportViewModel(_ => throw new InvalidOperationException(), () => { });
        Assert.Equal(ReportFormat.Html, vm.Format);
        Assert.Equal([ReportFormat.Html, ReportFormat.Pdf, ReportFormat.Word, ReportFormat.Csv], Enum.GetValues<ReportFormat>());
        vm.Format = ReportFormat.Csv;
        Assert.Equal(ReportFormat.Csv, vm.Format);
        Assert.True(vm.ExportCommand.CanExecute(null));
    }
    [Fact]
    public async Task Export_ClosesPopupThenSendsExactlyTheSelectedFormat()
    {
        foreach (var format in Enum.GetValues<ReportFormat>())
        {
            bool closed = false; ReportFormat? exported = null;
            var vm = new ExportReportViewModel(selected => { Assert.True(closed); exported = selected; return Task.CompletedTask; }, () => closed = true);
            vm.Format = format;
            await vm.ExportCommand.ExecuteAsync(null);
            Assert.Equal(format, exported);
        }
    }
    [Fact]
    public void Cancel_ClosesWithoutExporting()
    {
        bool closed = false;
        var vm = new ExportReportViewModel(_ => throw new InvalidOperationException(), () => closed = true);
        vm.CancelCommand.Execute(null);
        Assert.True(closed);
    }
}
