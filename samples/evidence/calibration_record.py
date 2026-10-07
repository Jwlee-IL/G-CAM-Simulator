"""Generate the calibration records' tables in docs/VV.Gcam.Calibration.md from the committed evidence files.

    python samples/evidence/calibration_record.py            # check everything, then rewrite the generated blocks
    python samples/evidence/calibration_record.py --check    # check everything, regenerate in memory, compare; no write
    python samples/evidence/calibration_record.py --release  # as --check, and also refuse prose still marked as a draft

Every file read is pinned in samples/evidence/calibration-records.json (SHA-256 of the bytes git stores; a file with a
CR byte is refused, so a CRLF checkout cannot pass as the pinned file). The generator never calls git, uses only the
standard library, and writes UTF-8 with LF line endings; its output depends on the pinned files only (no timestamps,
fixed number formats), so --check is byte-exact on any machine.

Cross-checks (any failure exits 1 and nothing is written):
- every pin in the spec, and every {File, Sha256} pin inside a manifest family's overrides or a request, matches the
  file; the manifest's config_sha256 matches each request and base scenario;
- the seed lists: expanded from seeds.json with the manifest's seeds / seed_offset / n, equal to the lists or counts the
  selection and validation files record; selection and validation seeds disjoint;
- thresholds: the validation file's per-configuration value equals the selection file's, key for key; the validation
  file (or its request) pins the selection file by hash;
- stored verdicts and pooled sums recomputed; the selection exceedances within the allowed count;
- angres floors: every validation condition maps to a floor; k = rate x N is an integer; the stored resolved
  separations recomputed.

Sections 1-5 of each record and the record header are written by a person and are never touched; the generator owns
only the text between `<!-- CAL-nn generated: begin -->` and `<!-- CAL-nn generated: end -->`, and the index between
`<!-- CAL index generated: begin -->` and `<!-- CAL index generated: end -->`.
"""
import argparse
import hashlib
import json
import math
import pathlib
import re
import sys

import provenance as pv

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
SPEC = HERE / 'calibration-records.json'
NO_CANDIDATE_FLOOR = -1e300
DRAFT = '*(draft — for review)*'
GENERATOR = 'python samples/evidence/calibration_record.py'


class Checks:
    def __init__(self):
        self.failures = []

    def require(self, ok, message):
        if not ok:
            self.failures.append(message)
        return ok


class Files:
    """Reads only pinned files, each verified once against its pin."""

    def __init__(self, pins, checks):
        self.pins, self.checks, self.cache, self.read, self.touched = pins, checks, {}, [], set()

    def raw(self, rel):
        rel = rel.replace('\\', '/')
        self.touched.add(rel)
        if rel in self.cache:
            return self.cache[rel]
        if rel not in self.pins:
            raise SystemExit(f'refused: {rel} is read but not pinned in {SPEC.name}')
        data = (REPO / rel).read_bytes()
        self.checks.require(b'\r' not in data, f'{rel}: contains CR bytes (expected LF as git stores it)')
        digest = hashlib.sha256(data).hexdigest()
        self.checks.require(digest == self.pins[rel], f'{rel}: SHA-256 {digest} does not match the pin {self.pins[rel]}')
        self.cache[rel] = data
        self.read.append(rel)
        return data

    def json(self, rel):
        return json.loads(self.raw(rel).decode('utf-8'))

    def sha(self, rel):
        self.raw(rel)
        return self.pins[rel]


# ---------- formatting (fixed, version-independent) ----------

def n(x):
    return f'{int(x):,}'


def pct(x, digits=3):
    return f'{100.0 * x:.{digits}f} %'


def kn(k, total):
    return f'{n(k)} / {n(total)}'


def sig(x, digits=4):
    """Significant digits without exponent notation: thousands as integers with separators."""
    if x == 0:
        return '0'
    if abs(x) >= 10 ** (digits - 1):
        return f'{x:,.0f}'
    return f'{x:.{digits}g}'


def num(s):
    return float(s)


def table(header, rows):
    out = ['| ' + ' | '.join(header) + ' |', '|' + '|'.join('---' for _ in header) + '|']
    out += ['| ' + ' | '.join(r) + ' |' for r in rows]
    return out


# ---------- shared pieces ----------

def manifest_family(manifest, family):
    fam = next((f for f in manifest['families'] if f['id'] == family), None)
    if fam is None:
        raise SystemExit(f'family {family} not in the manifest')
    return fam


def family_seeds(seeds, fam):
    first = fam.get('seed_offset', 0)
    return seeds[fam['seeds']][first:first + fam['n']], f"{fam['seeds']}[{first}:{first + fam['n']}]"


