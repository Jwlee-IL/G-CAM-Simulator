"""Manifest-driven multi-seed driver for the evidence quotes (TODO-27).

    python samples/evidence/run_seeds.py --out <dir> [--family ID ...] [--n N] [--jobs J] [--force]

For every (family, seed) in manifest.json it creates `<out>/runs/<family>/<seed>/` with an empty `samples/`
folder, and runs the family's recipe there, so commands that write fixed `samples/...` names cannot collide:

  cli    the scenario JSON is cloned COMPLETELY, only its Seed is overwritten (plus the family's declared
         `overrides`), written to config.json and run as `dotnet <Gcam.Cli.dll> [command] config.json`
  probe  `dotnet <Gcam.EvidenceProbe.dll> <mode> <seed> <repo>/samples` (the recipe is in probe/Program.cs)
  rtl    `python rtl_seeds.py <mode> <seed>` (the Python / RTL studies, their own generators)

stdout.txt, stderr.txt and done.json ({exit, seconds, config_sha256, ...}) are written for each run. A run whose
done.json says exit 0 is not repeated unless --force. Build first:

    dotnet build Gcam.sln -c Release
    dotnet build samples/evidence/probe/Gcam.EvidenceProbe.csproj -c Release

The driver refuses to start when a base scenario no longer matches the SHA-256 recorded in manifest.json (the
recorded figures would then describe a different experiment); pass --allow-changed-config to measure anyway.
Aggregate with aggregate.py, which refuses missing or failed runs.
"""
import argparse
import concurrent.futures as cf
import copy
import hashlib
import json
import locale
import os
import pathlib
import subprocess
import sys
import time

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
DEFAULT_CLI = REPO / 'src/Gcam.Cli/bin/Release/net9.0/Gcam.Cli.dll'
DEFAULT_PROBE = HERE / 'probe/bin/Release/net9.0/Gcam.EvidenceProbe.dll'


def sha256(path):
    return hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()


def set_path(obj, dotted, value):
    """Set a dotted key path, matching existing keys case-insensitively (the scenarios use camelCase)."""
    parts = dotted.split('.')
    for i, part in enumerate(parts):
        key = next((k for k in obj if k.lower() == part.lower()), None)
        if i == len(parts) - 1:
            obj[key if key is not None else part] = value
        else:
            if key is None:
                raise SystemExit(f'override path {dotted}: "{part}" not in the scenario')
            obj = obj[key]


def require_validated_spectrum_file(reference):
    """A spectrum named by file (repository-relative path + SHA-256 of its bytes) must match its pin and be validated."""
    file = next((v for k, v in reference.items() if k.lower() == 'file'), None)
    pin = next((v for k, v in reference.items() if k.lower() == 'sha256'), None)
    path = REPO / file
    if not file or not pin or sha256(path) != pin:
        raise SystemExit(f'ambient evidence refused: spectrum file {file} does not match its pinned SHA-256')
    spectrum = json.loads(path.read_text(encoding='utf-8'))
    if spectrum.get('IsValidated') is not True or 'NOT-VALIDATED' in spectrum.get('Id', ''):
        raise SystemExit(f'ambient evidence refused: {file} is not a validated spectrum')


def require_validated_ambient(config):
    """Development spectra must never become numerical evidence through this driver."""
    reference = next((v for k, v in config.items() if k.lower() == 'spectrum'), None)
    if isinstance(reference, dict) and any(k.lower() == 'file' for k in reference):
        require_validated_spectrum_file(reference)       # a gate-study request names its spectrum by file
    ambient = next((v for k, v in config.items() if k.lower() == 'ambient'), None)
    if ambient is None:
        return
    spectrum = next((v for k, v in ambient.items() if k.lower() == 'spectrum'), {})
    validated = next((v for k, v in spectrum.items() if k.lower() == 'isvalidated'), False)
    spectrum_id = next((v for k, v in spectrum.items() if k.lower() == 'id'), '')
    if validated is not True or 'NOT-VALIDATED' in spectrum_id:
        raise SystemExit('ambient evidence refused: incident spectrum is not validated (development placeholder)')
    set_path(config, 'ambient.RequireValidatedSpectrum', True)


def jobs_for(manifest, seeds, families, n_override):
    for fam in manifest['families']:
        if families and fam['id'] not in families:
            continue
        n = n_override if n_override else fam['n']
        first = fam.get('seed_offset', 0)                 # e.g. F128 without its shared first entry 12345
        for seed in seeds[fam['seeds']][first:first + n]:
            yield fam, seed


