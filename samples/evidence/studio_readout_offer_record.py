"""Generate / check Studio's read-only readout offer and imaging-cost record (TODO-41, SD-A13).

The record never re-judges published evidence. It is a deterministic reduction of the two published parents
(CONFIRM32 v4 `confirmation-v4.json` and the declared REPAIR32 v5 `repair-v5.json`):

* each parent is kept as a projection: the parent JSON without its `Provenance` envelope (bound instead by the
  publisher sidecar's hash and the provenance digest) and without the per-seed `SensitivityMapsPerSeed` arrays
  (diagnostic maps Studio does not display; the declared weakest / median scalar stays);
* `Cells` is the declared overlay: every v4 cell, with exactly Wide 661.7 and High 661.7 replaced by their v5 cells
  (the superseded v4 outcome stays in `Superseded`).

The committed file is itself the reduced, hash-chained archive: full parent SHA-256 and byte count -> sidecar SHA-256
and provenance digest -> projection digest -> record bytes -> the SHA-256 compiled into Studio (ReadoutOfferPin.cs).

Modes:
  --parents CONFIRMATION REPAIR           regenerate the record from the published parents (writes it)
  --check --parents CONFIRMATION REPAIR   byte-for-byte regeneration from the parents (full derivation check)
  --check                                 parents absent: verify the record against its recorded hashes and rebuild
                                          the derived cells from the embedded projections (integrity / consistency)
"""
import argparse
import copy
import hashlib
import json
import pathlib
import re
import sys

import provenance as pv

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
DATA = HERE / 'studio-readout'
OUTPUT = DATA / 'offer-cost-v1.json'
PIN_SOURCE = REPO / 'src' / 'Gcam.Studio.Services' / 'ReadoutOfferPin.cs'
KIND = 'gcam.studio.readout-offer-cost'
PARENTS = {
    'confirmation-v4.json': {'Sha256': '165280179215db6170e19dacb001d2c6b7724ad84fdd44e1c0b118f998fcc8ad',
                             'Bytes': 9288846, 'Version': 4, 'Phase': 'confirm'},
    'repair-v5.json': {'Sha256': '4dc2a598a108ac8974b35a76c0ec884da70ac72afe3559a8ce67edb2bdfb3d61',
                       'Bytes': 5522986, 'Version': 5, 'Phase': 'repair'},
}
GEOMETRIES = ('Baseline', 'Sharp', 'Wide', 'High')
LINES = (122.06, 661.7, 1332.5)
REPAIRED = (('Wide', 661.7), ('High', 661.7))
REDUCTION = ('Projection = parent JSON without its Provenance envelope (bound by the publisher sidecar SHA-256 and '
             'the provenance digest) and without the per-seed SensitivityMapsPerSeed diagnostic arrays; every other '
             'value is copied verbatim. Cells = v4 cells with exactly Wide/661.7 and High/661.7 replaced by the v5 '
             'repair cells; the superseded v4 outcome is kept in Superseded.')
# Benchmark fields Studio displays; identical in both recipes (checked).
BENCHMARK_FIELDS = ('FocalDistanceMm', 'PositionHalfFraction', 'Positions', 'Decoder', 'FloodCorrection',
                    'DirectReferenceCounts', 'RatesCps', 'TrialsPerPosition', 'ExposurePilotHistories', 'Exposure',
                    'OpticalPhotons', 'DepthBins', 'HistogramBinsPerCrystal', 'ValidationHistoriesPerCrystal')


class RecordError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise RecordError(message)


def encode(value):
    return (json.dumps(value, indent=1, sort_keys=True, ensure_ascii=True, allow_nan=False) + '\n').encode('ascii')


def project(parent):
    value = {k: copy.deepcopy(v) for k, v in parent.items() if k != 'Provenance'}
    for cells in value.get('Matrices', {}).values():
        for cell in cells:
            for diagnostics in (cell.get('Diagnostics') or {}).values():
                if isinstance(diagnostics, dict):
                    diagnostics.pop('SensitivityMapsPerSeed', None)
    return value


