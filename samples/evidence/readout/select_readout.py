"""TODO-19 turn 4: apply the readout decision rule to the selection aggregate, then check it on validation (stdlib only).

    python samples/evidence/readout/select_readout.py --selection <readout-selection-v1.json> [--validation <...>] [--out <report.json>]

The rule was fixed before either aggregate was read (PLAN.Physics.RigReadout.Turn2.md, "Turn 4"):
eligibility = calibration succeeded on every seed; constraints K1 (122 keV flood acceptance >= 0.90, per-crystal minimum
>= 0.50), K2 (1332 keV acceptance >= 0.90, single-crystal photopeak mis-ID <= 0.002), K3 (662 keV FWHM <= 0.08);
lexicographic objective with a 2-combined-SE tolerance at each step: L1 662 keV photopeak mis-ID, L2 662 keV localisation
penalty at equal counts against the geometry's own direct reference, L3 122 keV photopeak mis-ID, L4 pile-up-mispositioned
photopeak fraction at 1e5 cps, then at 3e5 cps; ties: four channels before 144, lower threshold, request order.
Amendment A1 (declared after reading selection, before validation): the same rule over realisable readouts only — the
analytic IdealBilinear divider is a reference, not a circuit. Both the literal and the A1 winner are reported and checked.
Validation (rule unchanged): V1 eligibility and K1-K3 on validation means; V2 the winner within 2 combined SE of the
validation best on L1 and L2 among the selection's L1 survivors. Reports every unit's numbers; never changes the choice.
"""
import argparse
import json
import math
import pathlib
import re
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import provenance as pv  # noqa: E402

TOL = 2.0


def units(metrics):
    found = []
    for key in metrics:
        m = re.match(r'Geometries\[Name=([^\]]+)\]/Readouts\[Name=([^\]]+)\]/Triggers\[Trigger=([^\]]+)\]/Calibration/Succeeded$', key)
        if m:
            found.append(m.groups())
    return found


class Unit:
    def __init__(self, metrics, g, v, t, order):
        self.g, self.v, self.t, self.order = g, v, t, order
        self.m = metrics
        self.base = f'Geometries[Name={g}]/Readouts[Name={v}]/Triggers[Trigger={t}]'
        self.geo = f'Geometries[Name={g}]'

    def get(self, path, field='Mean'):
        entry = self.m.get(self.base + '/' + path)
        return None if entry is None else entry.get(field)

    def stat(self, path):
        entry = self.m.get(self.base + '/' + path)
        if entry is None or entry.get('Mean') is None:
            return None
        return entry['Mean'], entry.get('SE') or 0.0, entry['N']

    def energy(self, e, path):
        return self.stat(f'Energies[EnergyKeV={e}]/{path}')

    def eligible(self, seeds):
        s = self.stat('Calibration/Succeeded')
        return s is not None and s[0] == 1.0 and s[2] == seeds

    def channels(self):
        return 144 if self.v.startswith('Independent') else 4

    def threshold(self):
        m = re.search(r'(\d+)', self.t)
        return int(m.group(1)) if m else 0

    def l2(self):
        a = self.energy(661.7, 'Localisation/EqualCountErrorMm')
        b = self.energy(661.7, 'Localisation/DirectEqualCountErrorMm')
        if a is None or b is None:
            return None
        return a[0] - b[0], math.sqrt(a[1] ** 2 + b[1] ** 2), min(a[2], b[2])

    def rate(self, cps):
        pile = self.m.get(f'{self.base}/Energies[EnergyKeV=661.7]/Rate[RateCps={cps}]/MispositionedPiledPhotopeak', {}).get('Pooled')
        share = self.m.get(f'{self.base}/Energies[EnergyKeV=661.7]/Rate[RateCps={cps}]/PiledShareOfPhotopeak')
        if not pile or pile['p'] is None or share is None:
            return None
        p, n = pile['p'], pile['n']
        se_p = math.sqrt(max(p * (1 - p), 1e-12) / n)
        value = p * share['Mean']
        se = math.sqrt((se_p * share['Mean']) ** 2 + (p * (share.get('SE') or 0.0)) ** 2)
        return value, se, share['N']

    def constraints(self):
        a122 = self.energy(122.06, 'Flood/TriggeredFraction')
        min122 = self.energy(122.06, 'Flood/Acceptance/Min')
        a1332 = self.energy(1332.5, 'Flood/TriggeredFraction')
        mis1332 = self.m.get(f'{self.base}/Energies[EnergyKeV=1332.5]/Flood/MisIdSinglePhotopeak', {}).get('Pooled')
        fwhm = self.energy(661.7, 'Flood/FullEnergy/FwhmFraction')
        checks = {
            'K1_122_acceptance>=0.90': a122 is not None and a122[0] >= 0.90,
            'K1_122_min_crystal>=0.50': min122 is not None and min122[0] >= 0.50,
            'K2_1332_acceptance>=0.90': a1332 is not None and a1332[0] >= 0.90,
            'K2_1332_misid<=0.002': bool(mis1332) and mis1332['p'] is not None and mis1332['p'] <= 0.002,
            'K3_662_fwhm<=0.08': fwhm is not None and fwhm[0] <= 0.08,
        }
        return checks

    def name(self):
        return f'{self.g} / {self.v} / {self.t}'

    def summary(self):
        def s(x):
            return None if x is None else {'Mean': x[0], 'SE': x[1], 'N': x[2]}
        return {'Unit': self.name(), 'Channels': self.channels(),
                'Calibration': s(self.stat('Calibration/Succeeded')),
                'L1_MisId662': s(self.energy(661.7, 'Flood/MisIdSinglePhotopeak/p')),
                'L2_LocPenalty662Mm': s(self.l2()),
                'Loc662EqualCountMm': s(self.energy(661.7, 'Localisation/EqualCountErrorMm')),
                'Direct662EqualCountMm': s(self.energy(661.7, 'Localisation/DirectEqualCountErrorMm')),
                'L3_MisId122': s(self.energy(122.06, 'Flood/MisIdSinglePhotopeak/p')),
                'L4_PileMispos1e5': s(self.rate('100000')), 'L4b_PileMispos3e5': s(self.rate('300000')),
                'Acceptance122': s(self.energy(122.06, 'Flood/TriggeredFraction')),
                'Acceptance122Min': s(self.energy(122.06, 'Flood/Acceptance/Min')),
                'Acceptance1332': s(self.energy(1332.5, 'Flood/TriggeredFraction')),
                'MisId1332': s(self.energy(1332.5, 'Flood/MisIdSinglePhotopeak/p')),
                'Fwhm122': s(self.energy(122.06, 'Flood/FullEnergy/FwhmFraction')),
                'Fwhm662': s(self.energy(661.7, 'Flood/FullEnergy/FwhmFraction')),
                'Fwhm1332': s(self.energy(1332.5, 'Flood/FullEnergy/FwhmFraction')),
                'PeakToValleyMin662': s(self.energy(661.7, 'Flood/PeakToValley/Min')),
                'DisagreeDirect662': s(self.energy(661.7, 'Flood/DisagreeDirectPhotopeak/p')),
                'Throughput1e6': s(self.energy(661.7, 'Rate[RateCps=1000000]/Throughput')),
                'PiledFraction1e6': s(self.energy(661.7, 'Rate[RateCps=1000000]/PiledFraction')),
                'Constraints': self.constraints()}


