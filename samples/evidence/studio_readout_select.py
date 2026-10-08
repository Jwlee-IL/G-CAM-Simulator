"""Failing-closed TODO-41 development freeze and per-condition qualification, with seed-cluster RMS bounds.

No phase runs another phase. Complete pinned ensembles and provenance are required; a calibration failure is data.
The one-sided Student interval is on equal-outer-seed mean squared angular error, then square rooted. Its normal
outer-cluster approximation (not an exact small-sample coverage guarantee) is part of the pinned statistical rule.
"""
import argparse
import json
import math
import pathlib
import statistics

import provenance as pv

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
T95 = {4: 2.353363434801823, 16: 1.753050355692555, 32: 1.695518782545865}


def upper(values):
    if len(values) not in T95 or any(not math.isfinite(v) or v < 0 for v in values):
        raise ValueError('unsupported cluster population')
    return statistics.mean(values) + T95[len(values)] * statistics.stdev(values) / math.sqrt(len(values))


def validate_row(row, geometry, recipe):
    n = geometry['Count'] ** 2
    arrays = [row[k] for k in ('Scored', 'Triggered', 'Windowed', 'Assigned')]
    if any(len(a) != n or any(type(x) is not int or x < 0 for x in a) for a in arrays):
        raise ValueError('invalid denominators')
    if any(not s >= t >= w >= a for s, t, w, a in zip(*arrays)):
        raise ValueError('non-nested denominators')
    expected = n * recipe['ValidationHistoriesPerCrystal'] if row['RateCps'] == 0 else recipe['RateHits']
    if sum(arrays[0]) != expected or row['AssignedTotal'] != sum(arrays[3]):
        raise ValueError('scored/assigned total mismatch')
    if row['CalibrationScored'] != n * recipe['CalibrationHistoriesPerCrystal']:
        raise ValueError('calibration budget mismatch')
    if not 0 <= row['CalibrationWindowed'] <= row['CalibrationTriggered'] <= row['CalibrationScored']:
        raise ValueError('invalid calibration denominators')
    if type(row['CalibrationSucceeded']) is not bool or row['CalibrationSucceeded'] and row['Markers'] != n:
        raise ValueError('calibration marker mismatch')
    if sum(row['Displacement'].values()) != row['AssignedTotal']:
        raise ValueError('displacement denominator mismatch')
    sums = [0.0, 0.0]
    for key, count in row['Displacement'].items():
        delta = [int(x) for x in key.split(',')]
        if len(delta) != 2 or any(abs(x) >= geometry['Count'] for x in delta) or type(count) is not int or count < 0:
            raise ValueError('invalid displacement')
        for axis in range(2):
            angle = math.atan(delta[axis] * geometry['PitchMm'] / geometry['DistanceMm'])
            sums[axis] += count * angle ** 2
    for expected_sum, key in zip(sums, ('SumSquaredAngleX', 'SumSquaredAngleY')):
        actual = row[key]
        # Both event accumulation and regrouped displacement accumulation have a gamma(N+4K) summation bound.
        operations = row['AssignedTotal'] + 4 * len(row['Displacement']) + 16
        mu = operations * 2 ** -53
        bound = 2 * mu / (1 - mu) * max(actual, expected_sum)
        if not math.isfinite(actual) or actual < 0 or abs(actual - expected_sum) > bound:
            raise ValueError('angular sufficient statistics mismatch')


def qualification(rows, geometry, threshold):
    scored = [sum(row['Scored'][c] for row in rows) for c in range(geometry['Count'] ** 2)]
    accepted = [sum(row['Assigned'][c] for row in rows) for c in range(len(scored))]
    populated = all(scored)
    acceptance = [a / s for a, s in zip(accepted, scored)] if populated else []
    ratio = min(acceptance) / statistics.median(acceptance) if acceptance and statistics.median(acceptance) > 0 else None
    rms = []
    for axis in ('X', 'Y'):
        mse = [row['SumSquaredAngle' + axis] / row['AssignedTotal'] for row in rows if row['AssignedTotal']]
        rms.append(math.sqrt(upper(mse)) if len(mse) == len(rows) else None)
    allocation = geometry['PitchMm'] / geometry['DistanceMm'] / math.sqrt(12)
    return {'Qualified': all(row['CalibrationSucceeded'] for row in rows)
            and all(v is not None and v <= allocation for v in rms) and ratio is not None and ratio >= threshold,
            'CalibrationFailures': [i for i, row in enumerate(rows) if not row['CalibrationSucceeded']],
            'Upper95RmsXRad': rms[0], 'Upper95RmsYRad': rms[1], 'AllocationRad': allocation,
            'WeakestAcceptance': min(acceptance) if acceptance else None,
            'MedianAcceptance': statistics.median(acceptance) if acceptance else None, 'WeakestMedianRatio': ratio,
            'Scored': scored, 'Assigned': accepted,
            'Triggered': [sum(row['Triggered'][c] for row in rows) for c in range(len(scored))],
            'Windowed': [sum(row['Windowed'][c] for row in rows) for c in range(len(scored))]}


