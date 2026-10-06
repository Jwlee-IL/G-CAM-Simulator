"""Aggregate the TODO-35 csco validation family (Cs-137 under Co-60, D-42) over its seeds.

    python samples/evidence/ambient/aggregate_csco.py --runs <driver-out> --family csco_validation
        --manifest samples/evidence/manifest-csco-v1.json --out samples/evidence/results/csco-v1-validation.json

Refuses missing or failed runs (no silent subset). Every seed transports its own response maps, so per-seed quantities
(R, L_D, A_D) are quoted as median [first, third quartile] over seeds (plus mean +- SD outside the count section);
acquisition outcomes are pooled over seeds as k / N, with exact one-sided 95 % Clopper-Pearson limits outside the count
section (gate_stats). Sections:
- Ratios: R per reference window (on-axis calibration; per scene; per extra direction), its change under the gain
  errors, the apparent Cs per Co Bq with the nominal R at each gain, the ring profile of the per-pixel R_i, the Cs-137
  leak into each reference window (DA-7).
- Ambient: the field's rates per window and bound, the stripped background-model error (DA-9) in counts and over
  sigma0 at each live time.
- Counts (Q1): exact and normal L_C / L_D, A_D, k_D, the window-counting bias; pooled detections per Cs multiple and
  rule (A exact a priori with the true R, P plug-in with the true R, B plug-in with the calibrated R) with the
  expectation for rule A (actual alpha at 0, 1 - beta = 0.95 at L_D) and a 4-SE verdict; gain errors.
- NullValidation (DA-5, DA-6): Z_s false-trusted rate per configuration (target: one-sided 95 % upper limit <= 1 %),
  false-trusted at Co-60's position, and the raw-window PR-SENS-02 gate's trusted false "Cs" at Co-60.
- Imaging (Q2): pooled rates per configuration and Cs count; the smallest Cs count with >= 95 % trusted and within one
  element (stripped) and the raw-window counterparts.
- Residual (Q3): expected Z at Co-60 and the false-trusted rates per injected variant.
"""
import argparse
import json
import math
import pathlib
import statistics

from gate_stats import cp_lower, cp_upper, family_seeds

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
        data = json.loads((run / 'ambient-evidence.json').read_text(encoding='utf-8'))
        if data.get('Family') != 'csco' or data.get('Phase') != 'validation':
            raise SystemExit(f'seed {seed}: not a csco validation run')
        out[seed] = data
    return out


def spread(values):
    values = [v for v in values if v is not None and not (isinstance(v, float) and math.isnan(v))]
    if not values:
        return None
    q = statistics.quantiles(values, n=4, method='inclusive') if len(values) > 1 else [values[0]] * 3
    return {'N': len(values), 'Median': q[1], 'Q1': q[0], 'Q3': q[2], 'Mean': statistics.fmean(values),
            'Sd': statistics.stdev(values) if len(values) > 1 else 0.0}


def spread3(values):
    """Median and quartiles only (the count section has ~400 conditions; Mean / SD add nothing quoted there)."""
    s = spread(values)
    return None if s is None else {'Median': s['Median'], 'Q1': s['Q1'], 'Q3': s['Q3']}


def rate_short(k, n):
    return {'K': k, 'N': n, 'Rate': k / n if n else None}


def rate(k, n):
    return {'K': k, 'N': n, 'Rate': k / n if n else None, 'Lower95': cp_lower(k, n) if n else None,
            'Upper95': cp_upper(k, n) if n else None}


