using System.Globalization;
using System.Text;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Tests;

public sealed class EventCsvWriterTests(ITestOutputHelper output)
{
    private static readonly string[] DirectHeader = ["index", "arrival_time_s", "pixel_x", "pixel_y", "deposit_keV", "measured_keV"];
    private static readonly string[] PhysicalHeader = ["index", "hold_time_s", "trigger_time_s", "A", "B", "C", "D", "raw_x", "raw_y", "crystal", "energy_keV"];

    [Theory]
    [InlineData("de-DE")]
    [InlineData("ko-KR")]
    public void Direct_RowsEqualRetainedEventsAndMeasuredEnergies_InInvariantCulture(string culture)
    {
        var report = Direct();
        byte[] invariant = WithCulture(CultureInfo.InvariantCulture, () => EventCsvWriter.Write(report, "events.csv").Content);
        byte[] local = WithCulture(CultureInfo.GetCultureInfo(culture), () => EventCsvWriter.Write(report, "events.csv").Content);
        Assert.Equal(invariant, local);
        var rows = Parse(local);
        Assert.Equal(DirectHeader, rows[0]);
        Assert.Equal(report.Events.Count + 1, rows.Count);
        for (int i = 0; i < report.Events.Count; i++)
        {
            var e = report.Events[i];
            var row = rows[i + 1];
            Assert.Equal(DirectHeader.Length, row.Length);
            Assert.Equal(i, int.Parse(row[0], CultureInfo.InvariantCulture));
            Assert.Equal(e.ArrivalTimeS, double.Parse(row[1], CultureInfo.InvariantCulture));
            Assert.Equal(e.PixelX, int.Parse(row[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
            Assert.Equal(e.PixelY, int.Parse(row[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
            Assert.Equal(e.DepositKeV, double.Parse(row[4], CultureInfo.InvariantCulture));
            double measured = report.MeasuredKeV![i];
            if (double.IsNaN(measured)) Assert.Equal("", row[5]);
            else Assert.Equal(measured, double.Parse(row[5], CultureInfo.InvariantCulture));
        }
        output.WriteLine(Encoding.UTF8.GetString(local).Replace("\r\n", "⏎\n"));
    }

    [Fact]
    public void Direct_WithoutMeasuredEnergies_LeavesTheColumnEmpty_AndRefusesForeignOnes()
    {
        var report = Direct() with { MeasuredKeV = null };
        var rows = Parse(EventCsvWriter.Write(report, "e.csv").Content);
        Assert.All(rows.Skip(1), row => Assert.Equal("", row[5]));
        Assert.Throws<InvalidOperationException>(() => EventCsvWriter.Write(Direct() with { MeasuredKeV = [1.0] }, "e.csv"));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("ko-KR")]
    public void Physical_RowsCopyIntegerCodesAndAssignments_UnassignedEnergyEmpty(string culture)
    {
        var report = Physical();
        byte[] csv = WithCulture(CultureInfo.GetCultureInfo(culture), () => EventCsvWriter.Write(report, "events.csv").Content);
        var rows = Parse(csv);
        Assert.Equal(PhysicalHeader, rows[0]);
        Assert.Equal(report.ReadoutRecords.Count + 1, rows.Count);
        for (int i = 0; i < report.ReadoutRecords.Count; i++)
        {
            var r = report.ReadoutRecords[i];
            var row = rows[i + 1];
            Assert.Equal(PhysicalHeader.Length, row.Length);
            Assert.Equal(r.Index, int.Parse(row[0], CultureInfo.InvariantCulture));
            Assert.Equal(r.HoldTimeS, double.Parse(row[1], CultureInfo.InvariantCulture));
            Assert.Equal(r.TriggerTimeS, double.Parse(row[2], CultureInfo.InvariantCulture));
            for (int c = 0; c < 4; c++)
            {
                Assert.DoesNotContain('.', row[3 + c]);
                Assert.Equal(r.Codes.Channel(c), int.Parse(row[3 + c], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
            }
            AssertCell(r.RawX, row[7]);
            AssertCell(r.RawY, row[8]);
            Assert.Equal(r.Crystal, int.Parse(row[9], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
            if (r.Crystal < 0) Assert.Equal("", row[10]);
            else Assert.Equal(r.EnergyKeV, double.Parse(row[10], CultureInfo.InvariantCulture));
        }
        Assert.DoesNotContain("4242", Encoding.UTF8.GetString(csv)); // analog values at hold are not exported
    }

    [Fact]
    public void Physical_RefusesFractionalCodes_InsteadOfRounding()
    {
        var record = new MeasuredReadoutRecord(0, 0, 0, new(.5, 2, 3, 4), default, 0, 0, 0, 600, 0, 1, 1);
        Assert.Throws<InvalidOperationException>(() => EventCsvWriter.Write(Physical() with { ReadoutRecords = [record] }, "e.csv"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Meta_NamesReadoutColumnsFilesAndConfiguration(bool physical)
    {
        var report = physical ? Physical() : Direct();
        var first = EventCsvWriter.Write(report, "run-7.csv");
        var second = EventCsvWriter.Write(report, "run-7.csv");
        Assert.Equal(first.Content, second.Content);
        Assert.Equal(first.MetaJson, second.MetaJson);
        Assert.Equal(Encoding.UTF8.GetBytes(report.ConfigurationJson), first.SceneJson);
        using var meta = JsonDocument.Parse(first.MetaJson!);
        var root = meta.RootElement;
        Assert.Equal("gcam.studio.events", root.GetProperty("schema").GetString());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Contains(report.Version, root.GetProperty("generator").GetString());
        Assert.Equal("2026-10-14T12:00:00.0000000+09:00", root.GetProperty("exportTimeLocal").GetString());
        Assert.Equal("2026-10-14T03:00:00.0000000Z", root.GetProperty("exportTimeUtc").GetString());
        Assert.Equal("Simulation — not a measurement", root.GetProperty("notice").GetString());
        var files = root.GetProperty("files");
        Assert.Equal("run-7.csv", files.GetProperty("events").GetString());
        Assert.Equal("run-7.scene.json", files.GetProperty("scene").GetString());
        Assert.Equal("run-7.meta.json", files.GetProperty("meta").GetString());
        var readout = root.GetProperty("readout");
        if (physical)
        {
            Assert.Equal("FourOutputAnger", readout.GetProperty("mode").GetString());
            Assert.Contains("four outputs, A–D", readout.GetProperty("description").GetString());
            Assert.Contains("14-bit ADC codes", readout.GetProperty("description").GetString());
            Assert.Contains("661.7 keV line", readout.GetProperty("description").GetString());
            Assert.Equal(14, readout.GetProperty("adcBits").GetInt32());
            Assert.Equal(661.7, readout.GetProperty("referenceLineKeV").GetDouble());
        }
        else
        {
            Assert.Equal("DirectCrystal", readout.GetProperty("mode").GetString());
            Assert.Equal("Largest-deposit crystal assignment from the transport; this readout has no light, network or ADC stage, so there are no ADC codes.",
                readout.GetProperty("description").GetString());
            Assert.True(readout.GetProperty("measuredEnergyAvailable").GetBoolean());
            Assert.True(readout.GetProperty("spectrumPileUp").GetBoolean());
        }
        Assert.Equal("CRLF", root.GetProperty("csv").GetProperty("lineEnding").GetString());
        var columns = root.GetProperty("columns").EnumerateArray().ToArray();
        Assert.Equal(physical ? PhysicalHeader : DirectHeader, columns.Select(c => c.GetProperty("name").GetString()));
        Assert.All(columns, c => Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("meaning").GetString())));
        Assert.Equal("keV", columns.Single(c => c.GetProperty("name").GetString() == (physical ? "energy_keV" : "deposit_keV")).GetProperty("unit").GetString());
        Assert.Equal(physical ? report.ReadoutRecords.Count : report.Events.Count, root.GetProperty("eventCount").GetInt32());
        Assert.Equal(report.Scene[0].Value, root.GetProperty("scene")[0].GetProperty("value").GetString());
        Assert.Equal(report.Acquisition[0].Value, root.GetProperty("acquisition")[0].GetProperty("value").GetString());
        Assert.Equal(73, root.GetProperty("configuration").GetProperty("Seed").GetInt32());
        Assert.Equal(Parse(first.Content)[0], columns.Select(c => c.GetProperty("name").GetString()));
        output.WriteLine($"{(physical ? "physical" : "direct")} meta: {first.MetaJson!.Length} bytes");
    }

    [Fact]
    public void EmptyAcquisition_WritesHeaderOnly()
    {
        var files = EventCsvWriter.Write(Direct() with { Events = [], MeasuredKeV = [] }, "e.csv");
        Assert.Equal("index,arrival_time_s,pixel_x,pixel_y,deposit_keV,measured_keV\r\n", Encoding.UTF8.GetString(files.Content));
        using var meta = JsonDocument.Parse(files.MetaJson!);
        Assert.Equal(0, meta.RootElement.GetProperty("eventCount").GetInt32());
    }

    private static void AssertCell(double expected, string cell)
    {
        if (double.IsFinite(expected)) Assert.Equal(expected, double.Parse(cell, CultureInfo.InvariantCulture));
        else Assert.Equal("", cell);
    }

    /// <summary>Strict CRLF rows of comma-separated cells: no BOM, no bare LF, a final line break.</summary>
    internal static List<string[]> Parse(byte[] bytes)
    {
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "CSV starts with a byte-order mark.");
        string text = Encoding.UTF8.GetString(bytes);
        Assert.EndsWith("\r\n", text);
        Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
        Assert.DoesNotContain("\r", text.Replace("\r\n", ""));
        return text[..^2].Split("\r\n").Select(line => line.Split(',')).ToList();
    }

    private static T WithCulture<T>(CultureInfo culture, Func<T> action)
    {
        var (current, ui) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
            return action();
        }
        finally { (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (current, ui); }
    }

    private static SimulationReport Direct() => ReportWriterTests.Report() with
    {
        ConfigurationJson = ConfigLoader.Serialize(new SimulationConfig { Seed = 73 }),
        Events = [new(0, 11, 661.7, 1e-7 + 2e-23), new(5, 3, 0.1 + 0.2, 0.25), new(11, 0, 1234567.891, 1.5), new(2, 2, 0, 3.0000000000000004)],
        MeasuredKeV = [655.123456789, double.NaN, 1234560.5, 1e-300],
        MeasuredPileUp = true
    };

    private static SimulationReport Physical()
    {
        var config = new SimulationConfig { Seed = 73 };
        config.Detector.Readout = new() { Mode = ReadoutMode.FourOutputAnger };
        return ReportWriterTests.Report() with
        {
            ConfigurationJson = ConfigLoader.Serialize(config), PhysicalReadout = true, AdcBits = 14, ReferenceLineKeV = 661.7,
            ReadoutRecords =
            [
                new(0, 2.5e-6, 2.4e-6, new(-1, 22, 33, 8191), new(4242.5, 4242.5, 4242.5, 4242.5), -0.125, 0.3, 17, 661.70000000000005, 0, 1, 1),
                new(1, 0.25, 0.2499999, new(-8192, 0, 1, 2), new(4242.5, 4242.5, 4242.5, 4242.5), double.NaN, double.NaN, -1, double.NaN, 1, 2, .5),
                new(2, 1.0, 0.9999999, new(100, 200, 300, 400), default, 0.999, -0.999, 143, 32.1, 2, 1, 1)
            ]
        };
    }
}
