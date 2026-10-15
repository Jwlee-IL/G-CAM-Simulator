using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Rendering;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Core.Reporting;

/// <summary>Deterministic, self-contained HTML and inline SVG from already-published values.</summary>
public static class ReportWriter
{
    private static string E(string text) => WebUtility.HtmlEncode(text);
    private static string N(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>The ambient spectrum's table members, by their configuration names. The terrestrial field's tables hold
    /// about 1.8 × 10⁴ rows (megabytes of JSON), which the scene file beside the report already records.</summary>
    private static readonly string[] AmbientSpectrumTables =
        [nameof(IncidentSpectrum.Lines), nameof(IncidentSpectrum.Continuum), nameof(IncidentSpectrum.EnergyZenith), nameof(IncidentSpectrum.NotIncluded)];
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static ReportFiles Write(SimulationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var html = new StringBuilder("<!doctype html>\n<html lang=\"en\"><head><meta charset=\"utf-8\"><title>GCAM Studio simulation report</title><style>");
        html.Append("body{font-family:sans-serif;max-width:1000px;margin:2em auto;padding:1em;color:#222}table{border-collapse:collapse;width:100%;margin:1em 0}th,td{border:1px solid #bbb;padding:.4em;text-align:left}svg{width:100%;max-height:500px}pre{white-space:pre-wrap;overflow-wrap:anywhere}figure{break-inside:avoid}h2{break-after:avoid}@media print{body{margin:0}}")
            .Append("</style></head><body><h1>GCAM Studio simulation report</h1><p><strong>Simulation — not a measurement</strong></p>")
            .Append("<p>Exported (local): ").Append(E(report.ExportedAt.ToString("O", CultureInfo.InvariantCulture)))
            .Append("<br>Exported (UTC): ").Append(E(report.ExportedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)))
            .Append("<br>Studio version: ").Append(E(report.Version)).Append("</p>");
        if (report.PhysicalReadout)
            html.Append("<p>Physical-readout acquisition depends on a captured calibration bank that the scene file does not hold. The ordinary CLI does not run this mode.</p>");
        html.Append("<h2>Scene</h2>");
        Facts(html, report.Scene);
        string configuration = DisplayedConfiguration(report.ConfigurationJson, out string? spectrumId, out var omitted);
        html.Append("<p>The scene file records the acquisition configuration; it does not promise reproduction.</p>");
        if (omitted.Count > 0)
            html.Append("<p>The ambient spectrum tables (").Append(E(string.Join(", ", omitted))).Append(") of ")
                .Append(E(spectrumId ?? "the ambient spectrum")).Append(" are in the scene file; the configuration below shows their entry counts only.</p>");
        html.Append("<details><summary>Acquired configuration (sources, optics, detector, decoder, readout, ambient)</summary><pre>")
            .Append(E(configuration)).Append("</pre></details><h2>Acquisition</h2>");
        Facts(html, report.Acquisition);
        html.Append("<h2>Imaging</h2>");
        Facts(html, report.Imaging);
        if (report.Image is { } image)
        {
            Heatmap(html, image.Flood, image.FloodOriginMm, image.FloodStepMm, "Detector flood map", "counts / displayed values", null, []);
            if (image.Reconstruction is { } reconstruction)
                Heatmap(html, reconstruction, image.ReconOriginMm, image.ReconStepMm, "Reconstruction", "decoded / MLEM intensity", image.Estimate, report.Peaks);
        }
        else html.Append("<p>Image unavailable.</p>");
        Facts(html, report.Peaks.Select((p, i) => new FactRow($"Found peak {i + 1}", p.Description)).ToArray());
        html.Append("<h2>Spectrum</h2>");
        Facts(html, report.SpectrumSettings);
        if (report.Spectrum is { } spectrum)
        {
            Spectrum(html, spectrum, report.LogY);
            Facts(html, spectrum.Bands.Select(b => new FactRow(b.Name, b.Description)).ToArray());
        }
        else html.Append("<p>Spectrum unavailable.</p>");
        html.Append("<h2>Imaging measurements</h2><table><thead><tr><th>Name</th><th>Pane</th><th>Kind</th><th>Value</th><th>Detail</th></tr></thead><tbody>");
        foreach (var m in report.Measurements)
        {
            html.Append("<tr>");
            foreach (string cell in new[] { m.Name, m.Pane, m.Kind, m.Value, m.Detail }) html.Append("<td>").Append(E(cell)).Append("</td>");
            html.Append("</tr>");
        }
        html.Append("</tbody></table>");
        if (report.Measurements.Count == 0) html.Append("<p>No Imaging measurements.</p>");
        html.Append("</body></html>\n");
        return new(Encoding.UTF8.GetBytes(html.ToString()), Encoding.UTF8.GetBytes(report.ConfigurationJson));
    }

