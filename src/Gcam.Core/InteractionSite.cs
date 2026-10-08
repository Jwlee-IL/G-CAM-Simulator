namespace Gcam.Core;

/// <summary>One gamma interaction inside the crystal array, recorded BEFORE any optical processing (light spread,
/// crosstalk, photosensor, electronics): where it happened, when, and how much energy the interaction left there.
/// A Compton history is a short list of these; a physical readout turns them into scintillation light at their
/// own XYZ (TODO-19, RD-4). <see cref="CrystalX"/> / <see cref="CrystalY"/> are the pitch-cell indices the transport
/// assigns (the same index the direct arg-max assignment uses).</summary>
/// <param name="XMm">Lateral x of the interaction (mm, detector frame).</param>
/// <param name="YMm">Lateral y of the interaction (mm, detector frame).</param>
/// <param name="ZMm">Height of the interaction (mm, detector frame; the crystal spans [plane − depth, plane]).</param>
/// <param name="TimeNs">Time after the photon entered the crystal (flight path / c), ns.</param>
/// <param name="EnergyKeV">Energy deposited at this interaction (keV).</param>
/// <param name="CrystalX">Crystal column index.</param>
/// <param name="CrystalY">Crystal row index.</param>
public readonly record struct InteractionSite(double XMm, double YMm, double ZMm, double TimeNs, double EnergyKeV,
    int CrystalX, int CrystalY);