def ratios(runs):
    first = next(iter(runs.values()))['Ratios']
    out = {}
    for ref, body in first.items():
        r = {'RAxisCalibrated': spread([s['Ratios'][ref]['RAxisCalibrated'] for s in runs.values()])}
        r['AxisRingR'] = [statistics.fmean(s['Ratios'][ref]['AxisRingR'][i] for s in runs.values()) for i in range(len(body['AxisRingR']))]
        r['AxisGains'] = {str(g['Gain']): spread([s['Ratios'][ref]['AxisGains'][i]['R'] for s in runs.values()])
                          for i, g in enumerate(body['AxisGains'])}
        scenes = {}
        for name in body['Scenes']:
            sc = [s['Ratios'][ref]['Scenes'][name] for s in runs.values()]
            scenes[name] = {
                'RTrue': spread([x['RTrue'] for x in sc]),
                'RTrueOverAxisCalibrated': spread([x['RTrue'] / s['Ratios'][ref]['RAxisCalibrated'] - 1 for x, s in zip(sc, runs.values())]),
                'RCalibratedHere': spread([x['RCalibratedHere'] for x in sc]),
                'CsLeakIntoRef': spread([x['CsLeakIntoRef'] for x in sc]),
                'CsRate662PerBq': spread([x['CsRate662PerBq'] for x in sc]),
                'CoRate662PerBq': spread([x['CoRate662PerBq'] for x in sc]),
                'CoRateRefPerBq': spread([x['CoRateRefPerBq'] for x in sc]),
                'RingR': [statistics.fmean(x['RingR'][i] for x in sc) for i in range(len(sc[0]['RingR']))],
                'RiRmsVsAxisCalibration': spread([x['RiRmsVsAxisCalibration'] for x in sc]),
                'Gains': {str(g['Gain']): {
                    'RelativeChange': spread([x['Gains'][i]['RelativeChange'] for x in sc]),
                    'ApparentCsBqPerCoBqNominalR': spread([x['Gains'][i]['ApparentCsBqPerCoBqNominalR'] for x in sc])}
                    for i, g in enumerate(sc[0]['Gains'])}}
        r['Scenes'] = scenes
        dirs = []
        for i, d in enumerate(body['Directions']):
            ds = [s['Ratios'][ref]['Directions'][i] for s in runs.values()]
            axis = [s['Ratios'][ref]['Scenes']['coloc']['RTrue'] for s in runs.values()] if 'coloc' in body['Scenes'] else None
            dirs.append({'Elements': d['Elements'], 'RTrue': spread([x['RTrue'] for x in ds]),
                         'RelativeToAxisTruth': spread([x['RTrue'] / a - 1 for x, a in zip(ds, axis)]) if axis else None,
                         'RingR': [statistics.fmean(x['RingR'][k] for x in ds) for k in range(len(ds[0]['RingR']))],
                         'RiRmsVsAxisCalibration': spread([x['RiRmsVsAxisCalibration'] for x in ds])})
        r['Directions'] = dirs
        out[ref] = r
    return out


def ambient(runs):
    first = next(iter(runs.values()))['Ambient']
    return {k: {f: spread([s['Ambient'][k][f] for s in runs.values()]) for f in v} for k, v in first.items()}


