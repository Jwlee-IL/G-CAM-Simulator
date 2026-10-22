"""Restricted Markdown and source-aware assembly for the software demonstration set.

No general Markdown fallback: unsupported block syntax and malformed inline syntax fail.
The archived M3 catalog/results remain independent of newly added documentation-tool tests.
"""
import collections
import copy
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import sys
import unicodedata
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[2]
M3 = ROOT / 'samples/testing/records/M3'
M3_SUBJECT = '161e74e770312aef37f16fa43ec396d76f1d0285'
VENDOR_SHA = 'a43bc1afd446f9c4cc66ac5dd45d02e8d65e26fc5344ec0ef787f88d6ddb6f9e'
TITLES = {'SRS': 'Software requirements specification', 'SDS': 'Software design specification',
          'VVR': 'Verification and validation report', 'STR': 'Software test report'}
BANNER = "Demonstration document — generated from the repository's Markdown; not a regulatory submission."


def require(ok, message):
    if not ok:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def slug(text):
    # GitHub-style source headings, retaining Unicode letters and literal hyphens.
    text = re.sub(r'`([^`]+)`', r'\1', text).lower()
    return ''.join(c for c in text if c in ' _-' or unicodedata.category(c)[0] in 'LN').replace(' ', '-')


def plain(runs):
    return ''.join(r['text'] for r in runs)


def inline(text, styles=()):
    """Inline code, emphasis, inline links, and escaped punctuation. No HTML evaluation."""
    out, buffer, i = [], [], 0
    def flush():
        if buffer:
            out.append({'text': ''.join(buffer), 'styles': list(styles)})
            buffer.clear()
    while i < len(text):
        c = text[i]
        if text.startswith('<!--', i):
            end = text.find('-->', i + 4)
            require(end >= 0, 'dialect: unclosed inline comment')
            i = end + 3; continue
        if c == '\\' and i + 1 < len(text) and text[i + 1] in r'\`*_{}[]()#+-.!|<>':
            buffer.append(text[i + 1]); i += 2; continue
        if c == '`':
            flush()
            end = text.find('`', i + 1)
            require(end > i + 1 and not text.startswith('``', i), 'dialect: unbalanced/unsupported code span')
            out.append({'text': text[i + 1:end].replace('\\|', '|'), 'styles': list(styles) + ['code']})
            i = end + 1; continue
        if c == '[':
            require(not text.startswith('[^', i), 'dialect: footnotes are unsupported')
            close = text.find('](', i + 1)
            if close >= 0:
                depth, end = 1, close + 2
                while end < len(text) and depth:
                    if text[end] == '(':
                        depth += 1
                    elif text[end] == ')':
                        depth -= 1
                    end += 1
                require(depth == 0, 'dialect: unbalanced link')
                target = text[close + 2:end - 1]
                require(target and not re.search(r'\s', target), 'dialect: link title/whitespace is unsupported')
                flush()
                linked = inline(text[i + 1:close], styles)
                require(all('target' not in r for r in linked), 'dialect: nested link')
                for r in linked:
                    r['target'] = target
                out.extend(linked); i = end; continue
            require(not re.match(r'\[[^\]]+\]\[', text[i:]), 'dialect: reference link is unsupported')
        if c == '*' and i and text[i - 1].isalnum():
            while i < len(text) and text[i] == '*':
                buffer.append('*'); i += 1
            continue
        if c == '*' and (i + 1 == len(text) or text[i + 1].isspace()):
            buffer.append(c); i += 1; continue
        if c == '*' or (c == '_' and (i == 0 or not text[i - 1].isalnum())):
            mark = '**' if text.startswith('**', i) else c
            end = text.find(mark, i + len(mark))
            if end > i + len(mark) and not text[i + len(mark)].isspace():
                flush()
                out.extend(inline(text[i + len(mark):end], styles + ('b' if mark == '**' else 'i',)))
                i = end + len(mark); continue
            require(c != '*' or (i and text[i - 1].isalnum()), 'dialect: unbalanced emphasis')
        require(not (c == '!' and text[i:i + 2] == '!['), 'dialect: images are unsupported')
        require(not text.startswith('~~', i), 'dialect: strikethrough is unsupported')
        buffer.append(c); i += 1
    flush()
    return out


def cells(line):
    """GFM pipe boundaries; an escaped pipe is literal, including within a code span."""
    line = line.strip()
    require(line.startswith('|'), 'dialect: table must start with a pipe')
    if line.endswith('|') and not line.endswith('\\|'):
        line = line[:-1]
    out, buf, escaped = [], [], False
    for c in line[1:]:
        if c == '|' and not escaped:
            out.append(''.join(buf).strip()); buf = []
        else:
            buf.append(c)
        escaped = c == '\\' and not escaped
    out.append(''.join(buf).strip())
    return out


