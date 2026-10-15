"""GCAM Studio manual: an edition's Markdown also as PDF and Word (standard library only, deterministic).

The writers read the edition's own MANUAL.Studio.md (the restricted Markdown the generator writes: headings, paragraphs
with bold / italic / code spans and internal links, lists, notice blocks and centred figures with captions) and the
edition's figure PNGs, and lay out the same content in the same order. Nothing is reworded or added beyond page furniture
(page numbers, the PDF outline). Every compressed stream is written by the fixed-Huffman deflate below, so the bytes are
the same on every machine; figure pixels keep their PNG compression (the PNG's own IDAT stream, PNG predictors).

PDF: A4, 20 mm margins, PDF 1.7; text in two Type 3 fonts built from the committed glyph subsets of M PLUS Rounded 1c
(Regular for body text, Bold for headings and bold text; SIL OFL 1.1) with ToUnicode maps, so the text can be searched and
copied; a heading is kept with what follows it and a figure with its caption.

Word: a hand-written Office Open XML package: styles (Title, Heading 1-3, Normal, Caption, Notice, a code character style,
Hyperlink), bullet numbering, inline PNG drawings, bookmarks on headings for the internal links; fonts are named by style
only (body Segoe UI, code Consolas) and supplied by Word.
"""
import bisect
import hashlib
import html
import json
from pathlib import Path
import re
import struct
import zlib

HERE = Path(__file__).resolve().parent
FONT_FILES = {'R': HERE / 'font/mplus-rounded-1c-regular.glyphs.json', 'B': HERE / 'font/mplus-rounded-1c-bold.glyphs.json'}
FONT_NAMES = {'R': 'Regular', 'B': 'Bold'}
_FONTS = {}


def require(condition, code):
    if not condition:
        raise ValueError(code)


def fonts():
    """The two glyph subsets, loaded once: {'R': subset, 'B': subset}, each with its file's sha256 and repository path."""
    if not _FONTS:
        for key, path in FONT_FILES.items():
            data = path.read_bytes()
            _FONTS[key] = {**json.loads(data), 'sha256': hashlib.sha256(data).hexdigest(),
                           'path': path.relative_to(HERE.parents[1]).as_posix()}
    return _FONTS


# ---------------------------------------------------------------- deflate (fixed Huffman, greedy LZ77; RFC 1951)

_LEN_BASE = [3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258]
_LEN_EXTRA = [0] * 8 + [1] * 4 + [2] * 4 + [3] * 4 + [4] * 4 + [5] * 4 + [0]
_DIST_BASE = [1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097,
              6145, 8193, 12289, 16385, 24577]
_DIST_EXTRA = [0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13]
CHAIN = 24   # earlier positions of a 3-byte key tried per match (newest first)


def _reversed_bits(code, n):
    out = 0
    for _ in range(n):
        out, code = (out << 1) | (code & 1), code >> 1
    return out


def deflate(data):
    """Raw deflate of data in one fixed-Huffman block: the same bytes for the same input on every machine."""
    data = bytes(data)
    out, state = bytearray(), [0, 0]   # bit buffer, bit count
    def put(value, n):
        state[0] |= value << state[1]
        state[1] += n
        while state[1] >= 8:
            out.append(state[0] & 255)
            state[0] >>= 8
            state[1] -= 8
    def symbol(v):
        if v < 144:
            put(_reversed_bits(0x30 + v, 8), 8)
        elif v < 256:
            put(_reversed_bits(0x190 + v - 144, 9), 9)
        elif v < 280:
            put(_reversed_bits(v - 256, 7), 7)
        else:
            put(_reversed_bits(0xC0 + v - 280, 8), 8)
    put(1, 1)   # final block
    put(1, 2)   # fixed Huffman codes
    n, chains, i = len(data), {}, 0
    def remember(k):
        if k + 2 < n:
            chains.setdefault(data[k:k + 3], []).append(k)
    while i < n:
        best, distance = 0, 0
        if i + 2 < n:
            limit = min(258, n - i)
            for j in reversed(chains.get(data[i:i + 3], [])[-CHAIN:]):
                if i - j > 32768:
                    break
                length = 3
                while length < limit and data[j + length] == data[i + length]:
                    length += 1
                if length > best:
                    best, distance = length, i - j
                    if length == limit:
                        break
        if best >= 3:
            k = bisect.bisect_right(_LEN_BASE, best) - 1
            symbol(257 + k)
            put(best - _LEN_BASE[k], _LEN_EXTRA[k])
            d = bisect.bisect_right(_DIST_BASE, distance) - 1
            put(_reversed_bits(d, 5), 5)
            put(distance - _DIST_BASE[d], _DIST_EXTRA[d])
            for k in range(i, i + best):
                remember(k)
            i += best
        else:
            symbol(data[i])
            remember(i)
            i += 1
    symbol(256)
    if state[1]:
        out.append(state[0] & 255)
    return bytes(out)


def zlib_stream(data):
    """A zlib stream (RFC 1950) around deflate(data), as PDF's FlateDecode reads it."""
    return b'\x78\x01' + deflate(data) + struct.pack('>I', zlib.adler32(bytes(data)))


# ---------------------------------------------------------------- the edition's Markdown

INLINE = re.compile(r'\*\*(?P<b>.+?)\*\*|`(?P<c>[^`]+)`|\[(?P<lt>[^\]]+)\]\(#(?P<la>[^)\s]+)\)|<em>(?P<em>.+?)</em>'
                    r'|(?<![*\w])\*(?P<i>[^*\s][^*]*?)\*(?![*\w])')
FIGURE = re.compile(r'<p align="center"><img src="([^"]+)" alt="([^"]*)"></p>$')
CAPTION = re.compile(r'<p align="center"><em>(.*?)</em> ?(.*)</p>$')
HEADING = re.compile(r'(#{1,4}) (.+)$')
ITEM = re.compile(r'( *)- (.+)$')


def inline(text, style=frozenset(), link=None):
    """Runs (text, style, link anchor) of one block's Markdown text; style holds 'b', 'i' and 'code'."""
    runs, pos = [], 0
    def plain(part):
        require('`' not in part and '**' not in part, 'formats: unbalanced Markdown emphasis or code span: ' + part[:40])
        if part:
            runs.append((part, style, link))
    for m in INLINE.finditer(text):
        plain(text[pos:m.start()])
        if m['b'] is not None:
            runs.extend(inline(m['b'], style | {'b'}, link))
        elif m['c'] is not None:
            runs.append((m['c'], style | {'code'}, link))
        elif m['lt'] is not None:
            runs.extend(inline(m['lt'], style, m['la']))
        elif m['em'] is not None:
            runs.extend(inline(m['em'], style | {'i'}, link))
        else:
            runs.extend(inline(m['i'], style | {'i'}, link))
        pos = m.end()
    plain(text[pos:])
    return runs