def keep_best(cands, key):
    scored = [(c, key(c)) for c in cands]
    scored = [(c, k) for c, k in scored if k is not None]
    if not scored:
        return cands
    best = min(scored, key=lambda x: x[1][0])[1]
    return [c for c, k in scored if k[0] - best[0] <= TOL * math.sqrt(k[1] ** 2 + best[1] ** 2)]


def load(path):
    doc = json.loads(pathlib.Path(path).read_text(encoding='utf-8'))
    return doc, doc['Metrics'], len(doc['Seeds'])


def geometry_summary(metrics):
    """Per geometry: optics and the direct reference per line (mean, SE, N over seeds)."""
    out = {}
    pattern = re.compile(r'Geometries\[Name=([^\]]+)\]/(Optics/(?:MeanCollection|PePerKeV|PeakPePerMicrocell50um|Surface|WallReflectance)'
                         r'|Direct\[EnergyKeV=[^\]]+\]/(?:InWindow|ErrorMm|Events))$')
    for key, entry in metrics.items():
        m = pattern.match(key)
        if m and entry.get('Mean') is not None:
            out.setdefault(m.group(1), {})[m.group(2)] = {'Mean': entry['Mean'], 'SE': entry.get('SE'), 'N': entry['N']}
    return out


def realisable(u):
    return u.v != 'Anger-Ideal'


