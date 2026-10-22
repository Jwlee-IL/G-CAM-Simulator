"""Software-set dialect, source-aware references, OOXML, privacy and limited Edge-PDF oracles. No browser or desktop."""
import copy
import io
import json
from pathlib import Path
import re
import struct
import sys
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET
import zipfile
import zlib

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'samples/docs'))
import software_model as model
import software_formats as formats
from edge_output import EdgePdf, pdf_literal, privacy
import software_set as generator


def marked(text, source='docs/fixture.md'):
    docs = {'SRS': model.parse(text, source)}
    links = model.LinkMap(docs); links.rewrite(docs)
    for i, b in enumerate(docs['SRS']):
        b['id'] = 'fixture' + str(i)
    return docs['SRS']


def meta():
    return {'edition': '2026-10-20-1', 'date': '2026-10-20', 'sourceCommit': 'a' * 40,
            'draft': False, 'treeStatus': 'clean at ' + 'a' * 40,
            'testSubject': model.M3_SUBJECT, 'manual': {'deploymentSha256': 'b' * 64},
            'history': {'SRS': [['docs/fixture.md', 'aaaaaaa 2026-10-01', 'bbbbbbb 2026-10-20']]}}


def change_zip(data, transform):
    output = io.BytesIO()
    with zipfile.ZipFile(io.BytesIO(data)) as src, zipfile.ZipFile(output, 'w') as dst:
        for name in src.namelist():
            dst.writestr(name, transform(name, src.read(name)))
    return output.getvalue()


def pdf_fixture(text='GCAM-SW-SRS Demo banner', action=False, cmap_array=False, literal=False, type3=False, actual=False, jpeg_image=None, image_uses=3):
    # Independent minimal PDF oracle with real offsets, indirect resources, compressed streams and a ToUnicode map.
    chars = sorted(set(text)); mapping = {c: i + 1 for i, c in enumerate(chars)}
    pairs = '\n'.join(f'<{n:04X}> <{ord(c):04X}>' for c, n in mapping.items())
    if cmap_array == 'base':
        cmap = f'{len(chars)} beginbfrange\n' + '\n'.join(f'<{n:04X}> <{n:04X}> <{ord(c):04X}>' for c, n in mapping.items()) + '\nendbfrange'
    elif cmap_array:
        cmap = f'1 beginbfrange\n<0001> <{len(chars):04X}> [' + ' '.join(f'<{ord(c):04X}>' for c in chars) + ']\nendbfrange'
    else:
        cmap = f'{len(chars)} beginbfchar\n{pairs}\nendbfchar'
    if type3:
        cmap = '1 begincodespacerange\n<00> <FF>\nendcodespacerange\n' + cmap
    encoded = b''.join(mapping[c].to_bytes(1 if type3 else 2, 'big') for c in text)
    if literal:
        encoded = b''.join(('\\%03o' % b).encode() for b in encoded)
        show = b'(' + encoded + b') Tj'
    else:
        show = b'<' + encoded.hex().encode() + b'> Tj'
    if jpeg_image is not None:
        show += b' ET\n' + b'/Photo Do\n' * image_uses + b'BT '
    def stream(data):
        raw = zlib.compress(data)
        return b'<< /Length ' + str(len(raw)).encode() + b' /Filter /FlateDecode >>\nstream\n' + raw + b'\nendstream'
    if actual:
        show += b' /Span<</ActualText <FEFF00780302> >> BDC <0000> Tj EMC'
    objects = [b'<< /Type /Catalog /Pages 2 0 R >>', b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
               b'<< /Type /Page /Parent 2 0 R /Resources 7 0 R /MediaBox [0 0 595 842] /Contents [4 0 R] >>',
               stream(b'BT /F1 10 Tf ' + show + b' ET'), b'<< /Type /Font /Subtype /' + (b'Type3' if type3 else b'Type0') + b' /ToUnicode 6 0 R >>',
               stream(cmap.encode()), b'<< /Font << /F1 5 0 R >> >>']
    if jpeg_image is not None:
        objects[6] = b'<< /Font << /F1 5 0 R >> /XObject << /Photo 8 0 R >> >>'
        objects.append(b'<< /Type /XObject /Subtype /Image /Width 8 /Height 8 /Filter /DCTDecode /Length ' + str(len(jpeg_image)).encode() + b' >>\nstream\n' + jpeg_image + b'\nendstream')
    if action:
        objects.append(b'<< /Type /Action /S /URI /URI (https://example.invalid/) >>')
    result, offsets = bytearray(b'%PDF-1.7\n'), [0]
    for i, obj in enumerate(objects, 1):
        offsets.append(len(result)); result.extend(f'{i} 0 obj\n'.encode() + obj + b'\nendobj\n')
    start = len(result)
    result.extend(f'xref\n0 {len(offsets)}\n0000000000 65535 f \n'.encode())
    for offset in offsets[1:]:
        result.extend(f'{offset:010} 00000 n \n'.encode())
    result.extend(f'trailer\n<< /Size {len(offsets)} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n'.encode())
    return bytes(result)


