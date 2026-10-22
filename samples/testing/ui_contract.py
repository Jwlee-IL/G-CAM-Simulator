"""Shared v4 production checks. These consume the exact attempt, never a latest-video index."""
import datetime as dt
import importlib.util
import json
from pathlib import Path
import re


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def video_status(record, require_pass=True):
    require(record.get('contract') in (None, 'v4'), 'video-contract: unsupported run contract')
    if record.get('contract') is None:
        return None
    status = record.get('videoStatus')
    if not require_pass:
        return status
    require(isinstance(status, dict) and status.get('contract') == 'v4', 'video-gate: missing v4 status')
    check = status.get('check') or {}
    require(status.get('runId') == record.get('runId') and check.get('contract') == 'v4', 'video-gate: different attempt or contract')
    digest = status.get('videoSha256')
    require(isinstance(digest, str) and re.fullmatch('[0-9a-f]{64}', digest)
            and digest == check.get('videoSha256'), 'video-gate: checked hash differs')
    require(status.get('status') == 'passed' and check.get('passed') is True
            and check.get('failedCriteria') == [], 'video-gate: video check did not pass')
    # A v4 record without its executed-step inventory (or with an empty one: every app-driving run records at least its
    # ready step) would make step coverage pass trivially.
    executed = record.get('executedStepIds')
    require(isinstance(executed, list) and executed and all(isinstance(i, str) and i for i in executed),
            'video-gate: missing executedStepIds')
    ids = {s.get('id') for s in (status.get('evidence') or {}).get('steps', []) if isinstance(s, dict)}
    require(set(executed).issubset(ids), 'video-gate: executed steps missing from recording')
    for step in record.get('steps', []):
        if isinstance(step, dict):  # manual reader steps; the scenario record's diagnostic log is plain text
            require(step.get('videoStepId') in executed and step.get('videoStepId') in ids,
                    'video-gate: manual step missing from execution or recording')
    if 'video' in record and not isinstance(record['video'], str):  # the scenario record's 'video' is a note string
        provenance = record['video']
        require(isinstance(provenance, dict) and provenance.get('runId') == record['runId']
                and provenance.get('videoSha256') == digest, 'video-gate: provenance differs')
    return status


def review_list(attempts, previous, predecessor):
    """attempts contain portable identity, source hash and exact run status (failed attempts stay in the list)."""
    rows = []
    for attempt in attempts:
        failed = (attempt['verdict'] != 'passed' or attempt.get('execution') not in (None, 'completed')
                  or (attempt.get('videoStatus') or {}).get('status') != 'passed')
        changed = not attempt.get('sourceSha256') or previous.get(attempt['scenario']) != attempt.get('sourceSha256')
        if failed or changed:
            status = attempt.get('videoStatus') or {}
            rows.append({**attempt, 'reason': 'failed-or-aborted' if failed else 'new-or-changed-scenario',
                         'videoSha256': status.get('videoSha256')})
    return {'contract': 'v4', 'predecessor': predecessor, 'blocking': False, 'attempts': rows}


def validate_approval(path, live, source):
    require(path is not None, 'approval-package: --approvals is required for desktop collection')
    path = Path(path).resolve()
    package = json.loads(path.read_text(encoding='utf-8-sig'))
    require(package.get('contract') == 'v4', 'approval-package: a v4 author package is required')
    require(live.get('workingTree') == 'clean' and package.get('commit') == live.get('sourceCommit') == source[0]
            and package.get('isolationSha256') == live.get('isolationSha256')
            and package.get('deploymentSha256') == live.get('deploymentSha256'), 'approval-package: live target differs')
    date = lambda s: dt.datetime.fromisoformat(s.replace('Z', '+00:00'))
    require(date(package['committedAt']) == date(source[1]), 'approval-package: commit date differs')
    require(re.fullmatch('[A-Za-z0-9][A-Za-z0-9_-]{0,70}', package.get('runId', '')), 'approval-package: invalid run ID')
    records = package.get('approvals', [])
    acts = {'charter', 'desktop-go', 'real-world-effect', 'physical', 'final-review'}
    require(all(a.get('humanActId') in acts for a in records), 'approval-package: named acts required')
    for act in ('charter', 'desktop-go'):
        matches = [a for a in records if a.get('humanActId') == act]
        require(len(matches) == 1, 'approval-package: exactly one ' + act + ' required')
    for record in records:
        require(record.get('id') and record.get('quote', '').strip() and record.get('targetCommit') == package['commit']
                and date(record['quotedAt']) >= date(package['committedAt']), 'approval-package: statement target/date differs')
    require(len({r['id'] for r in records}) == len(records), 'approval-package: duplicate approval IDs')
    charter = package.get('charter') or {}
    require(charter.get('approvalId') == next(a['id'] for a in records if a['humanActId'] == 'charter')
            and charter.get('status') == 'approved' and charter.get('scope') and charter.get('scenarioIds') and all(isinstance(s, str) and s for s in charter['scenarioIds'])
            and any(g.get('id') == 'G0' and g.get('status') == 'passed' for g in charter.get('gates', [])),
            'approval-package: applicable charter qualification required')
    gates = charter.get('gates', [])
    require(len(gates) == 2 and {g.get('id') for g in gates} == {'G0', 'G1'}
            and all(g.get('status') in ('passed', 'blocked') and g.get('reason') for g in gates),
            'approval-package: G0/G1 values and reasons required')
    require(all(g.get('status') == 'passed' for g in gates), 'approval-package: production requires passed G0/G1')
    for effect in package.get('realWorldEffects', []):
        approvals = [r for r in records if r['id'] == effect.get('approvalId') and r['humanActId'] == 'real-world-effect']
        require(len(approvals) == 1 and effect.get('effectId') == 'E6' and effect.get('runId') == package['runId']
                and effect.get('targetCommit') == package['commit'] and effect.get('deploymentSha256') == package['deploymentSha256']
                and effect.get('scenarioIds') and set(effect['scenarioIds']).issubset(charter['scenarioIds'])
                and effect.get('recordFamilies') and set(effect['recordFamilies']).issubset(
                    {'save-dialog-history', 'recent-document-links', 'recent-documents-registry'})
                and effect.get('typedNameTemplates') and set(effect['typedNameTemplates']).issubset(
                    {'report-{runId}.' + ext for ext in ('html', 'pdf', 'docx', 'csv')}), 'approval-package: invalid E6 permission')
    return path


def live_approval(path, repo):
    require(path is not None, 'approval-package: --approvals is required for desktop collection')
    spec = importlib.util.spec_from_file_location('gcam_live_pin', repo / 'tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py')
    stage0 = importlib.util.module_from_spec(spec); spec.loader.exec_module(stage0)
    return validate_approval(path, stage0.pin(), stage0.source_identity())
