"""HTML and hand-written OOXML from the same restricted document model."""
import base64
import html
from html.parser import HTMLParser
import io
from pathlib import Path
import re
import struct
import xml.etree.ElementTree as ET
import zipfile

from software_model import BANNER, TITLES, plain, require, sha
from edge_output import privacy

W = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
R = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
REL = 'http://schemas.openxmlformats.org/package/2006/relationships'
XML = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
NS = {'w': W, 'r': R}
HERE = Path(__file__).resolve().parent


def esc(value):
    return html.escape(str(value), quote=True)


def has_header(block):
    return any(plain(cell).strip() for cell in block['headers'])


def table_rows(block):
    if has_header(block):
        yield 0, block['headers']
    yield from enumerate(block['rows'], 1)


def segments(block):
    """Every text-bearing model segment, including each cell, has one independent coverage key."""
    kind, ident = block['kind'], block['id']
    if kind in ('heading', 'para'):
        yield ident, plain(block['runs']), block['runs']
    elif kind == 'list':
        for i, runs in enumerate(block['items']):
            yield ident + f'l{i}', plain(runs), runs
    elif kind == 'table':
        for i, row in table_rows(block):
            for j, runs in enumerate(row):
                yield ident + f'r{i}c{j}', plain(runs), runs
    elif kind == 'image':
        yield ident, block['caption'], inline_caption(block['caption'])
    elif kind == 'code':
        yield ident, block['text'], [{'text': block['text'], 'styles': ['code']}]


def inline_caption(text):
    return [{"text": text, "styles": []}]


def expected(doc):
    return {key: text for block in doc for key, text, _ in segments(block)}


def widths(block):
    if block.get('layout') == 'results':
        return [104 / 170, 22 / 170, 22 / 170, 22 / 170]
    rows = [block['headers']] + block['rows']
    sizes = []
    for j in range(len(block['headers'])):
        values = [plain(row[j]) for row in rows]
        mean = sum(len(v) for v in values) / len(values)
        numeric = all(re.fullmatch(r'[\d., /—-]*', v) for v in values[1:]) and bool(values[1:])
        sizes.append(8 if numeric else max(12, min(75, mean ** 0.65 * 5)))
    return [s / sum(sizes) for s in sizes]


def html_runs(runs):
    out = []
    for r in runs:
        s = esc(r['text'])
        for style, tag in [('code', 'code'), ('i', 'em'), ('b', 'strong')]:
            if style in r['styles']:
                s = f'<{tag}>' + s + f'</{tag}>'
        if r.get('link'):
            s = '<a href="#' + r['link'] + '">' + s + '</a>'
        out.append(s)
    return ''.join(out)


def control_rows(key, meta):
    return [('Document', 'GCAM-SW-' + key + ' · ' + TITLES[key]), ('Revision', meta['edition']),
            ('Date', meta['date']), ('Documentation source commit', meta['sourceCommit']),
            ('Source tree', meta['treeStatus']),
            (meta.get('milestone', 'M3') + ' test subject', meta['testSubject']),
            ('Manual companion deployment', meta['manual']['deploymentSha256']),
            ('Approvals', 'not applicable (demonstration)')]


def contents(doc):
    # Source H1 title maps to document Heading 1; source H2 to Heading 2. Both are in the static contents.
    return [b for b in doc if b['kind'] == 'heading' and b['level'] <= 2]


