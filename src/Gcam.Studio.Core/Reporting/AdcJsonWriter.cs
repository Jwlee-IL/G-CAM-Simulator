using System.Text.Json;

namespace Gcam.Studio.Core.Reporting;

/// <summary>Compact conversion columns copied from the retained measured prefix, including unassigned conversions.</summary>
public static class AdcJsonWriter
{
    public static ReportFiles Write(SimulationReport report)
    {
        if (report.AdcRecords.Count == 0) throw new InvalidOperationException("No retained four-channel ADC values are available.");
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("header");
            json.WriteString("schema", "gcam.studio.adc"); json.WriteNumber("version", 1);
            json.WriteString("generator", report.Version);
            json.WriteString("exportTimeLocal", report.ExportedAt); json.WriteString("exportTimeUtc", report.ExportedAt.ToUniversalTime());
            json.WriteString("notice", "Simulation — not a measurement");
            json.WritePropertyName("sceneSummary"); JsonSerializer.Serialize(json, report.Scene);
            json.WritePropertyName("configuration"); json.WriteRawValue(report.ConfigurationJson);
            json.WritePropertyName("acquisition"); JsonSerializer.Serialize(json, report.Acquisition);
            json.WriteStartObject("readout");
            json.WriteNumber("adcBits", report.AdcBits); json.WriteString("units", "signed, pedestal-subtracted ADC codes");
            json.WriteString("description", "Retained measured conversions, including unassigned records. Physical-readout acquisition depends on a captured calibration bank that is not embedded; the ordinary CLI does not run this mode.");
            json.WritePropertyName("channels"); JsonSerializer.Serialize(json, new[] { "A", "B", "C", "D" });
            json.WriteEndObject();
            json.WritePropertyName("columns"); JsonSerializer.Serialize(json, new[] { "index", "holdTimeS", "triggerTimeS", "A", "B", "C", "D" });
            json.WriteNumber("eventCount", report.AdcRecords.Count); json.WriteEndObject();
            json.WriteStartArray("events");
            foreach (var record in report.AdcRecords)
            {
                json.WriteStartArray(); json.WriteNumberValue(record.Index);
                json.WriteNumberValue(record.HoldTimeS); json.WriteNumberValue(record.TriggerTimeS);
                for (int channel = 0; channel < 4; channel++)
                {
                    double code = record.Codes.Channel(channel);
                    // Validate rather than round: these must be the acquisition's integer conversion codes.
                    if (!double.IsFinite(code) || code != Math.Truncate(code)) throw new InvalidOperationException("Retained ADC code is not an integer.");
                    json.WriteNumberValue(checked((int)code));
                }
                json.WriteEndArray();
            }
            json.WriteEndArray(); json.WriteEndObject();
        }
        return new(buffer.ToArray(), []) { HasScene = false };
    }
}
