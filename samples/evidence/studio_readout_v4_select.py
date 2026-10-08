"""Calibration-only experimental offer, descriptive costs, complete ensembles and provenance required."""
import argparse
import json
import math
import pathlib
import statistics as st

import provenance as pv

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
# Two-sided 95% Student cluster intervals; approximate coverage, not exact rare-event bounds.
T975 = {4: 3.182446305284263, 8: 2.364624251010299, 32: 2.039513446396408}


def interval(values, lower=None, upper=None, root=False):
    if len(values) not in T975 or any(not math.isfinite(v) for v in values):
        raise ValueError('invalid seed-cluster sample')
    mean = st.mean(values)
    width = T975[len(values)] * st.stdev(values) / math.sqrt(len(values))
    lo, hi = mean - width, mean + width
    if lower is not None:
        lo, hi = max(lower, lo), max(lower, hi)
    if upper is not None:
        lo, hi = min(upper, lo), min(upper, hi)
    transform = math.sqrt if root else lambda x: x
    return {'Estimate': transform(mean), 'Lower95': transform(lo), 'Upper95': transform(hi), 'Clusters': len(values)}


def offer(rows, expected_seeds):
    if len(rows) != len(expected_seeds) or [r['Seed'] for r in rows] != expected_seeds:
        raise ValueError('missing, duplicate or reordered calibration run')
    failures = []
    for row in rows:
        if type(row['CalibrationSucceeded']) is not bool:
            raise ValueError('corrupt calibration status')
        if not row['CalibrationSucceeded']:
            if not isinstance(row['Failure'], str) or not row['Failure'].strip():
                raise ValueError('missing calibration failure message')
            failures.append({'Seed': row['Seed'], 'Failure': row['Failure']})
        elif row['Failure'] is not None:
            raise ValueError('success with failure message')
    return {'Offered': not failures, 'Label': 'experimental' if not failures else 'refused', 'Failures': failures,
            'CalibrationSeeds': len(rows)}


def seed_cost(imaging, path, target, rate, trials=64):
    rows = [r for r in imaging if r['TargetDirectCounts'] == target and r['RateCps'] == rate]
    if len(rows) != 9 or sorted(r['Position'] for r in rows) != list(range(9)):
        raise ValueError('missing or duplicate imaging position')
    mse, bias2, remote, count = [], [], [], []
    missing = 0
    for row in rows:
        if len(row['Trials']) != trials or [r['Trial'] for r in row['Trials']] != list(range(trials)):
            raise ValueError('missing imaging trial')
        values = [r[path] for r in row['Trials']]
        if all(v is None for v in values):
            missing += 1
            continue
        if any(v is None for v in values):
            raise ValueError('partial missing path')
        for v in values:
            if (any(not isinstance(v[k], (int, float)) or not math.isfinite(v[k]) for k in ('ExDeg', 'EyDeg', 'Count'))
                    or v['Count'] < 0 or type(v['Remote']) is not bool):
                raise ValueError('corrupt imaging estimate')
        mse.append(st.mean(v['ExDeg'] ** 2 + v['EyDeg'] ** 2 for v in values))
        bias2.append(st.mean(v['ExDeg'] for v in values) ** 2 + st.mean(v['EyDeg'] for v in values) ** 2)
        remote.append(st.mean(v['Remote'] for v in values))
        count.append(st.mean(v['Count'] for v in values))
    if missing:
        if missing != 9:
            raise ValueError('partially missing imaging field')
        return None
    return {'Mse': st.mean(mse), 'BiasSquared': st.mean(bias2), 'Remote': st.mean(remote), 'Count': st.mean(count)}


