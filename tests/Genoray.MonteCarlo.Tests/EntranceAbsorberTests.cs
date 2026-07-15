using Genoray.MonteCarlo.Detector;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>The passive entrance absorber (source encapsulation + detector window) must behave like a real
/// low-energy filter: nearly transparent at 662 keV, strongly attenuating at the 32 keV Ba K X-ray, and
/// monotonic in between — so it tames the soft X-ray lines without eating the photopeak.</summary>
public class EntranceAbsorberTests
{
    [Fact]
    public void ZeroThickness_TransmitsEverything()
    {
        var a = new EntranceAbsorber(0.0);
        Assert.Equal(1.0, a.Transmit(32.1), 6);
        Assert.Equal(1.0, a.Transmit(661.7), 6);
    }

    [Fact]
    public void BarelyTouches662_ButStronglyAttenuates32()
    {
        var a = new EntranceAbsorber(0.15);          // the WPF default (~thin steel window)
        double t662 = a.Transmit(661.7);
        double t32 = a.Transmit(32.1);
        Assert.True(t662 > 0.98, $"662 keV should pass almost unattenuated, got {t662:P1}");
        Assert.InRange(t32, 0.35, 0.55);             // ~45% — a modest X-ray bump, not a blown-up peak
        Assert.True(t32 < t662, "low energy must be attenuated more than the photopeak");
    }

    [Fact]
    public void TransmissionIsMonotonicInEnergy()
    {
        var a = new EntranceAbsorber(0.3);
        double prev = 0.0;
        foreach (double e in new[] { 32.0, 60.0, 100.0, 200.0, 400.0, 662.0, 1000.0, 1332.0 })
        {
            double t = a.Transmit(e);
            Assert.True(t >= prev, $"transmission must rise with energy; dropped at {e} keV ({t:F3} < {prev:F3})");
            prev = t;
        }
    }

    [Fact]
    public void ThickerAbsorbs_More()
    {
        double thin = new EntranceAbsorber(0.1).Transmit(32.1);
        double thick = new EntranceAbsorber(0.5).Transmit(32.1);
        Assert.True(thick < thin, "a thicker absorber must transmit less at 32 keV");
    }
}
