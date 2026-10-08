"""TODO-41 qualification denominators, cluster bounds and refusal are structural, never sampled test tolerances."""
import copy
import math
import pathlib
import sys
import unittest

HERE = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(HERE))
import studio_readout_select as selector


class StudioReadoutSelectorTests(unittest.TestCase):
    geometry = {'Count': 4, 'PitchMm': 1.0, 'DistanceMm': 60.0}
    recipe = {'ValidationHistoriesPerCrystal': 100, 'RateHits': 1600, 'CalibrationHistoriesPerCrystal': 2000}

    def row(self):
        return {'Scored': [100] * 16, 'Triggered': [95] * 16, 'Windowed': [92] * 16, 'Assigned': [90] * 16,
                'RateCps': 1000, 'CalibrationScored': 32000, 'CalibrationTriggered': 30000,
                'CalibrationWindowed': 10000, 'CalibrationSucceeded': True, 'Markers': 16,
                'AssignedTotal': 1440, 'Displacement': {'0,0': 1440},
                'SumSquaredAngleX': 0.0, 'SumSquaredAngleY': 0.0}

    def test_exact_assignment_qualifies_both_axes(self):
        row = self.row()
        selector.validate_row(row, self.geometry, self.recipe)
        result = selector.qualification([copy.deepcopy(row) for _ in range(32)], self.geometry, .8)
        self.assertTrue(result['Qualified'])
        self.assertEqual(result['WeakestMedianRatio'], 1.0)
        self.assertEqual(result['Upper95RmsXRad'], 0)

    def test_calibration_failure_is_retained_and_refuses(self):
        rows = [self.row() for _ in range(32)]
        rows[7]['CalibrationSucceeded'] = False
        result = selector.qualification(rows, self.geometry, .8)
        self.assertFalse(result['Qualified'])
        self.assertEqual(result['CalibrationFailures'], [7])

    def test_empty_assignment_does_not_drop_a_cluster(self):
        rows = [self.row() for _ in range(32)]
        rows[0]['AssignedTotal'] = 0
        self.assertIsNone(selector.qualification(rows, self.geometry, .8)['Upper95RmsXRad'])

    def test_weakest_crystal_uses_scored_denominator(self):
        rows = [self.row() for _ in range(32)]
        for row in rows:
            row['Assigned'][0] = 50
        result = selector.qualification(rows, self.geometry, .8)
        self.assertFalse(result['Qualified'])
        self.assertEqual(result['WeakestAcceptance'], .5)
        self.assertEqual(result['MedianAcceptance'], .9)

    def test_angular_axis_bound_refuses_large_displacement(self):
        row = self.row()
        row['SumSquaredAngleX'] = 1440 * math.atan(1 / 60) ** 2
        row['Displacement'] = {'1,0': 1440}
        selector.validate_row(row, self.geometry, self.recipe)
        self.assertFalse(selector.qualification([copy.deepcopy(row) for _ in range(32)], self.geometry, .8)['Qualified'])

    def test_invalid_denominators_and_sufficient_statistics_refuse(self):
        row = self.row(); row['Assigned'][0] = 101
        with self.assertRaises(ValueError):
            selector.validate_row(row, self.geometry, self.recipe)
        row = self.row(); row['SumSquaredAngleX'] = 1e-8
        with self.assertRaises(ValueError):
            selector.validate_row(row, self.geometry, self.recipe)

    def test_cluster_count_and_nonfinite_values_refuse(self):
        for values in ([0] * 3, [0, 0, 0, float('nan')], [0, 0, 0, -1]):
            with self.assertRaises(ValueError):
                selector.upper(values)

    def test_positive_between_seed_variance_increases_the_upper_bound(self):
        self.assertGreater(selector.upper([0, 0, 0, 4]), 1)


if __name__ == '__main__':
    unittest.main()
