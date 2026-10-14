"""GCAM Studio manual editions: assemble, replay and self-test (standard library only).

An edition is assembled from the chapter registry, the static chapters, the Studio notice list and manual run records
(written by the desktop manual scenarios after cleanup). A generated chapter is printed only when the latest run of its
scenario on the edition's one Studio build passed; otherwise the edition carries an omission notice. Figures are the
run's original captures, cropped and overlaid with the marks the runner measured at capture time (rectangles with
numbered badges); nothing is placed or worded by the generator. The generator records; it approves nothing.

  python -B samples/manual/manual_records.py check-registry
  python -B samples/manual/manual_records.py generate --runs <folder> --edition-id <id> --date <yyyy-mm-dd> --out <new>
  python -B samples/manual/manual_records.py check [--edition <folder> | --all]
  python -B samples/manual/manual_records.py self-test [--out <scratch>]
"""
import argparse
import copy
import datetime as dt
import hashlib
import json
import math
import os
from pathlib import Path
import re
import struct
import sys
import tempfile
import zlib

REPO = Path(__file__).resolve().parents[2]
SOURCES = REPO / 'docs/manual'
EDITIONS = SOURCES / 'editions'
RUN_SCHEMA = ('gcam.manual-run', 1)
MARK_SCHEMA = ('gcam.figure-marks', 1)
LEVELS = ('warning', 'caution', 'note')
DEFAULT_BUDGET = 10 * 1024 * 1024
MARGIN = 24
RED, WHITE = (200, 30, 30), (255, 255, 255)
TOKEN = re.compile(r'\[\[(chapter|notice):([A-Za-z0-9.\-]+)\]\]')
PROSE_NOTICE = re.compile(r'^\s*(?:>\s*)?(?:\*\*)?(?:warning|caution|note|important)\b', re.I)
LEAK = re.compile(r'(?:\b[A-Za-z]:[\\/]|\\\\[A-Za-z0-9]|/(?:Users|home)/|\\Users\\)')


def require(condition, code):
    if not condition:
        raise ValueError(code)


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=1, ensure_ascii=False, allow_nan=False) + '\n').encode('utf-8')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read_json(path, code):
    try:
        return json.loads(Path(path).read_bytes())
    except (OSError, ValueError) as error:
        raise ValueError(code + ': ' + Path(path).name) from error


def confined(root, relative):
    """A recorded relative path inside root, resolved links included."""
    require(isinstance(relative, str) and relative and not relative.startswith(('/', '\\'))
            and ':' not in relative and '..' not in re.split(r'[\\/]', relative), 'path-escape: ' + str(relative))
    root = Path(root).resolve()
    path = (root / relative).resolve()
    require(path == root or root in path.parents, 'path-escape: ' + relative)
    return path


# ---------------------------------------------------------------- PNG (8-bit RGB / RGBA, non-interlaced)

