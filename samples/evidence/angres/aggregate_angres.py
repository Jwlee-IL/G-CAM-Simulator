"""Aggregate the TODO-34 angres families (angular resolution at use distance, D-41) over their seeds.

    python samples/evidence/angres/aggregate_angres.py --runs <driver-out> --manifest samples/evidence/manifest-angres-v1.json
        --family angres_1m [angres_5m ...] --out samples/evidence/results/angres-v1-<name>.json
    python samples/evidence/angres/aggregate_angres.py --runs <driver-out> --manifest ... --select angres_select angres_matched_select
        --out samples/evidence/results/angres-v1-iterations.json

Refuses missing or failed runs (no silent subset). Acquisition outcomes are pooled over seeds as k / N; each seed has its own
sampling phase (pair position and axis, DR-3), so the 95 % interval is a percentile bootstrap over seeds (2000 resamples,
fixed generator), not a binomial interval. Resolved-pair rule (DR-2): "resolved at Delta" = pooled pass >= 95 % and pooled
single-source false split <= 5 % at Delta and at every larger Delta of the grid on which the decoder ran; otherwise
"not reached" with the best pass rate. Per-seed point quantities (Q1, DR-9) are quoted as median [Q1, Q3] and range.

--select applies DR-5 to the selection families: for each MLEM variant, the iteration count with the smallest resolved
separation at the selection condition (valley 0.25), ties to fewer iterations; the whole table is kept as the iteration
sensitivity.

--floor SELECT VALIDATE [SELECT VALIDATE ...] (turn 3) applies the significance floor: per decoder x total counts x
placement, a floor F on the second peak's absolute prominence is selected on the selection family's single-source nulls and
applied to the validation family. Primary rule: the smallest F (among 0 and just above each observed null statistic) at
which the assigned false split is <= ALPHA_SEL at every separation of the grid. ALPHA_SEL = 3 %: 5 % minus about two
standard errors of a 1600-acquisition selection rate, sqrt(0.05 * 0.95 / 1600) = 0.55 %, so the validation rate stays under
5 %. Sensitivity: the hypothesis-free floor, the smallest F at which a second peak anywhere exceeds F in <= ALPHA_SEL of the
nulls. Both are reported beside the shape-only result of the same validation runs. --selftest checks the selection rule
against brute force.
"""
import argparse
import json
import math
import pathlib
import random
import statistics
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
import provenance as pv

import numpy as np

HERE = pathlib.Path(__file__).resolve().parent
PASS, NULL = 0.95, 0.05
BOOT = 2000


def family_seeds(manifest_path, family):
    manifest = json.loads(pathlib.Path(manifest_path).read_text(encoding='utf-8'))
    seeds = json.loads((HERE.parent / 'seeds.json').read_text(encoding='utf-8'))
    fam = next(f for f in manifest['families'] if f['id'] == family)
    first = fam.get('seed_offset', 0)
    return seeds[fam['seeds']][first:first + fam['n']], fam


def load(runs, family, seeds):
    out = {}
    for seed in seeds:
        run = pathlib.Path(runs) / 'runs' / family / str(seed)
        done = run / 'done.json'
        if not done.exists():
            raise SystemExit(f'{family} seed {seed}: missing run')
        if json.loads(done.read_text(encoding='utf-8'))['exit'] != 0:
            raise SystemExit(f'{family} seed {seed}: failed run')
        out[seed] = json.loads((run / 'ambient-evidence.json').read_text(encoding='utf-8'))
    return out


def spread(values):
    v = sorted(x for x in values if x is not None)
    if not v:
        return None
    q = statistics.quantiles(v, n=4, method='inclusive') if len(v) > 1 else [v[0]] * 3
    return {'N': len(v), 'Median': round(q[1], 4), 'Q1': round(q[0], 4), 'Q3': round(q[2], 4),
            'Min': round(v[0], 4), 'Max': round(v[-1], 4), 'Mean': round(statistics.fmean(v), 4)}


_INDEX = {}


