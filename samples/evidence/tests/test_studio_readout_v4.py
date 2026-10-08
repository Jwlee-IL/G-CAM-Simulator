"""Hand-computed v4 offer and descriptive seed-cluster cost oracles."""
import copy
import math
import json
import os
import pathlib
import sys
import unittest
import tempfile
from unittest import mock

HERE = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(HERE))
import studio_readout_v4_select as selector


class StudioReadoutRuleV4Tests(unittest.TestCase):
    seeds = [41000001, 41104730, 41209459, 41314188]

    def rows(self):
        return [{'Seed': s, 'CalibrationSucceeded': True, 'Failure': None} for s in self.seeds]

    def imaging(self, x, a_scale=2):
        result = []
        for target in (1000, 4000):
            for rate in (0, 100000):
                for position in range(9):
                    result.append({'TargetDirectCounts': target, 'RateCps': rate, 'Position': position,
                        'Trials': [{'Trial': t, 'Direct': {'ExDeg': x, 'EyDeg': 0, 'Remote': x > 1, 'Count': target},
                                    'Anger': {'ExDeg': a_scale * x, 'EyDeg': 0, 'Remote': x > 1, 'Count': target / 2}}
                                   for t in range(2)]})
        return result

    def test_offered_is_calibration_only_even_for_large_cost(self):
        rows = self.rows()
        for r in rows:
            r['Imaging'] = self.imaging(1000000, 1000)
        self.assertEqual(selector.offer(rows, self.seeds)['Label'], 'experimental')
        self.assertTrue(selector.offer(rows, self.seeds)['Offered'])

    def test_refused_cell_and_failure_message_carried_through(self):
        rows = self.rows()
        rows[2].update(CalibrationSucceeded=False, Failure='unsupported slot (4, 7)')
        result = selector.offer(rows, self.seeds)
        self.assertFalse(result['Offered'])
        self.assertEqual(result['Failures'], [{'Seed': self.seeds[2], 'Failure': 'unsupported slot (4, 7)'}])

    def test_missing_calibration_run_fails_closed(self):
        with self.assertRaisesRegex(ValueError, 'missing'):
            selector.offer(self.rows()[:-1], self.seeds)

    def test_duplicate_or_corrupt_status_fails_closed(self):
        rows = self.rows(); rows[-1]['Seed'] = rows[0]['Seed']
        with self.assertRaises(ValueError):
            selector.offer(rows, self.seeds)
        rows = self.rows(); rows[0]['CalibrationSucceeded'] = 1
        with self.assertRaises(ValueError):
            selector.offer(rows, self.seeds)

    def test_cost_table_intervals_have_hand_computed_oracle(self):
        rows = [{'Imaging': self.imaging(x)} for x in (1, 1, 1, 3)]
        result = selector.costs(rows, 1000, 0, trials=2)
        direct = result['Paths']['Direct']
        # MSE clusters 1,1,1,9: mean 3, sample SD 4, SE 2; t(3,.975)=3.182446305284263.
        self.assertAlmostEqual(direct['RmsDeg']['Estimate'], math.sqrt(3))
        self.assertEqual(direct['RmsDeg']['Lower95'], 0)
        self.assertAlmostEqual(direct['RmsDeg']['Upper95'], math.sqrt(3 + 2 * 3.182446305284263))
        self.assertEqual(direct['BiasDeg'], direct['RmsDeg'])
        self.assertEqual(direct['RemoteProbability']['Estimate'], .25)
        self.assertEqual(direct['RemoteProbability']['Lower95'], 0)
        self.assertEqual(direct['RemoteProbability']['Upper95'], 1)
        self.assertEqual(result['F'], {'Estimate': 4, 'Lower95': 4, 'Upper95': 4, 'Clusters': 4})
        self.assertEqual(result['Paths']['Anger']['AcceptedCounts']['Estimate'], 500)

    def test_identity_control_has_unit_f(self):
        result = selector.costs([{'Imaging': self.imaging(x, 1)} for x in (1, 1, 1, 3)], 4000, 100000, trials=2)
        self.assertEqual(result['F']['Estimate'], 1)
        self.assertEqual(result['F']['Lower95'], 1)
        self.assertEqual(result['F']['Upper95'], 1)

    def test_paired_log_ratio_interval_has_hand_computed_oracle(self):
        rows = [{'Imaging': self.imaging(1, x)} for x in (1, 1, 1, 3)]
        f = selector.costs(rows, 1000, 0, 2)['F']
        # D MSE=1, A MSE=(1,1,1,9); F=3; influence=(-2/3,-2/3,-2/3,2), SE=2/3.
        self.assertEqual(f['Estimate'], 3)
        half = 3.182446305284263 * 2 / 3
        self.assertAlmostEqual(f['Lower95'], 3 * math.exp(-half))
        self.assertAlmostEqual(f['Upper95'], 3 * math.exp(half))

    def test_crystal_diagnostics_have_signed_g_and_isolated_sensitivity(self):
        def diag(assigned, full, gx):
            return {'Accepted': 4, 'GSumXRad2': gx, 'GSumYRad2': 0, 'Single': 2, 'SingleMis': 1,
                    'Multi': 2, 'AssignedToCrystal': assigned, 'FullEnergyByFirst': full}
        rows = [{'Diagnostics': {'0': diag([1, 2], [2, 2], -4), '100000': diag([0, 0], [0, 0], 8)}} for _ in range(4)]
        result = selector.diagnostics(rows, 100000)
        self.assertEqual(result['GXRad2']['Estimate'], 2)
        self.assertEqual(result['SingleMisId']['Estimate'], .5)
        self.assertEqual(result['MultiCrystalShare']['Estimate'], .5)
        self.assertAlmostEqual(result['WeakestMedianSensitivity']['Estimate'], 2 / 3)
        self.assertEqual(selector.diagnostics(rows)['GXRad2']['Estimate'], -1)

    def test_bias_is_per_position_and_does_not_cancel(self):
        rows = [{'Imaging': self.imaging(1)} for _ in range(4)]
        for row in rows:
            for image in row['Imaging']:
                for trial in image['Trials']:
                    trial['Direct']['ExDeg'] = -1 if image['Position'] % 2 else 1
        self.assertEqual(selector.costs(rows, 1000, 0, 2)['Paths']['Direct']['BiasDeg']['Estimate'], 1)

    def test_failed_calibration_does_not_publish_survivor_only_cost(self):
        rows = [{'Imaging': self.imaging(1)} for _ in range(4)]
        for image in rows[0]['Imaging']:
            for trial in image['Trials']:
                trial['Anger'] = None
        result = selector.costs(rows, 1000, 0, 2)
        self.assertTrue(result['Paths']['Direct']['Available'])
        self.assertEqual(result['Paths']['Anger'], {'Available': False, 'MissingClusters': 1})
        self.assertIsNone(result['F'])

    def test_missing_imaging_trial_fails_closed(self):
        rows = [{'Imaging': self.imaging(1)} for _ in range(4)]
        rows[0]['Imaging'][0]['Trials'].pop()
        with self.assertRaisesRegex(ValueError, 'missing imaging trial'):
            selector.costs(rows, 1000, 0, 2)

    def test_nonfinite_cost_fails_closed(self):
        rows = [{'Imaging': self.imaging(1)} for _ in range(4)]
        rows[0]['Imaging'][0]['Trials'][0]['Direct']['ExDeg'] = float('nan')
        with self.assertRaisesRegex(ValueError, 'corrupt'):
            selector.costs(rows, 1000, 0, 2)

    def test_aggregate_rejects_missing_provenance_before_publication(self):
        # Genuine production boundary: an absent done.json is refused by provenance, before any output write.
        recipe = {'Version': 4, 'Rule': {'File': 'rule.json', 'Sha256': 'b' * 64}, 'CostSeeds': list(range(8)),
                  'High662BudgetEvidence': {'File': 'budget.json', 'Sha256': 'c' * 64},
                  'Geometries': [{'Id': 'Fixture'}]}
        manifest = {'Recipe': {'File': 'recipe.json', 'Sha256': 'a' * 64}, 'families': [
            {'Phase': 'dev', 'Geometry': 'Fixture', 'id': 'missing-v4-run', 'seeds': 'T41_DEV4', 'n': 4}]}
        read_json = selector.pv.read_json
        replies = iter([manifest, recipe, {'Version': 4}, {'T41_DEV4': self.seeds, 'T41_CONFIRM32': list(range(32))}])
        def read(path):
            return read_json(path) if pathlib.Path(path).name == 'done.json' else next(replies)
        with mock.patch.object(selector.pv, 'read_json', side_effect=read), \
             mock.patch.object(selector.pv, 'file_sha', side_effect=['a' * 64, 'b' * 64, 'c' * 64]):
            with self.assertRaises(SystemExit):
                selector.aggregate(HERE / 'absent-v4-fixture', HERE / 'not-read.json', 'dev')

    def test_complete_provenance_ensemble_and_failure_outside_cost_subset(self):
        pv = selector.pv
        parent = pathlib.Path(os.environ.get('GCAM_PROVENANCE_TEST_ROOT',
                              str(pathlib.Path(tempfile.gettempdir()) / 'gcam-todo41/v4-test-fixtures')))
        parent.mkdir(parents=True, exist_ok=True)
        root = pathlib.Path(tempfile.mkdtemp(prefix='v4-ensemble-', dir=parent))
        evidence = root / 'samples/evidence'; evidence.mkdir(parents=True)
        for name in ('studio_readout_v4_driver.py', 'provenance.py', 'run_seeds.py'):
            (evidence / name).write_bytes((HERE / name).read_bytes())
        # Synthetic seed IDs, no transport and no use of confirmation photon streams.
        seeds = list(range(1000, 1032))
        pv.write_json(evidence / 'seeds.json', {'T41_CONFIRM32': seeds, 'T41_DEV4': self.seeds})
        pv.write_json(evidence / 'rule.json', {'Version': 4})
        pv.write_json(evidence / 'budget.json', {'SelectedHistoriesPerCrystal': 2000})
        recipe = {'Version': 4, 'Rule': {'File': 'samples/evidence/rule.json', 'Sha256': pv.file_sha(evidence / 'rule.json')},
            'High662BudgetEvidence': {'File': 'samples/evidence/budget.json', 'Sha256': pv.file_sha(evidence / 'budget.json')},
            'CostSeeds': seeds[:8], 'Geometries': [{'Id': 'Fixture', 'Count': 2, 'CellMm': 1, 'DistanceMm': 60}],
            'EnergiesKeV': [661.7], 'CalibrationHistoriesPerCrystal': 2000, 'DirectReferenceCounts': [1000, 4000],
            'RatesCps': [0, 100000], 'TrialsPerPosition': 2}
        pv.write_json(evidence / 'recipe.json', recipe)
        pin = {'File': 'samples/evidence/recipe.json', 'Sha256': pv.file_sha(evidence / 'recipe.json')}
        files = {'managed/probe/Gcam.EvidenceProbe.dll': pv.sha(b'fixture assembly')}
        files.update({'samples/evidence/' + name: pv.file_sha(evidence / name)
                      for name in ('studio_readout_v4_driver.py', 'provenance.py', 'run_seeds.py')})
        executor = {'Kind': 'probe', 'Files': files, 'Tools': {'DriverPython': 'fixture'}}
        source = {'Commit': 'a' * 40, 'Dirty': False, 'Scope': ['samples'], 'DiffSha256': pv.sha(b''), 'Untracked': {}}
        source['SnapshotSha256'] = pv.digest(source)
        src = {'Source': source, 'Executor': executor, 'ExecutionId': pv.digest(executor)}
        family = {'id': 'fixture-confirm-v4', 'Phase': 'confirm', 'Geometry': 'Fixture', 'seeds': 'T41_CONFIRM32', 'n': 32,
            'Freeze': {'ManagedClosure': {'Gcam.EvidenceProbe.dll': files['managed/probe/Gcam.EvidenceProbe.dll']},
                       'RecipeSha256': pin['Sha256'], 'RuleSha256': recipe['Rule']['Sha256'],
                       'DriverSha256': files['samples/evidence/studio_readout_v4_driver.py']}}
        manifest = {'Recipe': pin, 'families': [family]}
        pv.write_json(evidence / 'manifest.json', manifest)
        pv.write_json(root / 'run-info.json', {'SchemaVersion': 1, 'Sources': {pv.digest(src): src}})
        prepared = {'Source': src, 'Recipe': pv.digest(family)}
        def write(seed, failed=False, empty_anger=False):
            run = root / 'runs' / family['id'] / str(seed); run.mkdir(parents=True, exist_ok=True)
            images = self.imaging(.5) if seed in seeds[:8] else []
            for image in images:
                image.update(MeanEmittedHistories=10000, ExposurePilotDirect=12000)
                for trial in image['Trials']:
                    if empty_anger:
                        trial['Anger'] = None
                    else:
                        trial['Anger']['Remote'] = True
            diag = {'Accepted': 4, 'Single': 2, 'SingleMis': 1, 'Multi': 2, 'GSumXRad2': 0, 'GSumYRad2': 0,
                    'AssignedToCrystal': [1] * 4, 'FullEnergyByFirst': [2] * 4}
            row = {'CalibrationSucceeded': not failed, 'Failure': 'unsupported slot (1, 1)' if failed else None,
                   'EnergyKeV': 661.7, 'CalibrationBudget': 2000, 'CalibrationScored': 8000, 'Markers': 0 if failed else 4,
                   'CostFailure': 'empty Anger correction bin' if empty_anger else None, 'FloodAnger': [0,1,1,1] if empty_anger else [1]*4,
                   'Imaging': images, 'Diagnostics': {'0': diag, '100000': diag} if not failed else None}
            value = {'SchemaVersion': 4, 'Seed': seed, 'Geometry': 'Fixture', 'Phase': 'confirm',
                'RecipeSha256': pin['Sha256'], 'RuleSha256': recipe['Rule']['Sha256'], 'FamilySha256': pv.digest(family),
                'CostScheduled': seed in seeds[:8], 'Cells': [row]}
            pv.write_json(run / 'stdout.txt', value)
            prov = pv.seed_provenance(prepared, family['id'], seed, {'stdout.txt': pv.file_sha(run / 'stdout.txt')})
            pv.write_json(run / 'done.json', {'exit': 0, 'family': family['id'], 'seed': seed, 'Provenance': prov})
            return run
        for seed in seeds:
            write(seed)
        with mock.patch.object(selector, 'REPO', root), mock.patch.object(selector, 'HERE', evidence):
            result = selector.aggregate(root, evidence / 'manifest.json', 'confirm')
            cell = result['Matrices']['Fixture'][0]
            self.assertTrue(cell['Offered'])
            self.assertEqual(cell['CalibrationSeeds'], 32)
            self.assertEqual(cell['CostTable'][0]['F']['Estimate'], 4)
            self.assertEqual(cell['CostTable'][0]['F']['Clusters'], 8)
            write(seeds[-1], failed=True)
            refused = selector.aggregate(root, evidence / 'manifest.json', 'confirm')['Matrices']['Fixture'][0]
            self.assertFalse(refused['Offered'])
            self.assertEqual(refused['Failures'], [{'Seed': seeds[-1], 'Failure': 'unsupported slot (1, 1)'}])
            write(seeds[-1])
            write(seeds[0], empty_anger=True)
            empty = selector.aggregate(root, evidence / 'manifest.json', 'confirm')['Matrices']['Fixture'][0]
            self.assertTrue(empty['Offered'])
            self.assertTrue(empty['CostTable'][0]['Paths']['Direct']['Available'])
            self.assertFalse(empty['CostTable'][0]['Paths']['Anger']['Available'])
            self.assertEqual(empty['CostUnavailable'], [{'Seed': seeds[0], 'Failure': 'empty Anger correction bin'}])
            run = root / 'runs' / family['id'] / str(seeds[0])
            (run / 'stdout.txt').write_bytes(b'corrupt')
            with self.assertRaises(SystemExit):
                selector.aggregate(root, evidence / 'manifest.json', 'confirm')


if __name__ == '__main__':
    unittest.main()
