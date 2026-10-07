"""Aggregate the seed runs of run_seeds.py into the per-metric summaries the evidence quotes come from (TODO-27).

    python samples/evidence/aggregate.py --runs <out> [<out> ...] [--family ID ...] [--write-results]

Before anything is joined it checks every declared (family, seed) of manifest.json: the run must exist, have
done.json with exit 0, and produce the same CSV columns and row labels as the family's other seeds. Any
missing, failed or schema-mismatched run stops the script — a partial ensemble is never summarised.

Metric keys read `<family>/<csv file>/<row label>/<column>` for CSV output, or `<family>/<name>` for values parsed
from stdout or derived across columns. For each key: N, the seed-12345 value, mean ± sample SD, median and
quartiles (inclusive method), min / max, the SD of the first and second half of the seed list, and — for keys
with at most 8 distinct values — the frequency of each value (picks, gates). N can be below the family's n
only where the quantity is undefined for some seeds (an RTL fit without enough peaks, a FOV gate never met);
such keys are conditional and keep their own denominator.

--write-results writes results/aggregate.csv, results/aggregate_discrete.csv and results/values.json (per-seed
values of the quoted keys) next to this script.
"""
import argparse
import collections
import csv
import json
import math
import pathlib
import re
import statistics
import sys
import io

import provenance as pv

HERE = pathlib.Path(__file__).resolve().parent

# CSV files the CLI writes, and the columns that label a row.
CSV_KEYS = {'align.csv': ['dof', 'magnitude'], 'antimask.csv': ['scenario', 'bg_per_pixel'], 'array.csv': ['pixels'],
            'background_gradient.csv': ['bsr'], 'background_sweep.csv': ['bsr'], 'cascade.csv': ['distance_mm'],
            'compton_strategies.csv': ['strategy'], 'deadtime.csv': ['true_cps'], 'defects.csv': ['bad_pixel_pct'],
            'depth.csv': ['true_s_mm', 'assumed_s_mm'], 'depth_joint.csv': ['scenario', 'counts'],
            'depth3d.csv': ['method', 'scenario', 'counts'], 'depthdesign.csv': ['distance_mm'],
            'doi.csv': ['source_x_mm', 'thickness_mm'], 'dose_overrange.csv': ['usv_per_h'],
            'dose_ratio.csv': ['label', 'energy_kev', 'angle_deg'], 'maskfab.csv': ['sigma_um'],
            'masktaper.csv': ['taper_deg'], 'mlem.csv': ['separation_mm'], 'noise.csv': ['source', 'detected_counts'],
            'nonprop.csv': ['energy_keV', 'crystal'], 'shield.csv': ['bg', 'thickness_mm'], 'subcell.csv': ['step_mm'],
            'thermal_off.csv': ['time'], 'thermal_on.csv': ['time'], 'thickness.csv': ['thickness_mm'],
            'uniformity.csv': ['level']}

