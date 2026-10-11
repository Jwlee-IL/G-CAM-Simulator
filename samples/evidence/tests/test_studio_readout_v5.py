"""V5 declared two-cell repair: analytic oracles and complete CLI publication."""
import copy
import json
import math
import os
import pathlib
import sys
import tempfile
import unittest
from unittest import mock

HERE = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(HERE))
import studio_readout_v5_select as selector
import studio_readout_v5_prepare as prepare


class StudioReadoutRuleV5Tests(unittest.TestCase):
    def test_development_budget_requires_all_four_and_smallest_success(self):
        seeds = list(range(4))
        def rows(geometry, level):
            n = 34 if geometry == 'Wide' else 44
            return [{'Geometry': geometry, 'Budget': level, 'Seed': s, 'EnergyKeV': 661.7,
                     'Scored': n * n * level, 'Succeeded': True, 'Markers': n * n, 'Failure': None} for s in seeds]
        low, high = rows('Wide', 8000), rows('Wide', 32000)
        self.assertEqual(prepare.select_budget(low + high, 'Wide', seeds), 8000)
        low[-1].update(Succeeded=False, Markers=1155, Failure='unsupported slot (0, 17)')
        self.assertEqual(prepare.select_budget(low + high, 'Wide', seeds), 32000)
        with self.assertRaisesRegex(ValueError, 'incomplete'):
            prepare.select_budget(high, 'Wide', seeds)
        high[-1].update(Succeeded=False, Markers=1155, Failure='unsupported slot (0, 17)')
        with self.assertRaisesRegex(ValueError, 'no candidate'):
            prepare.select_budget(low + high, 'Wide', seeds)
        with self.assertRaisesRegex(ValueError, 'incomplete'):
            prepare.select_budget(rows('High', 64000)[:-1], 'High', seeds)

    def images(self, x=0.1, scale=2):
        return [{'TargetDirectCounts': count, 'RateCps': rate, 'Position': pos,
                 'MeanEmittedHistories': 10000, 'ExposurePilotDirect': 100,
                 'Trials': [{'Trial': t,
                             'Direct': {'ExDeg': x, 'EyDeg': 0, 'Remote': False, 'Count': count},
                             'Anger': {'ExDeg': x * scale, 'EyDeg': 0, 'Remote': False, 'Count': count / 2}}
                            for t in range(2)]}
                for count in (1000, 4000) for rate in (0, 100000) for pos in range(9)]

    def test_calibration_offer_and_literal_refusal(self):
        rows = [{'Seed': s, 'CalibrationSucceeded': True, 'Failure': None} for s in range(32)]
        self.assertTrue(selector.offer(rows, list(range(32)))['Offered'])
        rows[31].update(CalibrationSucceeded=False, Failure='unsupported slot (0, 17)')
        result = selector.offer(rows, list(range(32)))
        self.assertFalse(result['Offered'])
        self.assertEqual(result['Failures'], [{'Seed': 31, 'Failure': 'unsupported slot (0, 17)'}])

    def test_missing_duplicate_and_corrupt_status_fail_closed(self):
        rows = [{'Seed': s, 'CalibrationSucceeded': True, 'Failure': None} for s in range(32)]
        for mutation in ('missing', 'duplicate', 'status'):
            changed = copy.deepcopy(rows)
            if mutation == 'missing': changed.pop()
            elif mutation == 'duplicate': changed[-1]['Seed'] = 0
            else: changed[0]['CalibrationSucceeded'] = 1
            with self.assertRaises(ValueError): selector.offer(changed, list(range(32)))

    def test_cost_interval_and_paired_ratio_oracles(self):
        rows = [{'Imaging': self.images(x)} for x in (1, 1, 1, 3)]
        for image in rows[-1]['Imaging']:
            for trial in image['Trials']:
                trial['Direct']['Remote'] = True
        result = selector.costs(rows, 1000, 0, 2)
        d = result['Paths']['Direct']['RmsDeg']
        self.assertAlmostEqual(d['Estimate'], math.sqrt(3))
        self.assertEqual(d['Lower95'], 0)
        self.assertAlmostEqual(d['Upper95'], math.sqrt(3 + 2 * 3.182446305284263))
        self.assertEqual(result['F'], {'Estimate': 4, 'Lower95': 4, 'Upper95': 4, 'Clusters': 4})
        self.assertEqual(result['Paths']['Direct']['RemoteProbability'],
                         {'Estimate': .25, 'Lower95': 0, 'Upper95': 1, 'Clusters': 4})
        rows = [{'Imaging': self.images(1, scale)} for scale in (1, 1, 1, 3)]
        f = selector.costs(rows, 4000, 100000, 2)['F']
        self.assertEqual(f['Estimate'], 3)
        half = 3.182446305284263 * 2 / 3
        self.assertAlmostEqual(f['Lower95'], 3 * math.exp(-half))
        self.assertAlmostEqual(f['Upper95'], 3 * math.exp(half))

    def test_no_survivor_only_cost_and_bias_does_not_cancel(self):
        rows = [{'Imaging': self.images(1)} for _ in range(4)]
        for r in rows:
            for image in r['Imaging']:
                for t in image['Trials']: t['Direct']['ExDeg'] = 1 if image['Position'] % 2 else -1
        self.assertEqual(selector.costs(rows, 1000, 0, 2)['Paths']['Direct']['BiasDeg']['Estimate'], 1)
        for image in rows[0]['Imaging']:
            for t in image['Trials']: t['Anger'] = None
        self.assertFalse(selector.costs(rows, 1000, 0, 2)['Paths']['Anger']['Available'])
        self.assertIsNone(selector.costs(rows, 1000, 0, 2)['F'])

    def fixture(self):
        pv = selector.pv
        parent = pathlib.Path(os.environ.get('GCAM_PROVENANCE_TEST_ROOT',
            str(pathlib.Path(tempfile.gettempdir()) / 'gcam-todo41/v5-test-fixtures')))
        parent.mkdir(parents=True, exist_ok=True)
        root = pathlib.Path(tempfile.mkdtemp(prefix='v5-ensemble-', dir=parent))
        evidence = root / 'samples/evidence'; evidence.mkdir(parents=True)
        for name in ('studio_readout_v5_driver.py', 'provenance.py', 'run_seeds.py'):
            (evidence / name).write_bytes((HERE / name).read_bytes())
        seeds = list(range(1000, 1032))  # Synthetic identities, no repair photon histories.
        pv.write_json(evidence / 'seeds.json', {'T41_REPAIR32': seeds, 'T41_DEV4': list(range(4))})
        pv.write_json(evidence / 'rule.json', {'Version': 5})
        pv.write_json(evidence / 'budget.json', {'Budgets': {'Wide': 8000, 'High': 64000}})
        geometries = [{'Id': g, 'Count': 2, 'CellMm': 1, 'DistanceMm': 60,
                       'CalibrationHistoriesPerCrystal': b} for g, b in [('Wide', 8000), ('High', 64000)]]
        recipe = {'Version': 5, 'Rule': {'File': 'samples/evidence/rule.json', 'Sha256': pv.file_sha(evidence / 'rule.json')},
                  'BudgetEvidence': {'File': 'samples/evidence/budget.json', 'Sha256': pv.file_sha(evidence / 'budget.json')},
                  'CostSeeds': seeds[:8], 'Geometries': geometries, 'EnergiesKeV': [661.7],
                  'DirectReferenceCounts': [1000, 4000], 'RatesCps': [0, 100000], 'TrialsPerPosition': 2}
        pv.write_json(evidence / 'recipe.json', recipe)
        pin = {'File': 'samples/evidence/recipe.json', 'Sha256': pv.file_sha(evidence / 'recipe.json')}
        files = {'managed/probe/Gcam.EvidenceProbe.dll': pv.sha(b'fixture assembly')}
        files.update({'samples/evidence/' + n: pv.file_sha(evidence / n)
                      for n in ('studio_readout_v5_driver.py', 'provenance.py', 'run_seeds.py')})
        executor = {'Kind': 'probe', 'Files': files, 'Tools': {'DriverPython': 'fixture'}}
        source = {'Commit': 'a' * 40, 'Dirty': False, 'Scope': ['samples'], 'DiffSha256': pv.sha(b''), 'Untracked': {}}
        source['SnapshotSha256'] = pv.digest(source)
        src = {'Source': source, 'Executor': executor, 'ExecutionId': pv.digest(executor)}
        families = [{'id': 'fixture-repair-' + g['Id'], 'Phase': 'repair', 'Geometry': g['Id'], 'seeds': 'T41_REPAIR32', 'n': 32,
                     'Freeze': {'ManagedClosure': {'Gcam.EvidenceProbe.dll': files['managed/probe/Gcam.EvidenceProbe.dll']},
                                'RecipeSha256': pin['Sha256'], 'RuleSha256': recipe['Rule']['Sha256'],
                                'DriverSha256': files['samples/evidence/studio_readout_v5_driver.py']}}
                    for g in geometries]
        pv.write_json(evidence / 'manifest.json', {'Recipe': pin, 'families': families})
        pv.write_json(root / 'run-info.json', {'SchemaVersion': 1, 'Sources': {pv.digest(src): src}})

        def write(geometry, seed, failed=False):
            family = next(f for f in families if f['Geometry'] == geometry)
            budget = next(g['CalibrationHistoriesPerCrystal'] for g in geometries if g['Id'] == geometry)
            run = root / 'runs' / family['id'] / str(seed); run.mkdir(parents=True, exist_ok=True)
            diag = {'Accepted': 4, 'Single': 2, 'SingleMis': 1, 'Multi': 2,
                    'GSumXRad2': -4, 'GSumYRad2': 8, 'AssignedToCrystal': [1, 2, 2, 1], 'FullEnergyByFirst': [2] * 4}
            row = {'CalibrationSucceeded': not failed, 'Failure': 'unsupported slot (0, 17)' if failed else None,
                   'EnergyKeV': 661.7, 'CalibrationBudget': budget, 'CalibrationScored': budget * 4,
                   'Markers': 3 if failed else 4, 'CostFailure': None,
                   'Imaging': self.images() if seed in seeds[:8] else [],
                   'Diagnostics': None if failed else {'0': diag, '100000': diag}}
            if failed:
                for image in row['Imaging']:
                    for trial in image['Trials']: trial['Anger'] = None
            value = {'SchemaVersion': 5, 'Seed': seed, 'Geometry': geometry, 'Phase': 'repair',
                     'RecipeSha256': pin['Sha256'], 'RuleSha256': recipe['Rule']['Sha256'],
                     'FamilySha256': pv.digest(family), 'CostScheduled': seed in seeds[:8], 'Cells': [row]}
            pv.write_json(run / 'stdout.txt', value)
            prov = pv.seed_provenance({'Source': src, 'Recipe': pv.digest(family)}, family['id'], seed,
                                     {'stdout.txt': pv.file_sha(run / 'stdout.txt')})
            pv.write_json(run / 'done.json', {'exit': 0, 'family': family['id'], 'seed': seed, 'Provenance': prov})
            return run
        for g in geometries:
            for seed in seeds: write(g['Id'], seed)
        return root, evidence, seeds, write

    def test_main_end_to_end_real_publisher_and_diagnostic_oracle(self):
        root, evidence, seeds, write = self.fixture()
        output = root / 'published.json'
        argv = ['selector', '--runs', str(root), '--manifest', str(evidence / 'manifest.json'), '--phase', 'repair', '--out', str(output)]
        with mock.patch.object(selector, 'REPO', root), mock.patch.object(selector, 'HERE', evidence), mock.patch.object(sys, 'argv', argv):
            selector.main()
        pv = selector.pv; value = pv.read_json(output); pv.validate_sidecar(output)
        self.assertEqual(value['SchemaVersion'], 5)
        self.assertEqual(value['Provenance']['Analysis']['Arguments'], {'Phase': 'repair'})
        self.assertEqual(value['Provenance']['Analysis']['Files']['samples/evidence/studio_readout_v5_select.py'], pv.file_sha(selector.__file__))
        for geometry in ('Wide', 'High'):
            cell = value['Matrices'][geometry][0]
            self.assertTrue(cell['Offered']); self.assertEqual(cell['CalibrationSeeds'], 32)
            self.assertEqual(cell['CostTable'][0]['F'], {'Estimate': 4, 'Lower95': 4, 'Upper95': 4, 'Clusters': 8})
            d = cell['Diagnostics']['0']
            self.assertEqual(d['GXRad2']['Estimate'], -1)
            self.assertEqual(d['GYRad2']['Estimate'], 2)
            self.assertEqual(d['SingleMisId']['Estimate'], .5)
            self.assertEqual(d['MultiCrystalShare']['Estimate'], .5)
            self.assertAlmostEqual(d['WeakestMedianSensitivity']['Estimate'], 2 / 3)

    def test_failure_outside_cost_subset_refuses_and_main_keeps_message(self):
        root, evidence, seeds, write = self.fixture()
        write('High', seeds[-1], failed=True)
        output = root / 'refused.json'
        argv = ['selector', '--runs', str(root), '--manifest', str(evidence / 'manifest.json'), '--phase', 'repair', '--out', str(output)]
        with mock.patch.object(selector, 'REPO', root), mock.patch.object(selector, 'HERE', evidence), mock.patch.object(sys, 'argv', argv):
            selector.main()
        value = selector.pv.read_json(output); selector.pv.validate_sidecar(output)
        self.assertTrue(value['Matrices']['Wide'][0]['Offered'])
        c = value['Matrices']['High'][0]
        self.assertFalse(c['Offered'])
        self.assertEqual(c['Failures'], [{'Seed': seeds[-1], 'Failure': 'unsupported slot (0, 17)'}])
        self.assertEqual(c['CostTable'][0]['F']['Clusters'], 8)
        self.assertFalse(c['Diagnostics']['0']['Available'])

    def test_missing_or_corrupt_run_prevents_main_publication(self):
        for corrupt in (False, True):
            root, evidence, seeds, write = self.fixture()
            run = write('Wide', seeds[0])
            if corrupt: (run / 'stdout.txt').write_bytes(b'corrupt')
            else:
                # Alter a fixture manifest's family identity to address an absent run, without deleting anything.
                manifest = selector.pv.read_json(evidence / 'manifest.json')
                manifest['families'][0]['id'] = 'absent-family'
                selector.pv.write_json(evidence / 'manifest.json', manifest)
            output = root / 'must-not-exist.json'
            argv = ['selector', '--runs', str(root), '--manifest', str(evidence / 'manifest.json'), '--phase', 'repair', '--out', str(output)]
            with mock.patch.object(selector, 'REPO', root), mock.patch.object(selector, 'HERE', evidence), mock.patch.object(sys, 'argv', argv):
                with self.assertRaises(SystemExit): selector.main()
            self.assertFalse(output.exists())
            self.assertFalse(output.with_name(output.name + '.provenance.json').exists())

    def test_repair_scope_rejects_confirmation_phase_and_other_line(self):
        root, evidence, seeds, write = self.fixture()
        with mock.patch.object(selector, 'REPO', root), mock.patch.object(selector, 'HERE', evidence):
            with self.assertRaisesRegex(ValueError, 'scope'):
                selector.aggregate(root, evidence / 'manifest.json', 'confirm')
            recipe = selector.pv.read_json(evidence / 'recipe.json')
            recipe['EnergiesKeV'] = [1332.5]
            selector.pv.write_json(evidence / 'recipe.json', recipe)
            manifest = selector.pv.read_json(evidence / 'manifest.json')
            manifest['Recipe']['Sha256'] = selector.pv.file_sha(evidence / 'recipe.json')
            selector.pv.write_json(evidence / 'manifest.json', manifest)
            with self.assertRaisesRegex(ValueError, 'scope'):
                selector.aggregate(root, evidence / 'manifest.json', 'repair')

    def test_prepare_pins_only_complete_development_and_preserves_v4(self):
        root, evidence, seeds, write = self.fixture()
        readout = evidence / 'studio-readout'; readout.mkdir()
        protected = {}
        for name in ('studio-readout/recipe-v4.json', 'studio-readout/rule-v4.json', 'manifest-studio-readout-v4.json'):
            path = evidence / name; data = (HERE / name).read_bytes()
            path.write_bytes(data); protected[path] = data
        (readout / 'rule-v5.json').write_bytes((HERE / 'studio-readout/rule-v5.json').read_bytes())
        (root / 'samples/scenario.json').write_bytes((HERE.parent / 'scenario.json').read_bytes())
        budget_source = evidence / 'probe-v5-budget'; budget_source.mkdir()
        (budget_source / 'Program.cs').write_bytes((HERE / 'probe-v5-budget/Program.cs').read_bytes())
        managed = root / 'managed'; managed.mkdir()
        (managed / 'Gcam.EvidenceProbe.dll').write_bytes(b'synthetic managed closure')
        development = root / 'development'; development.mkdir()
        # The declaration filename shares the budget- prefix but is not a result.
        selector.pv.write_json(development / 'budget-selection.json', {'Rule': 'predeclared'})
        for g, n, level in [('Wide', 34, 8000), ('High', 44, 64000)]:
            for seed in range(4):
                if g == 'Wide' and seed == 3: continue
                selector.pv.write_json(development / f'budget-{g}-{level}-{seed}.json',
                    {'Geometry': g, 'Budget': level, 'Seed': seed, 'EnergyKeV': 661.7, 'Scored': n*n*level,
                     'Succeeded': True, 'Markers': n*n, 'Failure': None})
        argv = ['prepare', '--development', str(development), '--probe', str(managed / 'Gcam.EvidenceProbe.dll')]
        with mock.patch.object(prepare, 'REPO', root), mock.patch.object(prepare, 'HERE', evidence), mock.patch.object(sys, 'argv', argv):
            with self.assertRaisesRegex(ValueError, 'incomplete'): prepare.main()
            self.assertFalse((readout / 'recipe-v5.json').exists())
            self.assertFalse((evidence / 'manifest-studio-readout-v5.json').exists())
            selector.pv.write_json(development / 'budget-Wide-8000-3.json',
                {'Geometry': 'Wide', 'Budget': 8000, 'Seed': 3, 'EnergyKeV': 661.7, 'Scored': 34*34*8000,
                 'Succeeded': True, 'Markers': 34*34, 'Failure': None})
            prepare.main()
        recipe = selector.pv.read_json(readout / 'recipe-v5.json')
        self.assertEqual([g['CalibrationHistoriesPerCrystal'] for g in recipe['Geometries']], [8000, 64000])
        self.assertEqual(recipe['CostSeeds'], seeds[:8])
        self.assertEqual(recipe['EnergiesKeV'], [661.7])
        manifest = selector.pv.read_json(evidence / 'manifest-studio-readout-v5.json')
        self.assertEqual(manifest['Recipe']['Sha256'], selector.pv.file_sha(readout / 'recipe-v5.json'))
        self.assertEqual([f['id'] for f in manifest['families']], ['t41-repair-Wide-v5', 't41-repair-High-v5'])
        self.assertEqual([f['seeds'] for f in manifest['families']], ['T41_REPAIR32'] * 2)
        self.assertEqual(recipe['BudgetEvidence']['Sha256'], selector.pv.file_sha(readout / 'development-calibration-budget-v5.json'))
        for path, data in protected.items(): self.assertEqual(path.read_bytes(), data)


if __name__ == '__main__':
    unittest.main()