def find_pins(obj, out):
    """Every {File, Sha256} object inside a JSON tree (keys matched case-insensitively)."""
    if isinstance(obj, dict):
        keys = {k.lower(): k for k in obj}
        if 'file' in keys and 'sha256' in keys and isinstance(obj[keys['file']], str):
            out.append((obj[keys['file']], obj[keys['sha256']]))
        for v in obj.values():
            find_pins(v, out)
    elif isinstance(obj, list):
        for v in obj:
            find_pins(v, out)
    return out


def check_family_inputs(files, checks, manifest_rel, manifest, fam):
    """Manifest config pins (request + base scenarios), and every pin inside the overrides and the request."""
    base = 'samples/'
    request = fam['config']
    checks.require(request in manifest['config_sha256'], f'{manifest_rel}: no config_sha256 for {request}')
    for cfg, pin in manifest['config_sha256'].items():
        if cfg == request or not cfg.startswith('evidence/'):
            checks.require(files.sha(base + cfg) == pin, f'{manifest_rel}: config_sha256 of {cfg} does not match the file')
    pins = find_pins(fam.get('overrides', {}), []) + find_pins(files.json(base + request), [])
    for path, pin in pins:
        checks.require(files.sha(path) == pin, f'{fam["id"]}: pinned {path} does not match the file')
    return pins


def data_section(rec, manifest, rows, files_read, fixed, pins, provenance_rows=None):
    out = ['### 6. Data', '']
    head = ['Item', 'Value']
    body = [['Date measured (manifest)', manifest['measured']]] + rows
    body.append(['Seed overlap between selection and validation', 'none (checked)'])
    body.append(['Value fixed before validation', fixed])
    # Legacy records keep precisely the original fallback and generated bytes.
    if provenance_rows:
        body += provenance_rows
    else:
        body.append(['Engine commit of the runs', f"not recorded; the code landed in `{rec['CodeLandedIn']}`"])
    out += table(head, body)
    out += ['', 'Files read (SHA-256 of the bytes git stores; pinned in `samples/evidence/calibration-records.json`):', '']
    out += table(['File', 'SHA-256'], [[f'`{f}`', f'`{pins[f]}`'] for f in files_read])
    return out


def reproduce_section(rec):
    lines = ['### 8. Reproduce', '', '```bash'] + rec['Reproduce']
    lines += [f'{GENERATOR}            # rewrites this block', f'{GENERATOR} --check    # verifies it', '```']
    return lines


# ---------- gate / csco (threshold) adapters ----------

GATE_CASES = {'lab': 'lab geometry', 'head': 'hand-held head, scenario distance', 'head1m': 'hand-held head, 1 m',
              'head5m': 'hand-held head, 5 m'}
BOUND = {'BareCrystalAllFaces': 'bare', 'FrontOnlyThroughMask': 'front-only'}


def parse_gate_key(key):
    case, t, f, bound, window = key.split('|')
    return {'case': case, 't': num(t[2:]), 'field': num(f[2:]), 'bound': bound, 'window': window}


def gate_sort(p):
    return (list(GATE_CASES).index(p['case']), p['t'], p['field'], list(BOUND).index(p['bound']),
            ['open', 'cs662'].index(p['window']))


def parse_csco_key(key):
    parts = key.split('|')
    scene, t, co = parts[0], num(parts[1][2:]), parts[-1]
    env = parts[2:-1]
    if env == ['ideal']:
        environment, bound = 'ideal', None
    else:
        environment, bound = num(env[0][2:]), env[1]
    return {'scene': scene, 't': t, 'env': environment, 'bound': bound, 'co': num(co[3:])}


def csco_sort(p):
    env_rank = 0 if p['env'] == 'ideal' else 1 + list(BOUND).index(p['bound'])
    return (['coloc', 'sep2', 'sep5'].index(p['scene']), p['t'], env_rank, p['co'])


def co_label(bq, co_dose):
    mbq = bq / 1e6
    text = f'{mbq:g}' if abs(mbq - round(mbq, 1)) < 1e-9 else f'{mbq:.2f}'
    if co_dose and abs(bq * co_dose - 10.0) < 1e-3:
        text += ' (10 µSv/h)'
    return text


