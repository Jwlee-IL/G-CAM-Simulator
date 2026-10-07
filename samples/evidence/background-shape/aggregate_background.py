"""TODO-33 signed-vector selection and qualification; independent outer seeds are the sampling clusters.

No validation subset is accepted. A simultaneous centred cluster bootstrap bounds the norm of each mean
paired vector, with a shared 95% radius across the preselected regimes. Bootstrap coverage is approximate;
conditional Clopper-Pearson limits alone do not account for a shared transported calibration map.
"""
import argparse
import hashlib
import json
import pathlib
import sys

import numpy as np
from scipy.stats import beta

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import provenance as pv


def lower(k, n):
    return 0.0 if k == 0 else float(beta.ppf(0.05, k, n - k + 1))


def simultaneous_radius(vectors, repeats=10000, seed=131071):
    """Resample entire seed clusters together across conditions; never acquisition rows independently."""
    a = np.asarray(vectors, dtype=float)
    if a.ndim != 3 or a.shape[2] != 2 or a.shape[0] < 2:
        raise ValueError('need seeds x conditions x signed components')
    if a.shape[1] == 0:
        return None
    centred = a - a.mean(axis=0)
    rng = np.random.default_rng(seed)
    maxima = []
    for _ in range(repeats):
        resampled = centred[rng.integers(0, len(a), size=len(a))].mean(axis=0)
        maxima.append(float(np.linalg.norm(resampled, axis=1).max()))
    return float(np.quantile(maxima, 0.95, method='higher'))


def summarize(rows, estimator):
    moments = [row['Estimators'][estimator] for row in rows]
    n = sum(m['N'] for m in moments)
    if n == 0 or any(m['N'] != moments[0]['N'] for m in moments):
        raise ValueError('unequal or empty acquisition clusters')
    def means(x):
        return np.asarray([m[x] / m['N'] for m in moments])
    vector = np.column_stack([means('SumExcessX'), means('SumExcessY')])
    error = np.column_stack([means('SumDx'), means('SumDy')])
    ideal_error = np.column_stack([means('SumIdealDx'), means('SumIdealDy')])
    def rate(x):
        k = sum(m[x] for m in moments)
        a = means(x)
        return {'Count': k, 'N': n, 'Rate': k / n, 'ConditionalLower95': lower(k, n),
                'SeedSE': float(a.std(ddof=1) / np.sqrt(len(a))), 'SeedRange': [float(a.min()), float(a.max())]}
    def estimate(x):
        a = means(x)
        return {'Mean': float(a.mean()), 'SeedSE': float(a.std(ddof=1) / np.sqrt(len(a)))}
    value = {'Seeds': len(rows), 'N': n,
             'MeanSignedErrorMm': error.mean(axis=0).tolist(),
             'SignedErrorSeedSE': (error.std(axis=0, ddof=1) / np.sqrt(len(rows))).tolist(),
             'PairedSignedVectorExcessMm': vector.mean(axis=0).tolist(),
             'VectorExcessNormMm': float(np.linalg.norm(vector.mean(axis=0))),
             'ExcessSeedSE': (vector.std(axis=0, ddof=1) / np.sqrt(len(rows))).tolist(),
             'RmsMm': float(np.sqrt(means('SumSquaredError').mean())),
             'IdealRmsMm': float(np.sqrt(means('SumIdealSquaredError').mean())),
             'RmsSquaredSeedSE': float(means('SumSquaredError').std(ddof=1) / np.sqrt(len(rows))),
             'Association': rate('Associated'), 'Trust': rate('Trusted'),
             'TrustedAssociation': rate('TrustedAssociated'), 'GateAssociation': rate('GateAssociated'),
             'FittedBackgroundCounts': estimate('SumBeta'), 'ObservedCounts': estimate('SumCounts'),
             'CalibrationCounts': float(np.mean([r['CalibrationCounts'] for r in rows])),
             'CalibrationCountsSeedSE': float(np.std([r['CalibrationCounts'] for r in rows], ddof=1) / np.sqrt(len(rows))),
             'ExpectedSourceCounts': float(np.mean([r['SourceCounts'] for r in rows])),
             'ExpectedBackgroundCounts': float(np.mean([r['ExpectedBackgroundCounts'] for r in rows]))}
    value['AssociationFailures'] = n - value['Association']['Count']
    value['FalseTrustedLocations'] = value['Trust']['Count'] - value['TrustedAssociation']['Count']
    value['IdealMeanSignedErrorMm'] = ideal_error.mean(axis=0).tolist()
    value['LegacyDifferenceOfBiasNormsMm'] = float(np.linalg.norm(error.mean(axis=0)) - np.linalg.norm(ideal_error.mean(axis=0)))
    z_means = np.asarray([m['SumZ'] / m['ZN'] if m['ZN'] else np.nan for m in moments])
    finite = z_means[np.isfinite(z_means)]
    value['GateZ'] = {'N': sum(m['ZN'] for m in moments), 'Mean': float(finite.mean()) if len(finite) else None,
                      'SeedSE': float(finite.std(ddof=1) / np.sqrt(len(finite))) if len(finite) > 1 else None}
    return value, vector


