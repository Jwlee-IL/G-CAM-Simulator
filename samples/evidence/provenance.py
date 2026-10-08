"""Versioned, stdlib-only evidence provenance. Historical evidence is never upgraded implicitly."""
import hashlib
import json
import os
import pathlib
import platform
import re
import subprocess
import sys

VERSION = 1
REPO = pathlib.Path(__file__).resolve().parents[2]
SCOPE = ['src', 'samples', 'rtl', '.gitattributes', 'Directory.Build.props',
         'Directory.Build.targets', 'global.json', 'NuGet.Config', 'nuget.config']


def refuse(message):
    raise SystemExit('provenance refused: ' + message)


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False,
                      allow_nan=False).encode('utf-8')


def digest(value):
    return hashlib.sha256(canonical(value)).hexdigest()


def sha(data):
    return hashlib.sha256(data).hexdigest()


def file_sha(path):
    return sha(pathlib.Path(path).read_bytes())


def read_json(path):
    try:
        return json.loads(pathlib.Path(path).read_text(encoding='utf-8-sig'))
    except (OSError, ValueError) as exc:
        refuse(f'{pathlib.Path(path).name}: {exc}')


def json_bytes(value):
    return (json.dumps(value, indent=1, ensure_ascii=False, allow_nan=False) + '\n').encode('utf-8')


def write_json(path, value):
    pathlib.Path(path).write_bytes(json_bytes(value))


def command(args, cwd=None, env=None):
    result = subprocess.run(args, cwd=cwd, env=env, capture_output=True)
    if result.returncode:
        refuse(f'{args[0]} query failed ({result.returncode}): '
               + result.stderr.decode('utf-8', errors='replace').strip())
    return result.stdout


def source_record(repo=REPO, scope=None, env=None):
    scope = SCOPE if scope is None else scope
    def git(*args):
        return command(['git', *args], cwd=repo, env=env)
    commit = git('rev-parse', 'HEAD').decode().strip()
    diff = git('-c', 'core.quotePath=true', '-c', 'diff.algorithm=myers', 'diff',
               '--binary', '--no-ext-diff', '--no-textconv', '--no-renames', '--full-index',
               '--src-prefix=a/', '--dst-prefix=b/', 'HEAD', '--', *scope)
    names = git('ls-files', '-z', '--others', '--exclude-standard', '--', *scope)
    root = pathlib.Path((env or {}).get('GIT_WORK_TREE', repo))
    untracked = {name.decode('utf-8'): file_sha(root / name.decode('utf-8'))
                 for name in names.split(b'\0') if name}
    value = {'Commit': commit, 'Dirty': bool(diff or untracked), 'Scope': scope,
             'DiffSha256': sha(diff), 'Untracked': untracked}
    value['SnapshotSha256'] = digest(value)
    return value


def is_hash(value):
    return isinstance(value, str) and re.fullmatch('[0-9a-f]{64}', value) is not None


def safe_path(value):
    p = pathlib.PurePosixPath(value)
    return bool(value) and '\\' not in value and ':' not in value and not p.is_absolute() and '..' not in p.parts


def hash_map(value):
    return isinstance(value, dict) and all(isinstance(k, str) and safe_path(k) and is_hash(v)
                                          for k, v in value.items())


def validate_source(value):
    if not isinstance(value, dict):
        refuse('missing source record')
    source, executor = value.get('Source'), value.get('Executor')
    if not isinstance(source, dict) or not isinstance(executor, dict):
        refuse('missing source/executor')
    core = {k: v for k, v in source.items() if k != 'SnapshotSha256'}
    if (not isinstance(source.get('Commit'), str)
            or re.fullmatch('[0-9a-f]{40}|[0-9a-f]{64}', source['Commit']) is None
            or type(source.get('Dirty')) is not bool or not isinstance(source.get('Scope'), list)
            or not all(isinstance(p, str) for p in source['Scope'])
            or not is_hash(source.get('DiffSha256')) or not hash_map(source.get('Untracked'))
            or source.get('SnapshotSha256') != digest(core)):
        refuse('invalid source snapshot')
    if (executor.get('Kind') not in ('cli', 'probe', 'rtl') or not hash_map(executor.get('Files'))
            or not executor['Files'] or not isinstance(executor.get('Tools'), dict)
            or not executor['Tools'] or not all(isinstance(v, str) and v for v in executor['Tools'].values())
            or value.get('ExecutionId') != digest(executor)):
        refuse('invalid execution identity')


