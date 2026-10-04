"""AB-7 / AB-11 threshold selection on the selection seeds only (TODO-30 turns 7 and 8).

    python samples/evidence/ambient/select_thresholds.py --runs <driver-out> --out samples/evidence/ambient/gate-thresholds-v1.json
    python samples/evidence/ambient/select_thresholds.py --runs <driver-out> --out samples/evidence/ambient/gate-thresholds-v2.json
        --manifest samples/evidence/manifest-ambient-v2.json --family gate_selection_v2 --rule inclusive

Two rules, each declared before its validation run (the strict one reproduces gate-thresholds-v1.json byte for byte;
its text is kept verbatim in RULE_STRICT):
- strict (turn 7, AB-7): T* is the (k+1)-th largest null Z, k = floor(ALPHA * n); "trusted" = Z > T*.
- inclusive (turn 8, AB-11): ties at the threshold count as exceedances; "trusted" = Z rounded to its recorded
  4 decimals >= T*. T* is the smallest recorded value v with #(Z >= v) <= k, so the selection exceedance fraction
  INCLUDING ties is <= ALPHA; if the largest value alone occurs more than k times, T* is one recording step above it.
"""
import argparse
import hashlib
import json
import math
import pathlib
from collections import Counter

from gate_stats import cp_upper, family_seeds, load_runs

HERE = pathlib.Path(__file__).resolve().parent
ALPHA = 0.003
NO_CANDIDATE_FLOOR = -1e300   # below every finite Z; an acquisition with no counts has no Z at all
STEP = 1e-4                   # recording precision of Z (AmbientGateStudy.Thresholds.RecordedDecimals = 4)

ABOUT_STRICT = 'AB-7 threshold selection on the selection seeds only (TODO-30 turn 7).'
RULE_STRICT = (
    'Rule, declared before any validation run: for every background-only configuration (case, exposure, field, bound, '
    'window) pool the maximum studentised correlation Z of all selection acquisitions (n = 64 seeds x 64), '
    'k = floor(ALPHA * n); the threshold T* is the (k+1)-th largest value, so at most k selection acquisitions exceed '
    'it ("trusted" = Z > T*). An acquisition without a candidate (no counts) never exceeds. If fewer than k+1 '
    'acquisitions have a candidate, any candidate is trusted (T* = NO_CANDIDATE_FLOOR). ALPHA = 0.003 leaves room '
    'under the 1 % target for the validation\'s one-sided 95 % Clopper-Pearson upper limit at ~2000 acquisitions. '
    'The universal threshold is the largest per- configuration T* (one rule for every configuration, never lower '
    'than any selected one).')
ABOUT_INCLUSIVE = 'AB-11 threshold re-selection on the selection seeds only, ties counted as exceedances (TODO-30 turn 8).'
RULE_INCLUSIVE = (
    'Rule (AB-11), declared before any validation run: for every background-only configuration (case, exposure, field, '
    'bound, window) pool the maximum studentised correlation Z, recorded to 4 decimals, of all selection acquisitions '
    '(n = seeds x null repeats), k = floor(ALPHA * n). "Trusted" = recorded Z >= T* (an acquisition tied with the '
    'threshold counts as trusted). T* is the smallest recorded value v with #(Z >= v) <= k, so the selection '
    'exceedance fraction including ties is <= ALPHA; if the largest value alone occurs more than k times, T* is one '
    'recording step (1e-4) above it. An acquisition without a candidate (no counts) never exceeds; if no acquisition '
    'has a candidate, any candidate is trusted (T* = NO_CANDIDATE_FLOOR). The universal threshold is the largest '
    'per-configuration T*, reported for comparison only (AB-11 keeps per-configuration thresholds).')


def strict_threshold(ordered, k):
    return ordered[k] if len(ordered) > k else NO_CANDIDATE_FLOOR