def parse(markdown, source):
    lines = markdown.replace('\r\n', '\n').split('\n')
    blocks, i, anchors = [], 0, collections.Counter()
    def unsupported(line):
        return bool(re.match(r'^(?:\s*(?:\d+[.)] |[*+] |[-*+] \[[ xX]\])|\s+[-*+] |>|~~~|\$\$|\[.+\]:| {4}\S)', line))
    def start(line):
        return bool(re.match(r'^(?:#{1,6} |[-*+] |\||```|<!--|---\s*$)', line))
    while i < len(lines):
        line = lines[i]
        if not line.strip():
            i += 1; continue
        if line.startswith('<!--'):
            while '-->' not in lines[i]:
                i += 1
                require(i < len(lines), 'dialect: unclosed comment')
            require(not lines[i].split('-->', 1)[1].strip(), 'dialect: text after block comment')
            i += 1; continue
        if line.startswith('```'):
            require(re.fullmatch(r'```(?:[A-Za-z0-9_-]+)?', line), 'dialect: unsupported fence')
            language = line[3:]; body = []; i += 1
            while i < len(lines) and lines[i] != '```':
                body.append(lines[i]); i += 1
            require(i < len(lines), 'dialect: unclosed fence')
            blocks.append({'kind': 'diagram' if language == 'mermaid' else 'code', 'language': language,
                           'text': '\n'.join(body), 'source': source})
            i += 1; continue
        require(not unsupported(line), 'dialect: unsupported block: ' + line[:70])
        require(not re.match(r'^<(?:/?(?:div|p|img|table|script|svg|details|br)\b)', line, re.I),
                'dialect: raw HTML block is unsupported')
        if re.fullmatch(r'---\s*', line):
            blocks.append({'kind': 'rule', 'source': source}); i += 1; continue
        m = re.match(r'^(#{1,6}) (.+)$', line)
        if m:
            require(len(m[1]) <= 4, 'dialect: heading depth exceeds four')
            anchor = slug(m[2]); ordinal = anchors[anchor]; anchors[anchor] += 1
            blocks.append({'kind': 'heading', 'level': len(m[1]), 'runs': inline(m[2]),
                           'anchor': anchor + (f'-{ordinal}' if ordinal else ''), 'source': source})
            i += 1; continue
        if line.startswith('|'):
            header = cells(line); i += 1
            require(i < len(lines), 'dialect: missing table separator')
            separator = cells(lines[i]); i += 1
            require(len(separator) == len(header) and all(re.fullmatch(r':?-{3,}:?', s) for s in separator),
                    'dialect: invalid table separator')
            rows = []
            while i < len(lines) and lines[i].startswith('|'):
                row = cells(lines[i]); require(len(row) <= len(header), 'dialect: excess table cells')
                # GFM defines missing trailing cells as empty (three existing SRS rows use this).
                row += [''] * (len(header) - len(row))
                rows.append([inline(c) for c in row]); i += 1
            blocks.append({'kind': 'table', 'headers': [inline(c) for c in header], 'rows': rows,
                           'align': ['right' if s.endswith(':') else 'left' for s in separator], 'source': source})
            continue
        if line.startswith('- '):
            items = []
            while i < len(lines) and lines[i].strip():
                if lines[i].startswith('- '):
                    items.append(lines[i][2:])
                elif lines[i].startswith('  ') and not unsupported(lines[i]):
                    require(items, 'dialect: list continuation without item'); items[-1] += ' ' + lines[i].strip()
                else:
                    break
                i += 1
            blocks.append({'kind': 'list', 'items': [inline(s) for s in items], 'source': source}); continue
        paragraph = [line]; i += 1
        while i < len(lines) and lines[i].strip() and not start(lines[i]):
            require(not unsupported(lines[i]), 'dialect: unsupported continuation: ' + lines[i][:70])
            paragraph.append(lines[i]); i += 1
        text = ' '.join(p.strip() for p in paragraph)
        require(not re.search(r'\n[=-]{3,}$', '\n'.join(paragraph)), 'dialect: setext headings unsupported')
        blocks.append({'kind': 'para', 'runs': inline(text), 'source': source})
    return blocks


def runs_in(block):
    if block['kind'] in ('heading', 'para'):
        yield block['runs']
    elif block['kind'] == 'list':
        yield from block['items']
    elif block['kind'] == 'table':
        yield from block['headers']
        for row in block['rows']:
            yield from row


