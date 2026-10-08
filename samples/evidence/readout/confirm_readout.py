"""TODO-19 turn 5: the pinned pitch-confirmation rule, applied to the confirmation aggregate (stdlib only).

    python samples/evidence/readout/confirm_readout.py --aggregate <readout-confirmation-v1.json> [--publish <out.json>]

The rule (also stated in confirmation-pin-v1.json, which pins this file, the request and the seed list by SHA-256 and was
written before any confirmation run) — every comparison uses seed-cluster standard errors, SE = SD/sqrt(N) over the 32
seeds, and a difference counts only when |delta| > 2 * sqrt(SE_a^2 + SE_b^2):

0. Eligibility: calibration succeeded on all seeds; constraints K1-K3 of the turn-4 rule (122 keV acceptance >= 0.90 and
   weakest crystal >= 0.50; 1332 keV acceptance >= 0.90 and pooled mis-ID <= 0.002; 662 keV FWHM <= 0.08).
1. Pitch, 1.0 mm against 3.2 mm. For each pitch take its eligible unit with the lowest mean M1 (662 keV single-crystal
   photopeak mis-identification). Compare M1; if no difference, compare M2 (662 keV localisation penalty at equal counts
   against the geometry's own direct reference, SE combined in quadrature); if still no difference, **3.2 mm wins**:
   the less-saturating choice (about 1.2 against 9 photoelectrons per 50 um microcell for a 662 keV deposit at the exit
   face) and a real catalogue sensor size at its array pitch, where 1.0 mm needs a virtual 1 mm sensor. 2.2 mm is
   reported as context; if its best unit beats both other pitches on M1, that is reported as a finding needing fresh
   seeds, the decision stays 1.0 vs 3.2.
2. Wall at 3.2 mm (only if 3.2 mm wins): 0.2 against 0.5 mm by M1, then M2; no difference -> **0.2 mm** (geometric
   fill 0.88 against 0.71 of the face; the published array example's reflector).
3. DPC column / row ratio within the chosen geometry: r0.01 against r0.03 by M3 (122 keV single-crystal photopeak
   mis-identification), then M1; no difference -> **r0.03** (the less extreme resistor ratio, robust in selection and
   validation: calibration succeeded on every seed at every pitch).
The microcell-saturation caveat stays a stated limitation whatever wins (no recovery model; infinite cells simulated).
"""
import argparse
import hashlib
import json
import math
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import provenance as pv  # noqa: E402
from select_readout import Unit, units  # noqa: E402

PIN = HERE / 'confirmation-pin-v1.json'


def differs(a, b):
    """-1 if a is significantly smaller, +1 if larger, 0 if no difference (2 combined seed SE)."""
    if a is None or b is None:
        return 0
    d, se = a[0] - b[0], math.sqrt(a[1] ** 2 + b[1] ** 2)
    return 0 if abs(d) <= 2 * se else (-1 if d < 0 else 1)


def m1(u):
    return u.energy(661.7, 'Flood/MisIdSinglePhotopeak/p')


def m2(u):
    return u.l2()


def m3(u):
    return u.energy(122.06, 'Flood/MisIdSinglePhotopeak/p')


def compare(a, b, metrics, default):
    """Lexicographic: first significant metric decides; otherwise the stated default."""
    trail = []
    for name, f in metrics:
        c = differs(f(a), f(b))
        trail.append({'Metric': name, 'A': f(a), 'B': f(b), 'Result': c})
        if c:
            return (a if c < 0 else b), trail
    return default, trail


def check_pin():
    pin = json.loads(PIN.read_text(encoding='utf-8'))
    for key, path in (('RequestSha256', HERE / 'request-confirmation-v1.json'), ('ScriptSha256', pathlib.Path(__file__)),
                      ('SelectScriptSha256', HERE / 'select_readout.py')):
        got = hashlib.sha256(path.read_bytes().replace(b'\r\n', b'\n')).hexdigest()
        if got != pin[key]:
            raise SystemExit(f'pin mismatch for {path.name}: the rule or request changed after pinning')
    return pin


