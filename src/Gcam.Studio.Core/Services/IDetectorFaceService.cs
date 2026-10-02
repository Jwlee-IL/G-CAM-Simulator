using Gcam.Configuration;
using Gcam.Studio.Core.Detector;

namespace Gcam.Studio.Core.Services;

/// <summary>Supplies the engine's seeded gain pattern to pure face geometry.</summary>
public interface IDetectorFaceService
{
    DetectorFace Build(OpticsSettings optics, DetectorSettings detector);
}