def aggregate(runs, manifest_path, phase):
    manifest = pv.read_json(manifest_path)
    recipe = pv.read_json(REPO / manifest['Recipe']['File'])
    rule = pv.read_json(REPO / recipe['Rule']['File'])
    if pv.file_sha(REPO / manifest['Recipe']['File']) != manifest['Recipe']['Sha256'] or pv.file_sha(REPO / recipe['Rule']['File']) != recipe['Rule']['Sha256']:
        raise ValueError('recipe/rule pin mismatch')
    seeds = pv.read_json(HERE / 'seeds.json')
    all_prov, matrices, numerical_ok = [], {}, rule.get('ResidualErrorEstablished') is True
    for geometry in recipe['Geometries']:
        family = next(f for f in manifest['families'] if f['Phase'] == phase and f['Geometry'] == geometry['Id'])
        expected_seeds = seeds[family['seeds']]
        if len(expected_seeds) != family['n']:
            raise ValueError('incomplete pinned seed list')
        data = []
        for seed in expected_seeds:
            run = pathlib.Path(runs) / 'runs' / family['id'] / str(seed)
            prov = pv.validate_run(run, family['id'], seed)
            all_prov.append(prov)
            value = json.loads((run / 'stdout.txt').read_text(encoding='utf-8'))
            if (value['Seed'] != seed or value['Geometry'] != geometry['Id'] or value['Phase'] != phase
                    or value['RecipeSha256'] != manifest['Recipe']['Sha256'] or value['RuleSha256'] != recipe['Rule']['Sha256']
                    or value['FamilySha256'] != pv.digest(family)):
                raise ValueError('output identity mismatch')
            executor = prov['Sources'][prov['Inputs'][0]['SourceId']]['Executor']['Files']
            if executor.get('samples/evidence/studio_readout_driver.py') != pv.file_sha(HERE / 'studio_readout_driver.py'):
                raise ValueError('unrecognised recipe driver')
            if phase != 'dev':
                frozen = family.get('Freeze')
                managed_files = {p.removeprefix('managed/probe/'): h for p, h in executor.items() if p.startswith('managed/probe/')}
                if (not frozen or frozen.get('FreezeEligible') is not True or frozen['ManagedClosure'] != managed_files
                        or frozen['RecipeSha256'] != manifest['Recipe']['Sha256'] or frozen['RuleSha256'] != recipe['Rule']['Sha256']):
                    raise ValueError('missing or mismatched frozen development result')
            keys = [(row['EnergyKeV'], row['RateCps']) for row in value['Cells']]
            expected_keys = [(energy, rate) for energy in recipe['EnergiesKeV'] for rate in [0] + recipe['RatesCps']]
            if sorted(keys) != sorted(expected_keys):
                raise ValueError('missing or duplicate condition')
            for row in value['Cells']:
                validate_row(row, geometry, recipe)
            data.append(value)
        matrix = []
        for energy in recipe['EnergiesKeV']:
            for rate in recipe['RatesCps']:
                rows = [next(r for r in value['Cells'] if r['EnergyKeV'] == energy and r['RateCps'] == rate) for value in data]
                matrix.append({'EnergyKeV': energy, 'RateCps': rate, **qualification(rows, geometry, rule['AcceptanceRatio'])})
        numerical = {}
        if phase == 'dev':
            for axis in ('X', 'Y'):
                total = 0.0
                for component in ('Photons', 'Doi', 'Calibration-0', 'Calibration-1', 'Calibration-2'):
                    values = [value['Numerical'].get(component, {}) for value in data]
                    bound = upper([v['MaxAngle' + axis] for v in values]) if all(v.get('Supported') is True for v in values) else None
                    numerical[component + axis] = bound
                    if bound is None:
                        numerical_ok = False
                    elif component in ('Photons', 'Doi'):
                        total += bound
                cal_bounds = [numerical['Calibration-' + str(i) + axis] for i in range(3)]
                if all(v is not None for v in cal_bounds):
                    total += max(cal_bounds)
                numerical['Total' + axis] = total if all(v is not None for v in cal_bounds) else None
                allocation = geometry['PitchMm'] / geometry['DistanceMm'] / math.sqrt(12)
                numerical_ok &= numerical['Total' + axis] is not None and total <= rule['NumericalAllocationFraction'] * allocation
        matrices[geometry['Id']] = {'Conditions': matrix, 'Numerical': numerical}
    provenance = pv.merge(all_prov)
    managed = next(iter(provenance['Sources'].values()))['Executor']['Files']
    managed = {path.removeprefix('managed/probe/'): pin for path, pin in managed.items() if path.startswith('managed/probe/')}
    return {'SchemaVersion': 1, 'Phase': phase, 'RecipeSha256': manifest['Recipe']['Sha256'],
            'RuleSha256': recipe['Rule']['Sha256'], 'QualificationPublished': phase in ('confirm', 'repair'),
            'FreezeEligible': phase == 'dev' and numerical_ok, 'SingleCandidate': True,
            'ManagedClosure': managed, 'Matrices': matrices, 'Provenance': provenance}


def main():
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    parser.add_argument('--runs', required=True)
    parser.add_argument('--manifest', default=str(HERE / 'manifest-studio-readout-v1.json'))
    parser.add_argument('--phase', choices=('dev', 'selection', 'confirm', 'repair'), required=True)
    parser.add_argument('--out', required=True)
    parser.add_argument('--freeze-manifest', help='write a new manifest with the eligible single candidate frozen; never changes the input manifest')
    args = parser.parse_args()
    result = aggregate(args.runs, args.manifest, args.phase)
    result['Provenance'] = pv.summary_provenance([result['Provenance']], __file__,
        argparse.Namespace(Phase=args.phase), {str(pathlib.Path(args.manifest).name): pv.file_sha(args.manifest)})
    pv.publish({args.out: pv.json_bytes(result)}, result['Provenance'])
    if args.freeze_manifest:
        if args.phase != 'dev' or not result['FreezeEligible']:
            raise ValueError('development did not meet the pinned numerical allocation; no freeze')
        manifest = pv.read_json(args.manifest)
        for family in manifest['families']:
            if family['Phase'] != 'dev':
                family['Freeze'] = result
        pv.write_json(args.freeze_manifest, manifest)
    print('complete; development freeze eligible:', result['FreezeEligible'])


if __name__ == '__main__':
    main()