def threshold_rows(checks, rec, sel, val_rows, target, comparison):
    """One neutral row per configuration: value, selection and validation counts, verdict."""
    rows = {}
    per = sel['PerConfiguration']
    checks.require(set(per) == set(val_rows), f'{rec["Id"]}: validation keys differ from the selection keys')
    checks.require(set(per) == set(sel['Selection']), f'{rec["Id"]}: Selection keys differ from PerConfiguration')
    for key, value in per.items():
        s = sel['Selection'][key]
        v = val_rows.get(key)
        if v is None:
            continue
        checks.require(s['Threshold'] == value, f'{rec["Id"]} {key}: Selection.Threshold != PerConfiguration')
        checks.require(v['threshold'] == value, f'{rec["Id"]} {key}: validation threshold {v["threshold"]} != {value}')
        allowed = math.floor(sel['Alpha'] * s['Acquisitions'] + 1e-9)
        checks.require(s['AllowedExceedances'] == allowed, f'{rec["Id"]} {key}: AllowedExceedances != floor(alpha n)')
        checks.require(s['SelectionExceedances'] <= allowed, f'{rec["Id"]} {key}: selection exceedances above allowed')
        empty = s['WithCandidate'] == 0
        checks.require(empty == (value <= NO_CANDIDATE_FLOOR / 2), f'{rec["Id"]} {key}: no-candidate floor inconsistent')
        if empty:
            checks.require(v['k'] == 0, f'{rec["Id"]} {key}: trusted acquisitions without counts')
            verdict = 'not applicable — no counts'
        else:
            verdict = 'pass' if v['upper'] <= target else 'FAIL'
        checks.require(v['stored_pass'] == (v['upper'] <= target),
                       f'{rec["Id"]} {key}: stored verdict disagrees with upper limit <= target')
        rows[key] = {'value': value, 'sel_k': s['SelectionExceedances'], 'sel_n': s['Acquisitions'],
                     'with': s['WithCandidate'], 'k': v['k'], 'n': v['n'], 'upper': v['upper'],
                     'extra': v.get('extra'), 'verdict': verdict}
    return rows


def summary_lines(rows, target, criterion):
    judged = [r for r in rows.values() if not r['verdict'].startswith('not applicable')]
    na = len(rows) - len(judged)
    passed = sum(1 for r in judged if r['verdict'] == 'pass')
    k = sum(r['k'] for r in judged)
    total = sum(r['n'] for r in judged)
    worst_key, worst = max(((key, r) for key, r in rows.items() if r in judged), key=lambda t: (t[1]['upper'], t[0]))
    ties = sum(1 for r in judged if r['upper'] == worst['upper'])
    lines = [f'**Summary.** {len(rows)} values; **{passed} / {len(judged)} judged configurations pass** ({criterion}); '
             f'{na} not applicable; {len(judged) - passed} FAIL. Pooled over the judged configurations: '
             f'{kn(k, total)} = {pct(k / total)}'
             + (f' (all rows: {kn(k, sum(r["n"] for r in rows.values()))} = '
                f'{pct(k / sum(r["n"] for r in rows.values()))})' if na else '')
             + f'. Worst: `{worst_key}`, {kn(worst["k"], worst["n"])}, upper limit {pct(worst["upper"])}'
             + (f' ({ties} configurations share this limit).' if ties > 1 else '.')]
    return lines, {'values': len(rows), 'judged': len(judged), 'passed': passed, 'na': na,
                   'fail': len(judged) - passed}


