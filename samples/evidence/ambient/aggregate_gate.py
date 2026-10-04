"""Aggregate the AB-7 gate validation and the EV-07 / EV-09 count gates under ambient background (TODO-30 turn 7).

    python samples/evidence/ambient/aggregate_gate.py --runs <driver-out> --out <results.json> [--csv <sources.csv>]

Validation (seeds O128[0:128], disjoint from the selection seeds): per background-only configuration, the number of
false trusted locations (Z > T*, any trusted location is false without a source) out of 128 x 16 acquisitions, its
one-sided 95 % Clopper-Pearson upper limit, and PASS when that limit is <= 1 % (AB-7). Over-dispersion across seeds
(acquisitions sharing one seed's MC maps are clustered) is reported as the dispersion index of the per-seed counts.

Source conditions: per seed the RMS error of the decoder's estimate (mean +- SD over seeds, as EV-07 quotes it),
pooled rates of gross failure (> 3 mm), decoder estimate within one resolution element, trusted (Z > T*), trusted and
correct (within one resolution element), correct among trusted, and the number of seeds whose RMS is below 1 mm.
Count gates: per (case, position, window, exposure, environment), the smallest declared source level at which a
criterion holds (pooled fraction >= 0.95; sub-mm in >= 95 % of seeds) — read off the declared grid, not interpolated.
"""
import argparse
import csv
import json
import pathlib
import statistics

from gate_stats import cp_lower, cp_upper, family_seeds, load_runs

HERE = pathlib.Path(__file__).resolve().parent
TARGET_FALSE = 0.01
TARGET_COVERAGE = 0.95