def decide(metrics, seeds):
    all_units = [Unit(metrics, g, v, t, i) for i, (g, v, t) in enumerate(units(metrics))]
    ok = [u for u in all_units if u.eligible(seeds) and all(u.constraints().values())]
    best = {}
    for pitch, geos in (('1.0', ('p1.0-g0',)), ('2.2', ('p2.2-g0.2',)), ('3.2', ('p3.2-g0.2', 'p3.2-g0.5'))):
        cands = [u for u in ok if u.g in geos and m1(u) is not None]
        best[pitch] = min(cands, key=lambda u: m1(u)[0]) if cands else None
    steps = {'Eligible': [u.name() for u in ok]}
    a, b = best['1.0'], best['3.2']
    if a is None or b is None:
        raise SystemExit('a pitch has no eligible unit; the confirmation cannot decide')
    winner, trail = compare(a, b, [('M1', m1), ('M2', m2)], b)
    pitch = '1.0' if winner is a else '3.2'
    steps['Pitch'] = {'A_1.0mm': a.name(), 'B_3.2mm': b.name(), 'Trail': trail, 'Winner': pitch}
    if best['2.2'] is not None:
        steps['Pitch2.2_Context'] = {'Unit': best['2.2'].name(), 'M1': m1(best['2.2']),
                                     'BeatsBoth': differs(m1(best['2.2']), m1(a)) < 0 and differs(m1(best['2.2']), m1(b)) < 0}
    geometry = 'p1.0-g0'
    if pitch == '3.2':
        walls = {g: min((u for u in ok if u.g == g), key=lambda u: m1(u)[0], default=None) for g in ('p3.2-g0.2', 'p3.2-g0.5')}
        if walls['p3.2-g0.2'] and walls['p3.2-g0.5']:
            w, trail = compare(walls['p3.2-g0.2'], walls['p3.2-g0.5'], [('M1', m1), ('M2', m2)], walls['p3.2-g0.2'])
            geometry = w.g
            steps['Wall'] = {'Trail': trail, 'Winner': geometry}
        else:
            geometry = next(g for g, u in walls.items() if u)
            steps['Wall'] = {'Winner': geometry, 'Note': 'only one wall eligible'}
    r1 = next((u for u in ok if u.g == geometry and u.v == 'Anger-DPC-r0.01'), None)
    r3 = next((u for u in ok if u.g == geometry and u.v == 'Anger-DPC-r0.03'), None)
    if r1 and r3:
        final, trail = compare(r1, r3, [('M3', m3), ('M1', m1)], r3)
        steps['Ratio'] = {'Trail': trail, 'Winner': final.v}
    else:
        final = r1 or r3
        steps['Ratio'] = {'Winner': final.v, 'Note': 'only one ratio eligible'}
    return final, steps, all_units


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--aggregate', required=True)
    ap.add_argument('--publish')
    ap.add_argument('--pilot', action='store_true', help='accept the pilot family (a smoke test, never a decision)')
    args = ap.parse_args()
    pin = check_pin()
    doc = json.loads(pathlib.Path(args.aggregate).read_text(encoding='utf-8'))
    family = 'readout_confirmation_pilot_v1' if args.pilot else 'readout_confirmation_v1'
    if doc['Family'] != family or doc['RequestSha256'] != pin['RequestSha256']:
        raise SystemExit('aggregate is not the pinned confirmation family / request')
    if args.pilot and args.publish:
        raise SystemExit('a pilot is never published as a decision')
    final, steps, all_units = decide(doc['Metrics'], len(doc['Seeds']))
    report = {'Rule': 'confirm_readout.py docstring; pinned in confirmation-pin-v1.json before any confirmation run',
              'PinSha256': pv.file_sha(PIN), 'Seeds': len(doc['Seeds']), 'Winner': final.name(), 'Steps': steps,
              'Limitation': pin['Limitation'], 'Units': [u.summary() for u in all_units]}
    print(f'{"PILOT (not a decision) " if args.pilot else ""}confirmation winner: {final.name()}')
    print(json.dumps(steps.get('Pitch', {}).get('Trail'), indent=None))
    if args.publish:
        pv.validate_sidecar(args.aggregate)
        prov = pv.summary_provenance([pv.pinned_summary(doc)], __file__, args,
                                     {p.relative_to(pv.REPO).as_posix(): pv.file_sha(p) for p in
                                      (PIN, HERE / 'request-confirmation-v1.json', pathlib.Path(__file__), HERE / 'select_readout.py')})
        report = {'SchemaVersion': 1, 'Provenance': prov, **report}
        pv.publish({pathlib.Path(args.publish): pv.json_bytes(report)}, prov)
        print(f'published {args.publish}')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