def costs(rows, target, rate, trials=64):
    result = {'TargetDirectCounts': target, 'RateCps': rate, 'Paths': {}, 'F': None}
    by_path = {p: [seed_cost(r['Imaging'], p, target, rate, trials) for r in rows] for p in ('Direct', 'Anger')}
    for path, clusters in by_path.items():
        # No survivor-only interval: missing Anger from a failed calibration remains unavailable.
        if any(v is None for v in clusters):
            result['Paths'][path] = {'Available': False, 'MissingClusters': sum(v is None for v in clusters)}
            continue
        result['Paths'][path] = {'Available': True,
            'RmsDeg': interval([v['Mse'] for v in clusters], lower=0, root=True),
            'BiasDeg': interval([v['BiasSquared'] for v in clusters], lower=0, root=True),
            'RemoteProbability': interval([v['Remote'] for v in clusters], lower=0, upper=1),
            'AcceptedCounts': interval([v['Count'] for v in clusters], lower=0)}
    if all(result['Paths'][p]['Available'] for p in by_path):
        d, a = by_path['Direct'], by_path['Anger']
        dm, am = st.mean(v['Mse'] for v in d), st.mean(v['Mse'] for v in a)
        if dm > 0 and am > 0:
            f = am / dm
            influence = [(aa['Mse'] - f * dd['Mse']) / am for aa, dd in zip(a, d)]
            half = T975[len(rows)] * st.stdev(influence) / math.sqrt(len(rows))
            result['F'] = {'Estimate': f, 'Lower95': f * math.exp(-half), 'Upper95': f * math.exp(half), 'Clusters': len(rows)}
        else:
            result['FUndefined'] = 'zero mean squared error'
    return result


def diagnostics(rows, rate=0):
    data = [r['Diagnostics'].get(str(rate)) if r['Diagnostics'] is not None else None for r in rows]
    if any(d is None for d in data):
        return {'Available': False, 'MissingClusters': sum(d is None for d in data)}
    result = {'Available': True}
    for name, numerator, denominator in [('GXRad2', 'GSumXRad2', 'Accepted'), ('GYRad2', 'GSumYRad2', 'Accepted'),
                                          ('SingleMisId', 'SingleMis', 'Single'), ('MultiCrystalShare', 'Multi', 'Accepted')]:
        if any(d[denominator] <= 0 for d in data):
            result[name] = None
        else:
            result[name] = interval([d[numerator] / d[denominator] for d in data],
                                    lower=None if name.startswith('G') else 0, upper=None if name.startswith('G') else 1)
    ratios = []
    sensitivity_data = [r['Diagnostics']['0'] for r in rows]
    for d in sensitivity_data:
        if len(d['AssignedToCrystal']) != len(d['FullEnergyByFirst']):
            raise ValueError('corrupt sensitivity denominators')
        if any(v == 0 for v in d['FullEnergyByFirst']):
            result['WeakestMedianSensitivity'] = None
            result['SensitivityUnavailable'] = 'unpopulated isolated full-energy denominator'
            return result
        sensitivity = [a / f for a, f in zip(d['AssignedToCrystal'], d['FullEnergyByFirst'])]
        if st.median(sensitivity) == 0:
            result['WeakestMedianSensitivity'] = None
            result['SensitivityUnavailable'] = 'zero isolated median sensitivity'
            return result
        ratios.append(min(sensitivity) / st.median(sensitivity))
    result['WeakestMedianSensitivity'] = interval(ratios, lower=0)
    result['SensitivityMapsPerSeed'] = [[a / f for a, f in zip(d['AssignedToCrystal'], d['FullEnergyByFirst'])] for d in sensitivity_data]
    result['SensitivitySource'] = 'isolated held-out flood, shared beside rate diagnostics'
    return result


