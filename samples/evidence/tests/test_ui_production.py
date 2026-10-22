"""v4 producer gates and portable final-review lists; no application or desktop launch."""
import copy
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
import sys as _sys
if str(Path(__file__).resolve().parent) not in _sys.path:  # the helper beside this file, under any runner
    _sys.path.insert(0, str(Path(__file__).resolve().parent))
from scratch_fixtures import ScratchTestCase, scratch as owned_scratch
import uuid
from unittest.mock import patch
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'samples/testing'))
import ui_contract as uc
import test_records as tr
spec = importlib.util.spec_from_file_location('manual_v4_fixture', ROOT / 'samples/manual/manual_records.py')
mr = importlib.util.module_from_spec(spec); spec.loader.exec_module(mr)


def scratch():
    return owned_scratch('gcam-fixture-')


def passing(run='fixture'):
    return {'contract': 'v4', 'runId': run, 'result': 'passed', 'executedStepIds': ['action-0001'],
        'videoStatus': {'contract': 'v4', 'runId': run, 'status': 'passed', 'videoSha256': 'b' * 64,
            'check': {'contract': 'v4', 'videoSha256': 'b' * 64, 'passed': True, 'failedCriteria': []},
            'evidence': {'steps': [{'id': 'action-0001'}]}}}


