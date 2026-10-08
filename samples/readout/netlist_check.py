"""TODO-19 RD-10: solve an exported charge-division netlist independently of the engine and compare (stdlib only).

    python samples/readout/netlist_check.py <prefix> [--write]

<prefix>.cir is the SPICE netlist and <prefix>.config.json the engine-resolved configuration with the engine's DC charge
fractions ("Weights"), both written by `montecarlo readout-export`. This script never calls the engine: it parses the
netlist (R, I and 0 V V elements), builds the nodal conductance matrix G (outputs on 0 V sources are fixed at ground,
load resistors stay in G), inverts G by Gaussian elimination with partial pivoting — a different algorithm from the
engine's Cholesky solve — and takes each SiPM's output currents for a 1 A injection (its column of G^-1).

Bound (normwise forward error of a backward-stable solve, Higham, Accuracy and Stability of Numerical Algorithms, 2nd ed.,
Thm 7.2 with the growth factor of a diagonally dominant matrix <= 2): each solver's node voltages satisfy
||dv||_inf <= k / (1 - k) * ||v||_inf with k = 2 * gamma(3n) * kappa_inf(G), gamma(m) = m*u / (1 - m*u), u = 2^-53. An
output current is a sum over at most d branch conductances, so its error is <= ||g_c||_1 * ||dv||_inf plus the rounding of
that sum, gamma(d) * ||g_c||_1 * ||v||_inf. Two independent solvers: the difference is bounded by twice that. The fractions
must agree within this bound, for every SiPM and output. --write stores <prefix>.comparison.json (deterministic: the same
inputs give byte-identical output on any IEEE-754 machine).
"""
import argparse
import hashlib
import json
import pathlib
import sys

U = 2.0 ** -53


def gamma(m):
    return m * U / (1 - m * U)


def parse(text):
    """Elements of a SPICE netlist: (kind, name, node+, node-, value). Only R, I and V cards; '*' comments; '.end'."""
    out = []
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith('*'):
            continue
        if line.lower().startswith('.end'):
            break
        if line.startswith('.'):
            continue
        parts = line.split()
        kind = parts[0][0].upper()
        if kind == 'R':
            out.append(('R', parts[0], parts[1], parts[2], float(parts[3])))
        elif kind in ('I', 'V'):
            value = float(parts[4] if len(parts) > 4 and parts[3].upper() == 'DC' else parts[3])
            out.append((kind, parts[0], parts[1], parts[2], value))
        else:
            raise ValueError(f'unsupported card: {line}')
    return out


def lu_inverse(a):
    """Inverse of a dense matrix by Gaussian elimination with partial pivoting (Doolittle, row interchanges)."""
    n = len(a)
    m = [row[:] + [1.0 if i == j else 0.0 for j in range(n)] for i, row in enumerate(a)]
    for col in range(n):
        piv = max(range(col, n), key=lambda r: abs(m[r][col]))
        if m[piv][col] == 0.0:
            raise ValueError('singular conductance matrix (a node without a path to an output)')
        m[col], m[piv] = m[piv], m[col]
        pivot_row = m[col]
        inv = 1.0 / pivot_row[col]
        for r in range(col + 1, n):
            row = m[r]
            f = row[col] * inv
            if f != 0.0:
                for j in range(col, 2 * n):
                    row[j] -= f * pivot_row[j]
    for col in range(n - 1, -1, -1):
        row = m[col]
        d = row[col]
        for j in range(n, 2 * n):
            row[j] /= d
        row[col] = 1.0
        for r in range(col):
            other = m[r]
            f = other[col]
            if f != 0.0:
                for j in range(n, 2 * n):
                    other[j] -= f * row[j]
                other[col] = 0.0
    return [row[n:] for row in m]