def parent_entry(path):
    path = pathlib.Path(path)
    pin = PARENTS.get(path.name)
    require(pin is not None, f'{path.name}: not a pinned parent')
    data = path.read_bytes()
    require(pv.sha(data) == pin['Sha256'] and len(data) == pin['Bytes'], f'{path.name}: published parent pin mismatch')
    prov = pv.validate_sidecar(path)  # output-set binding, provenance schema and envelope agreement
    sidecar = path.with_name(path.name + '.provenance.json')
    projection = project(json.loads(data.decode('utf-8')))
    return {'Name': path.name, 'Sha256': pin['Sha256'], 'Bytes': pin['Bytes'],
            'SidecarSha256': pv.file_sha(sidecar), 'ProvenanceDigest': pv.digest(prov),
            'ProjectionSha256': pv.digest(projection), 'Projection': projection}


def check_entry(entry):
    name = entry.get('Name')
    pin = PARENTS.get(name)
    require(pin is not None, f'unknown parent {name!r}')
    require(entry.get('Sha256') == pin['Sha256'] and entry.get('Bytes') == pin['Bytes'], f'{name}: parent pin mismatch')
    require(pv.is_hash(entry.get('SidecarSha256')) and pv.is_hash(entry.get('ProvenanceDigest')),
            f'{name}: sidecar binding missing')
    projection = entry.get('Projection')
    require(isinstance(projection, dict) and pv.digest(projection) == entry.get('ProjectionSha256'),
            f'{name}: projection digest mismatch')
    recipe = DATA / f'recipe-v{pin["Version"]}.json'
    rule = DATA / f'rule-v{pin["Version"]}.json'
    require(projection.get('SchemaVersion') == pin['Version'] and projection.get('Phase') == pin['Phase']
            and projection.get('QualificationPublished') is True
            and projection.get('RecipeSha256') == pv.file_sha(recipe)
            and projection.get('RuleSha256') == pv.file_sha(rule), f'{name}: publication / recipe / rule mismatch')
    return projection


def interval(value, clusters):
    require(isinstance(value, dict) and value.get('Clusters') == clusters
            and all(isinstance(value.get(k), (int, float)) for k in ('Estimate', 'Lower95', 'Upper95')),
            'invalid interval')


def check_cell(cell):
    require(cell.get('CalibrationSeeds') == 32 and type(cell.get('Offered')) is bool
            and isinstance(cell.get('Failures'), list)
            and cell['Offered'] == (len(cell['Failures']) == 0)
            and cell.get('Label') == ('experimental' if cell['Offered'] else 'refused'), 'corrupt calibration population')
    for failure in cell['Failures']:
        require(type(failure.get('Seed')) is int and isinstance(failure.get('Failure'), str) and failure['Failure'],
                'corrupt literal failure')
    rows = cell.get('CostTable')
    require(isinstance(rows, list) and sorted((r['TargetDirectCounts'], r['RateCps']) for r in rows)
            == [(1000, 0), (1000, 100000), (4000, 0), (4000, 100000)], 'invalid benchmark exposure set')
    for row in rows:
        paths = row.get('Paths', {})
        require(set(paths) == {'Direct', 'Anger'}, 'invalid cost paths')
        for path in paths.values():
            if path.get('Available'):
                for key in ('RmsDeg', 'BiasDeg', 'RemoteProbability', 'AcceptedCounts'):
                    interval(path.get(key), 8)
        if paths['Direct'].get('Available') and paths['Anger'].get('Available'):
            interval(row.get('F'), 8)
        else:
            require(row.get('F') is None, 'F without both paths')
    diagnostics = cell.get('Diagnostics')
    require(isinstance(diagnostics, dict) and set(diagnostics) == {'0', '100000'}, 'invalid diagnostics rates')
    for value in diagnostics.values():
        require('SensitivityMapsPerSeed' not in value, 'per-seed maps must not be shipped')
        if value.get('Available'):
            for key in ('GXRad2', 'GYRad2', 'SingleMisId', 'MultiCrystalShare', 'WeakestMedianSensitivity'):
                interval(value.get(key), 32)


