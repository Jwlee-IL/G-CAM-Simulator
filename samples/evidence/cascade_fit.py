"""Pooled Poisson fit of the Co-60 cascade sum-peak yield against the single-photopeak yield (TODO-27, ER-8).

`montecarlo cascade samples/scenario_co60.json` writes, per source distance, the single-photopeak and the
coincidence-sum yields per decay from 8,000,000 decays. The CLI's own log-log slope drops every distance whose sum
count is zero, so a per-seed slope is censored and noisy. This script fits ONE model to every (seed, distance) row,
zero rows included:

    sum_count[s, d] ~ Poisson( decays * exp(a + b * ln single_per_decay[s, d]) )

by iteratively re-weighted least squares (Poisson GLM, log link, offset ln decays). The single yield is measured from
~10^3–10^4 events per row, so it is used as an exact covariate. The fitted b is compared with the slope the
geometry implies (see `geometric_expectation`), not with an assumed 2.

Usage:
    python samples/evidence/cascade_fit.py --seeds O128 --runs <run-root> [<run-root> ...] [--out cascade_fit.json]

A run root is a directory holding `runs/cascade/<seed>/{done.json,samples/cascade.csv}` as written by
`run_seeds.py`. Every declared seed must have a successful run (exit 0) and seven rows, or the script stops.
"""
import argparse
import csv
import json
import math
import pathlib
import random
import sys

import numpy as np

HERE = pathlib.Path(__file__).resolve().parent
DECAYS = 8_000_000                      # IsotopeCommands.RunCascade: decays per distance
DISTANCES = [18.0, 24.0, 32.0, 43.0, 57.0, 76.0, 100.0]  # IsotopeCommands.RunCascade: source distances, mm


def load_rows(roots, seeds):
    """Return {seed: [(distance, single_per_decay, sum_count), ...]}; refuse missing or failed runs."""
    found = {}
    for root in roots:
        base = pathlib.Path(root) / 'runs' / 'cascade'
        if not base.is_dir():
            continue
        for run in base.iterdir():
            done = run / 'done.json'
            if not done.exists() or not run.name.isdigit():
                continue
            seed = int(run.name)
            if seed in found or json.loads(done.read_text(encoding='utf-8-sig'))['exit'] != 0:
                continue
            rows = []
            for r in csv.DictReader((run / 'samples' / 'cascade.csv').open(encoding='utf-8-sig')):
                count = float(r['sum_peak_per_decay']) * DECAYS
                if abs(count - round(count)) > 1e-3:
                    raise SystemExit(f'seed {seed}: sum yield {r["sum_peak_per_decay"]} is not an integer count')
                rows.append((float(r['distance_mm']), float(r['single_photopeak_per_decay']), int(round(count))))
            if [d for d, _, _ in rows] != DISTANCES:
                raise SystemExit(f'seed {seed}: unexpected distance rows {[d for d, _, _ in rows]}')
            found[seed] = rows
    missing = [s for s in seeds if s not in found]
    if missing:
        raise SystemExit(f'{len(missing)} declared seeds have no successful cascade run, e.g. {missing[:5]}')
    return {s: found[s] for s in seeds}


def poisson_fit(x, y, offset):
    """Poisson GLM with log link: returns (a, b, cov, deviance, pearson_chi2)."""
    X = np.column_stack([np.ones_like(x), x])
    beta = np.array([math.log(max(y.sum(), 1) / np.exp(offset).sum()) - 2.0 * float(x.mean()), 2.0])
    for _ in range(100):
        mu = np.exp(X @ beta + offset)
        z = X @ beta + (y - mu) / mu
        W = mu
        new = np.linalg.solve(X.T @ (W[:, None] * X), X.T @ (W * z))
        if np.max(np.abs(new - beta)) < 1e-12:
            beta = new
            break
        beta = new
    mu = np.exp(X @ beta + offset)
    cov = np.linalg.inv(X.T @ (mu[:, None] * X))
    with np.errstate(divide='ignore', invalid='ignore'):
        dev = 2 * np.sum(np.where(y > 0, y * np.log(y / mu), 0.0) - (y - mu))
    pearson = float(np.sum((y - mu) ** 2 / mu))
    return float(beta[0]), float(beta[1]), cov, float(dev), pearson


