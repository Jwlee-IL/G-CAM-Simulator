using Gcam.Configuration;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>One prepared line: its published view, the shared prepared device, the calibration and the resolved
/// readout policy it was built with, and the accounted bytes the session cache charges for it.</summary>
internal sealed record ReadoutArtifact(ReadoutPreparation View, ReadoutDevice Device, ReadoutCalibration Calibration,
    ReadoutConfig Policy, long AccountedBytes);