def build(entries):
    entries = sorted(entries, key=lambda e: e['Name'])
    require([e['Name'] for e in entries] == sorted(PARENTS), 'both pinned parents are required, once each')
    projections = {e['Name']: check_entry(e) for e in entries}
    base, repair = projections['confirmation-v4.json'], projections['repair-v5.json']
    recipe4 = pv.read_json(DATA / 'recipe-v4.json')
    recipe5 = pv.read_json(DATA / 'recipe-v5.json')
    for key in BENCHMARK_FIELDS:
        require(recipe4.get(key) == recipe5.get(key), f'recipe benchmark field differs: {key}')
    for key in ('FMeaning', 'IntervalMethod', 'IntervalCaution'):
        require(base.get(key) == repair.get(key) and isinstance(base.get(key), str), f'parent wording differs: {key}')
    matrices = base.get('Matrices')
    require(isinstance(matrices, dict) and set(matrices) == set(GEOMETRIES), 'invalid geometry population')
    require(isinstance(repair.get('Matrices'), dict) and set(repair['Matrices']) == {g for g, _ in REPAIRED},
            'invalid repair geometry population')
    v5 = {g['Id']: g for g in recipe5['Geometries']}
    cells = []
    for geometry in GEOMETRIES:
        rows = matrices[geometry]
        require(sorted(c.get('EnergyKeV') for c in rows) == list(LINES), f'{geometry}: incomplete line matrix')
        for line in LINES:
            source = next(c for c in rows if c['EnergyKeV'] == line)
            check_cell(source)
            budget = (recipe4['High662CalibrationHistoriesPerCrystal'] if (geometry, line) == ('High', 661.7)
                      else recipe4['CalibrationHistoriesPerCrystal'])
            cell = copy.deepcopy(source)
            cell.update(Geometry=geometry, SourceRecord='confirmation-v4.json', SourcePhase='confirm', Budget=budget)
            if (geometry, line) in REPAIRED:
                replacement = repair['Matrices'][geometry]
                require(len(replacement) == 1 and replacement[0].get('EnergyKeV') == line, 'invalid repair scope')
                check_cell(replacement[0])
                superseded = {k: cell[k] for k in ('Offered', 'Label', 'CalibrationSeeds', 'Failures', 'Budget',
                                                    'SourceRecord', 'SourcePhase')}
                cell = copy.deepcopy(replacement[0])
                cell.update(Geometry=geometry, SourceRecord='repair-v5.json', SourcePhase='repair',
                            Budget=v5[geometry]['CalibrationHistoriesPerCrystal'], Superseded=superseded)
            cells.append(cell)
    return {
        'SchemaVersion': 1, 'Kind': KIND, 'Reduction': REDUCTION,
        'Parents': [{k: e[k] for k in ('Name', 'Sha256', 'Bytes', 'SidecarSha256', 'ProvenanceDigest',
                                        'ProjectionSha256', 'Projection')} for e in entries],
        'Geometries': recipe4['Geometries'],
        'Benchmark': {**{k: recipe4[k] for k in BENCHMARK_FIELDS}, 'WindowFraction': 0.15,
                      'CostSeeds': len(recipe4['CostSeeds']), 'CalibrationSeeds': 32},
        'FMeaning': base['FMeaning'], 'IntervalMethod': base['IntervalMethod'],
        'IntervalCaution': base['IntervalCaution'], 'Cells': cells}


def compiled_pin():
    text = PIN_SOURCE.read_text(encoding='utf-8')
    match = re.search(r'public const string Sha256 = "([0-9a-f]{64})";', text)
    require(match is not None, 'compiled pin not found')
    return match.group(1)


def check(parents=None):
    shipped = OUTPUT.read_bytes()
    if parents:
        entries = [parent_entry(p) for p in parents]
        mode = 'parents: full byte-for-byte derivation from the published parents and their sidecars'
    else:
        record = json.loads(shipped)
        require(record.get('SchemaVersion') == 1 and record.get('Kind') == KIND, 'invalid record schema')
        entries = record.get('Parents')
        require(isinstance(entries, list), 'missing parents')
        mode = 'reduced: recorded hashes and embedded projections (parents absent)'
    require(encode(build(entries)) == shipped, 'shipped record differs from byte-for-byte regeneration')
    digest = hashlib.sha256(shipped).hexdigest()
    require(compiled_pin() == digest, 'Studio compiled pin differs from the record SHA-256')
    return mode, digest


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--parents', nargs=2, metavar=('CONFIRMATION', 'REPAIR'))
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args(argv)
    try:
        if args.check:
            mode, digest = check(args.parents)
            print(f'ok ({mode}); {OUTPUT.name} {len(OUTPUT.read_bytes())} bytes, sha256 {digest}')
            return 0
        if not args.parents:
            parser.error('generation needs --parents CONFIRMATION REPAIR')
        data = encode(build([parent_entry(p) for p in args.parents]))
        OUTPUT.write_bytes(data)
        print(f'wrote {OUTPUT.name} {len(data)} bytes, sha256 {hashlib.sha256(data).hexdigest()}')
        return 0
    except RecordError as exc:
        print(f'refused: {exc}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
