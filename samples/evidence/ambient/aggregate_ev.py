"""Aggregate the AB-13 evidence families under the absolute ambient field (TODO-30 turn 8).

    python samples/evidence/ambient/aggregate_ev.py --runs <driver-out> --family ev12_antimask --out <results.json>
        [--manifest samples/evidence/manifest-ambient-v2.json]

One summary per family, over the family's N outer seeds (every seed transports its own response maps, so the spread
over seeds is the quoted spread, as in VV.Gcam.Evidence §1 "How MC numbers are quoted"). Refuses missing or failed
runs (no silent subset). Families:
- ev12_antimask: per window x environment x method (single / calibrated / antimask) the RMS error (mean +- SD over
  seeds, median), the pooled failure rate (> 3 mm) and the seeds with any failure, the signed mean error; the paired
  per-seed difference antimask - calibrated (mean +- SD, seeds where the antimask is lower).
- ev15_separation (turn 8) / ev15_abs_a, ev15_abs_b (turn 9): per scene x environment the per-seed median relative
  Cs-count error of each estimate (stripping floored / unfloored / background-subtracted; turn 9 also the spatial-lever
  count read at the matched Cs peak, raw and background-subtracted), summarised as median [first, third quartile] over
  seeds, with the per-seed SD; the share of acquisitions with Cs located within 1 mm (raw two-peak match; turn 9 also
  from the stripped reconstruction) and Co located within 1 mm (Co window), with per-seed median errors; R.
- ev02_fov: per series (distance, direction, window, N0, environment) the legacy field-of-view metrics per seed
  (usable half-field >= 90 % within one resolution element, contiguous from 0; centroid side >= 95 %; outside flag
  >= 90 % past the fully coded field; unflagged wrong spot in angle bands), then over seeds: the most frequent value
  with its frequency, median and quartiles.
- ev01_sweep: per scenario x window x environment x decoder the expected number of localized positions (mean +- SD,
  median), the positions localized in >= 50 % of acquisitions, and the AB-12 pull: fixed points (cross-correlation and
  MLEM signed mean error, RMS), the noiseless pull of the expected ambient map, and per-position mean error vectors.
"""
import argparse
import collections
import json
import math
import pathlib
import statistics

HERE = pathlib.Path(__file__).resolve().parent


def family_seeds(manifest_path, family):
    manifest = json.loads(pathlib.Path(manifest_path).read_text(encoding='utf-8'))
    seeds = json.loads((HERE.parent / 'seeds.json').read_text(encoding='utf-8'))
    fam = next(f for f in manifest['families'] if f['id'] == family)
    first = fam.get('seed_offset', 0)
    return seeds[fam['seeds']][first:first + fam['n']], fam


def load(runs_dir, family, seeds):
    out = {}
    for seed in seeds:
        run = pathlib.Path(runs_dir) / 'runs' / family / str(seed)
        done = run / 'done.json'
        if not done.exists():
            raise SystemExit(f'{family} seed {seed}: missing run')
        if json.loads(done.read_text(encoding='utf-8'))['exit'] != 0:
            raise SystemExit(f'{family} seed {seed}: failed run')
        out[seed] = json.loads((run / 'ambient-evidence.json').read_text(encoding='utf-8'))
    return out


def spread(values):
    values = [v for v in values if v is not None and not (isinstance(v, float) and math.isnan(v))]
    if not values:
        return None
    q = statistics.quantiles(values, n=4, method='inclusive') if len(values) > 1 else [values[0]] * 3
    return {'N': len(values), 'Mean': statistics.mean(values), 'Sd': statistics.stdev(values) if len(values) > 1 else 0.0,
            'Median': q[1], 'Q1': q[0], 'Q3': q[2], 'Min': min(values), 'Max': max(values)}


def mode(values):
    """Most frequent value with its frequency 'k / N' (thresholded angles on a grid, as EV-02 quotes them)."""
    counts = collections.Counter(values)
    value, k = max(counts.items(), key=lambda p: (p[1], -abs(p[0]) if isinstance(p[0], (int, float)) else 0))
    return {'Value': value, 'Count': k, 'N': len(values), 'Distribution': {str(v): c for v, c in sorted(counts.items(), key=lambda p: str(p[0]))}}


