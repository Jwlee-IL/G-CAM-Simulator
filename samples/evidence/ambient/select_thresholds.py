"""AB-7 threshold selection on the selection seeds only (TODO-30 turn 7).

    python samples/evidence/ambient/select_thresholds.py --runs <driver-out> --out samples/evidence/ambient/gate-thresholds-v1.json

Rule, declared before any validation run: for every background-only configuration (case, exposure, field, bound, window)
pool the maximum studentised correlation Z of all selection acquisitions (n = 64 seeds x 64), k = floor(ALPHA * n);
the threshold T* is the (k+1)-th largest value, so at most k selection acquisitions exceed it ("trusted" = Z > T*).
An acquisition without a candidate (no counts) never exceeds. If fewer than k+1 acquisitions have a candidate, any
candidate is trusted (T* = NO_CANDIDATE_FLOOR). ALPHA = 0.003 leaves room under the 1 % target for the validation's
one-sided 95 % Clopper-Pearson upper limit at ~2000 acquisitions. The universal threshold is the largest per-
configuration T* (one rule for every configuration, never lower than any selected one).
"""
import argparse
import hashlib
import json
import math
import pathlib

from gate_stats import family_seeds, load_runs

HERE = pathlib.Path(__file__).resolve().parent
ALPHA = 0.003
NO_CANDIDATE_FLOOR = -1e300   # below every finite Z; an acquisition with no counts has no Z at all


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--runs', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--manifest', default=str(HERE.parent / 'manifest-ambient-v1.json'))
    args = ap.parse_args()
    seeds, fam = family_seeds(args.manifest, HERE.parent / 'seeds.json', 'gate_selection')
    runs = load_runs(args.runs, 'gate_selection', seeds)
    keys = sorted(next(iter(runs.values()))['Nulls'])
    per, report = {}, {}
    for key in keys:
        values = []
        for seed in seeds:
            if runs[seed]['Phase'] != 'selection':
                raise SystemExit(f'seed {seed}: not a selection run')
            values.extend(runs[seed]['Nulls'][key])
        n = len(values)
        k = math.floor(ALPHA * n)
        ordered = sorted((v for v in values if v is not None), reverse=True)
        threshold = ordered[k] if len(ordered) > k else NO_CANDIDATE_FLOOR
        exceed = sum(1 for v in values if v is not None and v > threshold)
        per[key] = threshold
        report[key] = {'Acquisitions': n, 'WithCandidate': len(ordered), 'AllowedExceedances': k,
                       'SelectionExceedances': exceed, 'Threshold': threshold,
                       'MedianZ': ordered[len(ordered) // 2] if ordered else None}
    universal = max(per.values())
    digest = hashlib.sha256()
    for seed in seeds:
        digest.update(hashlib.sha256((pathlib.Path(args.runs) / 'runs' / 'gate_selection' / str(seed) / 'ambient-gate.json').read_bytes()).digest())
    out = {'_about': __doc__.strip().splitlines()[0], 'Rule': __doc__.strip().split('\n\n', 2)[2].replace('\n', ' '),
           'Alpha': ALPHA, 'Family': 'gate_selection', 'SeedList': fam['seeds'], 'SeedOffset': fam.get('seed_offset', 0),
           'Seeds': seeds, 'RunsDigest': digest.hexdigest(), 'PerConfiguration': per, 'Universal': universal,
           'Selection': report}
    pathlib.Path(args.out).write_text(json.dumps(out, indent=1) + '\n', encoding='utf-8', newline='\n')
    print(f'{len(per)} configurations, universal threshold {universal:.4f}')
    for key in keys:
        r = report[key]
        print(f"{key:70s} n={r['Acquisitions']} cand={r['WithCandidate']} T*={r['Threshold']:.3f} exceed={r['SelectionExceedances']}")


if __name__ == '__main__':
    main()
