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
import uuid

import provenance as pv

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


def require_validated_spectrum_file(reference, repo=REPO):
    """A spectrum named by file (repository-relative path + SHA-256 of its bytes) must match its pin and be validated."""
    file = next((v for k, v in reference.items() if k.lower() == 'file'), None)
    pin = next((v for k, v in reference.items() if k.lower() == 'sha256'), None)
    path = repo / file
    if not file or not pin or sha256(path) != pin:
        raise SystemExit(f'ambient evidence refused: spectrum file {file} does not match its pinned SHA-256')
    spectrum = json.loads(path.read_text(encoding='utf-8'))
    if spectrum.get('IsValidated') is not True or 'NOT-VALIDATED' in spectrum.get('Id', ''):
        raise SystemExit(f'ambient evidence refused: {file} is not a validated spectrum')


def require_validated_ambient(config, repo=REPO):
    """Development spectra must never become numerical evidence through this driver."""
    reference = next((v for k, v in config.items() if k.lower() == 'spectrum'), None)
    if isinstance(reference, dict) and any(k.lower() == 'file' for k in reference):
        require_validated_spectrum_file(reference, repo) # a gate-study request names its spectrum by file
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


def run_one(fam, seed, out, prepared, python, force):
    run = pathlib.Path(out) / 'runs' / fam['id'] / str(seed)
    done = run / 'done.json'
    if done.exists() and not force and json.loads(done.read_text(encoding='utf-8'))['exit'] == 0:
        pv.check_reuse(run, prepared, fam['id'], seed)
        return f'skip {fam["id"]} {seed}'
    # Fresh attempt directories prevent cached RTL compilations or old numerical files entering a rerun.
    work = run / 'attempts' / uuid.uuid4().hex
    (work / 'samples').mkdir(parents=True, exist_ok=True)
    snapshot = pv.stage(work, prepared)
    tools = prepared['Source']['Executor']['Tools']
    runtime = tools.get('DotnetRuntime')
    cli = snapshot / 'managed/cli/Gcam.Cli.dll'
    probe = snapshot / 'managed/probe/Gcam.EvidenceProbe.dll'
    record = {'family': fam['id'], 'seed': seed, 'kind': fam['kind']}
    if fam['kind'] == 'cli':
        base = snapshot / 'samples' / fam['config']
        config = json.loads(base.read_text(encoding='utf-8'))
        clone = copy.deepcopy(config)
        set_path(clone, 'seed', seed)
        for path, value in fam.get('overrides', {}).items():
            set_path(clone, path, value)
        if fam.get('repo_root'):                          # requests that read repository files by relative path
            set_path(clone, 'RepoRoot', str(snapshot))
        require_validated_ambient(clone, snapshot)
        pv.write_json(work / 'config.json', clone)
        cmd = ['dotnet', '--fx-version', runtime, str(cli)] + ([fam['command']] if fam.get('command') else []) + [str(work / 'config.json')]
        record['config'] = fam['config']
        record['config_sha256'] = sha256(base)
    elif fam['kind'] == 'probe':
        if fam.get('recipe_driver'):
            pv.write_json(work / 'recipe.json', fam)
            cmd = [python, '-B', str(snapshot / fam['recipe_driver']),
                   str(snapshot / 'managed/probe' / fam['executable']), runtime,
                   str(seed), str(work / 'recipe.json')]
        else:
            cmd = ['dotnet', '--fx-version', runtime, str(probe), fam['mode'], str(seed), str(snapshot / 'samples')]
    elif fam['kind'] == 'rtl':
        cmd = [python, '-B', str(snapshot / 'samples/evidence/rtl_seeds.py'), fam['mode'], str(seed), '--cli', str(cli)]
        if runtime:
            cmd += ['--runtime', runtime]
    else:
        raise SystemExit(f'unknown kind {fam["kind"]}')
    def shown(arg):           # repository paths are recorded relative to the repository, others as given
        inside = os.path.isabs(arg) and pathlib.Path(arg).resolve().is_relative_to(work)
        return os.path.relpath(arg, work).replace('\\', '/') if inside else pathlib.Path(arg).name if os.path.isabs(arg) else arg

    record['command'] = [shown(c) for c in cmd]
    env = dict(os.environ, PYTHONUTF8='1', PYTHONIOENCODING='utf-8', PYTHONDONTWRITEBYTECODE='1')
    start = time.perf_counter()
    proc = subprocess.run(cmd, cwd=work, capture_output=True, env=env)
    code = proc.returncode
    # A redirected .NET console writes in the console code page, not UTF-8; store UTF-8 either way.
    for name, data in [('stdout.txt', proc.stdout), ('stderr.txt', proc.stderr)]:
        try:
            decoded, encoding = data.decode('utf-8'), 'utf-8'
        except UnicodeDecodeError:
            encoding = 'oem' if sys.platform == 'win32' else locale.getpreferredencoding(False)
            decoded = data.decode(encoding, errors='replace')
        (work / name).write_bytes(decoded.replace('\r\n', '\n').encode('utf-8'))
        record[name.replace('.txt', '_encoding')] = encoding
    record['exit'] = code
    record['seconds'] = time.perf_counter() - start
    # Publish only this attempt's outputs at the stable paths understood by existing numerical parsers.
    outputs = pv.output_hashes(work)
    for name in outputs:
        target = run / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes((work / name).read_bytes())
    record['Provenance'] = pv.seed_provenance(prepared, fam['id'], seed, outputs)
    pv.write_json(done, record)
    return f'{"ok  " if code == 0 else "FAIL"} {fam["id"]} {seed} exit={code} {record["seconds"]:.1f}s'


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--out', required=True, help='output root (runs/<family>/<seed>/ is created below it)')
    ap.add_argument('--family', nargs='*', default=[], help='family ids from manifest.json (default: all)')
    ap.add_argument('--manifest', default=str(HERE / 'manifest.json'), help='versioned manifest; default preserves legacy recipes')
    ap.add_argument('--seed-file', default=str(HERE / 'seeds.json'), help='seed lists; default preserves existing families')
    ap.add_argument('--n', type=int, default=0, help='run only the first N seeds of each family (smoke tests)')
    ap.add_argument('--jobs', type=int, default=max(1, (os.cpu_count() or 2) // 2))
    ap.add_argument('--cli', default=str(DEFAULT_CLI))
    ap.add_argument('--probe', default=str(DEFAULT_PROBE))
    ap.add_argument('--python', default=sys.executable)
    ap.add_argument('--force', action='store_true', help='repeat runs that already succeeded')
    ap.add_argument('--allow-changed-config', action='store_true')
    args = ap.parse_args()
    if args.n < 0 or args.jobs < 1:
        raise SystemExit('--n must be nonnegative and --jobs must be positive')

    manifest = json.loads(pathlib.Path(args.manifest).read_text(encoding='utf-8'))
    seeds = json.loads(pathlib.Path(args.seed_file).read_text(encoding='utf-8'))
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
    unique = {f['id']: f for f, _ in jobs}
    prepared = pv.prepare(list(unique.values()), args.cli, args.probe, args.python)
    if not args.allow_changed_config:
        for fam in unique.values():
            if fam['kind'] == 'cli' and pv.sha(prepared[fam['id']]['Files']['samples/' + fam['config']]) != manifest['config_sha256'][fam['config']]:
                pv.refuse(f'{fam["config"]}: captured request no longer matches the manifest')
    # Validate all reusable seeds before changing the invocation index or starting any workers.
    for fam, seed in jobs:
        run = out / 'runs' / fam['id'] / str(seed)
        if not args.force and (run / 'done.json').exists() and pv.read_json(run / 'done.json').get('exit') == 0:
            pv.check_reuse(run, prepared[fam['id']], fam['id'], seed)
    prior = pv.read_json(out / 'run-info.json') if (out / 'run-info.json').exists() else {}
    sources = dict(prior.get('Sources', {})) if prior.get('SchemaVersion') == pv.VERSION else {}
    sources.update({pv.digest(p['Source']): p['Source'] for p in prepared.values()})
    out.mkdir(parents=True, exist_ok=True)
    pv.write_json(out / 'run-info.json', {
        'SchemaVersion': pv.VERSION, 'Sources': sources, 'families': sorted(unique),
        'manifest': pathlib.Path(args.manifest).name, 'manifest_sha256': sha256(args.manifest),
        'n_override': args.n or None, 'jobs': len(jobs), 'workers': args.jobs,
        'started': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())})
    failures = 0
    with cf.ThreadPoolExecutor(max_workers=args.jobs) as pool:
        futures = [pool.submit(run_one, f, s, out, prepared[f['id']], args.python, args.force) for f, s in jobs]
        for fut in cf.as_completed(futures):
            line = fut.result()
            failures += line.startswith('FAIL')
            print(line, flush=True)
    print(f'{len(jobs)} runs, {failures} failed')
    return 1 if failures else 0


if __name__ == '__main__':
    sys.exit(main())
