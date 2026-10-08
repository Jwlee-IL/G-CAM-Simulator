using Gcam.Configuration;
using Gcam.Detector;

namespace Gcam.Tests;

public sealed class ChargeAdjointTests
{
    [Theory]
    [InlineData(false, 4, 5, 0)]
    [InlineData(false, 12, 12, 50)]
    [InlineData(true, 4, 5, 0)]
    [InlineData(true, 12, 12, 50)]
    public void Reciprocity_AgreesWithLegacyAndIndependentPivotedSolve_WithinConditioningBound(
        bool corner, int sx, int sy, double inputOhm)
    {
        ChargeDivisionNetwork Build(ChargeNetworkSolveMethod method) => corner
            ? ChargeDivisionNetwork.CornerGrid(sx, sy, 1000, 1000, inputOhm, method)
            : ChargeDivisionNetwork.Dpc(sx, sy, 1000, 10, inputOhm, method);
        var legacy = Build(ChargeNetworkSolveMethod.Legacy);
        var adjoint = Build(ChargeNetworkSolveMethod.Adjoint);
        var circuit = legacy.Circuit!;
        var inputs = circuit.InputNodes; var outputs = circuit.OutputNodes; var resistors = circuit.Resistors;
        var index = Enumerable.Range(0, circuit.NodeNames.Length).Where(i => inputOhm != 0 || !outputs.Contains(i))
            .Select((node, i) => (node, i)).ToDictionary(p => p.node, p => p.i);
        int n = index.Count;
        var g = new double[n, n]; var rhs = new double[n, 4];
        void Stamp(int a, int b, double conductance)
        {
            bool hasA = index.TryGetValue(a, out int ia), hasB = index.TryGetValue(b, out int ib);
            if (hasA) g[ia, ia] += conductance;
            if (hasB) g[ib, ib] += conductance;
            if (hasA && hasB) { g[ia, ib] -= conductance; g[ib, ia] -= conductance; }
        }
        foreach (var (a, b, ohm) in resistors) Stamp(a, b, 1 / ohm);
        for (int c = 0; c < 4; c++)
            if (inputOhm != 0) { Stamp(outputs[c], -1, 1 / inputOhm); rhs[index[outputs[c]], c] = 1 / inputOhm; }
            else foreach (var (a, b, ohm) in resistors)
            {
                int neighbour = a == outputs[c] ? b : b == outputs[c] ? a : -1;
                if (index.TryGetValue(neighbour, out int i)) rhs[i, c] += 1 / ohm;
            }
        var inverse = Inverse(g);
        double Norm(double[,] a) => Enumerable.Range(0, n).Max(i => Enumerable.Range(0, n).Sum(j => Math.Abs(a[i, j])));
        double giNorm = Norm(inverse), kappa = Norm(g) * giNorm;
        double Gamma(int m) { double mu = m * Math.Pow(2, -53); return mu / (1 - mu); }
        double k = 2 * Gamma(3 * n) * kappa;
        Assert.True(k < .5, "conditioning bound unavailable; never substitute an empirical tolerance");
        double voltage = 0, conductanceNorm = 0, maxAdjoint = 0, delta = 0;
        for (int s = 0; s < inputs.Length; s++)
        {
            for (int i = 0; i < n; i++) voltage = Math.Max(voltage, Math.Abs(inverse[i, index[inputs[s]]]));
            for (int c = 0; c < 4; c++)
            {
                double expected = 0, norm = 0;
                for (int i = 0; i < n; i++) { expected += rhs[i, c] * inverse[i, index[inputs[s]]]; norm += Math.Abs(rhs[i, c]); }
                conductanceNorm = Math.Max(conductanceNorm, norm); maxAdjoint = Math.Max(maxAdjoint, Math.Abs(expected));
                delta = Math.Max(delta, Math.Abs(adjoint[s, c] - expected));
                delta = Math.Max(delta, Math.Abs(adjoint[s, c] - legacy[s, c]));
            }
        }
        // Higham normwise forward bound for both solves, plus RHS conductance sums and current dot products.
        // Legacy current error is ||b||1 ||v||inf f; adjoint weight error is ||z||inf f.
        // A rounded adjoint RHS contributes ||G^-1||inf gamma(n) ||b||1. No observed delta enters the bound.
        double bound = 2 * ((conductanceNorm * voltage + maxAdjoint) * k / (1 - k)
            + Gamma(n) * conductanceNorm * (voltage + giNorm));
        Assert.True(delta <= bound, $"delta={delta:R}, bound={bound:R}, kappa={kappa:R}, unknowns={n}");
        Assert.True(adjoint.MaxChargeImbalance <= 4 * bound);
    }

    private static double[,] Inverse(double[,] matrix)
    {
        int n = matrix.GetLength(0); var augmented = new double[n, 2 * n];
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
        { augmented[i, j] = matrix[i, j]; augmented[i, n + j] = i == j ? 1 : 0; }
        // Gauss-Jordan with partial pivoting, independent of the engine's Cholesky and triangular sweeps.
        for (int c = 0; c < n; c++)
        {
            int pivot = Enumerable.Range(c, n - c).MaxBy(i => Math.Abs(augmented[i, c]));
            for (int j = 0; j < 2 * n; j++) (augmented[c, j], augmented[pivot, j]) = (augmented[pivot, j], augmented[c, j]);
            double scale = augmented[c, c];
            for (int j = 0; j < 2 * n; j++) augmented[c, j] /= scale;
            for (int i = 0; i < n; i++)
            {
                if (i == c) continue;
                double factor = augmented[i, c];
                for (int j = 0; j < 2 * n; j++) augmented[i, j] -= factor * augmented[c, j];
            }
        }
        var result = new double[n, n];
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) result[i, j] = augmented[i, n + j];
        return result;
    }

    [Fact]
    public void NetworkSolve_CancellationDuringFactorization_IsObserved()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.CancelAfter(10);
        Assert.Throws<OperationCanceledException>(() => ChargeDivisionNetwork.Dpc(44, 44, 1000, 10,
            method: ChargeNetworkSolveMethod.Adjoint, cancellationToken: cancellation.Token));
    }
}
