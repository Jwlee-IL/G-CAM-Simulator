"""Seed-swept runs of the Python / RTL studies behind EV-16, EV-17, EV-18, EV-21 and PAPER §3 (TODO-27).

    python samples/evidence/rtl_seeds.py <mode> <seed> [--cli <Gcam.Cli.dll>]

Run it from an empty working directory (run_seeds.py does): it writes result.json there plus the scratch files the
studies write (adc.txt, peaks.txt, ...). The studies in rtl/ and samples/ are NOT modified: each one is loaded
from its source with its plotting and process side effects (matplotlib, os.chdir, the iverilog call) removed, and
only the generator seed the mode names is replaced. Generators stay what the scripts use — NumPy PCG64 or the
Python stdlib — so these are synthetic-stimulus ensembles, not the C# xoshiro transport ensemble. Fixed fixtures
stay fixed: the pixel study's manufactured gain / decay map (NumPy seed 999) and the multi-isotope study's noise
draw (seed 9).

Modes (seed meaning in brackets):
    material   GAGG at 1 Mcps, CeBr3 at 2 Mcps through the peak-detector RTL (stimulus seed)      EV-21
    peak       the gen_stimulus.py run at 1.5 Mcps, 2000 events through the same RTL (stimulus seed) EV-21
    pixel      25 pixels × 500 events at gain σ 0 and 0.15, raw and calibrated FWHM (pixel i uses seed+i) EV-18
    multi      Cs/Co-60/Co-57 field through the integrating RTL at two gains (field seed)          EV-16
    frontend   `montecarlo eventstream` at the seed, then frontend_study / shaper_compare (timing + noise seed) EV-17
    openfraction  samples/open_fraction_study.py's coding-noise model (NumPy seed)                PAPER §3

Needs iverilog / vvp on PATH for material, peak, pixel and multi (compiled once into the working directory), and
for frontend the Release CLI (`--cli`, default src/Gcam.Cli/bin/Release/net9.0/Gcam.Cli.dll).
"""
import argparse
import ast
import json
import os
import pathlib
import statistics
import subprocess
import sys

import numpy as np

REPO = pathlib.Path(__file__).resolve().parents[2]
RTL = REPO / 'rtl'


def strip_and_exec(path, cut, replacements=()):
    """Execute a study up to `cut` without matplotlib, os.chdir, environment edits or its iverilog call."""
    source = path.read_text(encoding='utf-8')
    source = source[:source.index(cut)]
    for old, new in replacements:
        source = source.replace(old, new)
    tree = ast.parse(source)
    kept = []
    for node in tree.body:
        if isinstance(node, ast.Import) and any(a.name.startswith('matplotlib') for a in node.names):
            continue
        if isinstance(node, ast.Expr) and isinstance(node.value, ast.Call) and \
                ast.unparse(node.value.func) in ('os.chdir', 'matplotlib.use', 'subprocess.run'):
            continue
        if isinstance(node, ast.Assign) and ast.unparse(node.targets[0]).startswith('os.environ'):
            continue
        kept.append(node)
    tree.body = kept
    ns = {'__file__': str(path)}
    exec(compile(tree, path.name, 'exec'), ns)
    return ns


def compile_rtl(name, sources):
    """Compile an Icarus testbench into the working directory once; the studies call `vvp <name>` from here."""
    target = pathlib.Path(name).resolve()
    if not target.exists():
        subprocess.run(['iverilog', '-g2012', '-o', str(target)] + [str(RTL / s) for s in sources], check=True)
    return target.as_posix()


def material(seed):
    vvp = compile_rtl('peak.vvp', ['tb_peak_detector.sv', 'peak_detector.sv'])
    m = strip_and_exec(RTL / 'material_rate_study.py', 'rows = []', [('"sim.vvp"', repr(vvp))])
    rows = []
    for name, rate in [('GAGG', 1e6), ('CeBr3', 2e6)]:
        decay, afterglow = m['MATERIALS'][name]
        wave, truth = m['generate'](rate, decay, afterglow, seed=seed)
        eff, fwhm = m['score'](wave, truth)
        rows.append({'name': name, 'rate': rate, 'efficiency': eff, 'fwhm': fwhm})
    return rows


