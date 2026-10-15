using System.Text;
using Gcam.Configuration;
using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services.Tests;

/// <summary>The report records the service's finalized inputs, not a fresh scene-builder approximation.</summary>
public sealed class AcquisitionReportTests
{
    [Fact]
    public async Task Snapshot_RecordsFinalDetectorBackgroundAndSeed_OncePerAcquisition()
    {
        var scene = new[] { new SceneSource { X = 15, ActivityUCi = 500 } };
        var optics = new OpticsSettings();
        var detector = new DetectorSettings { GainSigma = .07, GainSeed = 19, ReflectorGapMm = .03 };
        var expected = SimulationService.BuildConfig(scene, optics, detector, .2);
        expected.Seed = 73;
        await using var session = new SimulationService().Start(scene, optics, .05, 10, detector, .2, 73);
        string? first = null;
        await foreach (var snapshot in session.ReadSnapshotsAsync())
        {
            Assert.Equal(ConfigLoader.Serialize(expected), snapshot.ConfigurationJson);
            first ??= snapshot.ConfigurationJson;
            Assert.Same(first, snapshot.ConfigurationJson);
        }
        Assert.NotNull(first);
    }

    [Fact]
    public async Task Snapshot_RecordsResolvedAmbientSpectrum_WithoutExternalFileReference()
    {
        var field = AmbientPreset.TerrestrialUnscear2000V1.Field(.1, AmbientGeometry.FrontOnlyThroughMask);
        var scene = new[] { new SceneSource { ActivityUCi = 500 } };
        var expected = SimulationService.BuildConfig(scene, new(), new(), ambient: field);
        expected.Seed = 73;
        Assert.Null(expected.Ambient!.SpectrumFile);
        Assert.NotNull(expected.Ambient.Spectrum);
        await using var session = new SimulationService().StartAmbient(scene, new(), .05, 10, field, seed: 73);
        await foreach (var snapshot in session.ReadSnapshotsAsync())
            Assert.Equal(ConfigLoader.Serialize(expected), snapshot.ConfigurationJson);
    }
    [Fact]
    public void Report_DefaultAmbientScene_HtmlOmitsSpectrumTables_SceneFileKeepsThem()
    {
        // Studio's default scene: one source and the default terrestrial field, resolved as an acquisition resolves it.
        var defaults = StudioOptionDefaults.Current;
        var config = SimulationService.BuildConfig([new SceneSource()], new(), new(), defaults.BackgroundToSignalRatio,
            AmbientPreset.TerrestrialUnscear2000V1.Field(defaults.AmbientDoseMicroSvPerHour, defaults.AmbientGeometry));
        var spectrum = config.Ambient!.Spectrum;
        string json = ConfigLoader.Serialize(config);
        var report = new SimulationReport(new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero), "test", json, false,
            [], [], null, [], [], null, false, [], []);
        var files = ReportWriter.Write(report);
        Assert.Equal(files.Html, ReportWriter.Write(report).Html);

        string html = Encoding.UTF8.GetString(files.Html);
        Assert.True(files.Html.Length < 1_000_000, $"HTML is {files.Html.Length:N0} bytes.");
        Assert.Contains($"&quot;Id&quot;: &quot;{AmbientPreset.TerrestrialUnscear2000V1.Id}&quot;", html);
        Assert.Contains($"&quot;ContentHash&quot;: &quot;{spectrum.ContentHash}&quot;", html);
        Assert.Contains("The ambient spectrum tables (Lines, Continuum, EnergyZenith, NotIncluded)", html);
        foreach (var (table, count) in new[] { ("Lines", spectrum.Lines.Length), ("Continuum", spectrum.Continuum.Length),
                     ("EnergyZenith", spectrum.EnergyZenith!.Length), ("NotIncluded", spectrum.NotIncluded!.Length) })
            Assert.Contains($"&quot;{table}&quot;: &quot;omitted from this report: {count} entries; the full table is in the scene file&quot;", html);
        Assert.Contains("&quot;DoseRateMicroSvPerHour&quot;: 0.1", html);

