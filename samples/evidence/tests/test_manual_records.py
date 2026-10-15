"""Manual edition generator: PNG codec, mark geometry, overlays, selection and the repository sources. No desktop."""
import importlib.util
import io
import json
from pathlib import Path
import random
import re
import struct
import tempfile
import unittest
import uuid
import xml.etree.ElementTree as ET
import zipfile
import zlib

ROOT = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location('gcam_manual_records', ROOT / 'samples/manual/manual_records.py')
mr = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mr)


def scratch():
    # Retained for audit under the temporary folder; no cleanup or deletion.
    root = Path(tempfile.gettempdir()) / ('gcam-manual-fixture-' + uuid.uuid4().hex)
    root.mkdir()
    return root


def filtered_png(width, height, pixels, channels, filters):
    """Independent forward encoder: applies PNG filter types 0-4 row by row (the decoder must undo them)."""
    stride = width * channels
    rows, previous = [], bytes(stride)
    for y in range(height):
        row = pixels[y * stride:(y + 1) * stride]
        kind = filters[y % len(filters)]
        out = bytearray()
        for i in range(stride):
            a = row[i - channels] if i >= channels else 0
            b = previous[i]
            c = previous[i - channels] if i >= channels else 0
            if kind == 0:
                predictor = 0
            elif kind == 1:
                predictor = a
            elif kind == 2:
                predictor = b
            elif kind == 3:
                predictor = (a + b) // 2
            else:
                p = a + b - c
                predictor = min((abs(p - a), 0, a), (abs(p - b), 1, b), (abs(p - c), 2, c))[2]
            out.append((row[i] - predictor) % 256)
        rows.append(bytes([kind]) + bytes(out))
        previous = row
    def chunk(kind, body):
        return struct.pack('>I', len(body)) + kind + body + struct.pack('>I', zlib.crc32(kind + body))
    colour = 2 if channels == 3 else 6
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, colour, 0, 0, 0))
            + chunk(b'tEXt', b'Software\x00fixture') + chunk(b'IDAT', zlib.compress(b''.join(rows)))
            + chunk(b'IEND', b''))


