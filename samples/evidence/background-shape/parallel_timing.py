"""Aggregate the two selection-only, 16-worker timing batches without validation verdicts."""
import argparse
import importlib.util
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import provenance as pv


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--main-runs', required=True)
    ap.add_argument('--sensitivity-runs', required=True)
    ap.add_argument('--main-wall-seconds', required=True, type=float)
    ap.add_argument('--sensitivity-wall-seconds', required=True, type=float)
    ap.add_argument('--out', required=True)
    args = ap.parse_args()
    seeds = json.loads((HERE.parent / 'seeds.json').read_bytes())['BG_SELECTION16']
    request = json.loads((HERE / 'request-v2.json').read_bytes())
    spec = importlib.util.spec_from_file_location('background_aggregate', HERE / 'aggregate_background.py')
    aggregate = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(aggregate)
    aggregate.validate_budget(request, json.loads((HERE / 'request-v1.json').read_bytes()),
                              pv.file_sha(HERE / 'request-v1.json'), pv.file_sha(HERE / 'pinned-v1.json'))
    reports, provs = {}, []
    for phase, folder, family, filename, wall, cells in [
        ('main', args.main_runs, 'background_shape_pilot_v2', 'background-shape.json', args.main_wall_seconds, 448),
        ('sensitivity', args.sensitivity_runs, 'background_shape_sensitivity_pilot_v2',
         'background-shape-sensitivity.json', args.sensitivity_wall_seconds, 24)]:
        info = json.loads((pathlib.Path(folder) / 'run-info.json').read_bytes())
        if info['workers'] != 16 or info['jobs'] != 16:
            raise ValueError('timing batch must contain all sixteen selection seeds and sixteen workers')
        provs.append(pv.collect(folder, family, seeds))
        values = [json.loads((pathlib.Path(folder) / 'runs' / family / str(seed) / filename).read_bytes()) for seed in seeds]
        for seed, value in zip(seeds, values):
            if (value['Seed'] != seed or value['Repeats'] != request['PilotRepeats']
                    or value['RequestSha256'] != pv.file_sha(HERE / 'request-v2.json')
                    or value['PinnedSha256'] != pv.file_sha(HERE / 'pinned-v1.json')
                    or value['Iterations'] != [400] or len(value['Cells']) != cells
                    or value['Phase'] != ('pilot' if phase == 'main' else 'sensitivity-pilot')):
                raise ValueError('incompatible timing recipe')
        seconds = [v['TotalSeconds'] for v in values]
        # Two full worker waves. Scaling all setup is conservative under linear throughput, not a time guarantee.
        estimate = wall * (32 / 16) * (100 / request['PilotRepeats'])
        reports[phase] = {'BatchWallSeconds': wall, 'WallSecondsPerCompletedSeed': wall / 16,
                          'SeedElapsedSeconds': seconds, 'SeedElapsedRangeSeconds': [min(seconds), max(seconds)],
                          'ExtrapolatedWallSeconds': estimate, 'ExtrapolatedWallHours': estimate / 3600}
    dependencies = [HERE / name for name in ['aggregate_background.py', 'request-v1.json', 'request-v2.json', 'pinned-v1.json']] + [HERE.parent / 'seeds.json']
    prov = pv.summary_provenance(provs, __file__, args,
                                 {p.relative_to(pv.REPO).as_posix(): pv.file_sha(p) for p in dependencies})
    report = {'SchemaVersion': 2, 'Seeds': seeds, 'Workers': 16, 'PilotRepeats': request['PilotRepeats'],
              'ValidationRepeats': 100, 'ValidationSeeds': 32, 'Iterations': 400,
              'RequestSha256': pv.file_sha(HERE / 'request-v2.json'), 'PinnedSha256': pv.file_sha(HERE / 'pinned-v1.json'),
              'Provenance': prov, 'Timing': reports,
              'Derivation': 'batch wall * two waves * (100 / 4); all setup scaled; descriptive throughput estimate, no timing confidence guarantee'}
    pv.publish({pathlib.Path(args.out): pv.json_bytes(report)}, prov)
    print(json.dumps({k: v for k, v in report.items() if k != 'Provenance'}, indent=1))


if __name__ == '__main__':
    main()
