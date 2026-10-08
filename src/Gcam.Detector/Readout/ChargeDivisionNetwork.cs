using Gcam.Configuration;

namespace Gcam.Detector;

/// <summary>
/// DC charge fractions of a resistive charge-division network: <c>Weights[k, c]</c> is the fraction of the charge
/// injected by sensor k that leaves through output c. Circuits are solved by Kirchhoff's current law (nodal analysis on
/// the conductance Laplacian; outputs are virtual grounds, or reach ground through a finite input impedance), so
/// linearity, edge compression or distortion EMERGE from the resistor graph — none is inserted. Output order for the
/// four-output topologies: 0 = (x−, y−), 1 = (x+, y−), 2 = (x−, y+), 3 = (x+, y+).
/// </summary>
public sealed class ChargeDivisionNetwork
{
    private readonly double[] _w;

    public int Inputs { get; }
    public int Outputs { get; }

    /// <summary>Largest |Σ_c W[k,c] − 1| over inputs: the charge not accounted for by the outputs (0 for a lossless
    /// network up to round-off).</summary>
    public double MaxChargeImbalance { get; }

    /// <summary>Largest nodal Kirchhoff residual |G·V − I| of the solved circuits (0 for the analytic topologies).</summary>
    public double MaxKirchhoffResidual { get; }

    /// <summary>The solved circuit (null for the analytic topologies): node names, resistors, input / output nodes and
    /// the output input impedance — kept so the same network can be exported as a netlist (RD-10).</summary>
    public ChargeNetworkCircuit? Circuit { get; private init; }

    private ChargeDivisionNetwork(int inputs, int outputs, double[] w, double residual)
    {
        Inputs = inputs;
        Outputs = outputs;
        _w = w;
        MaxKirchhoffResidual = residual;
        double imbalance = 0;
        for (int k = 0; k < inputs; k++)
        {
            double s = 0;
            for (int c = 0; c < outputs; c++) s += w[k * outputs + c];
            imbalance = Math.Max(imbalance, Math.Abs(s - 1));
        }
        MaxChargeImbalance = imbalance;
    }

    public double this[int input, int output] => _w[input * Outputs + output];

    /// <summary>The row of weights of one input (length <see cref="Outputs"/>).</summary>
    public ReadOnlySpan<double> Row(int input) => new(_w, input * Outputs, Outputs);

    /// <summary>One channel per sensor (no division): the IndependentSipm readout.</summary>
    public static ChargeDivisionNetwork Identity(int sensors)
    {
        var w = new double[sensors * sensors];
        for (int k = 0; k < sensors; k++) w[k * sensors + k] = 1;
        return new ChargeDivisionNetwork(sensors, sensors, w, 0);
    }

