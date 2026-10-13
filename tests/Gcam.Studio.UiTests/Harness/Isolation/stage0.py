"""Headless isolation inventory, deployment pins, and conservative trace attribution.

Raw machine-wide traces stay local. This tool never starts a trace or launches Studio.
"""
import argparse
import datetime as dt
import hashlib
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


def preflight(run_directory, live_pin=None, identity=None, version=None, scratch_root=None):
    """Read-only. Collect all failures before an administrator opens the trace window."""
    issues = []
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


def parse_trace(path):
    """Stream XML/JSONL. Retain only one Event subtree; array JSON is for small synthetic fixtures."""
    path = Path(path)
    if path.suffix == '.json':
        yield from json.loads(path.read_text(encoding='utf-8'))
        return
    if path.suffix == '.jsonl':
        with path.open(encoding='utf-8') as stream:
            for line in stream:
                yield json.loads(line)
        return
    stack = []
    with path.open('rb') as stream:
        for action, element in ET.iterparse(stream, events=('start', 'end')):
            if action == 'start':
                stack.append(element)
                continue
            if element.tag.rsplit('}', 1)[-1] == 'Event':
                row = parse_event(element)
                if len(stack) > 1:
                    stack[-2].remove(element)
                element.clear()
                yield row
            stack.pop()


def parse_event(event):
    data = {}
    provider = ''
    for node in event.iter():
        name = node.tag.rsplit('}', 1)[-1]
        if name == 'Provider':
            provider = node.attrib.get('Name', node.attrib.get('Guid', ''))
        if name == 'Data':
            data[node.attrib.get('Name', '')] = node.text or ''
        if name == 'Execution':
            data.setdefault('ProcessId', node.attrib.get('ProcessID', ''))
        if name in ('Opcode', 'Task', 'EventID', 'TimeCreated'):
            data[name] = node.attrib.get('SystemTime', node.text or '')
    raw_pid = data.get('PID', data.get('ProcessId', data.get('ProcessID', '')))
    try:
        pid = int(raw_pid, 16) if raw_pid.lower().startswith('0x') else int(raw_pid)
    except ValueError:
        pid = None
    text = (provider + ' ' + data.get('Task', '')).lower()
    channel = next((c for c, hints in {'file': ('file',), 'registry': ('registry', 'reg'),
                                     'process': ('process', 'image'), 'network': ('network', 'tcp', 'udp')}.items()
                    if any(h in text for h in hints)), 'unknown')
    raw_parent = data.get('ParentProcessID', data.get('ParentID', data.get('ParentId', '')))
    try:
        parent = int(raw_parent, 16) if raw_parent.lower().startswith('0x') else int(raw_parent)
    except ValueError:
        parent = None
    return {'pid': pid, 'channel': channel, 'operation': data.get('Operation', data.get('Opcode', '')),
                 'destination': data.get('FileName', data.get('KeyName', data.get('ImageName', data.get('daddr', '')))),
                 'parentPid': parent,
                 'valueName': data.get('ValueName', ''),
                 'valueData': data.get('ValueData'),
                 'status': data.get('Status'),
                 'at': data.get('TimeCreated', ''), 'provider': provider, 'fields': data}


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
    if event.get('status') not in (None, 0, '0', '0x0', '0x00000000', 'SUCCESS'):
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
                       and c['token'] in event.get('destination', '') for c in context.get('positiveControls', []))
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
        candidate = {'channel': event.get('channel', 'unknown'), 'pid': pid, 'classification': category,
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
    p = sub.add_parser('classify'); p.add_argument('--trace', required=True); p.add_argument('--context', required=True); p.add_argument('--out', required=True)
    args = parser.parse_args()
    if args.command == 'check-policy':
        check_policy(json.loads((ISOLATION / 'action-policy.json').read_text()), declarations())
        print('Action policy and current declarations agree; runtime qualification not performed.')
    elif args.command == 'pin':
        pin(args.out)
    elif args.command == 'fingerprint':
        files = fingerprint_files(Path(args.directory), args.kind == 'deployment')
        print(canonical({'files': files, 'sha256': fingerprint_map(files)}).decode(), end='')
    elif args.command == 'preflight':
        issues = preflight(args.run_directory)
        for issue in issues:
            print('FAIL: ' + issue)
        print('Preflight BLOCKED; do not start tracing.' if issues else 'Preflight PASS; author may now prepare the approved tracing window.')
        if issues:
            raise SystemExit(2)
    else:
        context = json.loads(Path(args.context).read_text())
        rules = json.loads((ISOLATION / 'classification-rules.json').read_text())
        candidate_path = Path(args.out).with_suffix('.candidates.jsonl')
        candidate_path.parent.mkdir(parents=True, exist_ok=True)
        with candidate_path.open('w', encoding='utf-8', newline='\n') as stream:
            result = classify(lambda: parse_trace(args.trace), context, rules,
                              lambda candidate: stream.write(json.dumps(candidate, separators=(',', ':')) + '\n'))
        result['candidateFile'] = candidate_path.name
        result['candidateSha256'] = file_digest(candidate_path)
        result['traceSha256'] = file_digest(args.trace)
        result['contextSha256'] = digest(canonical(context))
        result['rulesSha256'] = digest(canonical(rules))
        write(args.out, result)
        print(result['runStatus'] + ': ' + result['reason'])
        if not result['g1Eligible']:
            raise SystemExit(2)


if __name__ == '__main__':
    main()
