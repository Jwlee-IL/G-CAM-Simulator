namespace Gcam.Configuration;

/// <summary>
/// The physical readout of the crystal array (TODO-19). Null on <see cref="DetectorConfig.Readout"/>, or
/// <see cref="ReadoutMode.DirectCrystal"/>, is today's direct crystal assignment: nothing below is read and no random
/// number is drawn. The other modes are configured by the explicit, typed sections below, grouped by physical role.
/// Every default is either a repository preset (cited at the property) or a labelled assumption; none is taken from a
/// specific instrument.
/// </summary>
public sealed class ReadoutConfig
{
    public ReadoutMode Mode { get; set; } = ReadoutMode.DirectCrystal;

    /// <summary>The photosensor array behind the crystals (null sizes = 1:1 coupling to the crystal array).</summary>
    public ReadoutSensorConfig Sensors { get; set; } = new();

    /// <summary>Optical transport from an interaction site to the sensor plane.</summary>
    public ReadoutOpticsConfig Optics { get; set; } = new();

    /// <summary>Scintillation light yield and intrinsic (non-proportionality) fluctuation.</summary>
    public ReadoutScintillationConfig Scintillation { get; set; } = new();

    /// <summary>SiPM photon detection, avalanche gain fluctuation and dark counts.</summary>
    public ReadoutSipmConfig Sipm { get; set; } = new();

    /// <summary>The resistive charge-division network (used by <see cref="ReadoutMode.FourOutputAnger"/> only).</summary>
    public ChargeNetworkConfig Network { get; set; } = new();

    /// <summary>Per-channel electronic noise and analogue-to-digital conversion.</summary>
    public ReadoutDigitizerConfig Digitizer { get; set; } = new();

    /// <summary>Trigger logic and threshold, with explicit units (RD-2).</summary>
    public ReadoutTriggerConfig Trigger { get; set; } = new();

    /// <summary>Time-domain pulse, hold and re-arm (RD-3).</summary>
    public ReadoutPulseConfig Pulse { get; set; } = new();

    /// <summary>Flood-map look-up-table calibration (watershed, explicit failure).</summary>
    public FloodCalibrationConfig Calibration { get; set; } = new();
}