    /// <summary>The four-output network of <paramref name="config"/> for an Sx × Sy sensor grid.</summary>
    public static ChargeDivisionNetwork Build(ChargeNetworkConfig config, int sx, int sy)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (sx < 1 || sy < 1) throw new ArgumentOutOfRangeException(nameof(sx));
        return config.Topology switch
        {
            NetworkTopology.IdealBilinear => Bilinear(sx, sy),
            NetworkTopology.Dpc => Dpc(sx, sy, config.RowResistanceOhm, config.ColumnResistanceOhm, config.InputImpedanceOhm),
            NetworkTopology.CornerGrid => CornerGrid(sx, sy, config.GridResistanceOhm, config.DrainResistanceOhm, config.InputImpedanceOhm),
            _ => throw new ArgumentOutOfRangeException(nameof(config)),
        };
    }

    /// <summary>Ideal separable divider: u = (kx + ½)/Sx, v = (ky + ½)/Sy; weights (1−u)(1−v), u(1−v), (1−u)v, uv.</summary>
    public static ChargeDivisionNetwork Bilinear(int sx, int sy)
    {
        var w = new double[sx * sy * 4];
        for (int ky = 0; ky < sy; ky++)
            for (int kx = 0; kx < sx; kx++)
            {
                double u = (kx + 0.5) / sx, v = (ky + 0.5) / sy;
                int k = (ky * sx + kx) * 4;
                w[k] = (1 - u) * (1 - v); w[k + 1] = u * (1 - v); w[k + 2] = (1 - u) * v; w[k + 3] = u * v;
            }
        return new ChargeDivisionNetwork(sx * sy, 4, w, 0);
    }

    /// <summary>Discretised positioning circuit: row chain L_r — R — n(0,r) — R — … — n(Sx−1,r) — R — R_r for every row r;
    /// left column out0 — Rc — L_0 — Rc — … — L_(Sy−1) — Rc — out2, right column out1 — Rc — R_0 — … — R_(Sy−1) — Rc — out3.</summary>
    public static ChargeDivisionNetwork Dpc(int sx, int sy, double rowOhm, double columnOhm, double inputOhm = 0)
    {
        if (!(rowOhm > 0) || !(columnOhm > 0)) throw new ArgumentOutOfRangeException(nameof(rowOhm));
        int sensors = sx * sy;
        int Left(int r) => sensors + r;
        int Right(int r) => sensors + sy + r;
        int outBase = sensors + 2 * sy;
        var res = new List<(int, int, double)>();
        for (int r = 0; r < sy; r++)
        {
            res.Add((Left(r), r * sx, rowOhm));
            for (int k = 0; k + 1 < sx; k++) res.Add((r * sx + k, r * sx + k + 1, rowOhm));
            res.Add((r * sx + sx - 1, Right(r), rowOhm));
        }
        res.Add((outBase + 0, Left(0), columnOhm));
        res.Add((outBase + 1, Right(0), columnOhm));
        for (int r = 0; r + 1 < sy; r++)
        {
            res.Add((Left(r), Left(r + 1), columnOhm));
            res.Add((Right(r), Right(r + 1), columnOhm));
        }
        res.Add((Left(sy - 1), outBase + 2, columnOhm));
        res.Add((Right(sy - 1), outBase + 3, columnOhm));
        var names = new string[outBase + 4];
        for (int k = 0; k < sensors; k++) names[k] = $"S{k % sx}_{k / sx}";
        for (int r = 0; r < sy; r++) { names[Left(r)] = $"L{r}"; names[Right(r)] = $"R{r}"; }
        for (int c = 0; c < 4; c++) names[outBase + c] = "OUT_" + "ABCD"[c];
        return Solve(outBase + 4, res, Enumerable.Range(0, sensors).ToArray(),
            [outBase, outBase + 1, outBase + 2, outBase + 3], inputOhm, names);
    }

    /// <summary>Uniform grid: every sensor node joined to its 4 neighbours by R; corner nodes drained to the four outputs.</summary>
    public static ChargeDivisionNetwork CornerGrid(int sx, int sy, double gridOhm, double drainOhm, double inputOhm = 0)
    {
        if (!(gridOhm > 0) || !(drainOhm > 0)) throw new ArgumentOutOfRangeException(nameof(gridOhm));
        int sensors = sx * sy, outBase = sensors;
        var res = new List<(int, int, double)>();
        for (int ky = 0; ky < sy; ky++)
            for (int kx = 0; kx < sx; kx++)
            {
                int k = ky * sx + kx;
                if (kx + 1 < sx) res.Add((k, k + 1, gridOhm));
                if (ky + 1 < sy) res.Add((k, k + sx, gridOhm));
            }
        res.Add((0, outBase + 0, drainOhm));
        res.Add((sx - 1, outBase + 1, drainOhm));
        res.Add(((sy - 1) * sx, outBase + 2, drainOhm));
        res.Add((sensors - 1, outBase + 3, drainOhm));
        var names = new string[outBase + 4];
        for (int k = 0; k < sensors; k++) names[k] = $"S{k % sx}_{k / sx}";
        for (int c = 0; c < 4; c++) names[outBase + c] = "OUT_" + "ABCD"[c];
        return Solve(outBase + 4, res, Enumerable.Range(0, sensors).ToArray(),
            [outBase, outBase + 1, outBase + 2, outBase + 3], inputOhm, names);
    }

    /// <summary>Nodal analysis of an arbitrary resistor graph. A resistor endpoint −1 is ground. Each output node is a
    /// virtual ground (<paramref name="inputOhm"/> = 0) or reaches ground through <paramref name="inputOhm"/>. Unit current
    /// is injected at each input node in turn; the output currents are that input's weights. A node with no conducting
    /// path to an output makes the Laplacian singular and is refused.</summary>
    public static ChargeDivisionNetwork Solve(int nodes, IReadOnlyList<(int A, int B, double Ohm)> resistors,
        int[] inputNodes, int[] outputNodes, double inputOhm = 0, string[]? nodeNames = null)
    {
        ArgumentNullException.ThrowIfNull(resistors);
        if (!(inputOhm >= 0) || !double.IsFinite(inputOhm)) throw new ArgumentOutOfRangeException(nameof(inputOhm));
        bool grounded = inputOhm == 0;
        var isOutput = new bool[nodes];
        foreach (int o in outputNodes) isOutput[o] = true;
        var index = new int[nodes];
        int m = 0;
        for (int n = 0; n < nodes; n++) index[n] = grounded && isOutput[n] ? -1 : m++;
        var g = new double[m, m];
        void Stamp(int a, int b, double cond)
        {
            int ia = a < 0 ? -1 : index[a], ib = b < 0 ? -1 : index[b];
            if (ia >= 0) g[ia, ia] += cond;
            if (ib >= 0) g[ib, ib] += cond;
            if (ia >= 0 && ib >= 0) { g[ia, ib] -= cond; g[ib, ia] -= cond; }
        }
        foreach (var (a, b, ohm) in resistors)
        {
            if (!(ohm > 0) || !double.IsFinite(ohm)) throw new ArgumentOutOfRangeException(nameof(resistors));
            Stamp(a, b, 1 / ohm);
        }
        if (!grounded) foreach (int o in outputNodes) Stamp(o, -1, 1 / inputOhm);
        var chol = Cholesky(g, m);
        var w = new double[inputNodes.Length * outputNodes.Length];
        double residual = 0;
        var v = new double[m];
        for (int k = 0; k < inputNodes.Length; k++)
        {
            Array.Clear(v);
            v[index[inputNodes[k]]] = 1;
            SolveInPlace(chol, m, v);
            for (int i = 0; i < m; i++)
            {
                double gi = 0;
                for (int j = 0; j < m; j++) gi += g[i, j] * v[j];
                residual = Math.Max(residual, Math.Abs(gi - (i == index[inputNodes[k]] ? 1 : 0)));
            }
            double Volt(int n) => n < 0 || index[n] < 0 ? 0 : v[index[n]];
            for (int c = 0; c < outputNodes.Length; c++)
            {
                int o = outputNodes[c];
                double current = 0;
                if (grounded)
                    foreach (var (a, b, ohm) in resistors)
                    {
                        if (a == o) current += (Volt(b) - 0) / ohm;
                        else if (b == o) current += (Volt(a) - 0) / ohm;
                    }
                else current = Volt(o) / inputOhm;
                w[k * outputNodes.Length + c] = current;
            }
        }
        var names = nodeNames ?? Enumerable.Range(0, nodes).Select(n => $"N{n}").ToArray();
        return new ChargeDivisionNetwork(inputNodes.Length, outputNodes.Length, w, residual)
        {
            Circuit = new ChargeNetworkCircuit(names, resistors.ToArray(), inputNodes.ToArray(), outputNodes.ToArray(), inputOhm),
        };
    }

    private static double[,] Cholesky(double[,] a, int n)
    {
        var l = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j <= i; j++)
            {
                double s = a[i, j];
                for (int k = 0; k < j; k++) s -= l[i, k] * l[j, k];
                if (i == j)
                {
                    if (!(s > 1e-12 * Math.Max(1, Math.Abs(a[i, i]))))
                        throw new ArgumentException("Network has a node without a conducting path to an output (singular Laplacian).");
                    l[i, i] = Math.Sqrt(s);
                }
                else l[i, j] = s / l[j, j];
            }
        return l;
    }

    private static void SolveInPlace(double[,] l, int n, double[] b)
    {
        for (int i = 0; i < n; i++)
        {
            double s = b[i];
            for (int k = 0; k < i; k++) s -= l[i, k] * b[k];
            b[i] = s / l[i, i];
        }
        for (int i = n - 1; i >= 0; i--)
        {
            double s = b[i];
            for (int k = i + 1; k < n; k++) s -= l[k, i] * b[k];
            b[i] = s / l[i, i];
        }
    }
}