def validate(prov):
    if not isinstance(prov, dict) or prov.get('SchemaVersion') != VERSION:
        refuse('missing or unsupported schema')
    sources, inputs = prov.get('Sources'), prov.get('Inputs')
    if not isinstance(sources, dict) or not sources or not isinstance(inputs, list) or not inputs:
        refuse('missing sources/inputs')
    for sid, value in sources.items():
        validate_source(value)
        if sid != digest(value):
            refuse('source identifier does not bind its record')
    seen = {}
    for item in inputs:
        if (not isinstance(item, dict) or item.get('SourceId') not in sources
                or not isinstance(item.get('Family'), str) or not safe_path(item['Family'])
                or type(item.get('Seed')) is not int or not is_hash(item.get('RecipeSha256'))
                or not hash_map(item.get('Outputs')) or not item['Outputs']):
            refuse('invalid input binding')
        key = (item['Family'], item['Seed'])
        if key in seen and seen[key] != item:
            refuse(f'conflicting duplicate seed {key}')
        seen[key] = item
    compatible(prov)
    if 'Analysis' in prov:
        a = prov['Analysis']
        if (not isinstance(a, dict) or not hash_map(a.get('Files')) or not a['Files']
                or not isinstance(a.get('Tools'), dict) or not a['Tools']
                or not all(isinstance(v, str) and v for v in a['Tools'].values())
                or not isinstance(a.get('Arguments'), dict) or not hash_map(a.get('InputFiles', {}))):
            refuse('invalid analysis identity')
    return prov


def compatible(prov):
    executions, recipes = {}, {}
    for item in prov['Inputs']:
        src = prov['Sources'][item['SourceId']]
        # RTL modes have different executable closures; each mode is a typed executor.
        key = src['Executor'].get('Variant', src['Executor']['Kind'])
        eid = src['ExecutionId']
        if key in executions and executions[key] != eid:
            refuse(f'mixed execution identity for {key}')
        executions[key] = eid
        family, recipe = item['Family'], item['RecipeSha256']
        if family in recipes and recipes[family] != recipe:
            refuse(f'mixed recipe identity for {family}')
        recipes[family] = recipe


def merge(provs):
    out = {'SchemaVersion': VERSION, 'Sources': {}, 'Inputs': []}
    for p in provs:
        validate(p)
        out['Sources'].update(p['Sources'])
        out['Inputs'].extend(p['Inputs'])
    validate(out)
    unique = {(x['Family'], x['Seed']): x for x in out['Inputs']}
    out['Inputs'] = [unique[k] for k in sorted(unique)]
    return out


def validate_run(run, family=None, seed=None):
    run = pathlib.Path(run)
    done = read_json(run / 'done.json')
    if done.get('exit') != 0:
        refuse(f'{run.name}: unsuccessful run')
    prov = validate(done.get('Provenance'))
    if len(prov['Inputs']) != 1:
        refuse('seed record must bind exactly one input')
    item = prov['Inputs'][0]
    if (done.get('family') != item['Family'] or done.get('seed') != item['Seed']
            or family is not None and item['Family'] != family
            or seed is not None and item['Seed'] != seed):
        refuse('seed/family disagrees with provenance')
    root = read_json(run.parents[2] / 'run-info.json')
    if root.get('SchemaVersion') != VERSION or not isinstance(root.get('Sources'), dict):
        refuse('missing root provenance index')
    for sid, src in prov['Sources'].items():
        if root['Sources'].get(sid) != src:
            refuse('seed source not bound in root index')
    for name, pin in item['Outputs'].items():
        if not (run / name).is_file() or file_sha(run / name) != pin:
            refuse(f'{item["Family"]}/{item["Seed"]}: output hash mismatch: {name}')
    actual = {p.relative_to(run).as_posix() for p in run.rglob('*') if p.is_file()
              and p.name != 'done.json' and not {'attempts', 'snapshot'} & set(p.relative_to(run).parts)}
    if actual != set(item['Outputs']):
        refuse(f'{item["Family"]}/{item["Seed"]}: unbound output files')
    return prov