    /// <summary>The configuration as the report shows it: the scene file's JSON with each non-empty ambient spectrum table
    /// replaced by a note of its entry count. Every other member, the spectrum's Id and ContentHash included, is kept.
    /// The event-data metadata file shows the same reduced configuration.</summary>
    internal static string DisplayedConfiguration(string json, out string? spectrumId, out IReadOnlyList<string> omitted)
    {
        var tables = new List<string>();
        omitted = tables;
        spectrumId = null;
        if (JsonNode.Parse(json) is not JsonObject root
            || root[nameof(SimulationConfig.Ambient)]?[nameof(AmbientFieldConfig.Spectrum)] is not JsonObject spectrum) return json;
        foreach (string table in AmbientSpectrumTables)
            if (spectrum[table] is JsonArray { Count: > 0 } rows)
            {
                spectrum[table] = $"omitted from this report: {rows.Count.ToString(CultureInfo.InvariantCulture)} entries; the full table is in the scene file";
                tables.Add(table);
            }
        spectrumId = spectrum[nameof(IncidentSpectrum.Id)] is JsonValue id && id.TryGetValue(out string? text) ? text : null;
        return tables.Count == 0 ? json : root.ToJsonString(Indented);
    }

    private static void Facts(StringBuilder html, IReadOnlyList<FactRow> rows)
    {
        if (rows.Count == 0) return;
        html.Append("<table><tbody>");
        foreach (var row in rows) html.Append("<tr><th>").Append(E(row.Label)).Append("</th><td>").Append(E(row.Value)).Append("</td></tr>");
        html.Append("</tbody></table>");
    }