def render_html(key, doc, meta, diagrams):
    title = 'GCAM-SW-' + key + ' · ' + TITLES[key]
    head = ('<!doctype html><html lang="en"><head><meta charset="utf-8"><title>' + esc(title) + '</title>'
            '<meta http-equiv="Content-Security-Policy" content="default-src \'none\'; img-src data:; '
            'style-src \'unsafe-inline\'; connect-src \'none\'">'
            '<style>' + (HERE / 'software.css').read_text(encoding='utf-8') +
            '\n@page { @top-center { content: "' + esc(('DRAFT · ' if meta['draft'] else '') + title + ' · ' + meta['edition']) + '"; } }'
            '</style></head><body>')
    pieces = [head, '<section class="cover"><div class="eyebrow">GCAM software document set</div><h1>' + esc(title) +
              '</h1>' + ('<p class="draft">DRAFT</p>' if meta['draft'] else '') +
              '<p class="banner">' + esc(BANNER) + '</p><table class="control">']
    for label, value in control_rows(key, meta):
        pieces.append('<tr><th>' + esc(label) + '</th><td>' + esc(value) + '</td></tr>')
    pieces.append('</table></section><section class="front"><h2>Document control</h2><p>Source history endpoints '
                  '(first / last commit; no invented approval history).</p><table><thead><tr><th>Source</th>'
                  '<th>First commit / date</th><th>Last commit / date</th></tr></thead><tbody>')
    for row in meta['history'][key]:
        pieces.append('<tr>' + ''.join('<td>' + esc(c) + '</td>' for c in row) + '</tr>')
    pieces.append('</tbody></table><h2>Contents</h2><nav>')
    for b in contents(doc):
        pieces.append('<p class="toc level' + str(b['level']) + '"><a href="#' + b['bookmark'] + '">' +
                      esc(plain(b['runs'])) + '</a></p>')
    pieces.append('</nav></section><main>')
    for b in doc:
        ident, kind = b['id'], b['kind']
        pieces.append('<section class="block ' + kind + '" id="block-' + ident + '">')
        if kind == 'heading':
            level = min(b['level'], 4)
            pieces.append(f'<h{level} id="' + b['bookmark'] + '" data-text="' + ident + '">' + html_runs(b['runs']) + f'</h{level}>')
        elif kind == 'para':
            pieces.append('<p data-text="' + ident + '">' + html_runs(b['runs']) + '</p>')
        elif kind == 'list':
            pieces.append('<ul>')
            for i, runs in enumerate(b['items']):
                pieces.append('<li data-text="' + ident + f'l{i}">' + html_runs(runs) + '</li>')
            pieces.append('</ul>')
        elif kind == 'table':
            pieces.append('<table' + (' class="result-table"' if b.get('layout') == 'results' else '') +
                          '><colgroup>' + ''.join('<col style="width:' + f'{w * 100:.3f}' + '%">' for w in widths(b)) + '</colgroup>')
            if has_header(b):
                pieces.append('<thead>')
            else:
                pieces.append('<tbody>')
            for i, row in table_rows(b):
                pieces.append('<tr' + (' class="splittable"' if sum(len(plain(r)) for r in row) > 1500 else '') + '>')
                tag = 'th' if i == 0 else 'td'
                for j, runs in enumerate(row):
                    pieces.append('<' + tag + ' data-text="' + ident + f'r{i}c{j}" style="text-align:' + b['align'][j] + '">' +
                                  html_runs(runs) + '</' + tag + '>')
                pieces.append('</tr>')
                if i == 0:
                    pieces.append('</thead><tbody>')
            pieces.append('</tbody></table>')
        elif kind == 'image':
            pieces.append('<figure data-frame="' + b['asset'] + '" data-sha256="' + b['sha256'] +
                          '"><img alt="Studio UI evidence" src="data:image/jpeg;base64,' + b['jpeg'] +
                          '"><figcaption data-text="' + ident + '">' + esc(b['caption']) + '</figcaption></figure>')
        elif kind in ('code', 'diagram'):
            if kind == 'diagram':
                diagram = diagrams[b['asset']]
                data = base64.b64encode(diagram['svg'].encode()).decode()
                pieces.append('<figure data-diagram="' + b['asset'] + '" data-source-sha256="' + sha(b['text'].encode()) +
                              '"><img alt="Mermaid diagram" src="data:image/svg+xml;base64,' + data +
                              '"><figcaption>Diagram rendered from the source Mermaid block.</figcaption></figure>')
            else:
                pieces.append('<pre data-text="' + ident + '"><code>' + esc(b['text']) + '</code></pre>')
        elif kind == 'rule':
            pieces.append('<hr>')
        pieces.append('</section>')
    pieces.append('</main></body></html>')
    result = ''.join(pieces)
    privacy(result)
    return result