def peak(seed):
    vvp = compile_rtl('peak.vvp', ['tb_peak_detector.sv', 'peak_detector.sv'])
    m = strip_and_exec(RTL / 'material_rate_study.py', 'rows = []', [('"sim.vvp"', repr(vvp))])
    source = (RTL / 'gen_stimulus.py').read_text(encoding='utf-8')
    source = source[:source.index('np.savetxt')]
    for old, new in [('float(sys.argv[1]) if len(sys.argv) > 1 else 50_000.0', '1500000.0'),
                     ('int(sys.argv[2])   if len(sys.argv) > 2 else 2000', '2000'),
                     ('sys.argv[3]        if len(sys.argv) > 3 else "."', '"."'),
                     ('np.random.default_rng(12345)', f'np.random.default_rng({seed})')]:
        if old not in source:
            raise SystemExit(f'gen_stimulus.py changed: "{old}" not found')
        source = source.replace(old, new)
    ns = {}
    exec(source, ns)
    eff, fwhm = m['score'](ns['adc'], ns['truth'])
    return [{'efficiency': eff, 'fwhm': fwhm}]


def pixel(seed):
    vvp = compile_rtl('peak.vvp', ['tb_peak_detector.sv', 'peak_detector.sv'])
    m = strip_and_exec(RTL / 'pixel_uniformity_study.py', 'rows = []', [('"sim.vvp"', repr(vvp))])
    rows = []
    for sigma in [0, 0.15]:
        fixture = np.random.default_rng(999)          # the manufactured gain / decay map stays fixed
        gains = np.clip(fixture.normal(1, sigma, m['PIXELS']), 0.3, None)
        decays = np.clip(fixture.normal(m['DECAY_MEAN_NS'], m['DECAY_MEAN_NS'] * m['DECAY_SIGMA'], m['PIXELS']), 5, None)
        raw, cal = [], []
        for i in range(m['PIXELS']):
            amps = m['run_pixel'](decays[i], gains[i], seed=seed + i)
            raw.extend(amps / m['ADC_PER_KEV'])
            cal.extend(amps / gains[i] / m['ADC_PER_KEV'])
        rows.append({'sigma': sigma, 'raw_fwhm': m['fwhm_percent'](np.array(raw)),
                     'cal_fwhm': m['fwhm_percent'](np.array(cal))})
    return rows


def multi(seed):
    vvp = compile_rtl('int.vvp', ['tb_integrating.sv', 'integrating_peak_detector.sv'])
    path = RTL / 'multi_isotope_study.py'
    m = strip_and_exec(path, '# --- field:', [('../samples/isotopes/*.json', (REPO / 'samples/isotopes/*.json').as_posix()),
                                               ('"sim_int.vvp"', repr(vvp))])
    for node in ast.parse(path.read_text(encoding='utf-8')).body:
        if isinstance(node, ast.FunctionDef) and node.name == 'peak_fwhm':
            exec(compile(ast.Module(body=[node], type_ignores=[]), path.name, 'exec'), m)
    shape, L = m['shape_for'](m['DECAY_NS'])
    times, dep = m['build_field']({'Cs-137': 80e3, 'Co-60': 60e3, 'Co-57': 90e3}, 0.02, np.random.default_rng(seed))
    rows = []
    for label, gain in [('cs', 3000 / 662), ('low', 3600 / 1332)]:
        cal = m['calib'](gain, shape, L)
        adc = m['waveform'](times, dep, gain, shape, L)
        d = m['run_integrating'](adc)
        kev = d[:, 1] / cal * 662
        accepted = kev[d[:, 2] == 0]
        rows.append({'gain': label, 'fwhm122': m['peak_fwhm'](accepted, 122), 'fwhm662': m['peak_fwhm'](accepted, 662),
                     'events': len(accepted)})
    return rows