# Keys whose per-seed values are written to results/values.json (the ones the documents quote).
QUOTED = [
    'sweep/noncyclic', 'sweep/cyclic', 'sweep_head/noncyclic', 'sweep_head/cyclic',
    'precise/scenario/error', 'precise/scenario_offaxis/x', 'precise/scenario_ghost/x', 'precise/scenario_handheld/margin',
    'precise/scenario_handheld/eff', 'precise/scenario_orig_gagg/eff', 'precise/scenario_handheld_ir192/eff',
    'precise/scenario_ir192/error', 'precise/scenario_ir192/margin', 'precise/scenario/margin', 'head/ratio',
    'headir/analytic_ratio', 'bias/ratio', 'bias/biased', 'bias/isotropic',
    'noise/noise.csv/centered/25/failure_rate', 'noise/noise.csv/centered/50/rms_error_mm',
    'noise/noise.csv/centered/100/rms_error_mm', 'noise/noise.csv/centered/250/rms_error_mm', 'noise/submm/100',
    'noise_head/noise.csv/centered/250/rms_error_mm', 'noise_head/noise.csv/centered/5000/rms_error_mm',
    'noise_head/noise.csv/edge_8mm/500/rms_error_mm', 'noise_head/noise.csv/edge_8mm/5000/rms_error_mm',
    'noise_orig/noise.csv/centered/5000/rms_error_mm',
    'thickness_wide/best', 'thickness/best', 'array/rms16_over12', 'array/array.csv/6/fail_rate',
    'masktaper/masktaper.csv/0.0/edge_ratio', 'masktaper/masktaper.csv/4.0/edge_ratio', 'masktaper/gain4',
    'masktaper/masktaper.csv/4.0/rms_edge_mm', 'subcell/subcell.csv/1.80/rms_none', 'subcell/subcell.csv/1.80/rms_tent',
    'subcell/subcell.csv/2.40/rms_none', 'subcell/subcell.csv/2.40/rms_tent',
    'antimask/max_method_gap', 'background/background_sweep.csv/2.000/rms_mm', 'background/background_sweep.csv/4.000/rms_mm',
    'background/background_sweep.csv/2.000/fail_rate', 'background/background_sweep.csv/4.000/fail_rate',
    'compton/compton_strategies.csv/Argmax/rms_mm', 'compton/compton_strategies.csv/PerPixelWindow/rms_mm',
    'compton/compton_strategies.csv/Argmax/fail_rate', 'compton/compton_strategies.csv/PerPixelWindow/fail_rate',
    'compton/contamination_pct', 'mixedstrip/R', 'spatial/2/error0', 'spatial/co2_cs_submm',
    'shield/knee/scattered', 'shield/knee/cs', 'shield/knee/co', 'shield/knee/dir', 'shield/directional_delta',
    'maskfab/maskfab.csv/0/rms_mm', 'maskfab/maskfab.csv/40/rms_mm', 'maskfab/maskfab.csv/80/rms_mm',
    'maskfab/maskfab.csv/160/rms_mm', 'maskfab/maskfab.csv/0/psr', 'maskfab/maskfab.csv/160/psr',
    'defects/defects.csv/0.00/rms_corrected_mm', 'defects/defects.csv/8.00/rms_corrected_mm',
    'defects/defects.csv/8.00/rms_raw_mm', 'thermal/thermal_off.csv/1.000/efficiency',
    'thermal/thermal_off.csv/1.000/rms_bias_mm', 'thermal/thermal_on.csv/1.000/rms_bias_mm',
    'cascade/slope', 'cascade/zero_points', 'rtl_frontend/full/fwhm', 'rtl_frontend/cusp/noise_fwhm',
    'rtl_frontend/CR-RC^4/noise_fwhm', 'rtl_frontend/trapezoid/noise_fwhm', 'rtl_material/GAGG/efficiency',
    'rtl_material/CeBr3/efficiency', 'rtl_peak/all/efficiency', 'rtl_pixel/0.15/raw_fwhm', 'rtl_pixel/0.15/cal_fwhm',
    'rtl_multi/cs/fwhm122', 'rtl_multi/low/fwhm122', 'depthsharp/first150_fwhm_fraction', 'depthsharp/power',
    'gap/0.1/relativeZeroGapPPW', 'gap/0.2/relativeZeroGapPPW', 'openfraction/ratio_best', 'dose/frontal_max_abs_error',
]


class Ensemble:
    def __init__(self):
        self.d = collections.defaultdict(dict)

    def add(self, key, seed, value):
        try:
            value = float(value)
        except (TypeError, ValueError):
            return
        if math.isfinite(value):
            self.d[key][seed] = value

    def get(self, key):
        return self.d.get(key, {})


def find_runs(roots, family):
    """{seed: run dir} for a family, first root wins (duplicates across roots are not counted twice)."""
    found = {}
    for root in roots:
        base = pathlib.Path(root) / 'runs' / family
        if base.is_dir():
            for run in base.iterdir():
                if run.name.isdigit():
                    seed = int(run.name)
                    if seed in found:
                        pv.duplicate(found[seed], run)
                    else:
                        found[seed] = run
    return found