def gate_record(rec, files, checks, seeds):
    manifest = files.json(rec['Manifest'])
    sel = files.json(rec['Selection']['File'])
    val = files.json(rec['Validation']['File'])
    sfam = manifest_family(manifest, rec['Selection']['Family'])
    vfam = manifest_family(manifest, rec['Validation']['Family'])
    s_seeds, s_range = family_seeds(seeds, sfam)
    v_seeds, v_range = family_seeds(seeds, vfam)
    checks.require(sel['Family'] == sfam['id'] and val['Family'] == vfam['id'], f'{rec["Id"]}: family names differ')
    checks.require(sel['Seeds'] == s_seeds, f'{rec["Id"]}: selection seed list != manifest slice')
    checks.require(sel['SeedList'] == sfam['seeds'] and sel['SeedOffset'] == sfam.get('seed_offset', 0),
                   f'{rec["Id"]}: selection SeedList / SeedOffset != manifest')
    checks.require(val['SeedList'] == vfam['seeds'] and val.get('SeedOffset', 0) == vfam.get('seed_offset', 0)
                   and val['Seeds'] == len(v_seeds), f'{rec["Id"]}: validation SeedList / offset / count != manifest')
    checks.require(not set(s_seeds) & set(v_seeds), f'{rec["Id"]}: selection and validation seeds overlap')
    checks.require(sel.get('Comparison', 'strict') == rec['Comparison'], f'{rec["Id"]}: Comparison != spec')
    check_family_inputs(files, checks, rec['Manifest'], manifest, sfam)
    pins = check_family_inputs(files, checks, rec['Manifest'], manifest, vfam)
    sel_sha = files.sha(rec['Selection']['File'])
    pinned = any(p == rec['Selection']['File'] and h == sel_sha for p, h in pins)
    checks.require(pinned, f'{rec["Id"]}: the validation family does not pin the selection file')
    checks.require(val['Thresholds']['File'] == rec['Selection']['File'] and val['Thresholds']['Sha256'] == sel_sha,
                   f'{rec["Id"]}: the validation summary names another threshold file')
    target = rec['TargetUpper95']
    checks.require(val['TargetFalseUpper95'] == target, f'{rec["Id"]}: TargetFalseUpper95 != spec')
    val_rows = {k: {'threshold': v['Threshold'], 'k': v['FalseTrusted'], 'n': v['Acquisitions'], 'upper': v['Upper95'],
                    'stored_pass': v['Pass'], 'extra': v['ExpectedBackgroundCounts']} for k, v in val['Nulls'].items()}
    rows = threshold_rows(checks, rec, sel, val_rows, target, rec['Comparison'])
    if 'PooledNulls' in val:
        p = val['PooledNulls']
        checks.require(p['FalseTrusted'] == sum(r['k'] for r in rows.values())
                       and p['Acquisitions'] == sum(r['n'] for r in rows.values())
                       and p['ConfigurationsPassing'] == sum(r['verdict'] == 'pass' for r in rows.values()),
                       f'{rec["Id"]}: stored PooledNulls disagree with the rows')
    any_sel = next(iter(sel['Selection'].values()))
    any_val = next(iter(val['Nulls'].values()))
    data_rows = [
        ['Selection family', f"`{sfam['id']}`: {s_range}, {len(s_seeds)} seeds × {n(any_sel['Acquisitions'] // len(s_seeds))} "
                             f"background-only acquisitions = {n(any_sel['Acquisitions'])} per configuration"],
        ['Validation family', f"`{vfam['id']}`: {v_range}, {len(v_seeds)} seeds × {n(any_val['Acquisitions'] // len(v_seeds))} "
                              f"= {n(any_val['Acquisitions'])} background-only acquisitions per configuration"],
        ['Selection level α; comparison', f"{sel['Alpha']}; " + ('trusted when the recorded Z (4 decimals) ≥ T, ties count '
                                                               'as exceedances' if rec['Comparison'] == 'RoundedAtLeast'
                                                               else 'trusted when Z > T (the file names no comparison: strict)')],
        ['Selection runs digest', f"`{sel['RunsDigest']}` (SHA-256 over the per-seed run files; the runs are not committed)"],
    ]
    fixed = f"yes — `{vfam['id']}`'s overrides pin `{rec['Selection']['File']}` by SHA-256, and the validation summary records the same hash"
    criterion = f'one-sided 95 % Clopper–Pearson upper limit of the false-trusted rate ≤ {pct(target, 0)}'
    summary, stats = summary_lines(rows, target, criterion)
    body = []
    for case, label in GATE_CASES.items():
        keys = sorted((k for k in rows if parse_gate_key(k)['case'] == case), key=lambda k: gate_sort(parse_gate_key(k)))
        body += ['', f'**{case}** — {label} ({len(keys)} configurations)', '']
        trs = []
        for k in keys:
            p, r = parse_gate_key(k), rows[k]
            trs.append([f"{p['t']:g} s", f"{p['field']:.2f}", BOUND[p['bound']], p['window'], f"{r['value']:.4f}",
                        kn(r['sel_k'], r['sel_n']), sig(r['extra'], 3), kn(r['k'], r['n']), pct(r['upper']),
                        '**FAIL**' if r['verdict'] == 'FAIL' else r['verdict']])
        body += table(['Exposure', 'Field µSv/h', 'Bound', 'Window', 'Threshold T', 'Selection ≥ T (k / N)',
                       'Expected background counts', 'Validation false trusted (k / N)', 'Upper 95 %', 'Verdict'], trs)
    values = ['### 7. Values and validation', ''] + summary + [
        '', 'Columns: the threshold; the selection acquisitions at or above it (strict rule: above it); the expected '
        'background counts per validation acquisition; the validation false-trusted count and its one-sided 95 % '
        'Clopper–Pearson upper limit; the verdict against the target.'] + body
    return data_rows, fixed, values, stats


