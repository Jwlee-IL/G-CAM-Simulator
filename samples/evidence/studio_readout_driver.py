"""Pinned TODO-41 adapter, called inside run_seeds.py's immutable staged closure."""
import hashlib
import json
import pathlib
import subprocess
import sys

import provenance as pv


def load_pin(root, pin):
    if not isinstance(pin, dict) or set(pin) != {'File', 'Sha256'} or not pv.safe_path(pin['File']):
        raise ValueError('invalid input pin')
    raw = (root / pin['File']).read_bytes()
    if hashlib.sha256(raw).hexdigest() != pin['Sha256']:
        raise ValueError('input pin mismatch: ' + pin['File'])
    return json.loads(raw)


def main():
    exe, runtime, seed, family_path = sys.argv[1:]
    exe = pathlib.Path(exe)
    root = exe.parents[2]
    family = json.loads(pathlib.Path(family_path).read_text(encoding='utf-8'))
    recipe = load_pin(root, family['Recipe'])
    rule = load_pin(root, recipe['Rule'])
    if rule['Version'] != 1 or family['Geometry'] not in [g['Id'] for g in recipe['Geometries']]:
        raise ValueError('unsupported rule or geometry')
    seeds = json.loads((root / 'samples/evidence/seeds.json').read_text(encoding='utf-8'))
    if int(seed) not in seeds[family['seeds']] or len(seeds[family['seeds']]) != family['n']:
        raise ValueError('seed outside the complete pinned family')
    if family['Phase'] != 'dev':
        frozen = family.get('Freeze')
        if not frozen or frozen.get('FreezeEligible') is not True:
            raise ValueError('development has not frozen a numerically eligible design')
        if frozen['RecipeSha256'] != family['Recipe']['Sha256']:
            raise ValueError('frozen recipe mismatch')
        files = {name: pv.sha(raw) for name, raw in pv.managed_files(exe).items()}
        if frozen['ManagedClosure'] != files:
            raise ValueError('frozen managed binary closure mismatch')
    command = ['dotnet', '--fx-version', runtime, str(exe), 'studio-readout', seed,
               str(root / 'samples'), str(root / family['Recipe']['File']), family['Geometry'], family['Phase'], pv.digest(family)]
    return subprocess.run(command).returncode


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (ValueError, KeyError, OSError) as error:
        print('TODO-41 refused: ' + str(error), file=sys.stderr)
        sys.exit(2)