def check_complete(manifest, seeds, roots, families, n_override=0):
    """Return {family: [(seed, run dir)]}; stop on any missing, failed or schema-mismatched run."""
    problems, selected = [], {}
    for fam in manifest['families']:
        if families and fam['id'] not in families:
            continue
        first = fam.get('seed_offset', 0)
        declared = seeds[fam['seeds']][first:first + (n_override or fam['n'])]
        runs = find_runs(roots, fam['id'])
        chosen = []
        for seed in declared:
            run = runs.get(seed)
            done = run / 'done.json' if run else None
            if not done or not done.exists():
                problems.append(f'{fam["id"]} seed {seed}: missing')
                continue
            if json.loads(done.read_text(encoding='utf-8-sig'))['exit'] != 0:
                problems.append(f'{fam["id"]} seed {seed}: failed (exit != 0)')
                continue
            chosen.append((seed, run))
        if fam['kind'] == 'cli' and chosen:
            schemas = {}
            for seed, run in chosen:
                sig = []
                for f in sorted((run / 'samples').glob('*.csv')):
                    if f.name in CSV_KEYS:
                        rows = list(csv.DictReader(f.open(encoding='utf-8-sig')))
                        cols = tuple(rows[0].keys()) if rows else ()
                        sig.append((f.name, cols, tuple('/'.join(r[k] for k in CSV_KEYS[f.name]) for r in rows)))
                schemas.setdefault(tuple(sig), []).append(seed)
            if len(schemas) > 1:
                problems.append(f'{fam["id"]}: CSV schema differs between seeds ({len(schemas)} variants)')
        selected[fam['id']] = (fam, chosen)
    if problems:
        print('\n'.join(problems[:40]), file=sys.stderr)
        raise SystemExit(f'refusing to aggregate: {len(problems)} missing / failed / inconsistent runs')
    return selected


def text(path):
    return path.read_text(encoding='utf-8-sig') if path.exists() else ''


