"""Runner adapters. No verdict is inferred from diagnostic message wording."""
import json
import re
import xml.etree.ElementTree as ET

TRX_NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
GATES = {
    'GCAM_EVIDENCE_TESTS': 'numerical evidence',
    'GCAM_RENDER_SNAPSHOTS': 'offscreen rendering',
    'GCAM_UI_TESTS': 'desktop',
}


def skip_reason(text, switches):
    for switch in GATES:
        if switch in text and switches.get(switch) != '1':
            return 'opt_in_off', switch
    return 'runner_skip_unknown', None


def trx_cases(data, switches):
    root = ET.fromstring(data)
    definitions = {}
    for definition in root.findall('t:TestDefinitions/t:UnitTest', TRX_NS):
        method = definition.find('t:TestMethod', TRX_NS)
        if method is None:
            raise ValueError('trx: missing method definition')
        definitions[definition.get('id')] = method.attrib
    cases = []
    for result in root.findall('t:Results/t:UnitTestResult', TRX_NS):
        method = definitions.get(result.get('testId'))
        if method is None:
            raise ValueError('trx: undefined test result')
        raw = result.get('outcome')
        verdict = {'Passed': 'passed', 'Failed': 'failed', 'NotExecuted': 'not_executed'}.get(raw)
        if verdict is None:
            raise ValueError('trx: unsupported outcome ' + str(raw))
        duration = result.get('duration', '')
        if not re.fullmatch(r'[0-9]+:[0-9]{2}:[0-9]{2}(?:\.[0-9]+)?', duration):
            raise ValueError('trx: invalid duration')
        h, m, s = duration.split(':')
        seconds = int(h) * 3600 + int(m) * 60 + float(s)
        assembly = method['className'].rsplit('.', 1)[0]
        message = result.find('t:Output/t:ErrorInfo/t:Message', TRX_NS)
        text = message.text or '' if message is not None else ''
        case = {
            'id': assembly + '::' + result.get('testName', ''),
            'unit': assembly + '::' + method['className'],
            'selector': assembly + '::' + method['className'] + '.' + method['name'],
            'displayName': result.get('testName'), 'adapterId': result.get('testId'),
            'rawOutcome': raw, 'verdict': verdict, 'durationSeconds': seconds,
            'started': result.get('startTime'), 'finished': result.get('endTime'),
            'failureKind': 'unknown' if verdict == 'failed' else None,
            'reasonText': text,
        }
        if verdict == 'not_executed':
            case['skipReasonCode'], case['switch'] = skip_reason(text, switches)
        cases.append(case)
    if not cases:
        raise ValueError('trx: empty results')
    return cases, root.find('t:Times', TRX_NS).attrib


def unittest_cases(data, switches):
    result = json.loads(data)
    if result.get('schemaVersion') != 1:
        raise ValueError('unittest: unsupported schema')
    cases = []
    for item in result['cases']:
        name = item['name']
        unit = 'unittest::' + name.rsplit('.', 1)[0]
        case = {'id': 'unittest::' + name, 'unit': unit,
                'selector': 'unittest::' + name, 'displayName': name,
                'rawOutcome': item['outcome'], 'verdict': item['outcome'],
                'durationSeconds': item['durationSeconds'],
                'failureKind': item.get('failureKind'), 'reasonText': item.get('reason', '')}
        if 'parts' in item:
            case['parts'] = item['parts']
        if case['verdict'] == 'not_executed':
            if name.endswith('test_dirty_diff_on_temporary_repository') and switches.get('GCAM_PROVENANCE_GIT_TESTS') != '1':
                case['skipReasonCode'] = 'opt_in_off'
                case['switch'] = 'GCAM_PROVENANCE_GIT_TESTS'
            else:
                case['skipReasonCode'] = 'runner_skip_unknown'
        cases.append(case)
    if not cases:
        raise ValueError('unittest: empty results')
    return cases, {}


def cocotb_cases(data, configuration, module, switches):
    root = ET.fromstring(data)
    cases = []
    for result in root.findall('.//testcase'):
        name = result.get('name')
        failure, error, skip = (result.find(key) for key in ('failure', 'error', 'skipped'))
        verdict = 'failed' if failure is not None or error is not None else 'not_executed' if skip is not None else 'passed'
        unit = f'cocotb::{module}@{configuration}'
        case = {'id': unit + '.' + name, 'unit': unit, 'selector': unit + '.' + name,
                'displayName': name, 'rawOutcome': verdict, 'verdict': verdict,
                'durationSeconds': float(result.get('time', 0)),
                'failureKind': 'error' if error is not None else 'unknown' if failure is not None else None,
                'reasonText': ''.join(ET.tostring(e, encoding='unicode') for e in (failure, error, skip) if e is not None),
                'simulationTime': result.get('sim_time_ns'),
                'simulationTimeUnit': 'ns' if result.get('sim_time_ns') is not None else None}
        if skip is not None:
            case['skipReasonCode'] = ('optional_vectors_off' if name == 'csharp_vectors_match_python_and_rtl'
                                      and not switches.get('GCAM_CRRC_VECTORS') else 'runner_skip_unknown')
        cases.append(case)
    if not cases:
        raise ValueError('cocotb: empty results')
    return cases, {}
