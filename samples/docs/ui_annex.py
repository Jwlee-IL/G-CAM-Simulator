"""Portable STR stills: consume only the selected record's hashes and confined milestone JPEGs."""
import base64
import json
from pathlib import Path, PurePosixPath
import struct

from software_model import ROOT, require, sha, inline, parse
from edge_output import privacy


def confined(root, relative):
    require(isinstance(relative, str) and relative and '\\' not in relative,
            'frames: invalid relative path')
    parts = PurePosixPath(relative)
    require(not parts.is_absolute() and all(p not in ('..', '.') for p in relative.split('/'))
            and ':' not in relative, 'frames: path outside frame folder')
    path = root / relative
    require(not any(p.is_symlink() or getattr(p.lstat(), 'st_file_attributes', 0) & 0x400
                    for p in [path, *path.parents] if p.is_relative_to(root) and p.exists()),
            'frames: reparse path')
    require(path.resolve().is_relative_to(root.resolve()), 'frames: path outside frame folder')
    return path


def jpeg_size(data):
    """Read the SOF canvas, without a second image decoder or nonstandard dependency."""
    require(data[:2] == b'\xff\xd8' and data[-2:] == b'\xff\xd9', 'frames: invalid JPEG')
    offset = 2
    while offset + 4 <= len(data):
        require(data[offset] == 255, 'frames: invalid JPEG marker')
        marker = data[offset + 1]; offset += 2
        length = int.from_bytes(data[offset:offset + 2], 'big')
        require(length >= 2 and offset + length <= len(data), 'frames: invalid JPEG segment')
        if marker in (0xc0, 0xc1, 0xc2):
            require(length >= 8, 'frames: invalid JPEG dimensions')
            height, width = struct.unpack('>HH', data[offset + 3:offset + 7])
            require(width > 0 and height > 0, 'frames: invalid JPEG dimensions')
            return width, height
        require(marker != 0xda, 'frames: JPEG lacks canvas')
        offset += length
    raise ValueError('frames: JPEG lacks canvas')


def selected_desktop(selection, records, cases):
    by_run = {r['runId']: r for r in records}
    result = []
    for case in cases:
        record = by_run[selection['caseSelections'][case['id']]]
        inventory = record.get('desktopAllCaseIds', record.get('desktopCaseIds', list(record.get('desktopEvidence', {}))))
        if record.get('profile') == 'desktop' and case['id'] in inventory:
            result.append((case, record.get('desktopEvidence', {}).get(case['id'], {})))
    return result


def annex(archive, selection, records, cases):
    from software_model import testing_module
    testing_module()
    from ui_contract import video_status
    selected = selected_desktop(selection, records, cases)
    if not selected:
        return [], set()  # M3 retains its original model and has no annex.
    folder = archive / 'frames'
    manifest = confined(folder, 'manifest.json')
    rows = {}
    inputs = set()
    if manifest.is_file():
        raw = manifest.read_bytes(); privacy(raw.decode('utf-8'))
        value = json.loads(raw)
        require(value.get('schema') == 'gcam.str-frames' and value.get('contract') == 'v4'
                and value.get('schemaVersion') == 1, 'frames: unsupported manifest')
        for row in value['cases']:
            require(row['caseId'] not in rows, 'frames: duplicate case')
            rows[row['caseId']] = row
        require(set(rows) == {c['id'] for c, _ in selected}, 'frames: case membership differs')
        inputs.add(manifest.relative_to(ROOT).as_posix())
    source = archive.relative_to(ROOT).as_posix() + '/frames/manifest.json'
    blocks = parse('## UI automation evidence\n\nStills from the exact selected attempts; privacy intervals are never illustrated.\n', source)
    for case, evidence in selected:
        status = evidence.get('videoStatus') or {}
        heading = parse('### ' + case['id'] + '\n', source)
        heading[0]['anchor'] = 'case-' + sha(case['id'].encode())[:20]
        blocks += heading
        reason = None
        try:
            require(evidence.get('contract') == 'v4', 'video-gate: no v4 run record')
            video_status(evidence)
        except ValueError as error:
            reason = str(error)
        row = rows.get(case['id'])
        if row:
            require(row.get('runId') == evidence.get('runId') and row.get('verdict') == case['verdict'],
                    'frames: case/run/verdict differs')
            require(row.get('videoSha256') == status.get('videoSha256'), 'frames: video hash differs from selected run')
        description = ('Run: ' + str(evidence.get('runId')) + '; verdict: ' + case['verdict'] +
                       '; video status: ' + str(status.get('status', 'missing')) +
                       '; checked video SHA-256: ' + str(status.get('videoSha256', 'missing')) + '.')
        blocks.append({'kind': 'para', 'runs': inline(description), 'source': source})
        if reason:
            require(not row or not row.get('frames'), 'frames: unqualified case has images')
            blocks.append({'kind': 'para', 'runs': inline('No frames: ' + reason), 'source': source})
            continue
        require(row is not None and not row.get('reason'), 'frames: qualified case export missing or refused')
        require(row.get('checkContract') == 'v4' and row.get('videoStatus') == 'passed', 'frames: unqualified export')
        canvas = status.get('canvas')
        if canvas is not None:
            require((row['canvasWidthPx'], row['canvasHeightPx']) == (canvas['widthPx'], canvas['heightPx']), 'frames: recorded canvas differs')
        frames = row.get('frames', [])
        require(3 <= len(frames) <= 5 and len({f['file'] for f in frames}) == len(frames), 'frames: frame count or duplicate file')
        steps = {s['id'] for s in status['evidence']['steps'] if not s.get('privacy')}
        cues = status['evidence']['cues']
        for frame in frames:
            require(frame['stepId'] in steps and frame['stepId'] in evidence['executedStepIds'], 'frames: unknown or private step')
            cue = next((c for c in cues if frame['stepId'] in c['stepIds'] and c['text'] == frame['cueText']), None)
            require(cue is not None, 'frames: cue differs')
            start, end = frame['mediaTime100ns'], frame['mediaEnd100ns']
            require(0 <= start < end and start < cue['end100ns'] and end > cue['start100ns'], 'frames: time outside cue')
            require(not any(p['start100ns'] < end and p['end100ns'] > start
                            for p in status['evidence'].get('privacyIntervals', [])), 'frames: private frame')
            # Window-transition intervals (launch, harness-issued state change, close) are never illustrated either;
            # evidence written before the field existed has none.
            require(not any(t['start100ns'] < end and t['end100ns'] > start
                            for t in status['evidence'].get('transitionIntervals') or []), 'frames: transition frame')
            path = confined(folder, frame['file']); data = path.read_bytes()
            require(sha(data) == frame['sha256'], 'frames: JPEG hash differs')
            width, height = jpeg_size(data)
            require((width, height) == (frame['widthPx'], frame['heightPx']) and width <= 960
                    and width <= row['canvasWidthPx'] and height <= row['canvasHeightPx'], 'frames: canvas differs')
            caption = frame['stepId'] + ' · ' + frame['cueText'] + ' · ' + format(start / 10_000_000, '.3f') + ' s'
            privacy(caption)
            blocks.append({'kind': 'image', 'asset': 'frame-' + sha((case['id'] + frame['file']).encode())[:20],
                           'jpeg': base64.b64encode(data).decode(), 'sha256': frame['sha256'], 'width': width, 'height': height,
                           'caption': caption, 'source': source})
            inputs.add(path.relative_to(ROOT).as_posix())
    return blocks, inputs
