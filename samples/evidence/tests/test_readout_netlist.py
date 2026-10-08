"""TODO-19 RD-10: the independent stdlib nodal solver (samples/readout/netlist_check.py) against hand-solved circuits, and
the committed engine-exported netlists against the engine's own DC charge fractions within the derived bound."""
import json
import pathlib
import sys
import unittest

REPO = pathlib.Path(__file__).resolve().parents[3]
sys.path.insert(0, str(REPO / 'samples' / 'readout'))
import netlist_check as nc  # noqa: E402

ASSETS = REPO / 'docs' / 'assets' / 'readout'


class ReadoutNetlistTests(unittest.TestCase):
    def test_corner_grid_two_by_two_matches_hand_solution(self):
        # Equal resistors, unit current into S0: V1 = V2 = 1/5, V0 = 7/15, V3 = 2/15 (G = 1 S) -> drains 7/15, 3/15, 3/15, 2/15.
        text = '\n'.join([
            'I_S0 0 S0 DC 0', 'I_S1 0 S1 DC 0', 'I_S2 0 S2 DC 0', 'I_S3 0 S3 DC 0',
            'R1 S0 S1 1', 'R2 S0 S2 1', 'R3 S1 S3 1', 'R4 S2 S3 1',
            'R5 S0 OUT_A 1', 'R6 S1 OUT_B 1', 'R7 S2 OUT_C 1', 'R8 S3 OUT_D 1',
            'V_A OUT_A 0 DC 0', 'V_B OUT_B 0 DC 0', 'V_C OUT_C 0 DC 0', 'V_D OUT_D 0 DC 0', '.end'])
        row = nc.solve(text)['Fractions'][0]
        for got, want in zip(row, [7 / 15, 3 / 15, 3 / 15, 2 / 15]):
            self.assertAlmostEqual(got, want, delta=1e-12)

    def test_single_row_chain_divides_as_a_series_chain(self):
        # Row chain of S nodes, each end grounded through two column resistors r in parallel (r/2): right share of node k is
        # ((k+1)R + r/2) / ((S+1)R + r).
        s, big_r, small_r = 7, 1000.0, 100.0
        cards = [f'I_S{k} 0 N{k} DC 0' for k in range(s)]
        chain = ['L'] + [f'N{k}' for k in range(s)] + ['R']
        cards += [f'R{i} {a} {b} {big_r}' for i, (a, b) in enumerate(zip(chain, chain[1:]))]
        cards += [f'RL1 OUT_A L {small_r}', f'RL2 L OUT_C {small_r}', f'RR1 OUT_B R {small_r}', f'RR2 R OUT_D {small_r}']
        cards += ['V_A OUT_A 0 DC 0', 'V_B OUT_B 0 DC 0', 'V_C OUT_C 0 DC 0', 'V_D OUT_D 0 DC 0', '.end']
        fractions = nc.solve('\n'.join(cards))['Fractions']
        for k, row in enumerate(fractions):
            self.assertAlmostEqual(row[1] + row[3], ((k + 1) * big_r + small_r / 2) / ((s + 1) * big_r + small_r), delta=1e-12)

    def test_load_resistor_outputs_conserve_charge(self):
        text = '\n'.join(['I_S0 0 S0 DC 0', 'R1 S0 X 10', 'R2 X OUT_A 10', 'R3 X OUT_B 30',
                          'R_LOAD_A OUT_A 0 50', 'R_LOAD_B OUT_B 0 50', '.end'])
        row = nc.solve(text)['Fractions'][0]
        self.assertAlmostEqual(sum(row), 1.0, delta=1e-12)
        self.assertAlmostEqual(row[0], 80 / 140, delta=1e-12)  # branch resistances 60 and 80 ohm divide inversely

    def test_committed_netlists_match_the_engine_within_the_derived_bound(self):
        prefixes = sorted(p.with_name(p.name[:-4]) for p in ASSETS.glob('*.cir'))
        self.assertGreaterEqual(len(prefixes), 3)
        for prefix in prefixes:
            with self.subTest(netlist=prefix.name):
                report = nc.compare(prefix)
                self.assertTrue(report['Pass'], report)
                self.assertLessEqual(report['MaxDeviation'], report['Bound'])
                stored = json.loads(prefix.with_name(prefix.name + '.comparison.json').read_text(encoding='utf-8'))
                self.assertEqual(stored, report)  # deterministic, and bound to the committed netlist / config bytes


if __name__ == '__main__':
    unittest.main()
