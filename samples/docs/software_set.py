"""Generate/check a demonstration software document set, using only Python's standard library and local Edge.

  python -B samples/docs/software_set.py generate --edition-id YYYY-MM-DD-n --out <edition>
  python -B samples/docs/software_set.py check --edition <edition>
  python -B samples/docs/software_set.py review --edition <edition> --out <scratch-review>
"""
import argparse
import datetime as dt
import json
from pathlib import Path
import re
import subprocess
import sys

from software_model import ROOT, BANNER, TITLES, assemble, canonical, require, sha, plain
from software_formats import render_html, render_docx, check_html, check_docx, esc
from edge_output import Edge, EdgePdf, privacy

HERE = Path(__file__).resolve().parent


def git(*args):
    result = subprocess.run(['git', *args], cwd=ROOT, capture_output=True, check=True)
    return result.stdout.decode('utf-8').strip()


def history(documents, milestone="M3"):
    cache, result = {}, {}
    for key, blocks in documents.items():
        rows = []
        for path in sorted({b['source'] for b in blocks}):
            # Catalog narrative is the archived M3 input, not today's growing catalog.
            path = f'samples/testing/records/{milestone}/catalog.md' if path == 'docs/VV.Tests.md' else path
            if path not in cache:
                logs = git('log', '--format=%h %cs', '--', path).splitlines()
                require(logs, 'history: source has no commits: ' + path)
                cache[path] = [path, logs[-1], logs[0]]
            rows.append(cache[path])
        result[key] = rows
    return result


def semantic_inputs(paths):
    paths = set(paths)
    paths.update(p.relative_to(ROOT).as_posix() for p in HERE.rglob('*') if p.is_file() and '__pycache__' not in p.parts)
    paths.update(('samples/testing/test_records.py', 'samples/testing/adapters.py'))
    return {p: sha((ROOT / p).read_bytes()) for p in sorted(paths)}


def write(path, data):
    path = Path(path); path.parent.mkdir(parents=True, exist_ok=True)
    if isinstance(data, str):
        data = data.encode('utf-8')
    path.write_bytes(data)


def index(meta, counts, out):
    import os
    milestone = meta.get("milestone", "M3")
    totals = meta["totals"]
    subject_line = (milestone + " selected test subject: `" + meta["testSubject"] + "` (" +
                    f"{totals.get('passed', 0):,} passed, {totals.get('failed', 0):,} failed, {totals.get('not_executed', 0):,} not executed).")
    lines = ['# GCAM software demonstration set', '', ('**DRAFT**' if meta['draft'] else 'Final edition'), '', BANNER, '',
             'Edition: `' + meta['edition'] + '`. Source tree: ' + meta['treeStatus'] + '.', '',
             'Documentation source commit: `' + meta['sourceCommit'] + '`. Exact inputs are hashed in `generation-record.json`.',
             subject_line,
             'Manual deployment fingerprint: `' + meta['manual']['deploymentSha256'] + '`.', '',
             'Approval: not applicable (demonstration). PDF page counts below do not imply Word pagination.', '',
             '| ID | Title | Revision | Date | PDF pages | Word / PDF |', '|---|---|---|---|---:|---|']
    for key, title in TITLES.items():
        stem = 'GCAM-SW-' + key
        lines.append(f'| {stem} | {title} | {meta["edition"]} | {meta["date"]} | {counts[key]} | '
                     f'[Word]({stem}.docx) / [PDF]({stem}.pdf) |')
    manual = meta['manual']
    # Links remain portable if the folder is later generated at its intended repository destination.
    relative = lambda name: Path(os.path.relpath(ROOT / manual['paths'][name], out)).as_posix()
    lines += [f'| GCAM-SW-UM | User manual (unchanged companion) | {manual["edition"]} | {manual["date"]} | '
              f'{manual["pdfPages"]} | [Word]({relative("docx")}) / [PDF]({relative("pdf")}) |', '',
              'The manual keeps its original edition, cover and page furniture; the demonstration designation applies '
              'through this index. No cover was added and no manual was re-rendered.', '',
              'The set preserves the existing documentation baseline, including historical failures and pending checks. '
              'The STR History annex is dated evidence and is never added to selected ' + milestone + ' totals.', '',
              'Privacy: 11 local filesystem paths in the History source are replaced with “[local path omitted]”. '
              'The source files are unchanged; original source hashes and redaction counts are retained.', '',
              '## Unchanged companion hashes', '']
    for path, digest in manual['hashes'].items():
        lines.append('- `' + path + '`: `' + digest + '`')
    result = '\n'.join(lines) + '\n'; privacy(result)
    return result