def png_decode(data):
    """Returns (width, height, rgb bytes). Accepts the five scanline filters; refuses anything else."""
    require(data[:8] == b'\x89PNG\r\n\x1a\n', 'png: signature')
    pos, header, idat = 8, None, []
    while pos < len(data):
        require(pos + 8 <= len(data), 'png: truncated')
        length, kind = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        require(len(body) == length and pos + 12 + length <= len(data), 'png: truncated')
        require(struct.unpack('>I', data[pos + 8 + length:pos + 12 + length])[0] == zlib.crc32(kind + body), 'png: crc')
        if kind == b'IHDR':
            header = struct.unpack('>IIBBBBB', body)
        elif kind == b'IDAT':
            idat.append(body)
        elif kind == b'IEND':
            break
        pos += 12 + length
    require(header is not None and idat, 'png: missing chunks')
    width, height, depth, colour, compression, filtering, interlace = header
    require(depth == 8 and colour in (2, 6) and compression == 0 and filtering == 0 and interlace == 0, 'png: unsupported format')
    require(0 < width <= 16384 and 0 < height <= 16384, 'png: size')
    channels = 3 if colour == 2 else 4
    stride = width * channels
    try:
        raw = zlib.decompress(b''.join(idat))
    except zlib.error as error:
        raise ValueError('png: corrupt data') from error
    require(len(raw) == height * (stride + 1), 'png: data length')
    out = bytearray()
    previous = bytearray(stride)
    for y in range(height):
        kind = raw[y * (stride + 1)]
        row = bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        if kind == 1:
            for i in range(channels, stride):
                row[i] = (row[i] + row[i - channels]) & 255
        elif kind == 2:
            row = bytearray((a + b) & 255 for a, b in zip(row, previous))
        elif kind == 3:
            for i in range(stride):
                left = row[i - channels] if i >= channels else 0
                row[i] = (row[i] + ((left + previous[i]) >> 1)) & 255
        elif kind == 4:
            for i in range(stride):
                a = row[i - channels] if i >= channels else 0
                b = previous[i]
                c = previous[i - channels] if i >= channels else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                row[i] = (row[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        else:
            require(kind == 0, 'png: filter')
        out += row if channels == 3 else b''.join(row[i:i + 3] for i in range(0, stride, 4))
        previous = row
    return width, height, bytes(out)


def png_encode(width, height, rgb):
    """Deterministic RGB PNG: filter 0 on every row, zlib level 9, no ancillary chunks."""
    require(len(rgb) == width * height * 3, 'png: pixel length')
    stride = width * 3
    raw = b''.join(b'\x00' + rgb[y * stride:(y + 1) * stride] for y in range(height))
    def chunk(kind, body):
        return struct.pack('>I', len(body)) + kind + body + struct.pack('>I', zlib.crc32(kind + body))
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 2, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


# ---------------------------------------------------------------- marks and overlays

GLYPHS = {'0': '111101101101111', '1': '010110010010111', '2': '111001111100111', '3': '111001111001111',
          '4': '101101111001001', '5': '111100111001111', '6': '111100111101111', '7': '111001001001001',
          '8': '111101111101111', '9': '111101111001111'}
SCALE = 3


def image_rect(screen, origin):
    """Physical screen rectangle (x, y, w, h) → integer image rectangle; floor the near edge, ceil the far edge."""
    x, y, w, h = screen
    left, top = math.floor(x - origin[0]), math.floor(y - origin[1])
    return [left, top, math.ceil(x + w - origin[0]) - left, math.ceil(y + h - origin[1]) - top]


def validate_marks(doc, width, height, capture_sha):
    require((doc.get('schema'), doc.get('schemaVersion')) == MARK_SCHEMA, 'mark-schema')
    require(doc.get('captureSha256') == capture_sha, 'mark-capture: marks name another capture')
    require((doc.get('widthPx'), doc.get('heightPx')) == (width, height), 'mark-capture: size differs from the PNG')
    origin = doc.get('frameOriginPx')
    require(isinstance(origin, list) and len(origin) == 2 and all(isinstance(v, int) for v in origin), 'mark-geometry: origin')
    rects = []
    for item in [doc.get('region')] if doc.get('region') else []:
        rects.append(('region', item))
    marks = doc.get('marks')
    require(isinstance(marks, list), 'mark-schema: marks')
    for index, mark in enumerate(marks, 1):
        require(mark.get('n') == index, 'mark-numbering: marks must be numbered 1..n')
        require(isinstance(mark.get('label'), str) and mark['label'].strip(), 'mark-label: empty label')
        require(isinstance(mark.get('target'), str) and mark['target'], 'mark-target: empty target')
        rects.append(('mark', mark))
    for kind, item in rects:
        screen, rect = item.get('screen'), item.get('image')
        require(isinstance(screen, list) and len(screen) == 4 and all(isinstance(v, (int, float)) and math.isfinite(v) for v in screen),
                'mark-geometry: screen rectangle')
        require(isinstance(rect, list) and len(rect) == 4 and all(isinstance(v, int) and not isinstance(v, bool) for v in rect),
                'mark-geometry: image rectangle must be integers')
        require(rect == image_rect(screen, origin), 'mark-geometry: image rectangle is not the measured screen rectangle')
        left, top, w, h = rect
        require(w > 0 and h > 0, 'mark-bounds: empty rectangle')
        require(left >= 0 and top >= 0 and left + w <= width and top + h <= height, 'mark-bounds: rectangle outside the image')
    return marks


def crop_rect(doc, width, height):
    if doc.get('crop') == 'full':
        return [0, 0, width, height]
    boxes = [m['image'] for m in doc['marks']] + ([doc['region']['image']] if doc.get('region') else [])
    if not boxes:
        return [0, 0, width, height]
    left = max(0, min(b[0] for b in boxes) - MARGIN)
    top = max(0, min(b[1] for b in boxes) - MARGIN)
    right = min(width, max(b[0] + b[2] for b in boxes) + MARGIN)
    bottom = min(height, max(b[1] + b[3] for b in boxes) + MARGIN)
    return [left, top, right - left, bottom - top]


def derive_figure(width, height, rgb, doc):
    """Crop + rectangles + numbered badges. Returns (w, h, rgb, crop, flags)."""
    cx, cy, cw, ch = crop_rect(doc, width, height)
    stride = width * 3
    out = bytearray(b''.join(rgb[(cy + y) * stride + cx * 3:(cy + y) * stride + (cx + cw) * 3] for y in range(ch)))
    def fill(x0, y0, x1, y1, colour):
        for y in range(max(0, y0), min(ch, y1)):
            for x in range(max(0, x0), min(cw, x1)):
                out[(y * cw + x) * 3:(y * cw + x) * 3 + 3] = bytes(colour)
    flags, badges = [], []
    for mark in doc['marks']:
        left, top, w, h = mark['image']
        left, top = left - cx, top - cy
        for t in range(2):   # 2 px border, outside the control where room allows
            fill(left - 2 + t, top - 2 + t, left + w + 2 - t, top - 1 + t, RED)
            fill(left - 2 + t, top + h + 1 - t, left + w + 2 - t, top + h + 2 - t, RED)
            fill(left - 2 + t, top - 2 + t, left - 1 + t, top + h + 2 - t, RED)
            fill(left + w + 1 - t, top - 2 + t, left + w + 2 - t, top + h + 2 - t, RED)
        digits = str(mark['n'])
        bw, bh = len(digits) * 4 * SCALE - SCALE + 6, 5 * SCALE + 6
        candidates = [(left - 2, top - bh - 3), (left - bw - 3, top - 2), (left - 2, top + h + 3), (left + 1, top + 1)]
        inside = [c for c in candidates if c[0] >= 0 and c[1] >= 0 and c[0] + bw <= cw and c[1] + bh <= ch]
        free = [c for c in inside if all(c[0] + bw <= b[0] or b[0] + b[2] <= c[0] or c[1] + bh <= b[1] or b[1] + b[3] <= c[1] for b in badges)]
        if free:
            bx, by = free[0]
        else:
            bx, by = inside[0] if inside else (min(max(0, left), max(0, cw - bw)), min(max(0, top), max(0, ch - bh)))
            flags.append(f'badge-placement:{mark["n"]}')
        badges.append((bx, by, bw, bh))
        fill(bx, by, bx + bw, by + bh, RED)
        for k, digit in enumerate(digits):
            for gy in range(5):
                for gx in range(3):
                    if GLYPHS[digit][gy * 3 + gx] == '1':
                        x0 = bx + 3 + k * 4 * SCALE + gx * SCALE
                        fill(x0, by + 3 + gy * SCALE, x0 + SCALE, by + 3 + (gy + 1) * SCALE, WHITE)
    return cw, ch, bytes(out), [cx, cy, cw, ch], flags


# ---------------------------------------------------------------- sources

def slug(text):
    return re.sub(r'\s', '-', re.sub(r'[^\w\- ]', '', text.lower()))


def load_sources(root):
    """Registry, notices and static chapters, validated together. Returns a dict with bytes for retention."""
    root = Path(root)
    files = {}
    def take(relative, code):
        path = confined(root, relative)
        require(path.is_file(), code + ': missing ' + relative)
        files[relative] = path.read_bytes()
        return files[relative]
    registry = json.loads(take('chapters.json', 'registry'))
    require((registry.get('schema'), registry.get('schemaVersion')) == ('gcam.manual-registry', 1), 'registry: unknown schema')
    require(registry.get('locale') == 'en', 'registry: locale')
    parts = {p['number']: p['title'] for p in registry['parts']}
    require(len(parts) == len(registry['parts']), 'registry: duplicate part')
    numbers, scenarios = set(), set()
    for chapter in registry['chapters']:
        number, kind = chapter['number'], chapter['source']
        require(re.fullmatch(r'\d+\.\d+', number) and number not in numbers, 'registry: duplicate or malformed chapter ' + number)
        numbers.add(number)
        require(number.split('.')[0] == chapter['part'] and chapter['part'] in parts, 'registry: chapter part ' + number)
        require(kind in ('generated', 'static') and (('scenario' in chapter) == (kind == 'generated'))
                and (('static' in chapter) == (kind == 'static')), 'registry: generated/static collision ' + number)
        if kind == 'generated':
            require(chapter['scenario'] not in scenarios, 'registry: duplicate scenario ' + chapter['scenario'])
            scenarios.add(chapter['scenario'])
    require(all(any(c['part'] == p for c in registry['chapters']) for p in parts), 'registry: empty part')
    notices = json.loads(take('notices.json', 'notices'))
    require((notices.get('schema'), notices.get('schemaVersion')) == ('gcam.manual-notices', 1), 'notices: unknown schema')
    by_id = {}
    for notice in notices['notices']:
        require(re.fullmatch(r'N-[A-Z0-9-]+', notice['id']) and notice['id'] not in by_id, 'notices: duplicate or malformed id')
        require(notice['level'] in LEVELS and notice['text'].strip() and notice['sources'], 'notices: incomplete ' + notice['id'])
        require(set(notice['chapters']) <= numbers, 'notices: unknown chapter in ' + notice['id'])
        by_id[notice['id']] = notice
    static = {}
    for chapter in registry['chapters']:
        if chapter['source'] != 'static':
            continue
        text = take(chapter['static'], 'static').decode('utf-8')
        require('\r' not in text, 'static: CRLF in ' + chapter['static'])
        lines = text.split('\n')
        draft = [line for line in lines if line.startswith('> DRAFT')]
        body = '\n'.join(line for line in lines if not line.startswith('> DRAFT')).strip('\n')
        require(not re.search(r'^#{1,3} ', body, re.M), 'static: headings above level 4 in ' + chapter['static'])
        for kind, value in TOKEN.findall(body):
            require(value in (numbers if kind == 'chapter' else by_id), f'static: unknown {kind} {value}')
            if kind == 'notice':
                require(chapter['number'] in by_id[value]['chapters'], f'notice-chapter: {value} not allowed in {chapter["number"]}')
        static[chapter['number']] = {'body': body, 'draft': bool(draft), 'path': chapter['static']}
    return {'registry': registry, 'parts': parts, 'notices': by_id, 'noticeStatus': notices.get('status'),
            'static': static, 'files': files}


# ---------------------------------------------------------------- run records

def load_runs(runs_root, sources):
    runs = []
    root = Path(runs_root)
    for path in sorted(root.glob('*/manual-run.json')) if root.is_dir() else []:
        record = read_json(path, 'run-schema')
        require((record.get('schema'), record.get('schemaVersion')) == RUN_SCHEMA, 'run-schema: ' + path.parent.name)
        require(record.get('runId') == path.parent.name, 'run-id: folder and record differ for ' + path.parent.name)
        runs.append((path.parent, record, path.read_bytes()))
    scenarios = {c['scenario']: c for c in sources['registry']['chapters'] if c['source'] == 'generated'}
    subjects = {r['subject']['deploymentSha256'] for _, r, _ in runs}
    require(len(subjects) <= 1, 'mixed-binaries: run records come from more than one Studio build')
    for folder, record, _ in runs:
        require(record['scenario'] in scenarios, 'unknown-scenario: ' + record['scenario'])
        chapter = scenarios[record['scenario']]
        require(record['chapter'] == {'number': chapter['number'], 'title': chapter['title']}, 'registry-mismatch: ' + record['runId'])
        require(record['environment'].get('uiLocale') == sources['registry']['locale'], 'locale-mismatch: ' + record['runId'])
        require(record['subject'].get('unchanged') is True, 'subject-changed: ' + record['runId'])
        dt.datetime.fromisoformat(record['startedAt'])
    return runs


def validate_chosen(folder, record, sources, chapter):
    """Full validation of the run a printed chapter comes from. Returns figure inputs."""
    evidence = {}
    for item in record['evidence']:
        path = confined(folder, item['path'])
        require(item['path'] not in evidence, 'evidence-duplicate: ' + item['path'])
        require(path.is_file(), 'evidence-missing: ' + item['path'])
        data = path.read_bytes()
        require(len(data) == item['bytes'] and sha(data) == item['sha256'], 'evidence-hash: ' + item['path'])
        evidence[item['path']] = data
    steps = record['steps']
    require(steps and [s['n'] for s in steps] == list(range(1, len(steps) + 1)), 'step-order: ' + record['runId'])
    require(all(s['verdict'] == 'passed' for s in steps) and all(c['result'] == 'pass' for s in steps for c in s['checks']),
            'verdict-aggregate: a passed record holds a failed step or check')
    require(any(s['checks'] for s in steps), 'no-checks: a chapter needs automatic checks')
    figures = []
    for step in steps:
        require(not PROSE_NOTICE.search(step['text']) and not LEAK.search(step['text']), 'prose-notice: warning wording or path in step text')
        for kind, value in TOKEN.findall(step['text']):
            require(kind == 'chapter' and any(c['number'] == value for c in sources['registry']['chapters']), 'chapter-ref: ' + value)
        for notice in step['notices']:
            require(notice in sources['notices'], 'notice-unknown: ' + notice)
            require(chapter['number'] in sources['notices'][notice]['chapters'], f'notice-chapter: {notice} not allowed in {chapter["number"]}')
        for figure in step['figures']:
            require(figure['capture'] in evidence and figure['marks'] in evidence, 'evidence-missing: figure ' + figure['id'])
            width, height, rgb = png_decode(evidence[figure['capture']])
            doc = json.loads(evidence[figure['marks']])
            validate_marks(doc, width, height, sha(evidence[figure['capture']]))
            figures.append((step['n'], figure, width, height, rgb, doc, evidence[figure['capture']], evidence[figure['marks']]))
    return figures


# ---------------------------------------------------------------- assembly

def render_notice(notice):
    return f'> **{notice["level"].capitalize()}.** {notice["text"]}'


def resolve_tokens(text, chapters, printed):
    def replace(match):
        kind, value = match.groups()
        if kind == 'notice':
            return match.group(0)
        title = chapters[value]['title']
        if value in printed:
            return f'[chapter {value} ({title})](#{slug(value + " " + title)})'
        return f'chapter {value} ({title}, omitted from this edition)'
    return TOKEN.sub(replace, text)


def assemble(sources, runs, edition_id, date, formal, budget, reuse=None, generator=None):
    """Returns {relative path: bytes} for the whole edition, generation record included."""
    registry, notices = sources['registry'], sources['notices']
    chapters = {c['number']: c for c in registry['chapters']}
    by_scenario = {}
    for folder, record, raw in runs:
        by_scenario.setdefault(record['scenario'], []).append((folder, record, raw))
    outputs, figures_out, chapter_rows, flags, run_rows, chapter_hashes = {}, [], [], [], [], {}
    chosen_runs, printed, plans = set(), set(), {}
    for chapter in registry['chapters']:
        if chapter['source'] == 'static':
            printed.add(chapter['number'])
            continue
        candidates = sorted(by_scenario.get(chapter['scenario'], []), key=lambda r: dt.datetime.fromisoformat(r[1]['startedAt']))
        if len(candidates) > 1:
            require(candidates[-1][1]['startedAt'] != candidates[-2][1]['startedAt'], 'selection-tie: ' + chapter['number'])
        rows = [{'runId': r['runId'], 'startedAt': r['startedAt'], 'status': r['status'],
                 'execution': r['execution']['status'], 'selection': 'superseded'} for _, r, _ in candidates]
        omission = None
        if not candidates:
            omission = 'no run of its scenario on this build'
        else:
            folder, record, _ = candidates[-1]
            rows[-1]['selection'] = 'latest'
            if record['execution']['status'] != 'completed':
                omission = f'the latest run ({record["runId"]}) was aborted: {record["execution"].get("abortReason")}'
            elif record['status'] != 'passed':
                omission = f'the latest run ({record["runId"]}) did not pass ({record["status"]})'
            else:
                plans[chapter['number']] = (folder, record, validate_chosen(folder, record, sources, chapter))
                printed.add(chapter['number'])
                chosen_runs.add(record['runId'])
        chapter_rows.append({'number': chapter['number'], 'scenario': chapter['scenario'], 'candidates': rows, 'omission': omission})
    for notice in notices.values():   # every allowed placement in a printed chapter is required, never invented
        for number in notice['chapters']:
            if number not in printed:
                continue
            if chapters[number]['source'] == 'static':
                placed = f'[[notice:{notice["id"]}]]' in sources['static'][number]['body']
            else:
                placed = any(notice['id'] in s['notices'] for s in plans[number][1]['steps'])
            require(placed, f'notice-missing: {notice["id"]} has no placement in {number}')
    lines = [f'# {registry["title"]}', '',
             f'Edition `{edition_id}` · {date} · English · generated from UI-automation runs of one GCAM Studio build '
             f'(deployment `{runs[0][1]["subject"]["deploymentSha256"][:12] if runs else "none"}`). '
             'How each chapter was produced: `generation-record.json` beside this file.', '']
    draft = [f'static chapter {n} is a draft' for n, s in sorted(sources['static'].items()) if s['draft']]
    if sources['noticeStatus'] != 'approved':
        draft.append('the notice list is not approved')
    omitted = [r['number'] for r in chapter_rows if r['omission']]
    if omitted:
        draft.append('chapters omitted: ' + ', '.join(omitted))
    contents = ['## Contents', '']
    body = []
    for part_number, part_title in [(p['number'], p['title']) for p in registry['parts']]:
        contents.append(f'- {part_number} {part_title}')
        body += [f'## {part_number} {part_title}', '']
        for chapter in [c for c in registry['chapters'] if c['part'] == part_number]:
            number, title = chapter['number'], chapter['title']
            heading = f'### {number} {title}'
            contents.append(f'  - [{number} {title}](#{slug(number + " " + title)})')
            text = [heading, '']
            if chapter['source'] == 'static':
                for line in resolve_tokens(sources['static'][number]['body'], chapters, printed).split('\n'):
                    token = re.fullmatch(r'\[\[notice:([A-Za-z0-9\-]+)\]\]', line.strip())
                    text.append(render_notice(notices[token.group(1)]) if token else line)
                text.append('')
            elif number not in printed:
                row = next(r for r in chapter_rows if r['number'] == number)
                text += [f'> **Omitted from this edition.** This chapter is generated from an automated run of the viewer; '
                         f'it is not included because {row["omission"]}.', '']
            else:
                folder, record, figures = plans[number]
                figure_map = {f[1]['id']: f for f in figures}
                for step in record['steps']:
                    for notice in step['notices']:
                        text += [render_notice(notices[notice]), '']
                    text += [f'**Step {step["n"]}.** ' + resolve_tokens(step['text'], chapters, printed), '']
                    for figure in step['figures']:
                        _, meta, width, height, rgb, doc, png, marks_raw = figure_map[figure['id']]
                        fw, fh, frgb, crop, fig_flags = derive_figure(width, height, rgb, doc)
                        name = f'figures/{figure["id"]}.png'
                        encoded = (reuse(name, fw, fh, frgb) if reuse else None) or png_encode(fw, fh, frgb)
                        outputs[name] = encoded
                        legend = ' · '.join(f'**{m["n"]}** {escape(m["label"])}' for m in doc['marks'])
                        text += [f'![Figure {figure["id"]}]({name})', '',
                                 f'*Figure {figure["id"]}.* {escape(figure["caption"])}' + (f' — {legend}' if legend else ''), '']
                        figures_out.append({'figure': figure['id'], 'chapter': number, 'runId': record['runId'], 'step': step['n'],
                                            'source': figure['capture'], 'sourceSha256': sha(png), 'marksSha256': sha(marks_raw),
                                            'operation': {'crop': crop, 'overlay': 'rectangles+numbered-badges-v1'},
                                            'path': name, 'resultSha256': sha(encoded),
                                            'pixelSha256': sha(struct.pack('>II', fw, fh) + frgb), 'flags': fig_flags})
                        flags += [f'{figure["id"]}:{f}' for f in fig_flags]
                    for missing in step.get('missingFigures', []):
                        text += [f'> **Figure omitted.** Figure {missing["id"]} was not retained: the capture was rejected '
                                 f'({", ".join(missing["reasons"])}).', '']
                        flags.append(f'{missing["id"]}:capture-rejected')
            chapter_text = '\n'.join(text)
            chapter_hashes[number] = sha(chapter_text.encode('utf-8'))
            body.append(chapter_text)
    if draft:
        lines += ['> **Draft edition.** ' + '; '.join(draft) + '.', '']
    markdown = '\n'.join(lines + contents + [''] + body).rstrip('\n') + '\n'
    outputs['MANUAL.Studio.md'] = markdown.encode('utf-8')
    for relative, data in sources['files'].items():
        outputs['sources/manual/' + relative] = data
    for folder, record, raw in runs:
        retained = 'full' if record['runId'] in chosen_runs else 'record-only'
        outputs[f'sources/runs/{record["runId"]}/manual-run.json'] = raw
        if retained == 'full':
            for item in record['evidence']:
                outputs[f'sources/runs/{record["runId"]}/{item["path"]}'] = confined(folder, item['path']).read_bytes()
        run_rows.append({'runId': record['runId'], 'scenario': record['scenario'], 'sha256': sha(raw), 'retained': retained})
    for relative, data in outputs.items():
        if relative.endswith('.json') or relative.endswith('.md'):
            require(not identity_leak(data.decode('utf-8')), 'identity-leak: ' + relative)
    status = 'draft' if draft or flags else 'formal'
    if formal:
        require(status == 'formal', 'formal-refused: ' + '; '.join(draft + flags))
    size = sum(len(v) for v in outputs.values())
    record = {'schema': 'gcam.manual-generation', 'schemaVersion': 1, 'editionId': edition_id, 'date': date,
              'formal': formal, 'budgetBytes': budget, 'status': status, 'draftReasons': draft, 'flags': flags,
              'generator': generator or {'path': 'samples/manual/manual_records.py', 'sha256': sha(Path(__file__).read_bytes())},
              'registry': {'id': registry['id'], 'locale': registry['locale'], 'sha256': sha(sources['files']['chapters.json'])},
              'notices': {'sha256': sha(sources['files']['notices.json']), 'status': sources['noticeStatus']},
              'static': [{'chapter': n, 'path': s['path'], 'sha256': sha(sources['files'][s['path']]), 'draft': s['draft']}
                         for n, s in sorted(sources['static'].items())],
              'subject': {'deploymentSha256': runs[0][1]['subject']['deploymentSha256'] if runs else None},
              'runs': run_rows, 'chapters': chapter_rows, 'chapterOutputSha256': chapter_hashes, 'figures': figures_out,
              'outputs': {k: sha(v) for k, v in sorted(outputs.items())}, 'bytesExcludingRecord': size,
              'reviews': 'none recorded here: figure, source and edition review are the author\'s acts outside the generator'}
    outputs['generation-record.json'] = canonical(record)
    total = size + len(outputs['generation-record.json'])
    require(total <= budget, f'budget: edition is {total} bytes, budget {budget}')
    return outputs


def escape(text):
    return re.sub(r'([\\`*_\[\]<>|])', r'\\\1', text)


def identity_leak(text):
    if LEAK.search(text):
        return True
    names = {os.environ.get(k, '') for k in ('USERNAME', 'USER', 'COMPUTERNAME')}
    names = {n for n in names if len(n) >= 4 and n.lower() not in ('runner', 'root', 'user', 'admin', 'administrator')}
    return any(re.search(r'\b' + re.escape(n) + r'\b', text, re.I) for n in names)


# ---------------------------------------------------------------- commands

def generate(sources_root, runs_root, edition_id, date, out, formal=False, budget=DEFAULT_BUDGET):
    require(re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]{0,63}', edition_id or ''), 'edition: id')
    dt.date.fromisoformat(date)
    out = Path(out)
    require(not out.exists(), 'edition: output folder exists (editions are never overwritten)')
    sources = load_sources(sources_root)
    outputs = assemble(sources, load_runs(runs_root, sources), edition_id, date, formal, budget)
    for relative, data in outputs.items():
        path = out / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    return outputs


def check(edition):
    """Replays a retained edition from its own sources and compares every file byte for byte. A figure whose PNG
    encoding differs (another zlib build) is accepted only when it decodes to the regenerated pixels."""
    edition = Path(edition)
    record = read_json(edition / 'generation-record.json', 'check: generation record')
    sources = load_sources(edition / 'sources/manual')
    runs = load_runs(edition / 'sources/runs', sources)
    def reuse(name, width, height, rgb):
        path = edition / name
        if path.is_file():
            data = path.read_bytes()
            try:
                if png_decode(data) == (width, height, rgb):
                    return data
            except ValueError:
                return None
        return None
    outputs = assemble(sources, runs, record['editionId'], record['date'], record['formal'], record['budgetBytes'],
                       reuse, record['generator'])
    actual = {p.relative_to(edition).as_posix() for p in edition.rglob('*') if p.is_file()}
    require(actual == set(outputs), 'check-differs: file set ' + ', '.join(sorted(actual ^ set(outputs))))
    for relative, data in outputs.items():
        require((edition / relative).read_bytes() == data, 'check-differs: ' + relative)
    if record['generator']['sha256'] != sha(Path(__file__).read_bytes()):
        print(f'note: {edition.name} was generated by an earlier generator revision; the current one reproduces it')
    return record


def check_all():
    load_sources(SOURCES)
    editions = sorted(p.parent for p in EDITIONS.glob('*/generation-record.json')) if EDITIONS.is_dir() else []
    for edition in editions:
        check(edition)
    print(f'manual: sources valid; {len(editions)} committed editions replayed (none is not a passing edition)')


# ---------------------------------------------------------------- self-test

def fixture(root):
    """One valid input set: a static and two generated chapters, notices and two passed runs on one build."""
    src, runs = root / 'sources', root / 'runs'
    (src / 'static').mkdir(parents=True)
    registry = {'schema': 'gcam.manual-registry', 'schemaVersion': 1, 'id': 'fixture', 'locale': 'en', 'title': 'Fixture manual',
                'parts': [{'number': '1', 'title': 'One'}],
                'chapters': [{'number': '1.1', 'title': 'Intro', 'part': '1', 'source': 'static', 'static': 'static/intro.md'},
                             {'number': '1.2', 'title': 'Alpha', 'part': '1', 'source': 'generated', 'scenario': 'manual.alpha'},
                             {'number': '1.3', 'title': 'Beta', 'part': '1', 'source': 'generated', 'scenario': 'manual.beta'}]}
    notices = {'schema': 'gcam.manual-notices', 'schemaVersion': 1, 'status': 'approved',
               'notices': [{'id': 'N-SIM', 'level': 'caution', 'text': 'Simulated.', 'chapters': ['1.1', '1.2'], 'sources': ['fixture']}]}
    (src / 'chapters.json').write_bytes(canonical(registry))
    (src / 'notices.json').write_bytes(canonical(notices))
    (src / 'static/intro.md').write_bytes(b'Intro text, see [[chapter:1.2]].\n\n[[notice:N-SIM]]\n')
    width, height = 64, 48
    rgb = b''.join(bytes(p) for p in ((x * 6 % 256, y * 8 % 256, (x + y) % 256) for y in range(height) for x in range(width)))
    png = png_encode(width, height, rgb)
    for scenario, number, title, minute, notice in (('manual.alpha', '1.2', 'Alpha', 1, ['N-SIM']), ('manual.beta', '1.3', 'Beta', 2, [])):
        run_id = f'run-{scenario.split(".")[1]}'
        folder = runs / run_id / 'captures'
        folder.mkdir(parents=True)
        (folder / 'f1.png').write_bytes(png)
        marks = {'schema': 'gcam.figure-marks', 'schemaVersion': 1, 'capture': 'captures/f1.png', 'captureSha256': sha(png),
                 'widthPx': width, 'heightPx': height, 'frameOriginPx': [100, 50], 'dpi': 96, 'crop': 'marks',
                 'marks': [{'n': 1, 'target': 'StartAcquisition', 'label': 'Start', 'labelSource': 'name',
                            'screen': [110.25, 60.0, 8.5, 6.0], 'image': image_rect([110.25, 60.0, 8.5, 6.0], [100, 50])}]}
        (folder / 'f1.marks.json').write_bytes(canonical(marks))
        evidence = [{'path': 'captures/f1.png', 'sha256': sha(png), 'bytes': len(png), 'role': 'capture'},
                    {'path': 'captures/f1.marks.json', 'sha256': sha(canonical(marks)), 'bytes': len(canonical(marks)), 'role': 'marks'}]
        record = {'schema': RUN_SCHEMA[0], 'schemaVersion': RUN_SCHEMA[1], 'runId': run_id, 'scenario': scenario,
                  'chapter': {'number': number, 'title': title}, 'startedAt': f'2026-10-14T10:0{minute}:00+00:00',
                  'finishedAt': f'2026-10-14T10:0{minute}:30+00:00', 'status': 'passed',
                  'execution': {'status': 'completed', 'uiRunStatus': 'completed', 'abortReason': None},
                  'subject': {'deploymentSha256': 'a' * 64, 'unchanged': True}, 'tool': {'commit': 'b' * 40, 'dirtyFiles': 0},
                  'environment': {'uiLocale': 'en'},
                  'steps': [{'n': 1, 'text': 'Click **Start**.', 'labels': {'StartAcquisition': 'Start'}, 'notices': notice,
                             'checks': [{'id': 'fixture.start', 'result': 'pass', 'detail': 'state Acquiring'}],
                             'figures': [{'id': number + '-1', 'capture': 'captures/f1.png', 'marks': 'captures/f1.marks.json',
                                          'caption': 'The top bar.'}], 'missingFigures': [], 'verdict': 'passed'}],
                  'evidence': evidence}
        (runs / run_id / 'manual-run.json').write_bytes(canonical(record))
    return src, runs


def edit_json(path, change):
    value = json.loads(path.read_bytes())
    before = canonical(value)
    change(value)
    after = canonical(value)
    require(before != after, 'self-test: mutation did not change ' + path.name)
    path.write_bytes(after)


def self_test(out=None):
    base = Path(out) if out else Path(tempfile.mkdtemp(prefix='gcam-manual-selftest-'))
    base.mkdir(parents=True, exist_ok=True)
    results = []
    def case(name, mutate, expect, formal=False, budget=DEFAULT_BUDGET):
        root = base / name
        require(not root.exists(), 'self-test: scratch exists ' + name)
        src, runs = fixture(root)
        mutate(src, runs)
        try:
            outputs = generate(src, runs, 'fixture-1', '2026-10-14', root / 'edition', formal, budget)
        except ValueError as error:
            require(expect.startswith('refuse:') and str(error).startswith(expect[7:]), f'self-test: {name}: wrong refusal {error}')
            results.append((name, str(error)))
            return None
        require(not expect.startswith('refuse:'), f'self-test: {name}: defect accepted')
        markdown = outputs['MANUAL.Studio.md'].decode('utf-8')
        require(expect in markdown, f'self-test: {name}: expected text missing: {expect}')
        results.append((name, 'generated: ' + expect))
        return root / 'edition'
    def run_file(runs, which):
        return runs / f'run-{which}' / 'manual-run.json'
    def marks_file(runs, which):
        return runs / f'run-{which}' / 'captures/f1.marks.json'
    def rehash(runs, which):   # keeps evidence hashes true after a deliberate marks edit
        data = marks_file(runs, which).read_bytes()
        edit_json(run_file(runs, which), lambda r: r['evidence'][1].update(sha256=sha(data), bytes=len(data)))
    def mark_outside(src, runs):
        edit_json(marks_file(runs, 'alpha'), lambda m: m['marks'][0].update(screen=[160.0, 60.0, 8.0, 6.0], image=[60, 10, 8, 6]))
        rehash(runs, 'alpha')
    def double_scaled(src, runs):
        edit_json(marks_file(runs, 'alpha'), lambda m: m['marks'][0].update(image=[15, 15, 13, 9]))
        rehash(runs, 'alpha')
    def failed(src, runs):
        edit_json(run_file(runs, 'beta'), lambda r: (r.update(status='failed'), r['steps'][0].update(verdict='failed')))
    def later_failure(src, runs):
        target = runs / 'run-beta2'
        target.mkdir()
        (target / 'captures').mkdir()
        record = json.loads(run_file(runs, 'beta').read_bytes())
        record.update(runId='run-beta2', startedAt='2026-10-14T11:00:00+00:00', status='failed', evidence=[])
        record['steps'][0].update(verdict='failed', figures=[])
        (target / 'manual-run.json').write_bytes(canonical(record))
    valid = case('valid', lambda s, r: None, '### 1.3 Beta')
    check(valid)
    results.append(('valid-replay', 'check passed'))
    figure = valid / 'figures/1.2-1.png'
    original = figure.read_bytes()
    w, h, rgb = png_decode(original)
    figure.write_bytes(png_encode(w, h, bytes([255 - rgb[0]]) + rgb[1:]))
    try:
        check(valid)
        raise RuntimeError('self-test: tampered figure accepted')
    except ValueError as error:
        require(str(error).startswith('check-differs'), 'self-test: wrong replay refusal ' + str(error))
        results.append(('tampered-figure', str(error)))
    figure.write_bytes(original)
    case('missing-run', lambda s, r: [p.unlink() for p in (r / 'run-beta').rglob('*') if p.is_file()], 'it is not included because no run')
    case('missing-run-formal', lambda s, r: [p.unlink() for p in (r / 'run-beta').rglob('*') if p.is_file()], 'refuse:formal-refused', formal=True)
    case('failed-chapter', failed, 'did not pass (failed)')
    case('failed-chapter-formal', failed, 'refuse:formal-refused', formal=True)
    case('latest-failed-after-pass', later_failure, 'the latest run (run-beta2) did not pass')
    case('evidence-hash', lambda s, r: (r / 'run-alpha/captures/f1.png').write_bytes(png_encode(64, 48, bytes(64 * 48 * 3))), 'refuse:evidence-hash')
    case('mark-outside-image', mark_outside, 'refuse:mark-bounds')
    case('dpi-double-scaling', double_scaled, 'refuse:mark-geometry')
    case('mixed-binaries', lambda s, r: edit_json(run_file(r, 'beta'), lambda x: x['subject'].update(deploymentSha256='c' * 64)), 'refuse:mixed-binaries')
    case('budget-exceeded', lambda s, r: None, 'refuse:budget', budget=2000)
    case('unknown-notice', lambda s, r: edit_json(run_file(r, 'beta'), lambda x: x['steps'][0].update(notices=['N-NONE'])), 'refuse:notice-unknown')
    case('notice-wrong-chapter', lambda s, r: edit_json(run_file(r, 'beta'), lambda x: x['steps'][0].update(notices=['N-SIM'])), 'refuse:notice-chapter')
    case('notice-missing', lambda s, r: edit_json(run_file(r, 'alpha'), lambda x: x['steps'][0].update(notices=[])), 'refuse:notice-missing')
    case('warning-in-prose', lambda s, r: edit_json(run_file(r, 'beta'), lambda x: x['steps'][0].update(text='Caution: hot.')), 'refuse:prose-notice')
    case('path-escape', lambda s, r: edit_json(run_file(r, 'alpha'), lambda x: x['evidence'][0].update(path='../x.png')), 'refuse:path-escape')
    case('registry-collision', lambda s, r: edit_json(s / 'chapters.json', lambda x: x['chapters'][1].update(static='static/intro.md')), 'refuse:registry')
    case('identity-leak', lambda s, r: edit_json(run_file(r, 'beta'), lambda x: x['tool'].update(path='C:' + '\\Users\\someone')), 'refuse:identity-leak')
    for name, outcome in results:
        print(f'{name}: {outcome}')
    print(f'manual self-test: {len(results)} cases passed')
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest='action', required=True)
    p = sub.add_parser('check-registry')
    p.add_argument('--sources', type=Path, default=SOURCES)
    p = sub.add_parser('generate')
    p.add_argument('--sources', type=Path, default=SOURCES)
    p.add_argument('--runs', type=Path, required=True)
    p.add_argument('--edition-id', required=True)
    p.add_argument('--date', required=True)
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--formal', action='store_true', help='refuse unless every chapter is printed and every source approved')
    p.add_argument('--budget-mib', type=float, default=DEFAULT_BUDGET / 1024 / 1024)
    p = sub.add_parser('check')
    group = p.add_mutually_exclusive_group(required=True)
    group.add_argument('--edition', type=Path)
    group.add_argument('--all', action='store_true')
    p = sub.add_parser('self-test')
    p.add_argument('--out', type=Path)
    args = parser.parse_args()
    if args.action == 'check-registry':
        sources = load_sources(args.sources)
        print(f'registry: {len(sources["registry"]["chapters"])} chapters, {len(sources["notices"])} notices, valid')
    elif args.action == 'generate':
        outputs = generate(args.sources, args.runs, args.edition_id, args.date, args.out, args.formal,
                           int(args.budget_mib * 1024 * 1024))
        record = json.loads(outputs['generation-record.json'])
        print(f'edition {args.edition_id}: {record["status"]}, {len(outputs)} files, '
              f'{sum(len(v) for v in outputs.values())} bytes')
    elif args.action == 'check':
        if args.all:
            check_all()
        else:
            print(f'edition {check(args.edition)["editionId"]}: replayed byte for byte')
    else:
        self_test(args.out)
    return 0


if __name__ == '__main__':
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, 'reconfigure'):
            stream.reconfigure(encoding='utf-8', errors='replace')
    try:
        raise SystemExit(main())
    except (ValueError, KeyError, TypeError, OSError) as error:
        print('REFUSED:', type(error).__name__ if not isinstance(error, ValueError) else '', str(error), file=sys.stderr)
        raise SystemExit(1)