def counts(runs):
    first = next(iter(runs.values()))['Counts']
    out = {}
    for key, body in first.items():
        rows = [s['Counts'][key] for s in runs.values()]
        entry = {f: spread3([r[f] for r in rows]) for f in
                 ['Mu662', 'MuRef', 'Sigma0', 'LcNormal', 'LdNormal', 'LcExact', 'LdExact', 'ActualAlpha', 'ADBq', 'KD',
                  'RTrue', 'RCalibrated', 'Leak', 'WindowCountingCsCounts', 'WindowCountingCsBqPerCoBq', 'CalibratedBiasCounts']}
        # DA-9: the stripped background model's error, (truth - model) stripped with the true R, in counts and over sigma0.
        entry['ModelBiasCounts'] = spread3([(r['Mu662'] - r['RTrue'] * r['MuRef']) - (r['Model662'] - r['RTrue'] * r['ModelRef'])
                                           if r['Model662'] > 0 else 0.0 for r in rows]) if '|Co=0' in key else None
        mults = []
        for i, m in enumerate(body['Multiples']):
            n = sum(r['Repeats'] for r in rows)
            item = {'Multiple': m['Multiple'], 'CsCounts': spread3([r['Multiples'][i]['CsCounts'] for r in rows]),
                    'CsBq': spread3([r['Multiples'][i]['CsBq'] for r in rows])}
            for rule in ['DetectedExact', 'DetectedPlugIn', 'DetectedCalibrated']:
                item[rule] = rate_short(sum(r['Multiples'][i][rule] for r in rows), n)
            if m['Multiple'] == 0:
                expected = statistics.fmean(r['ActualAlpha'] for r in rows)
            elif m['Multiple'] == 1:
                expected = 0.95
            else:
                expected = None
            if expected is not None:
                se = math.sqrt(max(expected * (1 - expected), 1e-12) / n)
                item['ExpectedExact'] = expected
                item['ExactWithin4Se'] = abs(item['DetectedExact']['Rate'] - expected) <= 4 * se
                item['ExactZ'] = (item['DetectedExact']['Rate'] - expected) / se
            mults.append(item)
        entry['Multiples'] = mults
        gains = []
        for i, g in enumerate(body['Gains']):
            n = sum(r['Repeats'] for r in rows)
            gains.append({'Gain': g['Gain'], 'BiasCounts': spread3([r['Gains'][i]['BiasCounts'] for r in rows]),
                          'BiasOverSigma0': spread3([r['Gains'][i]['BiasOverSigma0'] for r in rows]),
                          'FreeExact': rate_short(sum(r['Gains'][i]['FreeExact'] for r in rows), n),
                          'FreeCalibrated': rate_short(sum(r['Gains'][i]['FreeCalibrated'] for r in rows), n),
                          'AtLdExact': rate_short(sum(r['Gains'][i]['AtLdExact'] for r in rows), n),
                          'AtLdCalibrated': rate_short(sum(r['Gains'][i]['AtLdCalibrated'] for r in rows), n)})
        entry['Gains'] = gains
        out[key] = entry
    return out


def nulls(runs):
    first = next(iter(runs.values()))['NullValidation']
    out = {}
    for key in first:
        rows = [s['NullValidation'][key] for s in runs.values()]
        n = sum(r['Repeats'] for r in rows)
        e = {'Threshold': rows[0]['Threshold'], 'Trusted': rate(sum(r['Trusted'] for r in rows), n),
             'TrustedAtCo': rate(sum(r['TrustedAtCo'] for r in rows), n),
             'ExpectedCounts662': spread([r['ExpectedCounts662'] for r in rows]),
             'ExpectedCountsRef': spread([r['ExpectedCountsRef'] for r in rows])}
        e['PassesOnePercent'] = e['Trusted']['Upper95'] <= 0.01
        if rows[0]['RawThreshold'] is not None:
            e['RawThreshold'] = rows[0]['RawThreshold']
            e['RawTrusted'] = rate(sum(r['RawTrusted'] for r in rows), n)
            e['RawTrustedAtCo'] = rate(sum(r['RawTrustedAtCo'] for r in rows), n)
        out[key] = e
    return out


def imaging(runs):
    first = next(iter(runs.values()))['Imaging']
    out, needed = {}, {}
    for key in first:
        rows = [s['Imaging'][key] for s in runs.values()]
        n = sum(r['Repeats'] for r in rows)
        e = {'CsCounts': rows[0]['CsCounts'], 'CsBq': spread([r['CsBq'] for r in rows]), 'K': spread([r['K'] for r in rows]),
             'Threshold': rows[0]['Threshold']}
        for f in ['ZTrusted', 'ZCorrect', 'ZAtCo', 'StrippedArgmaxCorrect', 'RawDecoderCorrect']:
            e[f] = rate(sum(r[f] for r in rows), n)
        if rows[0]['RawThreshold'] is not None:
            for f in ['RawTrusted', 'RawCorrect', 'RawAtCo']:
                e[f] = rate(sum(r[f] for r in rows), n)
        out[key] = e
        config, s = key.rsplit('|S=', 1)
        needed.setdefault(config, []).append((float(s), e))
    smallest = {}
    for config, items in needed.items():
        items.sort(key=lambda x: x[0])
        def first_at(field):
            return next((s for s, e in items if field in e and e[field]['Rate'] >= 0.95), None)
        smallest[config] = {'StrippedTrustedCorrect': first_at('ZCorrect'), 'StrippedArgmax': first_at('StrippedArgmaxCorrect'),
                            'RawDecoder': first_at('RawDecoderCorrect'), 'RawGateCorrect': first_at('RawCorrect'),
                            'Grid': [s for s, _ in items]}
    return out, smallest


