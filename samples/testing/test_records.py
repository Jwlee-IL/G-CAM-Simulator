"""Portable test records: collect, generate, check, check-catalog, self-test.

Records are evidence of an invocation, not a human approval. Archived replay checks
sanitised runner results and hashes; historical binaries are identified, not rebuilt.
"""
import argparse
import ast
import collections
import copy
import datetime as dt
import hashlib
import gzip
import io
import json
import math
import os
from pathlib import Path
import platform
import re
import subprocess
import sys
import tempfile
import uuid
import zlib
import xml.etree.ElementTree as ET

from adapters import trx_cases, unittest_cases, cocotb_cases

REPO = Path(__file__).resolve().parents[2]
CATALOG = REPO / 'docs/VV.Tests.md'
SWITCHES = ('GCAM_EVIDENCE_TESTS', 'GCAM_RENDER_SNAPSHOTS', 'GCAM_UI_TESTS',
            'GCAM_UI_BREAK_VERDICT', 'GCAM_README_CAPTURE_ONLY', 'GCAM_CRRC_VECTORS',
            'GCAM_PROVENANCE_GIT_TESTS', 'GCAM_RENDER_OUTPUT')
PROJECTS = tuple(sorted(p.stem for p in (REPO / 'tests').glob('*/*.csproj')))
SCOPE = ('src/', 'tests/', 'rtl/', 'samples/', '.github/workflows/',
         'Directory.Build.', 'Gcam.sln', 'global.json', 'NuGet.Config', '.gitattributes')
GAPS = ['background-shape numerical tests require NumPy/SciPy (not in default CI)',
        'RTL C# vector comparison requires an explicitly supplied vector export',
        'desktop, render and numerical-evidence opt-ins are absent in the normal profile']
GENERIC_ACCOUNTS = {'runner', 'root', 'user', 'admin', 'administrator'}


def canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False,
                       allow_nan=False) + '\n').encode('utf-8')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def gzip_bytes(raw):
    """Reproducible gzip: no filename, zero timestamp, fixed compression level."""
    output = io.BytesIO()
    with gzip.GzipFile(filename='', mode='wb', fileobj=output, mtime=0, compresslevel=9) as stream:
        stream.write(raw)
    return output.getvalue()


def evidence_exists(path):
    return Path(path).is_file() or Path(str(path) + '.gz').is_file()


def evidence_bytes(path, archive_root=None):
    """Logical evidence paths survive compression; hashes pin expanded original bytes."""
    path = Path(path)
    require(not (path.is_file() and Path(str(path) + '.gz').is_file()), 'archive: ambiguous evidence path')
    if path.is_file():
        return path.read_bytes()
    stored = Path(str(path) + '.gz').read_bytes()
    require(stored[:10] == b'\x1f\x8b\x08\x00\x00\x00\x00\x00\x02\xff', 'archive: noncanonical gzip header')
    try:
        raw = gzip.decompress(stored)
    except (OSError, EOFError, zlib.error) as error:
        raise ValueError('archive: corrupt gzip') from error
    if path.suffix == '.json':
        document = json.loads(raw)
        if isinstance(document, dict) and document.get('archiveEncoding') == 'manifest-pool-v1':
            require(archive_root is not None, 'archive: missing pool root')
            pool_path = Path(archive_root) / 'manifests.json'
            require(evidence_exists(pool_path), 'archive: missing manifest pool')
            pool = json.loads(evidence_bytes(pool_path))
            private_free(canonical(pool))
            require(all(sha(canonical(value)) == key for key, value in pool.items()), 'archive: manifest hash mismatch')
            value = document['document']
            for owner in ('source', 'generatorSource', 'subject'):
                for field in ('files', 'postFiles'):
                    if owner in value and field in value[owner]:
                        reference = value[owner][field]
                        if isinstance(reference, dict) and set(reference) == {'manifestRef'}:
                            require(reference['manifestRef'] in pool, 'archive: missing manifest reference')
                            value[owner][field] = pool[reference['manifestRef']]
            raw = canonical(value)
    return raw


def digest_file(path, archive_root=None):
    return sha(evidence_bytes(path, archive_root))


def write(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data if isinstance(data, bytes) else data.encode('utf-8'))


def json_read(path, archive_root=None):
    return json.loads(evidence_bytes(path, archive_root))


def require(condition, code):
    if not condition:
        raise ValueError(code)


def command(args, env=None, cwd=REPO):
    result = subprocess.run([str(a) for a in args], cwd=cwd, env=env, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT)
    return result.returncode, result.stdout.decode('utf-8', errors='replace')


def git(*args):
    code, output = command(['git', *args])
    require(code == 0, 'source: git query failed')
    return output.strip()


def source_identity():
    names = git('ls-files', '--cached', '--others', '--exclude-standard').splitlines()
    files = {}
    for name in sorted(set(names)):
        if name.startswith('samples/testing/records/'):
            continue
        if any(name.startswith(prefix) for prefix in SCOPE) and (REPO / name).is_file():
            # Canonical checkout text: Linux LF and Windows CRLF represent the same source.
            raw = (REPO / name).read_bytes()
            if b'\x00' not in raw:
                raw = raw.replace(b'\r\n', b'\n')
            files[name] = sha(raw)
    require(files, 'source: empty manifest')
    commit = git('rev-parse', 'HEAD')
    require(re.fullmatch(r'[0-9a-f]{40}', commit), 'source: invalid commit')
    return {'commit': commit, 'dirty': bool(git('status', '--porcelain')),
            'files': files, 'snapshotSha256': sha(canonical(files)), 'textNormalization': 'CRLF-to-LF'}


def sanitize_text(text, extra=()):
    replacements = [(str(REPO), '<repo>'), (tempfile.gettempdir(), '%TEMP%')]
    for original, token in replacements:
        for spelling in {original, original.replace('\\', '/'), original.replace('/', '\\')}:
            text = re.sub(re.escape(spelling), lambda _: token, text, flags=re.I)
    # Explicit identifiers from the raw record and process; never retain host/user identities.
    identifiers = list(extra) + [os.environ.get('USERNAME'), os.environ.get('USER'), platform.node()]
    for identifier in sorted({i for i in identifiers if i and not i.startswith('<') and i.lower() not in GENERIC_ACCOUNTS}, key=len, reverse=True):
        text = re.sub(r'(?<!\w)' + re.escape(identifier) + r'(?!\w)', '<identity>', text, flags=re.I)
    text = re.sub(r'(?<![A-Za-z])[A-Za-z]:[\\/][^\r\n<>"\']*', '<path>', text)
    text = re.sub(r'/(?:home|Users|private|tmp|opt|usr|var)/[^\r\n<>"\']*', '<path>', text)
    return text.replace('\r\n', '\n').replace('\r', '\n')


def private_free(data):
    text = data.decode('utf-8') if isinstance(data, bytes) else data
    require(not re.search(r'(?<![A-Za-z])[A-Za-z]:[\\/]|/(?:home|Users|private|tmp)/', text), 'privacy: absolute path')
    # Generic account words occur in schema/prose (e.g. "runner"). Sensitive XML
    # slots are unconditionally tokenised, regardless of account spelling.
    searchable = re.sub(r'<[^>]+>|&lt;[^&]+&gt;', '', text)
    for value in (os.environ.get('USERNAME'), os.environ.get('USER'), platform.node()):
        if value and value.lower() not in GENERIC_ACCOUNTS:
            require(not re.search(r'(?<!\w)' + re.escape(value) + r'(?!\w)', searchable, re.I), 'privacy: identity')


def sanitize_trx(data):
    root = ET.fromstring(data)
    extra = []
    for element in root.iter():
        for key, value in list(element.attrib.items()):
            if key == 'computerName':
                extra.append(value)
            if key == 'runUser':
                extra.extend(value.split('\\'))
    for element in root.iter():
        for key, value in list(element.attrib.items()):
            if key in ('computerName', 'runUser', 'runDeploymentRoot'):
                element.set(key, '<' + key + '>')
            elif element is root and key == 'name':
                element.set(key, '<run-name>')
            else:
                element.set(key, sanitize_text(value, extra))
        if element.text:
            element.text = sanitize_text(element.text, extra)
        if element.tail:
            element.tail = sanitize_text(element.tail, extra)
    # Canonical XML and normalised LF give identical replay bytes independent of attribute ordering.
    result = (ET.canonicalize(ET.tostring(root, encoding='unicode'), strip_text=False) + '\n').encode('utf-8')
    private_free(result)
    return result