def boot_ci(k_per_seed, n_per_seed, rng):
    """Percentile bootstrap over seeds of the pooled rate sum(k) / sum(n); one resample index matrix per seed count
    (numpy generator seeded from rng, so the intervals are reproducible)."""
    m = len(k_per_seed)
    if m not in _INDEX:
        _INDEX[m] = np.random.default_rng(rng.randrange(2 ** 32)).integers(0, m, size=(BOOT, m))
    idx = _INDEX[m]
    k = np.asarray(k_per_seed, dtype=float)[idx].sum(axis=1)
    n = np.asarray(n_per_seed, dtype=float)[idx].sum(axis=1)
    rates = np.sort(k / n)
    return [round(float(rates[int(0.025 * BOOT)]), 4), round(float(rates[int(0.975 * BOOT) - 1]), 4)]


def pooled_conditions(data, valleys, rng):
    """{condition key: {decoder: {'Pass': [...per v], 'Null': [...], 'N': ..., 'PassCi': [...], 'NullCi': [...]}}}."""
    seeds = list(data)
    keys = list(data[seeds[0]]['Conditions'])
    out = {}
    for key in keys:
        cond0 = data[seeds[0]]['Conditions'][key]
        reps = [data[s]['Conditions'][key]['Repeats'] for s in seeds]
        dec = {}
        for name in cond0['Decoders']:
            entry = {'N': sum(reps), 'Pass': [], 'Null': [], 'PassCi': [], 'NullCi': []}
            for v in range(len(valleys)):
                kp = [data[s]['Conditions'][key]['Decoders'][name]['Pass'][v] for s in seeds]
                kn = [data[s]['Conditions'][key]['Decoders'][name]['Null'][v] for s in seeds]
                entry['Pass'].append(round(sum(kp) / sum(reps), 4))
                entry['Null'].append(round(sum(kn) / sum(reps), 4))
                entry['PassCi'].append(boot_ci(kp, reps, rng))
                entry['NullCi'].append(boot_ci(kn, reps, rng))
            dec[name] = entry
        out[key] = {'Placement': cond0['Placement'], 'SeparationElements': cond0['SeparationElements'],
                    'CountsPerSource': cond0['CountsPerSource'], 'Ratio': cond0['Ratio'], 'Environment': cond0['Environment'],
                    'BackgroundCounts': round(statistics.fmean(data[s]['Conditions'][key]['BackgroundCounts'] for s in seeds), 3),
                    'Decoders': dec}
    return out


def resolved(conds, valleys):
    """DR-2 rule per (placement, counts, ratio, environment, decoder, v)."""
    groups = {}
    for c in conds.values():
        g = (c['Placement'], c['CountsPerSource'], c['Ratio'], c['Environment'])
        groups.setdefault(g, []).append(c)
    out = []
    for (pl, cnt, ratio, env), cs in sorted(groups.items(), key=lambda t: (t[0][0], t[0][1], -t[0][2], t[0][3])):
        cs.sort(key=lambda c: c['SeparationElements'])
        names = sorted({n for c in cs for n in c['Decoders']})
        for name in names:
            rows = [c for c in cs if name in c['Decoders']]
            for v, vv in enumerate(valleys):
                ok = [c['Decoders'][name]['Pass'][v] >= PASS and c['Decoders'][name]['Null'][v] <= NULL for c in rows]
                res = None
                for i in range(len(rows)):
                    if all(ok[i:]):
                        res = rows[i]['SeparationElements']
                        break
                best = max(rows, key=lambda c: c['Decoders'][name]['Pass'][v])
                out.append({'Placement': pl, 'CountsPerSource': cnt, 'Ratio': ratio, 'Environment': env, 'Decoder': name, 'Valley': vv,
                            'ResolvedElements': res, 'BestPass': best['Decoders'][name]['Pass'][v],
                            'BestPassAtElements': best['SeparationElements'],
                            'MaxNull': max(c['Decoders'][name]['Null'][v] for c in rows),
                            'PassBySeparation': {str(c['SeparationElements']): c['Decoders'][name]['Pass'][v] for c in rows},
                            'NullBySeparation': {str(c['SeparationElements']): c['Decoders'][name]['Null'][v] for c in rows}})
    return out


