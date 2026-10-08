using Gcam.Configuration;
using Gcam.Detector;

namespace Gcam.Tests;

/// <summary>TODO-19, RD-6 "network against independently solved small circuits". The Kirchhoff solver is checked against
/// circuits solved by hand (derivations in each test), then for charge conservation and mirror symmetry on full grids.
/// Round-off bound: the weights come from a Cholesky solve of an at most ~200-node Laplacian with condition number
/// well below 1e6, so absolute errors stay below ~1e-10; 1e-9 is used.</summary>
public class ChargeDivisionNetworkTests
{
    private const double RoundOff = 1e-9;

    /// <summary>DPC, one row of two sensors (row resistors R, column resistors r). Each end node of the row reaches the two
    /// grounded outputs of its column through r each (g = 2/r to ground); with G = 1/R and α = G/(G+g), node equations give
    /// V_L = α V0, V_R = α V1, V1 = V0/(2−α), so the left share is (2−α)/(3−α), split equally between the column's two
    /// outputs. r = R: α = 1/3, left = 5/8 → outputs 5/16, 3/16, 5/16, 3/16 (order x−y−, x+y−, x−y+, x+y+).</summary>
    [Theory]
    [InlineData(1000.0, 1000.0)]
    [InlineData(1000.0, 100.0)]
    [InlineData(470.0, 2200.0)]
    public void Dpc_OneRowOfTwo_MatchesHandSolvedCircuit(double row, double column)
    {
        var net = ChargeDivisionNetwork.Dpc(2, 1, row, column);
        double g = 2 / column, gr = 1 / row, alpha = gr / (gr + g), left = (2 - alpha) / (3 - alpha);
        Assert.Equal(left / 2, net[0, 0], RoundOff);
        Assert.Equal((1 - left) / 2, net[0, 1], RoundOff);
        Assert.Equal(left / 2, net[0, 2], RoundOff);
        Assert.Equal((1 - left) / 2, net[0, 3], RoundOff);
        Assert.Equal(net[0, 0], net[1, 1], RoundOff);                      // mirror image for the other sensor
        if (row == column) Assert.Equal(5.0 / 16, net[0, 0], RoundOff);
    }

    /// <summary>Corner grid 2×2, every resistor equal (G = 1), unit current into node 0. By symmetry V1 = V2; node 0:
    /// 3V0 − 2V1 = 1; node 1: 3V1 − V0 − V3 = 0; node 3: 3V3 = 2V1 → V1 = 1/5, V0 = 7/15, V3 = 2/15; drain currents = V:
    /// 7/15, 3/15, 3/15, 2/15.</summary>
    [Fact]
    public void CornerGrid_TwoByTwo_MatchesHandSolvedCircuit()
    {
        var net = ChargeDivisionNetwork.CornerGrid(2, 2, 1000, 1000);
        Assert.Equal(7.0 / 15, net[0, 0], RoundOff);
        Assert.Equal(3.0 / 15, net[0, 1], RoundOff);
        Assert.Equal(3.0 / 15, net[0, 2], RoundOff);
        Assert.Equal(2.0 / 15, net[0, 3], RoundOff);
    }

    /// <summary>A single-row DPC is a series chain: each chain end reaches ground through its column's two r in parallel
    /// (r/2), so node k (0-based) sees (k+1)·R + r/2 to the left ground and (S−k)·R + r/2 to the right; the current divides
    /// inversely, right share = ((k+1)·R + r/2) / ((S+1)·R + r) — linear in k, exactly (k+1)/(S+1) as r → 0.</summary>
    [Theory]
    [InlineData(7, 1000.0, 100.0)]
    [InlineData(7, 1000.0, 1000.0)]
    [InlineData(12, 1000.0, 1.0)]
    public void Dpc_SingleRow_DividesAsASeriesChain(int s, double row, double column)
    {
        var net = ChargeDivisionNetwork.Dpc(s, 1, row, column);
        for (int k = 0; k < s; k++)
            Assert.Equal(((k + 1) * row + column / 2) / ((s + 1) * row + column), net[k, 1] + net[k, 3], RoundOff);
    }

    [Theory]
    [InlineData(NetworkTopology.IdealBilinear)]
    [InlineData(NetworkTopology.Dpc)]
    [InlineData(NetworkTopology.CornerGrid)]
    public void FullGrid_ConservesCharge_AndIsMirrorSymmetric(NetworkTopology topology)
    {
        const int S = 12;
        var net = ChargeDivisionNetwork.Build(new ChargeNetworkConfig { Topology = topology }, S, S);
        Assert.True(net.MaxChargeImbalance < RoundOff, $"imbalance {net.MaxChargeImbalance}");
        Assert.True(net.MaxKirchhoffResidual < RoundOff, $"residual {net.MaxKirchhoffResidual}");
        for (int ky = 0; ky < S; ky++)
            for (int kx = 0; kx < S; kx++)
            {
                int k = ky * S + kx, mx = ky * S + (S - 1 - kx), my = (S - 1 - ky) * S + kx;
                Assert.Equal(net[k, 0], net[mx, 1], RoundOff);                // x mirror swaps x− and x+ outputs
                Assert.Equal(net[k, 2], net[mx, 3], RoundOff);
                Assert.Equal(net[k, 0], net[my, 2], RoundOff);                // y mirror swaps y− and y+ outputs
                for (int c = 0; c < 4; c++) Assert.True(net[k, c] >= -RoundOff);
            }
    }

    [Fact]
    public void FiniteInputImpedance_StillConservesCharge()
    {
        var net = ChargeDivisionNetwork.Dpc(5, 5, 1000, 100, inputOhm: 50);
        Assert.True(net.MaxChargeImbalance < RoundOff);
        var ideal = ChargeDivisionNetwork.Dpc(5, 5, 1000, 100);
        Assert.NotEqual(ideal[0, 0], net[0, 0], 6);                            // the load changes the division
    }

    /// <summary>RD-10: a solved circuit exports as a SPICE netlist with one current source per SiPM, every resistor (values
    /// round-trip exact) and a 0 V ammeter (virtual ground) or a load per output; the analytic divider has no circuit.</summary>
    [Fact]
    public void SolvedCircuit_ExportsAsSpice_IdealDividerHasNone()
    {
        var net = ChargeDivisionNetwork.Dpc(3, 2, 1000, 100);
        var cards = net.Circuit!.ToSpice("test").Split('\n').Where(l => l.Length > 0 && !l.StartsWith('*')).ToArray();
        Assert.Equal(6, cards.Count(c => c.StartsWith("I_S")));
        Assert.Equal(net.Circuit.Resistors.Length, cards.Count(c => c.StartsWith('R')));
        Assert.Equal(["V_A OUT_A 0 DC 0", "V_B OUT_B 0 DC 0", "V_C OUT_C 0 DC 0", "V_D OUT_D 0 DC 0"], cards.Where(c => c.StartsWith("V_")));
        Assert.Contains("I_S4 0 S1_1 DC 0", cards);
        Assert.Equal(".end", cards[^1]);
        var loaded = ChargeDivisionNetwork.CornerGrid(2, 2, 1000, 1000, inputOhm: 50).Circuit!.ToSpice("t");
        Assert.Contains("R_LOAD_A OUT_A 0 50", loaded);
        Assert.Null(ChargeDivisionNetwork.Bilinear(4, 4).Circuit);
    }

    [Fact]
    public void NodeWithoutPathToAnOutput_IsRefused()
        => Assert.Throws<ArgumentException>(() => ChargeDivisionNetwork.Solve(3, [(0, 2, 100.0)], [1], [2]));
}