class LinkMap:
    def __init__(self, documents):
        self.targets, self.files = {}, {}
        for key, blocks in documents.items():
            for b in blocks:
                source = b['source']
                if b['kind'] == 'heading':
                    bookmark = 'h' + sha((source + '#' + b['anchor']).encode())[:20]
                    require((source, b['anchor']) not in self.targets, 'links: duplicate source heading')
                    b['bookmark'] = bookmark
                    target = (key, bookmark, plain(b['runs']))
                    self.targets[(source, b['anchor'])] = target
                    self.files.setdefault(source, target)

    def resolve(self, source, target):
        url = urlsplit(target)
        if url.scheme or url.netloc:
            return None
        path = (PurePosixPath(source).parent / unquote(url.path)).as_posix() if url.path else source
        # PurePosixPath intentionally does not normalize '..'.
        parts = []
        for part in path.split('/'):
            if part == '..':
                require(parts, 'links: path escape'); parts.pop()
            elif part not in ('', '.'):
                parts.append(part)
        path = '/'.join(parts)
        if url.fragment:
            if path in self.files:
                require((path, unquote(url.fragment)) in self.targets, 'links: unresolved anchor ' + target)
            return self.targets.get((path, unquote(url.fragment)))
        found = self.files.get(path)
        return (found[0], found[1], None) if found else None

    def rewrite(self, documents):
        for key, blocks in documents.items():
            for b in blocks:
                for runs in runs_in(b):
                    rebuilt, i = [], 0
                    while i < len(runs):
                        run = runs[i]
                        if 'target' not in run:
                            rebuilt.append(run); i += 1; continue
                        target, group = run['target'], []
                        while i < len(runs) and runs[i].get('target') == target:
                            r = dict(runs[i]); r.pop('target'); group.append(r); i += 1
                        resolved = self.resolve(b['source'], target)
                        if resolved and resolved[0] == key:
                            for r in group:
                                r['link'] = resolved[1]
                        rebuilt.extend(group)
                        if resolved and resolved[0] != key:
                            rebuilt.append({'text': ' (GCAM-SW-' + resolved[0] +
                                            (', ' + resolved[2] if resolved[2] else '') + ')', 'styles': []})
                    runs[:] = rebuilt


def testing_module():
    sys.path.insert(0, str(ROOT / 'samples/testing'))
    import test_records
    return test_records


def catalog_sections(text):
    entries = []
    for section in re.split(r'(?m)(?=^## )', text)[1:]:
        m = re.search(r'```json\n(.*?)\n```', section, re.S)
        require(m is not None, 'assembly: catalog section has no metadata')
        meta = json.loads(m[1])
        narrative = section[:m.start()]
        # Keep purpose, oracle, tolerance and gates; source paths are not hyperlinks in the set.
        narrative = re.split(r'(?m)^Source:', narrative)[0].rstrip()
        require((('Oracle:' in narrative and 'Tolerance:' in narrative) or 'Oracle / tolerance:' in narrative) and 'Gates:' in narrative,
                'assembly: incomplete catalog narrative')
        entries.append((meta, narrative))
    return entries


def owner(case):
    return 'A' if case['id'].startswith('Gcam.Studio.') else 'B'


def case_unit(case, units):
    candidates = [u for u in units if case['id'].startswith(u + '.')]
    require(len(candidates) == 1, 'assembly: unresolved selected case ' + case['id'])
    return candidates[0]


def newest_milestone(records=None):
    """The milestone docs/VV.Tests.Results.md publishes: the highest-numbered archived folder (M10 after M9)."""
    records = records or ROOT / 'samples/testing/records'
    found = [int(p.name[1:]) for p in records.iterdir() if p.is_dir() and re.fullmatch(r'M[1-9][0-9]*', p.name)]
    require(found, 'assembly: no archived milestone')
    return 'M' + str(max(found))


def published_report():
    return (ROOT / 'docs/VV.Tests.Results.md').read_bytes()


def require_published_report(milestone, archive_report):
    """Only the published milestone is compared with docs; an older milestone's archived report is already replayed byte
    for byte by test_records.py check --archives, and docs has moved on to a newer one."""
    if milestone == newest_milestone():
        require(published_report() == archive_report,
                'assembly: results report no longer equals the archived ' + milestone + ' report')


