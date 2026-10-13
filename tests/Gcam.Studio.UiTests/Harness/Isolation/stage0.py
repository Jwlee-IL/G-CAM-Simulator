"""Headless isolation inventory, deployment pins, and conservative trace attribution.

Raw machine-wide traces stay local. This tool never starts a trace or launches Studio.
"""
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

REPO = Path(__file__).resolve().parents[4]
ISOLATION = Path(__file__).resolve().parent


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=2, ensure_ascii=True, allow_nan=False) + '\n').encode()


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


def fingerprint(directory):
    entries = [p.relative_to(directory).as_posix() + ':' + digest(p.read_bytes())
               for p in sorted(directory.rglob('*')) if p.is_file() and '__pycache__' not in p.parts]
    return digest('\n'.join(entries).encode())


def pin(out):
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
    binaries = {p.relative_to(deployment).as_posix(): digest(p.read_bytes()) for p in sorted(deployment.rglob('*'))
                if p.is_file() and p.suffix in ('.dll', '.exe', '.json')}
    if not binaries or 'Gcam.Studio.dll' not in binaries:
        raise ValueError('build-required')
    commit = subprocess.run(['git', '--no-optional-locks', 'rev-parse', 'HEAD'], cwd=REPO, check=True, capture_output=True, text=True).stdout.strip()
    dirty = bool(subprocess.run(['git', '--no-optional-locks', 'status', '--porcelain'], cwd=REPO, check=True, capture_output=True, text=True).stdout.strip())
    runner = {p.relative_to(REPO).as_posix(): digest(p.read_bytes()) for p in sorted((REPO / 'tests/Gcam.Studio.UiTests').rglob('*'))
              if p.is_file() and not any(part in ('bin', 'obj', '__pycache__') for part in p.parts)}
    result = {'schemaVersion': 1, 'sourceCommit': commit, 'sourceFiles': source, 'sourceSha256': digest(canonical(source)),
              'workingTree': 'dirty' if dirty else 'clean', 'runnerSourceFiles': runner, 'runnerSourceSha256': digest(canonical(runner)),
              'deploymentFiles': binaries, 'deploymentSha256': digest('\n'.join(k + ':' + v for k, v in binaries.items()).encode()),
              'isolationSha256': fingerprint(ISOLATION),
              'linkBasis': 'same-checkout Release build invocation; source and deployed file hashes; runtime not executed',
              'runtimeQualification': 'not performed'}
    write(out, result)
    return result


def parse_trace(path):
    """Read a normalised fixture or tracerpt XML; unrecognised providers/fields stay unknown."""
    path = Path(path)
    if path.suffix == '.json':
        return json.loads(path.read_text(encoding='utf-8'))
    root = ET.parse(path).getroot()
    rows = []
    for event in root.iter():
        if event.tag.rsplit('}', 1)[-1] != 'Event':
            continue
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
        rows.append({'pid': pid, 'channel': channel, 'operation': data.get('Operation', data.get('Opcode', '')),
                     'destination': data.get('FileName', data.get('KeyName', data.get('ImageName', data.get('daddr', '')))),
                     'parentPid': parent,
                     'at': data.get('TimeCreated', ''), 'provider': provider, 'fields': data})
    return rows


def classify(events, context, rules):
    """Identity/time is required; PID alone cannot promote recycled process events."""
    required = set(context.get('requiredChannels', [])) | set(rules.get('requiredChannels', []))
    candidates, controls = [], {channel: False for channel in sorted(required)}
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
    grew = True
    while grew:
        grew = False
        for event in events:
            if event.get('channel') != 'process' or event.get('operation') != 'ProcessStart':
                continue
            parents = [o for o in tree if o['pid'] == event.get('parentPid') and in_lifetime(o, event)]
            if parents and not any(o['pid'] == event.get('pid') and in_lifetime(o, event) for o in tree):
                tree.append({'pid': event['pid'], 'startedAt': event['at'], 'finishedAt': parents[0]['finishedAt'], 'parentPid': event['parentPid']})
                grew = True
    for event in events:
        pid = event.get('pid')
        owners = [o for o in tree if o['pid'] == pid and in_lifetime(o, event)]
        positive = any(event.get('channel') == c['channel'] and pid == c['pid']
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
        candidates.append({'channel': event.get('channel', 'unknown'), 'pid': pid, 'classification': category,
                           'eventSha256': digest(canonical(event))})
    proven = all(controls.values()) and bool(controls) and bool(owned)
    health = context.get('traceHealth', {})
    loss_proven = (health.get('complete') is True and type(health.get('eventsLost')) is int and type(health.get('buffersLost')) is int
                   and health['eventsLost'] == 0 and health['buffersLost'] == 0)
    unsafe = any(c['classification'] == 'out-of-declaration' for c in candidates)
    unresolved = any(c['classification'] == 'unknown' for c in candidates)
    return {'schemaVersion': 1, 'candidates': candidates, 'positiveControls': controls,
            'runStatus': 'unsafe_effect' if unsafe else 'blocked' if not proven or not loss_proven or unresolved else 'completed',
            'reason': 'undeclared-app-effect' if unsafe else 'instrument-not-proven' if not proven else 'trace-health-not-proven' if not loss_proven else 'unresolved-candidate' if unresolved else '',
            'g1Eligible': proven and loss_proven and not unsafe and not unresolved,
            'blindSpots': context.get('blindSpots', []),
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
    p = sub.add_parser('classify'); p.add_argument('--trace', required=True); p.add_argument('--context', required=True); p.add_argument('--out', required=True)
    args = parser.parse_args()
    if args.command == 'check-policy':
        check_policy(json.loads((ISOLATION / 'action-policy.json').read_text()), declarations())
        print('Action policy and current declarations agree; runtime qualification not performed.')
    elif args.command == 'pin':
        pin(args.out)
    else:
        context = json.loads(Path(args.context).read_text())
        rules = json.loads((ISOLATION / 'classification-rules.json').read_text())
        result = classify(parse_trace(args.trace), context, rules)
        result['traceSha256'] = digest(Path(args.trace).read_bytes())
        result['contextSha256'] = digest(canonical(context))
        result['rulesSha256'] = digest(canonical(rules))
        write(args.out, result)
        print(result['runStatus'] + ': ' + result['reason'])
        if not result['g1Eligible']:
            raise SystemExit(2)


if __name__ == '__main__':
    main()