def collect(runs, family, seeds):
    return merge([validate_run(pathlib.Path(runs) / 'runs' / family / str(s), family, s) for s in seeds])


def from_selected(selected):
    return merge([validate_run(run, family, seed) for family, (_, rows) in selected.items()
                  for seed, run in rows])


def duplicate(a, b):
    pa, pb = validate_run(a), validate_run(b)
    ia, ib = pa['Inputs'][0], pb['Inputs'][0]
    sa, sb = pa['Sources'][ia['SourceId']], pb['Sources'][ib['SourceId']]
    if (sa['ExecutionId'], ia['RecipeSha256'], ia['Outputs']) != (
            sb['ExecutionId'], ib['RecipeSha256'], ib['Outputs']):
        refuse(f'conflicting duplicate seed {ia["Family"]}/{ia["Seed"]}')


def analysis(writer, arguments):
    writer = pathlib.Path(writer)
    paths = [writer, pathlib.Path(__file__)]
    if writer.parent.name == 'ambient':
        paths += list(writer.parent.glob('*.py'))
    tools = {'Python': platform.python_version()}
    # Query installed distributions without importing scientific libraries into stdlib tests.
    import importlib.metadata
    for name in ('numpy', 'scipy'):
        if name in writer.read_text(encoding='utf-8') or writer.parent.name == 'ambient':
            try:
                tools[name] = importlib.metadata.version(name)
            except importlib.metadata.PackageNotFoundError:
                pass
    def portable(value):
        if isinstance(value, str) and os.path.isabs(value):
            path = pathlib.Path(value).resolve()
            return path.relative_to(REPO).as_posix() if path.is_relative_to(REPO) else path.name
        if isinstance(value, list):
            return [portable(v) for v in value]
        return value
    return {'Files': {p.relative_to(REPO).as_posix(): file_sha(p) for p in sorted(set(paths))},
            'Tools': tools, 'Arguments': {k: portable(v) for k, v in vars(arguments).items()
                                        if k not in ('out', 'runs', 'json', 'csv', 'gate', 'sweep')}}


def summary_provenance(provs, writer, arguments, input_files=None):
    value = merge(provs)
    value['Analysis'] = analysis(writer, arguments)
    value['Analysis']['InputFiles'] = input_files or {}
    return value


def publish(outputs, prov):
    """Preflight/serialize all members, then write hash-bound sidecars last (no partial set is valid)."""
    validate(prov)
    members = {pathlib.Path(p): b for p, b in outputs.items()}
    if not members or any(not isinstance(b, bytes) for b in members.values()):
        refuse('invalid output set')
    for p, b in members.items():
        if p.suffix == '.json':
            value = json.loads(b)
            if not isinstance(value, dict) or value.get('Provenance') != prov:
                refuse('JSON envelope disagrees with output-set provenance')
    # A common logical base also supports --json or --csv outside the primary output directory.
    base = pathlib.Path(os.path.commonpath([str(p.resolve().parent) for p in members]))
    pins = {p.resolve().relative_to(base).as_posix(): sha(b) for p, b in members.items()}
    sidecars = {}
    for p in members:
        depth = len(p.resolve().parent.relative_to(base).parts)
        sidecars[p] = json_bytes({'SchemaVersion': VERSION, 'Provenance': prov,
                                  'BaseParents': depth, 'Members': pins})
    for p, b in members.items():
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(b)
    for p in members:
        p.with_name(p.name + '.provenance.json').write_bytes(sidecars[p])


