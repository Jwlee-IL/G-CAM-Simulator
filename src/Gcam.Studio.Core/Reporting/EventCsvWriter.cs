using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Core.Reporting;

/// <summary>Plain per-event data for other programs: <c>&lt;name&gt;.csv</c> with one row per retained event,
/// <c>&lt;name&gt;.meta.json</c> naming the readout that produced it and what each column means, and the scene record.
/// Every value is copied from the frozen capture; nothing is computed here.</summary>
public static class EventCsvWriter
{
    public const string Schema = "gcam.studio.events";
    public const int SchemaVersion = 1;
    public const string Notice = "Simulation — not a measurement";
    /// <summary>CRLF, the line break of RFC 4180 and the one spreadsheet programs expect.</summary>
    public const string LineEnding = "\r\n";

    public const string DirectReadout = "Largest-deposit crystal assignment from the transport; this readout has no light, network or ADC stage, so there are no ADC codes.";

    public sealed record Column(string Name, string Unit, string Meaning);

    public static IReadOnlyList<Column> DirectColumns { get; } =
    [
        new("index", "", "Event number in acquisition order, from 0; the index the spectrum's measurement is keyed by."),
        new("arrival_time_s", "s", "Arrival time in simulated acquisition time."),
        new("pixel_x", "pixel", "Detector pixel column, 0 … Nx − 1; its centre is at x = (pixel_x − (Nx − 1) / 2) × pixel pitch."),
        new("pixel_y", "pixel", "Detector pixel row, 0 … Ny − 1; its centre is at y = (pixel_y − (Ny − 1) / 2) × pixel pitch."),
        new("deposit_keV", "keV", "Energy the photon history deposited in the detector (the transport's true deposit), before detector gain and front-end resolution."),
        new("measured_keV", "keV", "The measured energy the Spectrum workspace histogrammed for this event: detector gain and front-end resolution through Studio's measurement model, with the same index-keyed random stream. Empty for an event absorbed into a pile-up group (the group's summed measurement is on its first event) or when unavailable.")
    ];

    public static IReadOnlyList<Column> PhysicalColumns(int bits) =>
    [
        new("index", "", "Conversion number in acquisition order, from 0; unassigned conversions included."),
        new("hold_time_s", "s", "Time of the common hold of the four outputs, in simulated acquisition time."),
        new("trigger_time_s", "s", "Time the sum trigger fired, in simulated acquisition time."),
        .. "ABCD".Select(c => new Column(c.ToString(), "ADC code", $"Held output {c}: signed, pedestal-subtracted {bits}-bit ADC code.")),
        new("raw_x", "", "Anger-type x position from the four outputs' relative signals, normalised (nominally −1 … 1). Empty when the conversion could not be positioned."),
        new("raw_y", "", "Anger-type y position from the four outputs' relative signals, normalised (nominally −1 … 1). Empty when the conversion could not be positioned."),
        new("crystal", "", "Crystal from the captured calibration's flood-map look-up table, index = row × Nx + column; −1 when unassigned."),
        new("energy_keV", "keV", "Energy through the reference line's per-crystal calibration, the value the Spectrum workspace histograms. Empty when crystal is −1.")
    ];