def choose_iteration(cells, candidates):
    # The same independently association-valid comparison set for every candidate avoids rewarding a candidate
    # for failing association on a difficult cell. Trust determines the later verdict scope, not this ranking.
    common = [key for key, cell in cells.items() if cell['Field'] > 0 and all(
        cell['Estimators']['E4@' + str(it)]['Association']['ConditionalLower95'] >= 0.95
        for it in candidates)]
    if not common:
        raise ValueError('no common association-valid selection conditions; no iteration may be frozen')
    scores = {it: max(cells[k]['Estimators']['E4@' + str(it)]['VectorExcessNormMm'] for k in common) for it in candidates}
    return min(candidates, key=lambda it: (scores[it], it)), common, scores


def validate_budget(request, baseline, base_sha, pin_sha):
    """Allow only the explicitly authorised acquisition-budget delta, preserving the selected design."""
    import copy
    value = copy.deepcopy(request)
    revision = value.pop('BudgetRevision')
    if (revision['BaseRequestSha256'] != base_sha or revision['PinnedSha256'] != pin_sha
            or revision['Workers'] != 16 or value['ValidationRepeats'] != 100 or value['PilotRepeats'] != 4):
        raise ValueError('invalid version-two budget or immutable references')
    value['ValidationRepeats'] = baseline['ValidationRepeats']
    value['PilotRepeats'] = baseline['PilotRepeats']
    if value != baseline:
        raise ValueError('budget revision changes selected physics')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--runs', required=True)
    ap.add_argument('--phase', choices=['selection', 'pilot', 'validation', 'sensitivity'], required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--version', type=int, choices=[1, 2], default=1)
    args = ap.parse_args()
    if args.version == 2 and args.phase not in ['validation', 'sensitivity']:
        raise ValueError('v2 timing pilots use parallel_timing.py; selection pin remains v1')
    request_path = HERE / f'request-v{args.version}.json'
    request = json.loads(request_path.read_bytes())
    if args.version == 2:
        validate_budget(request, json.loads((HERE / 'request-v1.json').read_bytes()),
                        pv.file_sha(HERE / 'request-v1.json'), pv.file_sha(HERE / 'pinned-v1.json'))
    family = 'background_shape_' + args.phase + f'_v{args.version}'
    seed_lists = json.loads((HERE.parent / 'seeds.json').read_text(encoding='utf-8'))
    seeds = seed_lists['BG_VALIDATION32' if args.phase in ['validation', 'sensitivity'] else 'BG_SELECTION16']
    if args.phase == 'pilot':
        seeds = seeds[:1]
    provenance = pv.collect(args.runs, family, seeds)
    runs = []
    for seed in seeds:
        filename = 'background-shape-sensitivity.json' if args.phase == 'sensitivity' else 'background-shape.json'
        path = pathlib.Path(args.runs) / 'runs' / family / str(seed) / filename
        value = json.loads(path.read_text(encoding='utf-8'))
        if value['Phase'] != args.phase or value['Seed'] != seed or len(value['Cells']) != (24 if args.phase == 'sensitivity' else 448):
            raise ValueError('wrong phase, seed, or stage-1 condition count')
        if args.version == 2:
            if value['RequestSha256'] != pv.file_sha(request_path) or value['Repeats'] != 100 or value['Iterations'] != [400]:
                raise ValueError('v2 run does not match the authorised budget and iteration pin')
            if any(m['N'] != 100 for c in value['Cells'] for m in c['Estimators'].values()):
                raise ValueError('incomplete acquisition budget')
        runs.append(value)
    expected = {cell['Key'] for cell in runs[0]['Cells']}
    if any({cell['Key'] for cell in run['Cells']} != expected for run in runs):
        raise ValueError('incompatible condition grids')
    if len({r['RequestSha256'] for r in runs}) != 1:
        raise ValueError('incompatible pinned requests')
    if args.phase != 'selection':
        if any(r['PinnedSha256'] != pv.file_sha(HERE / 'pinned-v1.json') for r in runs):
            raise ValueError('run iteration pin differs from current pin')
    dependencies = [request_path, HERE / 'pinned-v1.json', HERE.parent / 'seeds.json'] if args.version == 2 else []
    prov = pv.summary_provenance([provenance], __file__, args,
                                 {p.relative_to(pv.REPO).as_posix(): pv.file_sha(p) for p in dependencies})
    report = {'SchemaVersion': 1, 'Phase': args.phase, 'Seeds': seeds, 'Provenance': prov,
              'RequestSha256': runs[0]['RequestSha256'], 'RunSeconds': [r['TotalSeconds'] for r in runs]}
    if args.phase == 'pilot':
        run = runs[0]
        fixed = run['MapSeconds']
        # Shared-map setup is conservatively included in the repeat-dependent part. Source transport and
        # matrix construction make this an upper estimate, not a measured full-validation time.
        estimate = len(seed_lists['BG_VALIDATION32']) * (fixed + (run['TotalSeconds'] - fixed) * 300 / run['Repeats'])
        sensitivity_path = pathlib.Path(args.runs) / 'runs' / family / str(seeds[0]) / 'background-shape-sensitivity.json'
        sensitivity = json.loads(sensitivity_path.read_text(encoding='utf-8'))
        if (sensitivity['Phase'] != 'sensitivity-pilot' or sensitivity['Seed'] != seeds[0]
                or sensitivity['PinnedSha256'] != run['PinnedSha256'] or len(sensitivity['Cells']) != 24):
            raise ValueError('incompatible sensitivity timing pilot')
        sensitivity_estimate = len(seed_lists['BG_VALIDATION32']) * sensitivity['TotalSeconds'] * 300 / sensitivity['Repeats']
        combined = estimate + sensitivity_estimate
        report.update({'PilotRepeats': run['Repeats'], 'Iterations': run['Iterations'],
                       'PinnedSha256': run['PinnedSha256'],
                       'ExtrapolatedSerialSeconds': estimate, 'ExtrapolatedSerialHours': estimate / 3600,
                       'SensitivityPilotSeconds': sensitivity['TotalSeconds'],
                       'SensitivityExtrapolatedSerialHours': sensitivity_estimate / 3600,
                       'CombinedExtrapolatedSerialHours': combined / 3600,
                       'TimingLimitHours': 12, 'StopRequired': estimate > 12 * 3600,
                       'Derivation': '32 * (ambient-map seconds + remaining pilot seconds * 300 / pilot repeats); setup conservatively scaled'})
    else:
        indexed = [{c['Key']: c for c in r['Cells']} for r in runs]
        cells, vectors = {}, {}
        for key in sorted(expected):
            rows = [r[key] for r in indexed]
            cell = {k: rows[0][k] for k in ['Case', 'Cyclic', 'TimeS', 'Level', 'Field', 'GridStepMm', 'TargetMm']}
            cell['Estimators'] = {}
            for estimator in rows[0]['Estimators']:
                estimator_rows = rows
                if args.phase == 'sensitivity':
                    variant = estimator.split('|')[0]
                    if variant in rows[0]['CalibrationAcquisitions']:
                        estimator_rows = [dict(row, CalibrationCounts=row['CalibrationAcquisitions'][variant]['RateCps'] * row['TimeS']) for row in rows]
                    elif variant.startswith('scale='):
                        scale = 1 + float(variant.split('=')[1])
                        estimator_rows = [dict(row, CalibrationCounts=row['CalibrationCounts'] * scale) for row in rows]
                cell['Estimators'][estimator], vectors[(key, estimator)] = summarize(estimator_rows, estimator)
            if args.phase == 'sensitivity':
                cell['WrongBoundRefused'] = all(row['WrongBoundRefused'] for row in rows)
                cell['Qualification'] = 'sensitivity only; wrong-bound variants are diagnostic and cannot qualify'
            cells[key] = cell
        report['Cells'] = cells
        if args.phase == 'selection':
            candidates = [60, 120, 240, 400, 800]
            iteration, common, scores = choose_iteration(cells, candidates)
            name = 'E4@' + str(iteration)
            qualified = [k for k in common if cells[k]['Estimators'][name]['TrustedAssociation']['ConditionalLower95'] >= 0.95]
            pin = {'SchemaVersion': 1, 'Iterations': iteration, 'Candidates': candidates,
                   'SelectionSeeds': seeds, 'SelectionRepeatsPerSeed': runs[0]['Repeats'], 'AssociationComparisonRegimes': common,
                   'WorstVectorExcessMm': scores, 'QualifiedRegimes': qualified,
                   'TargetsMm': {k: cells[k]['TargetMm'] for k in qualified},
                   'Rule': 'minimize worst paired signed-vector excess over common independently association-valid regimes; ties fewer iterations',
                   'Qualification': 'conditional one-sided CP lower95 >= .95 for association and jointly trusted association on selection; repeat on validation',
                   'SimultaneousBound': '10000 centred whole-seed cluster bootstrap resamples after dividing each signed vector by its fixed geometric target; radius = .95 higher quantile of maximum normalized mean-vector deviation across pinned regimes; condition UCL=observed norm+radius*target',
                   'BootstrapSeed': 131071, 'Confidence': 0.95, 'CoverageLimitation': 'bootstrap approximation; not a detector accuracy or a new gate guarantee',
                   'RequestSha256': runs[0]['RequestSha256'], 'SeedListsSha256': pv.file_sha(HERE.parent / 'seeds.json')}
            pin['GateThresholdSha256'] = pv.file_sha(HERE.parent / 'ambient/gate-thresholds-v2.json')
            pin['CalibrationKnowledge'] = 'independent transported background map, 500000 incident histories per head/seed; no generating Ambient access'
            pin['KnownScaleLevels'] = [100, 250, 1000, 'default']
            pin['SensitivityScope'] = {'Cases': ['lab', 'head', 'head1m', 'head5m'], 'Cyclic': [True, False],
                                       'LiveTimeS': 60, 'FieldMicroSvH': .2, 'SourceLevels': [250, 1000, 'default'],
                                       'CalibrationAcquisitionS': [600, 3600], 'KnownScaleOffsets': [-.5, -.25, -.1, .1, .25, .5],
                                       'WrongShape': 'front-only on bare truth, diagnostic only; public metadata refuses'}
            pin_path = HERE / 'pinned-v1.json'
            if pin_path.exists():
                raise ValueError('refuse to replace an iteration pin; a changed choice needs confirmation seeds and a new version')
            pin_path.write_text(json.dumps(pin, indent=1) + '\n', encoding='utf-8', newline='\n')
            report['Pin'] = pin
        elif args.phase == 'validation':
            pin_path = HERE / 'pinned-v1.json'
            pin = json.loads(pin_path.read_text(encoding='utf-8'))
            if any(r['PinnedSha256'] != pv.file_sha(pin_path) for r in runs):
                raise ValueError('run iteration pin differs from the aggregation pin')
            name = 'E4@' + str(pin['Iterations'])
            keys = pin['QualifiedRegimes']
            report['PinnedSha256'] = pv.file_sha(pin_path)
            if keys:
                array = np.stack([vectors[k, name] / pin['TargetsMm'][k] for k in keys], axis=1)
                radius = simultaneous_radius(array, seed=pin['BootstrapSeed'])
                report['SimultaneousNormalizedRadius'] = radius
                for key in keys:
                    m = cells[key]['Estimators'][name]
                    valid = m['Association']['ConditionalLower95'] >= .95 and m['TrustedAssociation']['ConditionalLower95'] >= .95
                    upper = m['VectorExcessNormMm'] + radius * pin['TargetsMm'][key]
                    m['VectorNormUpper95SimultaneousMm'] = upper
                    m['Verdict'] = 'pass' if valid and upper <= pin['TargetsMm'][key] else 'fail' if valid else 'unqualified'
            else:
                report['SimultaneousNormalizedRadius'] = None
            for key, cell in cells.items():
                cell['Estimators'][name].setdefault('Verdict', 'outside pinned regime')
        else:
            pin_path = HERE / 'pinned-v1.json'
            if any(r['PinnedSha256'] != pv.file_sha(pin_path) for r in runs):
                raise ValueError('sensitivity iteration pin differs from aggregation pin')
            report['PinnedSha256'] = pv.file_sha(pin_path)
            report['Qualification'] = 'descriptive sensitivities only; no new tolerance or pass verdict'
    pv.publish({pathlib.Path(args.out): pv.json_bytes(report)}, prov)
    print(json.dumps({k: v for k, v in report.items() if k not in ['Cells', 'Provenance', 'Pin']}, indent=1))


if __name__ == '__main__':
    main()