def antimask(runs, seeds):
    first = runs[seeds[0]]
    out = {'Rates': {k: spread([runs[s]['Rates'][k].get('CpsPerBq', runs[s]['Rates'][k].get('CpsPerMicroSvH')) for s in seeds])
                     for k in first['Rates']}, 'Conditions': {}}
    for key in first['Conditions']:
        rows = [runs[s]['Conditions'][key] for s in seeds]
        entry = {'ExpectedBackgroundCounts': spread([r['ExpectedBackgroundCounts'] for r in rows]),
                 'BackgroundPerPixel': spread([r['BackgroundPerPixel'] for r in rows]),
                 'ActivityBq': spread([r['ActivityBq'] for r in rows]),
                 'ExpectedAntimaskSourceCounts': spread([r['ExpectedAntimaskSourceCounts'] for r in rows])}
        for method in ('Single', 'Calibrated', 'Antimask'):
            reps = sum(r[method]['Repeats'] for r in rows)
            entry[method] = {'RmsErrorMm': spread([r[method]['RmsErrorMm'] for r in rows]),
                             'FailureRate': sum(r[method]['Failures'] for r in rows) / reps,
                             'SeedsWithFailures': sum(1 for r in rows if r[method]['Failures'] > 0),
                             'MeanDxMm': spread([r[method]['MeanDxMm'] for r in rows]),
                             'MeanDyMm': spread([r[method]['MeanDyMm'] for r in rows])}
        diff = [r['Antimask']['RmsErrorMm'] - r['Calibrated']['RmsErrorMm'] for r in rows]
        entry['AntimaskMinusCalibratedMm'] = spread(diff)
        entry['SeedsAntimaskLower'] = sum(1 for d in diff if d < 0)
        out['Conditions'][key] = entry
    return out


def separation(runs, seeds):
    first = runs[seeds[0]]
    out = {'R': spread([runs[s]['Rates']['R'] for s in seeds]), 'ExposureS': first['ExposureS'], 'Conditions': {}}
    for key in first['Conditions']:
        rows = [runs[s]['Conditions'][key] for s in seeds]
        entry = {k: spread([r[k] for r in rows]) for k in ('TrueCsCounts', 'ExpectedWindowCounts', 'ExpectedCoWindowCounts',
                                                           'ExpectedBackground662', 'ExpectedBackgroundCo')}
        for est in ('Floored', 'Unfloored', 'Subtracted'):
            entry[est] = {'PerSeedMedian': spread([r[est]['Median'] for r in rows]),
                          'PerSeedMedianAbs': spread([r[est]['MedianAbs'] for r in rows]),
                          'PerSeedSd': spread([r[est]['Sd'] for r in rows])}
        if rows[0]['CsLocatedWithin1Mm'] is not None:
            entry['CsLocatedWithin1Mm'] = spread([r['CsLocatedWithin1Mm'] for r in rows])
            entry['CsErrorMedianMm'] = spread([r['CsErrorMedianMm'] for r in rows])
        if 'PeakPerCount' in rows[0]:          # turn 9 (AB-14): spatial-lever count, stripped Cs location, Co location
            entry['PeakPerCount'] = spread([r['PeakPerCount'] for r in rows])
            for est in ('Spatial', 'SpatialSubtracted'):
                if rows[0][est] is not None:
                    entry[est] = {'PerSeedMedian': spread([r[est]['Median'] for r in rows]),
                                  'PerSeedMedianAbs': spread([r[est]['MedianAbs'] for r in rows]),
                                  'PerSeedSd': spread([r[est]['Sd'] for r in rows])}
            for k in ('CsStrippedWithin1Mm', 'CsStrippedErrorMedianMm', 'CoWithin1Mm', 'CoErrorMedianMm'):
                entry[k] = spread([r[k] for r in rows])
        out['Conditions'][key] = entry
    return out


def fov_metrics(rows, tan_fc):
    """Legacy parse_fov metrics for one series of one seed (rows sorted by angle)."""
    rows = sorted(rows, key=lambda r: r['Angle'])
    m = {}
    for field in ('LocNonCyclic', 'LocCyclic'):
        last = 0.0
        for r in rows:
            if r[field] < 0.9:
                break
            last = float(r['Angle'])
        m[field] = last
    d = math.radians(rows[0]['Direction'])
    fc = math.degrees(math.atan(tan_fc / max(abs(math.cos(d)), abs(math.sin(d)))))
    for field, gate in (('SideCentroid', 0.95), ('OutsideCentroid', 0.9)):
        run, started = [], False
        for r in rows:
            if r['Angle'] <= (fc if field == 'OutsideCentroid' else 0) or r[field] is None:
                continue
            if r[field] >= gate:
                started = True
                run.append(float(r['Angle']))
            elif started:
                break
        m[field + 'From'] = min(run) if run else None
        m[field + 'To'] = max(run) if run else None
    m['OutsideCentroidMax'] = max((r['OutsideCentroid'] or 0) for r in rows)
    for field in ('FalseInField', 'FalseInFieldUnflagged'):
        for lo, hi in ((5, 12), (7.5, 11), (7.5, 12), (14, 20)):
            values = [r[field] for r in rows if lo <= r['Angle'] <= hi and r[field] is not None]
            m[f'{field}_{lo}_{hi}_Max'] = max(values) if values else None
            m[f'{field}_{lo}_{hi}_Min'] = min(values) if values else None
    return m


