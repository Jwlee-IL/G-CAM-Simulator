using System.Diagnostics;
using Gcam.Studio.Core.Plotting;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

public static class PhysicalReadoutWaveform
{
    public static WaveformView Process(AcquisitionSnapshot snapshot, WaveformSettings settings, CancellationToken token)
    {
        var watch = Stopwatch.StartNew(); var physical = snapshot.Readout!; var p = physical.Preparation;
        if (settings.RateStudy || settings.Ideal) throw new NotSupportedException("Physical scope displays measured events only; replay studies are unavailable.");
        if (!double.IsFinite(settings.WindowUs) || settings.WindowUs <= 0) throw new ArgumentOutOfRangeException(nameof(settings));
        if (physical.Records.Count > 0 && (settings.TriggerIndex < 0 || settings.TriggerIndex >= physical.Records.Count))
            throw new ArgumentOutOfRangeException(nameof(settings.TriggerIndex));
        const int MaximumTotalSamples = 1_000_000; // Five lanes and their threshold share one allocation budget.
        int samples = (int)Math.Min(MaximumTotalSamples / 6, Math.Ceiling(settings.WindowUs * 125));
        double length = samples / 125.0;
        var selected = physical.Records.Count > 0 ? physical.Records[Math.Clamp(settings.TriggerIndex, 0, physical.Records.Count - 1)] : null;
        double hold = selected?.HoldTimeS ?? snapshot.LiveTimeS;
        double origin = hold - length * .2e-6;
        var lanes = Enumerable.Range(0,5).Select(_ => new double[samples]).ToArray();
        foreach (var hit in physical.Hits)
        {
            token.ThrowIfCancellationRequested();
            if (hit.ArrivalTimeS > origin + length * 1e-6 || hit.ArrivalTimeS + p.SupportNs * 1e-9 < origin) continue;
            for (int i = 0; i < samples; i++)
            {
                if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
                double ns = (origin - hit.ArrivalTimeS) * 1e9 + i * 8;
                if (ns < 0 || ns > p.SupportNs) continue;
                double u = (Math.Exp(-ns / p.TailNs) - Math.Exp(-ns / p.RiseNs)) / p.Normalization;
                for (int c = 0; c < 4; c++) lanes[c][i] += hit.Charges.Channel(c) * u;
            }
        }
        for (int i=0;i<samples;i++) lanes[4][i]=lanes[0][i]+lanes[1][i]+lanes[2][i]+lanes[3][i];
        var series = lanes.Select((values,c) => new PlotSeries(c == 4 ? "Sum" : "ABCD"[c].ToString(), values,
            Origin: -length*.2, Step: .008) { PreparedPyramid = new MinMaxPyramid(values) }).ToArray();
        var threshold = Enumerable.Repeat(p.ThresholdCodes, samples).ToArray();
        var thresholdSeries = new PlotSeries("Sum trigger threshold · code-equivalent", threshold, Origin: -length*.2,
            Step: .008, ColourRole: PlotColourRole.Series2) { PreparedPyramid = new MinMaxPyramid(threshold) };
        var markers = selected is null ? Array.Empty<PlotMarker>() : new[] {
            new PlotMarker((selected.TriggerTimeS-hold)*1e6,"Trigger"), new PlotMarker(0,"Hold") };
        var events = physical.Records.Where(e=>e.HoldTimeS>=origin && e.HoldTimeS<=Math.Min(snapshot.LiveTimeS,origin+length*1e-6))
            .Select(e=>new WaveformEvent(e.Index,e.Crystal<0?-1:e.Crystal%12,e.Crystal<0?-1:e.Crystal/12,
                double.NaN,e.HoldTimeS,(e.HoldTimeS-hold)*1e6,e.EnergyKeV) { IsMeasured = true }).ToArray();
        string reading = selected is null ? "No observed conversion" : $"Measured #{selected.Index} · LUT {(selected.Crystal < 0 ? "unknown" : selected.Crystal.ToString())} · raw X/Y {selected.RawX:0.####} / {selected.RawY:0.####} · {(selected.Crystal < 0 ? "energy unavailable" : $"{selected.EnergyKeV:0.##} keV")}\nHeld ADC A/B/C/D {selected.Codes.A:0}/{selected.Codes.B:0}/{selected.Codes.C:0}/{selected.Codes.D:0} · dominant hit #{selected.DominantHit} · contributors {selected.Contributors}\nCalibration {p.Id}";
        return new(series[4],series[4],Array.AsReadOnly(events),"Experimental GAGG · DPC 0.01 · four held 14-bit ADC outputs",reading,
            "Realised analogue pulses in code-equivalent units; held ADC values are listed separately. Sum threshold; no continuous digitizer noise, network RC, skew or jitter. " +
            (origin+length*1e-6>snapshot.LiveTimeS?"Partial observed horizon: no future hits. ":"") +
            (length<settings.WindowUs?"Window clipped to shared five-lane sample budget.":""),length,watch.Elapsed)
        { PhysicalLanes = Array.AsReadOnly(series), PhysicalMarkers = Array.AsReadOnly(markers), PhysicalThreshold = thresholdSeries };
    }
}
