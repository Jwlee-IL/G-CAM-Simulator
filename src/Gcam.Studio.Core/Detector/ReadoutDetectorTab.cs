namespace Gcam.Studio.Core.Detector;

/// <summary>The Detector workspace's views, in the order of its view switch: the detector face (both readouts), then the
/// physical readout's SiPM, Anger, Calibration and Diagnostics views.</summary>
public enum ReadoutDetectorTab { Face, Sipm, Anger, Calibration, Diagnostics }
