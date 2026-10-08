"""TODO-19 stage 1: aggregate the readout-study families over their outer seeds (stdlib only).

    python samples/evidence/readout/aggregate_readout.py --runs <dir> --family readout_selection_v1 --out <file.json>

Every seed's run is validated through provenance.collect (exit 0, output hashes, root index) before it is read. The probe
writes one JSON document per seed (stdout.txt): all seeds must carry the same request hash, equal to the request file in
this checkout, and the phase / seed list the family names. Every numeric leaf of the per-seed result is flattened to a
key (geometry / readout / trigger / line / metric path) and summarised over seeds: N (seeds with a value), mean, SD,
standard error of the mean, min, max; every {k, n} fraction also gets its pooled k / n. Timing leaves (Seconds,
Microseconds...) are summarised like any other number. The seed is the sampling cluster: SE = SD / sqrt(N).
"""
import argparse
import json
import math
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import provenance as pv

PHASES = {'readout_pilot_v1': ('pilot', 'request-pilot-v1.json'),
          'readout_timing_v1': ('timing', 'request-v1.json'),
          'readout_selection_v1': ('selection', 'request-v1.json'),
          'readout_validation_v1': ('validation', 'request-v1.json'),
          'readout_confirmation_v1': ('confirmation', 'request-confirmation-v1.json'),
          'readout_confirmation_pilot_v1': ('confirmation', 'request-confirmation-v1.json')}


def flatten(node, prefix, out, fractions):
    """Numeric leaves → out[key] = value; {k, n, p} objects → fractions[key] = (k, n). Named list items use their name."""
    if isinstance(node, dict):
        if set(node) == {'k', 'n', 'p'}:
            fractions[prefix] = (node['k'], node['n'])
        for key, value in node.items():
            flatten(value, f'{prefix}/{key}' if prefix else key, out, fractions)
    elif isinstance(node, list):
        for i, item in enumerate(node):
            label = str(i)
            if isinstance(item, dict):
                for name in ('Name', 'Trigger', 'EnergyKeV', 'RateCps'):
                    if name in item and not isinstance(item[name], (dict, list)):
                        label = f'{name}={item[name]}'
                        break
            flatten(item, f'{prefix}[{label}]', out, fractions)
    elif isinstance(node, bool):
        out[prefix] = 1.0 if node else 0.0
    elif isinstance(node, (int, float)):
        out[prefix] = float(node)


def summarise(values):
    n = len(values)
    mean = sum(values) / n
    sd = math.sqrt(sum((v - mean) ** 2 for v in values) / (n - 1)) if n > 1 else None
    return {'N': n, 'Mean': mean, 'SD': sd, 'SE': sd / math.sqrt(n) if sd is not None else None,
            'Min': min(values), 'Max': max(values)}


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--runs', required=True, help='run_seeds.py output root')
    ap.add_argument('--family', required=True, choices=sorted(PHASES))
    ap.add_argument('--manifest', default=str(HERE.parent / 'manifest-readout-v1.json'))
    ap.add_argument('--n', type=int, default=0, help='first N seeds only (pilot)')
    ap.add_argument('--out', required=True)
    args = ap.parse_args()
    manifest = json.loads(pathlib.Path(args.manifest).read_text(encoding='utf-8'))
    family = next(f for f in manifest['families'] if f['id'] == args.family)
    seeds_all = json.loads((HERE.parent / 'seeds.json').read_text(encoding='utf-8'))[family['seeds']]
    seeds = seeds_all[:args.n or family['n']]
    provenance = pv.collect(args.runs, args.family, seeds)
    phase, request_file = PHASES[args.family]
    request_sha = pv.file_sha(HERE / request_file)
    per_seed, per_fraction = {}, {}
    for seed in seeds:
        text = (pathlib.Path(args.runs) / 'runs' / args.family / str(seed) / 'stdout.txt').read_text(encoding='utf-8')
        doc = json.loads(text)
        if doc['Phase'] != phase or doc['Request'] != request_file or doc['RequestSha256'] != request_sha \
                or doc['SeedList'] != family['seeds'] or doc['Result']['Seed'] != seed:
            raise SystemExit(f'{args.family}/{seed}: phase, request, seed list or seed differs from this checkout')
        values, fractions = {}, {}
        flatten(doc['Result'], '', values, fractions)
        for key, value in values.items():
            per_seed.setdefault(key, []).append(value)
        for key, kn in fractions.items():
            per_fraction.setdefault(key, []).append(kn)
    metrics = {key: summarise(v) for key, v in sorted(per_seed.items())}
    for key, kns in per_fraction.items():
        k, n = sum(a for a, _ in kns), sum(b for _, b in kns)
        metrics.setdefault(key, {})['Pooled'] = {'k': k, 'n': n, 'p': k / n if n else None, 'Seeds': len(kns)}
    prov = pv.summary_provenance([provenance], __file__, args,
                                 {p.relative_to(pv.REPO).as_posix(): pv.file_sha(p)
                                  for p in [HERE / request_file, HERE.parent / 'seeds.json', pathlib.Path(args.manifest)]})
    report = {'SchemaVersion': 1, 'Family': args.family, 'Phase': phase, 'Seeds': seeds, 'RequestSha256': request_sha,
              'Provenance': prov, 'Metrics': metrics}
    pv.publish({pathlib.Path(args.out): pv.json_bytes(report)}, prov)
    print(f'{args.family}: {len(seeds)} seeds, {len(metrics)} keys -> {args.out}')


if __name__ == '__main__':
    main()
