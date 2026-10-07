"""AB-12 / TODO-33 baseline: the raw cross-correlation decoder's systematic pull under the absolute ambient field
(TODO-30 turn 8). A finding to measure, not to fix.

    python samples/evidence/ambient/bias_baseline.py --gate <gate aggregate.json> [--sweep <ev01 sweep aggregate.json>] --out <bias.json>

From the gate re-validation (aggregate_gate.py output of family gate_validation_v2, 128 seeds x 300 acquisitions per
condition): for every source condition the signed mean decoder error (estimate - truth at the source plane), averaged
per seed and then over seeds (mean +- SD over seeds), its length in mm and as an angle seen from the detector, the
same for the paired ideal acquisition (no field, same seed and source map), and the excess = |pull(field)| - |pull(ideal)|.
B / S is the expected background over net source counts. From the EV-01 family (optional): the fixed points'
cross-correlation and MLEM pulls at the recipe's 1 MBq x 1 s and the noiseless pull of the expected ambient map.
"""
import argparse
import json
import math
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
import provenance as pv

PLANE_MM = {'lab': 160.0, 'head': 155.0, 'head1m': 1000.0, 'head5m': 5000.0}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--gate', required=True)
    ap.add_argument('--sweep')
    ap.add_argument('--out', required=True)
    args = ap.parse_args()
    gate = json.loads(pathlib.Path(args.gate).read_text(encoding='utf-8'))
    parents = [pv.validate_sidecar(args.gate)]
    pv.pinned_summary(gate)
    inputs = {'Gate': pv.file_sha(args.gate)}
    if args.sweep:
        parents.append(pv.validate_sidecar(args.sweep))
        inputs['Sweep'] = pv.file_sha(args.sweep)
        pv.pinned_summary(pv.read_json(args.sweep))
    prov = pv.summary_provenance(parents, __file__, args, inputs)
    sources = gate['Sources']
    rows = {}
    for key, v in sources.items():
        case, pos, window, t, level, *env = key.split('|')
        env = '|'.join(env)
        if env == 'ideal':          # each field row carries its paired ideal pull
            continue
        if 'MeanDxMm' not in v:
            raise SystemExit('gate aggregate has no signed errors (turn-7 runs); use the turn-8 family')
        ideal = sources[f'{case}|{pos}|{window}|{t}|{level}|ideal']
        plane = PLANE_MM[case]
        pull = math.hypot(v['MeanDxMm'], v['MeanDyMm'])
        pull0 = math.hypot(ideal['MeanDxMm'], ideal['MeanDyMm'])
        rows[key] = {'Case': case, 'Position': pos, 'Window': window, 'Exposure': t, 'Level': level, 'Environment': env,
                     'ExpectedSourceCounts': v['ExpectedSourceCounts'], 'ExpectedBackgroundCounts': v['ExpectedBackgroundCounts'],
                     'BackgroundOverSource': v['ExpectedBackgroundCounts'] / v['ExpectedSourceCounts'],
                     'MeanDxMm': v['MeanDxMm'], 'MeanDyMm': v['MeanDyMm'], 'MeanDxMmSd': v['MeanDxMmSd'], 'MeanDyMmSd': v['MeanDyMmSd'],
                     'PullMm': pull, 'PullDeg': math.degrees(math.atan(pull / plane)),
                     'IdealPullMm': pull0, 'ExcessPullMm': pull - pull0, 'ExcessPullDeg': math.degrees(math.atan(pull / plane)) - math.degrees(math.atan(pull0 / plane)),
                     'RmsErrorMm': v['RmsErrorMmMean'], 'IdealRmsErrorMm': ideal['RmsErrorMmMean'], 'Seeds': v['Seeds']}
    rows = {k: {f: round(x, 5) if isinstance(x, float) else x for f, x in r.items()} for k, r in rows.items()}
    out = {'_about': __doc__.strip().splitlines()[0], 'Gate': {'Family': gate['Family'], 'Seeds': gate['Seeds']}, 'Conditions': rows}
    if args.sweep:
        sweep = json.loads(pathlib.Path(args.sweep).read_text(encoding='utf-8'))
        out['Sweep'] = {'Family': sweep['Family'], 'Seeds': sweep['Seeds'],
                        'Points': {f'{name}|{k}': {'CrossCorrelation': p['CrossCorrelation'], 'Mlem': p['Mlem'],
                                                   'ExpectedSourceCounts': p['ExpectedSourceCounts']['Mean'],
                                                   'ExpectedBackgroundCounts': p['ExpectedBackgroundCounts']['Mean']}
                                   for name, sc in sweep['Summary'].items() for k, p in sc['Points'].items()},
                        'BackgroundPull': {f'{name}|{k}': p for name, sc in sweep['Summary'].items() for k, p in sc['BackgroundPull'].items()}}
    out['Provenance'] = prov
    pv.publish({pathlib.Path(args.out): pv.json_bytes(out)}, prov)
    print(f'{len(rows)} conditions -> {args.out}')


if __name__ == '__main__':
    main()