def residual(runs):
    first = next(iter(runs.values()))['Residual']
    out = {}
    for key in first:
        rows = [s['Residual'][key] for s in runs.values()]
        n = sum(r['Repeats'] for r in rows)
        out[key] = {'Variant': rows[0]['Variant'], 'ExpectedZAtCo': spread([r['ExpectedZAtCo'] for r in rows]),
                    'ResidualAtCoCounts': spread([r['ResidualAtCoCounts'] for r in rows]),
                    'ExpectedCountsRef': spread([r['ExpectedCountsRef'] for r in rows]),
                    'Trusted': rate(sum(r['Trusted'] for r in rows), n), 'TrustedAtCo': rate(sum(r['TrustedAtCo'] for r in rows), n),
                    'MeanMaxZ': spread([r['MeanMaxZ'] for r in rows])}
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--runs', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--manifest', default=str(HERE.parent / 'manifest-csco-v1.json'))
    ap.add_argument('--family', default='csco_validation')
    args = ap.parse_args()
    seeds, fam = family_seeds(args.manifest, HERE.parent / 'seeds.json', args.family)
    runs = load(args.runs, args.family, seeds)
    first = next(iter(runs.values()))
    img, smallest = imaging(runs)
    summary = {
        '_about': 'TODO-35 csco validation summary (aggregate_csco.py); see the module docstring for the definitions.',
        'Family': args.family, 'SeedList': fam['seeds'], 'SeedOffset': fam.get('seed_offset', 0), 'Seeds': seeds, 'N': len(seeds),
        'ElementDeg': first['ElementDeg'], 'SourceDetectorMm': first['SourceDetectorMm'], 'Grid': first['Grid'],
        'CoLevelsBq': first['CoLevelsBq'], 'CoDoseMicroSvPerHourPerBq': first['CoDoseMicroSvPerHourPerBq'],
        'ProductReference': first['ProductReference'],
        'MeanComputeSeconds': statistics.fmean(s['ComputeSeconds'] for s in runs.values()),
        'Ratios': ratios(runs), 'Ambient': ambient(runs), 'Counts': counts(runs), 'NullValidation': nulls(runs),
        'Imaging': img, 'ImagingSmallestCounts': smallest, 'Residual': residual(runs)}
    # Six significant digits (far below every quoted spread) and one top-level section per line keep the committed file
    # small; the content is unchanged otherwise.
    def rounded(x):
        if isinstance(x, float):
            return float(f'{x:.6g}')
        if isinstance(x, dict):
            return {k: rounded(v) for k, v in x.items()}
        if isinstance(x, list):
            return [rounded(v) for v in x]
        return x
    summary = {k: (v if k == 'CoLevelsBq' else rounded(v)) for k, v in summary.items()}   # the levels name the keys
    text = '{\n' + ',\n'.join(f'{json.dumps(k)}: {json.dumps(v, separators=(",", ":"))}' for k, v in summary.items()) + '\n}\n'
    pathlib.Path(args.out).write_text(text, encoding='utf-8', newline='\n')
    nv = summary['NullValidation']
    print(f"{len(seeds)} seeds; Z_s null configurations passing the 1 % limit: {sum(v['PassesOnePercent'] for v in nv.values())} / {len(nv)}")
    checks = [m for c in summary['Counts'].values() for m in c['Multiples'] if 'ExactWithin4Se' in m]
    print(f"count-mode rule A checks within 4 SE: {sum(m['ExactWithin4Se'] for m in checks)} / {len(checks)}")


if __name__ == '__main__':
    main()