class ManualRecordsTests(unittest.TestCase):
    def pixels(self, width, height, channels):
        return bytes((x * 37 + y * 11 + k * 101) % 256 for y in range(height) for x in range(width) for k in range(channels))

    def test_decoder_undoes_every_scanline_filter_for_rgb_and_rgba(self):
        for channels in (3, 4):
            raw = self.pixels(7, 10, channels)
            data = filtered_png(7, 10, raw, channels, [0, 1, 2, 3, 4])
            width, height, rgb = mr.png_decode(data)
            expected = raw if channels == 3 else b''.join(raw[i:i + 3] for i in range(0, len(raw), 4))
            self.assertEqual((7, 10, expected), (width, height, rgb))

    def test_encoder_round_trips_and_is_deterministic(self):
        rgb = self.pixels(9, 4, 3)
        first = mr.png_encode(9, 4, rgb)
        self.assertEqual(first, mr.png_encode(9, 4, rgb))
        self.assertEqual((9, 4, rgb), mr.png_decode(first))

    def test_decoder_refuses_corruption_and_unsupported_formats(self):
        good = mr.png_encode(3, 2, bytes(18))
        broken = bytearray(good)
        broken[-5] ^= 1   # IEND CRC
        for data, code in ((bytes(broken), 'png: crc'), (b'GIF89a', 'png: signature'),
                           (good.replace(struct.pack('>IIBBBBB', 3, 2, 8, 2, 0, 0, 0), struct.pack('>IIBBBBB', 3, 2, 16, 2, 0, 0, 0)), 'png: crc')):
            with self.assertRaises(ValueError) as caught:
                mr.png_decode(data)
            self.assertTrue(str(caught.exception).startswith(code), caught.exception)
        body = struct.pack('>IIBBBBB', 3, 2, 8, 2, 0, 0, 1)   # interlaced, with a valid CRC
        interlaced = good[:8] + struct.pack('>I', 13) + b'IHDR' + body + struct.pack('>I', zlib.crc32(b'IHDR' + body)) + good[33:]
        with self.assertRaisesRegex(ValueError, 'png: unsupported format'):
            mr.png_decode(interlaced)

    def test_image_rect_matches_the_harness_rule(self):
        # Same fixtures as ManualRecordTests.ImageRect_* in the C# harness.
        self.assertEqual([10, 10, 9, 6], mr.image_rect([110.25, 60.0, 8.5, 6.0], [100, 50]))
        self.assertEqual([119, 10, 31, 21], mr.image_rect([-1800.5, 10.4, 30, 20], [-1920, 0]))
        self.assertEqual([75, 105, 30, 15], mr.image_rect([150, 150, 30, 15], [75, 45]))

    def marks(self, rects, crop='marks'):
        return {'crop': crop, 'marks': [{'n': i + 1, 'label': 'L', 'image': r} for i, r in enumerate(rects)]}

    def panel_image(self, width, height, text=None):
        """Window background, one bordered panel at (20, 20)-(179, 99) and an optional bright 'text' block."""
        def colour(x, y):
            if text and text[0] <= x < text[0] + text[2] and text[1] <= y < text[1] + text[3]:
                return (200, 200, 200)
            if 20 <= x < 180 and 20 <= y < 100:
                return (43, 53, 62) if x in (20, 179) or y in (20, 99) else (25, 31, 37)
            return (18, 22, 25)
        return b''.join(bytes(colour(x, y)) for y in range(height) for x in range(width))

    def test_labels_sit_beside_their_marks_off_text_and_inside_the_crop(self):
        width, height = 200, 120
        text = (34, 50, 22, 10)                               # a word just left of mark 1
        rgb = self.panel_image(width, height, text)
        w, h, out, crop, flags, operation = mr.derive_figure(width, height, rgb, self.marks([[60, 50, 20, 10], [100, 74, 30, 12]]))
        self.assertEqual([13, 13, 174, 94], crop)            # the whole panel (border at 20 and 179 / 99) + 6 px of background
        self.assertEqual(('panel-edges', [], {'left': 0, 'right': 0, 'bottom': 0}, [174, 94]),
                         (operation['cropRule'], flags, operation['margins'], [w, h]))
        labels = operation['labels']
        # Right of mark 1 is free: its label goes there, beside the frame (no join line). Right of mark 2 too.
        self.assertEqual([('right', None), ('right', None)], [(l['candidate'], l['join']) for l in labels])
        unit = mr.label_unit(1, 'L')
        self.assertEqual([60 + 20 + mr.BORDER + mr.LEAD - 13, 50 - mr.BORDER + (10 + 2 * mr.BORDER - mr.LABEL_H) // 2 - 13,
                          unit[0], mr.LABEL_H], labels[0]['box'])
        local = [[60 - 13, 50 - 13, 20, 10], [100 - 13, 74 - 13, 30, 12]]
        self.assertEqual([], mr.check_labels(local, labels, w, h))
        for label in labels:
            box = label['box']
            self.assertFalse(mr.overlap(box, [text[0] - 13, text[1] - 13, text[2], text[3]]))
            self.assertTrue(mr.inside(box, [0, 0, w, h]))
        def at(x, y):
            return tuple(out[(y * w + x) * 3:(y * w + x) * 3 + 3])
        self.assertEqual(mr.RED, at(60 - 13 - 2, 50 - 13 - 2))   # border outside the control's top-left corner
        self.assertEqual((25, 31, 37), at(60 - 13 + 5, 50 - 13 + 5))   # the control itself is not painted
        self.assertTrue(all(at(x - 13, y - 13) == (200, 200, 200)
                            for y in range(text[1], text[1] + text[3]) for x in range(text[0], text[0] + text[2])))
        x0, y0 = labels[0]['box'][:2]
        self.assertEqual(mr.RED, at(x0 + 3, y0 + mr.LABEL_H // 2))     # the disc, left of the number
        self.assertIn(mr.WHITE, {at(x, y) for y in range(y0 + 6, y0 + 18) for x in range(x0 + 7, x0 + 15)})   # the number
        label_pixels = {at(x, y) for y in range(y0, y0 + mr.LABEL_H) for x in range(x0 + mr.DISC + 2, x0 + unit[0])}
        self.assertIn(mr.WHITE, label_pixels)                 # white text ...
        self.assertTrue(any(max(c) < 25 for c in label_pixels))   # ... with a dark outline
        self.assertTrue(len(label_pixels) > 6)                # anti-aliased edges, not a bitmap font

    def test_text_is_rasterised_from_the_committed_rounded_glyph_subset(self):
        data = mr.FONT_DATA.read_bytes()
        subset = json.loads(data)
        self.assertEqual({chr(c) for c in range(32, 127)} | set('§±µ·×σ–—…−'), set(subset['glyphs']))
        self.assertEqual(('M PLUS Rounded 1c', 'Bold'), (subset['font']['family'], subset['font']['style']))
        self.assertTrue((mr.FONT_DATA.parent / 'OFL.txt').read_text(encoding='utf-8').startswith(
            'Copyright 2016 The Rounded M+ Project Authors.'))
        dot, ellipsis = subset['glyphs']['.'], subset['glyphs']['…']   # built from three periods on the baseline
        self.assertEqual(3 * dot['advance'], ellipsis['advance'])
        self.assertEqual([[v + (k * dot['advance'] if i % 3 == 0 else 0) for i, v in enumerate(c)]
                          for k in range(3) for c in dot['contours']], ellipsis['contours'])
        self.assertEqual(mr.sha(data), mr.label_font()['sha256'])
        # a closed square contour fills exactly its pixels; half a pixel of coverage is half alpha
        f = mr.fine()
        square = [(0, 0, 2 * f, 0), (2 * f, 0, 2 * f, 2 * f), (2 * f, 2 * f, 0, 2 * f), (0, 2 * f, 0, 0)]
        self.assertEqual([255, 255, 0] * 2 + [0] * 3, mr.coverage(square, 3, 3))
        half = [(0, 0, f // 2, 0), (f // 2, 0, f // 2, f), (f // 2, f, 0, f), (0, f, 0, 0)]
        self.assertEqual([127], mr.coverage(half, 1, 1))
        width, height, disc, layers, lines = mr.label_unit(7, 'Mask–detector · 0.7 mm …')
        self.assertEqual(['Mask–detector · 0.7 mm …'], lines)
        self.assertEqual((mr.LABEL_H, mr.DISC), (height, disc))
        text_alpha, digit_alpha = layers[1][0], layers[3][0]
        self.assertTrue(any(0 < a < 255 for a in text_alpha) and 255 in text_alpha)
        outline = layers[0][0]
        self.assertTrue(all(o >= a for o, a in zip(outline, text_alpha)))   # the outline lies under every text pixel
        ink = [(i % width, i // width) for i, a in enumerate(digit_alpha) if a >= 128]
        xs, ys = [x for x, _ in ink], [y for _, y in ink]
        self.assertLessEqual(abs(min(xs) + max(xs) + 1 - disc), 1)     # the number centred on the disc by its ink
        self.assertLessEqual(abs(min(ys) + max(ys) + 1 - height), 1)
        mr._UNITS.clear()
        mr._GLYPH_SEGMENTS.clear()
        self.assertEqual(layers, mr.label_unit(7, 'Mask–detector · 0.7 mm …')[3])   # deterministic

    def test_label_refuses_a_missing_glyph_and_prints_the_first_line(self):
        with self.assertRaisesRegex(ValueError, r'label-glyph: mark 1 label has no glyph for U\+2192'):
            mr.label_unit(1, 'Start →')
        self.assertEqual('Discard acquired data?', mr.label_text({'label': '\nDiscard acquired data?\r\nInputs unlock.'}))

    def test_long_labels_wrap_into_a_block_under_their_disc(self):
        label = 'Discard the acquired data (2.0 live s, 127 counts)? Inputs unlock for the next acquisition.'
        width, height, disc, layers, lines = mr.label_unit(1, label)
        self.assertEqual(['Discard the acquired data (2.0', 'live s, 127 counts)? Inputs', 'unlock for the next acquisition.'], lines)
        self.assertEqual(mr.LABEL_H + 2 * mr.LINE_PITCH, height)
        text = layers[1][0]
        rows = [y for y in range(height) if any(text[y * width + x] for x in range(width))]
        self.assertLess(max(rows), height - 1)                # every line drawn inside the box
        self.assertFalse(any(text[y * width + x] for y in range(height) for x in range(disc + 2)))   # all under the text
        self.assertEqual(['3.6 × 4.2 mm · 42 px · mean 5.45 ·', 'max 13'],
                         mr.wrap_text('3.6 × 4.2 mm · 42 px · mean 5.45 · max 13', 32))   # no line opens on a separator
        self.assertEqual(['abcdefgh', 'ijkl xy'], mr.wrap_text('abcdefghijkl xy', 8))   # a word longer than a line breaks
        self.assertEqual([(32, ['Pixels / side']), (12, ['Pixels /', 'side'])],   # a short label may fold once to fit
                         [(w, u[4]) for w, u in mr.label_variants(2, 'Pixels / side')])
        self.assertEqual([32], [w for w, _ in mr.label_variants(1, label)])   # a long block is never folded taller

    def stack_image(self, width, height, words):
        def colour(x, y):
            if any(a <= x < a + w and b <= y < b + h for a, b, w, h in words):
                return (200, 200, 200)
            if 4 <= x < width - 4 and 4 <= y < height - 4:
                return (43, 53, 62) if x in (4, width - 5) or y in (4, height - 5) else (25, 31, 37)
            return (18, 22, 25)
        return b''.join(bytes(colour(x, y)) for y in range(height) for x in range(width))

    def test_labels_without_a_place_go_to_a_plain_margin_and_never_widen_the_crop(self):
        # A stack of fields with captions on the left and the panel edge close on the right: the crop stays the panel,
        # a right margin holds both labels level with their fields, joined by straight lines.
        width, height = 300, 240
        rgb = self.stack_image(width, height, [(150, 12, 40, 8), (150, 42, 40, 8)])
        doc = {'crop': 'full', 'marks': [{'n': 1, 'label': 'Alpha', 'image': [200, 10, 52, 12]},
                                         {'n': 2, 'label': 'Beta', 'image': [200, 40, 52, 12]}]}
        w, h, out, crop, flags, operation = mr.derive_figure(width, height, rgb, doc)
        right = mr.MARGIN_GAP + mr.label_unit(1, 'Alpha')[0] + mr.MARGIN_PAD
        self.assertEqual(([0, 0, 300, 240], {'left': 0, 'right': right, 'bottom': 0}, [300 + right, 240], []),
                         (crop, operation['margins'], [w, h], flags))
        first, second = operation['labels']
        self.assertEqual(('margin-right', 'margin-right'), (first['candidate'], second['candidate']))
        self.assertEqual([[254, 16], [300 + mr.MARGIN_GAP, 16]], first['join'])   # level with the field's middle
        self.assertEqual([300 + mr.MARGIN_GAP, 16 - mr.LABEL_H // 2], first['box'][:2])
        def at(x, y):
            return tuple(out[(y * w + x) * 3:(y * w + x) * 3 + 3])
        self.assertEqual(mr.RED, at(280, 16))                 # the join line crosses the panel edge
        self.assertEqual((18, 22, 25), at(w - 2, h - 2))      # the margin is the window background
        # A wide mark along the bottom of a tall capture, under a larger mark: above it its label would read as the
        # other mark's, so it goes to a bottom margin (less area than a side margin).
        width, height = 160, 120
        doc = {'crop': 'full', 'marks': [{'n': 1, 'label': 'Start', 'image': [8, 108, 144, 8]},
                                         {'n': 2, 'label': 'P', 'image': [8, 4, 144, 100]}]}
        w, h, out, crop, flags, operation = mr.derive_figure(width, height, self.stack_image(width, height, []), doc)
        bottom = 2 * mr.STRIP_PAD + mr.LABEL_H
        self.assertEqual(({'left': 0, 'right': 0, 'bottom': bottom}, [160, 120 + bottom]), (operation['margins'], [w, h]))
        first = operation['labels'][0]
        self.assertEqual(('margin-bottom', [7, 120 + mr.STRIP_PAD]), (first['candidate'], first['box'][:2]))
        self.assertEqual([[18, 118], [18, 120 + mr.STRIP_PAD]], first['join'])   # straight down from the frame to the disc
        self.assertTrue(operation['labels'][1]['candidate'].startswith('inside-'))   # the large mark names itself inside

    def test_margin_lines_start_on_their_frames_and_never_share_a_rule(self):
        # A word left of a field blocks the straight line out of the field's middle: the route turns down a corridor,
        # and its line starts on the frame it leaves.
        width, height = 300, 240
        rgb = self.stack_image(width, height, [(60 + 4 * k, 12, 2, 8) for k in range(8)])   # strokes of a word
        crop = [0, 0, width, height]
        ink = mr.ink_counter(width, height, rgb, crop)
        rect = [100, 10, 40, 12]
        fl, ft, fw, fh = mr.framed(rect)
        routes = dict(mr.margin_routes(0, [rect], crop, ink))
        self.assertEqual([[fl + fw // 2, ft + fh], [fl + fw // 2, ft + fh + 6], [0, ft + fh + 6]], routes['left'])
        # an earlier mark's route to the same margin is avoided: here straight out of the frame's side above the word
        taken = {'left': mr.line_boxes(routes['left'], 2)}
        again = dict(mr.margin_routes(0, [rect], crop, ink, taken))['left']
        self.assertEqual([[fl, ft + 2], [0, ft + 2]], again)
        self.assertFalse(any(mr.overlap(a, b) for a in mr.line_boxes(again) for b in taken['left']))
        # three stacked labels whose lines step down to them: each step gets its own rule, the upper label's farther
        # out, so no two join lines touch; a single step keeps the shared rule
        unit = mr.label_unit(1, 'Label')
        items = [(0, [[100, 20], [100, 30], [0, 30]], unit), (1, [[150, 20], [150, 35], [0, 35]], unit),
                 (2, [[200, 20], [200, 40], [0, 40]], unit)]
        size, out = mr.margin_layout('left', items, crop)
        self.assertEqual(6 + 2 * mr.BUS + unit[0] + mr.MARGIN_PAD, size)
        self.assertEqual(3, len(out[0][1]))                                   # level with its route: no step
        buses = [out[i][1][-3][0] for i in (1, 2)]
        self.assertEqual([-(3 + mr.BUS), -3], buses)                         # the upper step's rule farther out
        for i in range(3):
            for j in range(i + 1, 3):
                self.assertFalse(any(mr.overlap(a, b) for a in mr.line_boxes(out[i][1]) for b in mr.line_boxes(out[j][1])))
        size, out = mr.margin_layout('left', items[:2], crop)
        self.assertEqual((mr.MARGIN_GAP + unit[0] + mr.MARGIN_PAD, -mr.MARGIN_GAP // 2), (size, out[1][1][-3][0]))

    def test_label_refuses_a_mark_without_a_place_or_a_route_to_a_margin(self):
        doc = self.marks([[40, 10, 20, 10], [0, 0, 36, 48], [64, 0, 96, 48], [40, 26, 20, 22]], crop='full')
        with self.assertRaisesRegex(ValueError, 'label-no-room: mark [12] has no place in the figure or a margin'):
            mr.derive_figure(160, 48, self.stack_image(160, 48, []), doc)

    def test_label_check_refuses_overlaps_and_reports_crossings(self):
        rects = [[10, 10, 9, 6], [36, 20, 9, 6]]
        def label(box, join=None):
            return {'box': box, 'join': join}
        self.assertEqual([], mr.check_labels(rects, [label([22, 7, 12, 12]), label([48, 17, 12, 12])], 64, 48))
        self.assertEqual([1], mr.check_labels(rects, [label([26, 14, 12, 12]), label([48, 30, 12, 12])], 64, 48))
        for labels, code in (([label([8, 8, 12, 12]), label([48, 17, 12, 12])], 'label-overlap: label 1 covers its own target'),
                             ([label([22, 7, 12, 12]), label([30, 10, 12, 12])], 'label-overlap: label 1 covers label 2'),
                             ([label([22, 30, 12, 12]), label([48, 30, 12, 12], [[48, 36], [24, 36]])],
                              'label-overlap: label 1 covers the join line of label 2'),
                             ([label([22, 7, 12, 12]), label([56, 17, 12, 12])], 'label-bounds: label 2 outside the figure')):
            with self.assertRaises(ValueError) as caught:
                mr.check_labels(rects, labels, 64, 48)
            self.assertEqual(code, str(caught.exception))

    def test_marks_must_be_the_measured_rectangles_inside_the_capture(self):
        png = mr.png_encode(64, 48, bytes(64 * 48 * 3))
        doc = {'schema': 'gcam.figure-marks', 'schemaVersion': 1, 'captureSha256': mr.sha(png), 'widthPx': 64, 'heightPx': 48,
               'frameOriginPx': [100, 50], 'marks': [{'n': 1, 'target': 'T', 'label': 'L', 'screen': [110.25, 60.0, 8.5, 6.0],
                                                      'image': [10, 10, 9, 6]}]}
        mr.validate_marks(doc, 64, 48, mr.sha(png))
        for change, code in ((lambda d: d['marks'][0].update(n=2), 'mark-numbering'),
                             (lambda d: d['marks'][0].update(label=' '), 'mark-label'),
                             (lambda d: d['marks'][0].update(image=[10.0, 10, 9, 6]), 'mark-geometry'),
                             (lambda d: d.update(widthPx=65), 'mark-capture'),
                             (lambda d: d['marks'][0].update(screen=[160.0, 60.0, 8.0, 6.0], image=[60, 10, 8, 6]), 'mark-bounds')):
            bad = json.loads(json.dumps(doc))
            change(bad)
            with self.assertRaises(ValueError) as caught:
                mr.validate_marks(bad, 64, 48, mr.sha(png))
            self.assertTrue(str(caught.exception).startswith(code), caught.exception)

    def test_repository_sources_are_valid_and_bind_every_notice(self):
        sources = mr.load_sources(mr.SOURCES)
        chapters = {c['number']: c for c in sources['registry']['chapters']}
        self.assertEqual(10, len(chapters))
        self.assertEqual({'1.1', '5.2'}, {n for n, c in chapters.items() if c['source'] == 'static'})
        # Second edition (TODO-48): approved by the author on 2026-10-16 with the edition.
        self.assertFalse(any(s['draft'] for s in sources['static'].values()), 'the static chapters are approved')
        self.assertEqual('approved', sources['noticeStatus'])
        self.assertEqual('approved', sources['purposeStatus'])
        for notice in sources['notices'].values():
            for number in notice['chapters']:
                if chapters[number]['source'] == 'static':
                    self.assertIn(f'[[notice:{notice["id"]}]]', sources['static'][number]['body'])

    def test_self_test_refuses_every_seeded_defect(self):
        results = dict(mr.self_test(scratch() / 'self-test'))
        for name in ('missing-run-formal', 'failed-chapter-formal', 'evidence-hash', 'mark-outside-image', 'mixed-binaries',
                     'budget-exceeded', 'tampered-figure', 'dpi-double-scaling', 'identity-leak', 'label-no-room',
                     'label-overlap', 'label-over-own-target', 'label-outside-figure', 'label-placement-defect',
                     'label-without-glyph', 'purpose-hash-mismatch', 'missing-purpose-formal', 'purpose-duplicate',
                     'purpose-no-glyph', 'purpose-schema-1', 'tampered-pdf', 'tampered-docx', 'pdf-missing-glyph'):
            self.assertIn(name, results)
            self.assertFalse(results[name].startswith('generated'), name)
        self.assertTrue(results['missing-run'].startswith('generated'))
        self.assertTrue(results['failed-chapter'].startswith('generated'))
        self.assertTrue(results['label-margin-bottom'].startswith('generated: margin-bottom'))
        self.assertTrue(results['label-margin-right'].startswith('generated: margin-right'))
        self.assertTrue(results['label-wrapped'].startswith('generated'))
        self.assertEqual('generated: marks without a purpose entry: StartAcquisition', results['missing-purpose'])
        self.assertEqual('generated: the mark purpose list is not approved', results['purposes-draft'])
        self.assertEqual('source-hash: purposes.json', results['purpose-hash-mismatch'])
        self.assertEqual('generated: Start: go', results['purpose-in-label'])
        self.assertEqual('generated: Run: go', results['purpose-name'])
        self.assertEqual('check-differs: MANUAL.Studio.pdf', results['tampered-pdf'])
        self.assertEqual('check-differs: MANUAL.Studio.docx', results['tampered-docx'])
        self.assertEqual('pdf-glyph: no glyph for U+2192 in Regular text', results['pdf-missing-glyph'])
        self.assertEqual('check passed without videos', results['video-hash-without-files-replay'])
        self.assertTrue(results['bad-video-hash'].startswith('video-provenance:'))

    def test_latest_run_wins_and_a_tie_refuses(self):
        root = scratch()
        src, runs = mr.fixture(root)
        record = json.loads((runs / 'run-beta/manual-run.json').read_bytes())
        record.update(runId='run-beta0', startedAt='2026-10-14T09:00:00+00:00')
        (runs / 'run-beta0').mkdir()
        (runs / 'run-beta0/manual-run.json').write_bytes(mr.canonical(record))
        outputs = mr.generate(src, runs, 'e1', '2026-10-14', root / 'e1')
        generation = json.loads(outputs['generation-record.json'])
        beta = next(c for c in generation['chapters'] if c['number'] == '1.3')
        self.assertEqual(['superseded', 'latest'], [c['selection'] for c in beta['candidates']])
        self.assertEqual({'run-beta0': 'record-only', 'run-beta': 'full'},
                         {r['runId']: r['retained'] for r in generation['runs'] if r['scenario'] == 'manual.beta'})
        record.update(startedAt=json.loads((runs / 'run-beta/manual-run.json').read_bytes())['startedAt'])
        (runs / 'run-beta0/manual-run.json').write_bytes(mr.canonical(record))
        with self.assertRaisesRegex(ValueError, 'selection-tie'):
            mr.generate(src, runs, 'e2', '2026-10-14', root / 'e2')

    def test_figures_are_centred_and_the_record_describes_their_labels(self):
        root = scratch()
        src, runs = mr.fixture(root)
        record = json.loads((runs / 'run-alpha/manual-run.json').read_bytes())
        record['steps'][0]['figures'][0]['caption'] = 'A <b> & c'
        (runs / 'run-alpha/manual-run.json').write_bytes(mr.canonical(record))
        outputs = mr.generate(src, runs, 'e1', '2026-10-14', root / 'e1')
        markdown = outputs['MANUAL.Studio.md'].decode('utf-8')
        self.assertIn('<p align="center"><img src="figures/1.2-1.png" alt="Figure 1.2-1"></p>\n'
                      '<p align="center"><em>Figure 1.2-1.</em> A &lt;b&gt; &amp; c</p>\n', markdown)
        self.assertNotIn('**1** Start', markdown)   # the label is in the figure, not in the caption
        generation = json.loads(outputs['generation-record.json'])
        font = {'path': 'samples/manual/font/mplus-rounded-1c-bold.glyphs.json', 'sha256': mr.sha(mr.FONT_DATA.read_bytes()),
                'font': 'M PLUS Rounded 1c Bold'}
        self.assertEqual(font, generation['labelFont'])
        figure = next(f for f in generation['figures'] if f['figure'] == '1.2-1')
        operation = figure['operation']
        self.assertEqual((mr.OVERLAY, {'left': 0, 'right': 0, 'bottom': 0}), (operation['overlay'], operation['margins']))
        self.assertEqual(font['sha256'], operation['font']['sha256'])
        self.assertEqual([(1, 'Start: go')], [(l['n'], l['text']) for l in operation['labels']])   # screen text: purpose
        self.assertTrue(operation['labels'][0]['candidate'].startswith('right'))
        width, height, _ = mr.png_decode(outputs['figures/1.2-1.png'])
        self.assertEqual([width, height], operation['size'])
        self.assertEqual(operation['crop'][2:], operation['size'])   # no margin: the labels add no space

    def test_each_label_pairs_the_screen_text_or_a_name_with_its_purpose(self):
        marks = [{'n': 1, 'target': 'A', 'label': 'Alpha\nsecond line'}, {'n': 2, 'target': 'B', 'label': 'x 6.3 mm · 10 counts'},
                 {'n': 3, 'target': 'C', 'label': 'Gamma'}]
        purposes = {'A': {'short': 'does a'}, 'B': {'name': 'Readout', 'short': 'does b'}}
        labelled, rows, missing = mr.purpose_marks(marks, purposes)
        self.assertEqual(['Alpha: does a', 'Readout: does b', 'Gamma'], [m['label'] for m in labelled])
        self.assertEqual([{'n': 1, 'target': 'A', 'screenText': 'Alpha', 'short': 'does a', 'label': 'Alpha: does a'},
                          {'n': 2, 'target': 'B', 'screenText': 'x 6.3 mm · 10 counts', 'name': 'Readout', 'short': 'does b',
                           'label': 'Readout: does b'},
                          {'n': 3, 'target': 'C', 'screenText': 'Gamma', 'label': 'Gamma'}], rows)
        self.assertEqual(['C'], missing)
        self.assertEqual(marks[0]['label'], 'Alpha\nsecond line')   # the measured marks are not changed
        root = scratch()
        src, runs = mr.fixture(root)
        outputs = mr.generate(src, runs, 'e1', '2026-10-14', root / 'e1')
        markdown = outputs['MANUAL.Studio.md'].decode('utf-8')
        self.assertIn('<p align="center"><em>Figure 1.2-1.</em> The top bar.</p>\n\n### 1.3 Beta', markdown)   # no list under it
        generation = json.loads(outputs['generation-record.json'])
        self.assertEqual({'sha256': mr.sha((src / mr.PURPOSES).read_bytes()), 'status': 'approved', 'missingTargets': [],
                          'labels': mr.PURPOSE_LABELS}, generation['purposes'])
        figure = next(f for f in generation['figures'] if f['figure'] == '1.2-1')
        self.assertEqual(('formal', [{'n': 1, 'target': 'StartAcquisition', 'screenText': 'Start', 'short': 'go', 'label': 'Start: go'}],
                          ['Start: go']), (generation['status'], figure['markLabels'], [l['text'] for l in figure['operation']['labels']]))
        self.assertEqual(outputs['sources/manual/purposes.json'], (src / mr.PURPOSES).read_bytes())

    def test_an_edition_from_before_the_purpose_list_replays_without_one(self):
        root = scratch()
        src, runs = mr.fixture(root)
        legacy = root / 'legacy-sources'
        (legacy / 'static').mkdir(parents=True)
        for relative in ('chapters.json', 'notices.json', 'static/intro.md'):
            (legacy / relative).write_bytes((src / relative).read_bytes())
        with self.assertRaisesRegex(ValueError, 'purposes: missing purposes.json'):
            mr.load_sources(legacy)   # a new edition always needs the purpose list
        sources = mr.load_sources(legacy, require_purposes=False)
        outputs = mr.assemble(sources, mr.load_runs(runs, sources), 'old', '2026-10-14', False, mr.DEFAULT_BUDGET)
        edition = root / 'old'
        for relative, data in outputs.items():
            (edition / relative).parent.mkdir(parents=True, exist_ok=True)
            (edition / relative).write_bytes(data)
        generation = json.loads(outputs['generation-record.json'])
        self.assertNotIn('purposes', generation)
        self.assertNotIn('1. **Start**', outputs['MANUAL.Studio.md'].decode('utf-8'))
        self.assertEqual('old', mr.check(edition)['editionId'])

    def test_repository_purposes_name_existing_sources(self):
        sources = mr.load_sources(mr.SOURCES)
        self.assertGreaterEqual(len(sources['purposes']), 38)
        for target, entry in sources['purposes'].items():
            for source in entry['sources']:
                self.assertTrue((ROOT / source.split(' ')[0]).is_file(), f'{target}: {source}')
            short = entry['short']
            self.assertTrue(len(short) <= 32 and not short.endswith('.') and short != target, f'{target}: {short}')
            self.assertLessEqual(len(entry.get('name', '')), 20, target)
            self.assertNotIn('purpose', entry, target)

    def test_deflate_streams_round_trip_and_do_not_depend_on_the_zlib_build(self):
        fm = mr.formats
        self.assertEqual(b'\x03\x00', fm.deflate(b''))   # one fixed-Huffman block holding only its end code
        rng = random.Random(7)
        samples = [b'a', b'abcabcabcabc' * 50, bytes(rng.randrange(256) for _ in range(5000)),
                   b''.join(b'BT /FR 9.5 Tf 1 0 0 1 %d 700 Tm (word) Tj ET\n' % k for k in range(400)), bytes(70000)]
        for data in samples:
            raw = fm.deflate(data)
            self.assertEqual(data, zlib.decompress(raw, -15))
            self.assertEqual(data, zlib.decompress(fm.zlib_stream(data)))
            self.assertEqual(raw, fm.deflate(data))
        self.assertLess(len(fm.deflate(samples[3])), len(samples[3]) // 4)   # it compresses, not only stores

    def test_markdown_blocks_cover_the_edition_subset_and_refuse_anything_else(self):
        fm = mr.formats
        markdown = ('# Title\n\nEdition `e1` · see [chapter 1.1 (Intro)](#11-intro) and **bold *x* text**.\n\n'
                    '> **Caution.** Simulated.\n\n- one\n  continued `code`\n  - [1.1 Intro](#11-intro)\n\n'
                    '### 1.1 Intro\n\nA paragraph\nwrapped.\n\n'
                    '<p align="center"><img src="figures/1.1-1.png" alt="Figure 1.1-1"></p>\n'
                    '<p align="center"><em>Figure 1.1-1.</em> A &lt;b&gt; &amp; c</p>\n')
        blocks = fm.blocks(markdown, mr.slug)
        self.assertEqual(['heading', 'para', 'notice', 'list', 'heading', 'para', 'figure'], [b[0] for b in blocks])
        self.assertEqual('11-intro', blocks[4][3])
        self.assertEqual([('Edition ', frozenset(), None), ('e1', frozenset({'code'}), None), (' · see ', frozenset(), None),
                          ('chapter 1.1 (Intro)', frozenset(), '11-intro'), (' and ', frozenset(), None),
                          ('bold ', frozenset({'b'}), None), ('x', frozenset({'b', 'i'}), None), (' text', frozenset({'b'}), None),
                          ('.', frozenset(), None)], blocks[1][1])
        self.assertEqual('caution', blocks[2][1])
        self.assertEqual([0, 1], [depth for depth, _ in blocks[3][1]])
        self.assertEqual('one continued ', blocks[3][1][0][1][0][0])
        self.assertEqual([('A paragraph wrapped.', frozenset(), None)], blocks[5][1])
        self.assertEqual(('figures/1.1-1.png', 'Figure 1.1-1'), blocks[6][1:3])
        self.assertEqual(['Figure 1.1-1. ', 'A <b> & c'], [r[0] for r in blocks[6][3]])
        self.assertTrue(all('i' in r[1] for r in blocks[6][3]))   # the caption is italic
        for bad in ('<div>raw</div>', '| a | b |', '```', 'an **unbalanced emphasis', 'a `code span'):
            with self.assertRaisesRegex(ValueError, 'formats: '):
                fm.blocks('# T\n\n' + bad + '\n', mr.slug)

    def fixture_edition(self):
        root = scratch()
        src, runs = mr.fixture(root)
        outputs = mr.generate(src, runs, 'e1', '2026-10-14', root / 'e1')
        return outputs, json.loads(outputs['generation-record.json'])

    @staticmethod
    def pdf_objects(data):
        """Independent reading of the writer's PDF: {number: (dictionary bytes, decoded stream or None)}."""
        objects = {}
        for number, body in re.findall(rb'(\d+) 0 obj\n(.*?)\nendobj\n', data, re.S):
            m = re.match(rb'(<<.*?>>)\nstream\n(.*)\nendstream$', body, re.S)
            if m:
                head, payload = m.groups()
                length = int(re.search(rb'/Length (\d+)', head)[1])
                assert length == len(payload)
                stream = zlib.decompress(payload) if b'/FlateDecode' in head and b'/Predictor' not in head else payload
                objects[int(number)] = (head, stream)
            else:
                objects[int(number)] = (body, None)
        return objects

    def test_pdf_is_well_formed_and_its_text_reads_back_through_tounicode(self):
        outputs, record = self.fixture_edition()
        data = outputs['MANUAL.Studio.pdf']
        self.assertTrue(data.startswith(b'%PDF-1.7\n') and data.endswith(b'%%EOF\n'))
        start = int(re.search(rb'startxref\n(\d+)\n%%EOF\n$', data)[1])
        self.assertTrue(data[start:].startswith(b'xref\n0 '))
        offsets = [int(v) for v in re.findall(rb'(\d{10}) 00000 n ', data[start:])]
        for number, offset in enumerate(offsets, 1):   # every cross-reference entry points at its object
            self.assertTrue(data[offset:].startswith(b'%d 0 obj\n' % number), number)
        objects = self.pdf_objects(data)
        self.assertEqual(len(offsets), len(objects))
        pages = [n for n, (head, _) in objects.items() if head.startswith(b'<< /Type /Page ')]
        self.assertEqual(record['formats']['pdf']['pages'], len(pages))
        self.assertIn(b'/Count %d' % len(pages), next(h for h, _ in objects.values() if h.startswith(b'<< /Type /Pages')))
        images = [h for h, _ in objects.values() if b'/Subtype /Image' in h]
        self.assertEqual(2, len(images))   # one image object per figure of the fixture
        fonts = {}
        for head, _ in objects.values():
            if b'/Subtype /Type3' in head:
                name = re.search(rb'/Name /(\w+)', head)[1].decode()
                cmap = objects[int(re.search(rb'/ToUnicode (\d+) 0 R', head)[1])][1].decode('ascii')
                fonts[name] = {int(a, 16): chr(int(b, 16)) for a, b in re.findall(r'<([0-9A-F]{2})> <([0-9A-F]{4})>', cmap)}
        self.assertEqual({'FR', 'FB'}, set(fonts))
        text = []
        for page in pages:
            head = objects[page][0]
            content = objects[int(re.search(rb'/Contents (\d+) 0 R', head)[1])][1].decode('latin-1')
            for font, string in re.findall(r'/(F[RB]) [\d.]+ Tf [^(]*\(((?:\\.|[^\\)])*)\) Tj', content):
                raw = re.sub(r'\\([0-7]{3}|.)', lambda m: chr(int(m[1], 8)) if m[1].isdigit() else m[1], string)
                text.append(''.join(fonts[font][ord(c)] for c in raw))
        joined = re.sub(r'\s+', '', ''.join(text))
        markdown = outputs['MANUAL.Studio.md'].decode('utf-8')
        for block in mr.formats.blocks(markdown, mr.slug):
            if block[0] in ('heading', 'figure'):
                runs = block[2] if block[0] == 'heading' else block[3]
                self.assertIn(re.sub(r'\s+', '', ''.join(r[0] for r in runs)), joined)
        self.assertIn('Intro text, see chapter 1.2 (Alpha).'.replace(' ', ''), joined)
        self.assertIn(b'/Subtype /Link', data)   # the contents and chapter links are live

    def test_docx_package_is_complete_and_every_part_resolves(self):
        outputs, record = self.fixture_edition()
        package = zipfile.ZipFile(io.BytesIO(outputs['MANUAL.Studio.docx']))
        names = package.namelist()
        self.assertEqual(sorted(names), names)
        self.assertTrue(all(info.date_time == (2026, 10, 14, 0, 0, 0) for info in package.infolist()))
        parts = {name: package.read(name) for name in names}
        trees = {name: ET.fromstring(data) for name, data in parts.items() if name.endswith(('.xml', '.rels'))}   # all parse
        types = trees['[Content_Types].xml']
        ns_ct = '{http://schemas.openxmlformats.org/package/2006/content-types}'
        overrides = {e.get('PartName').lstrip('/') for e in types.iter(ns_ct + 'Override')}
        defaults = {e.get('Extension') for e in types.iter(ns_ct + 'Default')}
        for name in names:
            if name != '[Content_Types].xml':
                self.assertTrue(name in overrides or name.rsplit('.', 1)[1] in defaults, name)
        self.assertTrue(overrides <= set(names))
        ns_rel = '{http://schemas.openxmlformats.org/package/2006/relationships}'
        relations = {}
        for name, tree in trees.items():
            if name.endswith('.rels'):
                base = name.replace('_rels/', '').replace('.rels', '')
                folder = base.rsplit('/', 1)[0] + '/' if '/' in base else ''
                for rel in tree.iter(ns_rel + 'Relationship'):
                    target = folder + rel.get('Target')
                    self.assertIn(target, names, target)
                    relations[(name, rel.get('Id'))] = target
        document = parts['word/document.xml'].decode('utf-8')
        embeds = set(re.findall(r'r:embed="([^"]+)"', document))
        media = {n for n in names if n.startswith('word/media/')}
        self.assertEqual(media, {relations[('word/_rels/document.xml.rels', e)] for e in embeds})   # every image used
        self.assertEqual({'word/media/1.2-1.png', 'word/media/1.3-1.png'}, media)
        ns_w = '{http://schemas.openxmlformats.org/wordprocessingml/2006/main}'
        paragraphs = []
        for p in trees['word/document.xml'].iter(ns_w + 'p'):
            style = p.find(f'{ns_w}pPr/{ns_w}pStyle')
            paragraphs.append((style.get(ns_w + 'val') if style is not None else None, ''.join(t.text or '' for t in p.iter(ns_w + 't'))))
        self.assertIn(('Title', 'Fixture manual'), paragraphs)
        self.assertIn(('Heading2', '1.2 Alpha'), paragraphs)
        self.assertIn(('Caption', 'Figure 1.2-1. The top bar.'), paragraphs)
        self.assertIn(('Notice', 'Caution. Simulated.'), paragraphs)
        anchors = set(re.findall(r'w:anchor="([^"]+)"', document))
        bookmarks = set(re.findall(r'w:bookmarkStart w:id="\d+" w:name="([^"]+)"', document))
        self.assertTrue(anchors and anchors <= bookmarks)   # every internal link has its heading bookmark
        self.assertEqual({'MANUAL.Studio.docx': mr.sha(outputs['MANUAL.Studio.docx']), 'MANUAL.Studio.pdf': mr.sha(outputs['MANUAL.Studio.pdf'])},
                         {k: v for k, v in record['outputs'].items() if k.startswith('MANUAL.Studio.') and not k.endswith('.md')})

    def test_font_subsets_cover_every_character_of_the_manual_sources(self):
        regular, bold = (json.loads(path.read_bytes()) for path in (mr.formats.FONT_FILES['R'], mr.formats.FONT_FILES['B']))
        self.assertEqual(set(regular['glyphs']), set(bold['glyphs']))
        self.assertEqual(('Regular', 'Bold'), (regular['font']['style'], bold['font']['style']))
        used = set()
        for path in list((ROOT / 'docs/manual').glob('*.json')) + list((ROOT / 'docs/manual/static').glob('*.md')):
            used |= set(path.read_text(encoding='utf-8')) - set('\n')
        self.assertLessEqual(used, set(regular['glyphs']))

if __name__ == '__main__':
    unittest.main()
