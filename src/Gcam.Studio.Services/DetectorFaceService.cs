using Gcam.Configuration;
using Gcam.Detector;
using Gcam.Studio.Core.Detector;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

public sealed class DetectorFaceService : IDetectorFaceService
{
    public DetectorFace Build(OpticsSettings optics, DetectorSettings detector)
    {
        var gain = new CrystalUniformity(new DetectorConfig { PixelsX = optics.DetectorPixels,
            PixelsY = optics.DetectorPixels, GainSigma = detector.GainSigma, UniformitySeed = detector.GainSeed }).Gain;
        return DetectorFace.Create(optics.DetectorPixels, optics.PixelPitchMm, detector.ReflectorGapMm, gain);
    }
}
