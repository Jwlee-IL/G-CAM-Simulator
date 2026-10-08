"""Run the unchanged research probe's two search stages in one isolated seed attempt."""
import csv
import json
import pathlib
import subprocess
import sys
import time


def main():
    dll, runtime, seed, recipe = sys.argv[1:]
    dll = pathlib.Path(dll)
    fam = json.loads(pathlib.Path(recipe).read_text(encoding='utf-8'))
    seed = int(seed)
    root = dll.parent
    n = str(fam['histories'])
    nc, kc = str(fam['coarse_histories']), str(fam['coarse_points'])
    def run(args):
        start = time.perf_counter()
        subprocess.run(['dotnet', '--fx-version', runtime, str(dll), *args], check=True)
        return time.perf_counter() - start
    v2 = run(['fit', n, nc, kc, '1', str(seed),
              ';'.join(map(str, fam['depths_mm'])), ';'.join(map(str, fam['angles_mrad'])),
              ';'.join(map(str, fam['live_s'])), 'stage2', '1', '1', '1'])
    v3 = run(['v3', n, nc, kc, str(root / 'fit_stage2.csv'), 'stage3', '1'])
    for name in ['fit_stage2.csv', 'fit_stage3.csv']:
        data = (root / name).read_bytes().replace(b'\r\n', b'\n')
        pathlib.Path(name).write_bytes(data)
        rows = list(csv.DictReader(data.decode('utf-8-sig').splitlines()))
        expected = 2 * len(fam['depths_mm']) * len(fam['angles_mrad']) * len(fam['live_s'])
        keys = {(r['z'], r['a_mrad'], r['live_s'], r['channel']) for r in rows}
        if len(rows) != expected or len(keys) != expected:
            raise SystemExit('incomplete or duplicate fit rows')
    pathlib.Path('timing.json').write_text(json.dumps({'v2_seconds': v2, 'v3_seconds': v3}) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
