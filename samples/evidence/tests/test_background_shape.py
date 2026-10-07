"""Small deterministic checks of the statistical unit and signed-vector criterion."""
import importlib.util
import pathlib
import sys
import unittest

HERE = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('background_aggregate', HERE / 'background-shape/aggregate_background.py')
aggregate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(aggregate)


class BackgroundShapeTests(unittest.TestCase):
    def test_budget_revision_preserves_selected_physics_and_immutable_references(self):
        import copy
        import json
        folder = HERE / 'background-shape'
        request = json.loads((folder / 'request-v2.json').read_bytes())
        base = json.loads((folder / 'request-v1.json').read_bytes())
        hashes = [aggregate.pv.file_sha(folder / name) for name in ['request-v1.json', 'pinned-v1.json']]
        aggregate.validate_budget(request, base, *hashes)
        for key, value in [('SourcePhotons', 1), ('ValidationRepeats', 300), ('PilotRepeats', 16)]:
            wrong = copy.deepcopy(request)
            wrong[key] = value
            with self.assertRaises(ValueError):
                aggregate.validate_budget(wrong, base, *hashes)
        for key in ['BaseRequestSha256', 'PinnedSha256']:
            wrong = copy.deepcopy(request)
            wrong['BudgetRevision'][key] = '0' * 64
            with self.assertRaises(ValueError):
                aggregate.validate_budget(wrong, base, *hashes)

    def test_version_two_families_separate_timing_seeds_from_validation(self):
        import json
        manifest = json.loads((HERE / 'manifest-background-shape-v2.json').read_bytes())
        for family in manifest['families']:
            pilot = 'pilot' in family['id']
            self.assertEqual('BG_SELECTION16' if pilot else 'BG_VALIDATION32', family['seeds'])
            self.assertEqual(16 if pilot else 32, family['n'])

    def test_conditional_screen_budget_3200_has_exact_integer_cutoff(self):
        minimum = next(k for k in range(3000, 3201) if aggregate.lower(k, 3200) >= .95)
        self.assertGreaterEqual(aggregate.lower(minimum, 3200), .95)
        self.assertLess(aggregate.lower(minimum - 1, 3200), .95)

    def test_vector_bound_does_not_confuse_equal_norms_with_equal_vectors(self):
        # Equal norms of two opposite errors still give a paired displacement of two units.
        vector = [[[2.0, 0.0]] for _ in range(16)]
        self.assertEqual(0.0, aggregate.simultaneous_radius(vector, repeats=20))
        self.assertEqual(2.0, float(aggregate.np.linalg.norm(aggregate.np.mean(vector, axis=0))))

    def test_bootstrap_resamples_clusters_and_is_deterministic(self):
        vector = [[[float(i), -float(i)], [float(i), -float(i)]] for i in range(16)]
        a = aggregate.simultaneous_radius(vector, repeats=100)
        self.assertEqual(a, aggregate.simultaneous_radius(vector, repeats=100))
        self.assertGreater(a, 0)
        # Duplicate conditions do not create independent calibration clusters or increase the radius.
        self.assertEqual(a, aggregate.simultaneous_radius([[row[0]] for row in vector], repeats=100))

    def test_no_association_valid_regime_refuses_iteration_selection(self):
        with self.assertRaises(ValueError):
            aggregate.choose_iteration({}, [60, 120])

    def test_iteration_tie_selects_smaller_count_on_common_regimes(self):
        cells = {'a': {'Field': .1, 'Estimators': {f'E4@{i}': {
            'Association': {'ConditionalLower95': .96}, 'VectorExcessNormMm': 1} for i in [60, 120]}}}
        selected, regimes, scores = aggregate.choose_iteration(cells, [60, 120])
        self.assertEqual(60, selected)
        self.assertEqual(['a'], regimes)

    def test_selection_acquisition_budget_is_derived_from_zero_failure_limit(self):
        self.assertGreaterEqual(aggregate.lower(64, 64), .95)
        self.assertLess(aggregate.lower(58, 58), .95)

    def test_target_scaling_preserves_the_simultaneous_bootstrap(self):
        # A fixed geometric target, not observed variance, supplies each condition's scale.
        targets = aggregate.np.asarray([.5, 20.0])
        vectors = aggregate.np.asarray([[[i*.5, 0], [i*20.0, 0]] for i in range(16)])
        normalized = vectors / targets[None, :, None]
        radius = aggregate.simultaneous_radius(normalized, repeats=100)
        self.assertEqual(radius, aggregate.simultaneous_radius(normalized[:, :1], repeats=100))
        self.assertEqual(40.0, targets[1] / targets[0])

    def test_phase_seed_lists_are_pinned_and_disjoint(self):
        import json
        catalog = json.loads((HERE / 'seeds.json').read_text(encoding='utf-8'))
        development = {330001, 330007, 330019}
        names = ['BG_SELECTION16', 'BG_VALIDATION32', 'BG_CONFIRMATION32']
        lists = [catalog[name] for name in names]
        self.assertEqual([530001 + 104729*i for i in range(16)], lists[0])
        self.assertEqual([730001 + 130363*i for i in range(32)], lists[1])
        self.assertEqual([970001 + 154858*i for i in range(32)], lists[2])
        joined = [seed for rows in lists for seed in rows]
        self.assertEqual(80, len(set(joined)))
        previous = {seed for name, rows in catalog.items() if isinstance(rows, list) and name not in names for seed in rows}
        self.assertFalse((previous | development) & set(joined))