def validate_sidecar(path):
    path = pathlib.Path(path)
    binding = read_json(path.with_name(path.name + '.provenance.json'))
    depth = binding.get('BaseParents')
    if (binding.get('SchemaVersion') != VERSION or not hash_map(binding.get('Members'))
            or type(depth) is not int or depth < 0 or depth > len(path.resolve().parents) - 1):
        refuse('invalid output-set binding')
    base = path.resolve().parent
    for _ in range(depth):
        base = base.parent
    if path.resolve().relative_to(base).as_posix() not in binding['Members']:
        refuse('output missing from its binding')
    for name, pin in binding['Members'].items():
        if not (base / name).is_file() or file_sha(base / name) != pin:
            refuse(f'output-set hash mismatch: {name}')
    prov = validate(binding.get('Provenance'))
    if path.suffix == '.json' and read_json(path).get('Provenance') != prov:
        refuse('JSON envelope disagrees with output-set provenance')
    return prov


def pinned_summary(value):
    """A pinned JSON file binds its own bytes; a sidecar is not needed by CAL generation."""
    return validate(value.get('Provenance'))


def data_rows(value, label, families=None):
    if 'Provenance' not in value:
        return []
    prov = pinned_summary(value)
    out = []
    for sid, src in sorted(prov['Sources'].items()):
        selected = sorted({i['Family'] for i in prov['Inputs'] if i['SourceId'] == sid
                           and (families is None or i['Family'] in families)})
        if not selected:
            continue
        s = src['Source']
        state = (f"dirty tree — diff `{s['DiffSha256']}`; snapshot `{s['SnapshotSha256']}`"
                 if s['Dirty'] else 'clean tree')
        out.append([f'{label} provenance ({", ".join(selected)})',
                    f"commit `{s['Commit']}`; {state}; execution `{src['ExecutionId']}`; "
                    + '; '.join(f'{k} {v}' for k, v in sorted(src['Executor']['Tools'].items()))])
    if 'Analysis' in prov:
        out.append([label + ' analysis identity', f"`{digest(prov['Analysis'])}`"])
    return out


def capture_inputs(repo=REPO):
    """A conservative repository-local closure: scripts, RTL and data, excluding generated result/build trees."""
    names = command(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard',
                     '--', 'samples', 'rtl'], cwd=repo).split(b'\0')
    files = {}
    for raw in names:
        if not raw:
            continue
        name = raw.decode('utf-8')
        p = pathlib.PurePosixPath(name)
        if any(x in p.parts for x in ('bin', 'obj', '__pycache__', 'tests')) or name.startswith('samples/evidence/results/'):
            continue
        if p.suffix.lower() in ('.py', '.sv', '.v', '.json', '.csv', '.dat', '.txt'):
            files[name] = (pathlib.Path(repo) / name).read_bytes()
    return files


def managed_files(exe):
    exe = pathlib.Path(exe).resolve()
    if not exe.is_file():
        refuse(f'{exe.name} not found; build first')
    # Copy the complete output directory, including native/nested dependency files.
    return {p.relative_to(exe.parent).as_posix(): p.read_bytes()
            for p in sorted(exe.parent.rglob('*')) if p.is_file()}


def runtime_version(exe, files):
    config = json.loads(files[pathlib.Path(exe).stem + '.runtimeconfig.json'])
    requested = config['runtimeOptions']['framework']['version']
    prefix = '.'.join(requested.split('.')[:2]) + '.'
    lines = command(['dotnet', '--list-runtimes']).decode().splitlines()
    versions = [line.split()[1] for line in lines if line.startswith('Microsoft.NETCore.App ')
                and line.split()[1].startswith(prefix)]
    if not versions:
        refuse('required .NET runtime is unavailable')
    return max(versions, key=lambda v: tuple(map(int, v.split('.'))))