def parse_cli(E, tag, seed, run):
    for f in (run / 'samples').glob('*.csv'):
        if f.name not in CSV_KEYS:
            continue
        keys = CSV_KEYS[f.name]
        for row in csv.DictReader(f.open(encoding='utf-8-sig')):
            ident = '/'.join(row[k] for k in keys)
            for field, value in row.items():
                if field not in keys:
                    E.add(f'{tag}/{f.name}/{ident}/{field}', seed, value)
    if tag == 'depth':
        rows = list(csv.DictReader((run / 'samples/depth.csv').open(encoding='utf-8-sig')))
        for true in sorted({r['true_s_mm'] for r in rows}):
            sub = [r for r in rows if r['true_s_mm'] == true]
            fs = [float(r['focus']) for r in sub]
            lo, hi = min(fs), max(fs)
            # the plot's width: assumed distances whose min/max-normalised focus is above one half
            above = [float(r['assumed_s_mm']) for r in sub if (float(r['focus']) - lo) / (hi - lo + 1e-9) > 0.5]
            E.add('depth/estimate/' + true, seed, sub[0]['estimated_s_mm'])
            E.add('depth/plot_width/' + true, seed, max(above) - min(above) if len(above) > 1 else 0)
    out = text(run / 'stdout.txt')
    if tag.startswith('sweep'):
        m = re.findall(r'(\d+)/625 points localized', out)
        if len(m) == 2:
            E.add(tag + '/cyclic', seed, m[0])
            E.add(tag + '/noncyclic', seed, m[1])
    if tag == 'mlem':
        for i, line in enumerate(out.splitlines()):
            if 'FWHM' in line or 'min ' in line or 'bias ' in line:
                for k, v in enumerate(re.findall(r'(?<![\w])[-+]?\d+\.?\d*(?:[eE][-+]?\d+)?', line)):
                    E.add(f'mlem/stdout/line{i}/n{k}', seed, v)
    if tag == 'compton':
        m = re.search(r'fraction in the 662 window: (\d+)%', out)
        if m:
            E.add('compton/contamination_pct', seed, m[1])
    if tag == 'mixedstrip':
        m = re.search(r'Calibrated R .* = ([\d.]+)', out)
        if m:
            E.add('mixedstrip/R', seed, m[1])
        for label in ['separated', 'co-located']:
            line = next((l for l in out.splitlines() if l.strip().startswith(label + ' ')), None)
            if line:
                for k, v in zip(['true', 'raw', 'stripped', 'raw_error_pct', 'stripped_error_pct'],
                                re.findall(r'[-+]?\d+\.?\d*', line)):
                    E.add(f'mixedstrip/{label}/{k}', seed, v)
    if tag == 'compton-strip':
        for label in ['separated', 'co-located']:
            line = next((l for l in out.splitlines() if l.strip().startswith(label + ' ')), None)
            if line:
                for k, v in enumerate(re.findall(r'[-+]?\d+\.?\d*', line)):
                    E.add(f'compton-strip/{label}/n{k}', seed, v)
        m = re.search(r'R\s*=\s*([\d.]+)', out)
        if m:
            E.add('compton-strip/R', seed, m[1])
    if tag in ('mixedfield', 'mixediso'):
        for i, line in enumerate(out.splitlines()):
            if re.search(r'\)\s+\(', line):
                E.add(f'{tag}/match{i}/error', seed, re.findall(r'[-+]?\d+\.?\d*', line)[-1])
    if tag == 'cascade':
        m = re.search(r'slope[^\d]*([\d.]+)', out, re.I)
        if m:
            E.add('cascade/slope', seed, m[1])
    if tag.startswith('thickness'):
        m = re.search(r'BEST thickness: ([\d.]+)', out)
        if m:
            E.add(tag + '/best', seed, m[1])
    if tag == 'shield':
        for bg, v in zip(['scattered', 'cs', 'co'], re.findall(r'useful thickness ~ ([\d.]+)', out)):
            E.add('shield/knee/' + bg, seed, v)
        m = re.search(r'uniform ~([\d.]+) mm\s+vs\s+directional ~([\d.]+)', out)
        if m:
            E.add('shield/knee/dir', seed, m[2])


def parse_fov(E, seed, out):
    groups = collections.defaultdict(list)
    for row in csv.DictReader(out.splitlines()):
        ident = '/'.join(row[k] for k in ['distance_mm', 'direction_deg', 'onaxis_counts', 'bsr'])
        groups[ident].append({k: float(v) for k, v in row.items()})
    for ident, rows in groups.items():
        rows.sort(key=lambda r: r['angle_deg'])
        for field in ['loc_noncyclic', 'loc_cyclic']:
            last = 0          # usable half-field: the last angle before the ≥ 90 % success fraction first fails
            for row in rows:
                if row[field] < 0.9:
                    break
                last = row['angle_deg']
            E.add(f'fov/{ident}/{field}', seed, last)
        for field, gate in [('side_centroid', 0.95), ('outside_centroid', 0.9)]:
            ok = [r['angle_deg'] for r in rows if r['angle_deg'] > 0 and r[field] >= gate]
            E.add(f'fov/{ident}/{field}_any', seed, bool(ok))
            if ok:
                # first contiguous run past the start (past the fully coded field for the outside flag)
                contiguous, started = [], False
                d = math.radians(rows[0]['direction_deg'])
                fc = math.degrees(math.atan(3.5 / 55 / max(abs(math.cos(d)), abs(math.sin(d)))))
                for row in rows:
                    if row['angle_deg'] <= (fc if field == 'outside_centroid' else 0):
                        continue
                    if row[field] >= gate:
                        started = True
                        contiguous.append(row['angle_deg'])
                    elif started:
                        break
                ok = contiguous
            if ok:
                E.add(f'fov/{ident}/{field}_from', seed, min(ok))
                E.add(f'fov/{ident}/{field}_to', seed, max(ok))
        E.add(f'fov/{ident}/outside_centroid_max', seed, max(r['outside_centroid'] for r in rows))
        for r in rows:
            if r['angle_deg'] == 9.5:
                E.add(f'fov/{ident}/outside_centroid_at9.5', seed, r['outside_centroid'])
        for field in ['false_infield', 'false_infield_unflagged']:
            E.add(f'fov/{ident}/{field}_max', seed, max(r[field] for r in rows))
            for lo, hi in [(5, 12), (7.5, 11), (7.5, 12), (14, 20)]:
                values = [r[field] for r in rows if lo <= r['angle_deg'] <= hi]
                E.add(f'fov/{ident}/{field}_{lo}_{hi}_min', seed, min(values))
                E.add(f'fov/{ident}/{field}_{lo}_{hi}_max', seed, max(values))