def rate_summary(runs, seeds, block, field):
    """Mean and SD over seeds of each per-seed MC rate (each seed transports its own maps)."""
    out = {}
    for key in runs[seeds[0]][block]:
        values = [runs[s][block][key][field] for s in seeds]
        out[key] = {'Mean': statistics.mean(values), 'Sd': statistics.stdev(values), 'Seeds': len(values)}
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--runs', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--csv')
    ap.add_argument('--manifest', default=str(HERE.parent / 'manifest-ambient-v1.json'))
    args = ap.parse_args()
    seeds, fam = family_seeds(args.manifest, HERE.parent / 'seeds.json', 'gate_validation')
    runs = load_runs(args.runs, 'gate_validation', seeds)
    thresholds_ref = fam['overrides']['Thresholds']
    thresholds = json.loads((HERE.parents[2] / thresholds_ref['File']).read_text(encoding='utf-8'))
    first = runs[seeds[0]]

    nulls = {}
    for key in sorted(first['Nulls']):
        per_seed = [sum(1 for z in runs[s]['Nulls'][key] if z is not None and z > thresholds['PerConfiguration'][key]) for s in seeds]
        per_seed_u = [sum(1 for z in runs[s]['Nulls'][key] if z is not None and z > thresholds['Universal']) for s in seeds]
        n = sum(len(runs[s]['Nulls'][key]) for s in seeds)
        k, ku = sum(per_seed), sum(per_seed_u)
        mean = k / len(seeds)
        dispersion = (statistics.variance(per_seed) / mean) if mean > 0 else None
        upper = cp_upper(k, n)
        nulls[key] = {'Acquisitions': n, 'ExpectedBackgroundCounts': first['NullBackground'][key]['ExpectedBackgroundCounts'],
                      'Threshold': thresholds['PerConfiguration'][key], 'FalseTrusted': k, 'Rate': k / n,
                      'Upper95': upper, 'Pass': upper <= TARGET_FALSE, 'DispersionIndex': dispersion,
                      'UniversalFalseTrusted': ku, 'UniversalUpper95': cp_upper(ku, n), 'UniversalPass': cp_upper(ku, n) <= TARGET_FALSE}

    sources = {}
    for key in sorted(first['Sources']):
        rows = [runs[s]['Sources'][key] for s in seeds]
        reps = sum(r['Repeats'] for r in rows)
        rms = [r['RmsErrorMm'] for r in rows]
        rms_deg = [r['RmsErrorDeg'] for r in rows]
        gated = rows[0]['Trusted'] is not None
        out = {'ActivityBq': rows[0]['ActivityBq'], 'ExpectedSourceCounts': statistics.mean(r['ExpectedSourceCounts'] for r in rows),
               'ExpectedBackgroundCounts': statistics.mean(r['ExpectedBackgroundCounts'] for r in rows),
               'Seeds': len(rows), 'Acquisitions': reps, 'ResolutionDeg': rows[0]['ResolutionDeg'],
               'RmsErrorMmMean': statistics.mean(rms), 'RmsErrorMmSd': statistics.stdev(rms),
               'RmsErrorMmMedian': statistics.median(rms), 'RmsErrorDegMean': statistics.mean(rms_deg),
               'SubMmSeeds': sum(1 for v in rms if v < 1.0),
               'FailureRate': sum(r['Failures'] for r in rows) / reps,
               'DecoderWithinResolution': sum(r['DecoderWithinResolution'] for r in rows) / reps}
        if gated:
            trusted = sum(r['Trusted'] for r in rows)
            correct = sum(r['TrustedCorrect'] for r in rows)
            out.update({'Trusted': trusted / reps, 'TrustedCorrect': correct / reps,
                        'TrustedCorrectLower95': cp_lower(correct, reps),
                        'CorrectAmongTrusted': correct / trusted if trusted else None,
                        'CorrectAmongTrustedLower95': cp_lower(correct, trusted) if trusted else None,
                        'WrongTrusted': (trusted - correct) / reps,
                        'TrustedCorrectUniversal': sum(r['TrustedCorrectUniversal'] for r in rows) / reps,
                        'TrustedUniversal': sum(r['TrustedUniversal'] for r in rows) / reps})
        sources[key] = out

    # Count gates read off the declared source-level grid.
    groups = {}
    for key, v in sources.items():
        case, pos, window, t, level, *env = key.split('|')
        if not level.startswith('S='):
            continue
        groups.setdefault((case, pos, window, t, '|'.join(env)), []).append((float(level[2:]), v))
    gates = {}
    for (case, pos, window, t, env), items in sorted(groups.items()):
        items.sort(key=lambda p: p[0])

        def first_level(pred):
            return next((s for s, v in items if pred(v)), None)
        g = {'DecoderWithinResolution95': first_level(lambda v: v['DecoderWithinResolution'] >= TARGET_COVERAGE),
             'FailureBelow5Percent': first_level(lambda v: v['FailureRate'] < 0.05),
             'SubMmIn95PercentOfSeeds': first_level(lambda v: v['SubMmSeeds'] >= TARGET_COVERAGE * v['Seeds']),
             'BackgroundCountsAtLevels': {s: v['ExpectedBackgroundCounts'] for s, v in items}}
        if env != 'ideal':
            g.update({'TrustedAndCorrect95': first_level(lambda v: v['TrustedCorrect'] >= TARGET_COVERAGE),
                      'TrustedAndCorrect50': first_level(lambda v: v['TrustedCorrect'] >= 0.5),
                      'CorrectAmongTrusted95': first_level(lambda v: v['CorrectAmongTrusted'] is not None and v['CorrectAmongTrusted'] >= TARGET_COVERAGE),
                      'UniversalTrustedAndCorrect95': first_level(lambda v: v['TrustedCorrectUniversal'] >= TARGET_COVERAGE)})
        gates[f'{case}|{pos}|{window}|{t}|{env}'] = g

    # Derived ratios (arithmetic on the measured rates, not re-measurements): ambient-to-source detected-rate ratio at the
    # default activity (exposure-independent) and, for EV-02's fixed on-axis budgets N0, the background counts per N0.
    request = json.loads((HERE / 'gate-request-v1.json').read_text(encoding='utf-8'))
    amb = rate_summary(runs, seeds, 'AmbientRates', 'CpsPerMicroSvH')
    src = rate_summary(runs, seeds, 'SourceRates', 'CpsPerBq')
    derived = {}
    for c in request['Cases']:
        for p in c['Positions']:
            for w in request['Windows']:
                s_rate = src[f"{c['Name']}|{p['Name']}|{w['Name']}"]['Mean'] * request['DefaultActivityBq']
                for b in request['Bounds']:
                    b_unit = amb[f"{c['Scenario']}|{b}|{w['Name']}"]['Mean']
                    for f in request['FieldsMicroSvPerHour']:
                        key = f"{c['Name']}|{p['Name']}|{w['Name']}|{b}|F={f}"
                        derived[key] = {'SourceCpsAtDefaultActivity': s_rate, 'AmbientCps': b_unit * f,
                                        'AmbientToSourceAtDefaultActivity': b_unit * f / s_rate,
                                        'BackgroundPerN0': {f'N0={n0}|t={t}': b_unit * f * t / n0 for n0 in (500, 5000) for t in (10, 60)}}

    result = {'_about': __doc__.strip().splitlines()[0], 'Family': 'gate_validation', 'SeedList': fam['seeds'],
              'Seeds': len(seeds), 'Thresholds': thresholds_ref, 'Alpha': thresholds['Alpha'], 'Universal': thresholds['Universal'],
              'TargetFalseUpper95': TARGET_FALSE, 'TargetCoverage': TARGET_COVERAGE,
              'AmbientRates': rate_summary(runs, seeds, 'AmbientRates', 'CpsPerMicroSvH'),
              'SourceRates': rate_summary(runs, seeds, 'SourceRates', 'CpsPerBq'),
              'Nulls': nulls, 'Sources': sources, 'CountGates': gates, 'DerivedRatios': derived,
              'MeanComputeSeconds': statistics.mean(runs[s]['ComputeSeconds'] for s in seeds)}
    pathlib.Path(args.out).write_text(json.dumps(result, indent=1) + '\n', encoding='utf-8', newline='\n')
    if args.csv:
        with open(args.csv, 'w', newline='\n', encoding='utf-8') as f:
            cols = sorted({c for v in sources.values() for c in v})
            w = csv.writer(f, lineterminator='\n')
            w.writerow(['condition'] + cols)
            for key, v in sources.items():
                w.writerow([key] + [v.get(c) for c in cols])
    failed = [k for k, v in nulls.items() if not v['Pass']]
    print(f'{len(nulls)} null configurations, {len(failed)} fail the <= 1 % upper limit; {len(sources)} source conditions')
    for k in failed:
        print('FAIL', k, nulls[k])


if __name__ == '__main__':
    main()