def generation_identity(edition_id, draft, commit, dirty_files):
    require(re.fullmatch(r'\d{4}-\d{2}-\d{2}-[1-9]\d*', edition_id or ''), 'generate: edition ID must be YYYY-MM-DD-n')
    dt.date.fromisoformat(edition_id.rsplit('-', 1)[0])
    require(not dirty_files or draft, 'generate: uncommitted changes present; --draft is required')
    return {'edition': edition_id, 'draft': bool(draft), 'sourceCommit': commit,
            'treeStatus': 'uncommitted changes present' if dirty_files else 'clean at ' + commit}


def edition_files():
    return {f'GCAM-SW-{key}.{extension}' for key in TITLES for extension in ('html', 'docx', 'pdf')} | {'INDEX.md'}


def generate(out, edition_id, draft=False, date=None, milestone="M3"):
    # Capture the source state before creating any output, including an edition inside the repository.
    dirty_files = git('status', '--porcelain', '--untracked-files=all').splitlines()
    identity = generation_identity(edition_id, draft, git('rev-parse', 'HEAD'), dirty_files)
    out = out.resolve(); record_path = out / 'generation-record.json'
    require(not out.exists() or not any(out.iterdir()) or (record_path.is_file() and
            json.loads(record_path.read_text(encoding='utf-8')).get('schema') == 'gcam.software-set' and
            {p.relative_to(out).as_posix() for p in out.rglob('*') if p.is_file()} <= edition_files() | {'generation-record.json'}),
            'generate: output must be empty or an owned software-set edition')
    out.mkdir(parents=True, exist_ok=True)
    write(record_path, canonical({'schema': 'gcam.software-set', 'schemaVersion': 2, 'state': 'incomplete', 'metadata': identity}))
    documents, provenance, paths = assemble(milestone)
    meta = dict(provenance, **identity, date=date or edition_id.rsplit('-', 1)[0], history=history(documents, milestone))
    require(re.fullmatch(r'\d{4}-\d{2}-\d{2}', meta['date']), 'generate: invalid date')
    inputs = semantic_inputs(paths)
    browser = Edge()
    version = browser.version()
    diagram_texts = {b['asset']: b['text'] for doc in documents.values() for b in doc if b['kind'] == 'diagram'}
    diagrams = browser.diagrams(diagram_texts, HERE / 'vendor/mermaid-11.4.1.min.js')
    counts = {}
    for key, doc in documents.items():
        stem = 'GCAM-SW-' + key
        page = render_html(key, doc, meta, diagrams)
        check_html(page, doc)
        word = render_docx(key, doc, meta, diagrams)
        check_docx(word, doc)
        write(out / (stem + '.html'), page)
        write(out / (stem + '.docx'), word)
        browser.print_pdf(out / (stem + '.html'), out / (stem + '.pdf'))
        pdf = EdgePdf((out / (stem + '.pdf')).read_bytes())
        counts[key] = pdf.check(BANNER, stem)
        pdf.check_evidence(doc)
        print(stem + ': ' + str(counts[key]) + ' PDF pages; HTML/DOCX coverage passed', flush=True)
    write(out / 'INDEX.md', index(meta, counts, out))
    outputs = {p.relative_to(out).as_posix(): sha(p.read_bytes()) for p in sorted(out.rglob('*'))
               if p.is_file() and p.name != 'generation-record.json'}
    record = {'schema': 'gcam.software-set', 'schemaVersion': 2, 'state': 'complete', 'editionId': edition_id,
              'metadata': meta, 'inputs': inputs,
              'outputs': outputs, 'pdfPages': counts, 'edgeVersion': version,
              'printSettings': {'paper': 'A4', 'marginMm': 20, 'defaultHeaders': False, 'headless': 'new'},
              'pdfReader': 'traditional-xref / direct Flate / ToUnicode, Edge output only',
              'verification': 'block/cell/code coverage; structural PDF and cover text; selected ' + milestone + ' outcomes',
              'dirtyFiles': dirty_files}
    privacy(canonical(record).decode())
    write(out / 'generation-record.json', canonical(record))
    check(out)
    return record


