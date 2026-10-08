"""Failing-closed TODO-41 development freeze and per-condition qualification, with seed-cluster RMS bounds.

Rule v1 (stage 1) and rule v2 (turn 3: acceptance on the isolated held-out rows, three-level residual-bias bound,
per-line numerical bounds recorded in the freeze) and rule v3 (turn 4: first-interaction truth, the paired statistic
G = RMS_Anger^2 - RMS_direct^2 on the same accepted events, escape-cancelling acceptance, replicate-floor bounds on G) are
read; the rule's Version selects the semantics.

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


def residual(d1, d2, limit):
    """Residual bias of the recipe level from two successive refinement differences (geometric series); None refuses."""
    if d1 == 0:
        return 0.0 if d2 == 0 else None
    r = d2 / d1
    return d1 / (1 - r) if r <= limit else None


def numerical_v2(data, geometry, recipe, rule):
    """Per-line numerical bound of rule v2 from the development outputs of one geometry (all seeds)."""
    allocation = geometry['PitchMm'] / geometry['DistanceMm'] / math.sqrt(12)
    limit, assumed = rule['ContractionLimit'], rule['PhotonContractionAssumed']
    rms = {'AllDeposits': 'Rms', 'SingleCrystal': 'SingleRms'}[rule['GateEvents']]
    out, optics = {}, {}
    for axis in ('X', 'Y'):
        photons = [v['Numerical'].get('Photons', {}) for v in data]
        doi = [v['Numerical'].get('Doi', {}).get('Differences', []) for v in data]
        value = None
        if all(p.get('Supported') is True for p in photons) and all(
                len(d) == 2 and all(x.get('Supported') is True for x in d) for d in doi):
            photon_residual = upper([p['MaxAngle' + axis] for p in photons]) / (1 - assumed)
            doi_residual = residual(upper([d[0]['MaxAngle' + axis] for d in doi]),
                                    upper([d[1]['MaxAngle' + axis] for d in doi]), limit)
            if doi_residual is not None:
                value = photon_residual + doi_residual
            out['PhotonsResidual' + axis], out['DoiResidual' + axis] = photon_residual, doi_residual
        optics[axis] = value
        out['OpticsResidual' + axis] = value
    bounded = {}
    for index, energy in enumerate(recipe['EnergiesKeV']):
        line = {}
        ok = True
        for axis in ('X', 'Y'):
            levels = [v['Numerical'].get('Calibration-' + str(index), {}).get('Levels', []) for v in data]
            supported = all(len(lv) == 3 and all(x.get('Succeeded') is True and x.get(rms + axis) is not None for x in lv)
                            for lv in levels)
            cal = None
            if supported:
                d1 = upper([abs(lv[0][rms + axis] - lv[1][rms + axis]) for lv in levels])
                d2 = upper([abs(lv[1][rms + axis] - lv[2][rms + axis]) for lv in levels])
                cal = residual(d1, d2, limit)
                line['CalibrationD1' + axis], line['CalibrationD2' + axis] = d1, d2
            line['CalibrationResidual' + axis] = cal
            total = None if cal is None or optics[axis] is None else cal + optics[axis]
            line['Total' + axis] = total
            ok &= total is not None and total <= rule['NumericalAllocationFraction'] * allocation
        line['Bounded'] = ok
        bounded[str(energy)] = line
    out['Lines'] = bounded
    out['AllocationRad'] = allocation
    return out


def single_view(row):
    """The row restricted to single-crystal histories (every deposit in one crystal), as qualification() reads it:
    the angular statistic over the assigned single-crystal photopeak histories, and the per-crystal acceptance as their
    identification efficiency (correctly identified / in the photopeak window; theme 68's single-crystal mis-ID per crystal)."""
    single = row['Single']
    return {**row, 'Scored': single['Windowed'], 'Triggered': single['Windowed'], 'Windowed': single['Windowed'],
            'Assigned': single['Correct'], 'AssignedTotal': single['AssignedTotal'],
            'SumSquaredAngleX': single['SumSquaredAngleX'], 'SumSquaredAngleY': single['SumSquaredAngleY']}


def validate_single(row, geometry):
    """Rule v2 rows also carry the single-crystal subset; it must nest inside the all-history denominators."""
    n = geometry['Count'] ** 2
    single = row['Single']
    arrays = [single[k] for k in ('Scored', 'Windowed', 'Assigned', 'Correct')]
    if any(len(a) != n or any(type(x) is not int or x < 0 for x in a) for a in arrays):
        raise ValueError('invalid single-crystal denominators')
    if any(not s >= w >= a >= c for s, w, a, c in zip(*arrays)):
        raise ValueError('non-nested single-crystal denominators')
    if any(s > t for s, t in zip(single['Scored'], row['Scored'])) or any(
            a > t for a, t in zip(single['Assigned'], row['Assigned'])):
        raise ValueError('single-crystal subset exceeds all histories')
    if single['AssignedTotal'] != sum(single['Assigned']):
        raise ValueError('single-crystal assigned total mismatch')
    for key in ('SumSquaredAngleX', 'SumSquaredAngleY'):
        if not math.isfinite(single[key]) or single[key] < 0 or single[key] > row[key] * (1 + 1e-12) + 1e-300:
            raise ValueError('single-crystal angular statistic exceeds all histories')


def qualification_v2(rows, isolated, geometry, threshold, bounded, events='AllDeposits'):
    """Rule v2 cell: spatial bound from the rate rows; per-crystal acceptance from the isolated held-out rows.
    `events` (pinned in the rule) selects all deposited-energy histories or the single-crystal subset."""
    if events == 'SingleCrystal':
        rows, isolated = [single_view(r) for r in rows], [single_view(r) for r in isolated]
    elif events != 'AllDeposits':
        raise ValueError('unknown gate event set')
    result = qualification(rows, geometry, threshold)
    accepted = qualification(isolated, geometry, threshold)
    scored = sum(sum(row['Scored']) for row in rows)
    assigned = sum(row['AssignedTotal'] for row in rows)
    iso_scored = sum(sum(row['Scored']) for row in isolated)
    iso_assigned = sum(row['AssignedTotal'] for row in isolated)
    throughput = assigned / scored if scored else None
    isolated_throughput = iso_assigned / iso_scored if iso_scored else None
    spatial = all(result[k] is not None and result[k] <= result['AllocationRad'] for k in ('Upper95RmsXRad', 'Upper95RmsYRad'))
    calibrated = all(row['CalibrationSucceeded'] for row in rows + isolated)
    ratio = accepted['WeakestMedianRatio']
    result.update({
        'Qualified': calibrated and spatial and ratio is not None and ratio >= threshold and bounded is True,
        'NumericallyBounded': bounded is True,
        'CalibrationFailures': [i for i, row in enumerate(isolated) if not row['CalibrationSucceeded']],
        'WeakestAcceptance': accepted['WeakestAcceptance'], 'MedianAcceptance': accepted['MedianAcceptance'],
        'WeakestMedianRatio': ratio, 'AcceptanceSource': 'isolated held-out rows',
        'GlobalThroughput': throughput, 'IsolatedGlobalAcceptance': isolated_throughput,
        'ThroughputRatio': throughput / isolated_throughput if throughput is not None and isolated_throughput else None,
        'GateEvents': events})
    return result


def upper_signed(values):
    """Student one-sided 95% upper bound of a signed per-seed statistic (equal-weight seeds)."""
    if len(values) not in T95 or any(v is None or not math.isfinite(v) for v in values):
        raise ValueError('unsupported cluster population')
    return statistics.mean(values) + T95[len(values)] * statistics.stdev(values) / math.sqrt(len(values))


def gate_statistic(entry, axis):
    """G = RMS_Anger^2 - RMS_direct^2 on the same accepted events (rad^2); None when nothing was accepted."""
    total = entry['AcceptedTotal']
    if type(total) is not int or total < 0:
        raise ValueError('invalid accepted total')
    if total == 0:
        return None
    return (entry['SumSquaredAnger' + axis] - entry['SumSquaredDirect' + axis]) / total


def validate_row_v3(row, geometry, recipe):
    """Rule v3 row: per first-interaction crystal denominators nest; both displacement matrices reproduce their angular
    sufficient statistics within the gamma(N + 4K) summation bound."""
    n = geometry['Count'] ** 2
    keys = ('Scored', 'FullEnergy', 'Triggered', 'Windowed', 'Accepted', 'AcceptedFullEnergy')
    arrays = {k: row[k] for k in keys}
    if any(len(a) != n or any(type(x) is not int or x < 0 for x in a) for a in arrays.values()):
        raise ValueError('invalid denominators')
    for i in range(n):
        s, f, t, w, a, af = (arrays[k][i] for k in keys)
        if not (s >= t >= w >= a and s >= f and af <= a and af <= f):
            raise ValueError('non-nested denominators')
    expected = n * recipe['ValidationHistoriesPerCrystal'] if row['RateCps'] == 0 else recipe['RateHits']
    if sum(arrays['Scored']) != expected or row['Inputs'] != expected or row['AcceptedTotal'] != sum(arrays['Accepted']):
        raise ValueError('scored/accepted total mismatch')
    if row['CalibrationScored'] != n * recipe['CalibrationHistoriesPerCrystal']:
        raise ValueError('calibration budget mismatch')
    if type(row['CalibrationSucceeded']) is not bool or row['CalibrationSucceeded'] and row['Markers'] != n:
        raise ValueError('calibration marker mismatch')
    assigned_to = row['AssignedToCrystal']
    if len(assigned_to) != n or any(type(x) is not int or x < 0 for x in assigned_to) or sum(assigned_to) != row['AcceptedTotal']:
        raise ValueError('invalid per-crystal assignment counts')
    for name, prefix in (('DisplacementAnger', 'Anger'), ('DisplacementDirect', 'Direct')):
        matrix = row[name]
        if sum(matrix.values()) != row['AcceptedTotal']:
            raise ValueError('displacement denominator mismatch')
        sums = [0.0, 0.0]
        for key, count in matrix.items():
            delta = [int(x) for x in key.split(',')]
            if len(delta) != 2 or any(abs(x) >= geometry['Count'] for x in delta) or type(count) is not int or count < 0:
                raise ValueError('invalid displacement')
            for axis in range(2):
                sums[axis] += count * math.atan(delta[axis] * geometry['PitchMm'] / geometry['DistanceMm']) ** 2
        for expected_sum, axis in zip(sums, ('X', 'Y')):
            actual = row['SumSquared' + prefix + axis]
            operations = row['AcceptedTotal'] + 4 * len(matrix) + 16
            mu = operations * 2 ** -53
            if not math.isfinite(actual) or actual < 0 or abs(actual - expected_sum) > 2 * mu / (1 - mu) * max(actual, expected_sum):
                raise ValueError('angular sufficient statistics mismatch')


def sensitivity_v3(isolated, geometry):
    """Rule v3 acceptance, the per-crystal sensitivity ratio pooled over seeds: windowed Anger events assigned to crystal k
    (LUT crystal c = k) / full-energy histories whose first interaction is in k. A LUT that compresses a crystal and moves
    its counts to a neighbour lowers that crystal's ratio; the crystal's escape cancels (both sides are dominated by
    full-energy events). The numerator also holds the windowed events that are not full-energy (reported)."""
    n = geometry['Count'] ** 2
    full = [sum(r['FullEnergy'][c] for r in isolated) for c in range(n)]
    assigned = [sum(r['AssignedToCrystal'][c] for r in isolated) for c in range(n)]
    if not all(full):
        return None, None, None
    ratios = [a / f for a, f in zip(assigned, full)]
    median = statistics.median(ratios)
    return min(ratios), median, (min(ratios) / median if median > 0 else None)


def acceptance_v3(isolated, geometry):
    """Diagnostic: pooled per first-interaction crystal, accepted full-energy histories / full-energy histories."""
    n = geometry['Count'] ** 2
    full = [sum(r['FullEnergy'][c] for r in isolated) for c in range(n)]
    accepted = [sum(r['AcceptedFullEnergy'][c] for r in isolated) for c in range(n)]
    if not all(full):
        return None, None, None
    ratios = [a / f for a, f in zip(accepted, full)]
    median = statistics.median(ratios)
    return min(ratios), median, (min(ratios) / median if median > 0 else None)


def pooled_matrix(rows, name):
    out = {}
    for row in rows:
        for key, count in row[name].items():
            out[key] = out.get(key, 0) + count
    return dict(sorted(out.items(), key=lambda kv: -kv[1]))


def qualification_v3(rows, isolated, geometry, threshold, bounded):
    """Rule v3 cell: paired spatial statistic on the cell's rows, acceptance on the line's isolated held-out rows."""
    allocation = geometry['PitchMm'] / geometry['DistanceMm'] / math.sqrt(12)
    result = {'AllocationRad': allocation, 'AllocationSquared': allocation ** 2}
    spatial = True
    for axis in ('X', 'Y'):
        values = [gate_statistic(r, axis) for r in rows]
        bound = upper_signed(values) if all(v is not None for v in values) else None
        result['Upper95G' + axis] = bound
        spatial &= bound is not None and bound <= allocation ** 2
        total = sum(r['AcceptedTotal'] for r in rows)
        for est in ('Anger', 'Direct'):
            result['Rms' + est + axis] = math.sqrt(sum(r['SumSquared' + est + axis] for r in rows) / total) if total else None
            result['Bias' + est + axis] = sum(r['Sum' + est + axis] for r in rows) / total if total else None
    weakest, median, ratio = sensitivity_v3(isolated, geometry)
    full_weakest, full_median, full_ratio = acceptance_v3(isolated, geometry)
    accepted_iso = sum(r['AcceptedTotal'] for r in isolated)
    not_full = accepted_iso - sum(sum(r['AcceptedFullEnergy']) for r in isolated)
    calibrated = all(r['CalibrationSucceeded'] for r in rows + isolated)
    inputs = sum(r['Inputs'] for r in rows)
    iso_inputs = sum(r['Inputs'] for r in isolated)
    throughput = sum(r['AcceptedTotal'] for r in rows) / inputs if inputs else None
    iso_throughput = sum(r['AcceptedTotal'] for r in isolated) / iso_inputs if iso_inputs else None
    result.update({
        'Qualified': calibrated and spatial and ratio is not None and ratio >= threshold and bounded is True,
        'SpatialPass': spatial, 'NumericallyBounded': bounded is True,
        'CalibrationFailures': [i for i, r in enumerate(isolated) if not r['CalibrationSucceeded']],
        'WeakestAcceptance': weakest, 'MedianAcceptance': median, 'WeakestMedianRatio': ratio,
        'AcceptanceSource': 'isolated held-out rows: windowed events assigned to crystal k / full-energy histories with f = k',
        'AcceptedNotFullEnergyFraction': not_full / accepted_iso if accepted_iso else None,
        'FullEnergyAcceptance': {'Weakest': full_weakest, 'Median': full_median, 'WeakestMedianRatio': full_ratio},
        'GlobalThroughput': throughput, 'IsolatedThroughput': iso_throughput,
        'ThroughputRatio': throughput / iso_throughput if throughput is not None and iso_throughput else None,
        'DisplacementAnger': pooled_matrix(rows, 'DisplacementAnger'),
        'DisplacementDirect': pooled_matrix(rows, 'DisplacementDirect')})
    return result


def component_bound(d1, d2, floor, limit):
    """Replicate-floor rule: D1 + D2 + F when both differences lie within the floor, else D1 / (1 - r), r <= limit."""
    if d1 <= floor and d2 <= floor:
        return d1 + d2 + floor
    return residual(d1, d2, limit)


def numerical_v3(data, geometry, recipe, rule):
    """Per line and axis, bounds on the gate statistic G from the development refinement levels and replicates."""
    allocation = geometry['PitchMm'] / geometry['DistanceMm'] / math.sqrt(12)
    limit, assumed = rule['ContractionLimit'], rule['PhotonContractionAssumed']
    budget = rule['NumericalAllocationFraction'] * allocation ** 2
    lines = {}
    for index, energy in enumerate(recipe['EnergiesKeV']):
        line = {}
        cal = [v['Numerical'].get('Calibration-' + str(index), {}) for v in data]
        level_ok = [[lv.get('Succeeded') is True and lv.get('AcceptedTotal', 0) > 0 for lv in c.get('Levels', [])] for c in cal]
        line['LevelSucceededAllSeeds'] = [all(seed[k] for seed in level_ok) if all(len(seed) == 3 for seed in level_ok) else False
                                          for k in range(3)]
        line['LargerBudgetCandidate'] = (not line['LevelSucceededAllSeeds'][0]) and any(line['LevelSucceededAllSeeds'][1:])
        entries = []
        for v, c in zip(data, cal):
            num = v['Numerical']
            try:
                entries.append({'cal': c['Levels'], 'calrep': c['Replicate'],
                                'photons': num['Photons']['Lines'][index],
                                'doi': [lv['Lines'][index] for lv in num['Doi']['Levels']],
                                'optrep': num['OpticsReplicate']['Lines'][index]})
            except (KeyError, IndexError, TypeError):
                entries = None
                break
        ok = entries is not None
        if ok:
            for e in entries:
                group = e['cal'] + [e['calrep'], e['photons'], e['optrep']] + e['doi']
                if len(e['cal']) != 3 or len(e['doi']) != 2 or any(x.get('Succeeded') is not True or x.get('AcceptedTotal', 0) <= 0 for x in group):
                    ok = False
        bounded = ok
        for axis in ('X', 'Y'):
            if not ok:
                line['Total' + axis] = None
                continue
            g = lambda x: gate_statistic(x, axis)
            d1 = upper([abs(g(e['cal'][0]) - g(e['cal'][1])) for e in entries])
            d2 = upper([abs(g(e['cal'][1]) - g(e['cal'][2])) for e in entries])
            floor = upper([abs(g(e['cal'][0]) - g(e['calrep'])) for e in entries])
            calibration = component_bound(d1, d2, floor, limit)
            ofloor = upper([abs(g(e['cal'][0]) - g(e['optrep'])) for e in entries])
            p1 = upper([abs(g(e['cal'][0]) - g(e['photons'])) for e in entries])
            photons = p1 + ofloor if p1 <= ofloor else p1 / (1 - assumed)
            o1 = upper([abs(g(e['cal'][0]) - g(e['doi'][0])) for e in entries])
            o2 = upper([abs(g(e['doi'][0]) - g(e['doi'][1])) for e in entries])
            doi = component_bound(o1, o2, ofloor, limit)
            line.update({'CalibrationD1' + axis: d1, 'CalibrationD2' + axis: d2, 'CalibrationFloor' + axis: floor,
                         'Calibration' + axis: calibration, 'OpticsFloor' + axis: ofloor, 'PhotonsD1' + axis: p1,
                         'Photons' + axis: photons, 'DoiD1' + axis: o1, 'DoiD2' + axis: o2, 'Doi' + axis: doi})
            total = None if calibration is None or doi is None else calibration + photons + doi
            line['Total' + axis] = total
            bounded &= total is not None and total <= budget
        line['Bounded'] = bounded
        lines[str(energy)] = line
    return {'Lines': lines, 'AllocationRad': allocation, 'BudgetRad2': budget}


def aggregate(runs, manifest_path, phase):
    manifest = pv.read_json(manifest_path)
    recipe = pv.read_json(REPO / manifest['Recipe']['File'])
    rule = pv.read_json(REPO / recipe['Rule']['File'])
    if pv.file_sha(REPO / manifest['Recipe']['File']) != manifest['Recipe']['Sha256'] or pv.file_sha(REPO / recipe['Rule']['File']) != recipe['Rule']['Sha256']:
        raise ValueError('recipe/rule pin mismatch')
    seeds = pv.read_json(HERE / 'seeds.json')
    if rule.get('Version') not in (1, 2, 3) or recipe.get('Version', 1) != rule['Version']:
        raise ValueError('unsupported rule / recipe version')
    all_prov, matrices = [], {}
    numerical_ok = False if rule['Version'] >= 2 else rule.get('ResidualErrorEstablished') is True
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
                if rule['Version'] == 3:
                    validate_row_v3(row, geometry, recipe)
                    continue
                validate_row(row, geometry, recipe)
                if rule['Version'] == 2:
                    validate_single(row, geometry)
            data.append(value)
        matrix = []
        if rule['Version'] == 3:
            if phase == 'dev':
                numerical = numerical_v3(data, geometry, recipe, rule)
                lines = {e: numerical['Lines'][e]['Bounded'] for e in numerical['Lines']}
                numerical_ok |= any(lines.values())
            else:
                numerical = {}
                lines = family['Freeze']['Matrices'][geometry['Id']]['Numerical']['Lines']
                lines = {e: lines[e]['Bounded'] for e in lines}
            for energy in recipe['EnergiesKeV']:
                isolated = [next(r for r in value['Cells'] if r['EnergyKeV'] == energy and r['RateCps'] == 0) for value in data]
                matrix.append({'EnergyKeV': energy, 'RateCps': 0, 'Isolated': True, **qualification_v3(
                    isolated, isolated, geometry, rule['AcceptanceRatio'], lines.get(str(energy)))})
                for rate in recipe['RatesCps']:
                    rows = [next(r for r in value['Cells'] if r['EnergyKeV'] == energy and r['RateCps'] == rate) for value in data]
                    matrix.append({'EnergyKeV': energy, 'RateCps': rate, **qualification_v3(
                        rows, isolated, geometry, rule['AcceptanceRatio'], lines.get(str(energy)))})
            matrices[geometry['Id']] = {'Conditions': matrix, 'Numerical': numerical}
            continue
        if rule['Version'] == 2:
            if phase == 'dev':
                numerical = numerical_v2(data, geometry, recipe, rule)
                lines = {e: numerical['Lines'][e]['Bounded'] for e in numerical['Lines']}
                numerical_ok |= any(lines.values())
            else:
                numerical = {}
                lines = family['Freeze']['Matrices'][geometry['Id']]['Numerical']['Lines']
                lines = {e: lines[e]['Bounded'] for e in lines}
            for energy in recipe['EnergiesKeV']:
                isolated = [next(r for r in value['Cells'] if r['EnergyKeV'] == energy and r['RateCps'] == 0) for value in data]
                for rate in recipe['RatesCps']:
                    rows = [next(r for r in value['Cells'] if r['EnergyKeV'] == energy and r['RateCps'] == rate) for value in data]
                    gate = rule['GateEvents']
                    other = 'SingleCrystal' if gate == 'AllDeposits' else 'AllDeposits'
                    cell = qualification_v2(rows, isolated, geometry, rule['AcceptanceRatio'], lines.get(str(energy)), gate)
                    # The other event set is reported beside the gate (information only, never gating).
                    alternative = qualification_v2(rows, isolated, geometry, rule['AcceptanceRatio'], lines.get(str(energy)), other)
                    cell['Alternative'] = {k: alternative[k] for k in ('GateEvents', 'Qualified', 'Upper95RmsXRad',
                                                                       'Upper95RmsYRad', 'WeakestMedianRatio')}
                    matrix.append({'EnergyKeV': energy, 'RateCps': rate, **cell})
            matrices[geometry['Id']] = {'Conditions': matrix, 'Numerical': numerical}
            continue
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