def parse_probe(E, mode, seed, run):
    out = text(run / 'stdout.txt')
    if mode in ('precise', 'gap'):
        key = 'fn' if mode == 'precise' else 'gap'
        for row in json.loads(out):
            for k, v in row.items():
                if k != key:
                    E.add(f'{mode}/{row[key]}/{k}', seed, v)
    elif mode == 'bias':
        for k, v in json.loads(out).items():
            E.add('bias/' + k, seed, v)
    elif mode == 'spatial':
        for row in json.loads(out):
            for i, m in enumerate(row['matches']):
                E.add(f"spatial/{row['ratio']}/error{i}", seed, m['ErrorMm'])
    elif mode == 'depthsharp':
        j = json.loads(out)
        for k in ['range10', 'range20']:
            E.add('depthsharp/' + k, seed, j[k])
        for row in j['rows']:
            E.add(f"depthsharp/{row['DistanceMm']}/fwhm", seed, row['DepthFwhmMm'])
    elif mode in ('scan', 'scanextra'):
        parts = out.split('SECOND') if mode == 'scanextra' else [out]
        for index, part in enumerate(parts):
            for row in csv.DictReader(part.strip().splitlines()):
                ident = '/'.join(row[k] for k in ['rank', 'cell_pitch_mm', 'dist_mm'])
                prefix = 'scan/' if mode == 'scan' else f'scanextra/{index}/'
                for k, v in row.items():
                    if k not in ['rank', 'cell_pitch_mm', 'dist_mm']:
                        E.add(prefix + ident + '/' + k, seed, v)
    elif mode == 'fov':
        parse_fov(E, seed, out)
    elif mode == 'materials':
        for row in json.loads(out):
            E.add(f"mat_{row['name']}/eff", seed, row['eff'])
    elif mode == 'viewer':
        for row in json.loads(out):
            ident = f"{row['z']}/{row['angle_mrad']}/{row['channel']}"
            for k in ['estimate', 'bias', 'edge', 'counts']:
                E.add(f'viewer/manual/{ident}/{k}', seed, row[k])
            for k, v in row['current'].items():
                if k == 'Interval':
                    for field, val in v.items():
                        E.add(f'viewer/current/{ident}/{field}', seed, val)
                else:
                    E.add(f'viewer/current/{ident}/{k}', seed, v)


def parse_rtl(E, family, seed, run):
    result = json.loads(text(run / 'result.json'))
    for row in result['rows']:
        if family == 'openfraction':
            for k, v in row.items():
                E.add('openfraction/' + k, seed, v)
            continue
        ident = str(row.get('name', row.get('gain', row.get('sigma', 'all'))))
        for k, v in row.items():
            if k not in ['name', 'gain', 'sigma']:
                E.add(f'{family}/{ident}/{k}', seed, v)


