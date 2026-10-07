"""TODO-35 (DA-5): select the stripped-statistic thresholds Z_s* per configuration on the csco selection seeds only.

    python samples/evidence/ambient/select_csco_thresholds.py --runs <driver-out> --family csco_selection
        --manifest samples/evidence/manifest-csco-v1.json --out samples/evidence/ambient/csco-thresholds-v1.json

The rule is AB-11's inclusive rule unchanged (select_thresholds.RULE_INCLUSIVE, ALPHA = 0.003): for every Cs-free null
configuration (scene, live time, environment, Co-60 activity) pool the maximum stripped studentised correlation Z_s,
recorded to 4 decimals, of all selection acquisitions (n = seeds x null repeats), k = floor(ALPHA * n); "trusted" =
recorded Z_s >= T*; T* is the smallest recorded value v with #(Z_s >= v) <= k. The validation phase applies the file
through AmbientGateStudy.Thresholds (Comparison RoundedAtLeast) on seeds disjoint from these.
"""
import argparse
import hashlib
import json
import math
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
import provenance as pv

from gate_stats import cp_upper, family_seeds
from select_thresholds import ALPHA, RULE_INCLUSIVE, inclusive_threshold

HERE = pathlib.Path(__file__).resolve().parent


def load(runs_dir, family, seeds):
    out = {}
    for seed in seeds:
        run = pathlib.Path(runs_dir) / 'runs' / family / str(seed)
        done = run / 'done.json'
        if not done.exists():
            raise SystemExit(f'{family} seed {seed}: missing run')
        if json.loads(done.read_text(encoding='utf-8'))['exit'] != 0:
            raise SystemExit(f'{family} seed {seed}: failed run')
        out[seed] = run / 'ambient-evidence.json'
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--runs', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--manifest', default=str(HERE.parent / 'manifest-csco-v1.json'))
    ap.add_argument('--family', default='csco_selection')
    args = ap.parse_args()
    seeds, fam = family_seeds(args.manifest, HERE.parent / 'seeds.json', args.family)
    prov = pv.summary_provenance([pv.collect(args.runs, args.family, seeds)], __file__, args)
    files = load(args.runs, args.family, seeds)
    pooled, digest = {}, hashlib.sha256()
    for seed in seeds:
        data = files[seed].read_bytes()
        digest.update(hashlib.sha256(data).digest())
        run = json.loads(data)
        if run.get('Family') != 'csco' or run.get('Phase') != 'selection':
            raise SystemExit(f'seed {seed}: not a csco selection run')
        for key, values in run['Nulls'].items():
            pooled.setdefault(key, []).extend(values)
    per, report = {}, {}
    for key in sorted(pooled):
        values = pooled[key]
        n = len(values)
        k = math.floor(ALPHA * n)
        ordered = sorted((v for v in values if v is not None), reverse=True)
        threshold = inclusive_threshold(ordered, k)
        exceed = sum(1 for v in ordered if v >= threshold)
        if exceed > k:
            raise SystemExit(f'{key}: selection exceeds its allowance ({exceed} > {k})')
        per[key] = threshold
        report[key] = {'Acquisitions': n, 'WithCandidate': len(ordered), 'AllowedExceedances': k,
                       'SelectionExceedances': exceed, 'SelectionRate': exceed / n, 'SelectionUpper95': cp_upper(exceed, n),
                       'Threshold': threshold, 'TiedAtThreshold': sum(1 for v in ordered if v == threshold),
                       'DistinctValues': len(set(ordered)), 'MedianZ': ordered[len(ordered) // 2] if ordered else None}
    out = {'_about': 'TODO-35 DA-5: stripped-statistic (Z_s, per-pixel R_i, product reference window) thresholds, '
                     'AB-11 inclusive rule on the csco selection seeds only.',
           'Rule': RULE_INCLUSIVE.replace('background-only configuration (case, exposure, field, bound, window)',
                                          'Cs-free null configuration (scene, exposure, environment, Co-60 activity)'),
           'Alpha': ALPHA, 'Family': args.family, 'SeedList': fam['seeds'], 'SeedOffset': fam.get('seed_offset', 0),
           'Seeds': seeds, 'RunsDigest': digest.hexdigest(), 'PerConfiguration': per, 'Universal': max(per.values()),
           'Comparison': 'RoundedAtLeast', 'NullsPerConfiguration': report[next(iter(report))]['Acquisitions'],
           'Selection': report}
    out['Provenance'] = prov
    pv.publish({pathlib.Path(args.out): pv.json_bytes(out)}, prov)
    print(f'{len(per)} configurations; universal (max) {out["Universal"]:.4f}')
    for key in sorted(per):
        r = report[key]
        print(f"{key:60s} n={r['Acquisitions']} cand={r['WithCandidate']} T*={r['Threshold']:.4f} exceed={r['SelectionExceedances']} median={r['MedianZ']}")


if __name__ == '__main__':
    main()