def select(metrics, seeds, admit=lambda u: True):
    all_units = [Unit(metrics, g, v, t, i) for i, (g, v, t) in enumerate(units(metrics))]
    steps = {}
    cands = [u for u in all_units if u.eligible(seeds) and admit(u)]
    steps['eligible'] = [u.name() for u in cands]
    cands = [u for u in cands if all(u.constraints().values())]
    steps['constraints'] = [u.name() for u in cands]
    cands = keep_best(cands, lambda u: u.energy(661.7, 'Flood/MisIdSinglePhotopeak/p'))
    steps['L1'] = [u.name() for u in cands]
    l1 = list(cands)
    cands = keep_best(cands, lambda u: u.l2())
    steps['L2'] = [u.name() for u in cands]
    cands = keep_best(cands, lambda u: u.energy(122.06, 'Flood/MisIdSinglePhotopeak/p'))
    steps['L3'] = [u.name() for u in cands]
    cands = keep_best(cands, lambda u: u.rate('100000'))
    steps['L4'] = [u.name() for u in cands]
    cands = keep_best(cands, lambda u: u.rate('300000'))
    steps['L4b'] = [u.name() for u in cands]
    cands.sort(key=lambda u: (u.channels(), u.threshold(), u.order))
    return all_units, steps, cands[0], l1


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--selection', required=True)
    ap.add_argument('--validation')
    ap.add_argument('--out')
    ap.add_argument('--publish', help='write the report with merged provenance and a sidecar (needs --validation)')
    args = ap.parse_args()
    sdoc, sm, sseeds = load(args.selection)
    all_units, steps, winner, l1 = select(sm, sseeds)
    _, steps_a1, winner_a1, l1_a1 = select(sm, sseeds, realisable)
    report = {'Rule': 'TODO-19 turn 4 rule (select_readout.py docstring); fixed before reading the aggregates; A1 declared '
                      'after selection, before validation',
              'Selection': {'Family': sdoc['Family'], 'Seeds': sseeds, 'Steps': steps, 'Winner': winner.name(),
                            'StepsA1': steps_a1, 'WinnerA1': winner_a1.name(),
                            'Units': [u.summary() for u in all_units]}}
    for label, w_, st in (('literal', winner, steps), ('A1 (realisable)', winner_a1, steps_a1)):
        print(f'selection winner, {label}: {w_.name()}')
        for step, names in st.items():
            print(f'  {step}: {len(names)} -> {names if len(names) <= 6 else names[:6] + ["..."]}')
    if args.validation:
        report['Validation'] = {}
        for label, w_sel, l1_sel, admit in (('Literal', winner, l1, lambda u: True), ('A1', winner_a1, l1_a1, realisable)):
            report['Validation'][label] = check(args.validation, w_sel, l1_sel, admit, label)
        _, vm_all, _ = load(args.validation)
        report['Validation']['Units'] = [Unit(vm_all, g, v, t, i).summary() for i, (g, v, t) in enumerate(units(vm_all))]
    if args.out:
        pathlib.Path(args.out).write_text(json.dumps(report, indent=1) + chr(10), encoding='utf-8')
    if args.publish:
        if not args.validation:
            raise SystemExit('--publish needs --validation')
        provs = []
        for path in (args.selection, args.validation):
            pv.validate_sidecar(path)
            provs.append(pv.pinned_summary(json.loads(pathlib.Path(path).read_text(encoding='utf-8'))))
        report['Selection']['Geometries'] = geometry_summary(sm)
        report['Validation']['Geometries'] = geometry_summary(load(args.validation)[1])
        report['Inputs'] = {pathlib.Path(p).name: pv.file_sha(p) for p in (args.selection, args.validation)}
        prov = pv.summary_provenance(provs, __file__, args,
                                     {pathlib.Path(__file__).relative_to(pv.REPO).as_posix(): pv.file_sha(__file__)})
        report = {'SchemaVersion': 1, 'Provenance': prov, **report}
        pv.publish({pathlib.Path(args.publish): pv.json_bytes(report)}, prov)
        print(f'published {args.publish}')
    return 0


def check(path, winner, l1, admit, label):
    vdoc, vm, vseeds = load(path)
    vunits = {u.name(): u for u in (Unit(vm, g, v, t, i) for i, (g, v, t) in enumerate(units(vm)))}
    w = vunits[winner.name()]
    v1 = w.eligible(vseeds) and all(w.constraints().values())
    pool = [vunits[u.name()] for u in l1 if u.name() in vunits and vunits[u.name()].eligible(vseeds)]

    def within(key):
        best = min((key(u) for u in pool if key(u) is not None), key=lambda k: k[0])
        mine = key(w)
        return mine is not None and mine[0] - best[0] <= TOL * math.sqrt(mine[1] ** 2 + best[1] ** 2), best, mine
    v2a = within(lambda u: u.energy(661.7, 'Flood/MisIdSinglePhotopeak/p'))
    v2b = within(lambda u: u.l2())
    _, vsteps, vwinner, _ = select(vm, vseeds, admit)
    result = {'Family': vdoc['Family'], 'Seeds': vseeds, 'Winner': winner.name(), 'V1_EligibleAndConstraints': v1,
              'V1_Constraints': w.constraints(),
              'V2_L1_WithinBest': v2a[0], 'V2_L1_Best': v2a[1], 'V2_L1_Winner': v2a[2],
              'V2_L2_WithinBest': v2b[0], 'V2_L2_Best': v2b[1], 'V2_L2_Winner': v2b[2],
              'Confirmed': bool(v1 and v2a[0] and v2b[0]),
              'RuleRerunOnValidation_Winner_InformationOnly': vwinner.name(),
              'RuleRerunOnValidation_Steps': vsteps,
              'WinnerSummary': w.summary()}
    print(f'validation {label} ({winner.name()}): V1 {v1}, V2-L1 {v2a[0]}, V2-L2 {v2b[0]} -> confirmed {result["Confirmed"]}')
    print(f'  (information only) rule re-run on validation picks: {vwinner.name()}')
    return result


if __name__ == '__main__':
    raise SystemExit(main())
