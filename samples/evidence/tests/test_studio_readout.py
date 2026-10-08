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


class StudioReadoutRuleV2Tests(unittest.TestCase):
    """Rule v2: acceptance from isolated rows, non-gating throughput, single-crystal subset, three-level residual bound."""
    geometry = {'Count': 4, 'PitchMm': 1.0, 'DistanceMm': 60.0}
    rule = {'ContractionLimit': 0.5, 'PhotonContractionAssumed': 0.5, 'NumericalAllocationFraction': 0.1,
            'GateEvents': 'AllDeposits'}
    recipe = {'EnergiesKeV': [661.7]}

    def row(self, rate=1000, assigned=90):
        row = StudioReadoutSelectorTests.row(self)
        row['RateCps'] = rate
        row['Assigned'] = [assigned] * 16
        row['AssignedTotal'] = 16 * assigned
        row['Displacement'] = {'0,0': 16 * assigned}
        row['Single'] = {'Scored': [60] * 16, 'Windowed': [55] * 16, 'Assigned': [50] * 16, 'Correct': [50] * 16,
                         'AssignedTotal': 800, 'SumSquaredAngleX': 0.0, 'SumSquaredAngleY': 0.0}
        return row

    def test_residual_is_a_geometric_series_and_refuses_slow_contraction(self):
        self.assertEqual(selector.residual(0, 0, .5), 0.0)
        self.assertIsNone(selector.residual(0, 1e-9, .5))
        self.assertAlmostEqual(selector.residual(4.0, 2.0, .5), 8.0)
        self.assertAlmostEqual(selector.residual(4.0, 1.0, .5), 4.0 / .75)
        self.assertIsNone(selector.residual(4.0, 2.000001, .5))

    def test_acceptance_comes_from_isolated_rows_and_throughput_does_not_gate(self):
        rows = [self.row(assigned=10) for _ in range(4)]  # heavy rate loss, uniform over crystals
        isolated = [self.row(rate=0) for _ in range(4)]
        for row in rows:
            row['Assigned'][0] = 1; row['AssignedTotal'] = sum(row['Assigned']); row['Displacement'] = {'0,0': row['AssignedTotal']}
        result = selector.qualification_v2(rows, isolated, self.geometry, .8, True)
        self.assertTrue(result['Qualified'])
        self.assertEqual(result['WeakestMedianRatio'], 1.0)
        self.assertLess(result['ThroughputRatio'], .2)
        self.assertEqual(result['AcceptanceSource'], 'isolated held-out rows')

    def test_weak_isolated_crystal_or_unbounded_line_refuses(self):
        rows = [self.row() for _ in range(4)]
        isolated = [self.row(rate=0) for _ in range(4)]
        self.assertFalse(selector.qualification_v2(rows, isolated, self.geometry, .8, False)['Qualified'])
        self.assertFalse(selector.qualification_v2(rows, isolated, self.geometry, .8, None)['Qualified'])
        for row in isolated:
            row['Assigned'][3] = 60; row['AssignedTotal'] = sum(row['Assigned'])
        result = selector.qualification_v2(rows, isolated, self.geometry, .8, True)
        self.assertFalse(result['Qualified'])
        self.assertAlmostEqual(result['WeakestMedianRatio'], 60 / 90)

    def test_single_crystal_view_gates_on_the_subset(self):
        rows = [self.row() for _ in range(4)]
        for row in rows:  # all-deposit displacement fails, single-crystal subset exact
            row['SumSquaredAngleX'] = row['AssignedTotal'] * math.atan(1 / 60) ** 2
        isolated = [self.row(rate=0) for _ in range(4)]
        self.assertFalse(selector.qualification_v2(rows, isolated, self.geometry, .8, True, 'AllDeposits')['Qualified'])
        single = selector.qualification_v2(rows, isolated, self.geometry, .8, True, 'SingleCrystal')
        self.assertTrue(single['Qualified'])
        self.assertEqual(single['GateEvents'], 'SingleCrystal')
        with self.assertRaises(ValueError):
            selector.qualification_v2(rows, isolated, self.geometry, .8, True, 'Other')

    def test_single_subset_must_nest(self):
        row = self.row()
        selector.validate_single(row, self.geometry)
        row['Single']['Assigned'][0] = 56
        with self.assertRaises(ValueError):
            selector.validate_single(row, self.geometry)
        row = self.row(); row['Single']['Scored'][0] = 101
        with self.assertRaises(ValueError):
            selector.validate_single(row, self.geometry)

    def output(self, d1, d2, photons=1e-5, doi=(2e-5, 1e-5), succeeded=True):
        level = lambda rms: {'Succeeded': succeeded, 'RmsX': rms, 'RmsY': rms, 'SingleRmsX': rms, 'SingleRmsY': rms}
        return {'Numerical': {
            'Photons': {'Supported': True, 'MaxAngleX': photons, 'MaxAngleY': photons},
            'Doi': {'Differences': [{'Supported': True, 'MaxAngleX': doi[0], 'MaxAngleY': doi[0]},
                                    {'Supported': True, 'MaxAngleX': doi[1], 'MaxAngleY': doi[1]}]},
            'Calibration-0': {'Levels': [level(1e-3 + d1 + d2), level(1e-3 + d2), level(1e-3)]}}}

    def test_numerical_bound_sums_residuals_per_line(self):
        allocation = 1 / 60 / math.sqrt(12)
        data = [self.output(1e-4, 4e-5) for _ in range(4)]
        result = selector.numerical_v2(data, self.geometry, self.recipe, self.rule)
        line = result['Lines']['661.7']
        expected = 1e-5 / .5 + 2e-5 / .5 + 1e-4 / (1 - .4)
        self.assertAlmostEqual(line['TotalX'], expected)
        self.assertEqual(line['Bounded'], expected <= .1 * allocation)
        slow = selector.numerical_v2([self.output(1e-4, 6e-5) for _ in range(4)], self.geometry, self.recipe, self.rule)
        self.assertFalse(slow['Lines']['661.7']['Bounded'])
        failed = selector.numerical_v2([self.output(1e-4, 4e-5, succeeded=False)] + data[1:], self.geometry, self.recipe, self.rule)
        self.assertFalse(failed['Lines']['661.7']['Bounded'])
        self.assertIsNone(failed['Lines']['661.7']['TotalX'])