def run_one(fam, seed, out, cli, probe, python, force):
    run = pathlib.Path(out) / 'runs' / fam['id'] / str(seed)
    done = run / 'done.json'
    if done.exists() and not force and json.loads(done.read_text(encoding='utf-8'))['exit'] == 0:
        return f'skip {fam["id"]} {seed}'
    (run / 'samples').mkdir(parents=True, exist_ok=True)
    record = {'family': fam['id'], 'seed': seed, 'kind': fam['kind']}
    if fam['kind'] == 'cli':
        base = REPO / 'samples' / fam['config']
        config = json.loads(base.read_text(encoding='utf-8'))
        clone = copy.deepcopy(config)
        set_path(clone, 'seed', seed)
        for path, value in fam.get('overrides', {}).items():
            set_path(clone, path, value)
        if fam.get('repo_root'):                          # requests that read repository files by relative path
            set_path(clone, 'RepoRoot', str(REPO))
        require_validated_ambient(clone)
        (run / 'config.json').write_text(json.dumps(clone, indent=1), encoding='utf-8')
        cmd = ['dotnet', str(cli)] + ([fam['command']] if fam.get('command') else []) + [str(run / 'config.json')]
        record['config'] = fam['config']
        record['config_sha256'] = sha256(base)
    elif fam['kind'] == 'probe':
        cmd = ['dotnet', str(probe), fam['mode'], str(seed), str(REPO / 'samples')]
    elif fam['kind'] == 'rtl':
        cmd = [python, str(HERE / 'rtl_seeds.py'), fam['mode'], str(seed), '--cli', str(cli)]
    else:
        raise SystemExit(f'unknown kind {fam["kind"]}')
    def shown(arg):           # repository paths are recorded relative to the repository, others as given
        inside = os.path.isabs(arg) and pathlib.Path(arg).resolve().is_relative_to(REPO)
        return os.path.relpath(arg, REPO).replace('\\', '/') if inside else arg

    record['command'] = [shown(c) for c in cmd]
    env = dict(os.environ, PYTHONUTF8='1', PYTHONIOENCODING='utf-8')
    start = time.perf_counter()
    proc = subprocess.run(cmd, cwd=run, capture_output=True, env=env)
    code = proc.returncode
    # A redirected .NET console writes in the console code page, not UTF-8; store UTF-8 either way.
    for name, data in [('stdout.txt', proc.stdout), ('stderr.txt', proc.stderr)]:
        try:
            decoded, encoding = data.decode('utf-8'), 'utf-8'
        except UnicodeDecodeError:
            encoding = 'oem' if sys.platform == 'win32' else locale.getpreferredencoding(False)
            decoded = data.decode(encoding, errors='replace')
        (run / name).write_text(decoded, encoding='utf-8')
        record[name.replace('.txt', '_encoding')] = encoding
    record['exit'] = code
    record['seconds'] = time.perf_counter() - start
    done.write_text(json.dumps(record), encoding='utf-8')
    return f'{"ok  " if code == 0 else "FAIL"} {fam["id"]} {seed} exit={code} {record["seconds"]:.1f}s'


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--out', required=True, help='output root (runs/<family>/<seed>/ is created below it)')
    ap.add_argument('--family', nargs='*', default=[], help='family ids from manifest.json (default: all)')
    ap.add_argument('--manifest', default=str(HERE / 'manifest.json'), help='versioned manifest; default preserves legacy recipes')
    ap.add_argument('--n', type=int, default=0, help='run only the first N seeds of each family (smoke tests)')
    ap.add_argument('--jobs', type=int, default=max(1, (os.cpu_count() or 2) // 2))
    ap.add_argument('--cli', default=str(DEFAULT_CLI))
    ap.add_argument('--probe', default=str(DEFAULT_PROBE))
    ap.add_argument('--python', default=sys.executable)
    ap.add_argument('--force', action='store_true', help='repeat runs that already succeeded')
    ap.add_argument('--allow-changed-config', action='store_true')
    args = ap.parse_args()
    if args.n < 0:
        raise SystemExit('--n must be positive')

    manifest = json.loads(pathlib.Path(args.manifest).read_text(encoding='utf-8'))
    seeds = json.loads((HERE / 'seeds.json').read_text(encoding='utf-8'))
    unknown = set(args.family) - {f['id'] for f in manifest['families']}
    if unknown:
        raise SystemExit(f'unknown families: {sorted(unknown)}')
    jobs = list(jobs_for(manifest, seeds, set(args.family), args.n))
    changed = sorted({f['config'] for f, _ in jobs if f['kind'] == 'cli'
                      and sha256(REPO / 'samples' / f['config']) != manifest['config_sha256'][f['config']]})
    if changed and not args.allow_changed_config:
        raise SystemExit(f'base scenarios changed since the recorded measurement: {changed}')
    for exe, kinds in [(args.cli, {'cli', 'rtl'}), (args.probe, {'probe'})]:
        if any(f['kind'] in kinds for f, _ in jobs) and not pathlib.Path(exe).exists():
            raise SystemExit(f'{exe} not found — build it first (see the module docstring)')

    out = pathlib.Path(args.out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    head = subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=REPO, capture_output=True, text=True).stdout.strip()
    dirty = subprocess.run(['git', 'status', '--porcelain', '--', 'src', 'samples', 'rtl'], cwd=REPO,
                           capture_output=True, text=True).stdout.strip()
    (out / 'run-info.json').write_text(json.dumps({
        'engine_commit': head, 'engine_tree_dirty': bool(dirty), 'families': sorted({f['id'] for f, _ in jobs}),
        'manifest': pathlib.Path(args.manifest).name, 'manifest_sha256': sha256(args.manifest),
        'n_override': args.n or None, 'jobs': len(jobs), 'started': time.strftime('%Y-%m-%dT%H:%M:%S')}, indent=1),
        encoding='utf-8')
    failures = 0
    with cf.ThreadPoolExecutor(max_workers=args.jobs) as pool:
        futures = [pool.submit(run_one, f, s, out, args.cli, args.probe, args.python, args.force) for f, s in jobs]
        for fut in cf.as_completed(futures):
            line = fut.result()
            failures += line.startswith('FAIL')
            print(line, flush=True)
    print(f'{len(jobs)} runs, {failures} failed')
    return 1 if failures else 0


if __name__ == '__main__':
    sys.exit(main())