class UiProductionTests(ScratchTestCase):
    def test_old_contract_replays_and_unknown_contract_refuses(self):
        self.assertIsNone(uc.video_status({'runId': 'old'}))
        with self.assertRaisesRegex(ValueError, 'unsupported run contract'):
            uc.video_status({'contract': 'v5'})

    def test_exact_run_hash_check_contract_and_complete_step_inventory_are_required(self):
        good = passing(); self.assertEqual(good['videoStatus'], uc.video_status(good))
        mutations = [lambda r: r.pop('executedStepIds'), lambda r: r.pop('videoStatus'), lambda r: r['videoStatus'].update(runId='other'),
            lambda r: r['videoStatus']['check'].update(contract='old'), lambda r: r['videoStatus'].update(videoSha256='c' * 64),
            lambda r: r['videoStatus']['check'].update(passed=False), lambda r: r['videoStatus']['check'].update(failedCriteria=['step-coverage']),
            lambda r: r.update(executedStepIds=['outside-recording'])]
        for mutate in mutations:
            with self.subTest(mutate=mutate):
                record = copy.deepcopy(good); mutate(record)
                with self.assertRaisesRegex(ValueError, 'video-gate:'): uc.video_status(record)

    def test_failed_aborted_and_changed_attempts_are_retained_but_review_never_blocks(self):
        good = {'runId': 'pass', 'scenario': 'same', 'sourceSha256': 'a', 'verdict': 'passed', 'videoStatus': passing()['videoStatus']}
        rows = [good, {**good, 'runId': 'changed', 'scenario': 'changed'},
                {**good, 'runId': 'failed', 'verdict': 'failed'}, {**good, 'runId': 'aborted', 'execution': 'aborted'},
                {**good, 'runId': 'bad-video', 'videoStatus': None}]
        result = uc.review_list(rows, {'same': 'a', 'changed': 'b'}, {'id': 'previous'})
        self.assertFalse(result['blocking'])
        self.assertEqual(['changed', 'failed', 'aborted', 'bad-video'], [a['runId'] for a in result['attempts']])

    def test_desktop_collection_refuses_without_an_explicit_author_package_before_running_commands(self):
        with patch.object(tr, 'command', side_effect=AssertionError('must not build or launch')):
            with self.assertRaisesRegex(ValueError, '--approvals is required'):
                tr.collect(scratch(), 'dotnet', profile='desktop', previous='none')

    def test_author_package_targets_the_live_build_and_charter(self):
        live = {'workingTree': 'clean', 'sourceCommit': 'a' * 40, 'isolationSha256': 'i', 'deploymentSha256': 'd'}
        at = '2026-10-22T00:00:00Z'
        package = {'contract': 'v4', 'runId': 'gcam-batch-fixture', 'commit': 'a' * 40, 'committedAt': at,
            'isolationSha256': 'i', 'deploymentSha256': 'd', 'approvals': [
                {'id': a, 'humanActId': a, 'quote': 'Synthetic statement', 'targetCommit': 'a' * 40, 'quotedAt': at}
                for a in ('charter', 'desktop-go')],
            'charter': {'approvalId': 'charter', 'status': 'approved', 'scope': 'synthetic', 'scenarioIds': ['case'],
                'gates': [{'id': 'G0', 'status': 'passed', 'reason': 'approved synthetic charter'},
                          {'id': 'G1', 'status': 'passed', 'reason': 'pending synthetic observation'}]}}
        path = scratch() / 'approvals.json'
        path.write_text(json.dumps(package), encoding='utf-8')
        self.assertEqual(path, uc.validate_approval(path, live, ('a' * 40, at)))
        for defect in ({'runId': 'batch-fixture'}, {'commit': 'c' * 40}, {'approvals': []}, {'charter': None}, {'contract': None}):
            with self.subTest(defect=defect):
                path.write_text(json.dumps({**package, **defect}), encoding='utf-8')
                with self.assertRaises(ValueError): uc.validate_approval(path, live, ('a' * 40, at))

    def test_collector_reads_only_the_manifest_emitted_by_the_exact_trx_case(self):
        root = scratch(); path = root / 'manifest.json'; path.write_text(json.dumps(passing()), encoding='utf-8')
        ns = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
        xml = ET.fromstring(f'<TestRun xmlns="{ns}"><Results><UnitTestResult testId="1"><Output><StdOut>manifest: {path}</StdOut></Output></UnitTestResult></Results></TestRun>')
        selector = next(s for s in tr.desktop_selectors() if 'ScenarioTests.' in s)
        unit = selector.rsplit('.', 1)[0]
        case = {'selector': selector, 'unit': unit, 'id': selector, 'adapterId': '1', 'verdict': 'passed'}
        result = tr.collect_desktop_evidence(xml, [case], {unit: 'source'})
        self.assertEqual('fixture', result[selector]['runId'])
        path.write_text(json.dumps({**passing(), 'videoStatus': None}), encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'video-gate:'): tr.collect_desktop_evidence(xml, [case], {unit: 'source'})

    def test_exempt_classes_are_explicit_and_no_other_case_can_claim_the_exemption(self):
        selector = next(s for s in tr.desktop_selectors() if '.ObservedRunTests.' in s)
        case = {'selector': selector, 'unit': selector.rsplit('.', 1)[0], 'id': selector, 'verdict': 'passed'}
        self.assertTrue(tr.collect_desktop_evidence(ET.fromstring('<TestRun/>'), [case], {})[selector]['exempt'])

    def test_refused_production_retains_the_run_record_and_blocks_selection_replay(self):
        root = scratch(); folder = root / 'refused'; folder.mkdir()
        source = {'dirty': False, 'snapshotSha256': 'synthetic'}
        env = {'switches': {'GCAM_UI_TESTS': '1', 'GCAM_UI_CACHE': '0'}}
        with patch.object(tr, 'source_identity', return_value=source), patch.object(tr, 'environment', return_value=env):
            record = tr.finish_record(root, folder, 'dotnet', ['dotnet', 'test'], {}, source, {}, {},
                '2026-10-22T00:00:00Z', '2026-10-22T00:00:01Z', 0, [], [], [], [], {'sdk': 'synthetic'},
                profile='desktop', desktop_evidence={}, production_refusal='video-gate: synthetic failed video')
        self.assertEqual('refused', record['productionStatus'])
        self.assertTrue((folder / 'run-record.json').is_file())
        self.assertEqual('refused', json.loads((root / 'selection.json').read_text())['records'][0]['runId'])
        with self.assertRaisesRegex(ValueError, 'video-gate: desktop production refused'):
            tr.validate_record(record, folder)

    def test_collection_keeps_a_trx_pass_with_failed_video_as_a_refused_attempt(self):
        root = scratch(); attempt = root / 'manifest.json'
        attempt.write_text(json.dumps({**passing(), 'videoStatus': None}), encoding='utf-8')
        selector = sorted(tr.desktop_selectors())[0]
        full = selector.split('::')[1]; cls, method = full.rsplit('.', 1); unit = selector.rsplit('.', 1)[0]
        ns = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
        xml = f'''<TestRun xmlns="{ns}"><Times start="2026-10-22T00:00:00Z" finish="2026-10-22T00:00:01Z"/>
          <Results><UnitTestResult testId="1" testName="{full}" outcome="Passed" duration="00:00:01">
          <Output><StdOut>manifest: {attempt}</StdOut></Output></UnitTestResult></Results><TestDefinitions>
          <UnitTest id="1"><TestMethod className="{cls}" name="{method}"/></UnitTest></TestDefinitions></TestRun>'''
        def command(args, env):
            folder = Path(args[args.index('--results-directory') + 1]); (folder / 'results.trx').write_text(xml, encoding='utf-8')
            return 0, 'synthetic runner'
        files = {'tests/fake.cs': 'a' * 64}
        source = {'commit': 'a' * 40, 'dirty': False, 'files': files, 'snapshotSha256': tr.sha(tr.canonical(files))}
        with patch.object(tr, 'live_approval', return_value=root / 'author-package.json'), \
             patch.object(tr, 'source_identity', return_value=source), patch.object(tr, 'binaries', return_value={'fake.dll': 'b' * 64}), \
             patch.object(tr, 'source_units', return_value={unit: {'sources': files}}), \
             patch.object(tr, 'discover', return_value=([full], 'synthetic discovery')), \
             patch.object(tr, 'version', return_value='synthetic'), patch.object(tr, 'command', side_effect=command):
            self.assertEqual(1, tr.collect(root / 'bundle', 'dotnet', no_build=True, profile='desktop', approvals=root / 'author-package.json', previous='none'))
        record_path = next((root / 'bundle').glob('dotnet/*/run-record.json'))
        record = json.loads(record_path.read_text())
        self.assertTrue(record['runId'].startswith('gcam-'))
        self.assertEqual('refused', record['productionStatus'])
        self.assertEqual('passed', record['cases'][0]['verdict'])  # Product truth is never rewritten to make a video pass.
        review = json.loads((record_path.parent / 'final-review.json').read_text())
        self.assertEqual('failed-or-aborted', review['attempts'][0]['reason'])

    def test_manual_generation_refuses_a_v4_pass_without_a_passing_video_before_render(self):
        sources, runs_root = mr.fixture(scratch())
        sources = mr.load_sources(sources)
        runs = mr.load_runs(runs_root, sources)
        record = runs[0][1]; record['contract'] = 'v4'; record.pop('videoStatus', None)
        with self.assertRaisesRegex(ValueError, 'video-gate:'):
            mr.assemble(sources, runs, 'fixture', '2026-10-22', False, mr.DEFAULT_BUDGET, previous={'id': 'none', 'sources': {}})

    def test_manual_requires_an_explicit_predecessor_for_new_v4_runs(self):
        sources, runs_root = mr.fixture(scratch()); sources = mr.load_sources(sources)
        runs = mr.load_runs(runs_root, sources); runs[0][1]['contract'] = 'v4'
        with self.assertRaisesRegex(ValueError, 'final-review:'):
            mr.assemble(sources, runs, 'fixture', '2026-10-22', False, mr.DEFAULT_BUDGET)

    def test_new_v4_edition_carries_its_exact_checks_and_review_list_and_replays(self):
        root = scratch(); sources, runs_root = mr.fixture(root)
        for path in runs_root.glob('*/manual-run.json'):
            record = json.loads(path.read_text(encoding='utf-8'))
            record.update(contract='v4', videoStatus=passing(record['runId'])['videoStatus'], scenarioSourceSha256='a' * 64)
            path.write_text(json.dumps(record), encoding='utf-8')
        out = root / 'edition'
        outputs = mr.generate(sources, runs_root, 'v4-fixture', '2026-10-22', out, previous='none')
        review = json.loads(outputs['final-review.json'])
        self.assertEqual(2, len(review['attempts'])); self.assertFalse(review['blocking'])
        self.assertEqual('v4', mr.check(out)['contract'])

    # ------------------------------------------------------------ turn C2

    def test_desktop_selectors_match_only_attribute_usages(self):
        project = scratch()
        (project / 'A.cs').write_text('''namespace X;
public sealed class AlphaTests
{
    [DesktopFact]
    public void Plain() { }
    [DesktopFact(longRunning: true)]
    public void WithArguments() { }
    [DesktopFact, ManualChapter("manual.navigate")]
    public void Combined() { }
    [Trait("kind", "ui"), DesktopTheory]
    [InlineData("a")]
    // a comment between the attributes and the method
    public void TheoryAfterAnotherAttribute(string a) { }
    [Fact]
    public void Key_UsesAMemberOfTheAttributeClass()
    {
        var switches = new Dictionary<string, string> { [DesktopFactAttribute.LongOptInVariable] = "1" };
    }
    [DesktopFactAttribute]
    public async Task FullName() { }
    [DesktopFactory]
    public void LongerName() { }
}
''', encoding='utf-8')
        names = {s.rsplit('.', 1)[1] for s in tr.desktop_selectors(project)}
        self.assertEqual({'Plain', 'WithArguments', 'Combined', 'TheoryAfterAnotherAttribute', 'FullName'}, names)
        real = tr.desktop_selectors()
        self.assertFalse([s for s in real if '.ResultCacheTests.' in s])
        self.assertIn('Gcam.Studio.UiTests::Gcam.Studio.UiTests.ManualScenarioTests.Navigate_WindowWorkspacesAndViewTabs', real)

    def test_production_refuses_a_package_whose_g1_is_blocked(self):
        live = {'workingTree': 'clean', 'sourceCommit': 'a' * 40, 'isolationSha256': 'i', 'deploymentSha256': 'd'}
        at = '2026-10-22T00:00:00Z'
        package = {'contract': 'v4', 'runId': 'gcam-batch-fixture', 'commit': 'a' * 40, 'committedAt': at,
            'isolationSha256': 'i', 'deploymentSha256': 'd', 'approvals': [
                {'id': a, 'humanActId': a, 'quote': 'Synthetic statement', 'targetCommit': 'a' * 40, 'quotedAt': at}
                for a in ('charter', 'desktop-go')],
            'charter': {'approvalId': 'charter', 'status': 'approved', 'scope': 'synthetic', 'scenarioIds': ['case'],
                'gates': [{'id': 'G0', 'status': 'passed', 'reason': 'approved synthetic charter'},
                          {'id': 'G1', 'status': 'blocked', 'reason': 'observed run pending'}]}}
        path = scratch() / 'approvals.json'
        path.write_text(json.dumps(package), encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'production requires passed G0/G1'):
            uc.validate_approval(path, live, ('a' * 40, at))

    def test_a_v4_record_with_an_empty_or_unlisted_step_inventory_is_refused(self):
        for mutate in (lambda r: r.update(executedStepIds=[]), lambda r: r.update(executedStepIds='action-0001'),
                       lambda r: r.update(steps=[{'videoStepId': 'action-0009'}]),
                       lambda r: r.update(video={'runId': 'fixture', 'videoSha256': 'c' * 64})):
            with self.subTest(mutate=mutate):
                record = passing(); mutate(record)
                with self.assertRaisesRegex(ValueError, 'video-gate:'): uc.video_status(record)
        record = passing(); record['video'] = 'disabled'  # the scenario record's note string is not provenance
        self.assertIsNotNone(uc.video_status(record))

    @staticmethod
    def v4_runs(root):
        sources, runs_root = mr.fixture(root)
        for path in runs_root.glob('*/manual-run.json'):
            record = json.loads(path.read_text(encoding='utf-8'))
            record.update(contract='v4', videoStatus=passing(record['runId'])['videoStatus'], scenarioSourceSha256='a' * 64)
            path.write_text(json.dumps(record), encoding='utf-8')
        return sources, runs_root

    def test_manual_checks_only_the_chosen_runs_and_lists_a_superseded_failed_video(self):
        root = scratch(); sources, runs_root = self.v4_runs(root)
        chosen = json.loads(next(runs_root.glob('*/manual-run.json')).read_text(encoding='utf-8'))
        old = dict(chosen, runId='run-superseded', startedAt='2026-10-13T09:00:00+00:00')
        old['videoStatus'] = dict(passing('run-superseded')['videoStatus'], status='failed')
        old['videoStatus']['check'] = dict(old['videoStatus']['check'], passed=False, failedCriteria=['step-coverage'])
        (runs_root / 'run-superseded').mkdir()
        (runs_root / 'run-superseded' / 'manual-run.json').write_text(json.dumps(old), encoding='utf-8')
        outputs = mr.generate(sources, runs_root, 'v4-superseded', '2026-10-22', root / 'edition', previous='none')
        review = json.loads(outputs['final-review.json'])
        self.assertFalse(review['blocking'])
        self.assertIn(('run-superseded', 'failed-or-aborted'), [(a['runId'], a['reason']) for a in review['attempts']])

    def test_a_new_manual_edition_refuses_old_contract_runs_and_a_missing_predecessor(self):
        root = scratch(); sources, runs_root = mr.fixture(root, legacy=True)
        with self.assertRaisesRegex(ValueError, 'final-review:'):
            mr.generate(sources, runs_root, 'legacy-new', '2026-10-22', root / 'no-previous')
        with self.assertRaisesRegex(ValueError, 'video-gate: new editions require v4 chosen runs'):
            mr.generate(sources, runs_root, 'legacy-new', '2026-10-22', root / 'legacy', previous='none')
        self.assertFalse((root / 'legacy').exists())

    def test_a_refused_manual_generation_leaves_no_edition_folder_and_writes_the_list_beside_it(self):
        root = scratch(); sources, runs_root = self.v4_runs(root)
        path = next(runs_root.glob('*/manual-run.json'))
        record = json.loads(path.read_text(encoding='utf-8'))
        record['videoStatus']['check']['passed'] = False
        path.write_text(json.dumps(record), encoding='utf-8')
        out = root / 'editions' / 'refused-1'
        for attempt in range(2):  # a repeated refusal never overwrites the first list nor masks the refusal
            with self.subTest(attempt=attempt):
                with self.assertRaisesRegex(ValueError, 'video-gate:'):
                    mr.generate(sources, runs_root, 'refused-1', '2026-10-22', out, previous='none')
                self.assertFalse(out.exists())
                review = json.loads((out.parent / 'refused-1.final-review.json').read_text(encoding='utf-8'))
                self.assertFalse(review['blocking'])
        # The edition ID is still free once the run is fixed.
        record['videoStatus']['check']['passed'] = True
        path.write_text(json.dumps(record), encoding='utf-8')
        mr.generate(sources, runs_root, 'refused-1', '2026-10-22', out, previous='none')
        self.assertEqual('v4', mr.check(out)['contract'])


if __name__ == '__main__': unittest.main()
