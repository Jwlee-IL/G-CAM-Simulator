"""Studio readout offer / cost record (TODO-41, SD-A13): the committed reduced record regenerates byte for byte from
its embedded projections, and every link of the hash chain fails closed when tampered with."""
import copy
import json
import os
import pathlib
import sys
import tempfile
import unittest
from unittest import mock

HERE = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(HERE))
import studio_readout_offer_record as record


def scratch():
    parent = pathlib.Path(os.environ.get('GCAM_PROVENANCE_TEST_ROOT',
                                         str(pathlib.Path(tempfile.gettempdir()) / 'gcam-todo41/offer-record-tests')))
    parent.mkdir(parents=True, exist_ok=True)
    return pathlib.Path(tempfile.mkdtemp(prefix='offer-record-', dir=parent))


class StudioReadoutOfferRecordTests(unittest.TestCase):
    def setUp(self):
        self.shipped = record.OUTPUT.read_bytes()
        self.value = json.loads(self.shipped)

    def check_copy(self, value, pin=None):
        """Run the reduced check against a modified copy of the record (and optionally another compiled pin)."""
        root = scratch()
        path = root / 'offer-cost-v1.json'
        path.write_bytes(record.encode(value) if isinstance(value, dict) else value)
        patches = [mock.patch.object(record, 'OUTPUT', path)]
        if pin is not None:
            patches.append(mock.patch.object(record, 'compiled_pin', lambda: pin))
        for p in patches:
            p.start()
        try:
            return record.check()
        finally:
            for p in patches:
                p.stop()

    def test_shipped_record_passes_the_reduced_check_and_matches_the_compiled_pin(self):
        mode, digest = record.check()
        self.assertTrue(mode.startswith('reduced'))
        self.assertEqual(digest, record.compiled_pin())
        self.assertLess(len(self.shipped), 1_000_000)  # the size rule: well under 1 MB added

    def test_published_outcome_and_reduction(self):
        cells = self.value['Cells']
        self.assertEqual(len(cells), 12)
        self.assertEqual(sum(c['Offered'] for c in cells), 8)
        self.assertTrue(all(not c['Offered'] for c in cells if c['EnergyKeV'] == 122.06))
        repaired = {(c['Geometry'], c['EnergyKeV']): c for c in cells if c['SourceRecord'] == 'repair-v5.json'}
        self.assertEqual(set(repaired), {('Wide', 661.7), ('High', 661.7)})
        self.assertEqual((repaired[('Wide', 661.7)]['Budget'], repaired[('High', 661.7)]['Budget']), (8000, 64000))
        self.assertEqual(repaired[('High', 661.7)]['Superseded']['Budget'], 32000)
        def keys(node):
            if isinstance(node, dict):
                for k, v in node.items():
                    yield k
                    yield from keys(v)
            elif isinstance(node, list):
                for v in node:
                    yield from keys(v)
        names = set(keys(self.value))
        self.assertNotIn('SensitivityMapsPerSeed', names)
        self.assertNotIn('Provenance', names)
        self.assertEqual({p['Name']: p['Sha256'] for p in self.value['Parents']},
                         {k: v['Sha256'] for k, v in record.PARENTS.items()})

    def test_rebuild_is_deterministic(self):
        self.assertEqual(record.encode(record.build(self.value['Parents'])), self.shipped)

    def test_sensitivity_mean_has_a_hand_computed_oracle(self):
        maps = [[1.0, 3.0], [3.0, 1.0]] * 16
        cell = {'Offered': True, 'Diagnostics': {'0': {'SensitivityMapsPerSeed': maps}}}
        projected = record.project({'Matrices': {'Fixture': [cell]}})['Matrices']['Fixture'][0]
        self.assertEqual(projected['SensitivitySummary']['Values'], [2.0, 2.0])
        self.assertEqual(projected['SensitivitySummary']['Clusters'], 32)
        self.assertIn('never flood-correction weights', projected['SensitivitySummary']['Meaning'])
        self.assertNotIn('SensitivityMapsPerSeed', projected['Diagnostics']['0'])
        self.assertNotIn('SensitivitySummary', cell)

    def test_each_hash_chain_link_fails_closed(self):
        # A changed value inside a projection breaks its recorded projection digest.
        value = copy.deepcopy(self.value)
        value['Parents'][0]['Projection']['Matrices']['Baseline'][1]['CostTable'][0]['F']['Estimate'] = 1.0
        with self.assertRaisesRegex(record.RecordError, 'projection digest mismatch'):
            self.check_copy(value)
        # A changed derived cell no longer equals the overlay of the projections.
        value = copy.deepcopy(self.value)
        value['Cells'][1]['Budget'] = 1000
        with self.assertRaisesRegex(record.RecordError, 'byte-for-byte regeneration'):
            self.check_copy(value)
        # A changed full-parent hash is not one of the pinned parents.
        value = copy.deepcopy(self.value)
        value['Parents'][1]['Sha256'] = '0' * 64
        with self.assertRaisesRegex(record.RecordError, 'parent pin mismatch'):
            self.check_copy(value)
        # A canonical record whose compiled pin was not updated is refused.
        with self.assertRaisesRegex(record.RecordError, 'compiled pin'):
            self.check_copy(self.shipped, pin='f' * 64)
        # Whitespace-only edits are not canonical.
        with self.assertRaisesRegex(record.RecordError, 'byte-for-byte regeneration'):
            self.check_copy(self.shipped + b'\n')

    def test_overlay_scope_and_population_are_enforced(self):
        entries = copy.deepcopy(self.value['Parents'])
        repair = next(e for e in entries if e['Name'] == 'repair-v5.json')
        repair['Projection']['Matrices']['Sharp'] = repair['Projection']['Matrices']['Wide']
        repair['ProjectionSha256'] = record.pv.digest(repair['Projection'])
        with self.assertRaisesRegex(record.RecordError, 'repair geometry population'):
            record.build(entries)
        entries = copy.deepcopy(self.value['Parents'])
        base = next(e for e in entries if e['Name'] == 'confirmation-v4.json')
        base['Projection']['Matrices']['Baseline'][1]['Failures'] = [{'Seed': 1, 'Failure': 'x'}]
        base['ProjectionSha256'] = record.pv.digest(base['Projection'])
        with self.assertRaisesRegex(record.RecordError, 'corrupt calibration population'):
            record.build(entries)


if __name__ == '__main__':
    unittest.main()