def geometric_expectation(samples=4_000_000, rng_seed=27):
    """Slope of ln P2 against ln P1 over the CLI's distances, where P1 is the chance that one photon from the source
    hits the detector face and P2 the chance that BOTH Co-60 cascade photons do, with the angular correlation
    W(θ) = 1 + cos²θ/8 + cos⁴θ/24 the engine samples (DecayScheme). Everything else in the CLI's yields — the 0.5
    open-fraction test and the crystal deposit, which the study always takes at normal incidence — does not depend
    on distance, so single ∝ P1 and sum ∝ P2 up to summing-out (bounded below). P1 is the exact solid angle of the
    rectangle; P2 = P1 × P(partner hits | first hits), the conditional estimated by Monte Carlo over first photons
    drawn uniformly on the face-hitting directions."""
    half = 0.5 * 1.0 * 7 * 2   # IsotopeCommands: 0.5 × pixel pitch × rank × mosaic = 7 mm (scenario_co60.json)
    a2, a4 = 1 / 8, 1 / 24
    rng = np.random.default_rng(rng_seed)
    g1 = 1 + a2 / 3 + a4 / 5

    def partner_cos(u):
        target = (2 * u - 1) * g1
        c = target / g1
        for _ in range(60):
            c2 = c * c
            g = c * (1 + c2 * (a2 / 3 + c2 * a4 / 5)) - target
            c = np.clip(c - g / (1 + c2 * (a2 + c2 * a4)), -1, 1)
        return c

    out = []
    for d in DISTANCES:
        p1 = 4 * math.asin(half * half / math.sqrt((half * half + d * d) * (half * half + d * d))) / (4 * math.pi)
        # first photon: uniform direction among those that hit the face (rejection from the cone that contains it)
        cmax = d / math.sqrt(2 * half * half + d * d)
        dirs = []
        need = samples
        while need > 0:
            n = int(need * 1.6) + 1000
            cz = rng.uniform(cmax, 1.0, n)
            phi = rng.uniform(0, 2 * math.pi, n)
            s = np.sqrt(1 - cz * cz)
            v = np.column_stack([s * np.cos(phi), s * np.sin(phi), cz])
            t = d / v[:, 2]
            ok = (np.abs(t * v[:, 0]) <= half) & (np.abs(t * v[:, 1]) <= half)
            dirs.append(v[ok][:need])
            need -= int(ok.sum())
        u = np.concatenate(dirs)[:samples]
        c = partner_cos(rng.uniform(0, 1, samples))
        phi = rng.uniform(0, 2 * math.pi, samples)
        helper = np.where(np.abs(u[:, :1]) < 0.9, np.array([[1.0, 0, 0]]), np.array([[0, 1.0, 0]]))
        e1 = np.cross(u, helper)
        e1 /= np.linalg.norm(e1, axis=1)[:, None]
        e2 = np.cross(u, e1)
        s = np.sqrt(np.maximum(0, 1 - c * c))[:, None]
        w = c[:, None] * u + s * (np.cos(phi)[:, None] * e1 + np.sin(phi)[:, None] * e2)
        toward = w[:, 2] > 0
        t = np.where(toward, d / np.where(toward, w[:, 2], 1), 0)
        hit = toward & (np.abs(t * w[:, 0]) <= half) & (np.abs(t * w[:, 1]) <= half)
        cond = float(hit.mean())
        out.append({'distance_mm': d, 'P1': p1, 'partner_given_first': cond, 'P2': p1 * cond})
    lx = np.log([r['P1'] for r in out])
    ly = np.log([r['P2'] for r in out])
    slope = float(np.polyfit(lx, ly, 1)[0])
    # Summing-out: a single-line photopeak event is lost when the partner also deposits; that needs the partner to
    # hit the face (partner_given_first), pass the 0.5 open-fraction test and interact (≤ 1). Upper bound on the loss:
    loss = [0.5 * r['partner_given_first'] for r in out]
    lx_lo = lx + np.log(1 - np.array(loss))
    slope_with_max_loss = float(np.polyfit(lx_lo, ly, 1)[0])
    return {'half_width_mm': half, 'rows': out, 'slope_lnP2_vs_lnP1': slope,
            'slope_if_summing_out_at_its_upper_bound': slope_with_max_loss,
            'summing_out_upper_bound_per_distance': loss, 'mc_first_photons_per_distance': samples,
            'numpy_seed': rng_seed}


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--seeds', default='O128', help='seed list name in seeds.json')
    ap.add_argument('--runs', nargs='+', required=True, help='run roots (directories holding runs/cascade/<seed>)')
    ap.add_argument('--bootstrap', type=int, default=2000)
    ap.add_argument('--out', default=None)
    args = ap.parse_args()
    seeds = json.loads((HERE / 'seeds.json').read_text())[args.seeds]
    data = load_rows(args.runs, seeds)

    def arrays(sample):
        x, y = [], []
        for s in sample:
            for _, single, count in data[s]:
                x.append(math.log(single))
                y.append(count)
        x = np.array(x)
        return x, np.array(y, dtype=float), np.full_like(x, math.log(DECAYS))

    x, y, off = arrays(seeds)
    a, b, cov, dev, pearson = poisson_fit(x, y, off)
    rows = len(y)
    per_distance = []
    for i, d in enumerate(DISTANCES):
        counts = [data[s][i][2] for s in seeds]
        per_distance.append({'distance_mm': d, 'sum_counts_total': int(sum(counts)),
                             'zero_rows': sum(1 for c in counts if c == 0),
                             'mean_single_per_decay': float(np.mean([data[s][i][1] for s in seeds]))})
    # Same model on the per-distance pooled counts (a check: the covariate barely varies between seeds).
    px = np.array([math.log(r['mean_single_per_decay']) for r in per_distance])
    py = np.array([r['sum_counts_total'] for r in per_distance], dtype=float)
    pa, pb, pcov, pdev, ppearson = poisson_fit(px, py, np.full_like(px, math.log(DECAYS * len(seeds))))
    rng = random.Random(27)
    boot = []
    for _ in range(args.bootstrap):
        bx, by, boff = arrays(rng.choices(seeds, k=len(seeds)))
        boot.append(poisson_fit(bx, by, boff)[1])
    boot.sort()
    geo = geometric_expectation()
    result = {
        'estimand': 'Poisson GLM, log link: sum_count ~ Poisson(decays * exp(a + b ln single_per_decay)), all '
                    '(seed, distance) rows including zero sum counts',
        'seeds': args.seeds, 'N_seeds': len(seeds), 'rows': rows, 'zero_rows': int((y == 0).sum()),
        'decays_per_row': DECAYS, 'slope': b, 'slope_se_wald': math.sqrt(cov[1, 1]), 'intercept': a,
        'deviance': dev, 'pearson_chi2': pearson, 'dof': rows - 2, 'dispersion_pearson': pearson / (rows - 2),
        'pooled_by_distance': {'slope': pb, 'slope_se_wald': math.sqrt(pcov[1, 1]), 'deviance': pdev,
                               'pearson_chi2': ppearson, 'dof': len(px) - 2, 'rows': per_distance},
        'seed_bootstrap': {'replicates': args.bootstrap, 'stdlib_seed': 27, 'sd': float(np.std(boot, ddof=1)),
                           'q025': boot[int(0.025 * len(boot))], 'q975': boot[int(0.975 * len(boot)) - 1]},
        'geometric_expectation': geo,
    }
    result['z_vs_geometric'] = (b - geo['slope_lnP2_vs_lnP1']) / result['slope_se_wald']
    text = json.dumps(result, indent=1)
    if args.out:
        pathlib.Path(args.out).write_text(text + '\n', encoding='utf-8')
    print(json.dumps({k: result[k] for k in ['N_seeds', 'rows', 'zero_rows', 'slope', 'slope_se_wald',
                                             'dispersion_pearson', 'z_vs_geometric']}, indent=1))
    print('pooled-by-distance slope', pb, '+-', math.sqrt(pcov[1, 1]))
    print('bootstrap', result['seed_bootstrap'])
    print('geometric slope', geo['slope_lnP2_vs_lnP1'], 'with max summing-out', geo['slope_if_summing_out_at_its_upper_bound'])


if __name__ == '__main__':
    sys.exit(main())
