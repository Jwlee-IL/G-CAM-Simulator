namespace Gcam.Configuration;

/// <summary>Resistive charge-division network (DC charge fractions; the network's own RC impulse response is not
/// modelled — every output shares the shaping chain's pulse, RD-5). Only resistance RATIOS matter for the charge
/// fractions when the outputs are virtual grounds. The DPC default makes the column chains ten times less resistive than
/// the row chains, so they carry little of the division (the near-separable regime the circuit is designed for); with
/// equal resistors the measured flood shows a strong non-separable distortion (central rows compressed) — a swept
/// alternative, not a hidden correction. Every value is a parameter; none is a tuned optimum.</summary>
public sealed class ChargeNetworkConfig
{
    public NetworkTopology Topology { get; set; } = NetworkTopology.Dpc;

    /// <summary>DPC: resistor between neighbouring nodes of a row chain, and at each chain end (Ω).</summary>
    public double RowResistanceOhm { get; set; } = 1000;

    /// <summary>DPC: resistor between neighbouring nodes of a column chain, and at each column end (Ω).</summary>
    public double ColumnResistanceOhm { get; set; } = 100;

    /// <summary>CornerGrid: resistor between neighbouring sensor nodes (Ω).</summary>
    public double GridResistanceOhm { get; set; } = 1000;

    /// <summary>CornerGrid: resistor from each corner node to its output (Ω).</summary>
    public double DrainResistanceOhm { get; set; } = 1000;

    /// <summary>Input impedance of each output amplifier to ground (Ω); 0 = ideal virtual ground.</summary>
    public double InputImpedanceOhm { get; set; }
}
