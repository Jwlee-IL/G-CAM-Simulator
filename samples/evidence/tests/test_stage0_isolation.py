"""Synthetic trace and policy negative controls. No registry, application, network or ETW access."""
import copy
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
        events = stage0.parse_trace(path)
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
        events = stage0.parse_trace(path)
        self.assertEqual(('file', 10, 'ambient.json'), (events[0]['channel'], events[0]['pid'], events[0]['destination']))
        self.assertEqual('unknown', events[1]['channel'])


if __name__ == '__main__':
    unittest.main()