def tokens(args):
    return [sanitize_text(str(a)) for a in args]


def sanitize_value(value):
    if isinstance(value, str):
        return sanitize_text(value)
    if isinstance(value, list):
        return [sanitize_value(v) for v in value]
    if isinstance(value, dict):
        return {k: sanitize_value(v) for k, v in value.items()}
    return value


def version(args):
    try:
        code, output = command(args)
    except OSError:
        return 'unavailable'
    return sanitize_text(output.strip()) if code == 0 else 'unavailable'


def environment(env):
    return {'os': platform.system(), 'osRelease': platform.release(), 'architecture': platform.machine(),
            'python': platform.python_version(), 'runnerLabel': '<runner>',
            'processors': os.cpu_count(), 'timezone': dt.datetime.now().astimezone().utcoffset().total_seconds(),
            'culture': env.get('LANG', 'system-default'),
            'switches': {key: sanitize_text(env[key]) if key in env else None for key in SWITCHES}}


def binaries(project):
    files = {}
    for path in (REPO / 'tests' / project / 'bin/Release').glob('*/*'):
        if path.is_file() and path.suffix in ('.dll', '.json', '.exe'):
            files[path.relative_to(REPO).as_posix()] = digest_file(path)
    return files


def source_units():
    """Source method membership; expanded discovery is independently required for xUnit."""
    units = {}
    pieces = {}
    for path in sorted((REPO / 'tests').rglob('*.cs')):
        if 'bin' in path.parts or 'obj' in path.parts:
            continue
        text = path.read_text(encoding='utf-8-sig')
        namespace = re.search(r'namespace\s+([\w.]+)', text)
        cls = re.search(r'public\s+(?:(?:sealed|partial|abstract)\s+)*class\s+(\w+)', text)
        if not namespace or not cls:
            continue
        methods = []
        pattern = r'\[((?:Fact|Theory|EvidenceFact|DesktopFact|RenderSnapshotFact)[^\]]*)\]((?:(?!\bpublic\b).)*?)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\s*\('
        for match in re.finditer(pattern, text, re.S):
            methods.append(match[3])
        assembly = path.relative_to(REPO).parts[1]
        full = namespace[1] + '.' + cls[1]
        unit = assembly + '::' + full
        pieces.setdefault(unit, {})[path.relative_to(REPO).as_posix()] = sha(text.replace('\r\n', '\n').encode())
        if not methods:
            continue
        entry = units.setdefault(unit, {'runner': 'dotnet', 'sources': {}, 'methods': []})
        entry['sources'][path.relative_to(REPO).as_posix()] = sha(text.replace('\r\n', '\n').encode())
        entry['methods'].extend(full + '.' + method for method in methods)
    for unit_id, entry in units.items():
        entry['sources'].update(pieces[unit_id])
        project = REPO / 'tests' / unit_id.split('::')[0]
        helpers = list((project / 'Harness').glob('*.cs')) + list(project.glob('*FactAttribute.cs'))
        for path in helpers:
            entry['sources'][path.relative_to(REPO).as_posix()] = sha(path.read_bytes().replace(b'\r\n', b'\n'))
    for path in sorted((REPO / 'samples/evidence/tests').glob('test_*.py')):
        tree = ast.parse(path.read_text(encoding='utf-8-sig'))
        for cls in (n for n in tree.body if isinstance(n, ast.ClassDef)):
            methods = [n.name for n in cls.body if isinstance(n, ast.FunctionDef) and n.name.startswith('test_')]
            if methods:
                unit = 'unittest::' + path.stem + '.' + cls.name
                units[unit] = {'runner': 'unittest', 'methods': [path.stem + '.' + cls.name + '.' + m for m in methods],
                               'sources': {path.relative_to(REPO).as_posix(): sha(path.read_bytes().replace(b'\r\n', b'\n'))}}
    # The configuration is part of the test identity, not a suffix discarded by aggregation.
    sys.path.insert(0, str(REPO / 'rtl'))
    from crrc_contract import fixtures
    configs = [('test_trap_shaper', t) for t in ('trapezoidal_shaper', 'trapezoidal_shaper_pl')]
    configs += [('test_blr', 'baseline_restorer')]
    configs += [('test_crrc', f'{name}-f{fraction}') for name in fixtures() for fraction in (0, 12)]
    for module, config in configs:
        path = REPO / 'rtl' / (module + '.py')
        methods = re.findall(r'@cocotb.test\([^\n]*\)\s*async def (\w+)', path.read_text(encoding='utf-8'))
        unit = f'cocotb::{module}@{config}'
        units[unit] = {'runner': 'cocotb', 'methods': [module + '@' + config + '.' + method for method in methods],
                       'sources': {path.relative_to(REPO).as_posix(): sha(path.read_bytes().replace(b'\r\n', b'\n')),
                                   'rtl/crrc_contract.py': sha((REPO / 'rtl/crrc_contract.py').read_bytes().replace(b'\r\n', b'\n')),
                                   'rtl/run_cocotb.py': sha((REPO / 'rtl/run_cocotb.py').read_bytes().replace(b'\r\n', b'\n'))}}
    for name, path in [('calibration', 'samples/evidence/calibration_record.py'),
                       ('test-record-self-test', 'samples/testing/test_records.py'),
                       ('test-catalog', 'samples/testing/test_records.py'),
                       ('archived-replay', 'samples/testing/test_records.py')]:
        units['checker::' + name] = {'runner': 'checker', 'methods': [name + '.' + name],
                                    'sources': {path: sha((REPO / path).read_bytes().replace(b'\r\n', b'\n'))}}
    return units


def catalog(path=CATALOG, verify_sources=True):
    text = Path(path).read_text(encoding='utf-8')
    entries = [json.loads(m) for m in re.findall(r'```json\n(.*?)\n```', text, re.S)]
    units = {}
    for entry in entries:
        require(entry['id'] not in units, 'catalog: duplicate unit')
        for field in ('runner', 'methods', 'sources', 'trace'):
            require(field in entry, 'catalog: missing ' + field)
        # Relative names are exact selectors qualified by this entry's unique id.
        entry['methods'] = [m if '::' in m else entry['id'] + '.' + m for m in entry['methods']]
        for mapping in entry['trace']:
            mapping['selectors'] = [m if '::' in m else entry['id'] + '.' + m for m in mapping['selectors']]
        require(entry['methods'], 'catalog: no methods')
        require(all(m.startswith(entry['id'] + '.') for m in entry['methods']), 'catalog: invalid selector')
        units[entry['id']] = entry
    if verify_sources:
        actual = source_units()
        require(set(actual) == set(units), 'catalog: missing or stale unit')
        for key, unit in actual.items():
            require(units[key]['sources'] == unit['sources'], 'catalog: source changed ' + key)
            expected = {key.split('::')[0] + '::' + method for method in unit['methods']}
            require(set(units[key]['methods']) == expected, 'catalog: missing or stale method ' + key)
        requirement_ids = set()
        for path in (REPO / 'docs').glob('VV.*.md'):
            if path.name.startswith('VV.Tests'):
                continue
            requirement_ids.update(re.findall(r'\b(?:SR-[A-Z]+-\d+|EV-\d+|VAL-\d+)\b', path.read_text(encoding='utf-8-sig')))
        for entry in units.values():
            for trace in entry['trace']:
                require(trace['requirement'] in requirement_ids, 'catalog: unknown requirement')
                require(set(trace['selectors']) <= set(entry['methods']), 'catalog: unresolved trace selector')
    return units