class CoverageHTML(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.depth, self.active, self.values, self.links = 0, [], {}, []

    def handle_starttag(self, tag, attributes):
        attrs = dict(attributes)
        if tag not in ('meta', 'img', 'hr', 'col', 'br', 'input', 'link'):
            self.depth += 1
        if 'data-text' in attrs:
            key = attrs['data-text']; require(key not in self.values, 'html: duplicate coverage key')
            self.values[key] = ''; self.active.append((key, self.depth))
        if 'href' in attrs:
            self.links.append(attrs['href'])

    def handle_endtag(self, tag):
        self.active = [(key, level) for key, level in self.active if level != self.depth]
        self.depth -= 1

    def handle_data(self, data):
        for key, _ in self.active:
            self.values[key] += data


def check_html(text, doc):
    parser = CoverageHTML(); parser.feed(text)
    require(parser.values == expected(doc) and list(parser.values) == list(expected(doc)), 'html: content coverage differs')
    bookmarks = {b['bookmark'] for b in doc if b['kind'] == 'heading'}
    require(all(t.startswith('#') and t[1:] in bookmarks for t in parser.links), 'html: outgoing/unresolved link')
    require(BANNER in html.unescape(text), 'html: missing banner')
    diagrams = {asset: digest for asset, digest in re.findall(r'data-diagram="([^"]+)" data-source-sha256="([^"]+)"', text)}
    require(diagrams == {b['asset']: sha(b['text'].encode()) for b in doc if b['kind'] == 'diagram'}, 'html: diagram coverage differs')
    for encoded in re.findall(r'src="data:image/svg\+xml;base64,([^"]+)"', text):
        svg = base64.b64decode(encoded, validate=True).decode('utf-8')
        ET.fromstring(svg); privacy(svg)
        require(not re.search(r'<script\b|(?:href|src)="(?:https?:|file:)', svg, re.I), 'html: SVG external content')
    require(len(re.findall(r'src="data:image/svg\+xml;base64,', text)) == len(diagrams), 'html: diagram image count differs')
    frames = re.findall(r'data-frame="([^"]+)" data-sha256="([^"]+)"[^>]*><img[^>]*src="data:image/jpeg;base64,([^"]+)"', text)
    expected_frames = {b['asset']: b for b in doc if b['kind'] == 'image'}
    require(len(re.findall(r'src="data:image/jpeg;base64,', text)) == len(frames), 'html: frame image count differs')
    require(all(src.startswith(('data:image/jpeg;base64,', 'data:image/svg+xml;base64,'))
                for src in re.findall(r'<img\b[^>]*src="([^"]+)"', text)), 'html: image is not self-contained')
    require(len(frames) == len(expected_frames) and {f[0] for f in frames} == set(expected_frames), 'html: frame coverage differs')
    for asset, digest, encoded in frames:
        require(digest == expected_frames[asset]['sha256'] == sha(base64.b64decode(encoded, validate=True)), 'html: JPEG hash differs')
    privacy(text)


def word_runs(runs):
    result = []
    for r in runs:
        props = ''.join({'b': '<w:b/>', 'i': '<w:i/>', 'code': '<w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/>'}[s]
                        for s in r['styles'])
        chunks = r['text'].split('\n')
        text = '<w:br/>'.join('<w:t xml:space="preserve">' + esc(s) + '</w:t>' for s in chunks)
        item = '<w:r><w:rPr>' + props + '</w:rPr>' + text + '</w:r>'
        if r.get('link'):
            item = '<w:hyperlink w:anchor="' + r['link'] + '">' + item + '</w:hyperlink>'
        result.append(item)
    return ''.join(result)


class Word:
    def __init__(self):
        self.serial, self.relationships, self.media = 0, [], {}

    def bookmark(self, name):
        self.serial += 1
        return '<w:bookmarkStart w:id="' + str(self.serial) + '" w:name="' + name + '"/>', '<w:bookmarkEnd w:id="' + str(self.serial) + '"/>'

    def p(self, runs, style='Normal', coverage=None, bookmark=None):
        before, after = '', ''
        for name in (bookmark, 't' + coverage if coverage else None):
            if name:
                a, z = self.bookmark(name); before += a; after = z + after
        return '<w:p><w:pPr><w:pStyle w:val="' + style + '"/></w:pPr>' + before + word_runs(runs) + after + '</w:p>'

    def text(self, text, style='Normal'):
        return self.p([{'text': text, 'styles': []}], style)

    def table(self, block):
        grid = [round(w * 9638) for w in widths(block)]
        parts = ['<w:tbl><w:tblPr><w:tblStyle w:val="Grid"/><w:tblW w:w="9638" w:type="dxa"/>'
                 '<w:tblLayout w:type="fixed"/></w:tblPr><w:tblGrid>' +
                 ''.join('<w:gridCol w:w="' + str(w) + '"/>' for w in grid) + '</w:tblGrid>']
        for i, row in table_rows(block):
            parts.append('<w:tr><w:trPr>' + ('<w:tblHeader/>' if i == 0 else '') +
                         ('<w:cantSplit/>' if sum(len(plain(r)) for r in row) < 1500 else '') + '</w:trPr>')
            for j, runs in enumerate(row):
                if i == 0:
                    runs = [dict(r, styles=list(set(r['styles']) | {'b'})) for r in runs]
                numeric = block.get('layout') == 'results' and j > 0
                parts.append('<w:tc><w:tcPr><w:tcW w:w="' + str(grid[j]) + '" w:type="dxa"/>' +
                             ('<w:noWrap/>' if numeric else '') +
                             ('<w:shd w:fill="EAF0F4"/>' if i == 0 else '') + '</w:tcPr>' +
                             self.p(runs, 'TableNumeric' if numeric else 'TableText', block['id'] + f'r{i}c{j}') + '</w:tc>')
            parts.append('</w:tr>')
        parts.append('</w:tbl>')
        return ''.join(parts)

    def image(self, asset, image, source):
        jpeg = 'jpeg' in image
        name = asset + ('.jpg' if jpeg else '.png'); rid = 'rImage' + str(len(self.media) + 1)
        pixels = base64.b64decode(image['jpeg']) if jpeg else image['png']
        self.media['word/media/' + name] = pixels
        description = 'JPEG SHA-256 ' + sha(pixels) if jpeg else 'Mermaid source SHA-256 ' + sha(source.encode())
        self.relationships.append((rid, 'image', 'media/' + name))
        width, height = (image['width'], image['height']) if jpeg else struct.unpack('>II', pixels[16:24])
        scale = min(6120130 / width, 4320000 / height)  # full 170 mm text width; 120 mm height cap
        cx, cy = round(width * scale), round(height * scale)
        return ('<w:p><w:pPr><w:pStyle w:val="Figure"/></w:pPr><w:r><w:drawing>'
                '<wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="' + str(cx) + '" cy="' + str(cy) +
                '"/><wp:docPr id="' + str(len(self.media)) + '" name="' + asset +
                '" descr="' + description + '"/>'
                '<a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">'
                '<pic:pic><pic:nvPicPr><pic:cNvPr id="0" name="' + name + '"/><pic:cNvPicPr/></pic:nvPicPr>'
                '<pic:blipFill><a:blip r:embed="' + rid + '"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>'
                '<pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="' + str(cx) + '" cy="' + str(cy) +
                '"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic>'
                '</a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>')


def render_docx(key, doc, meta, diagrams):
    w = Word(); body = []
    body += [w.text('GCAM software document set', 'Subtitle'), w.text('GCAM-SW-' + key + ' · ' + TITLES[key], 'Title'),
             w.text(BANNER, 'Banner')]
    if meta['draft']:
        body.insert(2, w.text('DRAFT', 'Banner'))
    for label, value in control_rows(key, meta):
        body.append(w.p([{'text': label + ': ', 'styles': ['b']}, {'text': value, 'styles': []}]))
    body += ['<w:p><w:r><w:br w:type="page"/></w:r></w:p>', w.text('Document control', 'Heading1'),
             w.text('Source history endpoints (first / last commit; no invented approval history).')]
    for source, first, last in meta['history'][key]:
        body.append(w.text(source + ' — first ' + first + '; last ' + last))
    body.append(w.text('Contents', 'Heading1'))
    for b in contents(doc):
        body.append(w.p([{'text': plain(b['runs']), 'styles': [], 'link': b['bookmark']}], 'Contents'))
    body.append('<w:p><w:r><w:br w:type="page"/></w:r></w:p>')
    for b in doc:
        kind = b['kind']
        if kind == 'heading':
            body.append(w.p(b['runs'], 'Heading' + str(min(b['level'], 4)), b['id'], b['bookmark']))
        elif kind == 'para':
            body.append(w.p(b['runs'], coverage=b['id']))
        elif kind == 'list':
            for i, runs in enumerate(b['items']):
                body.append(w.p(runs, 'Bullet', b['id'] + f'l{i}'))
        elif kind == 'table':
            body.append(w.table(b))
        elif kind == 'image':
            body += [w.image(b['asset'], b, ''), w.p(inline_caption(b['caption']), 'Caption', b['id'])]
        elif kind in ('code', 'diagram'):
            if kind == 'diagram':
                body += [w.image(b['asset'], diagrams[b['asset']], b['text']),
                         w.text('Diagram rendered from the source Mermaid block.', 'Caption')]
            else:
                body.append(w.p([{'text': b['text'], 'styles': ['code']}], 'Code', b['id']))
        elif kind == 'rule':
            body.append(w.text('—'))
    sect = ('<w:sectPr><w:headerReference w:type="default" r:id="rHeader"/>'
            '<w:footerReference w:type="default" r:id="rFooter"/><w:pgSz w:w="11906" w:h="16838"/>'
            '<w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134" '
            'w:header="567" w:footer="567"/></w:sectPr>')
    document = XML + '<w:document xmlns:w="' + W + '" xmlns:r="' + R + '" '
    document += ('xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" '
                 'xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" '
                 'xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><w:body>' + ''.join(body) + sect +
                 '</w:body></w:document>')
    header = XML + '<w:hdr xmlns:w="' + W + '">' + w.text(('DRAFT · ' if meta['draft'] else '') + 'GCAM-SW-' + key + ' · ' + TITLES[key] + ' · ' + meta['edition'], 'Furniture') + '</w:hdr>'
    footer = XML + '<w:ftr xmlns:w="' + W + '"><w:p><w:pPr><w:pStyle w:val="Furniture"/></w:pPr>'
    footer += word_runs([{'text': 'Page ', 'styles': []}]) + '<w:fldSimple w:instr="PAGE"><w:r><w:t>1</w:t></w:r></w:fldSimple>'
    footer += word_runs([{'text': ' of ', 'styles': []}]) + '<w:fldSimple w:instr="NUMPAGES"><w:r><w:t>1</w:t></w:r></w:fldSimple></w:p></w:ftr>'
    parts = {'word/document.xml': document, 'word/header1.xml': header, 'word/footer1.xml': footer,
             'word/styles.xml': styles_xml(),
             'word/settings.xml': XML + '<w:settings xmlns:w="' + W + '"><w:updateFields w:val="true"/></w:settings>',
             'word/numbering.xml': XML + '<w:numbering xmlns:w="' + W + '"><w:abstractNum w:abstractNumId="0">'
             '<w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val="•"/>'
             '<w:pPr><w:tabs><w:tab w:val="num" w:pos="360"/></w:tabs><w:ind w:left="360" w:hanging="180"/>'
             '</w:pPr></w:lvl></w:abstractNum><w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num></w:numbering>',
             'docProps/core.xml': XML + '<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" '
             'xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>' + esc('GCAM-SW-' + key + ' · ' + TITLES[key]) +
             '</dc:title><dc:creator>GCAM documentation generator</dc:creator><dc:language>en</dc:language></cp:coreProperties>'}
    w.relationships += [('rHeader', 'header', 'header1.xml'), ('rFooter', 'footer', 'footer1.xml'),
                        ('rStyles', 'styles', 'styles.xml'), ('rSettings', 'settings', 'settings.xml'), ('rNumbers', 'numbering', 'numbering.xml')]
    parts['word/_rels/document.xml.rels'] = relationships([(a, R + '/' + b, c) for a, b, c in w.relationships])
    parts['_rels/.rels'] = relationships([('rDocument', R + '/officeDocument', 'word/document.xml'),
                                         ('rCore', 'http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties', 'docProps/core.xml')])
    types = {'word/document.xml': 'application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml',
             'word/header1.xml': 'application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml',
             'word/footer1.xml': 'application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml',
             'word/styles.xml': 'application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml',
             'word/settings.xml': 'application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml',
             'word/numbering.xml': 'application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml',
             'docProps/core.xml': 'application/vnd.openxmlformats-package.core-properties+xml'}
    parts['[Content_Types].xml'] = XML + '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
    # Keep all parts explicitly typed; image compression is the PNG's own stream.
    parts['[Content_Types].xml'] += ('<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
                                   '<Default Extension="png" ContentType="image/png"/>' + ('<Default Extension="jpg" ContentType="image/jpeg"/>' if any(n.endswith('.jpg') for n in w.media) else '') + ''.join(
                                       '<Override PartName="/' + p + '" ContentType="' + t + '"/>' for p, t in types.items()) + '</Types>')
    output = io.BytesIO()
    with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for name, data in sorted({**{p: t.encode('utf-8') for p, t in parts.items()}, **w.media}.items()):
            info = zipfile.ZipInfo(name, (2026, 1, 1, 0, 0, 0)); info.compress_type = zipfile.ZIP_DEFLATED
            z.writestr(info, data)
    return output.getvalue()


def relationships(rows):
    return XML + '<Relationships xmlns="' + REL + '">' + ''.join(
        '<Relationship Id="' + esc(a) + '" Type="' + esc(b) + '" Target="' + esc(c) + '"/>' for a, b, c in rows) + '</Relationships>'


def styles_xml():
    styles = [XML + '<w:styles xmlns:w="' + W + '"><w:docDefaults><w:rPrDefault><w:rPr>'
              '<w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:sz w:val="20"/><w:lang w:val="en-US"/>'
              '</w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="100" w:line="280" w:lineRule="auto"/>'
              '</w:pPr></w:pPrDefault></w:docDefaults>']
    specs = {'Normal': ('', ''), 'Title': ('<w:spacing w:before="600" w:after="400"/>', '<w:b/><w:sz w:val="44"/>'),
             'Subtitle': ('', '<w:color w:val="365A70"/><w:sz w:val="24"/>'),
             'Banner': ('<w:spacing w:before="200" w:after="300"/>', '<w:b/><w:color w:val="365A70"/>'),
             'TableText': ('<w:spacing w:after="60"/>', '<w:sz w:val="18"/>'),
             'TableNumeric': ('<w:spacing w:after="60"/><w:jc w:val="right"/>', '<w:sz w:val="18"/>'),
             'Code': ('<w:shd w:fill="F2F4F6"/><w:spacing w:after="120"/>', '<w:sz w:val="16"/>'),
             'Bullet': ('<w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr>', ''),
             'Contents': ('<w:spacing w:after="70"/>', ''), 'Figure': ('<w:jc w:val="center"/><w:keepNext/>', ''),
             'Caption': ('', '<w:i/><w:sz w:val="18"/>'), 'Furniture': ('<w:jc w:val="center"/>', '<w:sz w:val="16"/>')}
    for i in range(1, 5):
        specs['Heading' + str(i)] = ('<w:keepNext/><w:outlineLvl w:val="' + str(i - 1) +
                                    '"/><w:spacing w:before="220" w:after="120"/>', '<w:b/><w:sz w:val="' + str(32 - i * 3) + '"/>')
    for name, (ppr, rpr) in specs.items():
        styles.append('<w:style w:type="paragraph" w:styleId="' + name + '"><w:name w:val="' + name +
                      '"/><w:pPr>' + ppr + '</w:pPr><w:rPr>' + rpr + '</w:rPr></w:style>')
    styles.append('<w:style w:type="table" w:styleId="Grid"><w:name w:val="Grid"/><w:tblPr><w:tblBorders>' +
                  ''.join('<w:' + side + ' w:val="single" w:sz="4" w:color="A0ACB4"/>' for side in
                          ('top', 'left', 'bottom', 'right', 'insideH', 'insideV')) +
                  '</w:tblBorders><w:tblCellMar><w:top w:w="80" w:type="dxa"/><w:left w:w="80" w:type="dxa"/>'
                  '<w:bottom w:w="80" w:type="dxa"/><w:right w:w="80" w:type="dxa"/></w:tblCellMar></w:tblPr></w:style></w:styles>')
    return ''.join(styles)


def check_docx(data, doc):
    import posixpath
    values, bookmarks, anchors, image_refs = {}, set(), [], set()
    with zipfile.ZipFile(io.BytesIO(data)) as z:
        names = set(z.namelist())
        require(len(names) == len(z.namelist()), 'docx: duplicate part')
        require({'[Content_Types].xml', 'word/document.xml', 'word/styles.xml', 'word/header1.xml', 'word/footer1.xml'} <= names,
                'docx: missing required part')
        parsed = {}
        for name in names:
            if name.endswith(('.xml', '.rels')):
                raw = z.read(name).decode('utf-8'); privacy(raw); parsed[name] = ET.fromstring(raw)
        types = parsed['[Content_Types].xml']
        defaults = {n.get('Extension') for n in types if n.tag.endswith('Default')}
        overrides = {n.get('PartName').lstrip('/') for n in types if n.tag.endswith('Override')}
        require(all(p in overrides or p.rsplit('.', 1)[-1] in defaults for p in names if p != '[Content_Types].xml'),
                'docx: untyped part')
        for name, root in parsed.items():
            if not name.endswith('.rels'):
                continue
            base = '' if name == '_rels/.rels' else name.split('/_rels/')[0] + '/'
            ids = set()
            for relation in root:
                require(relation.get('Id') not in ids, 'docx: duplicate relationship'); ids.add(relation.get('Id'))
                target = relation.get('Target', '')
                require(relation.get('TargetMode') != 'External' and not re.match(r'\w+:|/', target), 'docx: outgoing relationship')
                resolved = posixpath.normpath(base + target)
                require(resolved in names, 'docx: missing relationship target')
                if relation.get('Type', '').endswith('/image'):
                    image_refs.add(relation.get('Id'))
        root = parsed['word/document.xml']
        for p in root.findall('.//w:p', NS):
            text = ''.join(n.text or '' if n.tag == '{' + W + '}t' else '\n'
                           for n in p.iter() if n.tag in ('{' + W + '}t', '{' + W + '}br'))
            for mark in p.findall('w:bookmarkStart', NS):
                name = mark.get('{' + W + '}name')
                require(name not in bookmarks, 'docx: duplicate bookmark'); bookmarks.add(name)
                if name.startswith('t'):
                    values[name[1:]] = text
            anchors.extend(a.get('{' + W + '}anchor') for a in p.findall('.//w:hyperlink', NS))
        require(values == expected(doc) and list(values) == list(expected(doc)), 'docx: content coverage differs')
        require(set(anchors) <= bookmarks, 'docx: unresolved internal link')
        embeds = {n.get('{' + R + '}embed') for n in root.iter() if n.tag.endswith('}blip')}
        require(embeds == image_refs, 'docx: image relationship mismatch')
        diagram_marks = {n.get('name'): n.get('descr') for n in root.iter() if n.tag.endswith('}docPr')}
        expected_marks = {b['asset']: 'Mermaid source SHA-256 ' + sha(b['text'].encode()) for b in doc if b['kind'] == 'diagram'}
        expected_marks.update({b['asset']: 'JPEG SHA-256 ' + b['sha256'] for b in doc if b['kind'] == 'image'})
        require(diagram_marks == expected_marks, 'docx: diagram/frame coverage differs')
        require(sum(n.tag.endswith('}docPr') for n in root.iter()) == len(expected_marks)
                and sum(n.tag.endswith('}blip') for n in root.iter()) == len(expected_marks), 'docx: image count differs')
        image_targets = {r.get('Id'): r.get('Target') for r in parsed['word/_rels/document.xml.rels']}
        expected_jpegs = {b['asset'] for b in doc if b['kind'] == 'image'}
        for drawing in (n for n in root.iter() if n.tag.endswith('}inline')):
            name = next((n.get('name') for n in drawing.iter() if n.tag.endswith('}docPr')), None)
            if name in expected_jpegs:
                refs = [n.get('{' + R + '}embed') for n in drawing.iter() if n.tag.endswith('}blip')]
                require(len(refs) == 1 and image_targets.get(refs[0]) == 'media/' + name + '.jpg', 'docx: frame relationship differs')
        for block in (b for b in doc if b['kind'] == 'image'):
            require(sha(z.read('word/media/' + block['asset'] + '.jpg')) == block['sha256'], 'docx: JPEG hash differs')
        require({n for n in names if n.endswith('.jpg')} == {'word/media/' + b['asset'] + '.jpg' for b in doc if b['kind'] == 'image'}, 'docx: JPEG membership differs')
        jpeg_types = [n for n in types if n.get('Extension') == 'jpg']
        require(not any(b['kind'] == 'image' for b in doc) or len(jpeg_types) == 1 and jpeg_types[0].get('ContentType') == 'image/jpeg', 'docx: JPEG content type differs')
        require(BANNER in ''.join(root.itertext()), 'docx: missing banner')
        footer = parsed['word/footer1.xml']
        require({n.get('{' + W + '}instr') for n in footer.iter() if n.tag.endswith('}fldSimple')} == {'PAGE', 'NUMPAGES'},
                'docx: page fields missing')
    return len(values)