    private static void Heatmap(StringBuilder html, DetectorImage image, double originMm, double stepMm,
        string title, string unit, SourceEstimate? estimate, IReadOnlyList<ImagingPeak> peaks)
    {
        // Same min/max, rounding and bottom-up row convention as HeatmapView. Only rendering is calculated here.
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        foreach (double value in image.Raw) { min = Math.Min(min, value); max = Math.Max(max, value); }
        double span = max > min ? max - min : 1;
        const double plotSize = 360, left = 65, top = 30;
        double cellWidth = plotSize / image.Width, cellHeight = plotSize / image.Height;
        html.Append("<figure><figcaption>").Append(E(title)).Append(" — full published grid, x/y in mm</figcaption><svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 500 440\" role=\"img\"><title>").Append(E(title)).Append("</title>");
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                int index = (int)Math.Round((image[x, y] - min) / span * 255);
                uint colour = Colormap.Viridis[Math.Clamp(index, 0, 255)] & 0xFFFFFF;
                html.Append(CultureInfo.InvariantCulture, $"<rect x=\"{N(left + x * cellWidth)}\" y=\"{N(top + (image.Height - 1 - y) * cellHeight)}\" width=\"{N(cellWidth)}\" height=\"{N(cellHeight)}\" fill=\"#{colour:X6}\"><title>({N(originMm + x * stepMm)}, {N(originMm + y * stepMm)}) mm: {N(image[x, y])}</title></rect>");
            }
        if (estimate is not null) Marker(estimate.Position.X, estimate.Position.Y, "Estimate");
        foreach (var peak in peaks) Marker(peak.Xmm, peak.Ymm, peak.Description);
        void Marker(double x, double y, string label)
        {
            double px = left + ((x - originMm) / stepMm + .5) * cellWidth;
            double py = top + (image.Height - .5 - (y - originMm) / stepMm) * cellHeight;
            if (!double.IsFinite(px) || !double.IsFinite(py)) return;
            html.Append($"<circle cx=\"{N(px)}\" cy=\"{N(py)}\" r=\"5\" fill=\"none\" stroke=\"white\" stroke-width=\"2\"><title>{E(label)}</title></circle>");
        }
        Text(html, left, 410, $"x: {N(originMm - stepMm / 2)} to {N(originMm + (image.Width - .5) * stepMm)} mm");
        Text(html, left, 430, $"y: {N(originMm - stepMm / 2)} to {N(originMm + (image.Height - .5) * stepMm)} mm; {unit}");
        for (int i = 0; i < 256; i++)
            html.Append($"<rect x=\"440\" y=\"{N(top + (255 - i) * plotSize / 256)}\" width=\"12\" height=\"{N(plotSize / 256)}\" fill=\"#{Colormap.Viridis[i] & 0xFFFFFF:X6}\" />");
        Text(html, 435, 20, N(max));
        Text(html, 435, 410, N(min));
        html.Append("</svg></figure>");
    }

    private static void Spectrum(StringBuilder html, SpectrumView spectrum, bool logY)
    {
        if (spectrum.Counts.Length == 0 || spectrum.BinEdgesKeV.Length != spectrum.Counts.Length + 1)
        { html.Append("<p>Published histogram unavailable.</p>"); return; }
        const double left = 65, top = 30, width = 700, height = 300;
        double lo = spectrum.BinEdgesKeV[0], hi = spectrum.BinEdgesKeV[^1];
        double ceiling = Math.Max(1, spectrum.Counts.Max());
        double X(double energy) => left + (energy - lo) / (hi - lo) * width;
        double Y(double count) => top + height * (1 - (logY ? Math.Log10(Math.Max(1, count)) / Math.Log10(Math.Max(10, ceiling)) : count / ceiling));
        html.Append("<figure><figcaption>Full published spectrum — ").Append(logY ? "logarithmic" : "linear").Append(" counts; energy windows and line markers</figcaption><svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 820 390\" role=\"img\"><title>Measured energy spectrum</title>");
        foreach (var band in spectrum.Bands)
        {
            double start = Math.Clamp(band.LoKeV, lo, hi), end = Math.Clamp(band.HiKeV, lo, hi);
            html.Append($"<rect x=\"{N(X(start))}\" y=\"{N(top)}\" width=\"{N(X(end) - X(start))}\" height=\"{N(height)}\" fill=\"#eee\"><title>{E(band.Description)}</title></rect>");
        }
        html.Append("<path fill=\"none\" stroke=\"#2166ac\" stroke-width=\"1\" d=\"");
        for (int i = 0; i < spectrum.Counts.Length; i++)
            html.Append($"{(i == 0 ? "M" : "L")} {N(X(spectrum.BinEdgesKeV[i]))} {N(Y(spectrum.Counts[i]))} L {N(X(spectrum.BinEdgesKeV[i + 1]))} {N(Y(spectrum.Counts[i]))} ");
        html.Append("\" />");
        foreach (var line in spectrum.Bands.SelectMany(b => b.Lines).Distinct())
        {
            if (line.EnergyKeV < lo || line.EnergyKeV > hi) continue;
            html.Append($"<line x1=\"{N(X(line.EnergyKeV))}\" x2=\"{N(X(line.EnergyKeV))}\" y1=\"30\" y2=\"330\" stroke=\"#a33\" stroke-dasharray=\"3 3\"><title>{E(line.Isotope)} {N(line.EnergyKeV)} keV</title></line>");
        }
        html.Append("<path d=\"M 65 30 V 330 H 765\" fill=\"none\" stroke=\"#222\" />");
        Text(html, 10, 20, $"{N(ceiling)} counts");
        Text(html, 10, 330, logY ? "1" : "0");
        Text(html, left, 355, $"{N(lo)} keV");
        Text(html, 690, 355, $"{N(hi)} keV");
        Text(html, 290, 380, "measured energy (keV)");
        html.Append("</svg></figure>");
    }

    private static void Text(StringBuilder html, double x, double y, string text)
        => html.Append($"<text x=\"{N(x)}\" y=\"{N(y)}\" font-size=\"12\">{E(text)}</text>");
}