def discover(project, env):
    args = ['dotnet', 'test', 'tests/' + project, '-c', 'Release', '--no-build', '--list-tests']
    code, output = command(args, env)
    names = sorted(line.strip() for line in output.splitlines() if re.match(r'^\s{4}Gcam\.', line))
    require(code == 0 and names, 'discovery: failed or empty ' + project)
    require(len(names) == len(set(names)), 'discovery: duplicate case')
    return names, output


def discovery_block(units, names):
    lines = ['| Unit | Methods | Discovered cases |', '|---|---:|---:|']
    for key, unit in sorted(units.items()):
        cls = key.split('::')[1]
        count = sum(name.startswith(cls + '.') for name in names) if unit['runner'] == 'dotnet' else len(unit['methods'])
        lines.append(f'| `{key}` | {len(unit["methods"])} | {count} |')
    return '\n'.join(lines)


def replace_block(text, name, body):
    begin, end = f'<!-- BEGIN GENERATED: {name} -->', f'<!-- END GENERATED: {name} -->'
    require(text.count(begin) == 1 and text.count(end) == 1, 'markers: missing or duplicate ' + name)
    pattern = re.escape(begin) + r'.*?' + re.escape(end)
    require(re.search(pattern, text, re.S), 'markers: reversed ' + name)
    return re.sub(pattern, lambda _: begin + '\n' + body + '\n' + end, text, flags=re.S)


def check_catalog(discover_now=False, update=False):
    units = catalog()
    names = []
    if discover_now:
        env = os.environ.copy()
        env.update({k: '0' for k in ('GCAM_UI_TESTS', 'GCAM_RENDER_SNAPSHOTS', 'GCAM_EVIDENCE_TESTS')})
        env['DOTNET_CLI_UI_LANGUAGE'] = 'en'
        for project in PROJECTS:
            found, _ = discover(project, env)
            names.extend(found)
        actual_classes = {name.split('(')[0].rsplit('.', 1)[0] for name in names}
        # Theory names include argument text; class identity is the qualified prefix before method.
        expected = {key.split('::')[1] for key in units if units[key]['runner'] == 'dotnet'}
        require(actual_classes == expected, 'discovery: catalog class mismatch')
        actual_methods = {name.split('(')[0] for name in names}
        expected_methods = {method.split('::')[1] for entry in units.values() if entry['runner'] == 'dotnet'
                            for method in entry['methods']}
        require(actual_methods == expected_methods, 'discovery: catalog method mismatch')
        current = CATALOG.read_text(encoding='utf-8')
        generated = replace_block(current, 'TEST DISCOVERY', discovery_block(units, names))
        if update:
            write(CATALOG, generated)
        else:
            require(generated == current, 'catalog: discovery block stale')
    print(f'catalog: {len(units)} units, {sum(e.get("incomplete", False) for e in units.values())} incomplete descriptions')
    return units


def append_selection(bundle, record_path, record):
    selection_path = bundle / 'selection.json'
    selection = json_read(selection_path) if selection_path.exists() else {'schemaVersion': 1, 'records': [], 'caseSelections': {}}
    relative = record_path.relative_to(bundle).as_posix()
    selection['records'].append({'path': relative, 'sha256': digest_file(record_path), 'runId': record['runId']})
    repeats = 0
    for case in record['cases']:
        if record['profile'] != 'normal':
            continue
        if case['id'] in selection['caseSelections']:
            repeats += 1
        else:
            selection['caseSelections'][case['id']] = record['runId']
    write(selection_path, canonical(selection))
    if repeats:
        print(f'selection: {repeats} repeated cases retained; existing selections unchanged (edit manifest to choose)')


def finish_record(bundle, folder, family, args, env, before_source, before_subject, after_subject,
                  started, finished, exit_code, cases, expected, artifacts, diagnostics, tools):
    require(before_subject == after_subject, 'subject: changed during execution')
    after_source = source_identity()
    require(before_source['snapshotSha256'] == after_source['snapshotSha256'], 'source: changed during execution')
    missing = sorted(set(expected) - {case['id'] for case in cases})
    reasons = []
    if before_source['dirty'] or after_source['dirty']:
        reasons.append('working_tree_dirty')
    if any(v == 'unavailable' for v in tools.values()):
        reasons.append('tool_identity_incomplete')
    record = {'schemaVersion': 1, 'runId': folder.name, 'family': family, 'profile': 'normal',
              'startedUtc': started, 'finishedUtc': finished, 'command': tokens(args), 'exitCode': exit_code,
              'source': before_source, 'subject': {'files': before_subject, 'postFiles': after_subject},
              'tool': tools, 'environment': environment(env), 'cases': sorted(cases, key=lambda c: c['id']),
              'expectedCases': sorted(expected), 'missingCases': missing, 'diagnostics': diagnostics,
              'artifacts': artifacts, 'provenance': 'draft' if reasons else 'formal', 'draftReasons': reasons,
              'sanitization': {'version': 1, 'xml': 'canonical XML; LF; identity/path tokens',
                               'rawSha256Retained': True}, 'binaryVerification': 'before-and-after collection; hash-only on replay'}
    if env.get('GCAM_UI_BREAK_VERDICT') == '1' or env.get('GCAM_README_CAPTURE_ONLY') == '1':
        record['profile'] = 'fault-injection-or-capture'
    raw = canonical(record)
    private_free(raw)
    record_path = folder / 'run-record.json'
    write(record_path, raw)
    append_selection(bundle, record_path, record)
    return record


