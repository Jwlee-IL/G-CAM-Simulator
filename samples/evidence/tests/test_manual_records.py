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

    def test_overlay_crops_to_the_panel_and_keeps_badges_off_text_and_marks(self):
        width, height = 200, 120
        text = (40, 50, 16, 10)                               # a label just left of mark 1
        rgb = self.panel_image(width, height, text)
        w, h, out, crop, flags, operation = mr.derive_figure(width, height, rgb, self.marks([[60, 50, 20, 10], [100, 70, 30, 12]]))
        self.assertEqual([13, 13, 174, 94], crop)            # the whole panel (border at 20 and 179 / 99) + 6 px of background
        self.assertEqual(('panel-edges', []), (operation['cropRule'], flags))
        # Left of mark 1 is the label, so its badge goes above; left of mark 2 sits 3 px from mark 1's frame (reads as mark
        # 1's), so it goes above too.
        self.assertEqual([('above', [45, 12]), ('above', [85, 32])], [(b['side'], b['at']) for b in operation['badges']])
        def at(x, y):
            return tuple(out[(y * w + x) * 3:(y * w + x) * 3 + 3])
        self.assertEqual(mr.RED, at(60 - 13 - 2, 50 - 13 - 2))   # border outside the control's top-left corner
        self.assertEqual((25, 31, 37), at(60 - 13 + 5, 50 - 13 + 5))   # the control itself is not painted
        self.assertTrue(all(at(x - 13, y - 13) == (200, 200, 200)
                            for y in range(text[1], text[1] + text[3]) for x in range(text[0], text[0] + text[2])))
        white = [(x, y) for y in range(h) for x in range(w) if at(x, y) == mr.WHITE]
        self.assertTrue(white and all(45 <= x < 60 and 12 <= y < 33 for x, y in white if x < 70))   # mark 1's digit, above it

    def test_overlay_flags_a_badge_whose_only_free_place_is_on_content(self):
        width, height = 64, 48
        rgb = b''.join(bytes((255, 255, 255) if x % 2 else (0, 0, 0)) for y in range(height) for x in range(width))
        _, _, _, _, flags, _ = mr.derive_figure(width, height, rgb, self.marks([[24, 20, 10, 8]], crop='full'))
        self.assertEqual(['badge-on-content:1'], flags)

    def test_overlay_refuses_a_badge_without_a_free_place(self):
        with self.assertRaisesRegex(ValueError, 'badge-overlap: mark 1 has no place'):
            mr.derive_figure(12, 12, bytes(12 * 12 * 3), self.marks([[2, 2, 8, 8]], crop='full'))

    def test_badge_check_refuses_a_badge_on_another_mark_its_target_a_badge_or_the_edge(self):
        rects = [[10, 10, 9, 6], [36, 20, 9, 6]]
        mr.check_badges(rects, [[22, 0, 11, 16], [49, 20, 15, 21]], 64, 48)
        for badges, code in (([[30, 10, 15, 21], [49, 20, 15, 21]], 'badge-overlap: badge 1 covers mark 2'),
                             ([[8, 8, 15, 21], [49, 20, 15, 21]], 'badge-overlap: badge 1 covers its own target'),
                             ([[22, 0, 11, 16], [37, 21, 6, 4]], 'badge-overlap: badge 2 covers its own target'),
                             ([[22, 0, 11, 16], [25, 4, 5, 5]], 'badge-overlap: badge 1 covers badge 2'),
                             ([[22, 0, 11, 16], [49, 30, 15, 21]], 'badge-bounds: badge 2 outside the figure')):
            with self.assertRaises(ValueError) as caught:
                mr.check_badges(rects, badges, 64, 48)
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
                     'budget-exceeded', 'tampered-figure', 'dpi-double-scaling', 'identity-leak', 'badge-no-room',
                     'badge-over-other-mark', 'badge-over-own-target', 'badge-placement-defect'):
            self.assertIn(name, results)
            self.assertFalse(results[name].startswith('generated'), name)
        self.assertTrue(results['missing-run'].startswith('generated'))
        self.assertTrue(results['failed-chapter'].startswith('generated'))

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


if __name__ == '__main__':
    unittest.main()
