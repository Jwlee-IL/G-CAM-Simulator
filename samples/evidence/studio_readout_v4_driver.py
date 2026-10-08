"""Isolated v4 adapter; v1-v3 remain historical records."""
import json
import pathlib
import subprocess
import sys

import provenance as pv


def load_pin(root, pin):
    if not isinstance(pin, dict) or set(pin) != {'File', 'Sha256'} or not pv.safe_path(pin['File']):
        raise ValueError('invalid input pin')
    path = root / pin['File']
    if pv.file_sha(path) != pin['Sha256']:
        raise ValueError('input pin mismatch: ' + pin['File'])
    return json.loads(path.read_text(encoding='utf-8'))


def main():
    exe, runtime, seed, family_path = sys.argv[1:]
    exe = pathlib.Path(exe)
    root = exe.parents[2]
    family = pv.read_json(family_path)
    recipe = load_pin(root, family['Recipe'])
    rule = load_pin(root, recipe['Rule'])
    budget = load_pin(root, recipe['High662BudgetEvidence'])
    if budget['SelectedHistoriesPerCrystal'] != recipe['High662CalibrationHistoriesPerCrystal']:
        raise ValueError('development budget evidence mismatch')
    seeds = pv.read_json(root / 'samples/evidence/seeds.json')
    if recipe['Version'] != 4 or rule['Version'] != 4 or family['Phase'] not in ('dev', 'confirm'):
        raise ValueError('unsupported v4 phase or version')
    if family['Geometry'] not in [g['Id'] for g in recipe['Geometries']]:
        raise ValueError('unknown geometry')
    expected = seeds['T41_DEV4' if family['Phase'] == 'dev' else 'T41_CONFIRM32']
    if seeds[family['seeds']] != expected or family['n'] != len(expected) or int(seed) not in expected:
        raise ValueError('seed outside complete pinned family')
    if recipe['CostSeeds'] != seeds['T41_CONFIRM32'][:8]:
        raise ValueError('cost subset must be the predeclared first eight confirmation seeds')
    frozen = family['Freeze']
    if (frozen['RecipeSha256'] != family['Recipe']['Sha256'] or frozen['RuleSha256'] != recipe['Rule']['Sha256']
            or frozen['ManagedClosure'] != {p: pv.sha(b) for p, b in pv.managed_files(exe).items()}
            or frozen['DriverSha256'] != pv.file_sha(pathlib.Path(__file__))):
        raise ValueError('frozen v4 closure mismatch')
    return subprocess.run(['dotnet', '--fx-version', runtime, str(exe), 'studio-readout-v4', seed,
                           str(root / 'samples'), str(root / family['Recipe']['File']), family['Geometry'],
                           family['Phase'], pv.digest(family)]).returncode


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (ValueError, KeyError, OSError) as error:
        print('TODO-41 v4 refused: ' + str(error), file=sys.stderr)
        sys.exit(2)