class StudioReadoutRuleV3Tests(unittest.TestCase):
    """Rule v3: paired first-interaction statistic, escape-cancelling acceptance, replicate-floor bounds, fail-closed."""
    geometry = {'Count': 4, 'PitchMm': 1.0, 'DistanceMm': 60.0}
    recipe = {'ValidationHistoriesPerCrystal': 100, 'RateHits': 1600, 'CalibrationHistoriesPerCrystal': 2000}
    allocation = 1 / 60 / math.sqrt(12)

    def row(self, anger, direct, full=None, accepted_full=None):
        """Accepted events given as (dx, dy) displacements of the Anger crystal c and the direct crystal d from f."""
        a = lambda d: math.atan(d / 60)
        matrix = lambda events: {k: sum(1 for e in events if f'{e[0]},{e[1]}' == k) for k in {f'{e[0]},{e[1]}' for e in events}}
        n = len(anger)
        accepted = [n // 16 + (1 if i < n % 16 else 0) for i in range(16)]
        full = full or [90] * 16
        return {'RateCps': 0, 'Inputs': 1600, 'Scored': [100] * 16, 'FullEnergy': full, 'Triggered': [95] * 16,
                'Windowed': [95] * 16, 'Accepted': accepted, 'AcceptedFullEnergy': accepted_full or [min(x, f) for x, f in zip(accepted, full)],
                'AssignedToCrystal': list(accepted),
                'AcceptedTotal': n, 'CalibrationScored': 32000, 'CalibrationSucceeded': True, 'Markers': 16,
                'SumSquaredAngerX': sum(a(e[0]) ** 2 for e in anger), 'SumSquaredAngerY': sum(a(e[1]) ** 2 for e in anger),
                'SumSquaredDirectX': sum(a(e[0]) ** 2 for e in direct), 'SumSquaredDirectY': sum(a(e[1]) ** 2 for e in direct),
                'SumAngerX': sum(a(e[0]) for e in anger), 'SumAngerY': sum(a(e[1]) for e in anger),
                'SumDirectX': sum(a(e[0]) for e in direct), 'SumDirectY': sum(a(e[1]) for e in direct),
                'DisplacementAnger': matrix(anger), 'DisplacementDirect': matrix(direct)}

    def test_paired_statistic_negative_when_anger_beats_direct(self):
        anger = [(0, 0)] * 30 + [(1, 0)] * 2
        direct = [(0, 0)] * 30 + [(3, 0)] * 2
        row = self.row(anger, direct)
        selector.validate_row_v3(row, self.geometry, self.recipe)
        expected = 2 * (math.atan(1 / 60) ** 2 - math.atan(3 / 60) ** 2) / 32   # hand oracle, rad^2
        self.assertAlmostEqual(selector.gate_statistic(row, 'X'), expected, places=15)
        self.assertLess(expected, 0)
        result = selector.qualification_v3([copy.deepcopy(row) for _ in range(4)], [row] * 4, self.geometry, .8, True)
        self.assertAlmostEqual(result['Upper95GX'], expected, places=15)
        self.assertTrue(result['Qualified'])

    def test_paired_statistic_fails_when_anger_loses_beyond_the_allocation(self):
        anger = [(0, 0)] * 24 + [(2, 0)] * 8
        direct = [(0, 0)] * 32
        row = self.row(anger, direct)
        expected = 8 * math.atan(2 / 60) ** 2 / 32
        self.assertAlmostEqual(selector.gate_statistic(row, 'X'), expected, places=15)
        self.assertGreater(expected, self.allocation ** 2)
        result = selector.qualification_v3([row] * 4, [row] * 4, self.geometry, .8, True)
        self.assertFalse(result['SpatialPass'])
        self.assertFalse(result['Qualified'])

    def sensitivity_row(self, full, assigned):
        """Isolated row with 100 windowed events per first-interaction crystal; `assigned` = events per LUT crystal."""
        row = self.row([(0, 0)] * sum(assigned), [(0, 0)] * sum(assigned), full, [min(90, f) for f in full])
        row['Triggered'] = [100] * 16; row['Windowed'] = [100] * 16   # Accepted keeps row()'s split of the total
        row['AssignedToCrystal'] = assigned
        row['AcceptedFullEnergy'] = [min(a, f) for a, f in zip(row['Accepted'], full)]
        return row

    def test_sensitivity_catches_edge_compression(self):
        # The LUT compresses edge crystal 0: half of its 90 events land in neighbour 1. Ratios 45/90 = 0.5, 135/90 = 1.5,
        # fourteen at 90/90 = 1, so the median is 1 and weakest / median = 0.5 exactly.
        row = self.sensitivity_row([90] * 16, [45, 135] + [90] * 14)
        selector.validate_row_v3(row, self.geometry, self.recipe)
        weakest, median, ratio = selector.sensitivity_v3([row], self.geometry)
        self.assertAlmostEqual(weakest, 0.5); self.assertAlmostEqual(median, 1.0); self.assertAlmostEqual(ratio, 0.5)
        result = selector.qualification_v3([row] * 4, [row] * 4, self.geometry, .8, True)
        self.assertFalse(result['Qualified'])
        self.assertAlmostEqual(result['WeakestMedianRatio'], 0.5)

    def test_sensitivity_cancels_the_crystal_escape(self):
        # Edge crystal 0 has half the full-energy histories (45, the rest escaped) and the readout assigns 45 events to it:
        # 45/45 = 1, the others 90/90 = 1, weakest / median = 1.
        row = self.sensitivity_row([45] + [90] * 15, [45] + [90] * 15)
        selector.validate_row_v3(row, self.geometry, self.recipe)
        self.assertEqual(selector.sensitivity_v3([row], self.geometry), (1.0, 1.0, 1.0))
        self.assertTrue(selector.qualification_v3([row] * 4, [row] * 4, self.geometry, .8, True)['Qualified'])

    def test_full_energy_acceptance_stays_a_diagnostic(self):
        full = [45] + [90] * 15
        row = self.row([(0, 0)] * 1600, [(0, 0)] * 1600, full, [40] + [80] * 15)
        row['Accepted'] = [100] * 16; row['Triggered'] = [100] * 16; row['Windowed'] = [100] * 16
        row['AssignedToCrystal'] = [100] * 16
        selector.validate_row_v3(row, self.geometry, self.recipe)
        self.assertAlmostEqual(selector.acceptance_v3([row], self.geometry)[2], 1.0)
        result = selector.qualification_v3([row] * 4, [row] * 4, self.geometry, .8, True)
        self.assertAlmostEqual(result['FullEnergyAcceptance']['WeakestMedianRatio'], 1.0)
        self.assertAlmostEqual(result['AcceptedNotFullEnergyFraction'], (1600 - 40 - 15 * 80) / 1600)

    def test_fail_closed(self):
        row = self.row([(0, 0)] * 32, [(0, 0)] * 32)
        empty = self.row([], [])
        result = selector.qualification_v3([row, row, row, empty], [row] * 4, self.geometry, .8, True)
        self.assertIsNone(result['Upper95GX'])
        self.assertFalse(result['Qualified'])
        self.assertFalse(selector.qualification_v3([row] * 4, [row] * 4, self.geometry, .8, None)['Qualified'])
        failed = copy.deepcopy(row); failed['CalibrationSucceeded'] = False
        self.assertFalse(selector.qualification_v3([row] * 4, [row, row, row, failed], self.geometry, .8, True)['Qualified'])
        bad = copy.deepcopy(row); bad['AcceptedFullEnergy'][0] = bad['FullEnergy'][0] + 1
        with self.assertRaises(ValueError):
            selector.validate_row_v3(bad, self.geometry, self.recipe)
        bad = copy.deepcopy(row); bad['SumSquaredDirectX'] += 1e-6
        with self.assertRaises(ValueError):
            selector.validate_row_v3(bad, self.geometry, self.recipe)
        with self.assertRaises(ValueError):
            selector.upper_signed([0.0, 0.0, float('nan'), 0.0])
        nofull = copy.deepcopy(row); nofull['FullEnergy'][2] = 0; nofull['AcceptedFullEnergy'][2] = 0
        self.assertIsNone(selector.acceptance_v3([nofull], self.geometry)[2])
        self.assertIsNone(selector.sensitivity_v3([nofull], self.geometry)[2])
        bad = copy.deepcopy(row); bad['AssignedToCrystal'][0] += 1
        with self.assertRaises(ValueError):
            selector.validate_row_v3(bad, self.geometry, self.recipe)

    def test_replicate_floor_rule(self):
        self.assertAlmostEqual(selector.component_bound(1.0, 0.5, 2.0, .5), 3.5)          # both within the floor
        self.assertAlmostEqual(selector.component_bound(4.0, 1.0, 0.5, .5), 4.0 / .75)    # resolved trend, r = 0.25
        self.assertIsNone(selector.component_bound(4.0, 3.0, 0.5, .5))                    # r = 0.75 refuses
        self.assertAlmostEqual(selector.upper_signed([-1.0, -1.0, -1.0, -1.0]), -1.0)

    def output(self, g_levels, g_rep, g_photon, g_doi, g_optrep, succeeded=True):
        """Development output whose entries carry a chosen gate statistic G (X and Y) on 100 accepted events."""
        entry = lambda g: {'Succeeded': succeeded, 'AcceptedTotal': 100, 'SumSquaredAngerX': 100 * (1e-4 + g),
                           'SumSquaredAngerY': 100 * (1e-4 + g), 'SumSquaredDirectX': 1e-2, 'SumSquaredDirectY': 1e-2}
        return {'Numerical': {'Calibration-0': {'Levels': [entry(g) for g in g_levels], 'Replicate': entry(g_rep)},
                              'Photons': {'Lines': [entry(g_photon)]},
                              'Doi': {'Levels': [{'Lines': [entry(g)]} for g in g_doi]},
                              'OpticsReplicate': {'Lines': [entry(g_optrep)]}}}

    def test_numerical_bound_on_the_gate_statistic(self):
        rule = {'ContractionLimit': .5, 'PhotonContractionAssumed': .5, 'NumericalAllocationFraction': .1}
        a2 = self.allocation ** 2
        # Calibration differences 2e-8 / 1e-8 within the floor 3e-8; optics floor 2e-8; photons 1e-8; DOI 1e-8 / 1e-8.
        data = [self.output([0, 2e-8, 3e-8], 3e-8, 1e-8, [1e-8, 2e-8], 2e-8) for _ in range(4)]
        line = selector.numerical_v3(data, self.geometry, {'EnergiesKeV': [661.7]}, rule)['Lines']['661.7']
        expected = (2e-8 + 1e-8 + 3e-8) + (1e-8 + 2e-8) + (1e-8 + 1e-8 + 2e-8)
        self.assertAlmostEqual(line['TotalX'] / a2, expected / a2, places=9)
        self.assertEqual(line['Bounded'], expected <= .1 * a2)
        failed = [self.output([0, 2e-8, 3e-8], 3e-8, 1e-8, [1e-8, 2e-8], 2e-8, succeeded=False)] + data[1:]
        refused = selector.numerical_v3(failed, self.geometry, {'EnergiesKeV': [661.7]}, rule)['Lines']['661.7']
        self.assertFalse(refused['Bounded'])
        self.assertIsNone(refused['TotalX'])


if __name__ == '__main__':
    unittest.main()