    public static ReportFiles Write(SimulationReport report, string csvFileName)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(csvFileName);
        if (!report.PhysicalReadout && report.MeasuredKeV is { } measured && measured.Count != report.Events.Count)
            throw new InvalidOperationException("The measured energies do not belong to the retained events.");
        var columns = report.PhysicalReadout ? PhysicalColumns(report.AdcBits) : DirectColumns;
        byte[] csv = report.PhysicalReadout ? PhysicalCsv(report.ReadoutRecords) : DirectCsv(report.Events, report.MeasuredKeV);
        int count = report.PhysicalReadout ? report.ReadoutRecords.Count : report.Events.Count;
        byte[] meta = Meta(report, csvFileName, columns, count);
        return new(csv, Encoding.UTF8.GetBytes(report.ConfigurationJson)) { MetaJson = meta };
    }

    private static string R(double value) => double.IsFinite(value) ? value.ToString("R", CultureInfo.InvariantCulture) : "";
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static byte[] DirectCsv(IReadOnlyList<DetectedEvent> events, IReadOnlyList<double>? measured)
        => Csv(DirectColumns, events.Count, (row, i) =>
        {
            var e = events[i];
            row(I(i)); row(R(e.ArrivalTimeS)); row(I(e.PixelX)); row(I(e.PixelY)); row(R(e.DepositKeV));
            row(measured is null ? "" : R(measured[i]));
        });

    private static byte[] PhysicalCsv(IReadOnlyList<MeasuredReadoutRecord> records)
        => Csv(PhysicalColumns(0), records.Count, (row, i) =>
        {
            var r = records[i];
            row(I(r.Index)); row(R(r.HoldTimeS)); row(R(r.TriggerTimeS));
            for (int channel = 0; channel < 4; channel++)
            {
                double code = r.Codes.Channel(channel);
                // Validate rather than round: these must be the acquisition's integer conversion codes.
                if (!double.IsFinite(code) || code != Math.Truncate(code)) throw new InvalidOperationException("Retained ADC code is not an integer.");
                row(I(checked((int)code)));
            }
            row(R(r.RawX)); row(R(r.RawY)); row(I(r.Crystal));
            row(r.Crystal < 0 ? "" : R(r.EnergyKeV)); // Studio shows an unassigned conversion's energy as unavailable
        });

    private static byte[] Csv(IReadOnlyList<Column> columns, int count, Action<Action<string>, int> fill)
    {
        using var buffer = new MemoryStream();
        using (var writer = new StreamWriter(buffer, new UTF8Encoding(false), 1 << 16) { NewLine = LineEnding })
        {
            writer.WriteLine(string.Join(",", columns.Select(c => c.Name)));
            bool first = true;
            void Cell(string text)
            {
                if (!first) writer.Write(',');
                writer.Write(text);
                first = false;
            }
            for (int i = 0; i < count; i++)
            {
                first = true;
                fill(Cell, i);
                writer.WriteLine();
            }
        }
        return buffer.ToArray();
    }

    private static byte[] Meta(SimulationReport report, string csvFileName, IReadOnlyList<Column> columns, int count)
    {
        using var buffer = new MemoryStream();
        // A standalone file, not HTML: relaxed escaping keeps "—", "µ" and "×" readable.
        using (var json = new Utf8JsonWriter(buffer, new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            json.WriteString("schema", Schema);
            json.WriteNumber("version", SchemaVersion);
            json.WriteString("generator", $"GCAM Studio {report.Version}");
            json.WriteString("exportTimeLocal", report.ExportedAt.ToString("O", CultureInfo.InvariantCulture));
            json.WriteString("exportTimeUtc", report.ExportedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            json.WriteString("notice", Notice);

            json.WriteStartObject("files");
            json.WriteString("events", csvFileName);
            json.WriteString("scene", Path.GetFileName(ReportFiles.ScenePath(csvFileName)));
            json.WriteString("meta", Path.GetFileName(ReportFiles.MetaPath(csvFileName)));
            json.WriteEndObject();

            json.WriteStartObject("readout");
            if (report.PhysicalReadout)
            {
                json.WriteString("mode", nameof(ReadoutMode.FourOutputAnger)); // the one physical preset Studio runs
                json.WriteString("label", "Experimental GAGG four-output readout");
                string line = report.ReferenceLineKeV is { } keV ? $"the {keV.ToString("0.###", CultureInfo.InvariantCulture)} keV" : "the reference";
                json.WriteString("description", "Scintillation light reaches a SiPM array whose charge-division network gives four outputs, A–D, "
                    + $"held together and digitised as {report.AdcBits}-bit ADC codes; position comes from the outputs' relative signals, the crystal "
                    + $"from the captured calibration's flood-map look-up table and the energy from {line} line's calibration. Every retained "
                    + "conversion is a row, unassigned ones included. The captured calibration bank is not in the scene file, so the scene file "
                    + "records this run's inputs but does not reproduce it.");
                json.WriteNumber("adcBits", report.AdcBits);
                json.WritePropertyName("channels");
                JsonSerializer.Serialize(json, new[] { "A", "B", "C", "D" });
                if (report.ReferenceLineKeV is { } reference) json.WriteNumber("referenceLineKeV", reference);
            }
            else
            {
                json.WriteString("mode", nameof(ReadoutMode.DirectCrystal));
                json.WriteString("label", "Direct crystal assignment");
                json.WriteString("description", DirectReadout);
                json.WriteBoolean("measuredEnergyAvailable", report.MeasuredKeV is not null);
                json.WriteBoolean("spectrumPileUp", report.MeasuredPileUp);
            }
            json.WriteEndObject();

            json.WriteStartObject("csv");
            json.WriteString("encoding", "UTF-8 without byte-order mark");
            json.WriteString("delimiter", ",");
            json.WriteString("lineEnding", "CRLF");
            json.WriteString("header", "The first row holds the column names.");
            json.WriteString("numbers", "Invariant culture: decimal point, no thousands separator; shortest round-trip form, exponent as E (for example 2.5E-06). Codes, pixels and crystals are integers.");
            json.WriteString("emptyCell", "No value; the column's meaning says when.");
            json.WriteEndObject();

            json.WriteStartArray("columns");
            foreach (var column in columns)
            {
                json.WriteStartObject();
                json.WriteString("name", column.Name);
                json.WriteString("unit", column.Unit);
                json.WriteString("meaning", column.Meaning);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteNumber("eventCount", count);

            Facts(json, "scene", report.Scene);
            Facts(json, "acquisition", report.Acquisition);
            string configuration = ReportWriter.DisplayedConfiguration(report.ConfigurationJson, out string? spectrumId, out var omitted);
            json.WriteString("configurationNote", omitted.Count == 0
                ? "The acquired configuration, as in the scene file."
                : $"The acquired configuration; the ambient spectrum tables ({string.Join(", ", omitted)}) of {spectrumId ?? "the ambient spectrum"} show their entry counts only, the full tables are in the scene file.");
            json.WritePropertyName("configuration");
            JsonNode.Parse(configuration)!.WriteTo(json);
            json.WriteEndObject();
        }
        return buffer.ToArray();
    }

    private static void Facts(Utf8JsonWriter json, string name, IReadOnlyList<FactRow> rows)
    {
        json.WriteStartArray(name);
        foreach (var row in rows)
        {
            json.WriteStartObject();
            json.WriteString("label", row.Label);
            json.WriteString("value", row.Value);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }
}
