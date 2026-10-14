"""Headless isolation inventory, deployment pins, and conservative trace attribution.

Raw machine-wide traces stay local. This tool never starts a trace or launches Studio.
"""
import argparse
import datetime as dt
import hashlib
import gzip
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

REPO = Path(__file__).resolve().parents[4]
ISOLATION = Path(__file__).resolve().parent


def canonical(value):
    def ordered(item):
        if isinstance(item, dict):
            return {key: ordered(item[key]) for key in sorted(item, key=ordinal)}
        if isinstance(item, (list, tuple)):
            return [ordered(entry) for entry in item]
        return item
    return (json.dumps(ordered(value), indent=2, ensure_ascii=True, allow_nan=False) + '\n').encode()


def ordinal(value):
    return value.encode('utf-16-be', errors='surrogatepass')  # .NET StringComparer.Ordinal


def digest(data):
    return hashlib.sha256(data).hexdigest()


def write(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(canonical(value))


def declarations(repo=REPO):
    targets = {}

    def add(target, kind):
        targets.setdefault(target, set()).add(kind)

    for path in (repo / 'src/Gcam.Studio').rglob('*.xaml'):
        if any(part in ('bin', 'obj') for part in path.parts):
            continue
        for element in ET.parse(path).iter():
            kind = element.tag.rsplit('}', 1)[-1]
            for key, value in element.attrib.items():
                if key.rsplit('}', 1)[-1] != 'AutomationProperties.AutomationId':
                    continue
                if not value.startswith('{'):
                    add(value, kind)
                elif value == '{Binding DeleteAutomationId}':
                    add('measurement-row', 'Button')
                elif value == '{Binding HelpId}':
                    add('readout-line-help', 'HelpMark')
                elif value == '{Binding AutomationId}' and kind == 'RadioButton':
                    for model in (repo / 'src/Gcam.Studio.Core/ViewModels').glob('*WorkspaceViewModel.cs'):
                        for name in re.findall(r'"(Workspace\.[A-Za-z]+)"', model.read_text(encoding='utf-8-sig')):
                            add(name, kind)
                elif value == '{Binding AutomationId}' and kind == 'Button':
                    source = (repo / 'src/Gcam.Studio.Core/ViewModels/MainViewModel.Readout.cs').read_text(encoding='utf-8-sig')
                    for name in re.findall(r'"(Readout\.(?:Window[0-9]+|BenchmarkWindow))"', source):
                        add(name, kind)
                else:
                    raise ValueError('unclassified-bound-target:' + value)
    source = (repo / 'src/Gcam.Studio/Controls/CaptionButtons.cs').read_text(encoding='utf-8-sig')
    for name in re.findall(r'"(Window\.[A-Za-z]+)"', source):
        add(name, 'Button')
    add('measurement-row', 'ListBoxItem')
    return {key: sorted(value) for key, value in sorted(targets.items())}


def check_policy(policy, declared):
    entries = policy['targets']
    keys = [entry['target'] for entry in entries]
    if len(keys) != len(set(keys)):
        raise ValueError('duplicate-policy-target')
    missing, removed = set(declared) - set(keys), set(keys) - set(declared)
    if missing or removed:
        raise ValueError('policy-drift:' + ','.join(sorted(missing | removed)))
    for entry in entries:
        if entry['controlTypes'] != declared[entry['target']]:
            raise ValueError('policy-type-drift:' + entry['target'])
        if entry['policy'] not in ('allow', 'deny'):
            raise ValueError('unknown-policy-value')


def file_digest(path):
    result = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(chunk)
    return result.hexdigest()


def fingerprint_files(directory, deployment=False):
    files = {}
    for path in Path(directory).rglob('*'):
        relative = path.relative_to(directory)
        if (not path.is_file() or any(part in ('bin', 'obj', '__pycache__') for part in relative.parts)
                or deployment and path.suffix not in ('.dll', '.exe', '.json')):
            continue
        files[relative.as_posix()] = file_digest(path)
    return {name: files[name] for name in sorted(files, key=ordinal)}


def fingerprint_map(files):
    return digest('\n'.join(key + ':' + files[key] for key in sorted(files, key=ordinal)).encode('utf-8'))


def fingerprint(directory):
    return fingerprint_map(fingerprint_files(directory))


def pin(out=None):
    # Pin exact product inputs and binaries produced by the recorded build; not a runtime qualification.
    source = {}
    for path in sorted((REPO / 'src').rglob('*')):
        if path.is_file() and not any(p in ('bin', 'obj') for p in path.parts):
            source[path.relative_to(REPO).as_posix()] = digest(path.read_bytes())
    for name in ('Directory.Build.props', 'Gcam.sln'):
        source[name] = digest((REPO / name).read_bytes())
    for name in ('samples/ambient/terrestrial-unscear2000-v1.json', 'samples/evidence/studio-readout/offer-cost-v1.json'):
        source[name] = digest((REPO / name).read_bytes())
    deployment = REPO / 'src/Gcam.Studio/bin/Release/net9.0-windows'
    binaries = fingerprint_files(deployment, deployment=True)
    if not binaries or 'Gcam.Studio.dll' not in binaries:
        raise ValueError('build-required')
    commit = subprocess.run(['git', '--no-optional-locks', 'rev-parse', 'HEAD'], cwd=REPO, check=True, capture_output=True, text=True).stdout.strip()
    dirty = bool(subprocess.run(['git', '--no-optional-locks', 'status', '--porcelain'], cwd=REPO, check=True, capture_output=True, text=True).stdout.strip())
    runner = {p.relative_to(REPO).as_posix(): digest(p.read_bytes()) for p in sorted((REPO / 'tests/Gcam.Studio.UiTests').rglob('*'))
              if p.is_file() and not any(part in ('bin', 'obj', '__pycache__') for part in p.parts)}
    result = {'schemaVersion': 1, 'sourceCommit': commit, 'sourceFiles': source, 'sourceSha256': digest(canonical(source)),
              'workingTree': 'dirty' if dirty else 'clean', 'runnerSourceFiles': runner, 'runnerSourceSha256': digest(canonical(runner)),
              'fingerprintSpecVersion': 1,
              'deploymentFiles': binaries, 'deploymentSha256': fingerprint_map(binaries),
              'isolationSha256': fingerprint(ISOLATION),
              'linkBasis': 'same-checkout Release build invocation; source and deployed file hashes; runtime not executed',
              'runtimeQualification': 'not performed'}
    if out is not None:
        write(out, result)
    return result


def source_identity():
    commit = subprocess.run(['git', '--no-optional-locks', 'rev-parse', 'HEAD'], cwd=REPO,
                            check=True, capture_output=True, text=True, timeout=10).stdout.strip()
    date = subprocess.run(['git', '--no-optional-locks', 'show', '-s', '--format=%cI', 'HEAD'], cwd=REPO,
                          check=True, capture_output=True, text=True, timeout=10).stdout.strip()
    return commit, date


def pwsh_version():
    executable = shutil.which('pwsh')
    if executable is None:
        raise ValueError('pwsh 7 is missing; use PowerShell 7, not Windows PowerShell 5.1')
    result = subprocess.run([executable, '-NoLogo', '-NoProfile', '-NonInteractive', '-Command',
                             '$PSVersionTable.PSVersion.Major'], check=True, capture_output=True, text=True, timeout=10)
    return int(result.stdout.strip())


def profile_coverage(profile_path=None, required_channels=None):
    """Follow selected classic collector/provider references; declarations alone are not coverage."""
    required = {'file': {'FileIO', 'FileIOInit', 'Filename', 'ProcessThread'},
                'registry': {'Registry', 'ProcessThread'}, 'process': {'ProcessThread', 'Loader'}}
    issues, selected = [], set()
    try:
        doc = ET.parse(profile_path or ISOLATION / 'Probe.wprp').getroot()
        profiles = doc.findall("./Profiles/Profile[@Id='GcamProbe.Verbose.File']")
        if len(profiles) != 1 or profiles[0].get('LoggingMode') != 'File' or profiles[0].get('Base'):
            raise ValueError('expected one selected file-mode GcamProbe.Verbose.File profile')
        for ref in profiles[0].findall('./Collectors/SystemCollectorId'):
            collectors = doc.findall("./Profiles/SystemCollector[@Id='" + ref.get('Value', '') + "']")
            if len(collectors) != 1:
                raise ValueError('missing/ambiguous selected SystemCollector')
            provider_refs = ref.findall('./SystemProviderId')
            if len(provider_refs) != 1:
                raise ValueError('expected one selected SystemProviderId')
            provider_ref = provider_refs[0]
            providers = doc.findall("./Profiles/SystemProvider[@Id='" + provider_ref.get('Value', '') + "']")
            if len(providers) != 1 or providers[0].get('Base') or list(provider_ref):
                raise ValueError('missing/ambiguous/inherited/overridden SystemProvider')
            keywords = providers[0].find('Keywords')
            if keywords is None or keywords.get('Operation', 'Set') != 'Set':
                raise ValueError('expected explicit Set keywords')
            selected.update(node.get('Value') for node in keywords.findall('Keyword'))
        if profiles[0].findall('./Collectors/EventCollectorId'):
            issues.append('profile selects unapproved event collectors')
        if doc.findall('.//Stacks') or selected - set().union(*required.values()):
            issues.append('profile selects stacks or unapproved system keywords')
        for channel in required_channels if required_channels is not None else json.loads((ISOLATION / 'classification-rules.json').read_text())['requiredChannels']:
            if channel not in required:
                issues.append('profile required channel has no approved provider: ' + channel)
            elif required[channel] - selected:
                issues.append('profile missing required ' + channel + ' keywords: ' + ', '.join(sorted(required[channel] - selected)))
    except (OSError, ValueError, ET.ParseError, SyntaxError) as error:
        issues.append('profile coverage invalid: ' + str(error))
    return issues


def load_normalisation(schema_path=None):
    rules = json.loads((ISOLATION / 'normalisation-rules.json').read_text(encoding='utf-8'))
    if schema_path:
        doc = ET.parse(schema_path).getroot()
        local = lambda node: node.tag.rsplit('}', 1)[-1]
        templates = {}
        for provider in (node for node in doc.iter() if local(node) == 'provider'):
            tasks = {node.get('name'): node.get('eventGUID', '').strip('{}').lower()
                     for node in provider.iter() if local(node) == 'task'}
            opcodes = {node.get('name'): node.get('mofValue', node.get('value'))
                       for node in provider.iter() if local(node) == 'opcode'}
            fields = {node.get('tid'): [(field.get('name'), field.get('inType')) for field in node]
                      for node in provider.iter() if local(node) == 'template'}
            for node in (node for node in provider.iter() if local(node) == 'event'):
                key = (tasks.get(node.get('task')), node.get('version'), opcodes.get(node.get('opcode')))
                if key[0] not in {layout['guid'] for layout in rules.get('binaryLayouts', [])}:
                    continue
                value = fields.get(node.get('template'), [])
                if key in templates and templates[key] != value:
                    raise ValueError('conflicting classic schema templates')
                templates[key] = value
        rules['_templates'] = templates
        rules['_schemaSha256'] = file_digest(schema_path)
    return rules


def recover_payload(children, guid, opcode, version, rules, correlation):
    """Only independently evidenced fixed prefixes; schema/version/width/length must agree."""
    node = children.get('ProcessingErrorData')
    if node is None or not rules.get('_templates'):
        return None
    payload = {item.tag.rsplit('}', 1)[-1]: item.text or '' for item in node}
    if numeric(payload.get('ErrorCode')) != 15005:
        return None
    for layout in rules.get('binaryLayouts', []):
        if (guid != layout['guid'] or opcode not in layout['opcodes'] or version != str(layout['version'])
                or correlation.get('pointerSize') != layout['pointerSize']):
            continue
        template = rules['_templates'].get((guid, version, opcode), [])
        if template[:len(layout['schemaPrefix'])] != [tuple(field) for field in layout['schemaPrefix']]:
            return None
        try:
            raw = bytes.fromhex(payload.get('EventPayload', ''))
        except ValueError:
            return None
        if len(raw) < layout['minimumBytes'] or (layout.get('maximumBytes') and len(raw) > layout['maximumBytes']):
            return None
        result = {field: str(int.from_bytes(raw[offset:offset + width], 'little'))
                  for field, offset, width in layout['fields']}
        result.update({'sourceDecodeError': '15005', 'rawPayloadSha256': digest(raw),
                       'uninterpretedPayloadSha256': digest(raw[layout['decodedPrefixBytes']:]),
                       'payloadRecoverySchemaSha256': rules['_schemaSha256'], 'payloadRecoveryLayout': layout['id']})
        return result
    return None


def compact_trace(trace_path, out_path, schema_path=None):
    """Preserve every normalised event (including unknowns) in a compressed replayable intermediate."""
    out = Path(out_path)
    if out.suffixes[-2:] != ['.jsonl', '.gz'] or out.resolve() == Path(trace_path).resolve():
        raise ValueError('compact destination must be a separate .jsonl.gz file')
    normalisation = load_normalisation(schema_path)
    count = 0
    with gzip.open(out, 'xt', encoding='utf-8', newline='\n', compresslevel=6) as stream:
        for event in parse_trace(trace_path, normalisation):
            stream.write(json.dumps(event, separators=(',', ':')) + '\n')
            count += 1
    record = {'events': count, 'sourceBytes': Path(trace_path).stat().st_size,
              'sourceSha256': file_digest(trace_path), 'compactBytes': out.stat().st_size,
              'compactSha256': file_digest(out), 'normalisationSha256': file_digest(ISOLATION / 'normalisation-rules.json'),
              'schemaSha256': normalisation.get('_schemaSha256'), 'parserSourceSha256': file_digest(__file__),
              'evidenceRetention': 'All normalised events retained; raw trace remains the authoritative payload evidence.'}
    write(str(out) + '.record.json', record)
    return record


def preflight(run_directory, live_pin=None, identity=None, version=None, scratch_root=None, profile_path=None):
    """Read-only. Collect all failures before an administrator opens the trace window."""
    issues = profile_coverage(profile_path)
    root = Path(scratch_root or Path(tempfile.gettempdir()) / 'gcam-todo43').resolve()
    path = Path(run_directory).absolute()
    try:
        path.relative_to(root)
        if path == root:
            raise ValueError('run folder is the scratch root')
        for ancestor in [path, *path.parents]:
            if ancestor.exists() and (ancestor.is_symlink() or getattr(ancestor.stat(), 'st_file_attributes', 0) & 0x400):
                raise ValueError('reparse point in run folder')
            if ancestor == root:
                break
    except ValueError as error:
        return [f'run folder is not a confined scratch child: {error}']
    run_id = path.name
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,70}', run_id):
        issues.append('invalid run ID')
    if not path.is_dir():
        issues.append('run folder does not exist')
    for name in ('trace.ready.json', 'trace.complete.json', 'harness.complete.json', 'trace.lock'):
        if (path / name).exists():
            issues.append('run folder already contains ' + name + '; choose a fresh run ID')
    if path.exists() and any(path.glob('*.etl')):
        issues.append('run folder already contains ETL; retained runs are read-only')
    documents = {}
    for name in ('approvals.json', 'pin.json'):
        try:
            target = path / name
            if target.is_symlink() or getattr(target.stat(), 'st_file_attributes', 0) & 0x400:
                raise ValueError('reparse document')
            documents[name] = json.loads(target.read_text(encoding='utf-8-sig'))
            if not isinstance(documents[name], dict):
                raise ValueError('expected an object')
        except (OSError, ValueError) as error:
            issues.append(name + ' is missing/invalid: ' + type(error).__name__)
    try:
        major = (version or pwsh_version)()
        if major < 7:
            issues.append('pwsh must be version 7 or later; use pwsh, not powershell.exe')
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        issues.append('pwsh 7 validation failed: ' + str(error))
    try:
        live = (live_pin or pin)()
        commit, committed_at = (identity or source_identity)()
        if live.get('workingTree') != 'clean':
            issues.append('current HEAD worktree is dirty; review/commit and regenerate pin/approvals')
        approval = documents.get('approvals.json', {})
        recorded = documents.get('pin.json', {})
        if approval.get('runId') != run_id:
            issues.append('approvals runId differs from run folder')
        if approval.get('commit') != commit or recorded.get('sourceCommit') != commit:
            issues.append('approval/pin commit differs from current HEAD')
        parse_date = lambda value: dt.datetime.fromisoformat(value.replace('Z', '+00:00'))
        try:
            if parse_date(approval.get('committedAt', '')) != parse_date(committed_at):
                issues.append('approval commit date differs from actual HEAD commit date')
        except (ValueError, AttributeError, TypeError):
            issues.append('approval commit date is missing/invalid')
        if recorded.get('fingerprintSpecVersion') != 1:
            issues.append('stale pin: fingerprintSpecVersion must be 1; regenerate pin')
        if recorded.get('workingTree') != 'clean':
            issues.append('pin was made from a dirty worktree')
        if recorded.get('deploymentSha256') != fingerprint_map(recorded.get('deploymentFiles', {})):
            issues.append('stale pin: deploymentSha256 is not the canonical digest of deploymentFiles')
        for field in ('isolationSha256', 'deploymentSha256'):
            if recorded.get(field) != live.get(field):
                issues.append('stale pin: ' + field + ' differs from live files')
            if approval.get(field) != live.get(field):
                issues.append('approval ' + field + ' differs from live files')
        for field in ('sourceSha256', 'runnerSourceSha256'):
            if recorded.get(field) != live.get(field):
                issues.append('stale pin: ' + field + ' differs from live sources')
        records = approval.get('approvals', [])
        for act in ('HUM-01', 'HUM-04', 'HUM-07'):
            matches = [record for record in records if isinstance(record, dict) and record.get('humanActId') == act]
            if len(matches) != 1:
                issues.append(act + ' requires exactly one approval')
                continue
            record = matches[0]
            if (not isinstance(record.get('id'), str) or not record['id'].strip()
                    or not isinstance(record.get('quote'), str) or not record['quote'].strip()
                    or not re.fullmatch(r'[0-9a-f]{40}', str(record.get('targetCommit', '')))
                    or record.get('targetCommit') != commit):
                issues.append(act + ' record ID/quote/target commit is invalid')
            try:
                if parse_date(record.get('quotedAt', '')) < parse_date(committed_at):
                    issues.append(act + ' approval predates the approved commit')
            except (ValueError, TypeError, AttributeError):
                issues.append(act + ' approval date is missing/invalid')
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        issues.append('live source/deployment validation failed: ' + str(error))
    return issues