def derive(E):
    D = E.d
    add = E.add
    for k, vs in list(D.items()):
        if k.startswith('align/align.csv/') and k.endswith('/bias_x_mm'):
            base = 'align/align.csv/offset_x/0.000/'
            for seed, x in vs.items():
                y = D[k.replace('bias_x_mm', 'bias_y_mm')][seed]
                add(k.replace('bias_x_mm', 'increment_mm'), seed,
                    math.hypot(x - D[base + 'bias_x_mm'][seed], y - D[base + 'bias_y_mm'][seed]))
    for seed, z in E.get('gap/0/eff').items():
        for g in ['0', '0.02', '0.04', '0.1', '0.2']:
            if seed in E.get(f'gap/{g}/eff'):
                add(f'gap/{g}/relativeZeroGapPPW', seed, D[f'gap/{g}/eff'][seed] / z)
    for seed in E.get('antimask/antimask.csv/bg=0 (ideal)/0.00/calib_rms_mm'):
        gaps = [abs(vs[seed] - D[k.replace('calib_rms_mm', 'antimask_rms_mm')][seed]) for k, vs in list(D.items())
                if k.startswith('antimask/antimask.csv/') and k.endswith('/calib_rms_mm') and seed in vs]
        if gaps:
            add('antimask/max_method_gap', seed, max(gaps))
    for seed, x in E.get('precise/scenario_handheld_ir192/eff').items():
        add('headir/analytic_ratio', seed, x / 2.44e-4)   # URS §5 analytic estimate for the hand-held head
    distances = ['18.0', '24.0', '32.0', '43.0', '57.0', '76.0', '100.0']
    for seed in list(E.get('cascade/slope')):
        nonzero = zeros = 0
        for dist in distances:
            x = E.get(f'cascade/cascade.csv/{dist}/single_photopeak_per_decay').get(seed)
            y = E.get(f'cascade/cascade.csv/{dist}/sum_peak_per_decay').get(seed)
            zeros += y == 0
            nonzero += bool(x and y)
        add('cascade/nonzero_points', seed, nonzero)
        add('cascade/zero_points', seed, zeros)
    for seed in list(E.get('noise/noise.csv/centered/50/rms_error_mm')):
        for n in ['50', '100', '250']:
            v = E.get(f'noise/noise.csv/centered/{n}/rms_error_mm').get(seed)
            if v is not None:
                add('noise/submm/' + n, seed, int(v < 1))
    for seed, v in E.get('array/array.csv/12/rms_mm').items():
        if seed in E.get('array/array.csv/16/rms_mm'):
            ratio = D['array/array.csv/16/rms_mm'][seed] / v
            add('array/rms16_over12', seed, ratio)
            add('array/16_better_than12', seed, ratio < 1)
    for seed, v in E.get('masktaper/masktaper.csv/0.0/eff_center').items():
        if seed in E.get('masktaper/masktaper.csv/4.0/eff_center'):
            add('masktaper/gain4', seed, D['masktaper/masktaper.csv/4.0/eff_center'][seed] / v - 1)
    for seed in list(E.get('depthsharp/150/fwhm')):
        pts = [(math.log(float(z)), math.log(E.get(f'depthsharp/{z}/fwhm')[seed]))
               for z in ['150', '250', '400', '600', '900', '1400'] if E.get(f'depthsharp/{z}/fwhm').get(seed, 0) > 0]
        if len(pts) == 6:
            xm = statistics.mean(p[0] for p in pts)
            ym = statistics.mean(p[1] for p in pts)
            add('depthsharp/power', seed, sum((x - xm) * (y - ym) for x, y in pts) / sum((x - xm) ** 2 for x, _ in pts))
        x = D['depthsharp/150/fwhm'][seed]
        add('depthsharp/first150_fwhm_fraction', seed, x / 150)
        add('depthsharp/first150_pass10', seed, x / 150 <= 0.2)
    for seed, b in E.get('maskfab/maskfab.csv/0/rms_mm').items():
        for sigma in ['10', '20', '40', '80', '160']:
            v = E.get(f'maskfab/maskfab.csv/{sigma}/rms_mm').get(seed)
            if v is not None:
                add('maskfab/within2/' + sigma, seed, v <= 2 * b)
                add('maskfab/relativeRms/' + sigma, seed, v / b)
    for seed in list(E.get('dose/dose_ratio.csv/check/70.0/0.0/ratio')):
        errs = [abs(vs[seed] - 1) for k, vs in list(D.items())
                if k.startswith('dose/dose_ratio.csv/') and '/0.0/ratio' in k and seed in vs]
        if errs:
            add('dose/frontal_max_abs_error', seed, max(errs))
            add('dose/frontal_within13', seed, max(errs) <= 0.13)
    for seed, v in E.get('spatial/2/error0').items():
        add('spatial/co2_cs_submm', seed, v < 1)
    for seed in E.get('thickness/best'):
        vals = {k.split('/')[2]: vs[seed] for k, vs in list(D.items())
                if k.startswith('thickness/thickness.csv/') and k.endswith('/usable_radius_mm') and seed in vs}
        for t, v in vals.items():
            add('thickness/maxradius_member/' + t, seed, v == max(vals.values()))
    for seed, x in E.get('shield/knee/scattered').items():
        if seed in E.get('shield/knee/dir'):
            add('shield/directional_delta', seed, D['shield/knee/dir'][seed] - x)
    for bg, name in [('scattered_250keV', 'scattered'), ('Cs137_662keV', 'cs'), ('Co60_1250keV', 'co')]:
        for seed, t in E.get('shield/knee/' + name).items():
            key = f'shield/shield.csv/{bg}/{t:.1f}/shield_kg'
            if seed in E.get(key):
                add('shield/picked_mass/' + name, seed, D[key][seed])
    for seed, x in E.get('rtl_frontend/full/fwhm').items():
        if seed in E.get('rtl_frontend/full_no_adc_noise/fwhm'):
            add('rtl_frontend/adc_delta', seed, x - D['rtl_frontend/full_no_adc_noise/fwhm'][seed])
    for seed, x in E.get('rtl_frontend/cusp/noise_fwhm').items():
        if seed in E.get('rtl_frontend/CR-RC^4/noise_fwhm'):
            add('rtl_frontend/cusp_beats_crrc', seed, x < D['rtl_frontend/CR-RC^4/noise_fwhm'][seed])
    for k, vs in list(D.items()):
        if k.startswith('scan/') and k.endswith('/usable_fov_area_mm2'):
            for seed, x in vs.items():
                add(k.replace('usable_fov_area_mm2', 'usable_half_mm'), seed, math.sqrt(x) / 2)
        if k.startswith('scan/11/') and k.endswith('/usable_fraction'):
            for seed, x in vs.items():
                add('scan/r11/pass90', seed, int(x >= 0.9))
        if k.startswith('thickness') and '/thickness.csv/' in k and k.endswith('/eff_edge'):
            other = k.replace('/eff_edge', '/eff_center')
            for seed, x in vs.items():
                if seed in E.get(other):
                    add(k.replace('eff_edge', 'edge_ratio'), seed, x / D[other][seed])
    for seed, x in E.get('precise/scenario_handheld/eff').items():
        if seed in E.get('precise/scenario_orig_gagg/eff'):
            add('head/ratio', seed, x / D['precise/scenario_orig_gagg/eff'][seed])
    for seed, x in E.get('precise/scenario_handheld_ir192/eff').items():
        if seed in E.get('precise/scenario_handheld/eff'):
            add('head/ir_ratio', seed, x / D['precise/scenario_handheld/eff'][seed])


