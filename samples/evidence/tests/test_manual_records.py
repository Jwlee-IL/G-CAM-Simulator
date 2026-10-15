"""Manual edition generator: PNG codec, mark geometry, overlays, selection and the repository sources. No desktop."""
import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest
import uuid
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
        self.assertEqual({chr(c) for c in range(32, 127)} | set('·×–…'), set(subset['glyphs']))
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
        self.assertEqual(8, len(chapters))
        self.assertEqual({'1.1', '5.2'}, {n for n, c in chapters.items() if c['source'] == 'static'})
        self.assertTrue(all(s['draft'] for s in sources['static'].values()), 'static chapters stay drafts until approved')
        self.assertEqual('draft', sources['noticeStatus'])
        for notice in sources['notices'].values():
            for number in notice['chapters']:
                if chapters[number]['source'] == 'static':
                    self.assertIn(f'[[notice:{notice["id"]}]]', sources['static'][number]['body'])

    def test_self_test_refuses_every_seeded_defect(self):
        results = dict(mr.self_test(scratch() / 'self-test'))
        for name in ('missing-run-formal', 'failed-chapter-formal', 'evidence-hash', 'mark-outside-image', 'mixed-binaries',
                     'budget-exceeded', 'tampered-figure', 'dpi-double-scaling', 'identity-leak', 'label-no-room',
                     'label-overlap', 'label-over-own-target', 'label-outside-figure', 'label-placement-defect',
                     'label-without-glyph'):
            self.assertIn(name, results)
            self.assertFalse(results[name].startswith('generated'), name)
        self.assertTrue(results['missing-run'].startswith('generated'))
        self.assertTrue(results['failed-chapter'].startswith('generated'))
        self.assertTrue(results['label-margin-bottom'].startswith('generated: margin-bottom'))
        self.assertTrue(results['label-margin-right'].startswith('generated: margin-right'))
        self.assertTrue(results['label-wrapped'].startswith('generated'))

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
        self.assertEqual([(1, 'Start')], [(l['n'], l['text']) for l in operation['labels']])
        self.assertTrue(operation['labels'][0]['candidate'].startswith('right'))
        width, height, _ = mr.png_decode(outputs['figures/1.2-1.png'])
        self.assertEqual([width, height], operation['size'])
        self.assertEqual(operation['crop'][2:], operation['size'])   # no margin: the labels add no space

if __name__ == '__main__':
    unittest.main()