def check(edition):
    edition = edition.resolve()
    record = json.loads((edition / 'generation-record.json').read_text(encoding='utf-8'))
    require(record.get('schema') == 'gcam.software-set' and record.get('schemaVersion') == 2 and
            record.get('state') == 'complete', 'check: incomplete/unsupported record')
    meta = record['metadata']
    require(record['editionId'] == meta['edition'], 'check: edition ID differs')
    require(all(meta[k] == v for k, v in generation_identity(meta['edition'], meta['draft'], meta['sourceCommit'],
                                                          record['dirtyFiles']).items()), 'check: source-state identity differs')
    for path, digest in record['inputs'].items():
        target = (ROOT / path).resolve()
        require(target.is_relative_to(ROOT) and target.is_file() and sha(target.read_bytes()) == digest,
                'stale edition: input differs: ' + path)
    actual = {p.relative_to(edition).as_posix() for p in edition.rglob('*') if p.is_file()} - {'generation-record.json'}
    require(actual == set(record['outputs']) == edition_files(), 'check: output membership differs')
    for path, digest in record['outputs'].items():
        target = (edition / path).resolve()
        require(target.is_relative_to(edition) and sha(target.read_bytes()) == digest, 'check: output hash differs: ' + path)
    documents, provenance, paths = assemble(meta.get("milestone", "M3"))
    require(record['inputs'] == semantic_inputs(paths), 'stale edition: input membership differs')
    for key, value in provenance.items():
        require(record['metadata'][key] == value, 'check: provenance differs: ' + key)
    privacy(canonical(record).decode())
    privacy((edition / 'INDEX.md').read_text(encoding='utf-8'))
    for key, doc in documents.items():
        stem = 'GCAM-SW-' + key
        check_html((edition / (stem + '.html')).read_text(encoding='utf-8'), doc)
        check_docx((edition / (stem + '.docx')).read_bytes(), doc)
        pdf = EdgePdf((edition / (stem + '.pdf')).read_bytes())
        count = pdf.check(BANNER, stem)
        pdf.check_evidence(doc)
        require(count == record['pdfPages'][key], 'check: PDF count differs')
        verify_furniture(edition, stem, meta)
    print('software set: 4 documents checked; ' + meta.get('milestone', 'M3') + '; ' + str(provenance['totals']) + '; ' + str(provenance['unitCount']) + ' catalog units', flush=True)
    return record


def verify_furniture(edition, stem, meta):
    import io
    import zipfile
    import xml.etree.ElementTree as ET
    texts = EdgePdf((edition / (stem + '.pdf')).read_bytes()).texts()
    compact = lambda s: re.sub(r'\s+', '', s)
    require(all(meta['edition'] in text for text in texts), 'check: PDF running revision missing')
    require(compact(meta['treeStatus']) in compact(texts[0]), 'check: PDF source-state missing')
    require(('DRAFT' in texts[0]) == meta['draft'], 'check: PDF draft cover differs')
    require(all(('DRAFT' in text) == meta['draft'] for text in texts), 'check: PDF draft header differs')
    with zipfile.ZipFile(io.BytesIO((edition / (stem + '.docx')).read_bytes())) as z:
        body = ''.join(ET.fromstring(z.read('word/document.xml')).itertext())
        header = ''.join(ET.fromstring(z.read('word/header1.xml')).itertext())
        require(meta['edition'] in body and meta['edition'] in header and meta['treeStatus'] in body,
                'check: Word revision/source-state missing')
        require(('DRAFT' in header) == meta['draft'] and ('DRAFT' in body) == meta['draft'], 'check: Word draft differs')


