"""Synthetic trace and policy negative controls. No registry, application, network or ETW access."""
import copy
import gzip
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]
SCRIPT = ROOT / 'tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py'
spec = importlib.util.spec_from_file_location('gcam_stage0', SCRIPT)
stage0 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(stage0)


class StageZeroIsolationTests(unittest.TestCase):
    def test_numeric_opcode_identity_wins_over_wrong_rendering_label(self):
        import xml.etree.ElementTree as ET
        event = self.real_event('90cbdc39-4a3e-11d1-84f4-0000f80464e3', '68', {'FileObject': '0x123', 'TTID': 100})
        ET.SubElement(ET.SubElement(event, 'RenderingInfo'), 'Opcode').text = 'Read'
        self.assertEqual('WriteFile', stage0.parse_event(event)['operation'])
        event.find('System/Opcode').text = '999'
        event.find('RenderingInfo/Opcode').text = 'Write'
        self.assertEqual('unknown', stage0.parse_event(event)['operation'])

    def payload_fixture(self, guid, opcode, raw, fields, version='3'):
        import xml.etree.ElementTree as ET
        root = self.scratch(); schema = root / 'schema.man'
        manifest = ET.Element('instrumentationManifest'); provider = ET.SubElement(manifest, 'provider')
        ET.SubElement(ET.SubElement(provider, 'tasks'), 'task', name='task', eventGUID=guid)
        ET.SubElement(ET.SubElement(provider, 'opcodes'), 'opcode', name='op', mofValue=str(opcode))
        template = ET.SubElement(ET.SubElement(provider, 'templates'), 'template', tid='template')
        for name, kind in fields: ET.SubElement(template, 'data', name=name, inType=kind)
        ET.SubElement(ET.SubElement(provider, 'events'), 'event', task='task', opcode='op', version=version, template='template')
        ET.ElementTree(manifest).write(schema)
        event = self.real_event(guid, opcode, error=True)
        ET.SubElement(event.find('System'), 'Version').text = version
        error = event.find('ProcessingErrorData'); error.find('ErrorCode').text = '15005'
        ET.SubElement(error, 'EventPayload').text = raw.hex()
        return event, stage0.load_normalisation(schema)

    def test_thread_payload_identity_requires_schema_version_pointer_width_and_length(self):
        raw = (10).to_bytes(4, 'little') + (100).to_bytes(4, 'little') + bytes(64) + 'fixture\0'.encode('utf-16-le')
        event, rules = self.payload_fixture('3d6fa8d1-fe05-11d0-9dda-00c04fd7ba7c', '1', raw,
                                          [('ProcessId', 'win:UInt32'), ('TThreadId', 'win:UInt32')])
        state = {'threads': {}, 'files': {}, 'pointerSize': 8}
        row = stage0.parse_event(event, rules, state)
        self.assertFalse(row['decodeError']); self.assertEqual(10, row['pid'])
        self.assertEqual(10, state['threads'][100]['pid'])
        self.assertEqual('15005', row['fields']['sourceDecodeError'])
        self.assertTrue(row['fields']['uninterpretedPayloadSha256'])
        self.assertEqual('unknown', stage0.parse_event(event, correlation=state)['operation'])
        self.assertEqual('unknown', stage0.parse_event(event, rules, {'pointerSize': 4, 'threads': {}, 'files': {}})['operation'])
        short, short_rules = self.payload_fixture('3d6fa8d1-fe05-11d0-9dda-00c04fd7ba7c', '1', raw[:8],
                                                 [('ProcessId', 'win:UInt32'), ('TThreadId', 'win:UInt32')])
        self.assertEqual('unknown', stage0.parse_event(short, short_rules, state)['operation'])
        event.find('System/Version').text = '99'
        self.assertEqual('unknown', stage0.parse_event(event, rules, state)['operation'])

    def test_file_readwrite_payload_uses_v3_offsets_and_does_not_guess_tail(self):
        layout = stage0.load_normalisation()['binaryLayouts'][1]
        raw = bytearray(48)
        for name, offset, width in layout['fields']:
            value = {'FileObject': 0x123, 'FileKey': 0x456, 'TTID': 100, 'IoSize': 8}.get(name, 0)
            raw[offset:offset + width] = value.to_bytes(width, 'little')
        event, rules = self.payload_fixture(layout['guid'], '68', raw, layout['schemaPrefix'])
        state = {'threads': {100: {'pid': 20, 'startedAt': '2026-10-13T00:00:00Z'}},
                 'files': {0x456: 'owned/marker'}, 'pointerSize': 8}
        row = stage0.parse_event(event, rules, state)
        self.assertEqual('WriteFile', row['operation']); self.assertEqual(20, row['pid'])
        self.assertEqual('owned/marker', row['destination'])
        self.assertTrue(stage0.classify(self.controls + [row], self.context, self.rules)['positiveControls']['file'])
        wrong, wrong_rules = self.payload_fixture(layout['guid'], '68', raw, [('TTID', 'win:Pointer')])
        self.assertEqual('unknown', stage0.parse_event(wrong, wrong_rules, state)['operation'])
        for opcode in ('67', '68'):
            event.find('System/Opcode').text = opcode
            event.find('ProcessingErrorData/EventPayload').text = bytes(raw[:12]).hex()
            self.assertTrue(stage0.parse_event(event, rules, state)['decodeError'])

    def test_file_key_names_thread_retirement_and_completion_are_correlated(self):
        state = {'threads': {}, 'files': {}}
        events = [self.real_event('3d6fa8d1-fe05-11d0-9dda-00c04fd7ba7c', '1', {'ProcessId': 10, 'TThreadId': 100}),
                  self.real_event('90cbdc39-4a3e-11d1-84f4-0000f80464e3', '32', {'FileObject': '0X0000456', 'FileName': 'owned/fixture'}),
                  self.real_event('90cbdc39-4a3e-11d1-84f4-0000f80464e3', '68', {'FileObject': '0x123', 'FileKey': '0x456', 'TTID': 100, 'IrpPtr': '0x789'}),
                  self.real_event('90cbdc39-4a3e-11d1-84f4-0000f80464e3', '76', {'IrpPtr': '0x789', 'NtStatus': 0})]
        rows = [stage0.parse_event(event, correlation=state) for event in events]
        self.assertEqual('owned/fixture', rows[-1]['destination'])
        self.assertEqual(10, rows[-1]['pid']); self.assertEqual('file-request-correlation', rows[-1]['pidSource'])
        self.assertEqual('unknown', stage0.parse_event(events[-1], correlation=state)['operation'])
        end = self.real_event('3d6fa8d1-fe05-11d0-9dda-00c04fd7ba7c', '2', {'ProcessId': 10, 'TThreadId': 100})
        stage0.parse_event(end, correlation=state)
        self.assertIsNone(stage0.parse_event(events[2], correlation=state)['pid'])

    def test_duplicate_file_requests_do_not_lend_identity_to_completion(self):
        state = {'threads': {100: {'pid': 10, 'startedAt': '2026-10-13T00:00:00Z'}}, 'files': {}}
        request = self.real_event('90cbdc39-4a3e-11d1-84f4-0000f80464e3', '68',
                                  {'FileObject': '0x123', 'TTID': 100, 'IrpPtr': '0x789'})
        for _ in range(2): stage0.parse_event(request, correlation=state)
        end = self.real_event('90cbdc39-4a3e-11d1-84f4-0000f80464e3', '76', {'IrpPtr': '0x789', 'NtStatus': 0})
        self.assertEqual('unknown', stage0.parse_event(end, correlation=state)['operation'])
        state['requests'] = {}
        stage0.parse_event(request, correlation=state)
        unknown = self.real_event('90cbdc39-4a3e-11d1-84f4-0000f80464e3', '999', {'IrpPtr': '0x789'})
        stage0.parse_event(unknown, correlation=state)
        self.assertEqual('unknown', stage0.parse_event(end, correlation=state)['operation'])

    def test_file_position_is_non_effect_but_basic_and_unknown_information_stay_writes(self):
        for info, expected in ((14, 'FilePosition'), (4, 'SetInformation'), (999, 'SetInformation')):
            event = self.real_event('90cbdc39-4a3e-11d1-84f4-0000f80464e3', '69',
                                    {'FileObject': '0x123', 'TTID': 100, 'InfoClass': info})
            self.assertEqual(expected, stage0.parse_event(event)['operation'])

    def test_raw_clock_recovers_process_control_without_a_ledger_time_shift(self):
        import datetime as dt
        state = {'threads': {}, 'files': {}}
        ticks = (dt.datetime(2026, 10, 13) - dt.datetime(1601, 1, 1)).days * 86400 * 10000000
        header = self.real_event('68fdd900-4a3e-11d1-84f4-0000f80464e3', '0',
                                {'StartTime': ticks, 'PerfFreq': 10000000, 'ReservedFlags': 1})
        import xml.etree.ElementTree as ET
        ET.SubElement(ET.SubElement(header, 'RenderingInfo'), 'Opcode').text = 'Header'
        header.find('System/TimeCreated').attrib = {'RawTime': '1000'}
        process = self.real_event('3d6fa8d0-fe05-11d0-9dda-00c04fd7ba7c', '1',
                                 {'ProcessId': 10, 'ParentId': 20, 'ImageFileName': 'Studio'})
        process.find('System/TimeCreated').attrib = {'SystemTime': '2026-10-13T09:00:05+08:59'}
        bad = stage0.parse_event(process)
        self.assertFalse(stage0.classify([bad], self.context, self.rules)['positiveControls']['process'])
        stage0.parse_event(header, correlation=state)
        process.find('System/TimeCreated').attrib = {'RawTime': '50001000'}
        good = stage0.parse_event(process, correlation=state)
        self.assertEqual('2026-10-13T00:00:05.0000000Z', good['at'])
        self.assertTrue(stage0.classify([good], self.context, self.rules)['positiveControls']['process'])

    def test_registry_kcb_and_relative_value_prove_only_exact_live_marker(self):
        control = self.registry_fixture(); marker = self.context['positiveControls'][-1]
        state = {'threads': {}, 'files': {}}
        for opcode in ('22', '24', '25'):
            name = self.real_event('ae53722e-c863-11d2-8659-00c04fa321a1', opcode,
                                   {'KeyHandle': '0X0000123', 'KeyName': marker['traceKeyPath']})
            stage0.parse_event(name, correlation=state)
        value = self.real_event('ae53722e-c863-11d2-8659-00c04fa321a1', '14',
                               {'KeyHandle': '0x123', 'KeyName': 'RunId', 'Status': '   0'})
        row = stage0.parse_event(value, correlation=state)
        self.assertTrue(stage0.classify(self.controls + [row], self.context, self.rules)['positiveControls']['registry'])
        delete = self.real_event('ae53722e-c863-11d2-8659-00c04fa321a1', '23', {'KeyHandle': '0x123'})
        stage0.parse_event(delete, correlation=state)
        row = stage0.parse_event(value, correlation=state)
        self.assertFalse(stage0.registry_marker_match(row, marker, self.rules, {20}, proof=True))

    def test_profile_coverage_rejects_missing_each_keyword_and_inactive_declarations(self):
        import xml.etree.ElementTree as ET
        profile = SCRIPT.parent / 'Probe.wprp'
        self.assertEqual([], stage0.profile_coverage(profile))
        for value in ('FileIO', 'FileIOInit', 'Filename', 'Registry', 'ProcessThread', 'Loader'):
            doc = ET.parse(profile)
            keywords = doc.find('./Profiles/SystemProvider/Keywords')
            keywords.remove(next(node for node in keywords if node.get('Value') == value))
            fixture = self.scratch() / 'profile.wprp'; doc.write(fixture)
            self.assertTrue(stage0.profile_coverage(fixture), value)
        doc = ET.parse(profile)
        doc.find('./Profiles/Profile/Collectors/SystemCollectorId/SystemProviderId').set('Value', 'unselected')
        doc.write(fixture)
        self.assertTrue(stage0.profile_coverage(fixture))
        doc = ET.parse(profile)
        provider = doc.find('./Profiles/SystemProvider')
        inactive = copy.deepcopy(provider); inactive.set('Id', 'Inactive')
        doc.find('./Profiles').append(inactive)
        provider.find('Keywords').remove(provider.find("Keywords/Keyword[@Value='Registry']"))
        doc.write(fixture)
        self.assertTrue(any('registry' in issue for issue in stage0.profile_coverage(fixture)))
        self.assertTrue(stage0.profile_coverage(profile, ['new-channel']))

    def test_profile_coverage_is_checked_by_preflight_and_rejects_forbidden_keywords(self):
        import xml.etree.ElementTree as ET
        doc = ET.parse(SCRIPT.parent / 'Probe.wprp')
        ET.SubElement(doc.find('./Profiles/SystemProvider/Keywords'), 'Keyword', Value='NetworkTrace')
        root = self.scratch(); fixture = root / 'profile.wprp'; doc.write(fixture)
        issues = stage0.preflight(root / 'probe-fixture', live_pin=lambda: {}, identity=lambda: ('a', '2026-10-14T00:00:00Z'),
                                 version=lambda: 7, scratch_root=root, profile_path=fixture, trace=True)
        self.assertTrue(any('profile selects' in issue for issue in issues))

    def test_bundled_classic_reads_and_queries_are_non_effects_in_real_shape(self):
        fields = {'FileObject': '0x123', 'TTID': '100', 'FileName': 'owned/fixture', 'KeyName': 'fixture', 'Status': '0'}
        for guid, opcode in [('90cbdc39-4a3e-11d1-84f4-0000f80464e3', n) for n in ('67', '74')] + [
                ('ae53722e-c863-11d2-8659-00c04fa321a1', n) for n in ('13', '16', '17', '18', '19')]:
            state = {'threads': {100: {'pid': 10, 'startedAt': '2026-10-13T00:00:00Z'}}, 'files': {}}
            event = stage0.parse_event(self.real_event(guid, opcode, fields, pid=10), correlation=state)
            self.assertIn(event['operation'], self.rules['readOperations'][event['channel']])
            result = stage0.classify(self.controls + [event], self.context, self.rules)
            self.assertEqual('app-read', result['candidates'][-1]['classification'])

    def test_compact_replay_preserves_every_event_fields_health_and_verdict(self):
        import xml.etree.ElementTree as ET
        root = self.scratch(); raw = root / 'fixture.xml'; compact = root / 'fixture.jsonl.gz'
        tree = ET.Element('Events')
        tree.append(self.real_event('90cbdc39-4a3e-11d1-84f4-0000f80464e3', '999', {'FileName': 'synthetic'}))
        tree.append(self.real_event('68fdd900-4a3e-11d1-84f4-0000f80464e3', 'Header', {'EventsLost': 0, 'BuffersLost': 0}))
        ET.ElementTree(tree).write(raw)
        record = stage0.compact_trace(raw, compact)
        self.assertEqual(2, record['events'])
        self.assertEqual(list(stage0.parse_trace(raw)), list(stage0.parse_trace(compact)))
        result = lambda path: stage0.classify(lambda: stage0.parse_trace(path), self.context, self.rules)
        self.assertEqual(result(raw), result(compact))
        summary = root / 'summary.txt'; summary.write_text('Total Events Processed 2\nTotal Events Lost 0\n')
        self.assertTrue(stage0.read_trace_health(summary, compact)['complete'])
        self.assertEqual(stage0.file_digest(raw), record['sourceSha256'])
        with self.assertRaises(FileExistsError): stage0.compact_trace(raw, compact)
        for parser in ('stale', None):
            changed = dict(record, parserSourceSha256=parser)
            stage0.write(str(compact) + '.record.json', changed)
            with self.assertRaisesRegex(ValueError, 'parser source differs'): list(stage0.parse_trace(compact))
        record['normalisationSha256'] = 'stale'
        stage0.write(str(compact) + '.record.json', record)
        with self.assertRaisesRegex(ValueError, 'normalisation differs'): list(stage0.parse_trace(compact))

    @staticmethod
    def real_event(guid, opcode, fields=None, pid=20, at='2026-10-13T00:00:05Z', error=False, event_id=None, version=None):
        import xml.etree.ElementTree as ET
        event = ET.Element('Event', xmlns='http://schemas.microsoft.com/win/2004/08/events/event')
        system = ET.SubElement(event, 'System')
        ET.SubElement(system, 'Provider', Guid='{9e814aad-3204-11d2-9a82-006008a86939}')
        if event_id is not None:
            ET.SubElement(system, 'EventID').text = str(event_id)
        if version is not None:
            ET.SubElement(system, 'Version').text = str(version)
        ET.SubElement(system, 'Opcode').text = str(opcode)
        ET.SubElement(system, 'Execution', ProcessID=str(pid), ThreadID='100')
        ET.SubElement(system, 'TimeCreated', SystemTime=at)
        payload = ET.SubElement(event, 'ProcessingErrorData' if error else 'EventData')
        if error:
            ET.SubElement(payload, 'ErrorCode').text = '15003'
        else:
            for key, value in (fields or {}).items(): ET.SubElement(payload, 'Data', Name=key).text = str(value)
        ET.SubElement(ET.SubElement(event, 'ExtendedTracingInfo'), 'EventGuid').text = guid
        return event

    def test_evidence_rows_map_each_decoded_kind_and_do_not_reinterpret_metadata(self):
        rules = json.loads((SCRIPT.parent / 'normalisation-rules.json').read_text(encoding='utf-8'))
        fields = {'ProcessId': ' 0xA ', 'ParentId': '0x14', 'ImageFileName': 'Studio.exe', 'FileName': 'owned/file',
                  'FileObject': '0x123', 'OpenPath': 'owned/file', 'TTID': '100', 'TThreadId': '100', 'KeyName': 'key', 'Status': '0',
                  'KeyHandle': '0x123', 'IrpPtr': '0x123', 'NtStatus': '0'}
        for row in rules['rows']:
            self.assertTrue(row['evidence'])
            if row['operation'] == 'FileOperationEnd':
                continue # completion requires an actual preceding request; tested separately
            if 'eventIds' in row: # manifest rows map by Event ID and version, tested separately
                continue
            event = stage0.parse_event(self.real_event('{' + row['guid'] + '}', row['opcodes'][0], fields), rules)
            self.assertEqual(row['operation'], event['operation'])
            self.assertEqual(row['channel'], event['channel'])
        wrong = stage0.parse_event(self.real_event('{b3e675d7-2554-4f18-830b-2762732560de}', '36'))
        self.assertEqual('metadata', wrong['channel'])
        self.assertNotIn(wrong['operation'], ('Write', 'WriteFile', 'SetValue'))
        unmapped = stage0.parse_event(self.real_event('{90cbdc39-4a3e-11d1-84f4-0000f80464e3}', '999', fields))
        self.assertEqual('unknown', unmapped['operation'])
        self.assertEqual('unknown', unmapped['channel'])

    def test_real_shape_controls_use_payload_pid_thread_correlation_and_exact_marker(self):
        marker = self.registry_fixture()
        control = self.context['positiveControls'][-1]
        self.context['ownedProcesses'][0]['parentPid'] = 20
        state = {'threads': {}, 'files': {}}
        source = [
            self.real_event('{3d6fa8d0-fe05-11d0-9dda-00c04fd7ba7c}', 'Start',
                            {'ProcessId': '0xA', 'ParentId': '0x14', 'ImageFileName': 'Studio.exe'}, pid=20),
            self.real_event('{3d6fa8d1-fe05-11d0-9dda-00c04fd7ba7c}', 'Start', {'ProcessId': '20', 'TThreadId': '100'}),
            self.real_event('{90cbdc39-4a3e-11d1-84f4-0000f80464e3}', '64',
                            {'FileObject': '0x123', 'OpenPath': 'owned/marker', 'TTID': '100'}, pid=4),
            self.real_event('{90cbdc39-4a3e-11d1-84f4-0000f80464e3}', '68', {'FileObject': '0x123', 'TTID': '100'}, pid=4),
            self.real_event('{ae53722e-c863-11d2-8659-00c04fa321a1}', '14',
                            {'KeyName': control['traceKeyPath'] + '\\RunId', 'Status': '0'})]
        events = [stage0.parse_event(event, correlation=state) for event in source]
        self.assertEqual(10, events[0]['pid']) # target payload overrides emitter header
        self.assertEqual(20, events[3]['pid']) # requesting thread overrides worker header
        self.assertEqual('owned/marker', events[3]['destination'])
        result = stage0.classify(events, self.context, self.rules)
        self.assertEqual({'file': True, 'process': True, 'registry': True}, result['positiveControls'])
        state = {'threads': {}, 'files': {}}
        unresolved = stage0.parse_event(source[3], correlation=state)
        self.assertIsNone(unresolved['pid'])
        self.assertEqual('requesting-thread-unresolved', unresolved['pidSource'])

    def test_decoding_error_and_missing_identity_do_not_become_process_proof(self):
        row = stage0.parse_event(self.real_event('{3d6fa8d1-fe05-11d0-9dda-00c04fd7ba7c}', 'Start', error=True))
        self.assertTrue(row['decodeError'])
        self.assertEqual('unknown', row['operation'])
        row = stage0.parse_event(self.real_event('{3d6fa8d0-fe05-11d0-9dda-00c04fd7ba7c}', '1',
                                              {'ProcessId': 'not-a-pid', 'ParentId': '20', 'ImageFileName': 'Studio'}))
        self.assertEqual('unknown', row['operation'])

    def test_health_never_confuses_processed_buffers_with_lost_buffers(self):
        import xml.etree.ElementTree as ET
        root = self.scratch(); summary = root / 'summary.txt'; xml = root / 'trace.xml'
        summary.write_text('Total Buffers Processed 223\nTotal Events  Processed 1\nTotal Events  Lost 0\n')
        health = stage0.read_trace_health(summary)
        self.assertIsNone(health['buffersLost'])
        self.assertFalse(health['complete'])
        event = self.real_event('{68fdd900-4a3e-11d1-84f4-0000f80464e3}', 'Header', {'EventsLost': '0', 'BuffersLost': '0'})
        tree = ET.Element('Events'); tree.append(event); ET.ElementTree(tree).write(xml)
        health = stage0.read_trace_health(summary, xml)
        self.assertTrue(health['complete']); self.assertEqual(0, health['buffersLost'])
        self.assertEqual(stage0.file_digest(summary), health['summarySha256'])
        self.assertIn('TRACE_LOGFILE_HEADER', health['buffersLossSource'])
        summary.write_text('Total Buffers Processed 223\nTotal Events Processed 2\nTotal Events Lost 0\n')
        self.assertFalse(stage0.read_trace_health(summary, xml)['complete'])
        summary.write_text('Total Events Processed 1\nTotal Events Lost 0\nTotal Events Lost 0\n')
        self.assertIsNone(stage0.read_trace_health(summary, xml)['eventsLost'])

    def test_raw_qpc_clock_uses_header_anchor_not_guessed_timezone_or_ledger_alignment(self):
        import datetime as dt
        import xml.etree.ElementTree as ET
        root = self.scratch(); path = root / 'raw.xml'
        ticks = (dt.datetime(2026, 10, 13) - dt.datetime(1601, 1, 1)).days * 86400 * 10000000
        header = self.real_event('{68fdd900-4a3e-11d1-84f4-0000f80464e3}', 'Header',
                                 {'StartTime': ticks, 'PerfFreq': '10000000', 'ReservedFlags': '0x1', 'EventsLost': '0', 'BuffersLost': '0'})
        process = self.real_event('{3d6fa8d0-fe05-11d0-9dda-00c04fd7ba7c}', 'Start',
                                  {'ProcessId': '10', 'ParentId': '20', 'ImageFileName': 'Studio'})
        for event, raw in ((header, '1000'), (process, '11000')):
            node = next(node for node in event.iter() if node.tag == 'TimeCreated')
            node.attrib.clear(); node.set('RawTime', raw)
        tree = ET.Element('Events'); tree.extend([header, process]); ET.ElementTree(tree).write(path)
        events = list(stage0.parse_trace(path))
        self.assertEqual('2026-10-13T00:00:00.0010000Z', events[1]['at'])
        self.assertEqual(1000, events[1]['clockEvidence']['rawAnchor'])
        state = {'threads': {}, 'files': {}}
        for node in header.iter():
            if node.attrib.get('Name') == 'ReservedFlags': node.text = '3'
        stage0.parse_event(header, correlation=state)
        self.assertEqual('', stage0.parse_event(process, correlation=state)['at'])

    def test_owned_problem_summary_redacts_destinations_and_tracks_identity_source(self):
        event = self.event(10, 'registry', 'SetValue', r'\REGISTRY\USER\synthetic-identity\Software\Fixture')
        result = stage0.classify(self.controls + [event], self.context, self.rules)
        problem = result['ownedTreeProblems'][0]
        self.assertEqual('<REGISTRY>', problem['destinationCategory'])
        self.assertNotIn('synthetic-identity', json.dumps(problem))
        self.assertEqual('out-of-declaration', problem['classification'])

    @staticmethod
    def scratch():
        root = Path(tempfile.gettempdir()) / ('gcam-stage0-fixture-' + __import__('uuid').uuid4().hex)
        root.mkdir()
        return root

    def test_pin_deployment_hash_is_the_digest_of_its_serialised_file_list(self):
        path = self.scratch() / 'pin.json'
        stage0.pin(path)
        emitted = json.loads(path.read_text(encoding='utf-8'))
        self.assertEqual(1, emitted['fingerprintSpecVersion'])
        self.assertEqual(stage0.digest('\n'.join(k + ':' + v for k, v in emitted['deploymentFiles'].items()).encode()),
                         emitted['deploymentSha256'])

    def test_preflight_checks_all_failures_without_registry_trace_or_launch(self):
        root = self.scratch(); run = root / 'probe-20261014-03'; run.mkdir()
        commit = 'a' * 40
        files = {'a.dll': 'b' * 64}
        live = {'workingTree': 'clean', 'sourceCommit': commit, 'fingerprintSpecVersion': 1,
                'isolationSha256': 'i', 'deploymentSha256': stage0.fingerprint_map(files), 'deploymentFiles': files,
                'sourceSha256': 's', 'runnerSourceSha256': 'r'}
        approval = {'runId': run.name, 'commit': commit, 'committedAt': '2026-10-14T00:00:00Z',
                    'isolationSha256': live['isolationSha256'], 'deploymentSha256': live['deploymentSha256'],
                    'approvals': [{'id': act + '-fixture', 'humanActId': act, 'quote': 'synthetic approval fixture', 'targetCommit': commit,
                                   'quotedAt': '2026-10-14T00:01:00Z'} for act in ('HUM-01', 'HUM-04', 'HUM-07')]}
        stage0.write(run / 'pin.json', live); stage0.write(run / 'approvals.json', approval)
        check = lambda version=7: stage0.preflight(run, lambda: live, lambda: (commit, approval['committedAt']),
                                                 lambda: version, root)
        self.assertEqual([], check())
        for name in ('trace.ready.json', 'trace.complete.json', 'probe.etl'):
            (run / name).write_text('retained synthetic evidence')
        stale = dict(live); stale['deploymentSha256'] = 'stale'; stale.pop('fingerprintSpecVersion')
        stage0.write(run / 'pin.json', stale)
        approval['approvals'][0]['quotedAt'] = '2026-10-13T00:00:00Z'
        approval['runId'] = 'another'; stage0.write(run / 'approvals.json', approval)
        issues = check(5)
        for token in ('trace.ready', 'trace.complete', 'ETL', 'pwsh', 'runId', 'fingerprintSpecVersion',
                      'deploymentFiles', 'deploymentSha256', 'predates'):
            self.assertTrue(any(token in issue for issue in issues), token)
        self.assertTrue((run / 'probe.etl').exists())
        self.assertTrue(stage0.preflight(root.parent / 'foreign', scratch_root=root))

    def test_streaming_xml_releases_events_and_sink_preserves_unknown_candidates(self):
        import tracemalloc
        path = self.scratch() / 'large.xml'
        with path.open('w', encoding='utf-8') as stream:
            stream.write('<Events>')
            for _ in range(12000):
                stream.write('<Event><System><Provider Name="Unmapped"/><Execution ProcessID="99"/></System></Event>')
            stream.write('</Events>')
        count = 0
        def sink(candidate):
            nonlocal count
            count += 1
            self.assertEqual('unknown', candidate['classification'])
        tracemalloc.start()
        try:
            result = stage0.classify(lambda: stage0.parse_trace(path), {'ownedProcesses': []}, self.rules, sink)
            peak = tracemalloc.get_traced_memory()[1]
        finally:
            tracemalloc.stop()
        self.assertEqual(12000, count)
        self.assertEqual(count, result['candidateCount'])
        self.assertEqual([], result['candidates'])
        self.assertFalse(result['g1Eligible'])
        self.assertLess(peak, 4 * 1024 * 1024)  # Regression bound on a fixed synthetic tree, not a capture margin.

    def test_stream_factory_and_list_have_identical_tree_and_candidate_verdicts(self):
        child = self.event(11, 'process', 'ProcessStart', 'child'); child['parentPid'] = 10
        grandchild = self.event(12, 'process', 'ProcessStart', 'child'); grandchild['parentPid'] = 11
        events = self.controls + [grandchild, child, self.event(12, 'file', 'Write', 'outside/data')]
        expected = stage0.classify(events, self.context, self.rules)
        self.assertEqual(expected, stage0.classify(lambda: iter(events), self.context, self.rules))
        with self.assertRaises(ValueError): stage0.classify(iter(events), self.context, self.rules)

    def setUp(self):
        self.rules = json.loads((SCRIPT.parent / 'classification-rules.json').read_text())
        # A synthetic two-channel scope is not the real three-channel probe policy.
        self.rules['requiredChannels'] = ['file', 'process']
        self.context = {'ownedProcesses': [{'pid': 10, 'startedAt': '2026-10-13T00:00:00Z', 'finishedAt': '2026-10-13T00:01:00Z'}],
                        'harnessPids': [20], 'osPids': [4], 'ownedRoots': ['owned/'], 'requiredChannels': ['file', 'process'],
                        'positiveControls': [{'channel': 'file', 'pid': 20, 'token': 'marker'},
                                             {'channel': 'process', 'pid': 10, 'token': 'Studio'}], 'blindSpots': ['synthetic fixture'],
                        'traceHealth': {'complete': True, 'eventsLost': 0, 'buffersLost': 0}}
        self.controls = [self.event(20, 'file', 'Write', 'owned/marker'), self.event(10, 'process', 'ImageLoad', 'Studio')]

    @staticmethod
    def event(pid, channel, operation, destination):
        return {'pid': pid, 'channel': channel, 'operation': operation, 'destination': destination, 'at': '2026-10-13T00:00:05Z'}

    def test_valid_controls_and_reads_are_classified_without_runtime(self):
        result = stage0.classify(self.controls + [self.event(10, 'file', 'Read', 'deployed/ambient.json')], self.context, self.rules)
        self.assertTrue(result['g1Eligible'])
        self.assertEqual('app-read', result['candidates'][-1]['classification'])

    def test_attributed_write_is_undeclared_even_inside_sandbox(self):
        for channel, operation, destination in [('file', 'Write', 'owned/data'), ('registry', 'SetValue', 'key'), ('network', 'Connect', 'remote')]:
            result = stage0.classify(self.controls + [self.event(10, channel, operation, destination)], self.context, self.rules)
            self.assertEqual('unsafe_effect', result['runStatus'])
            self.assertFalse(result['g1Eligible'])

    def test_declared_surface_is_only_possible_when_rule_explicitly_allows_it(self):
        rules = copy.deepcopy(self.rules); rules['allowAppFileWrites'] = True
        result = stage0.classify(self.controls + [self.event(10, 'file', 'Write', 'owned/data')], self.context, rules)
        self.assertEqual('app-declared', result['candidates'][-1]['classification'])
        self.assertTrue(result['g1Eligible'])

    def test_unknown_opcode_and_unattributed_shell_effect_block(self):
        for event in [self.event(10, 'file', 'Unmapped', 'data'), self.event(99, 'file', 'Write', 'owned/marker')]:
            result = stage0.classify(self.controls + [event], self.context, self.rules)
            self.assertEqual('unknown', result['candidates'][-1]['classification'])
            self.assertEqual('unresolved-candidate', result['reason'])

    def test_pid_outside_recorded_lifetime_is_not_adopted(self):
        event = self.event(10, 'file', 'Write', 'data'); event['at'] = '2026-10-13T00:02:00Z'
        result = stage0.classify(self.controls + [event], self.context, self.rules)
        self.assertEqual('unknown', result['candidates'][-1]['classification'])

    def test_direct_root_launch_is_not_misclassified_as_an_app_spawn(self):
        self.context['ownedProcesses'][0]['parentPid'] = 20
        event = self.event(10, 'process', 'ProcessStart', 'Studio'); event['parentPid'] = 20
        result = stage0.classify(self.controls + [event], self.context, self.rules)
        self.assertEqual('os-or-harness-actor', result['candidates'][-1]['classification'])
        self.assertTrue(result['g1Eligible'])

    def test_child_tree_effects_and_harness_escape_are_not_ignored(self):
        child = self.event(11, 'process', 'ProcessStart', 'child'); child['parentPid'] = 10
        result = stage0.classify(self.controls + [child, self.event(11, 'file', 'Write', 'outside/data')], self.context, self.rules)
        self.assertEqual('out-of-declaration', result['candidates'][-1]['classification'])
        result = stage0.classify(self.controls + [self.event(20, 'file', 'Write', 'outside/data')], self.context, self.rules)
        self.assertEqual('out-of-declaration', result['candidates'][-1]['classification'])

    def test_create_is_ambiguous_without_access_and_disposition_metadata(self):
        result = stage0.classify(self.controls + [self.event(10, 'file', 'CreateFile', 'ambient.json')], self.context, self.rules)
        self.assertEqual('unknown', result['candidates'][-1]['classification'])

    def test_missing_positive_control_is_instrument_not_proven(self):
        result = stage0.classify(self.controls[:1], self.context, self.rules)
        self.assertEqual('instrument-not-proven', result['reason'])
        self.assertFalse(result['g1Eligible'])

    def test_policy_required_channel_cannot_be_removed_from_context(self):
        self.rules['requiredChannels'].append('registry')
        result = stage0.classify(self.controls, self.context, self.rules)
        self.assertFalse(result['g1Eligible'])
        self.assertEqual('instrument-not-proven', result['reason'])

    def registry_fixture(self):
        self.rules['requiredChannels'] = ['file', 'process', 'registry']
        marker = {'channel': 'registry', 'pid': 20, 'runId': 'probe-01',
                  'keyPath': r'HKCU\Software\GcamUiaProbe\probe-01',
                  'traceKeyPath': r'\REGISTRY\USER\S-1-5-21-123-456-789-1001\Software\GcamUiaProbe\probe-01',
                  'valueName': 'RunId', 'valueData': 'probe-01',
                  'startedAt': '2026-10-13T00:00:04Z', 'finishedAt': '2026-10-13T00:00:06Z'}
        self.context['positiveControls'].append(marker)
        self.context['channelApplicability'] = [{'channel': 'network', 'status': 'not_applicable', 'decision': 'MR6',
                                                 'sourceEvidence': ['src/Gcam.Studio/App.xaml.cs:17']}]
        event = self.event(20, 'registry', 'SetValue', marker['traceKeyPath'])
        event.update(valueName='RunId', valueData='probe-01', status=0)
        return event

    def test_registry_marker_proves_channel_and_network_is_not_applicable(self):
        marker = self.registry_fixture()
        result = stage0.classify(self.controls + [marker], self.context, self.rules)
        self.assertTrue(result['g1Eligible'])
        self.assertTrue(result['positiveControls']['registry'])
        self.assertNotIn('network', result['positiveControls'])
        self.assertEqual('not_applicable', result['channelApplicability'][0]['status'])
        self.assertEqual('os-or-harness-actor', result['candidates'][-1]['classification'])
        # Key/run identity is observable even when a standard registry trace omits value payload.
        marker.pop('valueData')
        self.assertTrue(stage0.classify(self.controls + [marker], self.context, self.rules)['g1Eligible'])

    def test_missing_registry_marker_is_instrument_not_proven(self):
        self.registry_fixture()
        result = stage0.classify(self.controls, self.context, self.rules)
        self.assertEqual('instrument-not-proven', result['reason'])
        self.assertFalse(result['g1Eligible'])

    def test_registry_control_rejects_wrong_identity_value_actor_time_or_operation(self):
        marker = self.registry_fixture()
        for field, value in [('destination', marker['destination'] + '-other'), ('valueName', 'Other'),
                             ('valueData', 'wrong-run'), ('pid', 99), ('status', 5),
                             ('at', '2026-10-13T00:00:07Z'), ('operation', 'Unmapped')]:
            event = dict(marker); event[field] = value
            result = stage0.classify(self.controls + [event], self.context, self.rules)
            self.assertFalse(result['positiveControls']['registry'], (field, value))
            self.assertEqual('unknown', result['candidates'][-1]['classification'])
        event = dict(marker); event['operation'] = 'CreateKey'; event.pop('valueName'); event.pop('valueData')
        result = stage0.classify(self.controls + [event], self.context, self.rules)
        self.assertFalse(result['positiveControls']['registry'])
        self.assertEqual('os-or-harness-actor', result['candidates'][-1]['classification'])

    def test_other_harness_registry_events_stay_unknown_and_app_marker_writes_are_undeclared(self):
        marker = self.registry_fixture()
        unrelated = self.event(20, 'registry', 'SetValue', r'HKCU\Software\Other')
        result = stage0.classify(self.controls + [marker, unrelated], self.context, self.rules)
        self.assertEqual('unknown', result['candidates'][-1]['classification'])
        self.assertFalse(result['g1Eligible'])
        marker['pid'] = 10
        result = stage0.classify(self.controls + [marker], self.context, self.rules)
        self.assertEqual('out-of-declaration', result['candidates'][-1]['classification'])

    def test_network_not_applicable_does_not_filter_observed_connections(self):
        marker = self.registry_fixture()
        result = stage0.classify(self.controls + [marker, self.event(10, 'network', 'Connect', 'remote')], self.context, self.rules)
        self.assertEqual('unsafe_effect', result['runStatus'])

    def test_tracerpt_registry_marker_fixture_uses_exact_value_fields(self):
        marker = self.registry_fixture()
        root = Path(tempfile.gettempdir()) / ('gcam-stage0-fixture-' + __import__('uuid').uuid4().hex)
        root.mkdir()
        path = root / 'registry.xml'
        path.write_text('<Events><Event><System><Provider Name="Registry"/><Execution ProcessID="20"/>'
                        '<TimeCreated SystemTime="2026-10-13T00:00:05Z"/><Opcode>SetValue</Opcode></System><EventData>'
                        '<Data Name="KeyName">' + marker['destination'] + '</Data><Data Name="ValueName">RunId</Data>'
                        '<Data Name="ValueData">probe-01</Data><Data Name="Status">0</Data></EventData></Event></Events>')
        events = list(stage0.parse_trace(path))
        self.assertTrue(stage0.classify(self.controls + events, self.context, self.rules)['g1Eligible'])

    def test_no_owned_product_run_is_not_green(self):
        self.context['ownedProcesses'] = []
        self.assertFalse(stage0.classify(self.controls, self.context, self.rules)['g1Eligible'])

    def test_trace_loss_or_unmeasured_health_never_qualifies(self):
        for health in [{}, {'complete': True, 'eventsLost': 1, 'buffersLost': 0}]:
            self.context['traceHealth'] = health
            result = stage0.classify(self.controls, self.context, self.rules)
            self.assertFalse(result['g1Eligible'])
            self.assertEqual('trace-health-not-proven', result['reason'])

    def test_os_bookkeeping_is_distinct_from_app_requested_service_write(self):
        events = self.controls + [self.event(4, 'process', 'ProcessAccounting', 'Studio')]
        self.assertEqual('os-or-harness-actor', stage0.classify(events, self.context, self.rules)['candidates'][-1]['classification'])
        events[-1]['operation'] = 'Write'
        self.assertEqual('unknown', stage0.classify(events, self.context, self.rules)['candidates'][-1]['classification'])

    def test_current_policy_and_all_seeded_drift_cases(self):
        policy = json.loads((SCRIPT.parent / 'action-policy.json').read_text())
        declared = stage0.declarations(); stage0.check_policy(policy, declared)
        for mutate in ['added', 'removed', 'duplicate', 'type']:
            bad = copy.deepcopy(policy)
            if mutate == 'added': changed = dict(declared); changed['New'] = ['Button']
            else: changed = declared
            if mutate == 'removed': bad['targets'].pop()
            if mutate == 'duplicate': bad['targets'].append(copy.deepcopy(bad['targets'][0]))
            if mutate == 'type': bad['targets'][0]['controlTypes'] = ['Other']
            self.assertNotEqual((policy, declared), (bad, changed))
            with self.assertRaises(ValueError): stage0.check_policy(bad, changed)

    def test_confined_paths_do_not_accept_sibling_or_parent_escape(self):
        self.assertTrue(stage0.confined('owned/a', 'owned'))
        self.assertFalse(stage0.confined('owned-other/a', 'owned'))
        self.assertFalse(stage0.confined('owned/../a', 'owned'))

    def test_tracerpt_xml_fixture_preserves_unknown_provider(self):
        # Retain fixtures in owned scratch; no recursive cleanup or system trace parsing.
        root = Path(tempfile.gettempdir()) / ('gcam-stage0-fixture-' + __import__('uuid').uuid4().hex)
        root.mkdir()
        path = root / 'events.xml'
        path.write_text('<Events><Event><System><Provider Name="FileIo"/><Execution ProcessID="10"/><TimeCreated SystemTime="2026-10-13T00:00:05Z"/><Opcode>Read</Opcode></System><EventData><Data Name="FileName">ambient.json</Data></EventData></Event><Event><System><Provider Name="Unmapped"/><Execution ProcessID="20"/></System></Event></Events>')
        events = list(stage0.parse_trace(path))
        self.assertEqual(('file', 10, 'ambient.json'), (events[0]['channel'], events[0]['pid'], events[0]['destination']))
        self.assertEqual('unknown', events[1]['channel'])

    # --- Run-04 decisions and rule changes (E1-E5, RC-05..RC-08); real-shape fixtures, no OS access ---------------------

    PROCESS, THREAD, IMAGE = '3d6fa8d0-fe05-11d0-9dda-00c04fd7ba7c', '3d6fa8d1-fe05-11d0-9dda-00c04fd7ba7c', '2cb15d1d-5fc1-11d2-abe1-00a0c911f518'
    FILE, REGISTRY, KERNEL_REGISTRY = '90cbdc39-4a3e-11d1-84f4-0000f80464e3', 'ae53722e-c863-11d2-8659-00c04fa321a1', '70eb4f03-c1de-4f73-a051-33d13d5413bd'
    STUDIO_IMAGE = '\\Device\\HarddiskVolume9\\fixture\\deploy\\Gcam.Studio.exe'
    SID = 'S-1-5-21-123-456-789-1001'

    def replay(self, sources):
        """Parse XML fixtures through one correlation state, as the streaming replay does."""
        state = {'threads': {}, 'files': {}}
        return [stage0.parse_event(event, correlation=state) for event in sources]

    def at(self, millis):
        return '2026-10-13T00:00:%02d.%07dZ' % (5 + millis // 1000, (millis % 1000) * 10000)

    def studio_start(self):
        """Owned Studio (pid 10, parent harness 20): start, its own image and its UI thread 100."""
        self.registry_fixture()
        self.context['ownedProcesses'][0]['parentPid'] = 20
        return [self.real_event(self.PROCESS, '1', {'ProcessId': 10, 'ParentId': 20, 'ImageFileName': 'Gcam.Studio.exe'}, pid=20, at=self.at(0)),
                self.real_event(self.IMAGE, '10', {'ProcessId': 10, 'FileName': self.STUDIO_IMAGE}, at=self.at(1)),
                self.real_event(self.THREAD, '1', {'ProcessId': 10, 'TThreadId': 100}, at=self.at(2))]

    def file_sequence(self, path, start, file_object='0xF1', disposition=1, thread=100, between=(), query=True, close_thread=None):
        ms = iter(range(start, start + 50))
        fields = lambda **extra: dict({'FileObject': file_object, 'TTID': thread, 'FileKey': '0xA1'}, **extra)
        events = [self.real_event(self.FILE, '64', dict(fields(), OpenPath=path, CreateOptions=(disposition << 24) | 0x160), pid=4, at=self.at(next(ms))),
                  self.real_event(self.FILE, '69', fields(InfoClass=4), pid=4, at=self.at(next(ms)))]
        events += [self.real_event(self.FILE, opcode, fields(), pid=4, at=self.at(next(ms))) for opcode in between]
        if query:
            events.append(self.real_event(self.FILE, '74', fields(InfoClass=6), pid=4, at=self.at(next(ms))))
        events.append(self.real_event(self.FILE, '65', fields(), pid=4, at=self.at(next(ms))))
        events.append(self.real_event(self.FILE, '66', dict(fields(), TTID=close_thread or thread), pid=4, at=self.at(next(ms))))
        return events

    def classified(self, rows, change_times=None):
        result = stage0.classify(self.controls + rows, self.context, self.rules, change_times=change_times)
        return result, [c for c in result['candidates'] if c['ownedTree'] or c['pid'] == 20][len(self.controls):]

    def test_manifest_set_information_class_maps_by_event_id_and_version_and_stays_effect_bearing(self):
        fields = {'KeyObject': '0xFFFF1', 'Status': '0', 'InfoClass': '5', 'DataSize': '4', 'KeyName': ''}
        row = stage0.parse_event(self.real_event(self.KERNEL_REGISTRY, '0', fields, pid=10, event_id=11, version=0))
        self.assertEqual(('registry', 'SetInformationKeyClass', '5'), (row['channel'], row['operation'], row['fields']['InfoClass']))
        self.assertIn('SetInformationKeyClass', self.rules['writeOperations']['registry'])
        for event_id, version in ((11, 1), (5, 0)):
            self.assertEqual('unknown', stage0.parse_event(self.real_event(self.KERNEL_REGISTRY, '0', fields, pid=10, event_id=event_id, version=version))['operation'])
        self.assertEqual('unknown', stage0.parse_event(self.real_event(self.KERNEL_REGISTRY, '11', fields, pid=10))['operation'])
        result = stage0.classify(self.controls + [row], self.context, self.rules)
        self.assertEqual('out-of-declaration', result['candidates'][-1]['classification']) # E4 is not exempted by the class event

    def test_profile_coverage_requires_exactly_the_approved_set_information_provider(self):
        import xml.etree.ElementTree as ET
        profile = SCRIPT.parent / 'Probe.wprp'; fixture = self.scratch() / 'profile.wprp'
        mutations = {
            'removed': lambda doc: doc.find('./Profiles/Profile/Collectors').remove(doc.find('./Profiles/Profile/Collectors/EventCollectorId')),
            'keyword': lambda doc: doc.find('./Profiles/EventProvider/Keywords/Keyword').set('Value', '0x140'),
            'stack': lambda doc: doc.find('./Profiles/EventProvider').set('Stack', 'true'),
            'other': lambda doc: doc.find('./Profiles/EventProvider').set('Name', 'Microsoft-Windows-Kernel-File'),
        }
        for name, mutate in mutations.items():
            doc = ET.parse(profile); mutate(doc); doc.write(fixture)
            self.assertTrue(stage0.profile_coverage(fixture), name)
        doc = ET.parse(profile)
        extra = copy.deepcopy(doc.find('./Profiles/EventProvider')); extra.set('Id', 'Extra'); extra.set('Name', 'Microsoft-Windows-Kernel-Process')
        doc.find('./Profiles').append(extra)
        ET.SubElement(doc.find('./Profiles/Profile/Collectors/EventCollectorId/EventProviders'), 'EventProviderId', Value='Extra')
        doc.write(fixture)
        self.assertTrue(any('unapproved event provider' in issue for issue in stage0.profile_coverage(fixture)))

    def test_e1_composition_window_is_declared_only_for_exact_key_values_hive_and_operation(self):
        rows = self.replay(self.studio_start())
        key = '\\REGISTRY\\USER\\' + self.SID + '\\Software\\Microsoft\\CTF\\CUAS\\DefaultCompositionWindow'
        cases = [(key + '\\Left', 'SetValue', 10, 'app-declared'), (key + '\\Top', 'SetValue', 10, 'app-declared'),
                 (key + '\\Width', 'SetValue', 10, 'out-of-declaration'),
                 (key[:-len('DefaultCompositionWindow')] + 'Other\\Left', 'SetValue', 10, 'out-of-declaration'),
                 (key.replace(self.SID, 'S-1-5-21-9-9-9-1002') + '\\Left', 'SetValue', 10, 'out-of-declaration'),
                 (key + '\\Left', 'DeleteValue', 10, 'out-of-declaration'), (key + '\\Left', 'SetValue', 20, 'unknown')]
        for destination, operation, pid, expected in cases:
            event = self.event(pid, 'registry', operation, destination)
            result, rows_out = self.classified(rows + [event])
            self.assertEqual(expected, result['candidates'][-1]['classification'], (destination, operation, pid))
            self.assertEqual('E1-app-tsf-composition-window' if expected == 'app-declared' else None, result['candidates'][-1]['rule'])
        event = self.event(10, 'registry', 'SetValue', key + '\\Left'); event['valueName'] = 'Top'
        self.assertEqual('out-of-declaration', self.classified(rows + [event])[0]['candidates'][-1]['classification'])

    def test_e3_driver_timestamps_are_declared_only_as_writes_into_that_file(self):
        rows = self.replay(self.studio_start())
        self.context['knownFolders'] = {'ProgramData': {'path': 'C:\\ProgramData', 'devicePath': '\\Device\\HarddiskVolume9\\ProgramData'}}
        target = '\\NVIDIA Corporation\\Drs\\nvAppTimestamps'
        for destination, operation, expected in [('\\Device\\HarddiskVolume9\\ProgramData' + target, 'WriteFile', 'app-declared'),
                                                 ('C:\\ProgramData' + target, 'WriteFile', 'app-declared'),
                                                 ('\\Device\\HarddiskVolume9\\ProgramData\\NVIDIA Corporation\\Drs\\nvdrsdb0.bin', 'WriteFile', 'out-of-declaration'),
                                                 ('\\Device\\HarddiskVolume8\\ProgramData' + target, 'WriteFile', 'out-of-declaration'),
                                                 ('\\Device\\HarddiskVolume9\\ProgramData' + target, 'SetInformation', 'out-of-declaration'),
                                                 ('\\Device\\HarddiskVolume9\\ProgramData' + target, 'Delete', 'out-of-declaration')]:
            result = self.classified(rows + [self.event(10, 'file', operation, destination)])[0]
            self.assertEqual(expected, result['candidates'][-1]['classification'], (destination, operation))
        self.assertEqual('<PROGRAMDATA>', result['candidates'][-1]['destinationCategory'])
        self.context.pop('knownFolders')
        result = self.classified(rows + [self.event(10, 'file', 'WriteFile', 'C:\\ProgramData' + target)])[0]
        self.assertEqual('out-of-declaration', result['candidates'][-1]['classification'])

    def test_e2_background_activity_record_needs_owned_image_value_run_user_and_process_end(self):
        sources = self.studio_start() + [self.real_event(self.PROCESS, '2', {'ProcessId': 10, 'ParentId': 20, 'ImageFileName': 'Gcam.Studio.exe'}, pid=10, at=self.at(4000))]
        bam = '\\REGISTRY\\MACHINE\\SYSTEM\\ControlSet001\\Services\\bam\\State\\UserSettings\\' + self.SID
        def record(value, millis, sid_key=bam, handle='0xB1'):
            return [self.real_event(self.REGISTRY, '14', {'KeyHandle': handle, 'KeyName': value, 'Status': '0'}, pid=10, at=self.at(millis)),
                    self.real_event(self.REGISTRY, '25', {'KeyHandle': handle, 'KeyName': sid_key}, pid=4, at=self.at(9000))]
        rows = self.replay(sources + record(self.STUDIO_IMAGE, 4002))
        result = self.classified(rows)[0]
        bam_row = [c for c in result['candidates'] if c['channel'] == 'registry' and c['pid'] == 10][-1]
        self.assertEqual(('os-or-harness-actor', 'E2-bam-process-end-record'), (bam_row['classification'], bam_row['rule']))
        self.assertEqual(1, result['ruleApplications']['RC-07-forward-kcb-name'])
        for label, extra in [('other image', record('\\Device\\HarddiskVolume9\\other\\tool.exe', 4002)),
                             ('mid-life', record(self.STUDIO_IMAGE, 3000)),
                             ('late', record(self.STUDIO_IMAGE, 4300)),
                             ('other user', record(self.STUDIO_IMAGE, 4002, bam.replace(self.SID, 'S-1-5-21-9-9-9-1002')))]:
            result = self.classified(self.replay(sources + extra))[0]
            row = [c for c in result['candidates'] if c['channel'] == 'registry' and c['pid'] == 10][-1]
            self.assertEqual('out-of-declaration', row['classification'], label)
            self.assertIsNone(row['rule'], label)

    def test_forward_kcb_rundown_names_a_handle_only_without_an_intervening_create_or_delete(self):
        set_info = self.real_event(self.REGISTRY, '20', {'KeyHandle': '0xC1', 'KeyName': '', 'Status': '0'}, pid=10, at=self.at(10))
        rundown = self.real_event(self.REGISTRY, '25', {'KeyHandle': '0xC1', 'KeyName': '\\REGISTRY\\MACHINE\\SOFTWARE\\Fixture'}, pid=4, at=self.at(90))
        delete = self.real_event(self.REGISTRY, '23', {'KeyHandle': '0xC1', 'KeyName': ''}, pid=4, at=self.at(50))
        for sources, expected in (([set_info, rundown], '<REGISTRY>'), ([set_info, delete, rundown], '<NO-DECODED-DESTINATION>')):
            result = stage0.classify(self.controls + self.replay(sources), self.context, self.rules)
            row = next(c for c in result['candidates'] if c['pid'] == 10 and c['kind'] == 'SetInformation')
            self.assertEqual(expected, row['destinationCategory'])
            self.assertEqual('out-of-declaration', row['classification']) # E4 stays out of declaration
        # A value name that starts with a backslash is a value, not a key path, when the handle is already named.
        state = {'threads': {}, 'files': {}}
        stage0.parse_event(rundown, correlation=state)
        value = stage0.parse_event(self.real_event(self.REGISTRY, '14', {'KeyHandle': '0xC1', 'KeyName': self.STUDIO_IMAGE, 'Status': '0'}, pid=10), correlation=state)
        self.assertEqual(('\\REGISTRY\\MACHINE\\SOFTWARE\\Fixture\\' + self.STUDIO_IMAGE, self.STUDIO_IMAGE), (value['destination'], value['valueName']))

    def test_e5_basic_information_sequence_is_non_effect_only_with_sequence_image_clause_and_old_change_time(self):
        module = '\\Device\\HarddiskVolume9\\fixture\\deploy\\Gcam.Studio.dll'
        load = self.real_event(self.IMAGE, '10', {'ProcessId': 10, 'FileName': module}, at=self.at(130))
        launch = stage0.iso_ticks(self.context['ownedProcesses'][0]['startedAt'])
        def run(sequence, change=None, extra=(load,)):
            result = self.classified(self.replay(self.studio_start() + sequence + list(extra)), change)[0]
            rows = [c for c in result['candidates'] if c['ownedTree'] and c['kind'] == 'SetInformation']
            return result, rows[-1]
        result, row = run(self.file_sequence(module, 100))
        self.assertEqual(('out-of-declaration', 'E5-sequence-changetime-not-proven'), (row['classification'], row['rule']))
        request = result['changeTimeRequest']
        self.assertEqual([module.casefold()], list(request['destinations']))
        evidence = lambda ticks: {'launchAt': request['launchAt'], 'records': {module.casefold(): {'changeTimeFileTime': ticks}}}
        self.assertEqual(('non-effect', 'E5-basic-information-sequence'), (run(self.file_sequence(module, 100), evidence(launch - 1))[1]['classification'],
                                                                           run(self.file_sequence(module, 100), evidence(launch - 1))[1]['rule']))
        self.assertEqual('E5-sequence-changetime-newer', run(self.file_sequence(module, 100), evidence(launch + 1))[1]['rule'])
        self.assertEqual('E5-sequence-changetime-not-proven', run(self.file_sequence(module, 100), {'launchAt': request['launchAt'], 'records': {module.casefold(): {'error': 'FileNotFoundError'}}})[1]['rule'])
        with self.assertRaisesRegex(ValueError, 'different launch'):
            run(self.file_sequence(module, 100), dict(evidence(launch - 1), launchAt='2026-10-12T00:00:00Z'))
        old = evidence(launch - 1)
        for label, sequence, extra in [('no query', self.file_sequence(module, 100, query=False), (load,)),
                                       ('open-if disposition', self.file_sequence(module, 100, disposition=3), (load,)),
                                       ('write in between', self.file_sequence(module, 100, between=('68',)), (load,)),
                                       ('rename in between', self.file_sequence(module, 100, between=('71',)), (load,)),
                                       ('other close thread', self.file_sequence(module, 100, close_thread=101), (load,)),
                                       ('no image load', self.file_sequence(module, 100), ()),
                                       ('other image next', self.file_sequence(module, 100),
                                        (self.real_event(self.IMAGE, '10', {'ProcessId': 10, 'FileName': module + '.other.dll'}, at=self.at(120)), load))]:
            row = run(sequence, old, extra)[1]
            self.assertEqual('out-of-declaration', row['classification'], label)
            self.assertIsNone(row['rule'], label)
        # The process's own image qualifies without a following load; a later-changed file keeps the effect.
        image = self.STUDIO_IMAGE.lower()
        own = {'launchAt': request['launchAt'], 'records': {image.casefold(): {'changeTimeFileTime': launch - 1}}}
        self.assertEqual('non-effect', run(self.file_sequence(image, 100), own, ())[1]['classification'])

    def test_change_time_step_reads_attributes_through_the_device_map_and_reports_unmapped(self):
        request = {'launchAt': '2026-10-13T00:00:00Z', 'destinations': {'\\device\\harddiskvolume9\\fixture\\a.dll': 1, '\\device\\other\\b.dll': 1}}
        seen = []
        result = stage0.change_times(request, devices={'C:': '\\Device\\HarddiskVolume9', 'S:': '\\??\\C:\\subst'},
                                     reader=lambda path: seen.append(path) or 123)
        self.assertEqual(['C:\\fixture\\a.dll'], seen)
        self.assertEqual({'changeTimeFileTime': 123}, result['records']['\\device\\harddiskvolume9\\fixture\\a.dll'])
        self.assertEqual({'error': 'unmapped-device'}, result['records']['\\device\\other\\b.dll'])
        self.assertEqual(stage0.iso_ticks('2026-10-13T00:00:00.0000001Z'), stage0.iso_ticks('2026-10-13T00:00:00Z') + 1)

    def test_device_form_roots_classify_harness_sandbox_writes_and_one_create_rule_for_both_actors(self):
        self.context['ownedRoots'] = ['C:\\Temp\\gcam-uia-fixture']
        device = '\\Device\\HarddiskVolume9\\Temp\\gcam-uia-fixture'
        write = self.event(20, 'file', 'WriteFile', device + '\\Roaming\\probe-diagnostic.txt')
        result = stage0.classify(self.controls + [write], self.context, self.rules)
        self.assertEqual('out-of-declaration', result['candidates'][-1]['classification']) # drive-letter roots never match device paths
        self.context['ownedRootDevicePaths'] = [device]
        result = stage0.classify(self.controls + [write], self.context, self.rules)
        self.assertEqual(('os-or-harness-actor', '<REDIRECTED-SANDBOX>'), (result['candidates'][-1]['classification'], result['candidates'][-1]['destinationCategory']))
        for pid, destination, expected in [(20, device + '\\Temp\\x', 'os-or-harness-actor'), (20, device, 'os-or-harness-actor'),
                                           (20, '\\Device\\HarddiskVolume9\\Windows\\x.dll', 'unknown'),
                                           (10, device + '\\Temp\\x', 'unknown'), (10, '\\Device\\HarddiskVolume9\\Windows\\x.dll', 'unknown')]:
            result = stage0.classify(self.controls + [self.event(pid, 'file', 'CreateFile', destination)], self.context, self.rules)
            self.assertEqual(expected, result['candidates'][-1]['classification'], (pid, destination))
        sibling = self.event(20, 'file', 'WriteFile', device + '-other\\x')
        self.assertEqual('out-of-declaration', stage0.classify(self.controls + [sibling], self.context, self.rules)['candidates'][-1]['classification'])


class SnapshotClassificationTests(unittest.TestCase):
    """MR8 diff-classify on synthetic snapshot triples. No registry, file-system walk, application or trace access."""
    LOCAL = 'C:\\Users\\fixture\\AppData\\Local'
    TEMP = LOCAL + '\\Temp'
    SANDBOX = TEMP + '\\gcam-uia-probe-fixture'
    RUN = TEMP + '\\gcam-todo43\\probe-fixture'
    LEASE = TEMP + '\\gcam-desktop-lease\\session-1.json'
    MARKER = 'HKCU\\Software\\GcamUiaProbe\\probe-fixture'
    E1 = 'HKCU\\Software\\Microsoft\\CTF\\CUAS\\DefaultCompositionWindow'
    E3 = 'C:\\ProgramData\\NVIDIA Corporation\\Drs\\nvAppTimestamps'

    def setUp(self):
        self.rules = json.loads((SCRIPT.parent / 'classification-rules.json').read_text())
        self.before = stage0.filetime('2026-10-14T00:00:00Z')
        self.inside = stage0.filetime('2026-10-14T00:00:15Z')
        self.after = stage0.filetime('2026-10-14T00:00:40Z')
        self.base = [
            {'c': 'k', 'p': 'HKCU', 't': self.before},
            {'c': 'k', 'p': self.E1, 't': self.before},
            {'c': 'v', 'p': self.E1, 'n': 'Left', 'k': 4, 'h': 'a' * 64},
            {'c': 'v', 'p': self.E1, 'n': 'Top', 'k': 4, 'h': 'b' * 64},
            {'c': 'd', 'p': self.LOCAL + '\\app', 't': self.before, 'a': 16},
            {'c': 'f', 'p': self.LOCAL + '\\app\\settings.json', 's': 1, 't': self.before, 'a': 32},
            {'c': 'f', 'p': self.E3, 's': 10, 't': self.before, 'a': 32},
        ]
        self.controls = [
            {'c': 'd', 'p': self.SANDBOX, 't': self.inside, 'a': 16},
            {'c': 'f', 'p': self.SANDBOX + '\\probe-diagnostic.txt', 's': 14, 't': self.inside, 'a': 32},
            {'c': 'k', 'p': self.MARKER, 't': self.inside},
            {'c': 'v', 'p': self.MARKER, 'n': 'RunId', 'k': 1, 'h': 'c' * 64},
        ]
        self.processes = [{'pid': 42, 'parentPid': 7, 'image': 'Gcam.Studio.exe', 'ending': 'exit 0'}]

    def run_folder(self, a, b, c, complete=True):
        root = StageZeroIsolationTests.scratch()
        roots = [{'category': 'LOCALAPPDATA', 'path': self.LOCAL}, {'category': 'TEMP', 'path': self.TEMP},
                 {'category': 'ProgramData', 'path': 'C:\\ProgramData'}, {'category': 'Sandbox', 'path': self.SANDBOX},
                 {'category': 'RunDirectory', 'path': self.RUN}]
        for label, rows in zip('ABC', (a, b, c)):
            with gzip.open(root / f'snapshot-{label}.jsonl.gz', 'wt', encoding='utf-8') as stream:
                for row in [{'c': 'header', 'schema': 'gcam.state-snapshot', 'schemaVersion': 1, 'label': label, 'roots': roots}] + rows \
                        + ([{'c': 'end'}] if complete or label != 'C' else []):
                    stream.write(json.dumps(row) + '\n')
        context = {'schemaVersion': 1, 'runId': 'probe-fixture',
                   'nullWindow': {'seconds': 12.0},
                   'studioWindow': {'launchAt': '2026-10-14T00:00:10.0000000+00:00', 'exitAt': '2026-10-14T00:00:20.1234567+00:00',
                                    'seconds': 10.1, 'marginBeforeSeconds': 1, 'marginAfterSeconds': 2},
                   'ownedProcesses': self.processes,
                   'harnessOwned': {'files': [self.SANDBOX, self.RUN, self.LEASE], 'registry': [self.MARKER]},
                   'positiveControls': {'file': self.SANDBOX + '\\probe-diagnostic.txt',
                                        'registry': {'key': self.MARKER, 'valueName': 'RunId'}, 'process': 'Gcam.Studio.exe'}}
        (root / 'observation-context.json').write_text(json.dumps(context), encoding='utf-8')
        return root

    def classify(self, extra_c=(), extra_b=(), replace=None, rules=None, complete=True):
        def apply(rows, extra):
            rows = [dict(row) for row in rows]
            for change in extra:
                rows = [row for row in rows if (row['p'], row.get('n')) != (change['p'], change.get('n'))]
                if not change.get('removed'):
                    rows.append(change)
            return rows
        b = apply(self.base, extra_b)
        c = apply(b + self.controls, extra_c)
        return stage0.diff_classify(self.run_folder(self.base, b, c, complete), rules or self.rules)

    def only(self, result, classification=None):
        rows = [row for row in result['candidates'] if row['classification'] != 'harness']
        self.assertEqual(1, len(rows), rows)
        if classification:
            self.assertEqual(classification, rows[0]['classification'], rows[0])
        return rows[0]

    def test_clean_run_sees_all_controls_and_is_eligible(self):
        result = self.classify()
        self.assertEqual(('completed', True), (result['runStatus'], result['g1Eligible']))
        self.assertEqual({'file': True, 'registry': True, 'process': True}, result['positiveControls'])
        self.assertEqual({'harness': 4}, result['classificationCounts'])
        self.assertEqual(['observation-context.json', 'snapshot-A.jsonl.gz', 'snapshot-B.jsonl.gz', 'snapshot-C.jsonl.gz'],
                         sorted(result['inputSha256']))

    def test_harness_owned_sandbox_run_folder_lease_and_marker(self):
        for path in (self.SANDBOX + '\\Roaming\\x.json', self.RUN + '\\probe.trx', self.LEASE):
            result = self.classify([{'c': 'f', 'p': path, 's': 1, 't': self.inside, 'a': 32}])
            self.assertEqual(0, len([r for r in result['candidates'] if r['classification'] != 'harness']), path)
        result = self.classify([{'c': 'f', 'p': self.SANDBOX + '-other\\x', 's': 1, 't': self.inside, 'a': 32}])
        self.only(result, 'out-of-declaration')  # a sibling of the sandbox is not owned

    def test_declared_e1_values_and_e3_content_change_only(self):
        result = self.classify([{'c': 'v', 'p': self.E1, 'n': 'Left', 'k': 4, 'h': 'd' * 64},
                                {'c': 'k', 'p': self.E1, 't': self.inside}])
        self.assertEqual(('declared', 'E1'), (self.only(result)['classification'], self.only(result)['rule']))
        self.assertTrue(result['g1Eligible'])
        result = self.classify([{'c': 'v', 'p': self.E1, 'n': 'Width', 'k': 4, 'h': 'd' * 64}, {'c': 'k', 'p': self.E1, 't': self.inside}])
        self.only(result, 'out-of-declaration')
        result = self.classify([{'c': 'v', 'p': self.E1, 'n': 'Top', 'removed': True}, {'c': 'k', 'p': self.E1, 't': self.inside}])
        self.only(result, 'out-of-declaration')  # E1 declares an update, not a deletion
        result = self.classify([{'c': 'f', 'p': self.E3, 's': 10, 't': self.inside, 'a': 32}])
        self.assertEqual('E3', self.only(result, 'declared')['rule'])
        result = self.classify([{'c': 'f', 'p': self.E3 + '.tmp', 's': 1, 't': self.inside, 'a': 32}])
        self.only(result, 'out-of-declaration')
        result = self.classify([{'c': 'f', 'p': self.E3, 'removed': True}])
        self.only(result, 'out-of-declaration')

    def test_change_outside_the_studio_window_is_not_of_this_window(self):
        for time in (self.before, self.after):
            result = self.classify([{'c': 'f', 'p': self.LOCAL + '\\other.log', 's': 1, 't': time, 'a': 32}])
            self.assertEqual('outside-studio-window', self.only(result, 'not-of-this-window')['rule'])
            self.assertFalse(self.only(result)['inStudioWindow'])
        margin = stage0.filetime('2026-10-14T00:00:22Z')  # exit + 1.88 s is inside the +2 s margin
        result = self.classify([{'c': 'f', 'p': self.LOCAL + '\\other.log', 's': 1, 't': margin, 'a': 32}])
        self.only(result, 'out-of-declaration')
        # A removed entry has no time of its own: the window rule cannot clear it.
        result = self.classify([{'c': 'f', 'p': self.LOCAL + '\\app\\settings.json', 'removed': True}])
        self.assertIsNone(self.only(result, 'out-of-declaration')['inStudioWindow'])

    def test_change_also_seen_in_the_null_window_is_another_actor(self):
        churn = self.LOCAL + '\\app\\settings.json'
        result = self.classify([{'c': 'f', 'p': churn, 's': 3, 't': self.inside, 'a': 32}],
                               extra_b=[{'c': 'f', 'p': churn, 's': 2, 't': self.before, 'a': 32}])
        self.assertEqual('changed-in-null-window', self.only(result, 'other-actor')['rule'])
        self.assertEqual(1, result['changeCounts']['A->B'])
        self.assertTrue(result['g1Eligible'])

    def test_recorded_background_filter_matches_the_redacted_target_and_is_validated(self):
        rules = copy.deepcopy(self.rules)
        rules['backgroundFilters'] = [{'pattern': r'<LOCALAPPDATA>\\cache\\.*', 'channel': 'file', 'reason': 'synthetic',
                                       'evidencedBy': 'probe-fixture'}]
        change = [{'c': 'f', 'p': self.LOCAL + '\\cache\\x.bin', 's': 1, 't': self.inside, 'a': 32}]
        self.assertTrue(self.only(self.classify(change, rules=rules), 'other-actor')['rule'].startswith('filter:'))
        rules['backgroundFilters'][0]['entry'] = 'directory'
        self.only(self.classify(change, rules=rules), 'out-of-declaration')  # an entry-restricted filter skips files
        del rules['backgroundFilters'][0]['entry']
        rules['backgroundFilters'][0]['channel'] = 'registry'
        self.only(self.classify(change, rules=rules), 'out-of-declaration')  # the channel must match too
        rules['backgroundFilters'] = [{'pattern': '.*', 'channel': 'file', 'reason': '', 'evidencedBy': 'probe-fixture'}]
        with self.assertRaises(ValueError):
            self.classify(change, rules=rules)

    def test_change_inside_another_git_working_tree_is_another_actor(self):
        repo = self.LOCAL + '\\repo'
        tree = [{'c': 'd', 'p': repo, 't': self.before, 'a': 16}, {'c': 'd', 'p': repo + '\\.git', 't': self.before, 'a': 16}]
        result = self.classify([{'c': 'f', 'p': repo + '\\out.log', 's': 1, 't': self.inside, 'a': 32}], extra_b=tree)
        self.assertEqual('other-git-working-tree', self.only(result, 'other-actor')['rule'])
        # A folder beside the working tree (no `.git` above it) stays a candidate.
        result = self.classify([{'c': 'f', 'p': self.LOCAL + '\\repo-x\\out.log', 's': 1, 't': self.inside, 'a': 32}], extra_b=tree)
        self.only(result, 'out-of-declaration')

    def test_out_of_declaration_stops_the_run_and_the_table_is_redacted(self):
        result = self.classify([{'c': 'f', 'p': self.LOCAL + '\\app\\settings.json', 's': 2, 't': self.inside, 'a': 32},
                                {'c': 'v', 'p': 'HKCU\\Software\\S-1-5-21-1-2-3-1001\\x', 'n': self.LOCAL + '\\a.exe', 'k': 3, 'h': 'e' * 64},
                                {'c': 'k', 'p': 'HKCU\\Software\\S-1-5-21-1-2-3-1001\\x', 't': self.inside}],
                               )
        self.assertEqual(('unsafe_effect', 'undeclared-app-effect', False), (result['runStatus'], result['reason'], result['g1Eligible']))
        paths = {(row['path'], row['valueName']) for row in result['candidates'] if row['classification'] == 'out-of-declaration'}
        self.assertIn(('<LOCALAPPDATA>\\app\\settings.json', None), paths)
        self.assertIn(('HKCU\\Software\\<SID>\\x', '<LOCALAPPDATA>\\a.exe'), paths)
        text = json.dumps(result)
        self.assertNotIn('fixture\\\\AppData', text); self.assertNotIn('S-1-5-21', text)
        named = stage0.diff_classify(self.run_folder(self.base, self.base, self.base + self.controls + [
            {'c': 'f', 'p': 'D:\\data\\leonfixture\\x', 's': 1, 't': self.inside, 'a': 32}]), self.rules, {'USER': 'leonfixture'})
        self.assertEqual('D:\\data\\<USER>\\x', self.only(named)['path'])

    def test_missing_controls_or_incomplete_snapshot_block_the_run(self):
        controls = self.controls
        self.controls = [row for row in controls if not row['p'].endswith('probe-diagnostic.txt')]
        result = self.classify()
        self.assertEqual(('blocked', 'instrument-not-proven', False), (result['runStatus'], result['reason'], result['g1Eligible']))
        self.assertFalse(result['positiveControls']['file'])
        self.controls = [row for row in controls if row['c'] != 'v']
        self.assertFalse(self.classify()['positiveControls']['registry'])
        self.controls, self.processes = controls, []
        self.assertFalse(self.classify()['positiveControls']['process'])
        self.processes = [{'pid': 42, 'image': 'Gcam.Studio.exe'}]
        self.assertEqual('snapshot-incomplete', self.classify(complete=False)['reason'])

    def test_metadata_only_directory_and_key_times_are_not_candidates_but_a_readability_flip_is_unclassified(self):
        result = self.classify([{'c': 'd', 'p': self.LOCAL + '\\app', 't': self.inside, 'a': 16}, {'c': 'k', 'p': self.E1, 't': self.inside}])
        self.assertEqual({'harness': 4}, result['classificationCounts'])
        root = self.run_folder(self.base, self.base, self.base + self.controls + [
            {'c': 'u', 'ch': 'file', 'p': self.LOCAL + '\\locked', 'e': 'UnauthorizedAccessException'}])
        result = stage0.diff_classify(root, self.rules)
        self.assertEqual(('blocked', 'unclassified-candidate'), (result['runStatus'], result['reason']))
        self.assertEqual('unreadable-in-one-snapshot', self.only(result, 'unclassified')['rule'])

    def test_preflight_checks_the_wpr_profile_only_for_the_optional_trace(self):
        import xml.etree.ElementTree as ET
        doc = ET.parse(SCRIPT.parent / 'Probe.wprp')
        ET.SubElement(doc.find('./Profiles/SystemProvider/Keywords'), 'Keyword', Value='NetworkTrace')
        root = StageZeroIsolationTests.scratch(); fixture = root / 'profile.wprp'; doc.write(fixture)
        check = lambda **flags: stage0.preflight(root / 'probe-fixture', live_pin=lambda: {}, identity=lambda: ('a', '2026-10-14T00:00:00Z'),
                                                 version=lambda: 7, scratch_root=root, profile_path=fixture, **flags)
        self.assertFalse(any('profile' in issue for issue in check()))
        self.assertTrue(any('profile selects' in issue for issue in check(trace=True)))
        run = root / 'probe-20261014-06'; run.mkdir()
        (run / 'snapshot-A.jsonl.gz').write_bytes(b'retained')
        self.assertTrue(any('snapshot-A' in issue for issue in stage0.preflight(run, live_pin=lambda: {}, identity=lambda: ('a', 'x'),
                                                                                version=lambda: 7, scratch_root=root)))

    def test_cli_writes_a_fresh_result_and_exits_nonzero_when_not_eligible(self):
        import subprocess, sys
        root = self.run_folder(self.base, self.base, self.base + self.controls)
        out = root / 'classified.json'
        command = [sys.executable, '-B', str(SCRIPT), 'diff-classify', '--run-directory', str(root), '--out', str(out)]
        self.assertEqual(0, subprocess.run(command, capture_output=True, text=True).returncode)
        self.assertTrue(json.loads(out.read_text())['g1Eligible'])
        self.assertNotEqual(0, subprocess.run(command, capture_output=True, text=True).returncode)  # never overwrites
        blocked = self.run_folder(self.base, self.base, self.base)
        self.assertEqual(2, subprocess.run(command[:5] + [str(blocked), '--out', str(blocked / 'classified.json')],
                                           capture_output=True, text=True).returncode)


if __name__ == '__main__':
    unittest.main()