def inclusive_threshold(ordered, k):
    """Smallest recorded value v with #(Z >= v) <= k (ordered: candidate values, descending)."""
    if not ordered:
        return NO_CANDIDATE_FLOOR
    threshold = round(ordered[0] + STEP, 4)
    cumulative = 0
    counts = Counter(ordered)
    for value in sorted(counts, reverse=True):
        cumulative += counts[value]
        if cumulative > k:
            break
        threshold = value
    return threshold


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--runs', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--manifest', default=str(HERE.parent / 'manifest-ambient-v1.json'))
    ap.add_argument('--family', default='gate_selection')
    ap.add_argument('--rule', choices=['strict', 'inclusive'], default='strict')
    ap.add_argument('--first-nulls', type=int, default=0,
                    help='use only the first N null repeats of every seed (reproduces a smaller selection from a larger run)')
    args = ap.parse_args()
    seeds, fam = family_seeds(args.manifest, HERE.parent / 'seeds.json', args.family)
    runs = load_runs(args.runs, args.family, seeds)
    keys = sorted(next(iter(runs.values()))['Nulls'])
    per, report = {}, {}
    for key in keys:
        values = []
        for seed in seeds:
            if runs[seed]['Phase'] != 'selection':
                raise SystemExit(f'seed {seed}: not a selection run')
            z = runs[seed]['Nulls'][key]
            values.extend(z[:args.first_nulls] if args.first_nulls else z)
        n = len(values)
        k = math.floor(ALPHA * n)
        ordered = sorted((v for v in values if v is not None), reverse=True)
        if args.rule == 'strict':
            threshold = strict_threshold(ordered, k)
            exceed = sum(1 for v in values if v is not None and v > threshold)
        else:
            threshold = inclusive_threshold(ordered, k)
            exceed = sum(1 for v in values if v is not None and v >= threshold)
            if exceed > k:
                raise SystemExit(f'{key}: inclusive selection exceeds its allowance ({exceed} > {k})')
        per[key] = threshold
        if args.rule == 'strict':
            report[key] = {'Acquisitions': n, 'WithCandidate': len(ordered), 'AllowedExceedances': k,
                           'SelectionExceedances': exceed, 'Threshold': threshold,
                           'MedianZ': ordered[len(ordered) // 2] if ordered else None}
        else:
            report[key] = {'Acquisitions': n, 'WithCandidate': len(ordered), 'AllowedExceedances': k,
                           'SelectionExceedances': exceed, 'SelectionRate': exceed / n,
                           'SelectionUpper95': cp_upper(exceed, n), 'Threshold': threshold,
                           'TiedAtThreshold': sum(1 for v in ordered if v == threshold),
                           'DistinctValues': len(set(ordered)),
                           'MedianZ': ordered[len(ordered) // 2] if ordered else None}
    universal = max(per.values())
    digest = hashlib.sha256()
    for seed in seeds:
        digest.update(hashlib.sha256((pathlib.Path(args.runs) / 'runs' / args.family / str(seed) / 'ambient-gate.json').read_bytes()).digest())
    strict = args.rule == 'strict'
    out = {'_about': ABOUT_STRICT if strict else ABOUT_INCLUSIVE, 'Rule': RULE_STRICT if strict else RULE_INCLUSIVE,
           'Alpha': ALPHA, 'Family': args.family, 'SeedList': fam['seeds'], 'SeedOffset': fam.get('seed_offset', 0),
           'Seeds': seeds, 'RunsDigest': digest.hexdigest(), 'PerConfiguration': per, 'Universal': universal,
           'Selection': report}
    if not strict:     # the turn-7 file names no comparison; the engine's default for it is GreaterThan
        out['Comparison'] = 'RoundedAtLeast'
        out['NullsPerConfiguration'] = report[keys[0]]['Acquisitions']
        out['FirstNulls'] = args.first_nulls or None
    pathlib.Path(args.out).write_text(json.dumps(out, indent=1) + '\n', encoding='utf-8', newline='\n')
    print(f'{len(per)} configurations, universal threshold {universal:.4f}')
    for key in keys:
        r = report[key]
        print(f"{key:70s} n={r['Acquisitions']} cand={r['WithCandidate']} T*={r['Threshold']:.4f} exceed={r['SelectionExceedances']}"
              + ('' if strict else f" tied={r['TiedAtThreshold']} distinct={r['DistinctValues']}"))


if __name__ == '__main__':
    main()