def csco_record(rec, files, checks, seeds):
    manifest = files.json(rec['Manifest'])
    sel = files.json(rec['Selection']['File'])
    val = files.json(rec['Validation']['File'])
    sfam = manifest_family(manifest, rec['Selection']['Family'])
    vfam = manifest_family(manifest, rec['Validation']['Family'])
    s_seeds, s_range = family_seeds(seeds, sfam)
    v_seeds, v_range = family_seeds(seeds, vfam)
    checks.require(sel['Family'] == sfam['id'] and val['Family'] == vfam['id'], f'{rec["Id"]}: family names differ')
    checks.require(sel['Seeds'] == s_seeds and sel['SeedList'] == sfam['seeds']
                   and sel['SeedOffset'] == sfam.get('seed_offset', 0), f'{rec["Id"]}: selection seeds != manifest slice')
    checks.require(val['Seeds'] == v_seeds and val['SeedList'] == vfam['seeds'] and val['N'] == len(v_seeds)
                   and val['SeedOffset'] == vfam.get('seed_offset', 0), f'{rec["Id"]}: validation seeds != manifest slice')
    checks.require(not set(s_seeds) & set(v_seeds), f'{rec["Id"]}: selection and validation seeds overlap')
    checks.require(sel['Comparison'] == rec['Comparison'], f'{rec["Id"]}: Comparison != spec')
    check_family_inputs(files, checks, rec['Manifest'], manifest, sfam)
    pins = check_family_inputs(files, checks, rec['Manifest'], manifest, vfam)
    sel_sha = files.sha(rec['Selection']['File'])
    checks.require(any(p == rec['Selection']['File'] and h == sel_sha for p, h in pins),
                   f'{rec["Id"]}: the validation request does not pin the selection file')
    target = rec['TargetUpper95']
    co_dose = val.get('CoDoseMicroSvPerHourPerBq')
    val_rows = {k: {'threshold': v['Threshold'], 'k': v['Trusted']['K'], 'n': v['Trusted']['N'],
                    'upper': v['Trusted']['Upper95'], 'stored_pass': v['PassesOnePercent'],
                    'extra': v['ExpectedCounts662']['Median']} for k, v in val['NullValidation'].items()}
    for k, v in val['NullValidation'].items():
        checks.require(v['Trusted']['K'] <= v['Trusted']['N'], f'{rec["Id"]} {k}: K > N')
    rows = threshold_rows(checks, rec, sel, val_rows, target, rec['Comparison'])
    any_sel = next(iter(sel['Selection'].values()))
    any_val = next(iter(val['NullValidation'].values()))
    data_rows = [
        ['Selection family', f"`{sfam['id']}`: {s_range}, {len(s_seeds)} seeds × {n(any_sel['Acquisitions'] // len(s_seeds))} "
                             f"Cs-free acquisitions = {n(any_sel['Acquisitions'])} per configuration"],
        ['Validation family', f"`{vfam['id']}`: {v_range}, {len(v_seeds)} seeds × {n(any_val['Trusted']['N'] // len(v_seeds))} "
                              f"= {n(any_val['Trusted']['N'])} Cs-free acquisitions per configuration"],
        ['Selection level α; comparison', f"{sel['Alpha']}; trusted when the recorded Z_s (4 decimals) ≥ T, ties count as exceedances"],
        ['Selection runs digest', f"`{sel['RunsDigest']}` (SHA-256 over the per-seed run files; the runs are not committed)"],
    ]
    fixed = (f"yes — the validation request `samples/{vfam['config']}` pins `{rec['Selection']['File']}` by SHA-256, and the "
             f"manifest pins the request")
    criterion = f'one-sided 95 % Clopper–Pearson upper limit of the false-trusted rate ≤ {pct(target, 0)}'
    summary, stats = summary_lines(rows, target, criterion)
    body = []
    for scene in ['coloc', 'sep2', 'sep5']:
        for t in sorted({parse_csco_key(k)['t'] for k in rows}):
            keys = sorted((k for k in rows if parse_csco_key(k)['scene'] == scene and parse_csco_key(k)['t'] == t),
                          key=lambda k: csco_sort(parse_csco_key(k)))
            body += ['', f'**{scene}, {t:g} s** ({len(keys)} configurations)', '']
            trs = []
            for k in keys:
                p, r = parse_csco_key(k), rows[k]
                env = 'ideal' if p['env'] == 'ideal' else f"{p['env']:.2f} µSv/h, {BOUND[p['bound']]}"
                empty = r['verdict'].startswith('not applicable')
                trs.append([env, co_label(p['co'], co_dose), '— (no counts)' if empty else f"{r['value']:.4f}",
                            kn(r['sel_k'], r['sel_n']), sig(r['extra'], 4), kn(r['k'], r['n']), pct(r['upper']),
                            '**FAIL**' if r['verdict'] == 'FAIL' else r['verdict']])
            body += table(['Environment', 'Co-60 MBq', 'Threshold T', 'Selection ≥ T (k / N)',
                           'Expected 662 keV counts (median)', 'Validation false trusted (k / N)', 'Upper 95 %',
                           'Verdict'], trs)
    values = ['### 7. Values and validation', ''] + summary + [
        '', 'Scenes: `coloc` Cs-137 and Co-60 co-located; `sep2` / `sep5` two / five angular elements apart. Columns as '
        'in the gate records; the expected counts are the median over the validation seeds.'] + body
    return data_rows, fixed, values, stats


# ---------- angres floor adapter ----------

def total_counts(c):
    return round(c['CountsPerSource'] * (1 + 1 / c['Ratio']), 6)