def review(edition, out):
    """HTML feature captures, explicitly not PDF rasterisations or proofs of print pagination."""
    edition, out = edition.resolve(), out.resolve(); out.mkdir(parents=True, exist_ok=True)
    require(out.is_relative_to(Path(__import__('tempfile').gettempdir()).resolve()), 'review: use TEMP/gcam-* output')
    record = check(edition)
    docs, _, _ = assemble(record['metadata'].get('milestone', 'M3'))
    browser = Edge()
    items = []
    for key, blocks in docs.items():
        full = (edition / ('GCAM-SW-' + key + '.html')).read_text(encoding='utf-8')
        style = re.search(r'<style>(.*?)</style>', full, re.S)[1]
        source = {b['id']: re.search(r'<section class="block [^"]+" id="block-' + b['id'] + r'">(.*?)</section>', full, re.S)[1]
                  for b in blocks}
        captures = [('cover', 'Cover and three provenance identities', re.search(r'<section class="cover">(.*?)</section>', full, re.S)[1])]
        tables = [b for b in blocks if b['kind'] == 'table']
        if tables:
            widest = max(tables, key=lambda b: len(b['headers']))
            longest = max(tables, key=lambda b: max((sum(len(plain(c)) for c in row) for row in b['rows']), default=0))
            captures += [('widest-table', 'Widest table; first rows', source[widest['id']]),
                         ('longest-row', 'Table containing the longest row; first rows', source[longest['id']])]
            # A focused row ensures the actual long row is visible, even when it occurs late in a trace table.
            row_index, row = max(enumerate(longest['rows']), key=lambda item: sum(len(plain(c)) for c in item[1]))
            original = source[longest['id']]
            rendered_rows = re.findall(r'<tr(?: [^>]*)?>.*?</tr>', original, re.S)
            focused = original.split('<tbody>', 1)[0] + '<tbody>' + rendered_rows[row_index + 1] + '</tbody></table>'
            captures.append(('longest-row-detail', 'Longest table row, isolated for clipping review', focused))
            if len(widest['rows']) > 8:
                original = source[widest['id']]
                rendered_rows = re.findall(r'<tr(?: [^>]*)?>.*?</tr>', original, re.S)
                fragment = original.split('<tbody>', 1)[0] + '<tbody>' + ''.join(rendered_rows[9:17]) + '</tbody></table>'
                captures.append(('table-continuation', 'Subsequent table rows with header; HTML specimen, not PDF continuation', fragment))
        for j, b in enumerate(b for b in blocks if b['kind'] == 'diagram'):
            captures.append(('diagram-' + str(j + 1), 'Rendered Mermaid diagram', source[b['id']]))
        codes = [b for b in blocks if b['kind'] == 'code']
        if codes:
            b = max(codes, key=lambda b: max(map(len, b['text'].splitlines()), default=0))
            captures.append(('code', 'Code wrapping, longest source line', source[b['id']]))
        glyphs = [b for b in blocks if any(ord(c) > 127 and c not in '—–·' for _, t, _ in __import__('software_formats').segments(b) for c in t)]
        if glyphs:
            b = max(glyphs, key=lambda b: len(set(c for _, t, _ in __import__('software_formats').segments(b) for c in t if ord(c) > 127)))
            captures.append(('glyphs', 'Greek / mathematical glyphs in source content', source[b['id']]))
            samples = []
            for char in 'Σσμπθλ√∈≠≤≥∞\u0302':
                match = next((t for block in blocks for _, t, _ in __import__('software_formats').segments(block) if char in t), None)
                if match:
                    pos = match.index(char)
                    samples.append('<tr><td>U+' + f'{ord(char):04X}' + '</td><td>' + esc(match[max(0, pos - 30):pos + 50]) + '</td></tr>')
            captures.append(('glyph-excerpts', 'Source excerpts: Greek, mathematical and combining glyphs',
                             '<table><thead><tr><th>Code point</th><th>Source excerpt</th></tr></thead><tbody>' + ''.join(samples) + '</tbody></table>'))
        pdf_texts = EdgePdf((edition / ('GCAM-SW-' + key + '.pdf')).read_bytes()).texts()
        for suffix, feature, fragment in captures:
            name = key + '-' + suffix
            page = out / (name + '.html')
            write(page, '<!doctype html><meta charset="utf-8"><title>GCAM feature review</title><style>' + style +
                  '\nbody{width:794px;max-width:none;margin:0;padding:76px;}html{background:white}.cover{min-height:0;padding-top:0}</style>' +
                  '<p style="color:#666;font-size:9pt">HTML feature specimen — ' + esc(feature) + '</p>' + fragment)
            browser.screenshot(page, out / (name + '.png'), 900, 1556)
            from html import unescape
            normalized = lambda text: re.sub(r'\s+', '', text)
            feature_text = normalized(unescape(re.sub(r'<[^>]+>', '', fragment)))
            needle = feature_text[:70]
            associated = [i + 1 for i, text in enumerate(pdf_texts) if needle and needle in normalized(text)]
            if suffix == 'cover':
                associated = [1]
            elif suffix == 'longest-row-detail':
                first = normalized(plain(row[0]))[:55]
                associated = [i + 1 for i, text in enumerate(pdf_texts) if first and first in normalized(text)]
            elif suffix == 'glyph-excerpts':
                associated = [i + 1 for i, text in enumerate(pdf_texts) if any(c in text for c in 'Σσμπθλ√∈≠≤≥∞\u0302')]
            items.append({'file': name + '.png', 'document': 'GCAM-SW-' + key, 'feature': feature,
                          'associatedPdfPages': associated,
                          'representation': 'HTML feature screenshot; not a PDF page'})
    write(out / 'review-manifest.json', canonical(items))
    write(out / 'INDEX.md', '# HTML feature review\n\nThese captures show named HTML features, not rasterised PDF pages. '
          'Use the edition PDFs to verify final print pagination and table continuation.\n\n' +
          '\n'.join('- [' + x['file'] + '](' + x['file'] + '): ' + x['feature'] for x in items) + '\n')
    print('review: ' + str(len(items)) + ' HTML feature PNGs; see review INDEX.md')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    subs = parser.add_subparsers(dest='action', required=True)
    gen = subs.add_parser('generate'); gen.add_argument('--out', type=Path, required=True)
    gen.add_argument('--milestone', default='M3'); gen.add_argument('--edition-id', required=True); gen.add_argument('--draft', action='store_true'); gen.add_argument('--date')
    chk = subs.add_parser('check'); chk.add_argument('--edition', type=Path, required=True)
    rev = subs.add_parser('review'); rev.add_argument('--edition', type=Path, required=True); rev.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    try:
        if args.action == 'generate':
            generate(args.out, args.edition_id, args.draft, args.date, args.milestone)
        elif args.action == 'check':
            check(args.edition)
        else:
            review(args.edition, args.out)
    except (ValueError, KeyError) as error:
        print('software set refused: ' + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