def point_summary(data):
    pts = [d['Point'] for d in data.values() if d.get('Point')]
    if not pts:
        return None
    s = {k: spread([p[k] for p in pts]) for k in ['CcFwhmXDeg', 'CcFwhmYDeg', 'CcHalfMaxDiameterDeg', 'CcPeakPerCount',
                                                   'CcArgmaxErrorDeg', 'CcTentErrorDeg', 'CcArgmaxDxDeg', 'CcArgmaxDyDeg',
                                                   'CcTentDxDeg', 'CcTentDyDeg']}
    s['CcFwhmXYDeg'] = spread([p['CcFwhmXDeg'] for p in pts] + [p['CcFwhmYDeg'] for p in pts])
    s['FwhmNaN'] = sum(1 for p in pts for k in ('CcFwhmXDeg', 'CcFwhmYDeg') if p[k] is None)
    mlem = {}
    for name in pts[0]['Mlem']:
        mlem[name] = {k: spread([p['Mlem'][name][k] for p in pts]) for k in ('FwhmXDeg', 'FwhmYDeg', 'HalfMaxDiameterDeg')}
    s['MlemNoiseless'] = mlem
    pois = []
    for i, row in enumerate(pts[0]['Poisson']):
        rs = [p['Poisson'][i] for p in pts]
        n = sum(r['Repeats'] for r in rs)
        pois.append({'Counts': row['Counts'], 'N': n,
                     'RmsErrorDeg': round((sum(r['RmsErrorDeg'] ** 2 * r['Repeats'] for r in rs) / n) ** 0.5, 4),
                     'PerSeedRmsDeg': spread([r['RmsErrorDeg'] for r in rs]),
                     'MeanAbsBiasDeg': round(statistics.fmean((r['MeanDxDeg'] ** 2 + r['MeanDyDeg'] ** 2) ** 0.5 for r in rs), 4),
                     'WithinOneElement': round(sum(r['WithinOneElement'] for r in rs) / n, 4)})
    s['Poisson'] = pois
    return s


def summarise(runs, manifest, family, rng):
    seeds, fam = family_seeds(manifest, family)
    data = load(runs, family, seeds)
    first = data[seeds[0]]
    out = {'Family': family, 'Seeds': len(seeds), 'Evidence': fam.get('evidence')}
    if first.get('Family') == 'angres-ladder':
        out.update(ladder(data, rng))
        return out
    valleys = first['Valleys']
    out.update({'ElementDeg': first['ElementDeg'], 'SourceDetectorMm': first['SourceDetectorMm'], 'Grid': first['Grid'],
                'ShadowCellPerPixel': first['ShadowCellPerPixel'], 'ClosedCellTransmission': first['ClosedCellTransmission'],
                'Valleys': valleys,
                'Axes': {'x': sum(1 for d in data.values() if d['Phase']['Axis'] == 'x'),
                         'y': sum(1 for d in data.values() if d['Phase']['Axis'] == 'y')},
                'ComputeSeconds': spread([d['ComputeSeconds'] for d in data.values()])})
    if 'Ambient' in first:
        out['Ambient'] = [{'Bound': a['Bound'],
                           'TruthCpsPerMicroSvH': spread([d['Ambient'][i]['TruthCpsPerMicroSvH'] for d in data.values()]),
                           'CountsInExposure': spread([d['Ambient'][i]['CountsInExposure'][0] for d in data.values()])}
                          for i, a in enumerate(first['Ambient'])]
    out['Point'] = point_summary(data)
    if first['Conditions']:
        conds = pooled_conditions(data, valleys, rng)
        out['Resolved'] = resolved(conds, valleys)
        out['Conditions'] = conds
    return out