def angres_record(rec, files, checks, seeds):
    manifest = files.json(rec['Manifest'])
    data = files.json(rec['Validation']['File'])
    target = rec['TargetUpper95']
    data_rows, floors_out, fail_rows, all_rows = [], [], [], []
    pairs = list(zip(rec['Selection']['Families'], rec['Validation']['Families']))
    checks.require(sorted(data['Families']) == sorted(rec['Validation']['Families']), f'{rec["Id"]}: families != spec')
    for sel_id, val_id in pairs:
        f = data['Families'][val_id]
        sfam, vfam = manifest_family(manifest, sel_id), manifest_family(manifest, val_id)
        s_seeds, s_range = family_seeds(seeds, sfam)
        v_seeds, v_range = family_seeds(seeds, vfam)
        checks.require(f['SelectionFamily'] == sel_id and f['ValidationFamily'] == val_id,
                       f'{rec["Id"]} {val_id}: family names differ')
        checks.require(f['SelectionSeeds'] == len(s_seeds) and f['ValidationSeeds'] == len(v_seeds),
                       f'{rec["Id"]} {val_id}: seed counts != manifest')
        checks.require(not set(s_seeds) & set(v_seeds), f'{rec["Id"]} {val_id}: selection and validation seeds overlap')
        check_family_inputs(files, checks, rec['Manifest'], manifest, sfam)
        check_family_inputs(files, checks, rec['Manifest'], manifest, vfam)
        checks.require(sfam['config'] == vfam['config'], f'{rec["Id"]} {val_id}: selection and validation requests differ')
        dist = f"{f['SourceDetectorMm'] / 1000:g} m"
        floors = {(x['Decoder'], x['TotalCounts'], x['Placement']): x for x in f['Floors']}
        conds = list(f['Conditions'].items())
        seps = sorted({c['SeparationElements'] for _, c in conds})
        sel_n = {x['SelectionNullsPerSeparation'] for x in f['Floors']}
        checks.require(len(sel_n) == 1, f'{rec["Id"]} {val_id}: selection N differs between floors')
        val_n = {e['N'] for _, c in conds for e in c['Decoders'].values()}
        checks.require(len(val_n) == 1, f'{rec["Id"]} {val_id}: validation N differs between conditions')
        data_rows.append([f'Selection family, {dist}',
                          f"`{sel_id}`: {s_range}, {len(s_seeds)} seeds; {n(next(iter(sel_n)))} single-source "
                          f"acquisitions per separation × {len(seps)} separations per floor"])
        data_rows.append([f'Validation family, {dist}',
                          f"`{val_id}`: {v_range}, {len(v_seeds)} seeds; {n(next(iter(val_n)))} single-source "
                          f"acquisitions per condition (separation × counts per source × ratio × placement)"])
        groups = {}
        for key, c in conds:
            for dec, e in c['Decoders'].items():
                fk = (dec, total_counts(c), c['Placement'])
                checks.require(fk in floors, f'{rec["Id"]} {val_id} {key} {dec}: no floor for the condition')
                fl = e['Floor']
                k = fl['Null'] * e['N']
                checks.require(abs(k - round(k)) < 0.33, f'{rec["Id"]} {val_id} {key} {dec}: k = rate x N not an integer')
                row = {'dist': dist, 'key': key, 'dec': dec, 'k': int(round(k)), 'n': e['N'], 'rate': fl['Null'],
                       'lo': fl['NullCi'][0], 'up': fl['NullCi'][1], 'sep': c['SeparationElements'],
                       'cps': c['CountsPerSource'], 'ratio': c['Ratio']}
                row['verdict'] = 'pass' if row['up'] <= target else 'FAIL'
                groups.setdefault(fk, []).append(row)
                all_rows.append(row)
                if row['verdict'] == 'FAIL':
                    fail_rows.append(row)
        # The stored resolved separations, recomputed (D-48's rule as aggregated: pooled rates).
        by = {}
        for _, c in conds:
            by.setdefault((c['Placement'], c['CountsPerSource'], c['Ratio']), []).append(c)
        stored = {(r['Placement'], r['CountsPerSource'], r['Ratio'], r['Decoder']): r for r in f['Resolved']}
        for (pl, cps, ratio), cs in by.items():
            cs.sort(key=lambda c: c['SeparationElements'])
            for dec in {d for c in cs for d in c['Decoders']}:
                rs = [c for c in cs if dec in c['Decoders']]
                ok = [c['Decoders'][dec]['Floor']['Pass'] >= 0.95 and c['Decoders'][dec]['Floor']['Null'] <= 0.05 for c in rs]
                res = next((rs[i]['SeparationElements'] for i in range(len(rs)) if all(ok[i:])), None)
                checks.require(stored[(pl, cps, ratio, dec)]['Floor']['ResolvedElements'] == res,
                               f'{rec["Id"]} {val_id}: stored resolved separation differs for {pl} {cps} {ratio} {dec}')
        for fk, fl in floors.items():
            rows = groups.get(fk, [])
            worst = max(rows, key=lambda r: (r['up'], r['rate'], r['key'])) if rows else None
            floors_out.append({'dist': dist, 'dec': fk[0], 'total': fk[1], 'placement': fk[2], 'assigned': fl['Assigned'],
                               'free': fl['HypothesisFree'], 'sel_n': fl['SelectionNullsPerSeparation'],
                               'conditions': len(rows), 'worst': worst,
                               'verdict': ('not applicable — no validation condition' if worst is None else
                                           'pass' if all(r['verdict'] == 'pass' for r in rows) else 'FAIL')})
        checks.require(all(x['Assigned'] is not None for x in f['Floors']), f'{rec["Id"]} {val_id}: an infinite floor')
    data_rows.append(['Selection level α; comparison',
                      f"{data['Families'][rec['Validation']['Families'][0]]['AlphaSelection']} at every separation of the grid; "
                      f"a second peak counts when its absolute prominence ≥ F (valley "
                      f"{data['Families'][rec['Validation']['Families'][0]]['Valley']})"])
    fixed = ('not needed — the validation runs record the raw per-acquisition statistic, so the floors are selected and '
             'applied in one aggregation step; the aggregator refuses overlapping seeds')
    judged = len(all_rows)
    passed = sum(r['verdict'] == 'pass' for r in all_rows)
    k = sum(r['k'] for r in all_rows)
    total = sum(r['n'] for r in all_rows)
    worst = max(all_rows, key=lambda r: (r['up'], r['rate'], r['key'], r['dec']))
    floor_fail = sum(x['verdict'] == 'FAIL' for x in floors_out)
    criterion = (f'upper end of the two-sided 95 % seed-bootstrap interval (a one-sided 97.5 % limit) of the single-source '
                 f'false split ≤ {pct(target, 0)}')
    summary = [f'**Summary.** {len(floors_out)} floors ({floor_fail} with a FAIL row); '
               f'**{passed} / {judged} validation rows pass** ({criterion}); {judged - passed} FAIL; 0 not applicable. '
               f'Pooled false split over all rows: {kn(k, total)} = {pct(k / total)}. Worst: {worst["dist"]}, `{worst["key"]}`, '
               f'{worst["dec"]}, {kn(worst["k"], worst["n"])} = {pct(worst["rate"], 2)} [{pct(worst["lo"], 2)}, {pct(worst["up"], 2)}].']
    body = []
    for dist in sorted({x['dist'] for x in floors_out}, key=lambda d: float(d[:-2])):
        for dec in ['cc', 'area@120', 'binary@8']:
            xs = sorted((x for x in floors_out if x['dist'] == dist and x['dec'] == dec),
                        key=lambda x: (['axis', 'edge'].index(x['placement']), x['total']))
            if not xs:
                continue
            body += ['', f'**{dist}, {dec}** ({len(xs)} floors)', '']
            trs = []
            for x in xs:
                w = x['worst']
                trs.append([x['placement'], n(x['total']), sig(x['assigned']), sig(x['free']), n(x['sel_n']),
                            str(x['conditions']),
                            '—' if w is None else f"Δ {w['sep']:g}, {n(w['cps'])} / source, 1 : {1 / w['ratio']:g}",
                            '—' if w is None else kn(w['k'], w['n']),
                            '—' if w is None else f"{pct(w['rate'], 2)} [{pct(w['lo'], 2)}, {pct(w['up'], 2)}]",
                            '**FAIL**' if x['verdict'] == 'FAIL' else x['verdict']])
            body += table(['Placement', 'Total counts', 'Floor F', 'Hypothesis-free F', 'Selection N per separation',
                           'Conditions', 'Worst condition', 'False split (k / N)', 'Rate [95 % interval]', 'Verdict'], trs)
    fails = ['', f'**Every FAIL row** ({len(fail_rows)} of {judged}):', '']
    fails += table(['Distance', 'Placement', 'Separation', 'Counts per source', 'Ratio', 'Decoder', 'False split (k / N)',
                    'Rate [95 % interval]', 'Verdict'],
                   [[r['dist'], r['key'].split('|')[0], f"Δ {r['sep']:g}", n(r['cps']), f"1 : {1 / r['ratio']:g}", r['dec'],
                     kn(r['k'], r['n']), f"{pct(r['rate'], 2)} [{pct(r['lo'], 2)}, {pct(r['up'], 2)}]", '**FAIL**']
                    for r in sorted(fail_rows, key=lambda r: (float(r['dist'][:-2]), r['key'].split('|')[0], r['sep'],
                                                              r['cps'], -r['ratio'], r['dec']))] or [['—'] * 9])
    values = ['### 7. Values and validation', ''] + summary + [
        '', 'Per floor (decoder × total counts × placement): F in the decoder\'s own image units (the primary floor, '
        'assigned to the hypothesised pair) and the hypothesis-free sensitivity floor; the selection N; the validation '
        'condition with the highest upper limit among the conditions this floor applies to, its false split k / N and '
        'the two-sided 95 % percentile bootstrap interval over seeds (2,000 resamples; the file stores only this '
        'interval, so a one-sided 95 % limit is not available); the verdict (FAIL when any of its conditions fails). The '
        'selection statistic at F is not recorded in the file (the rule bounds it at ≤ α per separation). All '
        f'{judged} condition rows stay in the JSON.'] + body + fails
    stats = {'values': len(floors_out), 'judged': judged, 'passed': passed, 'na': 0, 'fail': judged - passed}
    return data_rows, fixed, values, stats