def aggregate(runs, manifest_path, phase):
    manifest = pv.read_json(manifest_path)
    recipe = pv.read_json(REPO / manifest['Recipe']['File'])
    rule = pv.read_json(REPO / recipe['Rule']['File'])
    if (recipe['Version'] != 4 or rule['Version'] != 4
            or pv.file_sha(REPO / manifest['Recipe']['File']) != manifest['Recipe']['Sha256']
            or pv.file_sha(REPO / recipe['Rule']['File']) != recipe['Rule']['Sha256']):
        raise ValueError('v4 input pins mismatch')
    seeds = pv.read_json(HERE / 'seeds.json')
    if recipe['CostSeeds'] != seeds['T41_CONFIRM32'][:8]:
        raise ValueError('unrecognised cost seed subset')
    if pv.file_sha(REPO / recipe['High662BudgetEvidence']['File']) != recipe['High662BudgetEvidence']['Sha256']:
        raise ValueError('development budget evidence pin mismatch')
    matrices, provenance = {}, []
    for geometry in recipe['Geometries']:
        families = [f for f in manifest['families'] if f['Phase'] == phase and f['Geometry'] == geometry['Id']]
        if len(families) != 1:
            raise ValueError('missing or duplicate geometry family')
        family = families[0]
        expected = seeds['T41_DEV4' if phase == 'dev' else 'T41_CONFIRM32']
        if seeds[family['seeds']] != expected or family['n'] != len(expected):
            raise ValueError('incomplete seed family')
        data = []
        for seed in expected:
            run = pathlib.Path(runs) / 'runs' / family['id'] / str(seed)
            prov = pv.validate_run(run, family['id'], seed)
            executor = prov['Sources'][prov['Inputs'][0]['SourceId']]['Executor']['Files']
            for file in ('studio_readout_v4_driver.py', 'provenance.py', 'run_seeds.py'):
                if executor.get('samples/evidence/' + file) != pv.file_sha(HERE / file):
                    raise ValueError('unrecognised executor')
            frozen = family['Freeze']
            managed = {p.removeprefix('managed/probe/'): h for p, h in executor.items() if p.startswith('managed/probe/')}
            if (frozen['ManagedClosure'] != managed or frozen['RecipeSha256'] != manifest['Recipe']['Sha256']
                    or frozen['RuleSha256'] != recipe['Rule']['Sha256'] or frozen['DriverSha256'] != pv.file_sha(HERE / 'studio_readout_v4_driver.py')):
                raise ValueError('frozen closure mismatch')
            value = json.loads((run / 'stdout.txt').read_text(encoding='utf-8'))
            if (value['SchemaVersion'] != 4 or value['Seed'] != seed or value['Geometry'] != geometry['Id'] or value['Phase'] != phase
                    or value['RecipeSha256'] != manifest['Recipe']['Sha256'] or value['RuleSha256'] != recipe['Rule']['Sha256']
                    or value['FamilySha256'] != pv.digest(family)):
                raise ValueError('output identity mismatch')
            scheduled = phase == 'dev' or seed in recipe['CostSeeds']
            if value['CostScheduled'] is not scheduled or [r['EnergyKeV'] for r in value['Cells']] != recipe['EnergiesKeV']:
                raise ValueError('cost schedule or line population mismatch')
            for row in value['Cells']:
                row['Seed'] = seed
                if row['CostFailure'] is not None and (not isinstance(row['CostFailure'], str) or not row['CostFailure'].strip()):
                    raise ValueError('corrupt cost failure message')
                budget = recipe['High662CalibrationHistoriesPerCrystal'] if geometry['Id'] == 'High' and row['EnergyKeV'] == 661.7 else recipe['CalibrationHistoriesPerCrystal']
                if row['CalibrationBudget'] != budget or row['CalibrationScored'] != budget * geometry['Count'] ** 2:
                    raise ValueError('calibration budget mismatch')
                if row['CalibrationSucceeded'] and row['Markers'] != geometry['Count'] ** 2:
                    raise ValueError('marker count mismatch')
                if row['CostFailure'] not in (None, 'empty direct correction bin', 'empty Anger correction bin'):
                    raise ValueError('unrecognised measured cost failure')
                if row['CostFailure'] is not None:
                    field = 'FloodDirect' if row['CostFailure'] == 'empty direct correction bin' else 'FloodAnger'
                    flood = row[field]
                    if len(flood) != geometry['Count'] ** 2 or not any(v == 0 for v in flood) or any(v < 0 or not math.isfinite(v) for v in flood):
                        raise ValueError('corrupt empty-bin cost failure')
                expected_images = 9 * len(recipe['DirectReferenceCounts']) * len(recipe['RatesCps']) if scheduled and row['CostFailure'] != 'empty direct correction bin' else 0
                if len(row['Imaging']) != expected_images:
                    raise ValueError('missing or unexpected imaging cells')
                for image in row['Imaging']:
                    if image['TargetDirectCounts'] not in recipe['DirectReferenceCounts'] or image['RateCps'] not in recipe['RatesCps']:
                        raise ValueError('unrecognised exposure or rate')
                    if image['MeanEmittedHistories'] <= 0 or image['ExposurePilotDirect'] <= 0:
                        raise ValueError('corrupt exposure pilot')
                    for trial in image['Trials']:
                        if trial['Direct'] is None or row['CalibrationSucceeded'] and row['CostFailure'] is None and trial['Anger'] is None:
                            raise ValueError('missing calibrated imaging path')
                        for path in ('Direct', 'Anger'):
                            estimate = trial[path]
                            if estimate is not None and estimate['Remote'] != (math.hypot(estimate['ExDeg'], estimate['EyDeg']) > geometry['CellMm'] / geometry['DistanceMm'] * 180 / math.pi):
                                raise ValueError('remote classification mismatch')
                if row['CalibrationSucceeded'] and row['Diagnostics'] is None:
                    raise ValueError('missing crystal diagnostics')
                if not row['CalibrationSucceeded'] and (row['Diagnostics'] is not None or any(t['Anger'] is not None for r in row['Imaging'] for t in r['Trials'])):
                    raise ValueError('Anger measurements on failed calibration')
            data.append(value)
            provenance.append(prov)
        matrix = []
        for li, energy in enumerate(recipe['EnergiesKeV']):
            rows = [d['Cells'][li] for d in data]
            decision = offer(rows, expected)
            if phase == 'dev':
                decision['DevelopmentCalibrationSucceededAll'] = decision['Offered']
                decision['Offered'] = None
                decision['Label'] = 'development-only; confirmation pending'
            decision.update({'EnergyKeV': energy, 'QualificationPublished': phase == 'confirm', 'CostTable': []})
            selected = [r for r in rows if phase == 'dev' or r['Seed'] in recipe['CostSeeds']]
            if any(r['CostFailure'] is not None for r in selected):
                decision['CostUnavailable'] = [{'Seed': r['Seed'], 'Failure': r['CostFailure']} for r in selected if r['CostFailure']]
            if not any(r['CostFailure'] == 'empty direct correction bin' for r in selected):
                decision['CostTable'] = [costs(selected, target, rate, recipe['TrialsPerPosition'])
                    for target in recipe['DirectReferenceCounts'] for rate in recipe['RatesCps']]
            decision['Diagnostics'] = {str(int(rate)): diagnostics(rows, int(rate)) for rate in recipe['RatesCps']}
            matrix.append(decision)
        matrices[geometry['Id']] = matrix
    return {'SchemaVersion': 4, 'Phase': phase, 'RecipeSha256': manifest['Recipe']['Sha256'],
            'RuleSha256': recipe['Rule']['Sha256'], 'QualificationPublished': phase == 'confirm',
            'IntervalMethod': 'two-sided 95% Student outer-seed cluster approximation; MSE and bias-squared transformed; paired log-ratio delta interval for F',
            'IntervalCaution': 'not an exact coverage guarantee; zero between-seed variance gives a degenerate interval and does not establish zero rare-peak risk',
            'FMeaning': 'equal-fluence source MSE ratio, Direct reference = 1; not a measurement-time factor',
            'Matrices': matrices, 'Provenance': pv.merge(provenance)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runs', required=True)
    parser.add_argument('--manifest', required=True)
    parser.add_argument('--phase', choices=['dev', 'confirm'], required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    result = aggregate(args.runs, args.manifest, args.phase)
    result['Provenance'] = pv.summary_provenance([result['Provenance']], __file__, {'Phase': args.phase},
                                               {pathlib.Path(args.manifest).name: pv.file_sha(args.manifest)})
    pv.publish(result['Provenance'], {pathlib.Path(args.out): pv.json_bytes(result)})


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, OSError, TypeError) as error:
        raise SystemExit('TODO-41 v4 refused: ' + str(error))