def ladder(data, rng):
    seeds = list(data)
    first = data[seeds[0]]
    out = {'Valleys': first['Valleys'], 'Ev11CountsPerSource': spread([d['Ev11CountsPerSource'] for d in data.values()])}
    l0 = []
    for i, row in enumerate(first['L0']):
        l0.append({'SeparationMm': row['SeparationMm'], 'SeparationElements': round(row['SeparationElements'], 4),
                   'CrossResolvedSeeds': sum(d['L0'][i]['CrossResolved'] for d in data.values()),
                   'MlemResolvedSeeds': sum(d['L0'][i]['MlemResolved'] for d in data.values()),
                   'CrossValley': spread([d['L0'][i]['CrossValleyDepth'] for d in data.values()]),
                   'MlemValley': spread([d['L0'][i]['MlemValleyDepth'] for d in data.values()])})
    out['L0'] = l0
    steps = {}
    for name, step in first['Steps'].items():
        rows = []
        for i, row in enumerate(step['Rows']):
            reps = [d['Steps'][name]['Rows'][i]['Repeats'] for d in data.values()]
            decs = {}
            for k, dec in enumerate(row['Decoders']):
                e = {'N': sum(reps), 'Pass': [], 'Null': [], 'PassCi': []}
                for v in range(len(first['Valleys'])):
                    kp = [d['Steps'][name]['Rows'][i]['Decoders'][k]['Pass'][v] for d in data.values()]
                    kn = [d['Steps'][name]['Rows'][i]['Decoders'][k]['Null'][v] for d in data.values()]
                    e['Pass'].append(round(sum(kp) / sum(reps), 4))
                    e['Null'].append(round(sum(kn) / sum(reps), 4))
                    e['PassCi'].append(boot_ci(kp, reps, rng))
                decs[dec['Name']] = e
            level = 'ev11' if abs(row['CountsPerSource'] - data[seeds[0]]['Ev11CountsPerSource']) < 1e-6 else row['CountsPerSource']
            rows.append({'SeparationElements': row['SeparationElements'], 'CountLevel': level, 'Decoders': decs})
        # resolved separation per (count level, decoder, v)
        res = []
        for level in sorted({str(r['CountLevel']) for r in rows}):
            rs = sorted([r for r in rows if str(r['CountLevel']) == level], key=lambda r: r['SeparationElements'])
            for dname in rs[0]['Decoders']:
                for v, vv in enumerate(first['Valleys']):
                    ok = [r['Decoders'][dname]['Pass'][v] >= PASS and r['Decoders'][dname]['Null'][v] <= NULL for r in rs]
                    r0 = next((rs[i]['SeparationElements'] for i in range(len(rs)) if all(ok[i:])), None)
                    res.append({'CountLevel': level, 'Decoder': dname, 'Valley': vv, 'ResolvedElements': r0,
                                'BestPass': max(r['Decoders'][dname]['Pass'][v] for r in rs),
                                'MaxNull': max(r['Decoders'][dname]['Null'][v] for r in rs),
                                'PassBySeparation': {str(r['SeparationElements']): r['Decoders'][dname]['Pass'][v] for r in rs},
                                'NullBySeparation': {str(r['SeparationElements']): r['Decoders'][dname]['Null'][v] for r in rs}})
        steps[name] = {'SourceDetectorMm': step['SourceDetectorMm'], 'ElementDeg': step['ElementDeg'], 'Transported': step['Transported'],
                       'Cyclic': step['Cyclic'], 'ShadowCellPerPixel': step['ShadowCellPerPixel'], 'Resolved': res, 'Rows': rows}
    out['Steps'] = steps
    return out


def select(runs, manifest, families, rng):
    out = {'Rule': 'DR-5: per MLEM variant, the iteration count with the smallest resolved separation (valley 0.25, pass >= 95 %, '
                   'null <= 5 %, every larger separation also passing) at the selection condition; ties to fewer iterations',
           'Families': {}}
    for family in families:
        s = summarise(runs, manifest, family, rng)
        table = {}
        for r in s['Resolved']:
            if r['Decoder'] == 'cc':
                continue
            variant, it = r['Decoder'].split('@')
            table.setdefault(variant, {}).setdefault(int(it), {})[str(r['Valley'])] = {
                'ResolvedElements': r['ResolvedElements'], 'BestPass': r['BestPass'], 'MaxNull': r['MaxNull'],
                'PassBySeparation': r['PassBySeparation'], 'NullBySeparation': r['NullBySeparation']}
        chosen = {}
        for variant, its in table.items():
            def key(it):
                res = its[it]['0.25']['ResolvedElements']
                return (res if res is not None else 1e9, it)
            best = min(its, key=key)
            chosen[variant] = {'Iterations': best, 'ResolvedElements': its[best]['0.25']['ResolvedElements']}
        cc = [r for r in s['Resolved'] if r['Decoder'] == 'cc']
        out['Families'][family] = {'Seeds': s['Seeds'], 'Chosen': chosen, 'Sensitivity': table,
                                   'CrossCorrelation': [{k: r[k] for k in ('Valley', 'ResolvedElements', 'BestPass', 'MaxNull',
                                                                           'PassBySeparation', 'NullBySeparation')} for r in cc]}
    return out


ALPHA_SEL = 0.03