def solve(text):
    """Fractions[k][c] of SiPM k's charge reaching output c (A..D), plus conditioning data for the bound."""
    elements = parse(text)
    fixed = {e[2] for e in elements if e[0] == 'V' and e[3] == '0' and e[4] == 0.0}
    fixed |= {e[3] for e in elements if e[0] == 'V' and e[2] == '0' and e[4] == 0.0}
    nodes = sorted({n for e in elements for n in e[2:4]} - {'0'} - fixed)
    index = {n: i for i, n in enumerate(nodes)}
    size = len(nodes)
    g = [[0.0] * size for _ in range(size)]
    for kind, _, a, b, value in elements:
        if kind != 'R':
            continue
        c = 1.0 / value
        ia, ib = index.get(a), index.get(b)
        if ia is not None:
            g[ia][ia] += c
        if ib is not None:
            g[ib][ib] += c
        if ia is not None and ib is not None:
            g[ia][ib] -= c
            g[ib][ia] -= c
    ginv = lu_inverse(g)
    sources = sorted(((int(e[1][3:]), e[3] if e[2] == '0' else e[2]) for e in elements if e[0] == 'I' and e[1].startswith('I_S')))
    outputs = []
    for e in elements:
        if e[0] == 'V' and e[1].startswith('V_'):
            node = e[2] if e[3] == '0' else e[3]
            branches = [(x[3] if x[2] == node else x[2], 1.0 / x[4]) for x in elements
                        if x[0] == 'R' and node in (x[2], x[3])]
            outputs.append((e[1][2:], node, branches, None))
        elif e[0] == 'R' and e[1].startswith('R_LOAD_'):
            node = e[2] if e[3] == '0' else e[3]
            outputs.append((e[1][7:], node, [], 1.0 / e[4]))
    outputs.sort()
    fractions = []
    vmax = 0.0
    for k, node in sources:
        col = index[node]
        v = [ginv[i][col] for i in range(size)]
        vmax = max(vmax, max(abs(x) for x in v))
        row = []
        for _, onode, branches, load in outputs:
            if load is None:
                row.append(sum(c * (v[index[m]] if m in index else 0.0) for m, c in branches))
            else:
                row.append(load * v[index[onode]])
        fractions.append(row)
    norm_g = max(sum(abs(x) for x in r) for r in g)
    norm_ginv = max(sum(abs(x) for x in r) for r in ginv)
    gout = max(sum(c for _, c in b) if l is None else l for _, _, b, l in outputs)
    degree = max(len(b) if l is None else 1 for _, _, b, l in outputs)
    return {'Fractions': fractions, 'Outputs': [o[0] for o in outputs], 'Unknowns': size,
            'KappaInf': norm_g * norm_ginv, 'MaxVoltagePerAmp': vmax, 'MaxOutputConductance': gout, 'MaxDegree': degree}


def bound(result):
    k = 2 * gamma(3 * result['Unknowns']) * result['KappaInf']
    if not k < 0.5:
        raise ValueError('conductance matrix too ill-conditioned for the bound')
    per_solver = result['MaxOutputConductance'] * result['MaxVoltagePerAmp'] * (k / (1 - k) + gamma(result['MaxDegree']))
    return 2 * per_solver


def compare(prefix):
    prefix = pathlib.Path(prefix)
    cir = prefix.with_name(prefix.name + '.cir').read_bytes()
    cfg_bytes = prefix.with_name(prefix.name + '.config.json').read_bytes()
    engine = json.loads(cfg_bytes)['Weights']
    result = solve(cir.decode('utf-8'))
    if len(engine) != len(result['Fractions']) or result['Outputs'] != ['A', 'B', 'C', 'D']:
        raise ValueError('netlist and engine configuration disagree in size or outputs')
    deviation = max(abs(a - b) for er, pr in zip(engine, result['Fractions']) for a, b in zip(er, pr))
    imbalance = max(abs(sum(row) - 1.0) for row in result['Fractions'])
    b = bound(result)
    return {'SchemaVersion': 1, 'Netlist': prefix.name + '.cir', 'NetlistSha256': hashlib.sha256(cir).hexdigest(),
            'Config': prefix.name + '.config.json', 'ConfigSha256': hashlib.sha256(cfg_bytes).hexdigest(),
            'SiPMs': len(engine), 'Unknowns': result['Unknowns'], 'KappaInf': result['KappaInf'],
            'MaxVoltagePerAmp': result['MaxVoltagePerAmp'], 'MaxOutputConductance': result['MaxOutputConductance'],
            'Bound': b, 'MaxDeviation': deviation, 'MaxChargeImbalance': imbalance,
            'Pass': deviation <= b and imbalance <= b}


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('prefix', help='path prefix of <prefix>.cir and <prefix>.config.json')
    ap.add_argument('--write', action='store_true', help='write <prefix>.comparison.json')
    args = ap.parse_args()
    report = compare(args.prefix)
    text = json.dumps(report, indent=1) + '\n'
    if args.write:
        pathlib.Path(args.prefix + '.comparison.json').write_bytes(text.encode('utf-8'))
    print(f"{report['Netlist']}: max deviation {report['MaxDeviation']:.3e} <= bound {report['Bound']:.3e}: {report['Pass']}")
    return 0 if report['Pass'] else 1


if __name__ == '__main__':
    sys.exit(main())