def blocks(markdown, slug):
    """The edition's blocks in order: ('heading', level, runs, anchor), ('para', runs), ('notice', kind, runs),
    ('list', [(depth, runs)]), ('figure', src, alt, caption runs). Anything else is refused."""
    lines, out, i = markdown.split('\n'), [], 0
    def starts_block(line):
        return bool(HEADING.match(line) or FIGURE.match(line) or line.startswith('> ') or ITEM.match(line))
    while i < len(lines):
        line = lines[i]
        if not line.strip():
            i += 1
            continue
        m = HEADING.match(line)
        if m:
            out.append(('heading', len(m[1]), inline(m[2]), slug(m[2])))
            i += 1
            continue
        m = FIGURE.match(line)
        if m:
            caption = CAPTION.match(lines[i + 1]) if i + 1 < len(lines) else None
            require(caption, 'formats: a figure without its caption line: ' + m[1])
            runs = [(html.unescape(caption[1]) + ' ', frozenset({'i', 'b'}), None)]
            runs += [(text, style | {'i'}, link) for text, style, link in inline(html.unescape(caption[2]))]
            out.append(('figure', html.unescape(m[1]), html.unescape(m[2]), runs))
            i += 2
            continue
        if line.startswith('> '):
            text = []
            while i < len(lines) and lines[i].startswith('> '):
                text.append(lines[i][2:].strip())
                i += 1
            joined = ' '.join(text)
            kind = next((k for k in ('caution', 'warning', 'note') if joined.lower().startswith(f'**{k}.**')), 'info')
            out.append(('notice', kind, inline(joined)))
            continue
        if ITEM.match(line):
            items = []
            while i < len(lines) and lines[i].strip():
                m = ITEM.match(lines[i])
                if m:
                    items.append([len(m[1]) // 2, m[2]])
                else:
                    require(items and lines[i].startswith('  '), 'formats: unsupported list line: ' + lines[i][:40])
                    items[-1][1] += ' ' + lines[i].strip()
                i += 1
            out.append(('list', [(depth, inline(text)) for depth, text in items]))
            continue
        require(not line.startswith(('<', '|', '```', '    ')), 'formats: unsupported Markdown line: ' + line[:40])
        text = []
        while i < len(lines) and lines[i].strip() and not starts_block(lines[i]):
            require(not lines[i].startswith(('<', '|', '```')), 'formats: unsupported Markdown line: ' + lines[i][:40])
            text.append(lines[i].strip())
            i += 1
        out.append(('para', inline(' '.join(text))))
    return out


def png_parts(data):
    """(width, height, IDAT zlib stream) of an 8-bit RGB non-interlaced PNG; anything else is refused."""
    require(data[:8] == b'\x89PNG\r\n\x1a\n', 'formats: figure is not a PNG')
    pos, header, idat = 8, None, []
    while pos < len(data):
        length, kind = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        if kind == b'IHDR':
            header = struct.unpack('>IIBBBBB', body)
        elif kind == b'IDAT':
            idat.append(body)
        elif kind == b'IEND':
            break
        pos += 12 + length
    require(header is not None and header[2:] == (8, 2, 0, 0, 0) and idat, 'formats: figure PNG is not 8-bit RGB, non-interlaced')
    return header[0], header[1], b''.join(idat)


# ---------------------------------------------------------------- PDF

A4 = (595.28, 841.89)
MARGIN = 56.69   # 20 mm
TEXT_W = A4[0] - 2 * MARGIN
TOP, BOTTOM = A4[1] - MARGIN, MARGIN
STYLES = {   # font, size, leading, space before, space after
    'title': ('B', 20.0, 26.0, 0.0, 8.0), 1: ('B', 20.0, 26.0, 0.0, 8.0), 2: ('B', 15.0, 20.0, 16.0, 6.0),
    3: ('B', 12.5, 17.0, 12.0, 5.0), 4: ('B', 10.5, 15.0, 8.0, 3.0),
    'body': ('R', 9.5, 13.5, 0.0, 6.0), 'caption': ('R', 8.5, 12.0, 4.0, 10.0), 'footer': ('R', 7.5, 9.0, 0.0, 0.0)}
INK, LINK, MUTED = (0.13, 0.13, 0.13), (0.11, 0.33, 0.62), (0.42, 0.42, 0.42)
SHADE, CODE_BG = (0.95, 0.95, 0.94), (0.91, 0.91, 0.91)
BARS = {'caution': (0.80, 0.52, 0.0), 'warning': (0.75, 0.15, 0.10), 'note': (0.33, 0.47, 0.60), 'info': (0.55, 0.55, 0.55)}
ITALIC = 0.21    # shear of the italic text matrix (the font has no italic face)
LIST_INDENT, BULLET = 14.0, 1.6
PAD = 7.0        # notice box padding
PX = 0.75        # points per figure pixel at 96 px per inch: figures are never drawn larger than this
SHRINK = 0.8     # a figure may be drawn down to this share of its size to stay on the page with what leads to it


def num(value):
    text = f'{value:.2f}'.rstrip('0').rstrip('.')
    return '0' if text in ('-0', '') else text


class Layout:
    """Places the blocks on A4 pages. Pages hold draw operations; links and heading destinations are kept for the writer."""

    def __init__(self, footer):
        self.pages, self.dest, self.outline, self.used = [], {}, [], {'R': set(), 'B': set()}
        self.footer = footer
        self.new_page()

    def new_page(self):
        self.pages.append({'ops': [], 'links': []})
        self.y = TOP

    def ops(self):
        return self.pages[-1]['ops']

    def width(self, text, font, size):
        glyphs = fonts()[font]['glyphs']
        total = 0
        for char in text:
            require(char in glyphs, f'pdf-glyph: no glyph for U+{ord(char):04X} in {FONT_NAMES[font]} text')
            total += glyphs[char]['advance']
        return total * size / 1000

    def lines(self, runs, base, width, first_indent=0.0):
        """Greedy line breaking at spaces. Returns lines as lists of (x, text, font, size, italic, code, link)."""
        font0, size = base[0], base[1]
        tokens = []   # words: lists of fragments (text, font, italic, code, link); None marks a space
        for text, style, link in runs:
            font = 'B' if 'b' in style else font0
            for k, part in enumerate(re.split(r'( )', text)):
                if part == ' ':
                    tokens.append((None, font, 'i' in style, 'code' in style, link))
                elif part:
                    tokens.append((part, font, 'i' in style, 'code' in style, link))
        words, current, spaces = [], [], []
        for token in tokens:
            if token[0] is None:
                if current:
                    words.append(current)
                    current = []
                spaces.append((len(words), token))
            else:
                current.append(token)
        if current:
            words.append(current)
        space_before = {}
        for index, token in spaces:
            space_before.setdefault(index, token)
        out, line, x, limit = [], [], first_indent, width
        def flush():
            nonlocal line, x
            out.append(line)
            line, x = [], 0.0
        for index, word in enumerate(words):
            w = sum(self.width(t, f, size) for t, f, *_ in word)
            space = space_before.get(index)
            sw = self.width(' ', space[1], size) if space and line else 0.0
            if line and x + sw + w > limit:
                flush()
                sw = 0.0
            if sw:
                line.append((x, ' ', space[1], size, space[2], space[3] and line[-1][5], space[4] if line[-1][6] == space[4] else None))
                x += sw
            for text, font, italic, code, link in word:
                fw = self.width(text, font, size)
                if fw > limit:   # a word longer than the line: broken by characters
                    for char in text:
                        cw = self.width(char, font, size)
                        if line and x + cw > limit:
                            flush()
                        line.append((x, char, font, size, italic, code, link))
                        x += cw
                    continue
                line.append((x, text, font, size, italic, code, link))
                x += fw
        if line or not out:
            out.append(line)
        return [merge(l) for l in out]

    def draw_line(self, line, x0, top, leading, color=INK, centre_width=None):
        """Draws one line with its top at `top`; returns nothing. Code spans get a grey background, links a link box."""
        if centre_width is not None and line:
            end = line[-1][0] + self.width(line[-1][1], line[-1][2], line[-1][3])
            x0 += (centre_width - end) / 2
        for x, text, font, size, italic, code, link in line:
            w = self.width(text, font, size)
            base = top - leading / 2 - size * 0.34
            if code:
                self.ops().append(('rect', x0 + x - 1.2, base - size * 0.28, w + 2.4, size * 1.22, CODE_BG))
            self.used[font].update(text)
            self.ops().append(('text', font, size, italic, x0 + x, base, text, LINK if link else color))
            if link:
                self.pages[-1]['links'].append((x0 + x, base - size * 0.25, x0 + x + w, base + size * 0.85, link))

    # ---- block measurement and placement

    def heading_lines(self, block):
        style = STYLES[block[1]]
        return self.lines(block[2], style, TEXT_W), style

    def need(self, blocks, index, minimal=False):
        """Height that must fit on the page for block index to start here: a heading with what follows it, the first two
        lines of a paragraph or list (a short paragraph with the figure after it), a whole notice or figure."""
        block = blocks[index]
        kind = block[0]
        if kind == 'heading':
            lines, style = self.heading_lines(block)
            own = style[3] + len(lines) * style[2] + style[4]
            return own + (self.need(blocks, index + 1, minimal) if index + 1 < len(blocks) else 0)
        if kind == 'para':
            count = len(self.lines(block[1], STYLES['body'], TEXT_W))
            if not minimal and count <= 3 and index + 1 < len(blocks) and blocks[index + 1][0] == 'figure':   # a step stays with its figure
                together = count * STYLES['body'][2] + STYLES['body'][4] + self.figure_least(blocks[index + 1])
                if together <= TOP - BOTTOM:
                    return together
            return min(2, count) * STYLES['body'][2]
        if kind == 'list':
            depth, runs = block[1][0]
            return min(2, len(self.lines(runs, STYLES['body'], TEXT_W - (depth + 1) * LIST_INDENT))) * STYLES['body'][2]
        if kind == 'notice':
            return self.notice_height(block)
        return self.figure_least(block)

    def figure_least(self, block):
        """The least height a figure with its caption takes: its image drawn at SHRINK of its size."""
        w, h, _, total = self.figure_size(block)
        return total - h * (1 - SHRINK)

    def notice_height(self, block):
        lines = self.lines(block[2], STYLES['body'], TEXT_W - 2 * PAD - 3)
        return len(lines) * STYLES['body'][2] + 2 * PAD

    def figure_size(self, block, figures=None):
        figures = figures if figures is not None else self.figures
        pw, ph = figures[block[1]][:2]
        caption = self.lines(block[3], STYLES['caption'], TEXT_W)
        cap_h = STYLES['caption'][3] + len(caption) * STYLES['caption'][2]
        w = min(TEXT_W, pw * PX)
        h = w * ph / pw
        room = TOP - BOTTOM - cap_h - 6
        if h > room:
            w, h = w * room / h, room
        return w, h, caption, h + cap_h + STYLES['caption'][4]

    def room(self):
        return self.y - BOTTOM

    def ensure(self, height):
        if height > self.room() and self.y < TOP:
            self.new_page()

    def place(self, blocks, figures):
        self.figures = figures
        minimal = False
        for index, block in enumerate(blocks):
            kind = block[0]
            need = self.need(blocks, index, minimal)
            if need > TOP - BOTTOM:   # the whole group cannot share one page: keep only a heading with two lines,
                minimal = True        # down the rest of its heading chain too (else a heading is left alone)
                need = self.need(blocks, index, minimal)
            self.ensure(need if need <= TOP - BOTTOM else 0)
            minimal = minimal and kind == 'heading'
            if kind == 'heading':
                lines, style = self.heading_lines(block)
                if self.y < TOP:
                    self.y -= style[3]
                self.dest.setdefault(block[3], (len(self.pages) - 1, self.y))
                if block[1] in (2, 3):
                    self.outline.append((block[1], ''.join(r[0] for r in block[2]), block[3]))
                for line in lines:
                    self.draw_line(line, MARGIN, self.y, style[2], (0.07, 0.07, 0.07))
                    self.y -= style[2]
                self.y -= style[4]
            elif kind == 'para':
                self.flow(self.lines(block[1], STYLES['body'], TEXT_W), MARGIN)
                self.y -= STYLES['body'][4]
            elif kind == 'list':
                for depth, runs in block[1]:
                    indent = (depth + 1) * LIST_INDENT
                    lines = self.lines(runs, STYLES['body'], TEXT_W - indent)
                    self.flow(lines, MARGIN + indent, bullet=MARGIN + indent - 8 + 0 * depth)
                    self.y -= 1.5
                self.y -= STYLES['body'][4] - 1.5
            elif kind == 'notice':
                lines = self.lines(block[2], STYLES['body'], TEXT_W - 2 * PAD - 3)
                height = len(lines) * STYLES['body'][2] + 2 * PAD
                self.ops().append(('rect', MARGIN, self.y - height, TEXT_W, height, SHADE))
                self.ops().append(('rect', MARGIN, self.y - height, 3.0, height, BARS[block[1]]))
                top = self.y - PAD
                for line in lines:
                    self.draw_line(line, MARGIN + 3 + PAD, top, STYLES['body'][2])
                    top -= STYLES['body'][2]
                self.y -= height + STYLES['body'][4] + 2
            else:
                w, h, caption, total = self.figure_size(block)
                if total > self.room():   # drawn smaller (down to SHRINK) rather than leave the page
                    scale = max(SHRINK, (self.room() - (total - h)) / h)
                    w, h = w * scale, h * scale
                self.ops().append(('image', block[1], MARGIN + (TEXT_W - w) / 2, self.y - h, w, h))
                self.y -= h + STYLES['caption'][3]
                for line in caption:
                    self.draw_line(line, MARGIN, self.y, STYLES['caption'][2], MUTED, centre_width=TEXT_W)
                    self.y -= STYLES['caption'][2]
                self.y -= STYLES['caption'][4]
        total = len(self.pages)
        for number, page in enumerate(self.pages, 1):   # page furniture
            font, size, leading = STYLES['footer'][:3]
            for text, x in ((self.footer, MARGIN), (f'page {number} of {total}', None)):
                self.used[font].update(text)
                w = self.width(text, font, size)
                page['ops'].append(('text', font, size, False, x if x is not None else A4[0] - MARGIN - w, BOTTOM / 2, text, MUTED))

    def flow(self, lines, x0, bullet=None):
        """Places lines, breaking pages between them: never one line alone at a page's foot or head when avoidable."""
        leading = STYLES['body'][2]
        k = 0
        while k < len(lines):
            fit = int(self.room() // leading)
            left = len(lines) - k
            if fit < left:   # the block breaks here: keep two lines on each side where possible
                if fit < 2 and self.y < TOP:
                    self.new_page()
                    continue
                if left - fit == 1 and fit > 2:
                    fit -= 1
                take = fit
            else:
                take = left
            for line in lines[k:k + take]:
                if bullet is not None and k == 0:
                    self.ops().append(('dot', bullet, self.y - leading / 2 - STYLES['body'][1] * 0.02, BULLET, INK))
                self.draw_line(line, x0, self.y, leading)
                self.y -= leading
                k += 1
            if k < len(lines):
                self.new_page()


def merge(line):
    """Adjacent pieces of one line with the same font, size, style and link drawn as one string."""
    out = []
    for piece in line:
        if out and out[-1][2:] == piece[2:]:
            out[-1] = (out[-1][0], out[-1][1] + piece[1]) + piece[2:]
        else:
            out.append(piece)
    return out


def pdf_string(data):
    out = bytearray(b'(')
    for byte in data:
        if byte in b'()\\':
            out += b'\\' + bytes([byte])
        elif 32 <= byte < 127:
            out.append(byte)
        else:
            out += b'\\%03o' % byte
    return bytes(out + b')')


def pdf_text(text):
    """A PDF text string (title, outline entries): ASCII as is, anything else as UTF-16BE with a byte order mark."""
    if all(32 <= ord(c) < 127 for c in text):
        return pdf_string(text.encode('ascii'))
    return b'<FEFF' + text.encode('utf-16-be').hex().upper().encode('ascii') + b'>'


def glyph_proc(glyph, upem):
    """A Type 3 glyph procedure: d1 with the glyph's box, then its outline as a filled path (quadratics as cubics)."""
    paths, xs, ys = [], [], []
    for flat in glyph['contours']:
        points = [(flat[i], flat[i + 1], flat[i + 2]) for i in range(0, len(flat), 3)]
        if not points:
            continue
        start = next((i for i, p in enumerate(points) if p[2]), None)
        if start is None:
            first = ((points[-1][0] + points[0][0]) / 2, (points[-1][1] + points[0][1]) / 2, 1)
            sequence = [first] + points
        else:
            sequence = points[start:] + points[:start]
        xs += [p[0] for p in points]
        ys += [p[1] for p in points]
        current, control = sequence[0], None
        parts = [f'{num(current[0])} {num(current[1])} m']
        def curve(p0, p1, p2):
            c1 = (p0[0] + 2 * (p1[0] - p0[0]) / 3, p0[1] + 2 * (p1[1] - p0[1]) / 3)
            c2 = (p2[0] + 2 * (p1[0] - p2[0]) / 3, p2[1] + 2 * (p1[1] - p2[1]) / 3)
            parts.append(f'{num(c1[0])} {num(c1[1])} {num(c2[0])} {num(c2[1])} {num(p2[0])} {num(p2[1])} c')
        for point in sequence[1:] + [sequence[0]]:
            if not point[2] and control is not None:
                middle = ((control[0] + point[0]) / 2, (control[1] + point[1]) / 2, 1)
                curve(current, control, middle)
                current, control = middle, point
            elif not point[2]:
                control = point
            elif control is None:
                parts.append(f'{num(point[0])} {num(point[1])} l')
                current = point
            else:
                curve(current, control, point)
                current, control = point, None
        paths.append(' '.join(parts) + ' h')
    box = (min(xs), min(ys), max(xs), max(ys)) if xs else (0, 0, 0, 0)
    head = f'{glyph["advance"]} 0 {num(box[0])} {num(box[1])} {num(box[2])} {num(box[3])} d1'
    return ('\n'.join([head] + paths + (['f'] if paths else [])) + '\n').encode('ascii'), box


def codes_for(chars):
    """Single-byte codes: printable ASCII keeps its own code, other characters take 128, 129, ... in code point order."""
    mapping, extra = {}, 128
    for char in sorted(chars):
        if 32 <= ord(char) < 127:
            mapping[char] = ord(char)
        else:
            require(extra < 256, 'pdf: too many distinct characters for one Type 3 font')
            mapping[char] = extra
            extra += 1
    return mapping


def to_unicode(mapping):
    rows = sorted((code, char) for char, code in mapping.items())
    out = ['/CIDInit /ProcSet findresource begin', '12 dict begin', 'begincmap',
           '/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def', '/CMapName /Adobe-Identity-UCS def',
           '/CMapType 2 def', '1 begincodespacerange', '<00> <FF>', 'endcodespacerange']
    for k in range(0, len(rows), 100):
        chunk = rows[k:k + 100]
        out.append(f'{len(chunk)} beginbfchar')
        out += [f'<{code:02X}> <{char.encode("utf-16-be").hex().upper()}>' for code, char in chunk]
        out.append('endbfchar')
    out += ['endcmap', 'CMapName currentdict /CMap defineresource pop', 'end', 'end']
    return ('\n'.join(out) + '\n').encode('ascii')


class PdfObjects:
    def __init__(self):
        self.bodies = []

    def reserve(self):
        self.bodies.append(None)
        return len(self.bodies)

    def set(self, number, body):
        self.bodies[number - 1] = body

    def add(self, body):
        self.bodies.append(body)
        return len(self.bodies)

    def stream(self, dictionary, data, compress=True):
        payload = zlib_stream(data) if compress else data
        head = dictionary + (b' /Filter /FlateDecode' if compress else b'')
        return b'<< ' + head + b' /Length %d >>\nstream\n' % len(payload) + payload + b'\nendstream'

    def write(self, root, info, ident):
        out = bytearray(b'%PDF-1.7\n%\xe2\xe3\xcf\xd3\n')
        offsets = []
        for number, body in enumerate(self.bodies, 1):
            require(body is not None, f'pdf: object {number} left unset')
            offsets.append(len(out))
            out += b'%d 0 obj\n' % number + body + b'\nendobj\n'
        xref = len(out)
        out += b'xref\n0 %d\n0000000000 65535 f \n' % (len(self.bodies) + 1)
        out += b''.join(b'%010d 00000 n \n' % offset for offset in offsets)
        out += (b'trailer\n<< /Size %d /Root %d 0 R /Info %d 0 R /ID [<%s> <%s>] >>\nstartxref\n%d\n%%%%EOF\n'
                % (len(self.bodies) + 1, root, info, ident, ident, xref))
        return bytes(out)


def pdf(markdown, figures, meta, slug):
    """The edition as PDF bytes. figures: {path: png bytes}; meta: title, editionId, date. Returns (bytes, page count)."""
    parsed = blocks(markdown, slug)
    images = {path: png_parts(data) for path, data in figures.items()}
    layout = Layout(f'{meta["title"]} · edition {meta["editionId"]}')
    layout.place(parsed, images)
    objects = PdfObjects()
    catalog, pages_ref, info = objects.reserve(), objects.reserve(), objects.reserve()
    font_refs, mappings = {}, {}
    for key in ('R', 'B'):   # Type 3 fonts holding exactly the characters drawn in each face
        subset, chars = fonts()[key], layout.used[key]
        mapping = codes_for(chars)
        mappings[key] = mapping
        procs, widths, box = [], {}, [0, 0, 0, 0]
        for char, code in sorted(mapping.items(), key=lambda item: item[1]):
            data, gbox = glyph_proc(subset['glyphs'][char], subset['unitsPerEm'])
            procs.append((f'g{code:02X}', objects.add(objects.stream(b'', data))))
            widths[code] = subset['glyphs'][char]['advance']
            box = [min(box[0], gbox[0]), min(box[1], gbox[1]), max(box[2], gbox[2]), max(box[3], gbox[3])]
        first, last = min(widths), max(widths)
        unicode_ref = objects.add(objects.stream(b'', to_unicode(mapping)))
        scale = f'{1 / subset["unitsPerEm"]:.6f}'.rstrip('0')
        font_refs[key] = objects.add(
            b'<< /Type /Font /Subtype /Type3 /Name /F%s /FontBBox [%s] /FontMatrix [%s 0 0 %s 0 0]' % (
                key.encode(), ' '.join(num(v) for v in box).encode(), scale.encode(), scale.encode())
            + b' /CharProcs << ' + b' '.join(b'/%s %d 0 R' % (name.encode(), ref) for name, ref in procs) + b' >>'
            + b' /Encoding << /Type /Encoding /Differences [' + b' '.join(b'%d /%s' % (int(name[1:], 16), name.encode())
                                                                         for name, _ in procs) + b'] >>'
            + b' /FirstChar %d /LastChar %d /Widths [%s]' % (first, last, ' '.join(str(widths.get(c, 0))
                                                                                 for c in range(first, last + 1)).encode())
            + b' /ToUnicode %d 0 R /Resources << >> >>' % unicode_ref)
    image_refs = {}
    for page in layout.pages:
        for op in page['ops']:
            if op[0] == 'image' and op[1] not in image_refs:
                w, h, idat = images[op[1]]
                image_refs[op[1]] = (f'Im{len(image_refs) + 1}', objects.add(objects.stream(
                    b'/Type /XObject /Subtype /Image /Width %d /Height %d /ColorSpace /DeviceRGB /BitsPerComponent 8'
                    b' /Filter /FlateDecode /DecodeParms << /Predictor 15 /Colors 3 /BitsPerComponent 8 /Columns %d >>'
                    % (w, h, w), idat, compress=False)))
    page_refs = [objects.reserve() for _ in layout.pages]
    for index, page in enumerate(layout.pages):
        content = []
        for op in page['ops']:
            if op[0] == 'rect':
                _, x, y, w, h, colour = op
                content.append(f'{num(colour[0])} {num(colour[1])} {num(colour[2])} rg {num(x)} {num(y)} {num(w)} {num(h)} re f')
            elif op[0] == 'dot':
                _, x, y, r, colour = op
                k = r * 0.5523
                content.append(f'{num(colour[0])} {num(colour[1])} {num(colour[2])} rg {num(x + r)} {num(y)} m '
                               f'{num(x + r)} {num(y + k)} {num(x + k)} {num(y + r)} {num(x)} {num(y + r)} c '
                               f'{num(x - k)} {num(y + r)} {num(x - r)} {num(y + k)} {num(x - r)} {num(y)} c '
                               f'{num(x - r)} {num(y - k)} {num(x - k)} {num(y - r)} {num(x)} {num(y - r)} c '
                               f'{num(x + k)} {num(y - r)} {num(x + r)} {num(y - k)} {num(x + r)} {num(y)} c f')
            elif op[0] == 'image':
                _, path, x, y, w, h = op
                content.append(f'q {num(w)} 0 0 {num(h)} {num(x)} {num(y)} cm /{image_refs[path][0]} Do Q')
            else:
                _, font, size, italic, x, y, text, colour = op
                data = bytes(mappings[font][c] for c in text)
                content.append(f'{num(colour[0])} {num(colour[1])} {num(colour[2])} rg BT /F{font} {num(size)} Tf '
                               f'1 0 {num(ITALIC) if italic else 0} 1 {num(x)} {num(y)} Tm ' + pdf_string(data).decode('latin-1')
                               + ' Tj ET')
        content_ref = objects.add(objects.stream(b'', ('\n'.join(content) + '\n').encode('latin-1')))
        annots = []
        for x0, y0, x1, y1, anchor in page['links']:
            if anchor in layout.dest:
                target_page, top = layout.dest[anchor]
                annots.append(b'<< /Type /Annot /Subtype /Link /Rect [%s] /Border [0 0 0] /Dest [%d 0 R /XYZ %s %s null] >>' % (
                    ' '.join(num(v) for v in (x0, y0, x1, y1)).encode(), page_refs[target_page], num(MARGIN).encode(),
                    num(top).encode()))
        xobjects = sorted({image_refs[op[1]] for op in page['ops'] if op[0] == 'image'})
        resources = (b'<< /Font << /FR %d 0 R /FB %d 0 R >>' % (font_refs['R'], font_refs['B'])
                     + (b' /XObject << ' + b' '.join(b'/%s %d 0 R' % (n.encode(), r) for n, r in xobjects) + b' >>' if xobjects else b'')
                     + b' >>')
        objects.set(page_refs[index], b'<< /Type /Page /Parent %d 0 R /MediaBox [0 0 %s %s] /Resources %s /Contents %d 0 R%s >>' % (
            pages_ref, num(A4[0]).encode(), num(A4[1]).encode(), resources, content_ref,
            (b' /Annots [' + b' '.join(annots) + b']') if annots else b''))
    objects.set(pages_ref, b'<< /Type /Pages /Kids [%s] /Count %d >>' % (
        b' '.join(b'%d 0 R' % r for r in page_refs), len(page_refs)))
    outline_ref = outline(objects, layout, page_refs)
    objects.set(catalog, b'<< /Type /Catalog /Pages %d 0 R /Lang (en)%s >>' % (
        pages_ref, (b' /Outlines %d 0 R /PageMode /UseOutlines' % outline_ref) if outline_ref else b''))
    stamp = 'D:' + meta['date'].replace('-', '') + '000000Z'
    objects.set(info, b'<< /Title %s /Subject %s /Producer (samples/manual/manual_formats.py) /CreationDate (%s) /ModDate (%s) >>' % (
        pdf_text(meta['title']), pdf_text('Edition ' + meta['editionId']), stamp.encode(), stamp.encode()))
    ident = hashlib.sha256(('gcam-manual ' + meta['editionId'] + ' ' + meta['date']).encode()).hexdigest()[:32].upper().encode()
    data = objects.write(catalog, info, ident)
    return data, len(layout.pages)


def outline(objects, layout, page_refs):
    """The PDF outline (bookmarks): parts, each with its chapters."""
    if not layout.outline:
        return None
    root = objects.reserve()
    tree = []
    for level, title, anchor in layout.outline:
        if level == 2 or not tree:
            tree.append([(title, anchor), []])
        else:
            tree[-1][1].append((title, anchor))
    refs = [objects.reserve() for _ in tree]
    def dest(anchor):
        page, top = layout.dest[anchor]
        return b'[%d 0 R /XYZ %s %s null]' % (page_refs[page], num(MARGIN).encode(), num(top).encode())
    for k, ((title, anchor), children) in enumerate(tree):
        child_refs = [objects.reserve() for _ in children]
        for j, (child, child_anchor) in enumerate(children):
            links = (b' /Prev %d 0 R' % child_refs[j - 1] if j else b'') + (b' /Next %d 0 R' % child_refs[j + 1] if j + 1 < len(children) else b'')
            objects.set(child_refs[j], b'<< /Title %s /Parent %d 0 R /Dest %s%s >>' % (pdf_text(child), refs[k], dest(child_anchor), links))
        links = (b' /Prev %d 0 R' % refs[k - 1] if k else b'') + (b' /Next %d 0 R' % refs[k + 1] if k + 1 < len(tree) else b'')
        kids = (b' /First %d 0 R /Last %d 0 R /Count %d' % (child_refs[0], child_refs[-1], len(children))) if children else b''
        objects.set(refs[k], b'<< /Title %s /Parent %d 0 R /Dest %s%s%s >>' % (pdf_text(title), root, dest(anchor), links, kids))
    objects.set(root, b'<< /Type /Outlines /First %d 0 R /Last %d 0 R /Count %d >>' % (refs[0], refs[-1], len(tree)))
    return root


# ---------------------------------------------------------------- Word (Office Open XML)

W = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
R = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
REL = 'http://schemas.openxmlformats.org/package/2006/relationships'
PAGE_TWIPS, MARGIN_TWIPS = (11906, 16838), 1134   # A4, 20 mm
EMU_PER_TWIP, EMU_PER_PX = 635, 9525              # a figure pixel at 96 px per inch
HEADING_STYLES = {1: 'Title', 2: 'Heading1', 3: 'Heading2', 4: 'Heading3'}


def esc(text):
    return html.escape(text, quote=True)


def docx_runs(runs, anchors):
    out = []
    for text, style, link in runs:
        props = (('<w:rStyle w:val="CodeChar"/>' if 'code' in style else '') + ('<w:b/>' if 'b' in style else '')
                 + ('<w:i/>' if 'i' in style else ''))
        if link and link in anchors:
            props = '<w:rStyle w:val="Hyperlink"/>' + props.replace('<w:rStyle w:val="CodeChar"/>', '')
        run = f'<w:r>{"<w:rPr>" + props + "</w:rPr>" if props else ""}<w:t xml:space="preserve">{esc(text)}</w:t></w:r>'
        if link and link in anchors:
            run = f'<w:hyperlink w:anchor="{anchors[link]}" w:history="1">{run}</w:hyperlink>'
        if out and link and link in anchors and out[-1].startswith(f'<w:hyperlink w:anchor="{anchors[link]}"'):
            out[-1] = out[-1][:-len('</w:hyperlink>')] + run[run.index('<w:r>'):]
        else:
            out.append(run)
    return ''.join(out)


def docx(markdown, figures, meta, slug):
    """The edition as a Word document (bytes)."""
    parsed = blocks(markdown, slug)
    anchors, bookmark = {}, 0
    for block in parsed:
        if block[0] == 'heading' and block[3] not in anchors:
            bookmark += 1
            anchors[block[3]] = f'h{bookmark}'
    body, rels, media, seen, picture = [], [], [], {}, 0
    text_emu = (PAGE_TWIPS[0] - 2 * MARGIN_TWIPS) * EMU_PER_TWIP
    body_emu = (PAGE_TWIPS[1] - 2 * MARGIN_TWIPS) * EMU_PER_TWIP - 900000
    marked = set()
    for block in parsed:
        kind = block[0]
        if kind == 'heading':
            mark = ''
            name = anchors[block[3]]
            if name not in marked:
                marked.add(name)
                number = int(name[1:])
                mark = f'<w:bookmarkStart w:id="{number}" w:name="{name}"/><w:bookmarkEnd w:id="{number}"/>'
            body.append(f'<w:p><w:pPr><w:pStyle w:val="{HEADING_STYLES[block[1]]}"/></w:pPr>{mark}{docx_runs(block[2], anchors)}</w:p>')
        elif kind == 'para':
            body.append(f'<w:p>{docx_runs(block[1], anchors)}</w:p>')
        elif kind == 'notice':
            body.append(f'<w:p><w:pPr><w:pStyle w:val="Notice"/></w:pPr>{docx_runs(block[2], anchors)}</w:p>')
        elif kind == 'list':
            for depth, runs in block[1]:
                body.append(f'<w:p><w:pPr><w:pStyle w:val="ListParagraph"/><w:numPr><w:ilvl w:val="{min(depth, 1)}"/>'
                            f'<w:numId w:val="1"/></w:numPr></w:pPr>{docx_runs(runs, anchors)}</w:p>')
        else:
            path = block[1]
            if path not in seen:
                seen[path] = f'rIdImage{len(seen) + 1}'
                media.append((f'word/media/{Path(path).name}', figures[path]))
                rels.append((seen[path], 'http://schemas.openxmlformats.org/officeDocument/2006/relationships/image',
                             f'media/{Path(path).name}'))
            w, h, _ = png_parts(figures[path])
            cx = min(text_emu, w * EMU_PER_PX)
            cy = cx * h // w
            if cy > body_emu:
                cx, cy = cx * body_emu // cy, body_emu
            picture += 1
            body.append(
                f'<w:p><w:pPr><w:pStyle w:val="Figure"/></w:pPr><w:r><w:drawing><wp:inline distT="0" distB="0" distL="0" distR="0">'
                f'<wp:extent cx="{cx}" cy="{cy}"/><wp:docPr id="{picture}" name="Figure {picture}" descr="{esc(block[2])}"/>'
                '<wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect="1"/></wp:cNvGraphicFramePr>'
                '<a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic>'
                f'<pic:nvPicPr><pic:cNvPr id="{picture}" name="{esc(Path(path).name)}"/><pic:cNvPicPr/></pic:nvPicPr>'
                f'<pic:blipFill><a:blip r:embed="{seen[path]}"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>'
                f'<pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="{cx}" cy="{cy}"/></a:xfrm>'
                '<a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic>'
                '</wp:inline></w:drawing></w:r></w:p>')
            body.append(f'<w:p><w:pPr><w:pStyle w:val="Caption"/></w:pPr>{docx_runs(block[3], anchors)}</w:p>')
    sect = (f'<w:sectPr><w:footerReference w:type="default" r:id="rIdFooter"/>'
            f'<w:pgSz w:w="{PAGE_TWIPS[0]}" w:h="{PAGE_TWIPS[1]}"/><w:pgMar w:top="{MARGIN_TWIPS}" w:right="{MARGIN_TWIPS}" '
            f'w:bottom="{MARGIN_TWIPS}" w:left="{MARGIN_TWIPS}" w:header="567" w:footer="567" w:gutter="0"/></w:sectPr>')
    document = (XML + f'<w:document xmlns:w="{W}" xmlns:r="{R}" '
                'xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" '
                'xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" '
                'xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">'
                f'<w:body>{"".join(body)}{sect}</w:body></w:document>')
    footer = (XML + f'<w:ftr xmlns:w="{W}"><w:p><w:pPr><w:pStyle w:val="Footer"/></w:pPr>'
              f'<w:r><w:t xml:space="preserve">{esc(meta["title"])} · edition {esc(meta["editionId"])} · page </w:t></w:r>'
              '<w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText xml:space="preserve"> PAGE </w:instrText></w:r>'
              '<w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>1</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r>'
              '</w:p></w:ftr>')
    rels = [('rIdStyles', 'http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles', 'styles.xml'),
            ('rIdNumbering', 'http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering', 'numbering.xml'),
            ('rIdSettings', 'http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings', 'settings.xml'),
            ('rIdFooter', 'http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer', 'footer1.xml')] + rels
    stamp = meta['date'] + 'T00:00:00Z'
    parts = {
        '[Content_Types].xml': XML + '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
        '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
        '<Default Extension="xml" ContentType="application/xml"/><Default Extension="png" ContentType="image/png"/>'
        + ''.join(f'<Override PartName="/{name}" ContentType="{kind}"/>' for name, kind in CONTENT_TYPES) + '</Types>',
        '_rels/.rels': XML + f'<Relationships xmlns="{REL}">'
        '<Relationship Id="rIdDocument" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>'
        '<Relationship Id="rIdCore" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>'
        '<Relationship Id="rIdApp" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>'
        '</Relationships>',
        'docProps/core.xml': XML + '<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" '
        'xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" '
        'xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">'
        f'<dc:title>{esc(meta["title"])}</dc:title><dc:subject>Edition {esc(meta["editionId"])}</dc:subject>'
        '<dc:creator>samples/manual/manual_formats.py</dc:creator><dc:language>en</dc:language>'
        f'<dcterms:created xsi:type="dcterms:W3CDTF">{stamp}</dcterms:created>'
        f'<dcterms:modified xsi:type="dcterms:W3CDTF">{stamp}</dcterms:modified></cp:coreProperties>',
        'docProps/app.xml': XML + '<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties">'
        '<Application>samples/manual/manual_formats.py</Application></Properties>',
        'word/document.xml': document,
        'word/_rels/document.xml.rels': XML + f'<Relationships xmlns="{REL}">'
        + ''.join(f'<Relationship Id="{i}" Type="{t}" Target="{esc(target)}"/>' for i, t, target in rels) + '</Relationships>',
        'word/styles.xml': STYLES_XML,
        'word/numbering.xml': NUMBERING_XML,
        'word/settings.xml': SETTINGS_XML,
        'word/footer1.xml': footer}
    entries = [(name, text.encode('utf-8'), True) for name, text in parts.items()] + [(name, data, False) for name, data in media]
    return zip_bytes(entries, meta['date'])


XML = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n'
CONTENT_TYPES = [
    ('docProps/app.xml', 'application/vnd.openxmlformats-officedocument.extended-properties+xml'),
    ('docProps/core.xml', 'application/vnd.openxmlformats-package.core-properties+xml'),
    ('word/document.xml', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml'),
    ('word/footer1.xml', 'application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml'),
    ('word/numbering.xml', 'application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml'),
    ('word/settings.xml', 'application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml'),
    ('word/styles.xml', 'application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml')]


def _style(kind, ident, name, ppr='', rpr='', based='Normal', extra=''):
    based_on = f'<w:basedOn w:val="{based}"/>' if based else ''
    return (f'<w:style w:type="{kind}" w:styleId="{ident}"><w:name w:val="{name}"/>{based_on}{extra}'
            f'{"<w:pPr>" + ppr + "</w:pPr>" if ppr else ""}{"<w:rPr>" + rpr + "</w:rPr>" if rpr else ""}</w:style>')


STYLES_XML = (XML + f'<w:styles xmlns:w="{W}">'
              '<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Segoe UI" w:hAnsi="Segoe UI" w:eastAsia="Segoe UI" w:cs="Segoe UI"/>'
              '<w:sz w:val="20"/><w:szCs w:val="20"/><w:lang w:val="en-GB"/></w:rPr></w:rPrDefault>'
              '<w:pPrDefault><w:pPr><w:spacing w:after="120" w:line="264" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>'
              + _style('paragraph', 'Normal', 'Normal', based=None, extra='<w:qFormat/>').replace('<w:style ', '<w:style w:default="1" ', 1)
              + _style('paragraph', 'Title', 'Title', '<w:keepNext/><w:spacing w:after="160"/>', '<w:b/><w:sz w:val="40"/>', extra='<w:next w:val="Normal"/><w:qFormat/>')
              + _style('paragraph', 'Heading1', 'heading 1', '<w:keepNext/><w:spacing w:before="360" w:after="120"/><w:outlineLvl w:val="0"/>',
                       '<w:b/><w:sz w:val="30"/>', extra='<w:next w:val="Normal"/><w:qFormat/>')
              + _style('paragraph', 'Heading2', 'heading 2', '<w:keepNext/><w:spacing w:before="280" w:after="100"/><w:outlineLvl w:val="1"/>',
                       '<w:b/><w:sz w:val="25"/>', extra='<w:next w:val="Normal"/><w:qFormat/>')
              + _style('paragraph', 'Heading3', 'heading 3', '<w:keepNext/><w:spacing w:before="200" w:after="60"/><w:outlineLvl w:val="2"/>',
                       '<w:b/><w:sz w:val="21"/>', extra='<w:next w:val="Normal"/><w:qFormat/>')
              + _style('paragraph', 'Caption', 'caption', '<w:spacing w:before="60" w:after="240"/><w:jc w:val="center"/>',
                       '<w:i/><w:color w:val="595959"/><w:sz w:val="17"/>', extra='<w:qFormat/>')
              + _style('paragraph', 'Figure', 'Figure', '<w:keepNext/><w:spacing w:before="120" w:after="0"/><w:jc w:val="center"/>')
              + _style('paragraph', 'Notice', 'Notice', '<w:pBdr><w:left w:val="single" w:sz="18" w:space="6" w:color="C48500"/></w:pBdr>'
                       '<w:shd w:val="clear" w:color="auto" w:fill="F2F2F0"/><w:spacing w:before="120" w:after="160"/><w:ind w:left="170" w:right="170"/>')
              + _style('paragraph', 'ListParagraph', 'List Paragraph', '<w:spacing w:after="60"/><w:contextualSpacing/>')
              + _style('paragraph', 'Footer', 'footer', '<w:jc w:val="right"/>', '<w:color w:val="6B6B6B"/><w:sz w:val="15"/>')
              + _style('character', 'CodeChar', 'Code', rpr='<w:rFonts w:ascii="Consolas" w:hAnsi="Consolas" w:cs="Consolas"/>'
                       '<w:sz w:val="18"/><w:shd w:val="clear" w:color="auto" w:fill="E8E8E8"/>', based=None)
              + _style('character', 'Hyperlink', 'Hyperlink', rpr='<w:color w:val="1C549E"/><w:u w:val="single"/>', based=None)
              + '</w:styles>')

NUMBERING_XML = (XML + f'<w:numbering xmlns:w="{W}"><w:abstractNum w:abstractNumId="0"><w:multiLevelType w:val="hybridMultilevel"/>'
                 '<w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val="•"/><w:lvlJc w:val="left"/>'
                 '<w:pPr><w:ind w:left="360" w:hanging="240"/></w:pPr></w:lvl>'
                 '<w:lvl w:ilvl="1"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val="–"/><w:lvlJc w:val="left"/>'
                 '<w:pPr><w:ind w:left="720" w:hanging="240"/></w:pPr></w:lvl></w:abstractNum>'
                 '<w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num></w:numbering>')

SETTINGS_XML = (XML + f'<w:settings xmlns:w="{W}"><w:defaultTabStop w:val="720"/>'
                '<w:compat><w:compatSetting w:name="compatibilityMode" w:uri="http://schemas.microsoft.com/office/word" w:val="15"/>'
                '</w:compat></w:settings>')


def zip_bytes(entries, date):
    """A ZIP archive with entries sorted by name, every timestamp the edition date at 00:00 and fixed attributes;
    compressed entries use deflate() above, stored ones (PNG) are kept as they are."""
    year, month, day = (int(v) for v in date.split('-'))
    dos_date, dos_time = ((year - 1980) << 9) | (month << 5) | day, 0
    out, central = bytearray(), bytearray()
    names = [name for name, _, _ in entries]
    require(len(set(names)) == len(names), 'docx: duplicate part name')
    for name, data, compress in sorted(entries, key=lambda entry: entry[0]):
        raw = name.encode('ascii')
        payload = deflate(data) if compress else data
        method, crc = (8 if compress else 0), zlib.crc32(data)
        offset = len(out)
        out += struct.pack('<IHHHHHIIIHH', 0x04034B50, 20, 0, method, dos_time, dos_date, crc, len(payload), len(data), len(raw), 0)
        out += raw + payload
        central += struct.pack('<IHHHHHHIIIHHHHHII', 0x02014B50, 20, 20, 0, method, dos_time, dos_date, crc, len(payload),
                               len(data), len(raw), 0, 0, 0, 0, 0, offset) + raw
    start = len(out)
    out += central + struct.pack('<IHHHHIIH', 0x06054B50, 0, 0, len(entries), len(entries), len(central), start, 0)
    return bytes(out)