def select_floor(values, alpha):
    """Smallest F in {0} U {next double above each value} with share(value > 0 and value >= F) <= alpha (inclusive)."""
    n = len(values)
    pos = sorted(v for v in values if v > 0)
    allowed = math.floor(alpha * n + 1e-9)
    if len(pos) <= allowed:
        return 0.0
    return math.nextafter(pos[len(pos) - allowed - 1], math.inf)


def select_floor_assigned(null_by_sep, alpha):
    """null_by_sep: {separation: [signed statistics]} (stat > 0 = shape-resolved against the hypothesised pair, |stat| = the
    second peak's prominence). Smallest F with the assigned false split share(stat > 0 and stat >= F) <= alpha at every
    separation."""
    cands = sorted({0.0} | {math.nextafter(v, math.inf) for vs in null_by_sep.values() for v in vs if v > 0})
    for f in cands:
        if all(sum(1 for v in vs if v > 0 and v >= f) <= alpha * len(vs) + 1e-9 for vs in null_by_sep.values()):
            return f
    return math.inf


def _selftest():
    rng = random.Random(1)
    for _ in range(300):
        n = 1 + rng.randrange(300)
        xs = [0.0 if rng.random() < 0.3 else round(rng.random() * 50) / 10 for _ in range(n)]
        for a in (0.0, 0.01, 0.05, 0.2):
            def ex(t):
                return sum(1 for v in xs if v > 0 and v >= t) / n
            for f in (select_floor(xs, a), select_floor_assigned({0: xs}, a)):
                assert ex(f) <= a + 1e-12, (f, a)
                assert all(ex(c) > a for c in set(xs) if 0 < c < f), (f, a)
    print('selftest ok')


