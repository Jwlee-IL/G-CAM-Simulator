using System.Globalization;
using System.Text;

namespace Gcam.Detector;

/// <summary>A solved charge-division circuit: node names, resistor graph (endpoint −1 = ground), the SiPM input nodes in
/// sensor order, the four output nodes (A, B, C, D = x−y−, x+y−, x−y+, x+y+) and the amplifier input impedance (0 = ideal
/// virtual ground).</summary>
public sealed record ChargeNetworkCircuit(string[] NodeNames, (int A, int B, double Ohm)[] Resistors, int[] InputNodes,
    int[] OutputNodes, double InputOhm)
{
    /// <summary>SPICE netlist of the circuit (RD-10): one DC current source per SiPM node (value 0 A — a solver activates one
    /// at a time with unit current, superposition), the resistor graph, and per output either a 0 V source (an ammeter:
    /// the ideal virtual-ground amplifier input) or a load resistor to ground. Values are written round-trip exact.
    /// Illustrative: the generic published network form, not a board design.</summary>
    public string ToSpice(string title)
    {
        static string N(string name) => name;
        static string V(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.Append("* ").Append(title).Append('\n');
        sb.Append("* Gcam TODO-19 charge-division network (RD-10), generated from the engine's readout configuration.\n");
        sb.Append("* Illustrative generic network, not a buildable design. I_S<k> = SiPM k (k = ky*Sx + kx), 0 A each;\n");
        sb.Append("* a nodal solver drives one source at a time with 1 A. V_<c> = 0 V ammeter (virtual-ground input) or\n");
        sb.Append("* R_LOAD_<c> = amplifier input impedance; outputs A..D = x-y-, x+y-, x-y+, x+y+.\n");
        for (int k = 0; k < InputNodes.Length; k++) sb.Append($"I_S{k} 0 {N(NodeNames[InputNodes[k]])} DC 0\n");
        for (int i = 0; i < Resistors.Length; i++)
        {
            var (a, b, ohm) = Resistors[i];
            sb.Append($"R{i + 1} {(a < 0 ? "0" : N(NodeNames[a]))} {(b < 0 ? "0" : N(NodeNames[b]))} {V(ohm)}\n");
        }
        for (int c = 0; c < OutputNodes.Length; c++)
        {
            string label = ((char)('A' + c)).ToString();
            if (InputOhm == 0) sb.Append($"V_{label} {N(NodeNames[OutputNodes[c]])} 0 DC 0\n");
            else sb.Append($"R_LOAD_{label} {N(NodeNames[OutputNodes[c]])} 0 {V(InputOhm)}\n");
        }
        sb.Append(".end\n");
        return sb.ToString();
    }
}
