"""GCAM Studio manual editions: assemble, replay and self-test (standard library only).

An edition is assembled from the chapter registry, the static chapters, the Studio notice list and manual run records
(written by the desktop manual scenarios after cleanup). A generated chapter is printed only when the latest run of its
scenario on the edition's one Studio build passed; otherwise the edition carries an omission notice. Figures are the
run's original captures, cropped along panel edges and overlaid with the mark rectangles the runner measured at capture
time; the generator only adds each mark's label beside its rectangle, or in a plain margin beside the figure joined by
a line (a red disc with the number, then the label as measured, wrapped, drawn from a committed glyph subset of a
rounded open-licence font) by a fixed placement rule, and places no mark. A label reads "<screen text>: <purpose>": the
control's text as measured (or a short element name where that text is long or changes with the state), then what the
element is for, both from the hand-written, sourced purpose list (docs/manual/purposes.json); a mark without an entry
keeps its screen text alone and keeps the edition a draft. The generator records; it approves nothing.

  python -B samples/manual/manual_records.py check-registry
  python -B samples/manual/manual_records.py generate --runs <folder> --edition-id <id> --date <yyyy-mm-dd> --out <new>
  python -B samples/manual/manual_records.py check [--edition <folder> | --all]
  python -B samples/manual/manual_records.py self-test [--out <scratch>]
"""
import argparse
import copy
import datetime as dt
import hashlib
import html
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
BORDER = 2      # mark border width, drawn outside the control
PAD = 6         # crop padding into the window background beyond a panel's edge
FRAME = 8       # the window's own outline and rounded corners, ignored when testing a crop edge
HALO = 4        # screen ink is counted this far around a label (its clearance from text)
QUIET = 15      # colour step that counts as ink: text, icons, plots and control frames do (long straight rules are exempt)
RULE_DRIFT = 6  # colour drift between neighbours along one rule (anti-aliasing, shading)
RULE_NEAR = 16  # a pixel this close in shade to an adjoining rule is part of it (rounded corners, fringes)
RULE_RUN = 200  # a straight run of one grey this long is a panel or dialog border, separator or background, not a control
SATURATED = 60  # colourfulness (max - min channel) that counts as content: plots and accent buttons, not grey chrome
LABEL_H = 24    # label box height: the disc plus a pixel above and below
DISC = 22       # number disc diameter
TEXT_GAP = 5    # disc to label text
BASELINE = 18   # label text baseline below the box top: capitals centred on the disc
EM, DIGIT_EM = 16, 15   # label and disc-number text size, px per em (label text 15.4 px cap to descender)
SS = 4          # text and disc supersampling per axis
OUTLINE_KERNEL = [(dx, dy, w) for dy in range(-2, 3) for dx in range(-2, 3)
                  for w in [{0: 256, 1: 256, 2: 150, 4: 64}.get(dx * dx + dy * dy, 0)] if w]
LEAD = 3        # clearance between a label and its own mark's frame
SPACING = 6     # clearance between two labels
ROW = LABEL_H + 2   # one text row: the step of a shifted candidate
SLIDE = 6       # step of an above / below label slid along its frame
NUDGES = (-4, 4, -8, 8)   # px a left / right label may move off a line of text and still touch its frame
INSET, INSET_V = 4, 2   # an inside label's clearance from the control's edges (sides, top and bottom)
TEXT_INK = 8    # ink pixels near a label box that count as covering screen content (fewer: a stray pixel)
LINE_INK = 6    # ink pixels along a join line that count as crossing screen content (fewer: a separator)
SEPARATORS = ('\u00b7', '\u00d7', '\u2013', '|', '-')
PURPOSES = 'purposes.json'   # what each mark is for, drawn into its label
PURPOSE_LABELS = ('each mark is labelled "<screen text or the entry\'s name>: <short purpose>" from the purpose list; a mark '
                  'without an entry keeps its screen text alone')
WRAPS = (32, 20, 12)   # label line lengths in characters: the label as printed, then narrower variants that may fit
LINE_PITCH = 20 # label text line pitch, px
JOIN_GAP = 12   # clearance of a label shifted off its frame's side, so its join line shows
MARGIN_GAP = 10 # a plain margin's clearance between the capture and its labels (the join lines' run)
BUS = 4         # spacing of the separate rules in a margin whose join lines would otherwise share one
MARGIN_PAD = 8  # a plain margin's clearance beyond its labels
LINE_WEIGHT = 200   # figure area (px²) one pixel of margin join line counts as when choosing the margins
LINE = 2        # join line width, drawn with a 1 px dark halo
STRIP_PAD = 6   # bottom margin's clearance above and below its labels
SEARCH_LIMIT = 200000   # branch-and-bound nodes per figure; past it the best assignment so far wins, flagged
OVERLAY = 'rectangles+labels-beside-v4'
FONT_DATA = Path(__file__).resolve().parent / 'font/mplus-rounded-1c-bold.glyphs.json'
_FONT, _GLYPH_SEGMENTS, _UNITS, _DISCS = {}, {}, {}, {}
RED, WHITE, BLACK = (200, 30, 30), (255, 255, 255), (0, 0, 0)
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


def framed(rect):
    """A mark's rectangle with its border (drawn outside the control)."""
    x, y, w, h = rect
    return [x - BORDER, y - BORDER, w + 2 * BORDER, h + 2 * BORDER]


def overlap(a, b, gap=0):
    return not (a[0] + a[2] + gap <= b[0] or b[0] + b[2] + gap <= a[0] or a[1] + a[3] + gap <= b[1] or b[1] + b[3] + gap <= a[1])


def inside(a, b):
    """Box a lies wholly inside box b."""
    return b[0] <= a[0] and b[1] <= a[1] and a[0] + a[2] <= b[0] + b[2] and a[1] + a[3] <= b[1] + b[3]


def reach(point, box):
    """Squared distance from a point to a box (0 inside it)."""
    dx = max(box[0] - point[0], 0, point[0] - (box[0] + box[2]))
    dy = max(box[1] - point[1], 0, point[1] - (box[1] + box[3]))
    return dx * dx + dy * dy


def crosses(box, rect):
    """A box crosses a mark's frame line: it meets the framed rectangle without lying wholly inside the control."""
    return overlap(box, framed(rect)) and not inside(box, rect)


# Label text: outlines of M PLUS Rounded 1c Bold (SIL OFL 1.1), a committed subset of the characters labels may use,
# rasterised here (quadratic contours flattened, non-zero winding, SS x SS supersampling) in integer arithmetic so the
# pixels are the same on every machine. Glyph coordinates run in "fine" units, fine() per pixel.

def label_font():
    if not _FONT:
        data = FONT_DATA.read_bytes()
        _FONT.update(json.loads(data), sha256=sha(data), path=FONT_DATA.relative_to(REPO).as_posix())
    return _FONT


def fine():
    return 2 * SS * label_font()['unitsPerEm']


