"""One-off extractor (standard library only): the glyph outlines the manual generator draws text with.

Reads a TrueType font (cmap format 4, head, hhea, maxp, hmtx, loca, glyf — simple and composite glyphs) and writes the
outlines of exactly the characters the manual may use — printable ASCII plus the non-ASCII characters of the manual
sources (CHARS) — as a compact JSON subset: advance widths and contours of on/off-curve points in font units. Only these
outlines are included; the font files themselves are not part of the repository. The Bold subset draws the figure labels
and the PDF's headings and bold text, the Regular subset the PDF's body text.

The font is M PLUS Rounded 1c (SIL Open Font License 1.1, see OFL.txt beside this file), Bold and Regular, each pinned by
its SHA-256. Its U+2026 is drawn centred on the em (Japanese style), so the subset builds U+2026 from three '.' glyphs on
the baseline instead and says so. A character the font has no glyph for is refused.

  python -B samples/manual/font/extract_glyphs.py <MPLUSRounded1c-Bold.ttf | MPLUSRounded1c-Regular.ttf> [--out <json>]
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct

HERE = Path(__file__).resolve().parent
SOURCES = {'c358630584e8e2d8fbd6121d0f4693255ffef6d1e6d4f3441fd6e5a963a11f9e': 'Bold',
           'b75708b53e45b06d17d470aeeca5b766e3d1b3999f03f13ec4eb863ca846c14c': 'Regular'}
# Printable ASCII and every non-ASCII character the manual sources use (registry, notices, purposes, static chapters,
# run records); the generator refuses text with a character outside the subset.
EXTRA = '§±µ·×σ–—…−'   # § ± µ · × σ – — … −
CHARS = [chr(c) for c in range(32, 127)] + sorted(EXTRA)


class Font:
    def __init__(self, data):
        self.data = data
        version, count = struct.unpack('>IH', data[:6])
        if version not in (0x00010000, 0x74727565):
            raise ValueError('not a TrueType outline font')
        self.tables = {}
        for i in range(count):
            tag, _, offset, length = struct.unpack('>4sIII', data[12 + 16 * i:28 + 16 * i])
            self.tables[tag.decode('latin-1')] = (offset, length)
        head = self.table('head')
        self.units_per_em = struct.unpack('>H', head[18:20])[0]
        self.long_loca = struct.unpack('>h', head[50:52])[0] == 1
        hhea = self.table('hhea')
        self.ascender, self.descender = struct.unpack('>hh', hhea[4:8])
        self.metrics = struct.unpack('>H', hhea[34:36])[0]
        self.glyph_count = struct.unpack('>H', self.table('maxp')[4:6])[0]
        loca = self.table('loca')
        if self.long_loca:
            self.loca = struct.unpack(f'>{self.glyph_count + 1}I', loca[:4 * (self.glyph_count + 1)])
        else:
            self.loca = [2 * v for v in struct.unpack(f'>{self.glyph_count + 1}H', loca[:2 * (self.glyph_count + 1)])]
        self.cmap = self.read_cmap()

    def table(self, tag):
        offset, length = self.tables[tag]
        return self.data[offset:offset + length]

    def read_cmap(self):
        cmap = self.table('cmap')
        count = struct.unpack('>H', cmap[2:4])[0]
        for i in range(count):
            platform, encoding, offset = struct.unpack('>HHI', cmap[4 + 8 * i:12 + 8 * i])
            if (platform, encoding) in ((3, 1), (0, 3)) and struct.unpack('>H', cmap[offset:offset + 2])[0] == 4:
                return self.format4(cmap, offset)
        raise ValueError('no Unicode BMP cmap of format 4')

    @staticmethod
    def format4(cmap, offset):
        segs = struct.unpack('>H', cmap[offset + 6:offset + 8])[0] // 2
        def array(start):
            return struct.unpack(f'>{segs}H', cmap[start:start + 2 * segs])
        ends = array(offset + 14)
        starts = array(offset + 16 + 2 * segs)
        deltas = array(offset + 16 + 4 * segs)
        range_base = offset + 16 + 6 * segs
        ranges = array(range_base)
        def lookup(code):
            for i in range(segs):
                if starts[i] <= code <= ends[i]:
                    if ranges[i] == 0:
                        return (code + deltas[i]) & 0xFFFF
                    at = range_base + 2 * i + ranges[i] + 2 * (code - starts[i])
                    glyph = struct.unpack('>H', cmap[at:at + 2])[0]
                    return (glyph + deltas[i]) & 0xFFFF if glyph else 0
            return 0
        return lookup

    def advance(self, glyph):
        hmtx = self.table('hmtx')
        index = min(glyph, self.metrics - 1)
        return struct.unpack('>H', hmtx[4 * index:4 * index + 2])[0]

    def contours(self, glyph, depth=0):
        """Contours of a glyph: lists of (x, y, on-curve) in font units; composite glyphs resolved (offsets and scales)."""
        if depth > 8:
            raise ValueError('composite glyph nesting too deep')
        glyf = self.table('glyf')
        start, end = self.loca[glyph], self.loca[glyph + 1]
        if start == end:
            return []
        data = glyf[start:end]
        count = struct.unpack('>h', data[:2])[0]
        if count >= 0:
            return self.simple(data, count)
        return self.composite(data, depth)

    @staticmethod
    def simple(data, count):
        ends = struct.unpack(f'>{count}H', data[10:10 + 2 * count])
        points = ends[-1] + 1 if count else 0
        pos = 10 + 2 * count
        pos += 2 + struct.unpack('>H', data[pos:pos + 2])[0]   # skip instructions
        flags = []
        while len(flags) < points:
            flag = data[pos]
            pos += 1
            flags.append(flag)
            if flag & 8:
                flags += [flag] * data[pos]
                pos += 1
        def coords(short, same):
            nonlocal pos
            values, value = [], 0
            for flag in flags:
                if flag & short:
                    delta = data[pos]
                    pos += 1
                    value += delta if flag & same else -delta
                elif not flag & same:
                    value += struct.unpack('>h', data[pos:pos + 2])[0]
                    pos += 2
                values.append(value)
            return values
        xs = coords(2, 16)
        ys = coords(4, 32)
        out, first = [], 0
        for last in ends:
            out.append([(xs[i], ys[i], flags[i] & 1) for i in range(first, last + 1)])
            first = last + 1
        return out

    def composite(self, data, depth):
        pos, out = 10, []
        while True:
            flags, glyph = struct.unpack('>HH', data[pos:pos + 4])
            pos += 4
            if flags & 1:
                a, b = struct.unpack('>hh', data[pos:pos + 4])
                pos += 4
            else:
                a, b = struct.unpack('>bb', data[pos:pos + 2])
                pos += 2
            if not flags & 2:
                raise ValueError('composite glyph positioned by point matching is not supported')
            xx, xy, yx, yy = 1.0, 0.0, 0.0, 1.0
            if flags & 8:
                xx = yy = struct.unpack('>h', data[pos:pos + 2])[0] / 16384
                pos += 2
            elif flags & 0x40:
                xx, yy = (v / 16384 for v in struct.unpack('>hh', data[pos:pos + 4]))
                pos += 4
            elif flags & 0x80:
                xx, xy, yx, yy = (v / 16384 for v in struct.unpack('>hhhh', data[pos:pos + 8]))
                pos += 8
            for contour in self.contours(glyph, depth + 1):
                out.append([(round(xx * x + yx * y) + a, round(xy * x + yy * y) + b, on) for x, y, on in contour])
            if not flags & 0x20:
                return out


def extract(path):
    data = Path(path).read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    style = SOURCES.get(digest)
    if style is None:
        raise SystemExit(f'unexpected font file (sha256 {digest}); the subsets are pinned to M PLUS Rounded 1c Bold and Regular')
    font = Font(data)
    glyphs, notes = {}, []
    for char in CHARS:
        glyph = font.cmap(ord(char))
        if not glyph:
            raise SystemExit(f'the font has no glyph for U+{ord(char):04X}')
        contours = font.contours(glyph)
        glyphs[char] = {'advance': font.advance(glyph),
                        'contours': [[v for x, y, on in contour for v in (x, y, on)] for contour in contours]}
    dot = glyphs['.']
    ellipsis = font.contours(font.cmap(0x2026))
    low = min((y for contour in ellipsis for _, y, _ in contour), default=0)
    if low > font.units_per_em // 10:   # drawn above the baseline (centred on the em): build it from three periods
        glyphs['…'] = {'advance': 3 * dot['advance'],
                       'contours': [[v + (k * dot['advance'] if i % 3 == 0 else 0) for i, v in enumerate(contour)]
                                    for k in range(3) for contour in dot['contours']]}
        notes.append('U+2026 is built from three U+002E glyphs on the baseline (the font centres its own on the em)')
    return {'schema': 'gcam.glyph-subset', 'schemaVersion': 1,
            'font': {'family': 'M PLUS Rounded 1c', 'style': style, 'license': 'SIL Open Font License 1.1 (OFL.txt)',
                     'copyright': 'Copyright 2016 The Rounded M+ Project Authors.',
                     'sourceFile': f'MPLUSRounded1c-{style}.ttf', 'sourceSha256': digest},
            'subset': 'outlines of the listed characters only (printable ASCII, '
                      + ', '.join(f'U+{ord(c):04X}' for c in sorted(EXTRA)) + '); '
                      'contours are flat [x, y, onCurve, ...] in font units, y up, quadratic TrueType outlines',
            'notes': notes, 'unitsPerEm': font.units_per_em, 'ascender': font.ascender, 'descender': font.descender,
            'glyphs': glyphs}


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('font', type=Path)
    parser.add_argument('--out', type=Path)
    args = parser.parse_args()
    subset = extract(args.font)
    args.out = args.out or HERE / f'mplus-rounded-1c-{subset["font"]["style"].lower()}.glyphs.json'
    glyphs = subset.pop('glyphs')
    head = json.dumps(subset, ensure_ascii=False, sort_keys=True, indent=1)[:-2]   # one glyph per line: reviewable diffs
    rows = [json.dumps(c, ensure_ascii=False) + ':' + json.dumps(g, sort_keys=True, separators=(',', ':'))
            for c, g in sorted(glyphs.items())]
    text = head + ',\n "glyphs": {\n' + ',\n'.join(rows) + '\n }\n}\n'
    json.loads(text)
    args.out.write_bytes(text.encode('utf-8'))
    print(f'{args.out.name}: {len(glyphs)} glyphs, {args.out.stat().st_size} bytes')


if __name__ == '__main__':
    main()