def fov(runs, seeds):
    first = runs[seeds[0]]
    tan_fc = first['FullyCodedTan']
    per_series = collections.defaultdict(dict)
    background = collections.defaultdict(list)
    for s in seeds:
        groups = collections.defaultdict(list)
        for r in runs[s]['Rows']:
            groups[(r['S'], r['Direction'], r['Window'], r['N0'], r['Env'])].append(r)
        for key, rows in groups.items():
            per_series[key][s] = fov_metrics(rows, tan_fc)
            background[key].append(rows[0]['ExpectedBackgroundCounts'])
    out = {'ResolutionDeg': first['ResolutionDeg'], 'Series': {}}
    for key, by_seed in sorted(per_series.items(), key=lambda p: str(p[0])):
        metrics = {}
        for name in next(iter(by_seed.values())):
            values = [by_seed[s][name] for s in seeds]
            if name in ('LocNonCyclic', 'LocCyclic') or name.endswith('From') or name.endswith('To'):
                metrics[name] = mode(['none' if v is None else v for v in values])
                numeric = [v for v in values if v is not None]
                if numeric:
                    metrics[name]['Spread'] = spread(numeric)
            else:
                metrics[name] = spread(values)
        metrics['ExpectedBackgroundCounts'] = spread(background[key])
        out['Series']['|'.join(str(k) for k in key)] = metrics
    return out


def sweep(runs, seeds):
    first = runs[seeds[0]]
    out = {}
    for name, sc in first['Scenarios'].items():
        entry = {'ActivityBq': sc['ActivityBq'], 'ExposureS': sc['ExposureS'], 'PeriodMm': sc['PeriodMm'],
                 'GridPositions': sc['GridPositions'], 'Sweep': {}, 'Points': {}, 'BackgroundPull': {}}
        for key in sc['Sweep']:
            rows = [runs[s]['Scenarios'][name]['Sweep'][key] for s in seeds]
            n = len(rows[0]['MeanDxMm'])
            entry['Sweep'][key] = {
                'ExpectedLocalized': spread([r['ExpectedLocalized'] for r in rows]),
                'MajorityLocalized': spread([r['MajorityLocalized'] for r in rows]),
                # per-position pull averaged over seeds (the systematic part; each seed's value is a mean of its repeats)
                'MeanDxMmByPosition': [round(statistics.mean(r['MeanDxMm'][p] for r in rows), 4) for p in range(n)],
                'MeanDyMmByPosition': [round(statistics.mean(r['MeanDyMm'][p] for r in rows), 4) for p in range(n)],
                'FractionByPosition': [round(statistics.mean(r['Fraction'][p] for r in rows), 4) for p in range(n)]}
        for key in sc['Points']:
            rows = [runs[s]['Scenarios'][name]['Points'][key] for s in seeds]
            e = {'ExpectedSourceCounts': spread([r['ExpectedSourceCounts'] for r in rows]),
                 'ExpectedBackgroundCounts': spread([r['ExpectedBackgroundCounts'] for r in rows])}
            for dec in ('CrossCorrelation', 'Mlem'):
                reps = sum(r[dec]['Repeats'] for r in rows)
                e[dec] = {'RmsErrorMm': spread([r[dec]['RmsErrorMm'] for r in rows]),
                          'MeanDxMm': spread([r[dec]['MeanDxMm'] for r in rows]),
                          'MeanDyMm': spread([r[dec]['MeanDyMm'] for r in rows]),
                          'WithinRate': sum(r[dec]['Within'] for r in rows) / reps,
                          'FailureRate': sum(r[dec]['Failures'] for r in rows) / reps}
            if rows[0]['GhostFraction'] is not None:
                e['GhostFraction'] = spread([r['GhostFraction'] for r in rows])
            entry['Points'][key] = e
        for key in sc['BackgroundPull']:
            rows = [runs[s]['Scenarios'][name]['BackgroundPull'][key] for s in seeds]
            entry['BackgroundPull'][key] = {k: spread([r[k] for r in rows]) for k in rows[0]}
        out[name] = entry
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--runs', required=True)
    ap.add_argument('--family', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--manifest', default=str(HERE.parent / 'manifest-ambient-v2.json'))
    args = ap.parse_args()
    seeds, fam = family_seeds(args.manifest, args.family)
    runs = load(args.runs, args.family, seeds)
    kind = runs[seeds[0]]['Family']
    summary = {'antimask': antimask, 'separation': separation, 'fov': fov, 'sweep': sweep}[kind](runs, seeds)
    result = {'_about': __doc__.strip().splitlines()[0], 'Family': args.family, 'Kind': kind, 'SeedList': fam['seeds'],
              'SeedOffset': fam.get('seed_offset', 0), 'Seeds': len(seeds), 'Config': fam['config'],
              'MeanComputeSeconds': statistics.mean(runs[s]['ComputeSeconds'] for s in seeds), 'Summary': summary}
    pathlib.Path(args.out).write_text(json.dumps(result, indent=1) + '\n', encoding='utf-8', newline='\n')
    print(f'{args.family}: {len(seeds)} seeds -> {args.out}')


if __name__ == '__main__':
    main()
