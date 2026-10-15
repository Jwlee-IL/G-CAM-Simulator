using System.Text.Json;
using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Tests;

public sealed class AdcJsonWriterTests(ITestOutputHelper output)
{
    [Fact]
    public void Write_CopiesRetainedIntegerColumnsAndHeader_WithoutSidecar()
    {
        var record = new MeasuredReadoutRecord(7, .25, .24, new(-1, 22, 33, 44), new(999, 999, 999, 999), 0, 0, -1, 0, 0, 1, 1);
        var report = ReportWriterTests.Report() with { AdcRecords = [record], AdcBits = 14 };
        var first = AdcJsonWriter.Write(report);
        Assert.Equal(first.Content, AdcJsonWriter.Write(report).Content);
        Assert.False(first.HasScene);
        using var json = JsonDocument.Parse(first.Content);
        var header = json.RootElement.GetProperty("header");
        Assert.Equal("gcam.studio.adc", header.GetProperty("schema").GetString());
        Assert.Equal(14, header.GetProperty("readout").GetProperty("adcBits").GetInt32());
        Assert.Equal(report.Version, header.GetProperty("generator").GetString());
        Assert.Equal(report.Scene[0].Value, header.GetProperty("sceneSummary")[0].GetProperty("Value").GetString());
        Assert.Equal(report.Acquisition[0].Value, header.GetProperty("acquisition")[0].GetProperty("Value").GetString());
        using var configuration = JsonDocument.Parse(report.ConfigurationJson);
        Assert.Equal(configuration.RootElement.GetProperty("Seed").ValueKind, header.GetProperty("configuration").GetProperty("Seed").ValueKind);
        var values = json.RootElement.GetProperty("events")[0];
        Assert.Equal(new[] { -1, 22, 33, 44 }, Enumerable.Range(3, 4).Select(i => values[i].GetInt32()));
        Assert.Equal(7, values[0].GetInt32()); Assert.Equal(.25, values[1].GetDouble());
        Assert.DoesNotContain("999", System.Text.Encoding.UTF8.GetString(first.Content));
    }
    [Fact]
    public void Write_RefusesMissingOrFractionalCodes_InsteadOfRecomputingOrRounding()
    {
        Assert.Throws<InvalidOperationException>(() => AdcJsonWriter.Write(ReportWriterTests.Report()));
        var record = new MeasuredReadoutRecord(0, 0, 0, new(.5, 2, 3, 4), default, 0, 0, 0, 0, 0, 1, 1);
        Assert.Throws<InvalidOperationException>(() => AdcJsonWriter.Write(ReportWriterTests.Report() with { AdcRecords = [record] }));
    }
    [Fact]
    public void Write_TenThousandConversions_ReportsCompactSize()
    {
        var records = Enumerable.Range(0, 10000).Select(i => new MeasuredReadoutRecord(i, i * .01, i * .01,
            new(100, 200, 300, 400), default, 0, 0, -1, 0, 0, 1, 1)).ToArray();
        var report = ReportWriterTests.Report() with { AdcRecords = records, AdcBits = 14 };
        var files = AdcJsonWriter.Write(report);
        using var json = JsonDocument.Parse(files.Content);
        Assert.Equal(records.Length, json.RootElement.GetProperty("events").GetArrayLength());
        Assert.True(files.Content.Length < 1000000); // one million bytes is a generous serialization budget, not a physics bound
        output.WriteLine($"10,000 retained conversions: {files.Content.Length} JSON bytes including the small fixture configuration.");
    }
}