def glyph_segments(char, size):
    """A glyph's outline as line segments in fine units, origin on the pen position and baseline, y down."""
    key = (char, size)
    if key not in _GLYPH_SEGMENTS:
        k = size * SS   # half font units → fine units
        segments = []
        def line(a, b):
            segments.append((a[0], a[1], b[0], b[1]))
        for flat in label_font()['glyphs'][char]['contours']:
            points = [(2 * flat[i], 2 * flat[i + 1], flat[i + 2]) for i in range(0, len(flat), 3)]
            start = next((i for i, p in enumerate(points) if p[2]), None)
            if start is None:   # every point off-curve: start on the implied point between the last and the first
                first = ((points[-1][0] + points[0][0]) // 2, (points[-1][1] + points[0][1]) // 2, 1)
                sequence = [first] + points
            else:
                sequence = points[start:] + points[:start]
            def scaled(p):
                return (p[0] * k, -p[1] * k)
            current, control = sequence[0], None
            for point in sequence[1:] + [sequence[0]]:
                if not point[2] and control is not None:   # two off-curve points imply an on-curve one between them
                    point_on = ((control[0] + point[0]) // 2, (control[1] + point[1]) // 2, 1)
                    flatten(current, control, point_on, size, line, scaled)
                    current, control = point_on, point
                elif not point[2]:
                    control = point
                elif control is None:
                    line(scaled(current), scaled(point))
                    current = point
                else:
                    flatten(current, control, point, size, line, scaled)
                    current, control = point, None
        _GLYPH_SEGMENTS[key] = segments
    return _GLYPH_SEGMENTS[key]


def flatten(p0, p1, p2, size, line, scaled):
    """A quadratic Bezier (half font units) as about one line segment per pixel of its control polygon."""
    upem = label_font()['unitsPerEm']
    length = abs(p1[0] - p0[0]) + abs(p1[1] - p0[1]) + abs(p2[0] - p1[0]) + abs(p2[1] - p1[1])
    n = max(2, min(16, 1 + length * size // (2 * upem)))
    k, nn = size * SS, n * n
    previous = scaled(p0)
    for i in range(1, n + 1):
        a, b, c = (n - i) * (n - i), 2 * (n - i) * i, i * i
        point = ((a * p0[0] + b * p1[0] + c * p2[0]) * k // nn, -((a * p0[1] + b * p1[1] + c * p2[1]) * k // nn))
        line(previous, point)
        previous = point


def text_segments(text, size, pen, base):
    """Segments of a text run; pen and base are the start point and baseline in fine units. Returns (segments, end pen)."""
    glyphs, segments = label_font()['glyphs'], []
    for char in text:
        segments += [(x0 + pen, y0 + base, x1 + pen, y1 + base) for x0, y0, x1, y1 in glyph_segments(char, size)]
        pen += glyphs[char]['advance'] * 2 * size * SS
    return segments, pen


def coverage(segments, width, height):
    """Non-zero winding fill of segments (fine units) sampled SS x SS per pixel: alpha 0-255 per pixel, row by row."""
    upem, step = label_font()['unitsPerEm'], 2 * label_font()['unitsPerEm']
    rows = [[] for _ in range(height * SS)]
    for x0, y0, x1, y1 in segments:
        if y0 == y1:
            continue
        lo, hi = min(y0, y1), max(y0, y1)
        for j in range(max(0, -((upem - lo) // step)), min(height * SS, -((upem - hi) // step))):
            y = (2 * j + 1) * upem   # sample row centre
            rows[j].append((x0 + (y - y0) * (x1 - x0) // (y1 - y0), 1 if y1 > y0 else -1))
    counts = [0] * (width * height)
    for j, row in enumerate(rows):
        if not row:
            continue
        row.sort()
        base, winding = (j // SS) * width, 0
        for i in range(len(row) - 1):
            winding += row[i][1]
            if not winding:
                continue
            k, end = max(0, -((upem - row[i][0]) // step)), min(width * SS, -((upem - row[i + 1][0]) // step))
            while k < end:
                stop = min(end, (k // SS + 1) * SS)
                counts[base + k // SS] += stop - k
                k = stop
    return [c * 255 // (SS * SS) for c in counts]


def disc_coverage(width, height):
    """A filled disc (a stadium when wider than tall) filling a width x height box, alpha 0-255."""
    radius, half = height * SS, (width - height) * SS   # doubled sample units
    out = []
    for y in range(height):
        for x in range(width):
            count = 0
            for sy in range(SS):
                dy = 2 * (y * SS + sy) + 1 - height * SS
                for sx in range(SS):
                    dx = max(0, abs(2 * (x * SS + sx) + 1 - width * SS) - half)
                    count += dx * dx + dy * dy <= radius * radius
            out.append(count * 255 // (SS * SS))
    return out


def dilate(alpha, width, height):
    """The text outline: coverage spread about 1.5 px (full weight at distance 1, partial on the diagonals and at 2)."""
    out = [0] * (width * height)
    for y in range(height):
        for x in range(width):
            best = 0
            for dx, dy, weight in OUTLINE_KERNEL:
                u, v = x + dx, y + dy
                if 0 <= u < width and 0 <= v < height:
                    best = max(best, alpha[v * width + u] * weight // 256)
            out[y * width + x] = best
    return out


def ink_extent(segments):
    xs = [v for s in segments for v in (s[0], s[2])]
    ys = [v for s in segments for v in (s[1], s[3])]
    return (min(xs), min(ys), max(xs), max(ys)) if xs else None


def label_text(mark):
    """The label as printed: its first non-empty line (labels are drawn as measured, never reworded)."""
    return next(line.strip() for line in re.split(r'\r\n|\r|\n', mark['label']) if line.strip())


def wrap_text(text, width):
    """Greedy word wrap into lines of at most width characters; a word longer than a line is broken, and a lone
    separator (· × – |) stays at the end of its line rather than opening the next one."""
    lines, current = [], ''
    for word in text.split():
        if current and word in SEPARATORS:
            current += ' ' + word
            continue
        while len(word) > width:
            if current:
                lines.append(current)
                current = ''
            lines.append(word[:width])
            word = word[width:]
        if not current:
            current = word
        elif len(current) + 1 + len(word) <= width:
            current += ' ' + word
        else:
            lines.append(current)
            current = word
    return lines + [current] if current else lines


def label_unit(n, text, wrap=WRAPS[0]):
    """One mark's label: a red disc with the white number, then the label text wrapped at wrap characters (the disc on
    the first line, later lines under the text), white with a black outline. Returns (width, height, disc width,
    layers, lines); layers are (alpha, colour) in compositing order, relative to the label box."""
    key = (n, text, wrap)
    if key in _UNITS:
        return _UNITS[key]
    glyphs = label_font()['glyphs']
    for char in text:
        require(char in glyphs, f'label-glyph: mark {n} label has no glyph for U+{ord(char):04X}')
    lines = wrap_text(text, wrap)
    f, height = fine(), LABEL_H + (len(lines) - 1) * LINE_PITCH
    digits, _ = text_segments(str(n), DIGIT_EM, 0, 0)
    x0, y0, x1, y1 = ink_extent(digits)
    disc = max(DISC, -(-(x1 - x0) // f) + 10)
    disc += (disc - DISC) % 2
    cx, cy = disc * f // 2, LABEL_H * f // 2
    digits = [(a + cx - (x0 + x1) // 2, b + cy - (y0 + y1) // 2, c + cx - (x0 + x1) // 2, d + cy - (y0 + y1) // 2)
              for a, b, c, d in digits]
    segments, right = [], 0
    for k, line in enumerate(lines):
        piece, pen = text_segments(line, EM, (disc + TEXT_GAP) * f, (BASELINE + k * LINE_PITCH) * f)
        segments += piece
        right = max(right, pen)
    ex0, ey0, ex1, ey1 = ink_extent(segments) or (right, BASELINE * f, right, BASELINE * f)
    require(ey0 >= 2 * f and ey1 <= (height - 2) * f and ex0 >= (disc + 2) * f, f'label-clipped: mark {n} label text leaves its box')
    width = -(-max(right, ex1) // f) + 2
    text_alpha = coverage(segments, width, height)
    top, shape = (LABEL_H - DISC) // 2, disc_alpha_cache(disc)
    disc_alpha = [0] * (width * height)
    for y in range(DISC):
        disc_alpha[(top + y) * width:(top + y) * width + disc] = shape[y * disc:(y + 1) * disc]
    digit_alpha = coverage(digits, width, height)
    unit = (width, height, disc, [(dilate(text_alpha, width, height), BLACK), (text_alpha, WHITE), (disc_alpha, RED),
                                  (digit_alpha, WHITE)], lines)
    _UNITS[key] = unit
    return unit


def label_variants(n, text):
    """The label as printed (WRAPS[0]) and each narrower wrapping that changes its lines without making it taller than
    two lines or than the label as printed (a short label may fold once to fit beside its mark; a long one keeps its
    block)."""
    variants = []
    for wrap in WRAPS:
        unit = label_unit(n, text, wrap)
        if not variants or (unit[4] != variants[-1][1][4] and len(unit[4]) <= max(2, len(variants[0][1][4]))):
            variants.append((wrap, unit))
    return variants


def disc_alpha_cache(disc):
    if disc not in _DISCS:
        _DISCS[disc] = disc_coverage(disc, DISC)
    return _DISCS[disc]


def composite(out, width, height, x0, y0, w, h, alpha, colour):
    """Blend one layer into an RGB buffer with integer arithmetic (identical bytes on every machine)."""
    for y in range(max(0, -y0), min(h, height - y0)):
        row, base = y * w, ((y0 + y) * width + x0) * 3
        for x in range(max(0, -x0), min(w, width - x0)):
            a = alpha[row + x]
            if a:
                i = base + 3 * x
                out[i] = (out[i] * (255 - a) + colour[0] * a + 127) // 255
                out[i + 1] = (out[i + 1] * (255 - a) + colour[1] * a + 127) // 255
                out[i + 2] = (out[i + 2] * (255 - a) + colour[2] * a + 127) // 255


# Placement. The crop is the marks' own (panel edges around the marks and the region); labels never widen it. A label
# is a box beside its mark's frame inside the crop; candidates come in a fixed order, nearest first (label_candidates),
# each wrapping of the label in turn. A candidate whose box covers screen content (text, icons, plots, images: more than
# TEXT_INK ink pixels around it), whose join line crosses content (LINE_INK), or which crosses another mark's frame line
# is refused outright; panel borders, separators and gutters may be crossed. The rest are costed in tiers: (1) the disc
# sits nearer another mark's frame than its own, so it would read as that mark's; (2) the label has no place in the crop
# and goes to a plain margin; (3) remaining ink; (4) rank. The cheapest assignment for all marks together wins (a
# deterministic branch and bound); labels never overlap each other or another label's join line. Labels without a place
# go to a plain margin in the window background colour beside the crop (right, left or a bottom strip, the assignment
# adding the least area, long join lines counted as area), level with their marks and joined by lines; marks sharing a margin mark's column (row, for the
# bottom strip) join it, so a stack of fields is labelled in one place.

def join_line(box, disc, rect, route='across'):
    """Right-angled join from a label to its mark's frame, as polyline points; None when the label touches the frame
    (adjacent along at least half a disc) or lies inside the control. The line leaves the label's side that faces the
    frame (at the disc where it can), so it never runs under the label's own text. A label off a corner of the frame
    has two routes: 'across' (along the label's middle, then onto the frame's near edge) or 'down' (out of the label's
    near end, then into the frame's side at its middle)."""
    if inside(box, rect):
        return None
    bl, bt, br, bb = box[0], box[1], box[0] + box[2], box[1] + box[3]
    fl, ft, fr, fb = framed(rect)
    fr, fb = fl + fr, ft + fb
    ox, oy = min(br, fr) - max(bl, fl), min(bb, fb) - max(bt, ft)
    gx, gy = max(0, fl - br, bl - fr), max(0, ft - bb, bt - fb)
    if (ox >= DISC // 2 and gy <= LEAD + 1) or (oy >= DISC // 2 and gx <= LEAD + 1):
        return None
    above, before = bt + bb < ft + fb, bl + br < fl + fr
    if ox > 0:   # one over the other: a vertical line, through the disc when the disc is over the frame
        x = bl + disc // 2
        if not max(bl, fl) + 2 <= x < min(br, fr) - 2:
            x = (max(bl, fl) + min(br, fr)) // 2
        return [[x, bb if above else bt], [x, ft if above else fb]]
    y = (bt + bb) // 2
    if oy > 0:   # side by side: a horizontal line
        y = (max(bt, ft) + min(bb, fb)) // 2
        return [[br if before else bl, y], [fl if before else fr, y]]
    if route == 'down':
        x = br - 6 if before else bl + disc // 2
        ty = (ft + fb) // 2
        return [[x, bb if above else bt], [x, ty], [fl if before else fr, ty]]
    tx = (fl + 6 if before else fr - 6) if fr - fl > 12 else (fl + fr) // 2   # diagonal: along, then onto the frame
    return [[br if before else bl, y], [tx, y], [tx, ft if above else fb]]


def line_boxes(points, grow=0):
    """The rectangles a join polyline paints (LINE px wide), widened by grow px on every side."""
    boxes = []
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        half = LINE // 2
        if y0 == y1:
            boxes.append([min(x0, x1) - half - grow, y0 - half - grow, abs(x1 - x0) + LINE + 2 * grow, LINE + 2 * grow])
        else:
            boxes.append([x0 - half - grow, min(y0, y1) - half - grow, LINE + 2 * grow, abs(y1 - y0) + LINE + 2 * grow])
    return boxes


def label_candidates(rect, unit, crop):
    """(name, box) candidates in rank order, nearest first, inside the crop."""
    w, h, disc = unit[:3]
    x, y, rw, rh = rect
    fx, fy, fw, fh = framed(rect)
    middle = fy + (fh - LABEL_H) // 2   # the first line level with the frame's middle
    out = []
    def add(name, bx, by):
        if crop[0] <= bx and crop[1] <= by and bx + w <= crop[0] + crop[2] and by + h <= crop[1] + crop[3]:
            out.append((name, [bx, by, w, h]))
    def spans(bx):   # an above / below label keeps its disc over its own frame
        return fx <= bx + disc // 2 < fx + fw
    def above_below(k):
        for side, by in (('above', fy - LEAD - h - k * ROW), ('below', fy + fh + LEAD + k * ROW)):
            for align, bx in (('left', fx), ('right', fx + fw - w), ('centre', fx + (fw - w) // 2)):
                if spans(bx):
                    add(f'{side}-{align}' + (f'+{k}row' if k else ''), bx, by)
    add('right', fx + fw + LEAD, middle)
    add('left', fx - LEAD - w, middle)
    above_below(0)
    for nudge in NUDGES:   # beside the frame, nudged off a line of text without losing touch
        for side, bx in (('right', fx + fw + LEAD), ('left', fx - LEAD - w)):
            add(f'{side}{nudge:+d}', bx, middle + nudge)
    for side, by in (('above', fy - LEAD - h), ('below', fy + fh + LEAD)):
        slides = sorted(range(fx - disc // 2, fx + fw - disc // 2, SLIDE), key=lambda v: (abs(v - fx), v))
        for bx in slides:
            if bx not in (fx, fx + fw - w, fx + (fw - w) // 2):
                add(f'{side}-slide{bx - fx:+d}', bx, by)
    if rw >= max(2 * w, w + 2 * INSET) and rh >= h + 2 * INSET_V:   # a frame clearly larger than the label: inside it
        top, bottom, right = y + INSET_V, y + rh - INSET_V - h, x + rw - INSET - w
        add('inside-top-right', right, top)
        add('inside-bottom-right', right, bottom)
        add('inside-bottom-left', x + INSET, bottom)
        centre = x + (rw - w) // 2
        for bx in sorted(range(x + INSET, right, SLIDE), key=lambda v: (abs(v - centre), v)):   # then along it, middle first
            add(f'inside-slide{bx - centre:+d}', bx, y + (rh - h) // 2)
    for k in (1, 2):   # off the frame's side by a row or two, far enough out for the join line to show
        for side, bx in (('right', fx + fw + JOIN_GAP), ('left', fx - JOIN_GAP - w)):
            add(f'{side}-{k}up', bx, middle - k * ROW)
            add(f'{side}-{k}down', bx, middle + k * ROW)
        above_below(k)
    above_below(3)
    return out


def ink_counter(width, height, rgb, area):
    """Counts screen content in a box (widened by HALO): pixels that are strongly coloured (plots, images, accent
    buttons) or that step by more than QUIET in any colour channel to the next pixel right or below (text, icons, plot
    cells) - except grey pixels lying on a straight run of RULE_RUN or more pixels of their own colour, which are panel
    and dialog borders, separators and the background beside them."""
    ax0, ay0 = max(0, area[0]), max(0, area[1])
    ax1, ay1 = min(width, area[0] + area[2]), min(height, area[1] + area[3])
    w = max(0, ax1 - ax0)
    def colours(y):
        row = rgb[(y * width + ax0) * 3:(y * width + ax1) * 3]
        return [(row[i] << 16) | (row[i + 1] << 8) | row[i + 2] for i in range(0, 3 * w, 3)]
    grid = [colours(y) for y in range(ay0, min(height, ay1 + 1))]
    rows = ay1 - ay0
    def runs(line):   # length of the run each pixel lies on; a run continues while neighbours differ by at most RULE_DRIFT
        out, start = [0] * len(line), 0
        for i in range(1, len(line) + 1):
            if i == len(line) or near(line[i], line[i - 1]) is False:
                out[start:i] = [i - start] * (i - start)
                start = i
        return out
    def near(a, b):
        return a == b or (abs((a >> 16) - (b >> 16)) <= RULE_DRIFT and abs((a >> 8 & 255) - (b >> 8 & 255)) <= RULE_DRIFT
                          and abs((a & 255) - (b & 255)) <= RULE_DRIFT)
    across = [runs(line) for line in grid[:rows]]
    down = [[0] * w for _ in range(rows)]
    for x in range(w):
        column = runs([grid[y][x] for y in range(rows)])
        for y in range(rows):
            down[y][x] = column[y]
    def step(a, b):
        return abs((a >> 16) - (b >> 16)) > QUIET or abs((a >> 8 & 255) - (b >> 8 & 255)) > QUIET or abs((a & 255) - (b & 255)) > QUIET
    def beside_rule(x, y, c):   # a rounded corner or an anti-aliased fringe of a rule: next to a rule pixel of its shade
        for v in range(max(0, y - 1), min(rows, y + 2)):
            for u in range(max(0, x - 1), min(w, x + 2)):
                if (across[v][u] >= RULE_RUN or down[v][u] >= RULE_RUN) and not step_near(c, grid[v][u]):
                    return True
        return False
    def step_near(a, b):
        return abs((a >> 16) - (b >> 16)) > RULE_NEAR or abs((a >> 8 & 255) - (b >> 8 & 255)) > RULE_NEAR or abs((a & 255) - (b & 255)) > RULE_NEAR
    table = [[0] * (w + 1)]
    for y in range(rows):
        line, below, acc, previous, sums = grid[y], grid[y + 1] if y + 1 < len(grid) else grid[y], 0, table[-1], [0]
        for x in range(w):
            c = line[x]
            r, g, b = c >> 16, c >> 8 & 255, c & 255
            if max(r, g, b) - min(r, g, b) > SATURATED:
                acc += 1
            elif ((step(c, line[x + 1 if x + 1 < w else x]) or step(c, below[x])) and across[y][x] < RULE_RUN
                    and down[y][x] < RULE_RUN and not beside_rule(x, y, c)):
                acc += 1
            sums.append(previous[x + 1] + acc)
        table.append(sums)
    def count(box, halo=HALO):
        x0, y0 = max(ax0, box[0] - halo) - ax0, max(ay0, box[1] - halo) - ay0
        x1, y1 = min(ax1, box[0] + box[2] + halo) - ax0, min(ay1, box[1] + box[3] + halo) - ay0
        if x1 <= x0 or y1 <= y0:
            return 0
        return table[y1][x1] - table[y0][x1] - table[y1][x0] + table[y0][x0]
    return count


def place_labels(width, height, rgb, marks, variants, crop, ink, routes):
    """The labels' places inside the crop. Returns one row per mark: a placed candidate (name, box, unit, join line,
    costs) or a margin row (name 'margin', no box) for a mark the crop has no acceptable place for."""
    rects = [m['image'] for m in marks]
    def ink_of(join):
        return sum(ink(b, 1) for b in line_boxes(join)) if join else 0   # a thin line: crossing a separator is not content
    costed = []
    for index, options in enumerate(variants):
        own, others, rows, rank = framed(rects[index]), [r for j, r in enumerate(rects) if j != index], [], 0
        for wrap, unit in options:
            for name, box in label_candidates(rects[index], unit, crop):
                rank += 1
                covered = ink(box)
                if covered > TEXT_INK or any(crosses(box, r) for r in others):
                    continue
                route = min((ink_of(join_line(box, unit[2], rects[index], r)), k, r) for k, r in enumerate(('across', 'down')))[2]
                join = join_line(box, unit[2], rects[index], route)
                lines = line_boxes(join) if join else []
                if ink_of(join) > LINE_INK or any(crosses(b, r) for b in lines for r in others):
                    continue
                centre = (box[0] + unit[2] // 2, box[1] + LABEL_H // 2)
                ambiguous = any(reach(centre, framed(r)) < reach(centre, own) for r in others)
                rows.append({'cost': (int(ambiguous), 0, covered, rank), 'name': name if wrap == WRAPS[0] else f'{name}/wrap{wrap}',
                             'box': box, 'unit': unit, 'join': join, 'route': route, 'lines': line_boxes(join, 1) if join else [],
                             'ambiguous': ambiguous, 'ink': covered})
        if routes[index]:   # a margin needs a clean route out of the crop
            rows.append({'cost': (0, 1, 0, min(length(r) for _, r in routes[index])), 'name': 'margin', 'box': None, 'unit': options[0][1], 'join': None,
                         'route': None, 'lines': [], 'routes': [line_boxes(r, 1) for _, r in routes[index]],
                         'ambiguous': False, 'ink': 0})
        require(rows, f'label-no-room: mark {marks[index]["n"]} has no place in the figure or a margin beside it')
        rows.sort(key=lambda r: (r['cost'], r['box'] or []))
        costed.append(rows)
    order = sorted(range(len(costed)), key=lambda i: (len(costed[i]), i))   # fewest places first: conflicts surface early
    costed = [costed[i] for i in order]
    zero = (0,) * 4
    def add(a, b):
        return tuple(u + v for u, v in zip(a, b))
    lower = [zero] * (len(costed) + 1)
    for i in range(len(costed) - 1, -1, -1):
        lower[i] = add(lower[i + 1], costed[i][0]['cost'])
    best, chosen, nodes = [None, None], [], [0]
    def clear(row):
        for other in chosen:
            if row['box'] is None and other['box'] is None:
                continue
            if row['box'] is None or other['box'] is None:   # a margin mark keeps at least one route clear of the labels
                margin, placed = (row, other) if row['box'] is None else (other, row)
                boxes = [placed['box']] + placed['lines']
                if not any(all(not overlap(a, b) for a in route for b in boxes) for route in margin['routes']):
                    return False
                continue
            if overlap(row['box'], other['box'], SPACING):
                return False
            if any(overlap(row['box'], b) for b in other['lines']) or any(overlap(other['box'], b) for b in row['lines']):
                return False
            if any(overlap(a, b) for a in row['lines'] for b in other['lines']):
                return False
        return True
    def search(i, cost):
        if i == len(costed):
            best[0], best[1] = cost, list(chosen)
            return
        for row in costed[i]:
            if best[0] is not None and add(add(cost, row['cost']), lower[i + 1]) >= best[0]:
                return
            nodes[0] += 1
            if nodes[0] > SEARCH_LIMIT:
                return
            if clear(row):
                chosen.append(row)
                search(i + 1, add(cost, row['cost']))
                chosen.pop()
    search(0, zero)
    require(best[1] is not None, 'label-overlap: the labels have no places clear of each other')
    result = [None] * len(order)
    for slot, i in enumerate(order):
        result[i] = best[1][slot]
    return result, nodes[0] <= SEARCH_LIMIT


def length(points):
    return sum(abs(x1 - x0) + abs(y1 - y0) for (x0, y0), (x1, y1) in zip(points, points[1:]))


def margin_routes(index, rects, crop, ink, taken=None):
    """Join-line routes inside the crop from a mark's frame out to each margin, clear of screen content, of the other
    marks' frames and of taken[side] (the earlier marks' routes to that margin): straight out from the frame's middle, or, when that
    crosses something, one detour - along the frame's side to the nearest clean corridor, then out along it. Returns
    [(side, points)], at most one route per side; a corridor route starts on the frame."""
    frame = framed(rects[index])
    fl, ft, fr, fb = frame[0], frame[1], frame[0] + frame[2], frame[1] + frame[3]
    cl, ct, cr, cb = crop[0], crop[1], crop[0] + crop[2], crop[1] + crop[3]
    others = [r for j, r in enumerate(rects) if j != index]
    def clean(points, side):
        boxes = line_boxes(points)
        return (all(b[0] >= cl - LINE and b[1] >= ct - LINE and b[0] + b[2] <= cr + LINE and b[1] + b[3] <= cb + LINE for b in boxes)
                and sum(ink(b, 1) for b in boxes) <= LINE_INK and not any(crosses(b, r) for b in boxes for r in others)
                and not any(overlap(b, t) for b in boxes for t in (taken or {}).get(side, ())))
    def first(candidates, side):
        return next((c for c in candidates if clean(c, side)), None)
    mx, my, routes = (fl + fr) // 2, (ft + fb) // 2, []
    for side, edge, out in (('right', fr, cr), ('left', fl, cl)):
        def corridor(step, start, stop):
            return [[[mx, start], [mx, y], [out, y]] for y in range(start, stop, step)]
        # last resort: straight out from another height of the frame's side, nearest its middle first (past a caption
        # centred beside a field)
        level = [[[edge, y], [out, y]] for y in sorted(range(ft + 2, fb - 1, 2), key=lambda v: (abs(v - my), v)) if y != my]
        route = first([[[edge, my], [out, my]]] + corridor(-2, ft - 6, ct + 2) + corridor(2, fb + 6, cb - 2) + level, side)
        if route and route[0] in ([mx, ft - 6], [mx, fb + 6]):   # a corridor's line runs on to the frame it leaves
            route = [[mx, ft if route[0][1] < ft else fb]] + route[1:]
        if route:
            routes.append((side, route))
    xs = [fl + DISC // 2 + 1, mx, fr - DISC // 2 - 1]
    route = first([[[x, fb], [x, cb]] for x in xs] + [[[fr, my], [x, my], [x, cb]] for x in range(fr + 6, cr - 2, 2)]
                  + [[[fl, my], [x, my], [x, cb]] for x in range(fl - 6, cl + 2, -2)], 'bottom')
    if route:
        routes.append(('bottom', route))
    return routes


def margin_layout(side, items, crop):
    """Positions of the labels in one margin, in capture coordinates (the margin lies outside the crop), with their
    join lines. items: (index, route, unit), the route ending on the crop's edge. Returns (margin size, {index: (box,
    join)}) or None when they do not fit. Side margins stack the labels level with their routes; the bottom margin lays
    them out in rows, shifting a label along its row (the line bends to it) before opening another row."""
    cl, ct, cr, cb = crop[0], crop[1], crop[0] + crop[2], crop[1] + crop[3]
    out = {}
    if side in ('right', 'left'):
        width = max(unit[0] for _, _, unit in items)
        placed, bottom = [], ct + 2 - SPACING
        for index, route, unit in sorted(items, key=lambda it: (it[1][-1][1], it[0])):
            y = max(route[-1][1] - LABEL_H // 2, bottom + SPACING)
            placed.append([index, route, unit, y])
            bottom = y + unit[1]
        limit = cb - 2
        for item in reversed(placed):   # pushed back up from the crop's bottom edge when the stack runs over
            item[3] = min(item[3], limit - item[2][1])
            limit = item[3] - SPACING
        if placed and placed[0][3] < ct + 2:
            return None
        # Lines that step from their route to their label run down one shared rule beside the crop, unless two such
        # steps overlap: then each gets its own rule, the upper label's farther out, so no line crosses another.
        steps = [(item[1][-1][1], item[3] + LABEL_H // 2) for item in placed if item[3] + LABEL_H // 2 != item[1][-1][1]]
        nested = any(min(a[1], b[1]) + SPACING > max(a[0], b[0]) for k, a in enumerate(steps) for b in steps[k + 1:])
        gap = max(MARGIN_GAP, 6 + BUS * len(steps)) if nested else MARGIN_GAP
        rank = 0
        for index, route, unit, y in placed:
            x = cr + gap if side == 'right' else cl - gap - unit[0]
            ly, near = y + LABEL_H // 2, x if side == 'right' else x + unit[0]
            xm = cr + MARGIN_GAP // 2 if side == 'right' else cl - MARGIN_GAP // 2
            end = route[-1]
            if nested and ly != end[1]:
                offset = 3 + BUS * (len(steps) - 1 - rank)
                xm, rank = (cr + offset if side == 'right' else cl - offset), rank + 1
            join = route[:-1] + ([[near, ly]] if ly == end[1] else [[xm, end[1]], [xm, ly], [near, ly]])
            out[index] = ([x, y, unit[0], unit[1]], join)
        if nested and any(overlap(a, b) for i, (_, ja) in out.items() for j, (_, jb) in out.items() if i < j
                          for a in line_boxes(ja) for b in line_boxes(jb)):
            return None
        return gap + width + MARGIN_PAD, out
    rows = []
    row_h = max(unit[1] for _, _, unit in items)
    for index, route, unit in sorted(items, key=lambda it: (it[1][-1][0], it[0])):
        xv = route[-1][0]
        want = min(max(xv - unit[2] // 2, cl + 2), cr - 2 - unit[0])
        if want < cl + 2:
            return None
        for r, row in enumerate(rows + [[]]):
            x = max([want] + [b[0] + b[2] + SPACING for b in row])
            if x + unit[0] <= cr - 2 and x - want <= 2 * DISC:
                break
        if r == len(rows):
            rows.append([])
        y = cb + STRIP_PAD + r * (row_h + SPACING)
        box = [x, y, unit[0], unit[1]]
        rows[r].append(box)
        dx = x + unit[2] // 2
        join = route[:-1] + ([[xv, y]] if dx == xv else [[xv, cb + STRIP_PAD // 2], [dx, cb + STRIP_PAD // 2], [dx, y]])
        out[index] = (box, join)
    for index, (box, join) in out.items():   # a lower row's line must pass clear of the labels above it
        if any(overlap(b, other[0]) for b in line_boxes(join) for j, other in out.items() if j != index):
            return None
    return 2 * STRIP_PAD + len(rows) * row_h + (len(rows) - 1) * SPACING, out


def place_margins(marks, rows, routes, crop, printed=None):
    """Margins for the marks the crop has no place for: each such label to the right, left or bottom margin whose route
    is clear of the placed labels, the assignment adding the least figure area (a long join line counting as area too,
    LINE_WEIGHT per pixel); then marks sharing a margin mark's column
    (bottom: row) join it where their routes are clear too. Returns ({side: size}, {index: (side, box, join)})."""
    rects = [m['image'] for m in marks]
    def clear(index, route, keep):
        boxes = line_boxes(route)
        return not any(overlap(b, rows[j]['box']) or any(overlap(b, l) for l in rows[j]['lines'])
                       for b in boxes for j in keep if j != index)
    def sides_for(index, keep):
        return [(side, route) for side, route in routes[index] if clear(index, route, keep)]
    homeless = [i for i, row in enumerate(rows) if row['box'] is None]
    if not homeless:
        return {}, {}
    keep = {i for i, row in enumerate(rows) if row['box'] is not None}
    options = {}
    for i in homeless:
        options[i] = sides_for(i, keep)
        require(options[i], f'label-no-room: mark {marks[i]["n"]} has no place in the figure or a margin beside it')
    def area(assign):
        sizes, layout = {}, {}
        for side in ('right', 'left', 'bottom'):
            items = [(i, route, rows[i]['unit']) for i, (s, route) in assign.items() if s == side]
            if items:
                result = margin_layout(side, items, crop)
                if result is None:
                    return None
                sizes[side] = result[0]
                layout.update({i: (side,) + placed for i, placed in result[1].items()})
        width = crop[2] + sizes.get('right', 0) + sizes.get('left', 0)
        lines = sum(length(join) for _, _, join in layout.values())
        return ((sizes.get('right', 0) + sizes.get('left', 0)) * crop[3] + sizes.get('bottom', 0) * width + LINE_WEIGHT * lines,
                sizes, layout)
    best, chosen = None, None
    def walk(k, assign):
        nonlocal best, chosen
        if k == len(homeless):
            result = area(assign)
            if result and (best is None or result[0] < best[0]):
                best, chosen = result, dict(assign)
            return
        for option in options[homeless[k]]:
            assign[homeless[k]] = option
            walk(k + 1, assign)
            del assign[homeless[k]]
    if len(homeless) <= 6:
        walk(0, {})
    else:   # many marks: each to its own first side, in order
        chosen = {i: options[i][0] for i in homeless}
        best = area(chosen)
    require(best is not None, 'label-no-room: the margin labels do not fit beside the figure')
    for i, row in enumerate(rows):   # the rest of a margin mark's column (bottom: row) joins it
        if row['box'] is None or i in chosen:
            continue
        frame = framed(rects[i])
        for j, (side, _) in list(chosen.items()):
            other = framed(rects[j])
            same = (other[0], other[2]) == (frame[0], frame[2]) if side != 'bottom' else (other[1], other[3]) == (frame[1], frame[3])
            option = next((o for o in sides_for(i, keep - {i}) if o[0] == side), None) if same and i not in chosen else None
            if option:
                trial, placed = {**chosen, i: option}, rows[i]
                if printed:   # in the margin the label keeps its block as printed, not a narrower fold for the crop
                    rows[i] = {**placed, 'unit': printed[i]}
                result = area(trial)
                if result:
                    chosen, best = trial, result
                    keep.discard(i)
                else:
                    rows[i] = placed
    return best[1], best[2]


def check_labels(rects, labels, width, height):
    """Independent post-condition of the placement, in figure coordinates: every label and join line inside the figure,
    no label on its own frame (lying wholly inside its control is an inside corner), labels clear of each other and of
    each other's join lines. Returns the marks whose label or join line crosses another mark's frame line."""
    crossings = []
    for i, label in enumerate(labels):
        n, box = i + 1, label['box']
        paint = [box] + (line_boxes(label['join'], 1) if label['join'] else [])
        for b in paint:
            require(b[0] >= 0 and b[1] >= 0 and b[0] + b[2] <= width and b[1] + b[3] <= height, f'label-bounds: label {n} outside the figure')
        require(not crosses(box, rects[i]), f'label-overlap: label {n} covers its own target')
        for j, other in enumerate(labels):
            if j != i:
                require(not overlap(box, other['box']), f'label-overlap: label {n} covers label {j + 1}')
                require(not any(overlap(box, b) for b in (line_boxes(other['join']) if other['join'] else [])),
                        f'label-overlap: label {n} covers the join line of label {j + 1}')
        if any(crosses(b, r) for j, r in enumerate(rects) if j != i for b in [box] + (line_boxes(label['join']) if label['join'] else [])):
            crossings.append(n)
    return crossings


def background(width, height, rgb):
    """The window background: the commonest colour of the window's outer columns (the gutters beside the panels)."""
    counts = {}
    for x in sorted({min(2, width - 1), max(0, width - 3)}):
        for y in range(height):
            colour = rgb[(y * width + x) * 3:(y * width + x) * 3 + 3]
            counts[colour] = counts.get(colour, 0) + 1
    return max(sorted(counts), key=lambda c: counts[c])


def crop_rect(doc, width, height, rgb, extra, cache=None):
    """'full', or the smallest box that holds every framed mark, the region and every extra box (the labels) and whose
    edges run along the window background (the gutter between panels) or the window's edge, so no panel, heading or
    word is cut; then padded up to PAD px into the background. Grows to the whole window when no such edge exists.
    cache (a dict) keeps the background and per-column tables between calls on one capture."""
    if doc.get('crop') == 'full':
        return [0, 0, width, height], 'full'
    boxes = [framed(m['image']) for m in doc['marks']] + [list(b) for b in extra]
    if doc.get('region'):
        boxes.append(doc['region']['image'])
    if not boxes:
        return [0, 0, width, height], 'full'
    cache = {} if cache is None else cache
    if 'bg' not in cache:
        cache['bg'], cache['cols'] = background(width, height, rgb), {}
    bg, cols = cache['bg'], cache['cols']
    def clean_row(y, l, r):
        l, r = max(l, FRAME), min(r, width - FRAME)
        return rgb[(y * width + l) * 3:(y * width + r) * 3] == bg * max(0, r - l)
    def clean_col(x, t, b):
        t, b = max(t, FRAME), min(b, height - FRAME)
        if x not in cols:   # running count of pixels that are not window background, down the column
            counts, total = [0], 0
            for y in range(height):
                total += rgb[(y * width + x) * 3:(y * width + x) * 3 + 3] != bg
                counts.append(total)
            cols[x] = counts
        return t >= b or cols[x][b] == cols[x][t]
    l = max(0, min(box[0] for box in boxes))
    t = max(0, min(box[1] for box in boxes))
    r = min(width, max(box[0] + box[2] for box in boxes))
    b = min(height, max(box[1] + box[3] for box in boxes))
    while True:
        before = (l, t, r, b)
        while l > 0 and not clean_col(l, t, b):
            l -= 1
        while r < width and not clean_col(r - 1, t, b):
            r += 1
        while t > 0 and not clean_row(t, l, r):
            t -= 1
        while b < height and not clean_row(b - 1, l, r):
            b += 1
        if (l, t, r, b) == before:
            break
    for _ in range(PAD):
        l -= 1 if l > 0 and clean_col(l - 1, t, b) else 0
        r += 1 if r < width and clean_col(r, t, b) else 0
        t -= 1 if t > 0 and clean_row(t - 1, l, r) else 0
        b += 1 if b < height and clean_row(b, l, r) else 0
    if (l, t, r, b) == (0, 0, width, height):
        return [0, 0, width, height], 'full'
    return [l, t, r - l, b - t], 'panel-edges'


def derive_figure(width, height, rgb, doc):
    """Crop chosen, labels placed (inside the crop or in a plain margin beside it), mark rectangles, join lines and
    labels drawn. Returns (w, h, rgb, crop, flags, operation)."""
    marks = doc['marks']
    variants = [label_variants(m['n'], label_text(m)) for m in marks]
    (cx, cy, cw, ch), rule = crop_rect(doc, width, height, rgb, [])
    ink = ink_counter(width, height, rgb, [cx, cy, cw, ch])
    rects = [m['image'] for m in marks]
    routes, taken = [], {}
    for i in range(len(marks)):   # each mark's side routes clear of the earlier marks' to that side (else as if alone)
        found = margin_routes(i, rects, [cx, cy, cw, ch], ink, taken) or margin_routes(i, rects, [cx, cy, cw, ch], ink)
        routes.append(found)
        for side, points in found:   # side margins only: a bottom route runs straight down to its own label
            if side != 'bottom':
                taken.setdefault(side, []).extend(line_boxes(points, 2))
    rows, complete = place_labels(width, height, rgb, marks, variants, [cx, cy, cw, ch], ink, routes)
    sizes, margin = place_margins(marks, rows, routes, [cx, cy, cw, ch], [v[0][1] for v in variants])
    lw, rw, bh = sizes.get('left', 0), sizes.get('right', 0), sizes.get('bottom', 0)
    fw, fh = lw + cw + rw, ch + bh
    bg = bytes(background(width, height, rgb))
    stride = width * 3
    out = bytearray(b''.join(bg * lw + rgb[(cy + y) * stride + cx * 3:(cy + y) * stride + (cx + cw) * 3] + bg * rw
                             for y in range(ch)) + bg * (fw * bh))
    dx, dy = lw - cx, -cy   # capture → figure coordinates
    def moved(points):
        return [[x + dx, y + dy] for x, y in points]
    rects = [[m['image'][0] + dx, m['image'][1] + dy, m['image'][2], m['image'][3]] for m in marks]
    placed = []
    for index, (rect, row) in enumerate(zip(rects, rows)):
        unit = row['unit']
        if index in margin:
            side, box, join = margin[index]
            placed.append({'box': [box[0] + dx, box[1] + dy, box[2], box[3]], 'join': moved(join), 'name': f'margin-{side}',
                           'ink': 0, 'ambiguous': False, 'unit': unit})
        else:
            box = [row['box'][0] + dx, row['box'][1] + dy, unit[0], unit[1]]
            placed.append({'box': box, 'join': join_line(box, unit[2], rect, row['route']), 'name': row['name'],
                           'ink': row['ink'], 'ambiguous': row['ambiguous'], 'unit': unit})
    crossings = check_labels(rects, placed, fw, fh)
    def fill(x0, y0, x1, y1, colour):
        for yy in range(max(0, y0), min(fh, y1)):
            for xx in range(max(0, x0), min(fw, x1)):
                out[(yy * fw + xx) * 3:(yy * fw + xx) * 3 + 3] = bytes(colour)
    for left, top, w, h in rects:
        for t in range(BORDER):   # border outside the control
            fill(left - 2 + t, top - 2 + t, left + w + 2 - t, top - 1 + t, RED)
            fill(left - 2 + t, top + h + 1 - t, left + w + 2 - t, top + h + 2 - t, RED)
            fill(left - 2 + t, top - 2 + t, left - 1 + t, top + h + 2 - t, RED)
            fill(left + w + 1 - t, top - 2 + t, left + w + 2 - t, top + h + 2 - t, RED)
    for label in placed:   # join lines: a 1 px dark halo, then the red line
        if label['join']:
            for x0, y0, w, h in line_boxes(label['join'], 1):
                fill(x0, y0, x0 + w, y0 + h, BLACK)
    for label in placed:
        if label['join']:
            for x0, y0, w, h in line_boxes(label['join']):
                fill(x0, y0, x0 + w, y0 + h, RED)
    for label in placed:
        x0, y0, w, h = label['box']
        for alpha, colour in label['unit'][3]:
            composite(out, fw, fh, x0, y0, w, h, alpha, colour)
    flags = [f'label-crosses-mark:{n}' for n in crossings] + ([] if complete else ['label-search-cut'])
    font = label_font()
    operation = {'crop': [cx, cy, cw, ch], 'cropRule': rule, 'overlay': OVERLAY, 'size': [fw, fh],
                 'font': {'path': font['path'], 'sha256': font['sha256'], 'emPx': EM, 'digitEmPx': DIGIT_EM},
                 'margins': {'left': lw, 'right': rw, 'bottom': bh}, 'imageAt': [lw, 0],
                 'labels': [{'n': m['n'], 'text': label_text(m), 'lines': p['unit'][4], 'candidate': p['name'], 'box': p['box'],
                             'join': p['join'], 'inkUnder': p['ink'], 'nearerAnotherMark': p['ambiguous']}
                            for m, p in zip(marks, placed)]}
    return fw, fh, bytes(out), [cx, cy, cw, ch], flags, operation


# ---------------------------------------------------------------- sources

def slug(text):
    return re.sub(r'\s', '-', re.sub(r'[^\w\- ]', '', text.lower()))


def load_sources(root, require_purposes=True):
    """Registry, notices, mark purposes and static chapters, validated together. Returns a dict with bytes for
    retention. require_purposes=False only replays an edition generated before the purpose list existed."""
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
    purposes, purpose_status = None, None
    if require_purposes or confined(root, PURPOSES).is_file():   # an edition from before the purpose list has none
        document = json.loads(take(PURPOSES, 'purposes'))
        require(document.get('schema') == 'gcam.manual-purposes', 'purposes: unknown schema')
        require(document.get('schemaVersion') != 1, 'purposes: schema 1 (a list under each figure) is no longer supported')
        require(document.get('schemaVersion') == 2, 'purposes: unknown schema')
        purposes, purpose_status = {}, document.get('status')
        glyphs = label_font()['glyphs']
        def line(text):
            return (isinstance(text, str) and text and text.strip() == text and '\n' not in text and '\r' not in text
                    and all(c in glyphs for c in text))
        for entry in document['purposes']:
            target = entry.get('target')
            require(isinstance(target, str) and re.fullmatch(r'[A-Za-z0-9.]+', target) and target not in purposes,
                    'purposes: duplicate or malformed target ' + str(target))
            fields = ('short',) + (('name',) if 'name' in entry else ())
            require(all(line(entry.get(f)) for f in fields) and entry.get('sources'),
                    'purposes: incomplete, or a character the label font has no glyph for, in ' + target)
            purposes[target] = entry
    return {'registry': registry, 'parts': parts, 'notices': by_id, 'noticeStatus': notices.get('status'),
            'static': static, 'files': files, 'purposes': purposes, 'purposeStatus': purpose_status}


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


def purpose_marks(marks, purposes):
    """The marks with their labels as drawn: "<screen text or name>: <short>" (PURPOSE_LABELS). Returns (marks, record
    rows, targets without a purpose entry)."""
    out, rows, missing = [], [], []
    for mark in marks:
        screen, entry = label_text(mark), purposes.get(mark['target'])
        row = {'n': mark['n'], 'target': mark['target'], 'screenText': screen}
        if entry:
            row.update({'short': entry['short'], **({'name': entry['name']} if 'name' in entry else {})})
            label = f'{entry.get("name", screen)}: {entry["short"]}'
        else:
            label = screen
            missing.append(mark['target'])
        out.append({**mark, 'label': label})
        rows.append({**row, 'label': label})
    return out, rows, missing


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
    chosen_runs, printed, plans, missing_purposes = set(), set(), {}, set()
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
    if sources['purposes'] is not None and sources['purposeStatus'] != 'approved':
        draft.append('the mark purpose list is not approved')
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
                        labelled = None
                        if sources['purposes'] is not None and doc['marks']:   # the purpose drawn into each label
                            marks, labelled, absent = purpose_marks(doc['marks'], sources['purposes'])
                            doc = {**doc, 'marks': marks}
                            missing_purposes.update(absent)
                        fw, fh, frgb, crop, fig_flags, operation = derive_figure(width, height, rgb, doc)
                        name = f'figures/{figure["id"]}.png'
                        encoded = (reuse(name, fw, fh, frgb) if reuse else None) or png_encode(fw, fh, frgb)
                        outputs[name] = encoded
                        label = html.escape(f'Figure {figure["id"]}')
                        text += [f'<p align="center"><img src="{html.escape(name)}" alt="{label}"></p>',
                                 f'<p align="center"><em>{label}.</em> {html.escape(figure["caption"], quote=False)}</p>', '']
                        row = {'figure': figure['id'], 'chapter': number, 'runId': record['runId'], 'step': step['n'],
                               'source': figure['capture'], 'sourceSha256': sha(png), 'marksSha256': sha(marks_raw),
                               'operation': operation,
                               'path': name, 'resultSha256': sha(encoded),
                               'pixelSha256': sha(struct.pack('>II', fw, fh) + frgb), 'flags': fig_flags}
                        if labelled is not None:
                            row['markLabels'] = labelled
                        figures_out.append(row)
                        flags += [f'{figure["id"]}:{f}' for f in fig_flags]
                    for missing in step.get('missingFigures', []):
                        text += [f'> **Figure omitted.** Figure {missing["id"]} was not retained: the capture was rejected '
                                 f'({", ".join(missing["reasons"])}).', '']
                        flags.append(f'{missing["id"]}:capture-rejected')
            chapter_text = '\n'.join(text)
            chapter_hashes[number] = sha(chapter_text.encode('utf-8'))
            body.append(chapter_text)
    if missing_purposes:
        draft.append('marks without a purpose entry: ' + ', '.join(sorted(missing_purposes)))
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
              'labelFont': {'path': label_font()['path'], 'sha256': label_font()['sha256'],
                            'font': label_font()['font']['family'] + ' ' + label_font()['font']['style']},
              'registry': {'id': registry['id'], 'locale': registry['locale'], 'sha256': sha(sources['files']['chapters.json'])},
              'notices': {'sha256': sha(sources['files']['notices.json']), 'status': sources['noticeStatus']},
              **({'purposes': {'sha256': sha(sources['files'][PURPOSES]), 'status': sources['purposeStatus'],
                               'missingTargets': sorted(missing_purposes), 'labels': PURPOSE_LABELS}}
                 if sources['purposes'] is not None else {}),
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
    retained = edition / 'sources/manual'
    pinned = [('chapters.json', record['registry']['sha256']), ('notices.json', record['notices']['sha256'])]
    pinned += [(s['path'], s['sha256']) for s in record['static']]
    if 'purposes' in record:   # an edition from before the purpose list pins none and is replayed without one
        pinned.append((PURPOSES, record['purposes']['sha256']))
    for relative, expected in pinned:   # a retained source must be the one the record pins, before anything is replayed
        path = confined(retained, relative)
        require(path.is_file() and sha(path.read_bytes()) == expected, 'source-hash: ' + relative)
    sources = load_sources(retained, 'purposes' in record)
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

def fixture_png(width, height, words=()):
    """A capture like the real ones: window background, one bordered panel 4 px in, and bright 'words' (x, y, w, h)."""
    def colour(x, y):
        if any(a <= x < a + w and b <= y < b + h for a, b, w, h in words):
            return (200, 200, 200)
        if 4 <= x < width - 4 and 4 <= y < height - 4:
            return (43, 53, 62) if x in (4, width - 5) or y in (4, height - 5) else (25, 31, 37)
        return (18, 22, 25)
    return png_encode(width, height, b''.join(bytes(colour(x, y)) for y in range(height) for x in range(width)))


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
    purposes = {'schema': 'gcam.manual-purposes', 'schemaVersion': 2, 'status': 'approved',
                'purposes': [{'target': 'StartAcquisition', 'short': 'go', 'sources': ['fixture']}]}
    (src / 'chapters.json').write_bytes(canonical(registry))
    (src / 'notices.json').write_bytes(canonical(notices))
    (src / PURPOSES).write_bytes(canonical(purposes))
    (src / 'static/intro.md').write_bytes(b'Intro text, see [[chapter:1.2]].\n\n[[notice:N-SIM]]\n')
    width, height = 160, 48
    png = fixture_png(width, height)
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
    def capture(runs, which, width, height, words, marks):   # the run's capture replaced, its marks measured on it
        png = fixture_png(width, height, words)
        (runs / f'run-{which}' / 'captures/f1.png').write_bytes(png)
        def change(m):
            m.update(captureSha256=sha(png), widthPx=width, heightPx=height, marks=[
                {'n': n, 'target': f'T{n}', 'label': label, 'labelSource': 'name',
                 'screen': [float(r[0] + 100), float(r[1] + 50), float(r[2]), float(r[3])], 'image': r}
                for n, (label, r) in enumerate(marks, 1)])
        edit_json(marks_file(runs, which), change)
        if sha(png) != json.loads(run_file(runs, which).read_bytes())['evidence'][0]['sha256']:
            edit_json(run_file(runs, which), lambda r: r['evidence'][0].update(sha256=sha(png), bytes=len(png)))
        rehash(runs, which)
    def walled_in(src, runs):   # other marks on every side: no place in the crop and no clean route to a margin
        capture(runs, 'alpha', 160, 48, [], [('A', [40, 10, 20, 10]), ('L', [0, 0, 36, 48]), ('R', [64, 0, 96, 48]),
                                            ('B', [40, 26, 20, 22])])
    def bottom_mark(src, runs):   # a wide mark along the bottom of a tall capture under a larger one: above it its
        capture(runs, 'alpha', 160, 120, [], [('Start', [8, 108, 144, 8]), ('P', [8, 4, 144, 100])])   # label reads as P's
    def field_stack(src, runs):   # two fields with their captions, the panel's edge close beside them: a right margin
        capture(runs, 'alpha', 300, 240, [(150, 12, 40, 8), (150, 42, 40, 8)],
                [('Alpha', [200, 10, 52, 12]), ('Beta', [200, 40, 52, 12])])
    def long_label(src, runs):   # a long message wraps into a block of lines of at most WRAPS[0] characters
        capture(runs, 'alpha', 400, 240, [], [('Discard the acquired data (2.0 live s, 127 counts)? Inputs unlock for the '
                                               'next acquisition.', [20, 20, 60, 20])])
    def mark_outside(src, runs):
        edit_json(marks_file(runs, 'alpha'), lambda m: m['marks'][0].update(screen=[256.0, 60.0, 8.0, 6.0], image=[156, 10, 8, 6]))
        rehash(runs, 'alpha')
    def double_scaled(src, runs):
        edit_json(marks_file(runs, 'alpha'), lambda m: m['marks'][0].update(image=[15, 15, 13, 9]))
        rehash(runs, 'alpha')
    def label_without_glyph(src, runs):   # labels are drawn as measured and never substitute a character
        edit_json(marks_file(runs, 'alpha'), lambda m: m['marks'][0].update(label='Start \u2192'))
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
    def mark_labels(edition):
        record = json.loads((edition / 'generation-record.json').read_bytes())
        figure = next(f for f in record['figures'] if f['figure'] == '1.2-1')
        return record, [l['text'] for l in figure['operation']['labels']], figure.get('markLabels')
    record, drawn, rows = mark_labels(valid)
    require(drawn == ['Start: go'] and rows == [{'n': 1, 'target': 'StartAcquisition', 'screenText': 'Start', 'short': 'go',
                                                  'label': 'Start: go'}] and record['status'] == 'formal'
            and '1. **Start**' not in (valid / 'MANUAL.Studio.md').read_text(encoding='utf-8'),
            'self-test: valid: the purpose is not drawn into the label')
    results.append(('purpose-in-label', 'generated: ' + drawn[0]))
    retained = valid / 'sources/manual' / PURPOSES
    original = retained.read_bytes()
    edit_json(retained, lambda p: p['purposes'][0].update(short='stop'))
    try:   # a retained source that is not the one the record pins is refused before anything is replayed
        check(valid)
        raise RuntimeError('self-test: altered purpose list accepted')
    except ValueError as error:
        require(str(error) == 'source-hash: ' + PURPOSES, 'self-test: wrong replay refusal ' + str(error))
        results.append(('purpose-hash-mismatch', str(error)))
    retained.write_bytes(original)
    def without_purpose(src, runs):
        edit_json(src / PURPOSES, lambda p: p.update(purposes=[]))
    edition = case('missing-purpose', without_purpose, 'marks without a purpose entry: StartAcquisition')
    record, drawn, rows = mark_labels(edition)
    require(drawn == ['Start'] and rows[0]['label'] == 'Start' and 'short' not in rows[0] and record['status'] == 'draft'
            and record['purposes']['missingTargets'] == ['StartAcquisition'],
            'self-test: missing-purpose: the screen text alone and a draft edition expected')
    def named(src, runs):
        edit_json(src / PURPOSES, lambda p: p['purposes'][0].update(name='Run'))
    record, drawn, rows = mark_labels(case('purpose-name', named, '### 1.2 Alpha'))
    require(drawn == ['Run: go'] and rows[0]['screenText'] == 'Start' and rows[0]['name'] == 'Run',
            'self-test: purpose-name: the name replaces the screen text, which the record keeps')
    results[-1] = ('purpose-name', 'generated: ' + drawn[0])
    case('missing-purpose-formal', without_purpose, 'refuse:formal-refused', formal=True)
    case('purposes-draft', lambda s, r: edit_json(s / PURPOSES, lambda p: p.update(status='draft')),
         'the mark purpose list is not approved')
    case('purpose-duplicate', lambda s, r: edit_json(s / PURPOSES, lambda p: p['purposes'].append(dict(p['purposes'][0]))),
         'refuse:purposes: duplicate or malformed target')
    case('purpose-no-glyph', lambda s, r: edit_json(s / PURPOSES, lambda p: p['purposes'][0].update(short='in \u00b5Ci')),
         'refuse:purposes: incomplete, or a character the label font has no glyph for, in StartAcquisition')
    case('purpose-schema-1', lambda s, r: edit_json(s / PURPOSES, lambda p: p.update(schemaVersion=1)),
         'refuse:purposes: schema 1 (a list under each figure) is no longer supported')
    case('missing-run', lambda s, r: [p.unlink() for p in (r / 'run-beta').rglob('*') if p.is_file()], 'it is not included because no run')
    case('missing-run-formal', lambda s, r: [p.unlink() for p in (r / 'run-beta').rglob('*') if p.is_file()], 'refuse:formal-refused', formal=True)
    case('failed-chapter', failed, 'did not pass (failed)')
    case('failed-chapter-formal', failed, 'refuse:formal-refused', formal=True)
    case('latest-failed-after-pass', later_failure, 'the latest run (run-beta2) did not pass')
    case('evidence-hash', lambda s, r: (r / 'run-alpha/captures/f1.png').write_bytes(png_encode(160, 48, bytes(160 * 48 * 3))), 'refuse:evidence-hash')
    case('mark-outside-image', mark_outside, 'refuse:mark-bounds')
    case('dpi-double-scaling', double_scaled, 'refuse:mark-geometry')
    case('label-no-room', walled_in, 'refuse:label-no-room')
    case('label-without-glyph', label_without_glyph, 'refuse:label-glyph')
    def operation_of(edition):
        record = json.loads((edition / 'generation-record.json').read_bytes())
        return next(f['operation'] for f in record['figures'] if f['figure'] == '1.2-1')
    for name, mutate, side, predicate in (
            ('label-margin-bottom', bottom_mark, 'bottom', lambda o: o['labels'][0]['candidate'] == 'margin-bottom'),
            ('label-margin-right', field_stack, 'right',
             lambda o: [l['candidate'] for l in o['labels']] == ['margin-right'] * 2 and all(l['join'] for l in o['labels'])),
            ('label-wrapped', long_label, None,
             lambda o: len(o['labels'][0]['lines']) == 3 and all(len(s) <= WRAPS[0] for s in o['labels'][0]['lines']))):
        edition = case(name, mutate, '### 1.2 Alpha')
        operation = operation_of(edition)
        margins = operation['margins']
        require(predicate(operation), f'self-test: {name}: unexpected placement {operation["labels"]}')
        added = {k: v for k, v in margins.items() if v}
        require(list(added) == ([side] if side else []), f'self-test: {name}: margins {margins}')
        require(operation['size'] == [operation['crop'][2] + margins['left'] + margins['right'], operation['crop'][3] + margins['bottom']],
                f'self-test: {name}: the margin is not the only added space')
        check(edition)
        results[-1] = (name, f'generated: {operation["labels"][0]["candidate"]}, margins {added or "none"}, replayed')
    rects = [[10, 10, 9, 6], [36, 20, 9, 6]]
    for name, box, code in (('label-overlap', [40, 30, 20, 12], 'label-overlap: label 1 covers label 2'),
                            ('label-over-own-target', [8, 8, 20, 12], 'label-overlap: label 1 covers its own target'),
                            ('label-outside-figure', [50, 40, 20, 12], 'label-bounds: label 1 outside the figure')):
        try:   # the post-condition alone, on a seeded placement
            check_labels(rects, [{'box': box, 'join': None}, {'box': [44, 34, 18, 12], 'join': None}], 64, 48)
            raise RuntimeError(f'self-test: {name}: defect accepted')
        except ValueError as error:
            require(str(error) == code, f'self-test: {name}: wrong refusal {error}')
            results.append((name, str(error)))
    global place_labels
    sound = place_labels
    def on_target(width, height, rgb, marks, variants, crop, ink, routes):   # a defective placement that skips its rules
        return [{'box': [m['image'][0], m['image'][1], v[0][1][0], v[0][1][1]], 'unit': v[0][1], 'route': 'across',
                 'name': 'defect', 'ink': 0, 'ambiguous': False, 'lines': []} for m, v in zip(marks, variants)], True
    place_labels = on_target
    try:   # the figure derivation re-checks every label and refuses the edition
        case('label-placement-defect', lambda s, r: None, 'refuse:label-overlap')
    finally:
        place_labels = sound
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
        print(f'registry: {len(sources["registry"]["chapters"])} chapters, {len(sources["notices"])} notices, '
              f'{len(sources["purposes"])} mark purposes, valid')
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
