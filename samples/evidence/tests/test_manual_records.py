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

    def test_overlay_crops_around_marks_and_draws_numbered_badges(self):
        width, height = 200, 120
        rgb = bytes(width * height * 3)
        w, h, out, crop, flags = mr.derive_figure(width, height, rgb, self.marks([[60, 50, 20, 10], [100, 70, 30, 12]]))
        self.assertEqual([36, 26, 118, 80], crop)            # union ± 24 px margin
        self.assertEqual((118, 80, []), (w, h, flags))
        def at(x, y):
            return tuple(out[(y * w + x) * 3:(y * w + x) * 3 + 3])
        self.assertEqual(mr.RED, at(60 - 36 - 2, 50 - 26 - 2))  # border outside the control's top-left corner
        self.assertEqual((0, 0, 0), at(60 - 36 + 5, 50 - 26 + 5))   # the control itself is not painted
        badge = [(x, y) for y in range(h) for x in range(w) if at(x, y) == mr.WHITE]
        self.assertTrue(badge and all(y < 50 - 26 for x, y in badge[:15]))   # mark 1's digit sits above its rectangle

    def test_overlay_flags_a_badge_without_room(self):
        _, _, _, _, flags = mr.derive_figure(12, 12, bytes(12 * 12 * 3), self.marks([[2, 2, 8, 8]], crop='full'))
        self.assertEqual(['badge-placement:1'], flags)

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
                     'budget-exceeded', 'tampered-figure', 'dpi-double-scaling', 'identity-leak'):
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
