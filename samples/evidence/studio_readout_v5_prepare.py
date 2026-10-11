"""Freeze v5 only after complete DEV4 budget selection; never launches a measurement."""
import argparse
import json
import pathlib

import provenance as pv

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
LEVELS = {'Wide': [8000, 32000], 'High': [64000]}


def select_budget(records, geometry, seeds):
    for level in LEVELS[geometry]:
        rows = [r for r in records if r['Geometry'] == geometry and r['Budget'] == level]
        if len(rows) != len(seeds) or sorted(r['Seed'] for r in rows) != sorted(seeds):
            raise ValueError(f'incomplete DEV4 {geometry}/{level}; no recipe frozen')
        for row in rows:
            n = 34 if geometry == 'Wide' else 44
            if (row['EnergyKeV'] != 661.7 or row['Scored'] != n * n * level
                    or type(row['Succeeded']) is not bool
                    or row['Succeeded'] and (row['Markers'] != n * n or row['Failure'] is not None)
                    or not row['Succeeded'] and not row['Failure']):
                raise ValueError('corrupt development result')
        if all(row['Succeeded'] for row in rows):
            return level
    raise ValueError(f'{geometry}: no candidate succeeds on DEV4; no repair recipe frozen')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--development', required=True, help='directory of calibration-only budget-*.json results')
    parser.add_argument('--probe', default=str(HERE / 'probe-v5/bin/Release/net9.0/Gcam.EvidenceProbe.dll'))
    args = parser.parse_args()
    source = pathlib.Path(args.development)
    rows = [pv.read_json(p) for p in sorted(source.glob('budget-*.json'))
            if p.name.startswith(('budget-Wide-', 'budget-High-'))]
    seeds = pv.read_json(HERE / 'seeds.json')
    budgets = {g: select_budget(rows, g, seeds['T41_DEV4']) for g in LEVELS}
    evidence = {'Version': 5, 'Budgets': budgets, 'Seeds': seeds['T41_DEV4'],
                'CandidateLevels': LEVELS, 'Rule': 'Smallest tested level succeeding on all four DEV4',
                'SourceSha256': pv.file_sha(HERE / 'probe-v5-budget/Program.cs'),
                'DevelopmentResults': rows,
                'ZeroOfFourNominalOneSided95FailureUpper': 1 - .05 ** .25,
                'WideTwoLevelBonferroniOneSided95FailureUpper': 1 - .025 ** .25,
                'PriorConfirmation': {'Wide': {'Budget': 2000, 'Failed': 4, 'Total': 32},
                                      'High': {'Budget': 32000, 'Failed': 1, 'Total': 32}},
                'RiskScope': 'Development-selected budget; old confirmation failures at different budgets cannot be pooled. Fresh REPAIR32 is final; zero/32 gives nominal one-sided95 failure upper1-.05^(1/32).'}
    evidence_pin = {'File': 'samples/evidence/studio-readout/development-calibration-budget-v5.json',
                    'Sha256': pv.sha(pv.json_bytes(evidence))}
    rule_pin = {'File': 'samples/evidence/studio-readout/rule-v5.json', 'Sha256': pv.file_sha(HERE / 'studio-readout/rule-v5.json')}
    old = pv.read_json(HERE / 'studio-readout/recipe-v4.json')
    recipe = {k: v for k, v in old.items() if k not in ('CalibrationHistoriesPerCrystal', 'High662CalibrationHistoriesPerCrystal', 'High662BudgetEvidence')}
    recipe.update(Version=5, Rule=rule_pin, BudgetEvidence=evidence_pin,
                  Design='Declared repair: identical v4 optics, sensors, pulse/trigger, LUT v2 and constants; only two calibration budgets change',
                  Geometries=[dict(g, CalibrationHistoriesPerCrystal=budgets[g['Id']]) for g in old['Geometries'] if g['Id'] in budgets],
                  EnergiesKeV=[661.7], CostSeeds=seeds['T41_REPAIR32'][:8],
                  CostReduction='Fixed first eight repair seeds; calibration and crystal diagnostics all32',
                  Scope=['Wide/661.7', 'High/661.7'], OtherCells='RetainPublishedV4Verdicts', FinalRepair=True)
    recipe['BaseV4'] = {name: {'File': file, 'Sha256': pv.file_sha(REPO / file)} for name, file in {
        'Recipe': 'samples/evidence/studio-readout/recipe-v4.json',
        'Rule': 'samples/evidence/studio-readout/rule-v4.json',
        'Manifest': 'samples/evidence/manifest-studio-readout-v4.json'}.items()}
    recipe['PurposeKeys']['LineOffset'] = '100 fixed for 661.7keV, preserving the v4 physical stream convention; optics offset0'
    recipe_pin = {'File': 'samples/evidence/studio-readout/recipe-v5.json', 'Sha256': pv.sha(pv.json_bytes(recipe))}
    managed = {p: pv.sha(b) for p, b in pv.managed_files(args.probe).items()}
    driver = pv.file_sha(HERE / 'studio_readout_v5_driver.py')
    manifest = {'_about': 'Final declared repair on fresh REPAIR32; only Wide/661.7 and High/661.7. Other v4 verdicts retained. Costs descriptive.',
                'Recipe': recipe_pin,
                'config_sha256': {'scenario.json': pv.file_sha(REPO / 'samples/scenario.json'),
                                  'evidence/studio-readout/recipe-v5.json': recipe_pin['Sha256']},
                'families': [{'id': 't41-repair-' + g + '-v5', 'kind': 'probe', 'mode': 'studio-readout-v5',
                              'recipe_driver': 'samples/evidence/studio_readout_v5_driver.py', 'executable': 'Gcam.EvidenceProbe.dll',
                              'seeds': 'T41_REPAIR32', 'n': 32, 'Phase': 'repair', 'Geometry': g, 'Recipe': recipe_pin,
                              'Freeze': {'Kind': 'FinalDeclaredBudgetRepair;SelectionSkipped', 'RecipeSha256': recipe_pin['Sha256'],
                                         'RuleSha256': rule_pin['Sha256'], 'DriverSha256': driver, 'ManagedClosure': managed}}
                             for g in ('Wide', 'High')]}
    # Preflight all measured populations and serialize before writing any of the new pins.
    outputs = {REPO / evidence_pin['File']: pv.json_bytes(evidence), REPO / recipe_pin['File']: pv.json_bytes(recipe),
               HERE / 'manifest-studio-readout-v5.json': pv.json_bytes(manifest)}
    for path in outputs:
        if path.exists():
            raise ValueError('refusing to replace frozen v5 inputs: ' + path.name)
    for path, data in outputs.items():
        path.write_bytes(data)
        print(path.name, pv.sha(data))


if __name__ == '__main__':
    main()