def assemble(milestone="M3"):
    """Return documents, provenance, and all repository-relative semantic input paths."""
    tr = testing_module()
    require(re.fullmatch(r"M[1-9][0-9]*", milestone or ""), "assembly: invalid milestone")
    archive = ROOT / "samples/testing/records" / milestone
    selection, records, cases = tr.load_selection(archive)
    subjects = {r["source"]["commit"] for r in records}
    require(len(subjects) == 1, "assembly: mixed milestone subjects")
    subject = next(iter(subjects))
    if milestone == 'M3':
        require(subject == M3_SUBJECT, 'assembly: wrong M3 subject')
    require(all(r['provenance'] == 'formal' for r in records), 'assembly: ' + milestone + ' provenance changed')
    require_published_report(milestone, (archive / 'report.md').read_bytes())
    archive_report = (archive / 'report.md').read_text(encoding='utf-8')
    catalog = (archive / 'catalog.md').read_text(encoding='utf-8')
    entries = catalog_sections(catalog); units = {e['id']: e for e, _ in entries}
    require(len(units) == len(entries), 'assembly: duplicate unit')
    counts = collections.Counter(c['verdict'] for c in cases)
    if milestone == 'M3':
        require(dict(counts) == {'passed': 1534, 'failed': 1, 'not_executed': 53}, 'assembly: M3 totals changed')
    per_unit = {u: collections.Counter() for u in units}
    for case in cases:
        per_unit[case_unit(case, units)][case['verdict']] += 1
    require(all(sum(c.values()) for c in per_unit.values()), 'assembly: unit without selected results')
    documents, inputs, redactions = {}, set(), []
    def source(name):
        path = 'docs/' + name; inputs.add(path)
        text = (ROOT / path).read_text(encoding='utf-8')
        # The source history includes machine worktree names. R9 forbids them in public deliverables;
        # retain source hashes and counts, but never copy those names into the model or record.
        text, count = re.subn(r'(?<![\w])[A-Za-z]:[\\/][A-Za-z0-9._/\\-]+', '[local path omitted]', text)
        if count:
            redactions.append({'source': path, 'count': count, 'kind': 'local filesystem path'})
        return parse(text, path)
    documents['SRS'] = source('VV.Studio.SRS.md')
    documents['SDS'] = source('VV.Studio.SDS.md')
    vvr = source('VV.Studio.md')
    for letter, name in zip('ABCD', ('Acquisition', 'Imaging', 'Detector', 'Waveform')):
        annex = source('VV.Studio.' + name + '.md')
        annex[0]['runs'] = inline('Annex ' + letter + ' — ' + plain(annex[0]['runs']))
        vvr.extend(annex)
    documents['VVR'] = vvr
    str_source = 'docs/VV.Tests.Results.md'
    inputs.add(str_source)
    blocks = parse(f'# Software test report\n\nSelected milestone {milestone}; historical automated execution only. '
                   'Formal is collection provenance, not approval. Inspection, compiler and manual judgments remain separate.\n',
                   str_source)
    # The archived report has a single heading; map its original title to this report's title too.
    blocks[0]['anchor'] = slug('VV.Tests.Results — generated automated test results')
    for part, title in [('A', 'GCAM Studio'), ('B', 'Simulation engine and supporting verification tools')]:
        selected = [c for c in cases if owner(c) == part]
        totals = collections.Counter(c['verdict'] for c in selected)
        intro = f'## Part {part} — {title}\n\nSelected {milestone} cases: ' + ', '.join(
            f'{k}={totals[k]}' for k in ('passed', 'failed', 'not_executed')) + '.\n\n'
        blocks.extend(parse(intro, str_source))
        part_units = [u for u in units if ('A' if u.startswith('Gcam.Studio.') else 'B') == part]
        for family in sorted({u.split('::')[0] for u in part_units}):
            blocks.extend(parse('### Results — ' + family + '\n', str_source))
            family_units = sorted(u for u in part_units if u.split('::')[0] == family)
            rows = []
            for u in family_units:
                ct = per_unit[u]
                name = u.split('::', 1)[1].removeprefix(family + '.')
                rows.append([name, str(ct['passed']), str(ct['failed']), str(ct['not_executed'])])
            blocks.append({'kind': 'table', 'headers': [inline(s) for s in ('Class / configuration', 'Passed', 'Failed', 'Not executed')],
                           'rows': [[inline(c) for c in row] for row in rows], 'align': ['left', 'right', 'right', 'right'],
                           'source': str_source, 'layout': 'results', 'family': family, 'unitIds': family_units})
        blocks.extend(parse('### Failed and not-executed cases — Part ' + part + '\n', str_source))
        for c in sorted(selected, key=lambda x: x['id']):
            if c['verdict'] == 'passed':
                continue
            blocks.extend(parse('`' + c['id'] + '`\n', str_source))
            detail = c.get('failureMessage') or c.get('message') or c.get('skipReason') or ''
            # Keep every normalized detail field, without reproducing local machine paths.
            for field in ('verdict', 'failureKind', 'failureMessage', 'failureStackTrace', 'skipReasonCode', 'skipReason', 'reason',
                          'reasonText', 'switch', 'message'):
                if c.get(field):
                    blocks.append({'kind': 'para', 'runs': [{'text': field + ': ' + str(c[field]), 'styles': []}],
                                   'source': str_source})
            if not detail:
                blocks.append({'kind': 'para', 'runs': inline(c['verdict']), 'source': str_source})
        if part == 'A' and milestone == 'M3':
            blocks.extend(parse('The selected Compare timeout remains failed despite 20 passing repeats; '
                                'see [AN-18](VV.Studio.md#anomalies-and-gaps). Repeats are not substituted.\n', str_source))
        blocks.extend(parse('### Catalog appendix — Part ' + part + '\n', 'docs/VV.Tests.md'))
        for e, narrative in entries:
            if e['id'] not in part_units:
                continue
            section = parse(narrative, 'docs/VV.Tests.md')
            section[0]['level'] = 3
            section.insert(1, {'kind': 'para', 'runs': inline('Unit: `' + e['id'] + '`; selected cases: ' +
                                                          str(sum(per_unit[e['id']].values())) + '.'),
                               'source': 'docs/VV.Tests.md'})
            blocks.extend(section)
    from ui_annex import annex
    evidence_blocks, evidence_inputs = annex(archive, selection, records, cases)
    # Place the evidence within Part A, before Part B.
    position = next(i for i, b in enumerate(blocks) if b['kind'] == 'heading' and plain(b['runs']).startswith('Part B'))
    blocks[position:position] = evidence_blocks
    inputs.update(evidence_inputs)
    # Keep report diagnostics/gaps and run/provenance details verbatim, rather than inventing a summary.
    for label, first, last in [('Selected invocation provenance', '| Run |', '| Catalog unit |'),
                               ('Run diagnostics and known profile gaps', 'Run diagnostics:', None)]:
        require(first in archive_report, 'assembly: report layout changed')
        text = archive_report.split(first, 1)[1]
        text = first + (text.split(last, 1)[0] if last else text)
        blocks.extend(parse('## ' + label + '\n\n' + text, str_source))
    history = source('VV.Studio.History.md')
    history[0]['runs'] = inline(f'History annex — dated verification records (not added to {milestone} totals)')
    blocks.extend(history)
    documents['STR'] = blocks
    links = LinkMap(documents); links.rewrite(documents)
    # Model block IDs are stable in one assembly, used independently by HTML and OOXML coverage checks.
    for key, doc in documents.items():
        for i, block in enumerate(doc):
            block['id'] = key.lower() + f'{i:04d}'
            if block['kind'] == 'diagram':
                block['asset'] = 'diagram-' + sha(block['text'].encode())[:16]
    inputs.update(p.relative_to(ROOT).as_posix() for p in archive.rglob('*') if p.is_file())
    manual = sorted((ROOT / 'docs/manual/editions').glob('*/generation-record.json'))[-1].parent
    record = json.loads((manual / 'generation-record.json').read_text(encoding='utf-8'))
    manual_paths = [manual / n for n in ('generation-record.json', 'MANUAL.Studio.md', 'MANUAL.Studio.pdf', 'MANUAL.Studio.docx')]
    inputs.update(p.relative_to(ROOT).as_posix() for p in manual_paths)
    provenance = {'testSubject': subject, 'milestone': milestone, 'totals': dict(counts), 'privacyRedactions': redactions,
                  'unitCount': len(units), 'partUnits': {'A': sum(u.startswith('Gcam.Studio.') for u in units),
                                                       'B': sum(not u.startswith('Gcam.Studio.') for u in units)},
                  'partTotals': {part: dict(collections.Counter(c['verdict'] for c in cases if owner(c) == part))
                                 for part in ('A', 'B')},
                  'manual': {'edition': record['editionId'], 'date': record['date'],
                             'deploymentSha256': record['subject']['deploymentSha256'],
                             'pdfPages': record['formats']['pdf']['pages'],
                             'paths': {p.suffix[1:]: p.relative_to(ROOT).as_posix() for p in manual_paths[1:]},
                             'hashes': {p.name: sha(p.read_bytes()) for p in manual_paths}}}
    if milestone == 'M3':
        require(provenance['partUnits'] == {'A': 101, 'B': 138}, 'assembly: unit inventory changed')
    return documents, provenance, sorted(inputs)