def prepare(families, cli, probe, python, repo=REPO):
    """Capture once before launching workers; no running child reads the original repository closure."""
    inputs = capture_inputs(repo)
    source = source_record(repo)
    git = command(['git', '--version'], cwd=repo).decode().strip()
    prepared = {}
    managed = {}
    for kind, exe in [('cli', cli), ('probe', probe)]:
        if any(f['kind'] == kind or kind == 'cli' and f['kind'] == 'rtl' and f['mode'] == 'frontend' for f in families):
            files = managed_files(exe)
            managed[kind] = (pathlib.Path(exe).name, files, runtime_version(exe, files))
    for fam in families:
        files = dict(inputs)
        tools = {'OS': platform.system(), 'Architecture': platform.machine(),
                 'DriverPython': platform.python_version()}
        kind = fam['kind']
        executable = {p: sha(inputs[p]) for p in ('samples/evidence/run_seeds.py', 'samples/evidence/provenance.py')}
        if kind in managed or kind == 'rtl' and fam['mode'] == 'frontend':
            mkind = 'cli' if kind == 'rtl' else kind
            name, dlls, runtime = managed[mkind]
            files.update({f'managed/{mkind}/{p}': b for p, b in dlls.items()})
            executable.update({f'managed/{mkind}/{p}': sha(b) for p, b in dlls.items()})
            tools['DotnetRuntime'] = runtime
        if fam.get('recipe_driver'):
            driver = fam['recipe_driver']
            if kind != 'probe' or not safe_path(driver) or driver not in inputs:
                refuse('invalid probe recipe driver')
            executable[driver] = sha(inputs[driver])
        if kind == 'rtl':
            # Local imports and AST-loaded studies keep their original repository-relative layout.
            executable.update({p: sha(b) for p, b in inputs.items() if p.endswith(('.py', '.sv', '.v'))})
            info = json.loads(command([python, '-B', '-c',
                'import json,platform,importlib.metadata; print(json.dumps({"Python":platform.python_version(),'
                '"numpy":importlib.metadata.version("numpy")}))']).decode())
            tools.update(info)
            if fam['mode'] in ('material', 'peak', 'pixel', 'multi'):
                for tool in ('iverilog', 'vvp'):
                    # Icarus emits some version details on stderr; bind the complete successful response.
                    res = subprocess.run([tool, '-V'], capture_output=True)
                    if res.returncode:
                        refuse(f'{tool} version query failed')
                    lines = (res.stdout + res.stderr).decode(errors='replace').splitlines()
                    tools[tool] = next((line.strip() for line in lines if 'version' in line.lower()), tool + ' unknown')
        executor = {'Kind': kind, 'Files': executable, 'Tools': tools}
        if kind == 'rtl':
            executor['Variant'] = 'rtl/' + fam['mode']
        src = {'Source': source, 'Executor': executor, 'ExecutionId': digest(executor),
               'BuildObservation': {'Git': git}}
        if kind != 'rtl' or fam['mode'] == 'frontend':
            src['BuildObservation']['DotnetSdk'] = command(['dotnet', '--version']).decode().strip()
        # Inputs are frozen separately from executable equality; different families legitimately use different data.
        data = {p: sha(b) for p, b in inputs.items() if not p.endswith(('.py', '.sv', '.v'))}
        prepared[fam['id']] = {'Files': files, 'Source': src, 'Recipe': digest({'Family': fam, 'Data': data})}
    return prepared


def stage(run, prepared):
    root = pathlib.Path(run) / 'snapshot'
    for name, data in prepared['Files'].items():
        if not safe_path(name):
            refuse('unsafe snapshot path')
        target = root / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    return root


def output_hashes(run):
    run = pathlib.Path(run)
    return {p.relative_to(run).as_posix(): file_sha(p) for p in sorted(run.rglob('*'))
            if p.is_file() and p.name != 'done.json' and 'snapshot' not in p.relative_to(run).parts}


def seed_provenance(prepared, family, seed, outputs):
    src = prepared['Source']
    return {'SchemaVersion': VERSION, 'Sources': {digest(src): src}, 'Inputs': [
        {'Family': family, 'Seed': seed, 'SourceId': digest(src),
         'RecipeSha256': prepared['Recipe'], 'Outputs': outputs}]}


def check_reuse(run, prepared, family, seed):
    prov = validate_run(run, family, seed)
    item = prov['Inputs'][0]
    source = prov['Sources'][item['SourceId']]
    if source['ExecutionId'] != prepared['Source']['ExecutionId'] or item['RecipeSha256'] != prepared['Recipe']:
        refuse(f'{family}/{seed}: reuse identity changed; use --force or a new output root')
    return prov