ADAPTERS = {'gate': gate_record, 'csco': csco_record, 'angres_floor': angres_record}


# ---------- document ----------

def generate(spec, checks):
    files = Files(spec['Files'], checks)
    seeds = files.json('samples/evidence/seeds.json')
    blocks, index = {}, []
    for rec in spec['Records']:
        files.touched = {'samples/evidence/seeds.json'}
        data_rows, fixed, values, stats = ADAPTERS[rec['Kind']](rec, files, checks, seeds)
        prov_rows = []
        role_files = {role: files.json(rec[role]['File']) for role in ('Selection', 'Validation')}
        if any('Provenance' in value for value in role_files.values()):
            for role, value in role_files.items():
                if 'Provenance' not in value:
                    prov_rows.append([role + ' provenance', f"not recorded; the code landed in `{rec['CodeLandedIn']}`"])
                else:
                    names = rec[role].get('Families', [rec[role].get('Family')])
                    prov_rows += pv.data_rows(value, role, names)
        manifest = files.json(rec['Manifest'])
        record_files = sorted(files.touched)
        lines = data_section(rec, manifest, data_rows, record_files, fixed, files.pins, prov_rows) + [''] + values + [''] + reproduce_section(rec)
        blocks[rec['Id']] = lines
        index.append([rec['Id'], rec['Title'], f"`{rec['Selection']['File'].split('/')[-1]}`", str(stats['values']),
                      f"{stats['passed']} / {stats['judged']}", str(stats['fail']), str(stats['na'])])
    unused = sorted(set(spec['Files']) - set(files.read))
    checks.require(not unused, f'pinned but never read: {unused}')
    idx = table(['Record', 'Calibration', 'Selection file', 'Values', 'Validation rows passing', 'FAIL', 'Not applicable'],
                index)
    return blocks, idx