def parse_trace(path, normalisation=None):
    """Stream XML/JSONL. Retain only one Event subtree; array JSON is for small synthetic fixtures."""
    path = Path(path)
    if path.suffix == '.json':
        yield from json.loads(path.read_text(encoding='utf-8'))
        return
    if path.suffix == '.jsonl' or path.suffixes[-2:] == ['.jsonl', '.gz']:
        if path.suffix == '.gz':
            record = json.loads(Path(str(path) + '.record.json').read_text())
            if record['normalisationSha256'] != file_digest(ISOLATION / 'normalisation-rules.json'):
                raise ValueError('compact intermediate normalisation differs from current approved rules; replay raw trace')
            if record.get('parserSourceSha256') != file_digest(__file__):
                # Parse/correlation logic lives in this file; an older parser's rows are not current evidence.
                raise ValueError('compact intermediate parser source differs from the current classifier; replay raw trace')
            if record.get('schemaSha256') and record['schemaSha256'] != (normalisation or {}).get('_schemaSha256'):
                raise ValueError('compact intermediate requires its matching schema')
        opener = gzip.open if path.suffix == '.gz' else open
        with opener(path, 'rt', encoding='utf-8') as stream:
            for line in stream:
                yield json.loads(line)
        return
    normalisation = normalisation or json.loads((ISOLATION / 'normalisation-rules.json').read_text(encoding='utf-8'))
    correlation = {'threads': {}, 'files': {}, 'clock': None}
    stack = []
    with path.open('rb') as stream:
        for action, element in ET.iterparse(stream, events=('start', 'end')):
            if action == 'start':
                stack.append(element)
                continue
            if element.tag.rsplit('}', 1)[-1] == 'Event':
                row = parse_event(element, normalisation, correlation)
                if len(stack) > 1:
                    stack[-2].remove(element)
                element.clear()
                yield row
            stack.pop()