def frontend(seed, cli):
    sys.path.insert(0, str(RTL))
    import event_stream
    import trap_ref
    config = json.loads((REPO / 'samples/scenario.json').read_text(encoding='utf-8'))
    key = next((k for k in config if k.lower() == 'seed'), 'seed')
    config[key] = seed
    pathlib.Path('config.json').write_text(json.dumps(config, indent=1), encoding='utf-8')
    res = subprocess.run(['dotnet', cli, 'eventstream', str(pathlib.Path('config.json').resolve())],
                         capture_output=True, text=True, check=True)
    pathlib.Path('cli-stdout.txt').write_text(res.stdout, encoding='utf-8')

    def load(name):
        source = (RTL / name).read_text(encoding='utf-8')
        tree = ast.parse(source[:source.index('def main():')])
        tree.body = [n for n in tree.body
                     if not (isinstance(n, ast.Import) and any(a.name.startswith('matplotlib') for a in n.names))
                     and not (isinstance(n, ast.Expr) and isinstance(n.value, ast.Call)
                              and ast.unparse(n.value.func) == 'matplotlib.use')]
        ns = {}
        exec(compile(tree, name, 'exec'), ns)
        return ns

    f = load('frontend_study.py')
    s = load('shaper_compare.py')
    flat = event_stream.calibrate_flat_per_kev()     # the calibration stays the original fixture
    real_raster = event_stream.rasterize
    base_adc = event_stream.ADC
    adc = base_adc
    event_stream.calibrate_flat_per_kev = lambda **kw: flat

    def raster(*a, **kw):                              # event timing / noise follow the outer seed
        kw['seed'] = seed
        kw['adc'] = adc
        return real_raster(*a, **kw)

    event_stream.rasterize = raster
    energies = [e for _, e in event_stream.read_stream(str(pathlib.Path('rtl/event_stream.txt').resolve()))[0]]
    ev = f['overlay_rate'](energies, 500, seed=seed)
    rows = []
    for name, intrinsic, noise in [('electronic', 0, 3), ('intrinsic', 0.06, 0), ('full', 0.06, 3)]:
        rows.append({'name': name, 'fwhm': f['recover'](ev, trap_ref.RISE, intrinsic, noise)[1]})
    adc = {**base_adc, 'adc_noise_codes': 0}
    rows.append({'name': 'full_no_adc_noise', 'fwhm': f['recover'](ev, trap_ref.RISE, 0.06, 3)[1]})
    adc = base_adc
    e100 = s['overlay'](energies, 100, seed=seed)
    e2000 = s['overlay'](energies, 2000, seed=seed)
    for kind in s['COLORS']:
        rec = s['recover'](kind, e100, 0, isolated=True)
        fwhm = 2.3548 * statistics.pstdev(rec) / statistics.mean(rec) * 100
        rec2 = s['recover'](kind, e2000, 0.06, isolated=False)
        good = sum(abs(v - 661.7) <= 0.05 * 661.7 for v in rec2)
        rows.append({'name': kind, 'noise_fwhm': fwhm, 'within5_pct': 100 * good / len(rec2),
                     'photopeak_events': len(rec2)})
    return rows


def openfraction(seed):
    path = REPO / 'samples/open_fraction_study.py'
    source = path.read_text(encoding='utf-8')
    source = source[:source.index('# One representative reconstruction')]
    for old, new in [('import matplotlib\nmatplotlib.use("Agg")\nimport matplotlib.pyplot as plt\n', ''),
                     ('default_rng(12345)', 'default_rng(SEED)')]:
        if old not in source:
            raise SystemExit(f'open_fraction_study.py changed: "{old[:40]}" not found')
        source = source.replace(old, new)
    scope = {'SEED': seed, '__file__': str(path)}
    exec(compile(source, path.name, 'exec'), scope)
    values = scope['snr_rand']
    mura = float(scope['mura_snr'])
    return [{'mura': mura, 'random_best': float(values.max()), 'random_half': float(values[8]),
             'ratio_best': mura / float(values.max()), 'ratio_half': mura / float(values[8]),
             'best_rho': float(scope['rhos'][values.argmax()])}]


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('mode', choices=['material', 'peak', 'pixel', 'multi', 'frontend', 'openfraction'])
    ap.add_argument('seed', type=int)
    ap.add_argument('--cli', default=str(REPO / 'src/Gcam.Cli/bin/Release/net9.0/Gcam.Cli.dll'))
    args = ap.parse_args()
    os.environ.setdefault('PYTHONUTF8', '1')
    rows = frontend(args.seed, args.cli) if args.mode == 'frontend' else globals()[args.mode](args.seed)
    result = {'seed': args.seed, 'mode': args.mode, 'rows': rows}
    pathlib.Path('result.json').write_text(json.dumps(result), encoding='utf-8')
    print(json.dumps(rows))


if __name__ == '__main__':
    main()