def floor_family(runs, manifest, select_family, validate_family, rng):
    sel_seeds, _ = family_seeds(manifest, select_family)
    val_seeds, _ = family_seeds(manifest, validate_family)
    sel = load(runs, select_family, sel_seeds)
    val = load(runs, validate_family, val_seeds)
    if set(sel_seeds) & set(val_seeds):
        raise SystemExit('selection and validation seeds overlap')
    first = val[val_seeds[0]]
    v = 0                                        # the claimed valley (the turn-3 requests record v = 0.25 only)

    def total(c):
        return round(c['CountsPerSource'] * (1 + 1 / c['Ratio']), 6)

    groups = {}
    for d in sel.values():
        for c in d['Conditions'].values():
            for name, dec in c['Decoders'].items():
                g = groups.setdefault((name, total(c), c['Placement']), {'by_sep': {}, 'all': []})
                g['by_sep'].setdefault(c['SeparationElements'], []).extend(dec['NullStat'][v])
                g['all'].extend(abs(x) for x in dec['NullStat'][v])
    floors = {k: {'Assigned': select_floor_assigned(g['by_sep'], ALPHA_SEL), 'HypothesisFree': select_floor(g['all'], ALPHA_SEL),
                  'SelectionNullsPerSeparation': min(len(x) for x in g['by_sep'].values())}
              for k, g in groups.items()}
    conds = {}
    for key, c0 in first['Conditions'].items():
        reps = [val[s]['Conditions'][key]['Repeats'] for s in val_seeds]
        fl_key = None
        decs = {}
        for name in c0['Decoders']:
            fl = floors[(name, total(c0), c0['Placement'])]
            e = {'N': sum(reps)}
            for label, f in (('Shape', 0.0), ('Floor', fl['Assigned']), ('FloorHypothesisFree', fl['HypothesisFree'])):
                kp = [sum(1 for x in val[s]['Conditions'][key]['Decoders'][name]['PairStat'][v] if x > 0 and x >= f) for s in val_seeds]
                kn = [sum(1 for x in val[s]['Conditions'][key]['Decoders'][name]['NullStat'][v] if x > 0 and x >= f) for s in val_seeds]
                if label == 'Shape':
                    if sum(kp) != sum(val[s]['Conditions'][key]['Decoders'][name]['Pass'][v] for s in val_seeds):
                        raise SystemExit(f'{key} {name}: recorded statistics disagree with the pass counts')
                e[label] = {'Pass': round(sum(kp) / sum(reps), 4), 'Null': round(sum(kn) / sum(reps), 4),
                            'PassCi': boot_ci(kp, reps, rng), 'NullCi': boot_ci(kn, reps, rng)}
            decs[name] = e
        conds[key] = {'Placement': c0['Placement'], 'SeparationElements': c0['SeparationElements'], 'CountsPerSource': c0['CountsPerSource'],
                      'Ratio': c0['Ratio'], 'Decoders': decs}
    by = {}
    for c in conds.values():
        by.setdefault((c['Placement'], c['CountsPerSource'], c['Ratio']), []).append(c)
    rows_out = []
    for (pl, cnt, ratio), cs in sorted(by.items(), key=lambda t: (t[0][0], -t[0][2], t[0][1])):
        cs.sort(key=lambda c: c['SeparationElements'])
        for name in sorted({n for c in cs for n in c['Decoders']}):
            rows = [c for c in cs if name in c['Decoders']]
            fl = floors[(name, round(cnt * (1 + 1 / ratio), 6), pl)]
            row = {'Placement': pl, 'CountsPerSource': cnt, 'Ratio': ratio, 'Decoder': name,
                   'FloorAssigned': None if fl['Assigned'] == math.inf else fl['Assigned'], 'FloorHypothesisFree': fl['HypothesisFree']}
            for label in ('Shape', 'Floor', 'FloorHypothesisFree'):
                ok = [c['Decoders'][name][label]['Pass'] >= PASS and c['Decoders'][name][label]['Null'] <= NULL for c in rows]
                row[label] = {'ResolvedElements': next((rows[i]['SeparationElements'] for i in range(len(rows)) if all(ok[i:])), None),
                              'BestPass': max(c['Decoders'][name][label]['Pass'] for c in rows),
                              'MaxNull': max(c['Decoders'][name][label]['Null'] for c in rows),
                              'PassBySeparation': {str(c['SeparationElements']): c['Decoders'][name][label]['Pass'] for c in rows},
                              'NullBySeparation': {str(c['SeparationElements']): c['Decoders'][name][label]['Null'] for c in rows}}
            rows_out.append(row)
    return {'SelectionFamily': select_family, 'SelectionSeeds': len(sel_seeds), 'ValidationFamily': validate_family,
            'ValidationSeeds': len(val_seeds), 'AlphaSelection': ALPHA_SEL, 'Valley': first['Valleys'][v],
            'SourceDetectorMm': first['SourceDetectorMm'], 'ElementDeg': first['ElementDeg'],
            'Floors': [{'Decoder': k[0], 'TotalCounts': k[1], 'Placement': k[2],
                        'Assigned': None if f['Assigned'] == math.inf else f['Assigned'], 'HypothesisFree': f['HypothesisFree'],
                        'SelectionNullsPerSeparation': f['SelectionNullsPerSeparation']} for k, f in sorted(floors.items())],
            'Resolved': rows_out, 'Conditions': conds}


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--runs', default='')
    ap.add_argument('--manifest', default='')
    ap.add_argument('--family', nargs='*', default=[])
    ap.add_argument('--select', nargs='*', default=[])
    ap.add_argument('--floor', nargs='*', default=[], help='turn 3: SELECT VALIDATE family pairs')
    ap.add_argument('--selftest', action='store_true')
    ap.add_argument('--out', default='')
    ap.add_argument('--compact', action='store_true', help='drop per-condition detail (keep Resolved and point summaries)')
    a = ap.parse_args()
    rng = random.Random(20261006)
    if a.selftest:
        _selftest()
        return
    families = a.floor or a.select or a.family
    prov = pv.summary_provenance([pv.collect(a.runs, f, family_seeds(a.manifest, f)[0])
                                  for f in families], __file__, a)
    if a.floor:
        result = {'Families': {}}
        for i in range(0, len(a.floor), 2):
            result['Families'][a.floor[i + 1]] = floor_family(a.runs, a.manifest, a.floor[i], a.floor[i + 1], rng)
    elif a.select:
        result = select(a.runs, a.manifest, a.select, rng)
    else:
        result = {'Families': {}}
        for f in a.family:
            s = summarise(a.runs, a.manifest, f, rng)
            if a.compact:
                s.pop('Conditions', None)
                if 'Steps' in s:
                    for st in s['Steps'].values():
                        st.pop('Rows', None)
            result['Families'][f] = s
    result['Provenance'] = prov
    pv.publish({pathlib.Path(a.out): pv.json_bytes(result)}, prov)
    print(f'wrote {a.out}')


if __name__ == '__main__':
    main()