def numeric(value):
    try:
        text = str(value).strip()
        return int(text, 16 if text.lower().startswith('0x') else 10)
    except (ValueError, TypeError):
        return None


def parse_event(event, normalisation=None, correlation=None):
    """System attributes and rendered strings are separate; extended GUID identifies a classic task."""
    normalisation = normalisation or json.loads((ISOLATION / 'normalisation-rules.json').read_text(encoding='utf-8'))
    correlation = correlation if correlation is not None else {'threads': {}, 'files': {}, 'clock': None}
    local = lambda node: node.tag.rsplit('}', 1)[-1]
    children = {local(node): node for node in event}
    system = {local(node): node for node in children.get('System', [])}
    rendered = {local(node): node for node in children.get('RenderingInfo', [])}
    extended = {local(node): node for node in children.get('ExtendedTracingInfo', [])}
    provider_node = system.get('Provider')
    attributes = provider_node.attrib if provider_node is not None else {}
    guid = (extended.get('EventGuid').text if extended.get('EventGuid') is not None else attributes.get('Guid', '')) or ''
    provider = guid or attributes.get('Name', '')
    get_text = lambda nodes, name: (nodes[name].text or '').strip() if name in nodes else ''
    opcode = get_text(rendered, 'Opcode') or get_text(system, 'Opcode')
    raw_opcode = numeric(get_text(system, 'Opcode'))
    identity_opcode = str(raw_opcode) if raw_opcode is not None else opcode
    data = {node.attrib.get('Name', ''): (node.text or '').strip()
            for node in children.get('EventData', []) if local(node) == 'Data'}
    if key_pointer := numeric(data.get('PointerSize')):
        if guid.strip('{}').lower() == '68fdd900-4a3e-11d1-84f4-0000f80464e3' and identity_opcode in ('0', 'Header'):
            correlation['pointerSize'] = key_pointer
    recovered = recover_payload(children, guid.strip('{}').lower(), identity_opcode,
                                get_text(system, 'Version'), normalisation, correlation)
    if recovered:
        data.update(recovered)
    if 'ProcessingErrorData' in children:
        source_error = {local(item): item.text or '' for item in children['ProcessingErrorData']}
        data['processingErrorCode'] = source_error.get('ErrorCode', '')
        payload = source_error.get('EventPayload', '')
        try:
            data['processingErrorPayloadSha256'] = digest(bytes.fromhex(payload))
        except ValueError:
            data['processingErrorPayloadSha256'] = digest(payload.encode())
    execution_node = system.get('Execution')
    execution = execution_node.attrib if execution_node is not None else {}
    header_pid = numeric(execution.get('ProcessID'))
    pid = numeric(data.get('PID', data.get('ProcessId', data.get('ProcessID'))))
    pid_source = 'payload-ProcessId' if pid is not None else 'header-ProcessID'
    if pid is None:
        pid = header_pid
    if pid is not None and not 0 < pid < 0xFFFFFFFF:
        pid = None
    at_node = system.get('TimeCreated')
    at = at_node.attrib.get('SystemTime', '') if at_node is not None else ''
    raw_time = numeric(at_node.attrib.get('RawTime')) if at_node is not None else None
    if (guid.strip('{}').lower() == '68fdd900-4a3e-11d1-84f4-0000f80464e3' and identity_opcode in ('0', 'Header')
            and raw_time is not None and numeric(data.get('ReservedFlags')) in (1, 2)
            and (numeric(data.get('PerfFreq')) or 0) > 0 and (numeric(data.get('StartTime')) or 0) > 0):
        correlation['clock'] = {'clockType': numeric(data['ReservedFlags']), 'rawAnchor': raw_time,
                                'utcFileTimeAnchor': numeric(data['StartTime']), 'frequency': numeric(data['PerfFreq'])}
    clock = correlation.get('clock')
    if raw_time is not None and clock:
        ticks = (clock['utcFileTimeAnchor'] + (raw_time - clock['rawAnchor']) * 10000000 // clock['frequency']
                 if clock['clockType'] == 1 else raw_time)
        utc = dt.datetime(1601, 1, 1, tzinfo=dt.timezone.utc) + dt.timedelta(seconds=ticks // 10000000)
        at = utc.strftime('%Y-%m-%dT%H:%M:%S') + f'.{ticks % 10000000:07d}Z'
        data['RawTime'] = str(raw_time)
    data.update({'RawOpcode': get_text(system, 'Opcode'), 'Opcode': opcode, 'EventID': get_text(system, 'EventID'),
                 'Task': get_text(system, 'Task'), 'TimeCreated': at})
    error = ('ProcessingErrorData' in children and not recovered) or ('ProcessId' in data and numeric(data['ProcessId']) is None)
    opaque = next((children[name] for name in ('BinaryEventData',) if name in children), None)
    if opaque is not None:
        data['opaquePayloadSha256'] = digest((opaque.text or '').encode())
    channel, operation, kind, mapping = 'unknown', 'unknown', opcode or 'Unknown', None
    key = guid.strip('{}').lower()
    if key == '68fdd900-4a3e-11d1-84f4-0000f80464e3' and identity_opcode in ('0', 'Header'):
        kind = 'Header'
    for index, row in enumerate(normalisation['rows']):
        if key == row['guid'] and identity_opcode.casefold() in [op.casefold() for op in row['opcodes']]:
            channel, kind, mapping = row['channel'], row['operation'], index
            if not error and all(field in data for field in row['requiredFields']):
                operation = kind
            break
    if not guid:
        # Historical labelled fixture shape remains diagnostic; opaque real GUIDs never use substring guessing.
        label = (attributes.get('Name', '') + ' ' + data['Task']).lower()
        channel = next((c for c, hints in {'file': ('file',), 'registry': ('registry', 'reg'),
                         'process': ('process', 'image'), 'network': ('network', 'tcp', 'udp')}.items()
                        if any(h in label for h in hints)), 'unknown')
        operation = data.get('Operation', opcode)
    destination = data.get('OpenPath', data.get('FileName', data.get('KeyName', data.get('ImageFileName', data.get('ImageName', data.get('daddr', ''))))))
    thread = numeric(data.get('TTID', data.get('TThreadId', data.get('ThreadId'))))
    if operation in ('ThreadStart', 'ThreadRundownStart') and thread is not None and pid is not None:
        correlation['threads'][thread] = {'pid': pid, 'startedAt': at}
    if channel == 'file' and thread is not None:
        identity = correlation['threads'].get(thread)
        owner = None
        if identity:
            try:
                time = lambda value: dt.datetime.fromisoformat(value.replace('Z', '+00:00'))
                if time(at) >= time(identity['startedAt']):
                    owner = identity['pid']
            except (ValueError, TypeError):
                pass
        if owner is not None:
            pid, pid_source = owner, 'thread-lifecycle-payload'
        elif guid:
            # A file emitter header can be the servicing worker, not the requesting thread's process.
            pid, pid_source = None, 'requesting-thread-unresolved'
    if channel == 'file' and operation == 'SetInformation':
        refinement = normalisation.get('fileInformationClasses', {}).get(str(numeric(data.get('InfoClass'))))
        if refinement:
            operation = kind = refinement['operation']
            data['semanticRefinement'] = 'fileInformationClasses/' + str(numeric(data['InfoClass']))
    file_object = numeric(data.get('FileObject'))
    file_key = numeric(data.get('FileKey'))
    if channel == 'file':
        if destination and file_object and operation in ('CreateFile', 'FileName'):
            correlation['files'][file_object] = destination
        if not destination:
            destination = correlation['files'].get(file_key, correlation['files'].get(file_object, ''))
        if operation == 'CloseFile' and file_object:
            correlation['files'].pop(file_object, None)
    if channel == 'registry':
        keys = correlation.setdefault('registryKeys', {})
        handle = numeric(data.get('KeyHandle'))
        if handle and destination and operation in ('RegistryKcbCreate', 'RegistryKcbRundownStart', 'RegistryKcbRundownEnd', 'CreateKey', 'OpenKey'):
            if destination.casefold().startswith('\\registry\\'):
                keys[handle] = destination
        if handle and handle in keys:
            if operation in ('SetValue', 'DeleteValue', 'QueryValue') and destination and not destination.startswith('\\'):
                data['ValueName'] = destination
                destination = keys[handle] + '\\' + destination
            elif not destination:
                destination = keys[handle]
        if operation == 'RegistryKcbDelete' and handle:
            keys.pop(handle, None)
    requests = correlation.setdefault('requests', {})
    irp = numeric(data.get('IrpPtr'))
    if key == '90cbdc39-4a3e-11d1-84f4-0000f80464e3' and operation == 'unknown' and irp:
        requests[irp] = None # an unmapped file event may invalidate an earlier request association
    if channel == 'file' and irp:
        if operation == 'FileOperationEnd':
            request = requests.pop(irp, None)
            ordered = False
            if request:
                try:
                    ordered = dt.datetime.fromisoformat(request['at'].replace('Z', '+00:00')) <= dt.datetime.fromisoformat(at.replace('Z', '+00:00'))
                except ValueError:
                    pass
            if ordered:
                pid, pid_source, destination = request['pid'], 'file-request-correlation', request['destination']
                data['requestOperation'] = request['operation']
            else:
                operation = 'unknown'
        elif thread is not None:
            if operation == 'unknown' or irp in requests:
                requests[irp] = None # conflicting or unmapped request cannot lend identity to completion
            else:
                requests[irp] = {'pid': pid, 'destination': destination, 'operation': operation, 'at': at}
    if operation in ('ThreadEnd', 'ThreadRundownEnd') and thread is not None:
        correlation['threads'].pop(thread, None)
    return {'pid': pid, 'pidSource': pid_source, 'headerPid': header_pid, 'channel': channel, 'operation': operation,
            'kind': kind, 'mappingRow': mapping, 'decodeError': error, 'destination': destination,
            'clockEvidence': clock,
            'parentPid': numeric(data.get('ParentProcessID', data.get('ParentID', data.get('ParentId')))),
            'valueName': data.get('ValueName', ''), 'valueData': data.get('ValueData'), 'status': data.get('Status'),
            'at': at, 'provider': provider, 'fields': data}


def read_trace_health(summary_path, trace_path=None, normalisation=None):
    """Processed buffers are not lost buffers. Missing/duplicate/loss/conflicting evidence never becomes zero."""
    raw = Path(summary_path).read_bytes()
    text = raw.decode('utf-16' if raw.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig')
    def field(label):
        matches = re.findall(r'^\s*' + label + r'\s+([0-9]+)\s*$', text, re.M)
        return int(matches[0]) if len(matches) == 1 else None
    lost = field(r'Total\s+Events\s+Lost')
    buffers_lost = field(r'(?:Total\s+)?Buffers\s+Lost')
    expected = field(r'Total\s+Events\s+Processed')
    buffers = field(r'Total\s+Buffers\s+Processed')
    health = {'complete': False, 'eventsLost': lost, 'buffersLost': buffers_lost, 'eventsProcessed': expected,
              'buffersProcessed': buffers, 'summarySha256': digest(raw), 'buffersLossSource': 'summary' if buffers_lost is not None else 'not reported'}
    if trace_path is not None:
        count, headers = 0, []
        for event in parse_trace(trace_path, normalisation):
            count += 1
            if event.get('provider', '').strip('{}').lower() == '68fdd900-4a3e-11d1-84f4-0000f80464e3' and event['kind'] == 'Header':
                headers.append({key: numeric(event['fields'].get(key)) for key in ('EventsLost', 'BuffersLost')})
                if event.get('clockEvidence'):
                    health['clockEvidence'] = event['clockEvidence']
        health['parsedEvents'] = count
        if len(headers) == 1 and headers[0]['EventsLost'] == lost and headers[0]['BuffersLost'] is not None:
            if buffers_lost is None:
                health['buffersLost'] = headers[0]['BuffersLost']
                health['buffersLossSource'] = 'decoded TRACE_LOGFILE_HEADER.BuffersLost; summary omits this field'
            elif buffers_lost != headers[0]['BuffersLost']:
                health['buffersLost'] = None
        health['traceSha256'] = file_digest(trace_path)
        health['complete'] = (expected is not None and count == expected and lost is not None
                              and health['buffersLost'] is not None and len(headers) <= 1)
    else:
        health['complete'] = expected is not None and lost is not None and buffers_lost is not None
    return health


def destination_category(event, context):
    path = event.get('destination', '')
    value = path.replace('\\', '/').casefold()
    if not path:
        return '<UNDECODED-IMAGE-METADATA>' if event.get('channel') == 'metadata' else '<NO-DECODED-DESTINATION>'
    if any(confined(path, root) for root in context.get('ownedRoots', [])):
        return '<REDIRECTED-SANDBOX>'
    if event.get('channel') == 'registry':
        return '<REGISTRY>'
    if '/namedpipe/' in value:
        return '<NAMED-PIPE>'
    if '/appdata/local/' in value:
        return '<LOCALAPPDATA>'
    if '/users/' in value:
        return '<USERPROFILE>'
    if '/windows/' in value:
        return '<WINDOWS>'
    if '/program files/' in value or '/program files (x86)/' in value:
        return '<PROGRAMFILES>'
    if '/software/' in value or value.startswith('\\registry'):
        return '<REGISTRY>'
    if 'gcam.studio' in value:
        return '<STUDIO-DEPLOYMENT>'
    return '<OTHER>'


def registry_marker_match(event, control, rules, harness, proof=False):
    """One narrow approved harness surface; unknown registry events are never PID-filtered away."""
    rule = rules.get('registryMarker', {})
    run_id = control.get('runId', '')
    if (event.get('channel') != 'registry' or control.get('channel') != 'registry'
            or event.get('pid') != control.get('pid') or event.get('pid') not in harness
            or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,70}', run_id)
            or control.get('valueData') != run_id or control.get('valueName') != rule.get('valueName')):
        return False
    key = rule.get('keyRoot', '') + '\\' + run_id
    native = control.get('traceKeyPath', '')
    if control.get('keyPath', '').casefold() != key.casefold():
        return False
    if not re.fullmatch(r'\\REGISTRY\\USER\\S-1-[0-9]+(?:-[0-9]+)+\\Software\\GcamUiaProbe\\' + re.escape(run_id), native, re.I):
        return False
    operation = event.get('operation')
    if operation not in rule.get('declaredOperations', []) or (proof and operation != rule.get('proofOperation')):
        return False
    destination = event.get('destination', '').rstrip('\\')
    names = (key, 'HKEY_CURRENT_USER' + key[4:], native)
    # Some exports put the value name at the end of the destination instead of in a separate field.
    is_value_path = any(destination.casefold() == (name + '\\' + control['valueName']).casefold() for name in names)
    if not is_value_path and not any(destination.casefold() == name.casefold() for name in names):
        return False
    if proof and not is_value_path and event.get('valueName') != control['valueName']:
        return False
    if event.get('valueName') and event['valueName'] != control['valueName']:
        return False
    if event.get('valueData') is not None and event['valueData'] != run_id:
        return False
    if event.get('status') is not None and event['status'] != 'SUCCESS' and numeric(event['status']) != 0:
        return False
    try:
        time = lambda value: dt.datetime.fromisoformat(value.replace('Z', '+00:00'))
        if not time(control['startedAt']) <= time(event.get('at', '')) <= time(control['finishedAt']):
            return False
    except (KeyError, ValueError, TypeError):
        return False
    return True


def classify(events, context, rules, candidate_sink=None):
    """Identity/time is required; PID alone cannot promote recycled process events."""
    required = set(context.get('requiredChannels', [])) | set(rules.get('requiredChannels', []))
    candidates, controls = [], {channel: False for channel in sorted(required)}
    read_events = events if callable(events) else lambda: iter(events)
    if not callable(events) and iter(events) is events:
        raise ValueError("Supply a replayable event factory, not a consumed iterator")
    counts = {}
    owned_problem_counts = {}
    pid_sources = {}
    candidate_count = 0
    unsafe = unresolved = False
    owned = context.get('ownedProcesses', [])
    harness = set(context.get('harnessPids', []))
    roots = context.get('ownedRoots', [])
    def in_lifetime(owner, event):
        try:
            time = lambda value: dt.datetime.fromisoformat(value.replace('Z', '+00:00'))
            return time(owner['startedAt']) <= time(event.get('at', '')) <= time(owner['finishedAt'])
        except (ValueError, TypeError):
            return False
    tree = list(owned)
    grew = bool(tree)
    while grew:
        grew = False
        for event in read_events():
            if event.get('channel') != 'process' or event.get('operation') != 'ProcessStart':
                continue
            parents = [o for o in tree if o['pid'] == event.get('parentPid') and in_lifetime(o, event)]
            if parents and not any(o['pid'] == event.get('pid') and in_lifetime(o, event) for o in tree):
                tree.append({'pid': event['pid'], 'startedAt': event['at'], 'finishedAt': parents[0]['finishedAt'], 'parentPid': event['parentPid']})
                grew = True
    for event in read_events():
        pid = event.get('pid')
        owners = [o for o in tree if o['pid'] == pid and in_lifetime(o, event)]
        positive = any(registry_marker_match(event, c, rules, harness, proof=True) if c.get('channel') == 'registry'
                       else event.get('channel') == c['channel'] and pid == c['pid']
                       and c['token'].casefold() in event.get('destination', '').casefold()
                       and (c['channel'] == 'file' and event.get('operation') in ('Write', 'WriteFile')
                            or c['channel'] == 'process' and bool(owners) and event.get('operation') in ('ProcessStart', 'ImageLoad'))
                       for c in context.get('positiveControls', []))
        if positive:
            controls[event['channel']] = True
        if owners:
            # A record channel is not necessarily a write. Unknown opcodes must not be silently ignored.
            operation = event.get('operation', '')
            channel = event.get('channel', 'unknown')
            if channel == 'process' and operation == 'ProcessStart' and any(o.get('parentPid') == event.get('parentPid')
                    and event.get('parentPid') in harness for o in owners):
                category = 'os-or-harness-actor'
            elif operation in ('CreateFile', 'CreateKey'):
                # Kernel create events can mean opening an existing object; no guessed write classification.
                category = 'unknown'
            elif operation in rules.get('readOperations', {}).get(channel, []):
                category = 'app-read'
            elif operation in rules.get('writeOperations', {}).get(channel, []):
                category = 'app-declared' if channel == 'file' and any(confined(event.get('destination', ''), r) for r in roots) else 'out-of-declaration'
                # Studio has no declared file writes, including inside its sandbox.
                if not rules.get('allowAppFileWrites', False):
                    category = 'out-of-declaration'
            else:
                category = 'unknown'
        elif any(registry_marker_match(event, c, rules, harness) for c in context.get('positiveControls', [])):
            category = 'os-or-harness-actor'
        elif pid in harness and event.get('channel') in rules.get('harnessChannels', []):
            category = 'os-or-harness-actor'
            if (event.get('channel') == 'file' and event.get('operation') in rules.get('writeOperations', {}).get('file', [])
                    and not any(confined(event.get('destination', ''), r) for r in roots)):
                category = 'out-of-declaration'
            if event.get('channel') == 'network' and event.get('operation') in rules.get('writeOperations', {}).get('network', []):
                category = 'out-of-declaration'
        elif pid in context.get('osPids', []) and event.get('operation') in rules.get('osBookkeepingOperations', []):
            category = 'os-or-harness-actor'
        else:
            # Includes app-requested shell effects without sufficient causal metadata.
            category = 'unknown'
        kind = event.get('kind', event.get('operation', 'unknown'))
        destination = destination_category(event, context)
        if owners and category in ('unknown', 'out-of-declaration'):
            key = category + '|' + kind + '|' + destination
            owned_problem_counts[key] = owned_problem_counts.get(key, 0) + 1
        source = event.get('pidSource', 'fixture-explicit')
        pid_sources[source] = pid_sources.get(source, 0) + 1
        candidate = {'channel': event.get('channel', 'unknown'), 'pid': pid, 'classification': category,
                     'kind': kind, 'ownedTree': bool(owners), 'destinationCategory': destination,
                     'pidSource': source,
                     'eventSha256': digest(canonical(event))}
        if candidate_sink is None:
            candidates.append(candidate)
        else:
            candidate_sink(candidate)
        candidate_count += 1
        counts[category] = counts.get(category, 0) + 1
        unsafe |= category == 'out-of-declaration'
        unresolved |= category == 'unknown'
    proven = all(controls.values()) and bool(controls) and bool(owned)
    health = context.get('traceHealth', {})
    loss_proven = (health.get('complete') is True and type(health.get('eventsLost')) is int and type(health.get('buffersLost')) is int
                   and health['eventsLost'] == 0 and health['buffersLost'] == 0)
    return {'schemaVersion': 1, 'candidates': candidates, 'candidateCount': candidate_count,
            'classificationCounts': counts, 'positiveControls': controls,
            'ownedTreeProblems': [{'classification': key.split('|')[0], 'kind': key.split('|')[1],
                                   'destinationCategory': key.split('|')[2], 'count': count}
                                  for key, count in sorted(owned_problem_counts.items())],
            'pidSources': pid_sources,
            'traceHealth': health,
            'runStatus': 'unsafe_effect' if unsafe else 'blocked' if not proven or not loss_proven or unresolved else 'completed',
            'reason': 'undeclared-app-effect' if unsafe else 'instrument-not-proven' if not proven else 'trace-health-not-proven' if not loss_proven else 'unresolved-candidate' if unresolved else '',
            'g1Eligible': proven and loss_proven and not unsafe and not unresolved,
            'blindSpots': context.get('blindSpots', []),
            'channelApplicability': context.get('channelApplicability', []),
            'warning': 'Only this trace window is assessed; raw events and context remain local.'}


def confined(path, root):
    # Trace paths may be Windows paths even on the Linux checker; avoid host Path semantics.
    value, base = path.replace('\\', '/').casefold(), root.replace('\\', '/').rstrip('/').casefold() + '/'
    return value.startswith(base) and '..' not in value.split('/')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    sub.add_parser('check-policy')
    p = sub.add_parser('pin'); p.add_argument('--out', required=True)
    p = sub.add_parser('fingerprint'); p.add_argument('--directory', required=True); p.add_argument('--kind', choices=('isolation', 'deployment'), default='isolation')
    p = sub.add_parser('preflight'); p.add_argument('--run-directory', required=True)
    p = sub.add_parser('compact'); p.add_argument('--trace', required=True); p.add_argument('--out', required=True); p.add_argument('--schema')
    p = sub.add_parser('classify'); p.add_argument('--trace', required=True); p.add_argument('--context', required=True); p.add_argument('--out', required=True)
    p.add_argument('--summary'); p.add_argument('--schema')
    args = parser.parse_args()
    if args.command == 'check-policy':
        check_policy(json.loads((ISOLATION / 'action-policy.json').read_text()), declarations())
        print('Action policy and current declarations agree; runtime qualification not performed.')
    elif args.command == 'pin':
        pin(args.out)
    elif args.command == 'fingerprint':
        files = fingerprint_files(Path(args.directory), args.kind == 'deployment')
        print(canonical({'files': files, 'sha256': fingerprint_map(files)}).decode(), end='')
    elif args.command == 'compact':
        print(json.dumps(compact_trace(args.trace, args.out, args.schema), separators=(',', ':')))
    elif args.command == 'preflight':
        issues = preflight(args.run_directory)
        for issue in issues:
            print('FAIL: ' + issue)
        print('Preflight BLOCKED; do not start tracing.' if issues else 'Preflight PASS; author may now prepare the approved tracing window.')
        print('Export with tracerpt -rts -export schema.man; classify with --schema schema.man for evidenced payload recovery.')
        if issues:
            raise SystemExit(2)
    else:
        context = json.loads(Path(args.context).read_text(encoding='utf-8-sig'))
        original_context_sha = file_digest(args.context)
        normalisation_bytes = (ISOLATION / 'normalisation-rules.json').read_bytes()
        normalisation = load_normalisation(args.schema)
        if args.summary:
            context['traceHealth'] = read_trace_health(args.summary, args.trace, normalisation)
        rules = json.loads((ISOLATION / 'classification-rules.json').read_text())
        candidate_path = Path(args.out).with_suffix('.candidates.jsonl')
        candidate_path.parent.mkdir(parents=True, exist_ok=True)
        with candidate_path.open('w', encoding='utf-8', newline='\n') as stream:
            result = classify(lambda: parse_trace(args.trace, normalisation), context, rules,
                              lambda candidate: stream.write(json.dumps(candidate, separators=(',', ':')) + '\n'))
        result['candidateFile'] = candidate_path.name
        result['candidateSha256'] = file_digest(candidate_path)
        result['normalisationVersion'] = normalisation['normalisationVersion']
        result['normalisationSha256'] = digest(normalisation_bytes)
        result['classifierSourceSha256'] = file_digest(__file__)
        if args.schema:
            result['schemaSha256'] = file_digest(args.schema)
        result['traceSha256'] = file_digest(args.trace)
        if Path(args.trace).suffix == '.gz':
            record_path = Path(str(args.trace) + '.record.json')
            record = json.loads(record_path.read_text())
            if record['compactSha256'] != result['traceSha256']:
                raise ValueError('compact intermediate digest differs from its generation record')
            result['rawTraceSha256'] = record['sourceSha256']
            result['compactGenerationRecordSha256'] = file_digest(record_path)
        result['contextSha256'] = digest(canonical(context))
        result['inputContextSha256'] = original_context_sha
        result['rulesSha256'] = digest(canonical(rules))
        write(args.out, result)
        print(result['runStatus'] + ': ' + result['reason'])
        if not result['g1Eligible']:
            raise SystemExit(2)


if __name__ == '__main__':
    main()