class SoftwareSetTests(unittest.TestCase):
    def test_table_escaped_pipe_in_code_and_missing_trailing_cells(self):
        blocks = model.parse('| ID | Regex | Note |\n|---|---|---|\n| a | `^x\\|y$` |\n', 'docs/a.md')
        self.assertEqual(3, len(blocks[0]['rows'][0]))
        self.assertEqual('^x|y$', model.plain(blocks[0]['rows'][0][1]))
        self.assertEqual('', model.plain(blocks[0]['rows'][0][2]))

    def test_fenced_comment_is_literal_not_a_heading(self):
        blocks = model.parse('# Real\n\n```powershell\n# not a heading\n<message>\n```\n', 'docs/a.md')
        self.assertEqual(['heading', 'code'], [b['kind'] for b in blocks])
        self.assertEqual('# not a heading\n<message>', blocks[1]['text'])

    def test_inline_emphasis_links_placeholders_and_math(self):
        runs = model.inline('**bold *inner* text**; H*(10); xoshiro256**; 2 * x; `a_b`; <message>; [ref](a.md#x)')
        self.assertEqual('bold inner text; H*(10); xoshiro256**; 2 * x; a_b; <message>; ref', model.plain(runs))
        self.assertIn({'b', 'i'}, [set(r['styles']) for r in runs])
        self.assertEqual('a.md#x', runs[-1]['target'])

    def test_comments_dropped_and_flat_list_continuations(self):
        blocks = model.parse('<!-- hidden\nmetadata -->\n- one\n  continued\n- two\n', 'docs/a.md')
        self.assertEqual(['one continued', 'two'], [model.plain(r) for r in blocks[0]['items']])
        self.assertEqual('before  after', model.plain(model.inline('before <!-- hidden --> after')))

    def test_unsupported_dialect_is_refused(self):
        for text in ['- one\n  - nested\n', '1. ordered\n', '> quote\n', '~~~text\nx\n~~~',
                     '<div>HTML</div>', '![image](a.png)', '[^n]', '[ref][id]', '[id]: a.md',
                     '- [x] task', '    indented code', '~~strike~~', '#'*5 + ' deep', 'Title\n===']:
            with self.subTest(text=text), self.assertRaisesRegex(ValueError, 'dialect:'):
                model.parse(text, 'docs/a.md')

    def test_malformed_tables_fences_comments_and_links_are_refused(self):
        for text in ['|a|b|\n|---|---|\n|1|2|3|', '|a|\n|x|', '```text\nx', '<!-- never closed',
                     'a `unterminated', '[bad](missing', '**unclosed', '[a](a.md "title")']:
            with self.subTest(text=text), self.assertRaisesRegex(ValueError, 'dialect:'):
                model.parse(text, 'docs/a.md')

    def test_link_map_history_annex_file_only_and_duplicate_headings(self):
        docs = {'SRS': model.parse('# SRS\n\n[history](VV.Studio.History.md#dated) '
                                  '[annex](VV.Studio.Imaging.md#same) [file](VV.Studio.History.md) '
                                  '[outside](DESIGN.Other.md) [local](#srs)', 'docs/VV.Studio.SRS.md'),
                'STR': model.parse('# History\n## Dated', 'docs/VV.Studio.History.md'),
                'VVR': model.parse('# Imaging\n## Same\n## Same', 'docs/VV.Studio.Imaging.md')}
        links = model.LinkMap(docs); links.rewrite(docs)
        text = model.plain(docs['SRS'][1]['runs'])
        self.assertIn('history (GCAM-SW-STR, Dated)', text)
        self.assertIn('annex (GCAM-SW-VVR, Same)', text)
        self.assertIn('file (GCAM-SW-STR)', text)
        self.assertIn('outside local', text)
        self.assertNotIn('§', text)
        self.assertEqual(1, sum('link' in r for r in docs['SRS'][1]['runs']))
        self.assertNotEqual(docs['VVR'][1]['bookmark'], docs['VVR'][2]['bookmark'])

    def test_link_map_namespaces_duplicate_headings_between_sources(self):
        docs = {'SRS': model.parse('# Same', 'docs/a.md'), 'SDS': model.parse('# Same', 'docs/b.md')}
        model.LinkMap(docs)
        self.assertNotEqual(docs['SRS'][0]['bookmark'], docs['SDS'][0]['bookmark'])

    def test_link_map_refuses_missing_included_anchor_and_path_escape(self):
        links = model.LinkMap({'SRS': model.parse('# A', 'docs/a.md')})
        with self.assertRaisesRegex(ValueError, 'unresolved anchor'):
            links.resolve('docs/a.md', 'a.md#absent')
        with self.assertRaisesRegex(ValueError, 'path escape'):
            links.resolve('docs/a.md', '../../../escape.md')
        self.assertIsNone(links.resolve('docs/a.md', 'https://example.invalid/'))

    def test_html_body_cells_code_and_links_are_independently_covered(self):
        doc = marked('# Fixture\n\nA <message> and **bold**.\n\n|A|B|\n|---|---|\n|x|y|\n\n```text\nline\n  next\n```')
        output = formats.render_html('SRS', doc, meta(), {})
        formats.check_html(output, doc)
        self.assertIn('&lt;message&gt;', output)
        for changed in [output.replace('>y</td>', '>lost</td>'), output.replace('  next', 'next')]:
            with self.assertRaisesRegex(ValueError, 'coverage differs'):
                formats.check_html(changed, doc)

    def test_html_outgoing_link_is_refused(self):
        doc = marked('# Fixture')
        text = formats.render_html('SRS', doc, meta(), {}).replace('</main>', '<a href="https://example.invalid/">bad</a></main>')
        with self.assertRaisesRegex(ValueError, 'outgoing'):
            formats.check_html(text, doc)

    def test_docx_parts_relationships_fields_and_exact_content(self):
        doc = marked('# Fixture\n\nA **bold** [self](#fixture).\n\n- item\n\n|A|B|\n|---|---|\n|x|y|\n\n```text\nx\n y\n```')
        data = formats.render_docx('SRS', doc, meta(), {})
        self.assertEqual(len(formats.expected(doc)), formats.check_docx(data, doc))
        with zipfile.ZipFile(io.BytesIO(data)) as z:
            self.assertIn('NUMPAGES', z.read('word/footer1.xml').decode())
            self.assertIn('tblHeader', z.read('word/document.xml').decode())
            self.assertNotIn('TOC ', z.read('word/document.xml').decode())

    def test_docx_broken_relationship_and_missing_cell_are_refused(self):
        doc = marked('# Fixture\n\n|A|B|\n|---|---|\n|x|y|')
        data = formats.render_docx('SRS', doc, meta(), {})
        broken = change_zip(data, lambda name, raw: raw.replace(b'Target="styles.xml"', b'Target="absent.xml"')
                            if name.endswith('.rels') else raw)
        with self.assertRaisesRegex(ValueError, 'relationship target'):
            formats.check_docx(broken, doc)
        changed = change_zip(data, lambda name, raw: raw.replace(b'>y</w:t>', b'>lost</w:t>') if name == 'word/document.xml' else raw)
        with self.assertRaisesRegex(ValueError, 'coverage differs'):
            formats.check_docx(changed, doc)

    def test_docx_outgoing_relationship_is_refused(self):
        doc = marked('# Fixture'); data = formats.render_docx('SRS', doc, meta(), {})
        broken = change_zip(data, lambda name, raw: raw.replace(b'Target="styles.xml"', b'Target="styles.xml" TargetMode="External"')
                            if name.endswith('.rels') else raw)
        with self.assertRaisesRegex(ValueError, 'outgoing'):
            formats.check_docx(broken, doc)

    def test_docx_diagram_image_is_embedded_with_its_source_and_relationship(self):
        doc = marked('# Fixture\n\n```mermaid\nflowchart LR\nA-->B\n```')
        block = doc[1]; block['asset'] = 'fixture-diagram'
        def chunk(kind, data):
            return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))
        png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 1, 1, 8, 2, 0, 0, 0))
        png += chunk(b'IDAT', zlib.compress(b'\0\xff\xff\xff')) + chunk(b'IEND', b'')
        data = formats.render_docx('SRS', doc, meta(), {'fixture-diagram': {'png': png}})
        formats.check_docx(data, doc)
        with zipfile.ZipFile(io.BytesIO(data)) as z:
            self.assertEqual(png, z.read('word/media/fixture-diagram.png'))
            xml = z.read('word/document.xml').decode()
            self.assertNotIn('Diagram source (Mermaid)', xml)
            self.assertNotIn('flowchart LR', xml)
            self.assertIn('Diagram rendered from the source Mermaid block.', xml)
            extent = ET.fromstring(xml).find('.//{http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing}extent')
            self.assertLessEqual(int(extent.get('cx')), 6120130)
            self.assertLessEqual(int(extent.get('cy')), 4320000)

    def test_required_edition_id_and_dirty_tree_gate_before_writing(self):
        with patch.object(sys, 'argv', ['software_set.py', 'generate', '--out', 'unused']), \
                patch('sys.stderr', new_callable=io.StringIO):
            with self.assertRaises(SystemExit) as caught:
                generator.main()
        self.assertEqual(2, caught.exception.code)
        for bad in ['', 'edition', '2026-10-20', '2026-02-30-1', '2026-10-20-0']:
            with self.subTest(edition=bad), self.assertRaises(ValueError):
                generator.generation_identity(bad, False, 'a' * 40, [])
        with self.assertRaisesRegex(ValueError, '--draft is required'):
            generator.generation_identity('2026-10-20-1', False, 'a' * 40, [' M fixture.py'])
        clean = generator.generation_identity('2026-10-20-1', False, 'a' * 40, [])
        self.assertEqual('clean at ' + 'a' * 40, clean['treeStatus'])
        self.assertFalse(clean['draft'])
        draft = generator.generation_identity('2026-10-20-1', True, 'a' * 40, [' M fixture.py'])
        self.assertTrue(draft['draft'])
        self.assertEqual('uncommitted changes present', draft['treeStatus'])

    def test_revision_tree_status_and_draft_on_covers_and_headers(self):
        doc = marked('# Fixture')
        for draft in (False, True):
            identity = meta(); identity['draft'] = draft
            identity['treeStatus'] = 'uncommitted changes present' if draft else 'clean at ' + 'a' * 40
            output = formats.render_html('SRS', doc, identity, {})
            self.assertIn('2026-10-20-1', output)
            self.assertIn(identity['treeStatus'], output)
            self.assertEqual(draft, '<p class="draft">DRAFT</p>' in output)
            self.assertEqual(draft, 'content: "DRAFT · GCAM-SW-SRS' in output)
            with zipfile.ZipFile(io.BytesIO(formats.render_docx('SRS', doc, identity, {}))) as z:
                for part in ('word/document.xml', 'word/header1.xml'):
                    text = ''.join(ET.fromstring(z.read(part)).itertext())
                    self.assertIn('2026-10-20-1', text)
                    self.assertEqual(draft, 'DRAFT' in text)

    def test_all_empty_table_headers_are_omitted_in_both_formats(self):
        doc = marked('# Fixture\n\n| | |\n|---|---|\n|Purpose|Description|')
        page = formats.render_html('SRS', doc, meta(), {})
        formats.check_html(page, doc)
        body_table = re.search(r'id="block-fixture1">(.*?)</section>', page, re.S)[1]
        self.assertNotIn('<thead>', body_table)
        self.assertNotIn('<th ', body_table)
        self.assertNotIn('fixture1r0', page)
        self.assertEqual(2, len(formats.widths(doc[1])))
        data = formats.render_docx('SRS', doc, meta(), {}); formats.check_docx(data, doc)
        with zipfile.ZipFile(io.BytesIO(data)) as z:
            root = ET.fromstring(z.read('word/document.xml')); table = root.find('.//w:tbl', formats.NS)
            self.assertEqual(1, len(table.findall('w:tr', formats.NS)))
            self.assertIsNone(table.find('.//w:tblHeader', formats.NS))
            self.assertIsNone(table.find('.//w:shd', formats.NS))

    def test_diagram_is_full_width_without_literal_source_listing(self):
        doc = marked('# Fixture\n\n```mermaid\nflowchart LR\nA-->B\n```'); doc[1]['asset'] = 'diagram-fixture'
        page = formats.render_html('SRS', doc, meta(), {'diagram-fixture': {'svg': '<svg xmlns="http://www.w3.org/2000/svg"/>'}})
        formats.check_html(page, doc)
        self.assertNotIn('flowchart LR', page)
        self.assertNotIn('Diagram source (Mermaid)', page)
        self.assertIn('Diagram rendered from the source Mermaid block.', page)
        css = (ROOT / 'samples/docs/software.css').read_text(encoding='utf-8')
        self.assertRegex(css, r'figure img \{[^}]*width: 100%')
        self.assertIn('max-height: 120mm', css)
        with self.assertRaisesRegex(ValueError, 'diagram'):
            formats.check_html(page.replace('data-diagram=', 'data-missing='), doc)

    def test_result_groups_short_names_fixed_numeric_columns_and_complete_membership(self):
        docs, _, _ = model.assemble()
        tables = [b for b in docs['STR'] if b.get('layout') == 'results']
        units = [u for b in tables for u in b['unitIds']]
        self.assertEqual(239, len(set(units)))
        self.assertEqual(8, len(tables))
        for b in tables:
            self.assertEqual([104 / 170, 22 / 170, 22 / 170, 22 / 170], formats.widths(b))
            self.assertEqual(['Class / configuration', 'Passed', 'Failed', 'Not executed'], [model.plain(r) for r in b['headers']])
            self.assertTrue(all('::' not in model.plain(row[0]) and not model.plain(row[0]).startswith(b['family'] + '.') for row in b['rows']))
        doc = marked('# Fixture'); table = copy.deepcopy(tables[0]); table['id'] = 'results'; doc.append(table)
        page = formats.render_html('SRS', doc, meta(), {}); formats.check_html(page, doc)
        self.assertIn('class="result-table"', page)
        self.assertIn('width:12.941%', page)
        data = formats.render_docx('SRS', doc, meta(), {}); formats.check_docx(data, doc)
        with zipfile.ZipFile(io.BytesIO(data)) as z:
            self.assertIn('<w:noWrap/>', z.read('word/document.xml').decode())
        self.assertEqual(13, len(generator.edition_files()))
        self.assertFalse(any(n.endswith(('.svg', '.png')) or n == 'model.json' for n in generator.edition_files()))

    def test_pdf_reader_indirect_resources_unicode_and_contents_array(self):
        text = 'GCAM-SW-SRS Demo banner Σ μ √ x̂'
        pdf = EdgePdf(pdf_fixture(text))
        self.assertEqual([text], pdf.texts())
        self.assertEqual(1, pdf.check('Demo banner', 'GCAM-SW-SRS'))

    def test_pdf_reader_bfrange_array_and_octal_literal_strings(self):
        text = 'GCAM-SW-SRS Demo banner'
        self.assertEqual([text], EdgePdf(pdf_fixture(text, cmap_array=True, literal=True)).texts())
        self.assertEqual(b'a\nb(c)\\d', pdf_literal(rb'a\nb\(c\)\\d'))

    def test_pdf_type3_codespaces_actualtext_and_numeric_bfrange(self):
        text = 'GCAM-SW-SRS Demo banner'
        self.assertEqual([text], EdgePdf(pdf_fixture(text, type3=True)).texts())
        self.assertEqual([text + 'x̂'], EdgePdf(pdf_fixture(text, cmap_array='base', actual=True)).texts())

    def test_pdf_reader_refuses_corruption_unsupported_encoding_and_actions(self):
        data = pdf_fixture()
        for bad in [data[:-6], data.replace(b'%PDF-', b'%BAD-'), data.replace(b'/Type /Font', b'/Encrypt /X'),
                    data.replace(b'/Type /Font', b'/ObjStm /X'), pdf_fixture(action=True),
                    data.replace(b'/FlateDecode', b'/LZWDecode')]:
            with self.subTest(prefix=bad[:15]), self.assertRaises(ValueError):
                EdgePdf(bad).texts()
        with self.assertRaisesRegex(ValueError, 'cover banner'):
            EdgePdf(data).check('absent', 'GCAM-SW-SRS')

    def test_privacy_paths_accounts_and_namespace_urls(self):
        privacy('http://schemas.openxmlformats.org/wordprocessingml/2006/main', account='fixtureaccount')
        for bad in ['Z:' + '/private/input', 'Z:' + '\\private\\input', 'file:' + '///input',
                    '/' + 'home/account/input', 'hello fixtureaccount']:
            with self.subTest(text=bad), self.assertRaisesRegex(ValueError, 'privacy:'):
                privacy(bad, account='fixtureaccount')

    def test_assembly_pins_m3_membership_outcomes_and_history_redactions(self):
        docs, provenance, inputs = model.assemble()
        self.assertEqual({'passed': 1534, 'failed': 1, 'not_executed': 53}, provenance['totals'])
        self.assertEqual(239, provenance['unitCount'])
        self.assertEqual({'A': 101, 'B': 138}, provenance['partUnits'])
        self.assertEqual(3, sum(b['kind'] == 'diagram' for b in docs['SDS']))
        text = '\n'.join(t for b in docs['STR'] for _, t, _ in formats.segments(b))
        self.assertIn('System.TimeoutException', text)
        self.assertIn('optional_vectors_off', text)
        self.assertIn('History annex', text)
        self.assertIn('[local path omitted]', text)
        self.assertEqual(11, provenance['privacyRedactions'][0]['count'])
        privacy(text)
        self.assertIn('samples/testing/records/M3/catalog.md', inputs)
        self.assertNotIn('docs/VV.Tests.md', inputs)

    def test_catalog_narrative_preserves_combined_oracle_tolerance_and_gates(self):
        catalog = (ROOT / 'samples/testing/records/M3/catalog.md').read_text(encoding='utf-8')
        entries = model.catalog_sections(catalog)
        self.assertEqual(239, len(entries))
        combined = [n for e, n in entries if e['id'].endswith('CompareWorkspaceTests')][0]
        self.assertIn('Oracle / tolerance:', combined)
        self.assertIn('five-second waits are deadlock deadlines', combined)
        self.assertIn('Gates: none.', combined)

    def test_selected_cases_cannot_silently_miss_catalog_units(self):
        with self.assertRaisesRegex(ValueError, 'unresolved selected case'):
            model.case_unit({'id': 'assembly::Class.Missing'}, {'assembly::Other': {}})
        self.assertEqual('assembly::Class', model.case_unit({'id': 'assembly::Class.Method'}, {'assembly::Class': {}}))


if __name__ == '__main__':
    unittest.main()
