namespace Gcam.Studio.Core.Services;

/// <summary>The resolved engine resistor graph, detached from its mutable export arrays.</summary>
public sealed record ReadoutCircuit(IReadOnlyList<string> Nodes,
    IReadOnlyList<(int A, int B, double Ohm)> Resistors, double InputOhm);