def replace_block(text, name, lines, checks):
    begin, end = f'<!-- {name} generated: begin -->', f'<!-- {name} generated: end -->'
    pattern = re.compile(re.escape(begin) + r'\n.*?' + re.escape(end), re.S)
    if len(pattern.findall(text)) != 1:
        checks.require(False, f'the document needs exactly one {begin} … {end} pair')
        return text
    return pattern.sub(lambda _: begin + '\n' + '\n'.join(lines) + '\n' + end, text)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--check', action='store_true', help='regenerate in memory and compare; never write')
    ap.add_argument('--release', action='store_true', help='as --check, and refuse prose still marked as a draft')
    args = ap.parse_args()
    for stream in (sys.stdout, sys.stderr):      # messages carry non-ASCII; a legacy console code page must not crash them
        if hasattr(stream, 'reconfigure'):
            stream.reconfigure(encoding='utf-8', errors='replace')
    spec = json.loads(SPEC.read_text(encoding='utf-8'))
    checks = Checks()
    blocks, idx = generate(spec, checks)
    doc_path = REPO / spec['Document']
    raw = doc_path.read_bytes()
    checks.require(b'\r' not in raw, f'{spec["Document"]}: contains CR bytes')
    current = raw.decode('utf-8')
    text = replace_block(current, 'CAL index', idx, checks)
    for rid, lines in blocks.items():
        text = replace_block(text, rid, lines, checks)
    if args.release:
        checks.require(DRAFT not in current, f'{spec["Document"]}: prose still marked {DRAFT}')
    if checks.failures:
        for f in checks.failures:
            print('FAIL', f)
        print(f'{len(checks.failures)} check(s) failed; nothing written')
        return 1
    if args.check or args.release:
        if text != current:
            a, b = current.split('\n'), text.split('\n')
            first = next((i for i in range(min(len(a), len(b))) if a[i] != b[i]), min(len(a), len(b)))
            print(f'{spec["Document"]} is out of date (first difference at line {first + 1}):')
            print('  committed: ' + (a[first] if first < len(a) else '<end>'))
            print('  generated: ' + (b[first] if first < len(b) else '<end>'))
            print(f'rebuild with: {GENERATOR}')
            return 1
        print(f'{spec["Document"]}: {len(blocks)} records up to date; all checks passed')
        return 0
    if text != current:
        with open(doc_path, 'w', encoding='utf-8', newline='\n') as fh:
            fh.write(text)
        print(f'{spec["Document"]}: rewritten ({len(blocks)} records); all checks passed')
    else:
        print(f'{spec["Document"]}: already up to date; all checks passed')
    return 0


if __name__ == '__main__':
    sys.exit(main())