def utc():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def collect(bundle, family, no_build=False, include_background=False, checker=None, python_command=None):
    bundle = Path(bundle).resolve()
    env = os.environ.copy()
    env['PYTHONDONTWRITEBYTECODE'] = '1'
    env['DOTNET_CLI_UI_LANGUAGE'] = 'en'
    if family == 'dotnet':
        env.update({key: '0' for key in ('GCAM_EVIDENCE_TESTS', 'GCAM_RENDER_SNAPSHOTS', 'GCAM_UI_TESTS',
                                        'GCAM_UI_BREAK_VERDICT', 'GCAM_README_CAPTURE_ONLY')})
        env.pop('GCAM_CRRC_VECTORS', None)
        if not no_build:
            code, output = command(['dotnet', 'build', 'Gcam.sln', '-c', 'Release'], env)
            write(bundle / 'build.log', sanitize_text(output))
            require(code == 0, 'build: failed')
    names = PROJECTS if family == 'dotnet' else (family,)
    exit_codes = []
    for name in names:
        folder = bundle / family / (name + '-' + uuid.uuid4().hex)
        folder.mkdir(parents=True, exist_ok=False)
        started = utc()
        before_source = source_identity()
        subject = binaries(name) if family == 'dotnet' else {}
        cases, expected, diagnostics, artifacts = [], [], [], []
        tools = {'collectorSha256': digest_file(Path(__file__)), 'python': platform.python_version()}
        if family == 'dotnet':
            require(subject, 'subject: no built binaries')
            found, listing = discover(name, env)
            write(folder / 'discovery.log', sanitize_text(listing))
            expected = [name + '::' + display for display in found]
            args = ['dotnet', 'test', 'tests/' + name, '-c', 'Release', '--no-build',
                    '--logger', 'trx;LogFileName=results.trx', '--results-directory', str(folder)]
            tools.update({'sdk': version(['dotnet', '--version']),
                          'runtime': version(['dotnet', '--list-runtimes'])})
        elif family == 'unittest':
            env['GCAM_PROVENANCE_TEST_ROOT'] = str(folder / 'fixtures')
            args = [sys.executable, '-B', 'samples/testing/unittest_adapter.py', '--start', 'samples/evidence/tests',
                    '--pattern', 'test_*.py' if include_background else 'test_provenance*.py', '--out', str(folder / 'unittest.json')]
            expected = [method for unit, e in source_units().items() if e['runner'] == 'unittest'
                        and (include_background or 'test_provenance.' in unit) for method in (unit.split('::')[0] + '::' + m for m in e['methods'])]
            if include_background:
                tools['numericalPackages'] = version([sys.executable, '-B', '-c', 'import numpy,scipy; print(numpy.__version__,scipy.__version__)'])
        elif family == 'cocotb':
            interpreter = python_command or [sys.executable]
            args = [*interpreter, '-B', 'rtl/run_cocotb.py', '--output-root', str(folder / 'rtl')]
            expected = [unit.split('::')[0] + '::' + method for unit, e in source_units().items() if e['runner'] == 'cocotb' for method in e['methods']]
            tools.update({'icarus': version(['iverilog', '-V']), 'vvp': version(['vvp', '-V']),
                          'cocotb': version([*interpreter, '-B', '-c', 'import cocotb; print(cocotb.__version__)']),
                          'pythonCommand': [sanitize_text(part) for part in interpreter],
                          'executionPython': version([*interpreter, '-B', '-c',
                              'import platform; print(platform.python_implementation(), platform.python_version())'])})
        else:
            require(checker in ('calibration', 'test-record-self-test', 'test-catalog', 'archived-replay'), 'checker: unknown')
            args = {'calibration': [sys.executable, '-B', 'samples/evidence/calibration_record.py', '--release'],
                    'test-record-self-test': [sys.executable, '-B', str(Path(__file__)), 'self-test', '--out', str(folder / 'fixtures')],
                    'test-catalog': [sys.executable, '-B', str(Path(__file__)), 'check-catalog'],
                    'archived-replay': [sys.executable, '-B', str(Path(__file__)), 'check', '--archives']}[checker]
            expected = ['checker::' + checker + '.' + checker]
        # Source/data are the execution subject for interpreted checks and RTL inputs.
        if family != 'dotnet':
            prefixes = ('rtl/',) if family == 'cocotb' else ('samples/evidence/', 'samples/testing/')
            subject = {path: value for path, value in before_source['files'].items()
                       if path.startswith(prefixes)}
        post_subject = dict(subject)
        try:
            if family == 'cocotb' and any(value == 'unavailable' for value in tools.values()):
                exit_code, output = 127, 'cocotb toolchain unavailable; runner was not started'
            else:
                exit_code, output = command(args, env)
        except OSError as error:
            exit_code, output = 127, str(error)
        write(folder / 'runner.log', sanitize_text(output))
        if family == 'dotnet':
            for key, pattern in [('vstest', r'VSTest version ([\w.]+)'),
                                 ('xunitAdapter', r'xUnit.net VSTest Adapter v([\w.+-]+)'),
                                 ('actualRuntime', r'\(64-bit (\.NET [\w.]+)\)')]:
                match = re.search(pattern, output)
                tools[key] = match[1] if match else 'unavailable'
            for path in (REPO / 'tests' / name / 'bin/Release').glob('*/*.deps.json'):
                deps = json_read(path)
                tools['testPackages'] = ', '.join(sorted(key for key in deps.get('libraries', {})
                                                       if key.startswith(('xunit', 'Microsoft.NET.Test.Sdk/'))))
        files = list(folder.glob('*.trx')) if family == 'dotnet' else list(folder.glob('unittest.json')) if family == 'unittest' else list(folder.glob('rtl/*/results.xml')) if family == 'cocotb' else []
        for path in sorted(files):
            original = path.read_bytes()
            if family == 'dotnet':
                original_xml = ET.fromstring(original)
                tool_output = '\n'.join(original_xml.itertext())
                for key, pattern in [('xunitAdapter', r'xUnit.net VSTest Adapter v([\w.+-]+)'),
                                     ('actualRuntime', r'\(64-bit (\.NET [\w.]+)\)')]:
                    match = re.search(pattern, tool_output)
                    if match:
                        tools[key] = match[1]
                sanitized = sanitize_trx(original)
            elif family == 'unittest':
                sanitized = canonical(sanitize_value(json.loads(original)))
            else:
                sanitized = sanitize_trx(original)
            write(path, sanitized)
            artifact = {'path': path.relative_to(folder).as_posix(), 'sha256': sha(sanitized),
                        'rawSha256': sha(original), 'bytes': len(sanitized), 'kind': family}
            if family == 'cocotb':
                metadata = json_read(path.parent / 'configuration.json')
                artifact['configuration'], artifact['module'] = metadata['name'], metadata['module']
                metadata_bytes = canonical(sanitize_value(metadata))
                write(path.parent / 'configuration.json', metadata_bytes)
                artifacts.append({'path': (path.parent / 'configuration.json').relative_to(folder).as_posix(),
                                  'sha256': sha(metadata_bytes), 'rawSha256': sha(metadata_bytes),
                                  'bytes': len(metadata_bytes), 'kind': 'configuration'})
            artifacts.append(artifact)
            try:
                parsed, _ = (trx_cases(sanitized, environment(env)['switches']) if family == 'dotnet' else
                             unittest_cases(sanitized, environment(env)['switches']) if family == 'unittest' else
                             cocotb_cases(sanitized, artifact['configuration'], artifact['module'], environment(env)['switches']))
                cases.extend(parsed)
                if family == 'dotnet':
                    summary = original_xml.find('t:ResultSummary/t:Counters', {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'})
                    if summary is not None and int(summary.get('notExecuted', 0)) != sum(c['verdict'] == 'not_executed' for c in parsed):
                        diagnostics.append('TRX summary notExecuted differs; case outcomes are authoritative')
            except (ValueError, ET.ParseError, KeyError) as error:
                diagnostics.append(str(error))
        if family == 'checker':
            case = {'id': expected[0], 'unit': 'checker::' + checker, 'selector': expected[0], 'displayName': checker,
                    'rawOutcome': str(exit_code), 'verdict': 'passed' if exit_code == 0 else 'failed',
                    'failureKind': None if exit_code == 0 else 'unknown',
                    'durationSeconds': (dt.datetime.fromisoformat(utc()) - dt.datetime.fromisoformat(started)).total_seconds(),
                    'reasonText': ''}
            cases.append(case)
            invocation = canonical({'schemaVersion': 1, 'cases': cases, 'exitCode': exit_code})
            write(folder / 'invocation.json', invocation)
            artifacts.append({'path': 'invocation.json', 'sha256': sha(invocation), 'rawSha256': sha(invocation),
                              'bytes': len(invocation), 'kind': 'checker'})
        if family == 'cocotb':
            retained_paths = {a['path'] for a in artifacts}
            for path in sorted(folder.glob('rtl/*/configuration.json')):
                metadata = json_read(path)
                if 'simulationBeforeSha256' in metadata:
                    key = 'rtl-build/' + platform.system() + '/' + metadata['name'] + '/sim.vvp'
                    subject[key] = metadata['simulationBeforeSha256']
                    post_subject[key] = metadata.get('simulationAfterSha256')
                relative = path.relative_to(folder).as_posix()
                if relative not in retained_paths:
                    raw = canonical(sanitize_value(json_read(path)))
                    write(path, raw)
                    artifacts.append({'path': relative, 'sha256': sha(raw), 'rawSha256': sha(raw),
                                      'bytes': len(raw), 'kind': 'configuration'})
        if exit_code:
            diagnostics.append('runner exit ' + str(exit_code))
        for path in sorted(folder.glob('*.log')):
            data = path.read_bytes()
            private_free(data)
            artifacts.append({'path': path.name, 'sha256': sha(data), 'rawSha256': sha(data),
                              'bytes': len(data), 'kind': 'log'})
        record = finish_record(bundle, folder, family, args, env, before_source, subject,
                               binaries(name) if family == 'dotnet' else post_subject, started, utc(), exit_code,
                               cases, expected, artifacts, diagnostics, tools)
        exit_codes.append(exit_code)
        print(f'{family}/{name}: {dict(collections.Counter(c["verdict"] for c in record["cases"]))}, missing={len(record["missingCases"])}')
    return 1 if any(exit_codes) else 0


def validate_record(record, folder, archive_root=None):
    require(record.get('schemaVersion') == 1, 'record: unsupported schema')
    require(re.fullmatch(r'[0-9a-f]{40}', record['source']['commit']), 'record: invalid commit')
    require(all(re.fullmatch(r'[0-9a-f]{64}', h) for h in record['source']['files'].values()), 'record: invalid source hash')
    require(sha(canonical(record['source']['files'])) == record['source']['snapshotSha256'], 'record: source hash mismatch')
    require(record['subject']['files'] == record['subject']['postFiles'], 'record: subject changed')
    if 'tool' in record:
        require(record['tool'] and record['environment'].get('os'), 'record: missing identification')
    require(record['provenance'] in ('formal', 'draft'), 'record: invalid provenance')
    require(not (record['provenance'] == 'formal' and (record['source']['dirty'] or record['draftReasons'])), 'record: invalid formal claim')
    private_free(canonical(record))
    parsed = []
    for artifact in record['artifacts']:
        path = (folder / artifact['path']).resolve()
        require(path.is_relative_to(folder.resolve()), 'artifact: path escape')
        require(evidence_exists(path), 'artifact: missing')
        raw = evidence_bytes(path, archive_root)
        require(sha(raw) == artifact['sha256'] and len(raw) == artifact['bytes'], 'artifact: hash mismatch')
        private_free(raw)
        switches = record['environment']['switches']
        if artifact['kind'] == 'dotnet':
            require(sanitize_trx(raw) == raw, 'artifact: noncanonical sanitized TRX')
            rows, _ = trx_cases(raw, switches)
        elif artifact['kind'] == 'unittest':
            rows, _ = unittest_cases(raw, switches)
        elif artifact['kind'] == 'cocotb':
            rows, _ = cocotb_cases(raw, artifact['configuration'], artifact['module'], switches)
        elif artifact['kind'] == 'checker':
            rows = json.loads(raw)['cases']
        elif artifact['kind'] in ('configuration', 'log'):
            rows = []
        else:
            raise ValueError('artifact: unsupported kind')
        parsed.extend(rows)
    require(sorted(parsed, key=lambda c: c['id']) == record['cases'], 'record: normalized cases differ')
    ids = [case['id'] for case in record['cases']]
    require(len(ids) == len(set(ids)), 'record: duplicate case')
    require(sorted(set(record['expectedCases']) - set(ids)) == record['missingCases'], 'record: missing-case mismatch')
    for case in record['cases']:
        require(case['verdict'] in ('passed', 'failed', 'not_executed'), 'record: invalid verdict')
        require(math.isfinite(case['durationSeconds']) and case['durationSeconds'] >= 0, 'record: invalid duration')
        if case['verdict'] == 'failed':
            require(case['failureKind'] in ('assertion', 'error', 'unknown'), 'record: invalid failure kind')
        if case['verdict'] == 'not_executed':
            require(case.get('skipReasonCode') in ('opt_in_off', 'optional_vectors_off', 'missing_fixture',
                                                  'desktop_unavailable', 'platform_unsupported', 'runner_skip_unknown'), 'record: invalid skip code')


def load_selection(bundle):
    selection = json_read(bundle / 'selection.json')
    require(selection.get('schemaVersion') == 1, 'selection: unsupported schema')
    records = []
    for entry in selection['records']:
        path = (bundle / entry['path']).resolve()
        require(path.is_relative_to(bundle.resolve()), 'selection: path escape')
        require(evidence_exists(path) and digest_file(path, bundle) == entry['sha256'], 'selection: record hash mismatch')
        record = json_read(path, bundle)
        require(record['runId'] == entry['runId'], 'selection: run identity mismatch')
        validate_record(record, path.parent, bundle)
        records.append(record)
    require(records, 'selection: no records')
    require(len({r['runId'] for r in records}) == len(records), 'selection: duplicate run')
    require(len({(r['source']['commit'], r['source']['snapshotSha256']) for r in records}) == 1, 'selection: mixed sources')
    subjects = {}
    for record in records:
        for path, hash_value in record['subject']['files'].items():
            require(path not in subjects or subjects[path] == hash_value, 'selection: mixed subjects')
            subjects[path] = hash_value
    selected = []
    for case_id, run_id in selection['caseSelections'].items():
        record = next((r for r in records if r['runId'] == run_id), None)
        require(record is not None, 'selection: unknown run')
        require(record['profile'] == 'normal', 'selection: fault-injection selected')
        case = next((c for c in record['cases'] if c['id'] == case_id), None)
        require(case is not None, 'selection: unknown case')
        selected.append(case)
    all_ids = {c['id'] for r in records if r['profile'] == 'normal' for c in r['cases']}
    require(set(selection['caseSelections']) == all_ids, 'selection: omitted executed case')
    return selection, records, selected


def merge_bundles(bundle, inputs):
    selection = {'schemaVersion': 1, 'records': [], 'caseSelections': {}}
    for source in inputs:
        base = Path(source).resolve()
        sub = json_read(base / 'selection.json')
        # Downloaded bundles live within the aggregate root; do not copy or silently select repeats.
        for entry in sub['records']:
            path = base / entry['path']
            require(path.resolve().is_relative_to(bundle.resolve()), 'aggregate: inputs must be under output root')
            selection['records'].append({**entry, 'path': path.relative_to(bundle).as_posix()})
        for key, value in sub['caseSelections'].items():
            require(key not in selection['caseSelections'], 'aggregate: duplicate selection')
            selection['caseSelections'][key] = value
    write(bundle / 'selection.json', canonical(selection))


def table(headers, rows):
    def cell(value):
        return str(value).replace('|', '\\|').replace('\n', ' ')
    return '\n'.join(['| ' + ' | '.join(headers) + ' |', '| ' + ' | '.join('---' for _ in headers) + ' |'] +
                     ['| ' + ' | '.join(cell(v) for v in row) + ' |' for row in rows])


def render(bundle, catalog_path, generation_pin=None):
    selection, records, selected = load_selection(bundle)
    units = catalog(catalog_path, verify_sources=False)
    source = records[0]['source']
    generator_source = source_identity()
    reasons = sorted({reason for r in records for reason in r['draftReasons']})
    if generator_source['dirty']:
        reasons.append('generator_tree_dirty')
    if any(entry.get('incomplete') for entry in units.values()):
        # Catalog review quality is reported separately from provenance; it does not turn executions into passes.
        catalog_review = 'incomplete descriptions require planner review'
    else:
        catalog_review = 'all descriptions marked established'
    status = 'draft' if reasons else 'formal'
    if generation_pin:
        reasons, status = generation_pin['draftReasons'], generation_pin['status']
    counts = collections.Counter(c['verdict'] for c in selected)
    unit_results = collections.defaultdict(collections.Counter)
    for case in selected:
        require(case['unit'] in units, 'report: uncatalogued unit')
        require(case['selector'] in units[case['unit']]['methods'], 'report: uncatalogued method')
        unit_results[case['unit']][case['verdict']] += 1
    inventory = table(['Assembly / family', 'Passed', 'Failed', 'Not executed', 'Missing cases'],
                      [(family, sum(c['verdict'] == 'passed' for c in selected if c['unit'].split('::')[0] == family),
                        sum(c['verdict'] == 'failed' for c in selected if c['unit'].split('::')[0] == family),
                        sum(c['verdict'] == 'not_executed' for c in selected if c['unit'].split('::')[0] == family),
                        sum(m.startswith(family + '::') for r in records for m in r['missingCases']))
                       for family in sorted({c['unit'].split('::')[0] for c in selected} |
                                            {c.split('::')[0] for r in records for c in r['expectedCases']})])
    inventory = f'Subject `{source["commit"]}`; {status}; source snapshot `{source["snapshotSha256"]}`.\n\n' + inventory
    trace_selectors = collections.defaultdict(set)
    for entry in units.values():
        for mapping in entry['trace']:
            trace_selectors[mapping['requirement']].update(mapping['selectors'])
    trace_rows = []
    for requirement, selectors in sorted(trace_selectors.items()):
        matching = [case for case in selected if case['selector'] in selectors]
        results = collections.Counter(c['verdict'] for c in matching)
        trace_rows.append((requirement, results['passed'], results['failed'], results['not_executed'],
                           len(selectors - {c['selector'] for c in matching})))
    trace = 'Automated execution only; inspection/compiler/manual verdicts in the matrix remain separate.\n\n' + table(
        ['SR / EV / VAL', 'Passed', 'Failed', 'Not executed', 'Missing methods'], trace_rows)
    missing_units = sorted(set(units) - set(unit_results))
    family_rows = []
    for family in sorted({r['family'] for r in records}):
        run_ids = {r['runId'] for r in records if r['family'] == family}
        results = collections.Counter(c['verdict'] for c in selected if selection['caseSelections'][c['id']] in run_ids)
        family_rows.append((family, results['passed'], results['failed'], results['not_executed'],
                            sum(len(r['missingCases']) for r in records if r['family'] == family)))
    attention = []
    failed = [(r['runId'], c['id'], c['failureKind'], c['reasonText']) for r in records for c in r['cases']
              if c['verdict'] == 'failed']
    if failed:
        attention.append('Failed cases (all retained invocations):\n\n' + table(['Run', 'Case', 'Kind', 'Reason'], failed))
    skipped = collections.defaultdict(list)
    for case in selected:
        if case['verdict'] == 'not_executed':
            skipped[(case['skipReasonCode'], case['reasonText'])].append(case['id'])
    for (code, reason), names in sorted(skipped.items()):
        attention.append(f'Not executed — {code}: {reason}\n\n' + '\n'.join('- `' + name + '`' for name in sorted(names)))
    missing_methods = sorted({m for entry in units.values() for m in entry['methods']} - {c['selector'] for c in selected})
    missing_cases = sorted({m for r in records for m in r['missingCases']})
    if missing_cases or missing_methods:
        attention.append('Missing cases / method executions:\n\n' + '\n'.join('- `' + m + '`' for m in sorted(set(missing_cases + missing_methods))))
    body = '\n\n'.join([
        '# VV.Tests.Results — generated automated test results',
        'Scope: selected runner invocations for one source snapshot; no human approval or requirement sign-off.',
        '**At a glance**\n- Provenance: ' + status + '.\n- Cases: ' + ', '.join(f'{k}={v}' for k, v in sorted(counts.items())) +
        f'.\n- Catalog units without results: {len(missing_units)}.\n- Archived replay revalidates sanitised runner evidence; binaries are hash-only.',
        'Draft reasons: ' + (', '.join(sorted(set(reasons))) or 'none'),
        'Catalog review: ' + catalog_review + '.',
        inventory,
        table(['Runner family', 'Passed', 'Failed', 'Not executed', 'Missing cases'], family_rows),
        table(['Run', 'Family', 'Started UTC', 'Exit', 'Provenance', 'OS', 'Missing'],
              [(r['runId'], r['family'], r['startedUtc'], r['exitCode'], r['provenance'], r['environment']['os'],
                len(r['missingCases'])) for r in records]),
        table(['Catalog unit', 'Passed', 'Failed', 'Not executed', 'Missing methods'],
              [(key, unit_results[key]['passed'], unit_results[key]['failed'], unit_results[key]['not_executed'],
                len(set(entry['methods']) - {c['selector'] for c in selected if c['unit'] == key})) for key, entry in sorted(units.items())]),
        trace,
        '\n\n'.join(attention) or 'No failed, skipped or missing cases.',
        'Run diagnostics:\n\n' + ('\n'.join('- ' + r['runId'] + ': ' + ', '.join(r['diagnostics']) for r in records if r['diagnostics']) or 'none'),
        'Known profile gaps:\n\n' + '\n'.join('- ' + gap for gap in GAPS),
        'Replay: `python samples/testing/test_records.py check --bundle <record-directory>`; selection and generation manifests identify all inputs. '
        'Sanitised TRX/XML/JSON hashes are rechecked; original raw hashes are collection claims, and historical DLLs are not reopened.']) + '\n'
    private_free(body)
    generation = {'schemaVersion': 1, 'source': source['snapshotSha256'], 'status': status, 'draftReasons': sorted(set(reasons)),
                  'generatorCommit': generator_source['commit'], 'generatorDirty': generator_source['dirty'],
                  'generatorSha256': digest_file(Path(__file__)), 'generatorSource': generator_source,
                  'catalogSha256': digest_file(catalog_path),
                  'selectionSha256': digest_file(bundle / 'selection.json'),
                  'outputs': {'report': sha(body.encode()), 'inventory': sha(inventory.encode()), 'trace': sha(trace.encode())},
                  'verification': 'sanitised-runner-evidence-replay; binaries-hash-only'}
    return body, inventory, trace, generation


def generate(bundle, output=None, studio=None, checking=False, export=None):
    bundle = Path(bundle).resolve()
    pinned_catalog = bundle / 'catalog.md'
    if not checking and not pinned_catalog.exists():
        write(pinned_catalog, CATALOG.read_bytes())
    require(pinned_catalog.exists(), 'report: missing pinned catalog')
    stored = json_read(bundle / 'generation.json', bundle) if checking else None
    body, inventory, trace, generation = render(bundle, pinned_catalog, stored)
    paths = [(bundle / 'report.md', body.encode()), (bundle / 'inventory.md', inventory.encode()),
             (bundle / 'trace.md', trace.encode())]
    if checking:
        require(stored['catalogSha256'] == digest_file(pinned_catalog), 'generation: catalog hash mismatch')
        require(stored['selectionSha256'] == digest_file(bundle / 'selection.json'), 'generation: selection hash mismatch')
        # Generator provenance describes the original generation, not the current replay machine/tree.
        require(stored['generatorSha256'] == stored['generatorSource']['files']['samples/testing/test_records.py'],
                'generation: generator identity hash mismatch')
        require(sha(canonical(stored['generatorSource']['files'])) == stored['generatorSource']['snapshotSha256'],
                'generation: generator source hash mismatch')
        for path, raw in paths:
            require(path.is_file() and path.read_bytes() == raw, 'report: generated bytes differ ' + path.name)
        # Input/output pins protect against a stored report changed along with its apparent text.
        require(stored['outputs']['report'] == digest_file(bundle / 'report.md'), 'generation: output hash mismatch')
        require(stored['outputs']['inventory'] == digest_file(bundle / 'inventory.md'), 'generation: inventory hash mismatch')
        require(stored['outputs']['trace'] == digest_file(bundle / 'trace.md'), 'generation: trace hash mismatch')
        if output:
            require(Path(output).read_bytes() == (bundle / 'report.md').read_bytes(), 'report: external report differs')
        if studio:
            text = Path(studio).read_text(encoding='utf-8')
            require(replace_block(replace_block(text, 'TEST INVENTORY', inventory), 'AUTOMATED TRACE RESULTS', trace) == text,
                    'report: Studio blocks differ')
    else:
        for path, raw in paths:
            write(path, raw)
        write(bundle / 'generation.json', canonical(generation))
        if output:
            write(output, body)
        if studio:
            path = Path(studio)
            original = path.read_bytes()
            newline = '\r\n' if b'\r\n' in original else '\n'
            text = original.decode('utf-8').replace('\r\n', '\n')
            text = replace_block(text, 'TEST INVENTORY', inventory)
            text = replace_block(text, 'AUTOMATED TRACE RESULTS', trace)
            write(path, text.replace('\n', newline))
        if export:
            export_bundle(bundle, Path(export))
    print('record replay passed' if checking else 'report generated: ' + generation['status'])


def export_bundle(bundle, target):
    require(not target.exists(), 'export: destination already exists')
    selection = json_read(bundle / 'selection.json')
    retained = {'selection.json', 'generation.json', 'catalog.md', 'report.md', 'inventory.md', 'trace.md'}
    pooled = {'generation.json'}
    for entry in selection['records']:
        record_path = bundle / entry['path']
        retained.add(entry['path'])
        pooled.add(entry['path'])
        for artifact in json_read(record_path)['artifacts']:
            retained.add((record_path.parent / artifact['path']).relative_to(bundle).as_posix())
    pool = {}
    for relative in sorted(retained):
        raw = evidence_bytes(bundle / relative, bundle)
        private_free(raw)
        encoded = raw
        if relative in pooled:
            document = json.loads(raw)
            for owner in ('source', 'generatorSource', 'subject'):
                for field in ('files', 'postFiles'):
                    if owner in document and field in document[owner]:
                        manifest = document[owner][field]
                        fingerprint = sha(canonical(manifest))
                        pool[fingerprint] = manifest
                        document[owner][field] = {'manifestRef': fingerprint}
            encoded = canonical({'archiveEncoding': 'manifest-pool-v1', 'document': document})
        compressed = Path(relative).suffix in ('.json', '.trx')
        write(target / (relative + '.gz' if compressed else relative), gzip_bytes(encoded) if compressed else encoded)
    write(target / 'manifests.json.gz', gzip_bytes(canonical(pool)))
    # Prove losslessness across every retained file, including all normalized fields.
    for relative in sorted(retained):
        require(evidence_bytes(target / relative, target) == evidence_bytes(bundle / relative, bundle),
                'archive: expansion differs ' + relative)


def self_test(out):
    out = Path(out)
    out.mkdir(parents=True, exist_ok=True)
    checks = []
    def refuses(name, code, action):
        try:
            action()
        except ValueError as error:
            require(str(error).startswith(code), 'self-test: wrong refusal ' + name + ': ' + str(error))
            checks.append({'defect': name, 'refusal': str(error)})
        else:
            raise ValueError('self-test: defect accepted ' + name)
    fake_prefix = ('C' + ':' + '/' + 'Users/' + 'privateperson').encode()
    raw = b'''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010" runUser="PRIVATEHOST\\privateperson" name="privateperson@PRIVATEHOST"><Times start="2026-10-08T00:00:00Z" finish="2026-10-08T00:00:01Z"/><Results><UnitTestResult testId="1" testName="Gcam.Tests.FixtureTests.Skip" computerName="PRIVATEHOST" duration="00:00:00.001" outcome="NotExecuted"><Output><ErrorInfo><Message>Set GCAM_UI_TESTS=1 to run FAKE_PATH/repo/x.cs</Message></ErrorInfo></Output></UnitTestResult></Results><TestDefinitions><UnitTest id="1"><TestMethod className="Gcam.Tests.FixtureTests" name="Skip" codeBase="FAKE_PATH/repo/tests.dll"/></UnitTest></TestDefinitions><ResultSummary outcome="Completed"><Counters notExecuted="0"/></ResultSummary></TestRun>'''.replace(b'FAKE_PATH', fake_prefix)
    sanitized = sanitize_trx(raw)
    write(out / 'sanitized.trx', sanitized)
    require(b'privateperson' not in sanitized and b'PRIVATEHOST' not in sanitized, 'self-test: identity retained')
    require(sanitize_trx(sanitized) == sanitized, 'self-test: sanitizer not idempotent')
    cases, _ = trx_cases(sanitized, {'GCAM_UI_TESTS': '0'})
    require(cases[0]['verdict'] == 'not_executed', 'self-test: skipped passed')
    for raw_outcome in ('Failed', 'Passed'):
        fixture = sanitized.replace(b'NotExecuted', raw_outcome.encode())
        rows, _ = trx_cases(fixture, {})
        require(rows[0]['failureKind'] == ('unknown' if raw_outcome == 'Failed' else None), 'self-test: guessed failure')
    source = {'commit': 'a' * 40, 'dirty': True, 'files': {'tests/fixture.cs': 'b' * 64}}
    source['snapshotSha256'] = sha(canonical(source['files']))
    base = {'schemaVersion': 1, 'runId': 'fixture', 'source': source, 'subject': {'files': {}, 'postFiles': {}},
            'provenance': 'draft', 'draftReasons': ['working_tree_dirty'], 'environment': {'switches': {'GCAM_UI_TESTS': '0'}},
            'cases': cases, 'expectedCases': [cases[0]['id']], 'missingCases': [],
            'artifacts': [{'path': 'sanitized.trx', 'sha256': sha(sanitized), 'bytes': len(sanitized), 'kind': 'dotnet'}]}
    validate_record(base, out)
    defects = [
        ('source-hash', 'record: source hash mismatch', lambda x: x['source'].update(snapshotSha256='0' * 64)),
        ('subject-change', 'record: subject changed', lambda x: x['subject']['postFiles'].update({'a.dll': '0' * 64})),
        ('invalid-formal', 'record: invalid formal claim', lambda x: x.update(provenance='formal')),
        ('artifact-hash', 'artifact: hash mismatch', lambda x: x['artifacts'][0].update(sha256='0' * 64)),
        ('missing-artifact', 'artifact: missing', lambda x: x['artifacts'][0].update(path='absent.trx')),
        ('path-escape', 'artifact: path escape', lambda x: x['artifacts'][0].update(path='../escaped.trx')),
        ('normalized-tamper', 'record: normalized cases differ', lambda x: x['cases'][0].update(verdict='passed')),
        ('missing-case-count', 'record: missing-case mismatch', lambda x: x.update(missingCases=['typed-count'])),
        ('local-path', 'privacy: absolute path', lambda x: x.update(leak=fake_prefix.decode() + '/x')),
    ]
    for name, reason, mutate in defects:
        bad = copy.deepcopy(base)
        mutate(bad)
        write(out / (name + '.json'), canonical(bad))
        refuses(name, reason, lambda bad=bad: validate_record(bad, out))
    refuses('duplicate-marker', 'markers:', lambda: replace_block('<!-- BEGIN GENERATED: X -->' * 2, 'X', ''))
    for subtype in ('assertion', 'error'):
        data = canonical({'schemaVersion': 1, 'cases': [{'name': 'fixture.Class.method', 'outcome': 'failed',
                                                       'failureKind': subtype, 'durationSeconds': 0.1}]})
        require(unittest_cases(data, {})[0][0]['failureKind'] == subtype, 'self-test: unittest subtype lost')
    for child in ('failure', 'error', 'skipped'):
        data = f'<testsuite><testcase name="fixture" time="0.1"><{child}/></testcase></testsuite>'.encode()
        require(cocotb_cases(data, 'config', 'module', {})[0][0]['verdict'] != 'passed', 'self-test: XML failure lost')
    # Exercise real unittest callbacks, including multiple failed subtests of one case.
    import io
    import unittest
    from unittest_adapter import Result
    class AdapterFixture(unittest.TestCase):
        def test_assertion(self):
            self.assertEqual(1, 2)
        def test_error(self):
            raise RuntimeError('seeded exception')
        def test_skip(self):
            self.skipTest('seeded skip')
        def test_subtests(self):
            for i in (1, 2):
                with self.subTest(i=i):
                    self.assertEqual(i, 0)
    result = unittest.TextTestRunner(stream=io.StringIO(), resultclass=Result).run(unittest.defaultTestLoader.loadTestsFromTestCase(AdapterFixture))
    require(len(result.records) == 4, 'self-test: unittest duplicate parent')
    subcase = next(r for r in result.records if r['name'].endswith('test_subtests'))
    require(subcase['outcome'] == 'failed' and len(subcase['parts']) == 2, 'self-test: subtest failure lost')
    require({r['failureKind'] for r in result.records if r['outcome'] == 'failed'} == {'assertion', 'error'}, 'self-test: callback classification')
    # Selection defects each start from a valid retained record/artifact pair.
    bundle = out / 'selection-fixture'
    folder = bundle / 'run'
    write(folder / 'sanitized.trx', sanitized)
    run = {**base, 'profile': 'normal', 'family': 'dotnet'}
    write(folder / 'run-record.json', canonical(run))
    entry = {'path': 'run/run-record.json', 'sha256': digest_file(folder / 'run-record.json'), 'runId': 'fixture'}
    valid_selection = {'schemaVersion': 1, 'records': [entry], 'caseSelections': {cases[0]['id']: 'fixture'}}
    write(bundle / 'selection.json', canonical(valid_selection))
    load_selection(bundle)
    for name, reason, mutate in [
        ('selection-hash', 'selection: record hash mismatch', lambda s: s['records'][0].update(sha256='0' * 64)),
        ('selection-identity', 'selection: run identity mismatch', lambda s: s['records'][0].update(runId='absent')),
        ('unknown-run', 'selection: unknown run', lambda s: s['caseSelections'].update({cases[0]['id']: 'absent'})),
        ('missing-executed', 'selection: omitted executed case', lambda s: s.update(caseSelections={})),
        ('duplicate-run', 'selection: duplicate run', lambda s: s['records'].append(copy.deepcopy(s['records'][0]))),
    ]:
        selection = copy.deepcopy(valid_selection)
        mutate(selection)
        write(bundle / 'selection.json', canonical(selection))
        refuses(name, reason, lambda: load_selection(bundle))
    write(bundle / 'selection.json', canonical(valid_selection))
    fault = copy.deepcopy(run)
    fault['profile'] = 'fault-injection-or-capture'
    write(folder / 'run-record.json', canonical(fault))
    fault_selection = copy.deepcopy(valid_selection)
    fault_selection['records'][0]['sha256'] = digest_file(folder / 'run-record.json')
    write(bundle / 'selection.json', canonical(fault_selection))
    refuses('fault-injection-selection', 'selection: fault-injection selected', lambda: load_selection(bundle))
    write(folder / 'run-record.json', canonical(run))
    write(bundle / 'selection.json', canonical(valid_selection))
    mixed = copy.deepcopy(run)
    mixed['runId'] = 'other'
    mixed['source']['commit'] = 'c' * 40
    write(bundle / 'other/run-record.json', canonical(mixed))
    write(bundle / 'other/sanitized.trx', sanitized)
    selection = copy.deepcopy(valid_selection)
    selection['records'].append({'path': 'other/run-record.json', 'sha256': digest_file(bundle / 'other/run-record.json'), 'runId': 'other'})
    write(bundle / 'selection.json', canonical(selection))
    refuses('mixed-source', 'selection: mixed sources', lambda: load_selection(bundle))
    # Compressed storage is transparent and does not discard normalized fields.
    write(bundle / 'selection.json', canonical(valid_selection))
    write(bundle / 'generation.json', canonical({'generatorSource': source}))
    for name in ('catalog.md', 'report.md', 'inventory.md', 'trace.md'):
        write(bundle / name, 'fixture\n')
    archive = out / 'compressed-fixture'
    export_bundle(bundle, archive)
    load_selection(archive)
    stored_path = archive / 'run/run-record.json.gz'
    original_gzip = stored_path.read_bytes()
    require(gzip_bytes(b'fixture') == gzip_bytes(b'fixture'), 'self-test: gzip not deterministic')
    require(original_gzip[:10] == b'\x1f\x8b\x08\x00\x00\x00\x00\x00\x02\xff', 'self-test: gzip header')
    require(evidence_bytes(archive / 'run/run-record.json', archive) == canonical(run), 'self-test: fields dropped')
    require(evidence_bytes(archive / 'run/sanitized.trx', archive) == sanitized, 'self-test: TRX changed')
    damaged = bytearray(original_gzip)
    damaged[4] = 1
    write(stored_path, bytes(damaged))
    refuses('gzip-timestamp', 'archive: noncanonical gzip header', lambda: load_selection(archive))
    write(stored_path, original_gzip[:-5])
    refuses('gzip-truncated', 'archive: corrupt gzip', lambda: load_selection(archive))
    write(stored_path, original_gzip)
    pool_path = archive / 'manifests.json.gz'
    pool_raw = pool_path.read_bytes()
    pool = json.loads(gzip.decompress(pool_raw))
    changed_pool = copy.deepcopy(pool)
    changed_pool[next(iter(changed_pool))]['wrong'] = '0' * 64
    write(pool_path, gzip_bytes(canonical(changed_pool)))
    refuses('manifest-pool-hash', 'archive: manifest hash mismatch', lambda: load_selection(archive))
    write(pool_path, gzip_bytes(canonical({})))
    refuses('manifest-pool-missing-reference', 'archive: missing manifest reference', lambda: load_selection(archive))
    write(pool_path, pool_raw)
    load_selection(archive)
    # Source hashes, exact selectors and catalog membership are independent freshness checks.
    if CATALOG.exists():
        text = CATALOG.read_text(encoding='utf-8')
        entry_match = re.search(r'```json\n(.*?)\n```', text, re.S)
        require(entry_match is not None, 'self-test: missing catalog fixture')
        original_entry = json.loads(entry_match[1])
        for name, code, mutation in [
            ('catalog-source-change', 'catalog: source changed', lambda e: e['sources'].update({next(iter(e['sources'])): '0' * 64})),
            ('catalog-stale-method', 'catalog: missing or stale method', lambda e: e['methods'].append(e['id'] + '.Absent')),
        ]:
            bad_entry = copy.deepcopy(original_entry)
            mutation(bad_entry)
            defect_text = text[:entry_match.start(1)] + json.dumps(bad_entry) + text[entry_match.end(1):]
            path = out / (name + '.md')
            write(path, defect_text)
            refuses(name, code, lambda path=path: catalog(path))
        duplicate = out / 'catalog-duplicate.md'
        write(duplicate, text + '\n```json\n' + json.dumps(original_entry) + '\n```\n')
        refuses('catalog-duplicate', 'catalog: duplicate unit', lambda: catalog(duplicate))
        missing = out / 'catalog-missing.md'
        write(missing, text[:entry_match.start()] + text[entry_match.end():])
        refuses('catalog-missing', 'catalog: missing or stale unit', lambda: catalog(missing))
    refuses('empty-trx', 'trx: empty results', lambda: trx_cases(b'<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"/>', {}))
    require(sanitize_text('https://example.invalid/page') == 'https://example.invalid/page', 'self-test: URL corrupted')
    from unittest import mock
    with mock.patch.dict(os.environ, {'USER': 'runner'}):
        private_free('runnerLabel=<runner>; selected runner invocations')
        require(sanitize_text('--output-root') == '--output-root', 'self-test: argument name corrupted')
    write(out / 'self-test.json', canonical({'schemaVersion': 1, 'defects': checks, 'adapterChecks': 8}))
    print(f'self-test: {len(checks)} precise refusals; adapter/sanitizer checks passed')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='action', required=True)
    collect_parser = sub.add_parser('collect')
    collect_parser.add_argument('--out', type=Path, required=True)
    collect_parser.add_argument('--family', choices=('dotnet', 'unittest', 'cocotb', 'checker'), required=True)
    collect_parser.add_argument('--no-build', action='store_true')
    collect_parser.add_argument('--include-background', action='store_true')
    collect_parser.add_argument('--checker')
    collect_parser.add_argument('--python', nargs='+', metavar='COMMAND',
                                help='cocotb interpreter command prefix, e.g. --python py -3.13')
    for action in ('generate', 'check'):
        p = sub.add_parser(action)
        p.add_argument('--bundle', type=Path)
        p.add_argument('--archives', action='store_true')
        p.add_argument('--output', type=Path)
        p.add_argument('--studio', type=Path)
        p.add_argument('--inputs', type=Path, nargs='*')
        p.add_argument('--export', type=Path)
    p = sub.add_parser('check-catalog')
    p.add_argument('--discover', action='store_true')
    p.add_argument('--update-discovery', action='store_true')
    p = sub.add_parser('self-test')
    p.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    if args.action == 'collect':
        require(args.python is None or args.family == 'cocotb', '--python is for the cocotb family')
        return collect(args.out, args.family, args.no_build, args.include_background, args.checker, args.python)
    if args.action == 'check-catalog':
        check_catalog(args.discover or args.update_discovery, args.update_discovery)
    elif args.action == 'self-test':
        self_test(args.out)
    elif args.archives:
        archives = sorted((REPO / 'samples/testing/records').glob('*/selection.json*'))
        for selection in archives:
            generate(selection.parent, checking=True)
        print(f'archived replay: {len(archives)} committed milestones (none is not a passing milestone)')
    else:
        require(args.bundle is not None, 'report: bundle required')
        if args.inputs:
            merge_bundles(args.bundle.resolve(), args.inputs)
        generate(args.bundle, args.output, args.studio, args.action == 'check', args.export)
    return 0


if __name__ == '__main__':
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, 'reconfigure'):
            stream.reconfigure(encoding='utf-8', errors='replace')
    try:
        raise SystemExit(main())
    except (ValueError, KeyError, OSError, ET.ParseError) as error:
        print('REFUSED:', sanitize_text(str(error)), file=sys.stderr)
        raise SystemExit(1)
