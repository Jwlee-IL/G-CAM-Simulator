"""STR frames, exact-attempt provenance, confined JPEGs and self-contained document coverage; no browser."""
import copy
import io
import json
from pathlib import Path
import sys
import zipfile
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'samples/docs'))
sys.path.insert(0, str(Path(__file__).parent))
from scratch_fixtures import ScratchTestCase, scratch
import software_model as model
import software_formats as formats
import software_set as generator
import ui_annex as annex


def jpeg():
    # Valid baseline 8x8 grayscale JPEG: one zero DC coefficient, EOB, and one-bit Huffman codes.
    def segment(marker, data):
        return bytes([255, marker]) + (len(data) + 2).to_bytes(2, 'big') + data
    counts = bytes([1] + [0] * 15)
    return (b'\xff\xd8' + segment(0xdb, b'\x00' + bytes([1] * 64)) +
            segment(0xc0, bytes([8, 0, 8, 0, 8, 1, 1, 0x11, 0])) +
            segment(0xc4, b'\x00' + counts + b'\x00') + segment(0xc4, b'\x10' + counts + b'\x00') +
            segment(0xda, bytes([1, 1, 0, 0, 63, 0])) + b'\x3f\xff\xd9')


class SoftwareEvidenceTests(ScratchTestCase):
    def setUp(self):
        self.root = scratch('gcam-str-fixture-')
        self.archive = self.root / 'samples/testing/records/M4'
        self.folder = self.archive / 'frames'; self.folder.mkdir(parents=True)
        self.case = {'id': 'Gcam.Studio.UiTests::Gcam.Studio.UiTests.ScenarioTests.Example', 'verdict': 'passed'}
        self.selection = {'caseSelections': {self.case['id']: 'gcam-batch'}}
        evidence = {'steps': [{'id': str(i), 'privacy': False} for i in range(3)],
                    'cues': [{'stepIds': [str(i)], 'text': 'visible ' + str(i), 'start100ns': i * 10_000_000,
                              'end100ns': (i + 1) * 10_000_000} for i in range(3)], 'privacyIntervals': []}
        self.evidence = {'contract': 'v4', 'runId': 'gcam-run', 'executedStepIds': ['0', '1', '2'],
            'videoStatus': {'contract': 'v4', 'runId': 'gcam-run', 'status': 'passed', 'videoSha256': 'a' * 64,
                'canvas': {'widthPx': 8, 'heightPx': 8}, 'check': {'contract': 'v4', 'videoSha256': 'a' * 64, 'passed': True, 'failedCriteria': []}, 'evidence': evidence}}
        self.records = [{'contract': 'v4', 'profile': 'desktop', 'runId': 'gcam-batch',
                         'desktopAllCaseIds': [self.case['id']], 'desktopEvidence': {self.case['id']: self.evidence}}]
        self.row = {'caseId': self.case['id'], 'runId': 'gcam-run', 'verdict': 'passed', 'videoSha256': 'a' * 64,
                    'checkContract': 'v4', 'videoStatus': 'passed', 'reason': None, 'canvasWidthPx': 8, 'canvasHeightPx': 8,
                    'frames': [{'stepId': str(i), 'cueText': 'visible ' + str(i), 'mediaTime100ns': i * 10_000_000 + 9_000_000,
                                'mediaEnd100ns': (i + 1) * 10_000_000, 'file': 'case-' + str(i) + '.jpg',
                                'sha256': model.sha(jpeg()), 'widthPx': 8, 'heightPx': 8} for i in range(3)]}
        for f in self.row['frames']: (self.folder / f['file']).write_bytes(jpeg())
        self.write()

    def write(self):
        (self.folder / 'manifest.json').write_bytes(model.canonical({'schema': 'gcam.str-frames', 'schemaVersion': 1,
            'contract': 'v4', 'cases': [self.row]}))

    def assemble(self):
        with patch.object(annex, 'ROOT', self.root):
            blocks, inputs = annex.annex(self.archive, self.selection, self.records, [self.case])
        docs = {'STR': blocks}; model.LinkMap(docs).rewrite(docs)
        for i, b in enumerate(blocks): b['id'] = 'evidence' + str(i)
        return blocks, inputs

    def metadata(self):
        return {'edition': '2026-10-22-1', 'date': '2026-10-22', 'sourceCommit': 'b' * 40, 'draft': False,
                'treeStatus': 'clean', 'milestone': 'M4', 'testSubject': 'c' * 40,
                'manual': {'deploymentSha256': 'd' * 64}, 'history': {'STR': []}}

    def test_annex_embeds_every_selected_frame_and_caption_in_html_and_docx(self):
        blocks, inputs = self.assemble()
        self.assertEqual(4, len(inputs)); self.assertEqual(3, sum(b['kind'] == 'image' for b in blocks))
        html = formats.render_html('STR', blocks, self.metadata(), {})
        formats.check_html(html, blocks)
        self.assertEqual(3, html.count('src="data:image/jpeg;base64,'))
        word = formats.render_docx('STR', blocks, self.metadata(), {})
        formats.check_docx(word, blocks)
        with zipfile.ZipFile(io.BytesIO(word)) as z:
            self.assertEqual(3, len([n for n in z.namelist() if n.endswith('.jpg')]))
            self.assertIn(b'image/jpeg', z.read('[Content_Types].xml'))
        self.assertIn('M4 test subject', html)

    def test_html_cannot_add_an_unlisted_or_external_image(self):
        blocks, _ = self.assemble(); page = formats.render_html('STR', blocks, self.metadata(), {})
        encoded = next(b['jpeg'] for b in blocks if b['kind'] == 'image')
        for source in ('data:image/jpeg;base64,' + encoded, 'https://example.invalid/image.jpg'):
            with self.subTest(source=source[:32]):
                changed = page.replace('</main>', '<img src="' + source + '"></main>')
                with self.assertRaises(ValueError): formats.check_html(changed, blocks)

    def test_milestone_without_desktop_evidence_has_no_annex(self):
        self.assertEqual(([], set()), annex.annex(self.archive, {'caseSelections': {}}, [], []))

    def test_failed_status_is_listed_with_reason_and_never_illustrated(self):
        self.evidence['videoStatus']['check']['passed'] = False
        self.row.update(reason='video-gate: video check did not pass', frames=[]); self.write()
        blocks, _ = self.assemble()
        self.assertFalse(any(b['kind'] == 'image' for b in blocks))
        self.assertIn('video check did not pass', ' '.join(model.plain(b['runs']) for b in blocks if 'runs' in b))

    def test_missing_export_and_unexecuted_cases_are_listed_with_reasons(self):
        (self.folder / 'manifest.json').unlink()  # this test's own fixture
        self.records[0]['desktopEvidence'] = {}; self.case['verdict'] = 'not_executed'
        blocks, _ = self.assemble()
        text = ' '.join(model.plain(b['runs']) for b in blocks if 'runs' in b)
        self.assertIn(self.case['id'], text); self.assertIn('No frames:', text)
        self.assertIn('not_executed', text)

    def test_passing_v4_case_requires_its_frame_export(self):
        (self.folder / 'manifest.json').unlink()
        with self.assertRaisesRegex(ValueError, 'qualified case export missing'): self.assemble()

    def test_different_attempt_video_hash_is_refused(self):
        self.row['videoSha256'] = 'f' * 64; self.write()
        with self.assertRaisesRegex(ValueError, 'video hash differs'): self.assemble()

    def test_modified_jpeg_is_refused(self):
        (self.folder / self.row['frames'][0]['file']).write_bytes(jpeg() + b'corruption')
        with self.assertRaisesRegex(ValueError, 'JPEG hash differs'): self.assemble()

    def test_manifest_paths_cannot_escape_the_frame_folder(self):
        for path in ('../outside.jpg', '/outside.jpg', 'C:/outside.jpg', 'nested/../../outside.jpg'):
            with self.subTest(path=path):
                self.row['frames'][0]['file'] = path; self.write()
                with self.assertRaises(ValueError): self.assemble()

    def test_private_times_and_mismatched_step_captions_are_refused(self):
        self.evidence['videoStatus']['evidence']['privacyIntervals'] = [{'start100ns': 9_000_000, 'end100ns': 10_000_000}]
        with self.assertRaisesRegex(ValueError, 'private frame'): self.assemble()
        self.evidence['videoStatus']['evidence']['privacyIntervals'] = []
        self.row['frames'][0]['cueText'] = 'another attempt'; self.write()
        with self.assertRaisesRegex(ValueError, 'cue differs'): self.assemble()

    def test_window_transition_times_are_refused_and_old_evidence_has_none(self):
        self.assemble()  # evidence without the field (written before window transitions existed) is accepted as before
        self.evidence['videoStatus']['evidence']['transitionIntervals'] = []
        self.assemble()
        self.evidence['videoStatus']['evidence']['transitionIntervals'] = [
            {'start100ns': 19_000_000, 'end100ns': 20_000_000, 'reason': 'maximise', 'stepIds': ['1']}]
        with self.assertRaisesRegex(ValueError, 'transition frame'): self.assemble()

    def test_incomplete_case_membership_and_frame_counts_are_refused(self):
        self.row['caseId'] = 'other'; self.write()
        with self.assertRaisesRegex(ValueError, 'case membership'): self.assemble()
        self.row['caseId'] = self.case['id']; self.row['frames'] = self.row['frames'][:2]; self.write()
        with self.assertRaisesRegex(ValueError, 'frame count'): self.assemble()

    def test_canvas_must_match_the_exact_run_record(self):
        self.row['canvasWidthPx'] = 9; self.write()
        with self.assertRaisesRegex(ValueError, 'recorded canvas differs'): self.assemble()

    def test_edge_version_queries_the_running_browser_without_reading_its_executable(self):
        from edge_output import Edge
        edge = Edge(self.root / 'browser', executable=self.root / 'not-an-executable')
        with patch.object(edge, 'run', return_value='<html data-edge-version="155.0.4283.62"></html>'):
            self.assertEqual('155.0.4283.62', edge.version())
        with patch.object(edge, 'run', return_value='<html></html>'):
            with self.assertRaisesRegex(ValueError, 'runtime version'): edge.version()

    def test_docx_tampering_cannot_keep_the_original_image_hash(self):
        blocks, _ = self.assemble(); original = formats.render_docx('STR', blocks, self.metadata(), {})
        buffer = io.BytesIO()
        with zipfile.ZipFile(io.BytesIO(original)) as src, zipfile.ZipFile(buffer, 'w') as dst:
            for name in src.namelist(): dst.writestr(name, b'corrupt' if name.endswith('.jpg') else src.read(name))
        with self.assertRaisesRegex(ValueError, 'JPEG hash differs'): formats.check_docx(buffer.getvalue(), blocks)

    def test_docx_relationship_cannot_point_at_another_frames_image(self):
        blocks, _ = self.assemble(); original = formats.render_docx('STR', blocks, self.metadata(), {})
        frames = [b for b in blocks if b['kind'] == 'image']; buffer = io.BytesIO()
        with zipfile.ZipFile(io.BytesIO(original)) as src, zipfile.ZipFile(buffer, 'w') as dst:
            for name in src.namelist():
                raw = src.read(name)
                if name == 'word/_rels/document.xml.rels':
                    raw = raw.replace(('media/' + frames[0]['asset'] + '.jpg').encode(), ('media/' + frames[1]['asset'] + '.jpg').encode())
                dst.writestr(name, raw)
        with self.assertRaisesRegex(ValueError, 'frame relationship differs'): formats.check_docx(buffer.getvalue(), blocks)

    def test_pdf_requires_exact_jpeg_streams_placements_and_captions(self):
        from test_software_set import pdf_fixture
        from edge_output import EdgePdf
        blocks, _ = self.assemble()
        text = ' '.join(t for b in blocks for _, t, _ in formats.segments(b))
        pdf = EdgePdf(pdf_fixture(text=text, jpeg_image=jpeg()))
        pdf.check_evidence(blocks)
        with self.assertRaisesRegex(ValueError, 'JPEG hash/count'):
            EdgePdf(pdf_fixture(text=text, jpeg_image=jpeg(), image_uses=2)).check_evidence(blocks)
        different = jpeg().replace(bytes([1] * 64), bytes([2] * 64), 1)
        with self.assertRaisesRegex(ValueError, 'JPEG hash/count'):
            EdgePdf(pdf_fixture(text=text, jpeg_image=different)).check_evidence(blocks)
        with self.assertRaisesRegex(ValueError, 'caption/case missing'):
            EdgePdf(pdf_fixture(text='omitted', jpeg_image=jpeg())).check_evidence(blocks)

    def test_pdf_refuses_a_long_token_clipped_at_the_margin(self):
        from test_software_set import pdf_fixture, marked
        from edge_output import EdgePdf
        case = 'Gcam.Studio.UiTests::Gcam.Studio.UiTests.CompareScenarioTests.PhysicalPrefix_KeepTheirPopulationLabels'
        blocks = marked('# Fixture\n\n### ' + case + '\n\nRun: gcam-run; checked video SHA-256: ' + 'a' * 64 + '.\n\n'
                        'Repeated: ' + case + '\n')
        whole = ' '.join(t for b in blocks for _, t, _ in formats.segments(b))
        EdgePdf(pdf_fixture(text=whole)).check_tokens(blocks)
        clipped = whole.replace(case + ' Run:', case[:-12] + ' Run:', 1)  # the heading stops at the right margin
        with self.assertRaisesRegex(ValueError, 'pdf: long token clipped or missing'):
            EdgePdf(pdf_fixture(text=clipped)).check_tokens(blocks)  # refused although the token occurs once elsewhere
        with self.assertRaisesRegex(ValueError, 'pdf: long token clipped or missing'):
            EdgePdf(pdf_fixture(text=whole.replace('a' * 64, 'a' * 57))).check_tokens(blocks)

    def test_m4_assembly_uses_its_selected_subject_catalog_and_evidence_in_part_a(self):
        import shutil
        tr = model.testing_module()
        selection, records, cases = tr.load_selection(ROOT / 'samples/testing/records/M3')
        selection, records, cases = copy.deepcopy((selection, records, cases))
        for record in records: record['source']['commit'] = 'e' * 40
        case = next(c for c in cases if '.ScenarioTests.' in c['id'])
        batch = next(r for r in records if r['runId'] == selection['caseSelections'][case['id']])
        batch.update(profile='desktop', contract='v4', desktopAllCaseIds=[case['id']], desktopEvidence={case['id']: self.evidence})
        self.row.update(caseId=case['id'], verdict=case['verdict']); self.write()
        for name in ('catalog.md', 'report.md'):
            shutil.copyfile(ROOT / 'samples/testing/records/M3' / name, self.archive / name)
        docs = self.root / 'docs'; docs.mkdir()
        shutil.copyfile(self.archive / 'report.md', docs / 'VV.Tests.Results.md')  # the report a repo publishing M4 holds
        for name in ('SRS', 'SDS', '', 'Acquisition', 'Imaging', 'Detector', 'Waveform', 'History'):
            filename = 'VV.Studio' + ('.' + name if name else '') + '.md'
            shutil.copyfile(ROOT / 'docs' / filename, docs / filename)
        manual = docs / 'manual/editions/fixture'; manual.mkdir(parents=True)
        (manual / 'generation-record.json').write_bytes(model.canonical({'editionId': 'fixture', 'date': '2026-10-22',
            'subject': {'deploymentSha256': 'f' * 64}, 'formats': {'pdf': {'pages': 1}}}))
        for ext in ('md', 'pdf', 'docx'): (manual / ('MANUAL.Studio.' + ext)).write_bytes(b'fixture')
        with patch.object(model, 'ROOT', self.root), patch.object(annex, 'ROOT', self.root), \
             patch.object(tr, 'load_selection', return_value=(selection, records, cases)) as load:
            documents, provenance, paths = model.assemble('M4')
        load.assert_called_once_with(self.archive)
        self.assertEqual(('M4', 'e' * 40), (provenance['milestone'], provenance['testSubject']))
        self.assertIn('samples/testing/records/M4/frames/manifest.json', paths)
        headings = [model.plain(b['runs']) for b in documents['STR'] if b['kind'] == 'heading']
        self.assertLess(headings.index('UI automation evidence'), next(i for i, h in enumerate(headings) if h.startswith('Part B')))
        self.assertEqual(3, sum(b['kind'] == 'image' for b in documents['STR']))
