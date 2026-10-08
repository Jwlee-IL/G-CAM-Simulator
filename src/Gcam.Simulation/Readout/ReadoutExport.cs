using System.Text.Json;
using System.Text.Json.Nodes;
using Gcam.Configuration;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>
/// The engine's resolved readout configuration for one geometry × readout × trigger of a study request (RD-9 / RD-10):
/// every value the schematic draws (crystal and sensor grids, network topology and resistors, pulse and hold constants,
/// ADC, trigger, segmentation) after the engine applied its defaults, the DC charge-division fractions the engine
/// computed (round-trip exact), and — for a solved circuit — the SPICE netlist of that same network. The schematic
/// generator and the independent netlist solver read these files instead of re-deriving defaults, so they show and check
/// exactly what the engine simulates.
/// </summary>
public static class ReadoutExport
{
    public static (JsonObject Description, string? Netlist) Build(SimulationConfig scenario, ReadoutStudyRequest request,
        string geometryName, string readoutName, string triggerName)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ReadoutStudy.Validate(request);
        var geometry = request.Geometries.FirstOrDefault(g => g.Name == geometryName)
            ?? throw new ArgumentException($"No geometry '{geometryName}' in the request.");
        var variant = request.Readouts.FirstOrDefault(v => v.Name == readoutName)
            ?? throw new ArgumentException($"No readout '{readoutName}' in the request.");
        var trigger = request.Triggers.FirstOrDefault(t => t.Name == triggerName)
            ?? throw new ArgumentException($"No trigger '{triggerName}' in the request.");
        var detector = scenario.Clone().Detector;
        detector.PixelPitchMm = geometry.PitchMm;
        detector.ReflectorGapMm = geometry.GapMm;
        var ro = Copy(request.Base);
        if (geometry.Optics is not null) ro.Optics = Copy(geometry.Optics);
        ro.Mode = variant.Mode;
        if (variant.Network is not null) ro.Network = Copy(variant.Network);
        if (variant.Segmentation is { } seg) ro.Calibration.Segmentation = seg;
        if (variant.ZeroSuppressionSigma is { } zs) ro.Digitizer.ZeroSuppressionSigma = zs;
        ro.Trigger = Copy(trigger.Trigger);

        // The optical table is not exported; a one-photon table is enough to instantiate the chain's constants.
        var drawing = Copy(ro);
        drawing.Optics.PhotonsPerBin = 1;
        drawing.Optics.DepthBins = 1;
        var crystals = CrystalArrayGeometry.From(detector);
        var device = new ReadoutDevice(crystals, drawing, 1);
        var processor = new ReadoutPulseProcessor(device, ro.Pulse, ro.Trigger);
        var s = device.Sensors;
        var weights = new JsonArray();
        for (int k = 0; k < device.Network.Inputs; k++)
            weights.Add(new JsonArray(device.Network.Row(k).ToArray().Select(w => (JsonNode?)JsonValue.Create(w)).ToArray()));
        var description = new JsonObject
        {
            ["About"] = "Engine-resolved readout configuration (TODO-19 RD-9 / RD-10). Illustrative; not a buildable design.",
            ["Geometry"] = geometry.Name,
            ["Readout"] = variant.Name,
            ["TriggerName"] = trigger.Name,
            ["Crystals"] = new JsonObject
            {
                ["CountX"] = crystals.CountX, ["CountY"] = crystals.CountY, ["PitchMm"] = crystals.PitchMm,
                ["GapMm"] = crystals.GapMm, ["ActiveWidthMm"] = crystals.ActiveWidthMm, ["DepthMm"] = crystals.DepthMm,
                ["Material"] = CrystalMaterial.ForConfig(detector.Material).Name,
            },
            ["Sensors"] = new JsonObject
            {
                ["CountX"] = s.CountX, ["CountY"] = s.CountY, ["PitchMm"] = s.PitchMm, ["ActiveWidthMm"] = s.ActiveWidthMm,
                ["MatchedOneToOne"] = s.CountX == crystals.CountX && s.CountY == crystals.CountY && s.PitchMm == crystals.PitchMm,
                ["Pde"] = device.Pde, ["Enf"] = device.Enf,
            },
            ["Optics"] = new JsonObject { ["Surface"] = ro.Optics.Surface.ToString(), ["WallReflectance"] = ro.Optics.WallReflectance },
            ["Mode"] = ro.Mode.ToString(),
            ["Network"] = new JsonObject
            {
                ["Topology"] = ro.Mode == ReadoutMode.FourOutputAnger ? ro.Network.Topology.ToString() : "None",
                ["RowResistanceOhm"] = ro.Network.RowResistanceOhm, ["ColumnResistanceOhm"] = ro.Network.ColumnResistanceOhm,
                ["GridResistanceOhm"] = ro.Network.GridResistanceOhm, ["DrainResistanceOhm"] = ro.Network.DrainResistanceOhm,
                ["InputImpedanceOhm"] = ro.Network.InputImpedanceOhm, ["Outputs"] = device.Channels,
            },
            ["Pulse"] = new JsonObject
            {
                ["RiseNs"] = processor.RiseNs, ["TailNs"] = processor.TailNs, ["HoldWindowNs"] = processor.HoldWindowNs,
                ["Hold"] = ro.Pulse.Hold.ToString(), ["DeadTimeNs"] = processor.DeadTimeNs,
            },
            ["Digitizer"] = new JsonObject
            {
                ["Bits"] = ro.Digitizer.Bits, ["FullScaleKeV"] = ro.Digitizer.FullScaleKeV, ["Enob"] = ro.Digitizer.Enob,
                ["SampleRateMsps"] = FrontEndParts.AdcSampleRateHz / 1e6, ["NoiseKeV"] = ro.Digitizer.NoiseKeV,
            },
            ["Trigger"] = new JsonObject
            {
                ["Logic"] = ro.Trigger.Logic.ToString(), ["Threshold"] = ro.Trigger.Threshold, ["Unit"] = ro.Trigger.Unit.ToString(),
            },
            ["Segmentation"] = ro.Calibration.Segmentation.ToString(),
            ["Weights"] = weights,
        };
        string? netlist = device.Network.Circuit?.ToSpice(
            $"{variant.Name} on {geometry.Name}: {ro.Network.Topology}, {s.CountX} x {s.CountY} SiPM nodes");
        return (description, netlist);
    }

    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