        // The scene file is the record: the full configuration, loadable and unchanged by the round trip.
        Assert.Equal(Encoding.UTF8.GetBytes(json), files.SceneJson);
        string folder = Path.Combine(Path.GetTempPath(), "gcam-todo46", "impl6", "scene-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "report.scene.json");
        File.WriteAllBytes(path, files.SceneJson);
        File.WriteAllBytes(Path.Combine(folder, "report.html"), files.Html);
        var loaded = ConfigLoader.Load(path);
        Assert.Equal(spectrum.EnergyZenith.Length, loaded.Ambient!.Spectrum.EnergyZenith!.Length);
        Assert.Equal(spectrum.ContentHash, loaded.Ambient.Spectrum.ComputeContentHash());
        Assert.Equal(json, ConfigLoader.Serialize(loaded));
    }

    [Fact]
    public async Task EventCsv_DirectAcquisition_RowsAreTheSnapshotsEvents_MetaShowsAmbientTableCounts()
    {
        var defaults = StudioOptionDefaults.Current;
        var field = AmbientPreset.TerrestrialUnscear2000V1.Field(defaults.AmbientDoseMicroSvPerHour, defaults.AmbientGeometry);
        await using var session = new SimulationService().StartAmbient([new SceneSource()], new(), .2, 10, field, seed: 73);
        AcquisitionSnapshot? last = null;
        await foreach (var snapshot in session.ReadSnapshotsAsync()) last = snapshot;
        Assert.NotNull(last);
        Assert.NotEmpty(last.Events);
        var settings = new SpectrumSettings
        { Detector = last.Detector, PixelsX = last.Imaging.Flood.Width, PixelsY = last.Imaging.Flood.Height };
        var service = new SpectrumService();
        await service.ProcessAsync(Guid.NewGuid(), last.Events, [new("Cs-137", 661.7)], settings);
        var report = new SimulationReport(new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero), "test", last.ConfigurationJson!, false,
            [], [], null, [], [], null, false, [], [])
        { Events = last.Events, MeasuredKeV = service.MeasuredEnergies(last.Events, settings) };
        var files = EventCsvWriter.Write(report, "events.csv");

        var lines = Encoding.UTF8.GetString(files.Content).Split("\r\n");
        Assert.Equal("", lines[^1]);
        Assert.Equal(last.Events.Count + 1, lines.Length - 1);
        for (int i = 0; i < last.Events.Count; i++)
        {
            var cells = lines[i + 1].Split(',');
            var e = last.Events[i];
            Assert.Equal(e.ArrivalTimeS, double.Parse(cells[1], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal($"{e.PixelX},{e.PixelY}", $"{cells[2]},{cells[3]}");
            Assert.Equal(e.DepositKeV, double.Parse(cells[4], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(report.MeasuredKeV![i], double.Parse(cells[5], System.Globalization.CultureInfo.InvariantCulture));
        }

        using var meta = System.Text.Json.JsonDocument.Parse(files.MetaJson!);
        var spectrum = meta.RootElement.GetProperty("configuration").GetProperty("Ambient").GetProperty("Spectrum");
        using var scene = System.Text.Json.JsonDocument.Parse(last.ConfigurationJson!);
        var resolved = scene.RootElement.GetProperty("Ambient").GetProperty("Spectrum");
        Assert.Equal($"omitted from this report: {resolved.GetProperty("Continuum").GetArrayLength()} entries; the full table is in the scene file",
            spectrum.GetProperty("Continuum").GetString());
        Assert.Equal(resolved.GetProperty("ContentHash").GetString(), spectrum.GetProperty("ContentHash").GetString());
        Assert.True(files.MetaJson!.Length < 64_000, $"meta.json is {files.MetaJson.Length:N0} bytes.");
        Assert.Equal(Encoding.UTF8.GetBytes(last.ConfigurationJson!), files.SceneJson);
    }
}