def summarise(E, order_of):
    def sd(xs):
        return statistics.stdev(xs) if len(xs) > 1 else 0.0

    rows = []
    for key, vs in sorted(E.d.items()):
        order = order_of(key)
        seeds = sorted(vs, key=lambda s: order.index(s) if s in order else len(order) + s)
        x = [vs[s] for s in seeds]
        n = len(x)
        if not n:
            continue
        q = statistics.quantiles(x, n=4, method='inclusive') if n > 1 else [x[0]] * 3
        half = n // 2
        row = {'key': key, 'N': n, 'seed12345': vs.get(12345), 'mean': statistics.mean(x), 'sd': sd(x),
               'median': statistics.median(x), 'q25': q[0], 'q75': q[2], 'min': min(x), 'max': max(x),
               'sd_first': sd(x[:half]), 'sd_second': sd(x[half:])}
        distinct = collections.Counter(x)
        if len(distinct) <= 8:
            row['frequencies'] = {format(k, 'g'): c for k, c in sorted(distinct.items())}
        rows.append((row, {s: vs[s] for s in seeds}))
    return rows


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--runs', nargs='+', required=True, help='output roots of run_seeds.py (first root wins per seed)')
    ap.add_argument('--family', nargs='*', default=[])
    ap.add_argument('--n', type=int, default=0, help='check and summarise only the first N declared seeds (smoke tests)')
    ap.add_argument('--write-results', action='store_true')
    ap.add_argument('--json', default=None, help='also write the full summary (with per-seed values) here')
    args = ap.parse_args()
    manifest = json.loads((HERE / 'manifest.json').read_text(encoding='utf-8'))
    seeds = json.loads((HERE / 'seeds.json').read_text(encoding='utf-8'))
    if args.n and args.write_results:
        raise SystemExit('--n is for smoke tests; results/ is only written from complete ensembles')
    selected = check_complete(manifest, seeds, args.runs, set(args.family), args.n)
    prov = pv.summary_provenance([pv.from_selected(selected)], __file__, args)

    E = Ensemble()
    family_of_prefix = {}
    for fid, (fam, chosen) in selected.items():
        for seed, run in chosen:
            if fam['kind'] == 'cli':
                parse_cli(E, fid, seed, run)
            elif fam['kind'] == 'probe':
                parse_probe(E, fam['mode'], seed, run)
            else:
                parse_rtl(E, fid, seed, run)
        family_of_prefix[fid] = seeds[fam['seeds']]
    derive(E)
    def order_of(key):
        """The seed order of the family a key came from (sets the first / second half for sd_first / sd_second)."""
        first = key.split('/')[0]
        if first in ('head', 'headir'):
            first = 'precise'
        elif first.startswith('mat_'):
            first = 'materials'
        return family_of_prefix.get(first, seeds['O128'])

    rows = summarise(E, order_of)
    n_declared = {fid: len(chosen) for fid, (_, chosen) in selected.items()}
    print(f'{len(rows)} metric keys from {sum(n_declared.values())} runs in {len(n_declared)} families')
    outputs = {}
    if args.json:
        outputs[pathlib.Path(args.json)] = pv.json_bytes({'Provenance': prov, 'Metrics': [dict(r, values=v) for r, v in rows]})
    if args.write_results:
        res = HERE / 'results'
        cols = ['key', 'N', 'seed12345', 'mean', 'sd', 'median', 'q25', 'q75', 'min', 'max', 'sd_first', 'sd_second']

        def fmt(v):
            return '' if v is None else (format(v, '.6g') if isinstance(v, float) else v)

        f = io.StringIO(newline='')
        w = csv.writer(f, lineterminator='\n')
        w.writerow(cols)
        for r, _ in rows:
            w.writerow([fmt(r[c]) for c in cols])
        outputs[res / 'aggregate.csv'] = f.getvalue().encode('utf-8')
        f = io.StringIO(newline='')
        w = csv.writer(f, lineterminator='\n')
        w.writerow(['key', 'N', 'value', 'count'])
        for r, _ in rows:
            for value, count in r.get('frequencies', {}).items():
                w.writerow([r['key'], r['N'], value, count])
        outputs[res / 'aggregate_discrete.csv'] = f.getvalue().encode('utf-8')
        quoted = {r['key']: {str(s): v for s, v in vals.items()} for r, vals in rows if r['key'] in QUOTED}
        missing = [k for k in QUOTED if k not in quoted]
        if missing and not args.family:
            raise SystemExit(f'quoted keys absent from the ensemble: {missing}')
        outputs[res / 'values.json'] = pv.json_bytes({'Provenance': prov, 'Values': quoted})
        print(f'wrote {res}')
    if outputs:
        pv.publish(outputs, prov)
    return 0


if __name__ == '__main__':
    sys.exit(main())
