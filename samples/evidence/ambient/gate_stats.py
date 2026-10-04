"""Shared helpers for the TODO-30 gate study scripts: exact binomial limits (SciPy beta quantiles) and run loading."""
import json
import pathlib

from scipy.stats import beta


def cp_upper(k, n, confidence=0.95):
    """One-sided Clopper-Pearson upper limit (exact, via the beta quantile)."""
    return 1.0 if k >= n else float(beta.ppf(confidence, k + 1, n - k))


def cp_lower(k, n, confidence=0.95):
    """One-sided Clopper-Pearson lower limit (exact, via the beta quantile)."""
    return 0.0 if k <= 0 else float(beta.ppf(1 - confidence, k, n - k + 1))


def load_runs(runs_dir, family, seeds):
    """Every seed's ambient-gate.json; refuses a missing, failed or wrong-phase run (no silent subset)."""
    out = {}
    for seed in seeds:
        run = pathlib.Path(runs_dir) / 'runs' / family / str(seed)
        done = run / 'done.json'
        if not done.exists():
            raise SystemExit(f'{family} seed {seed}: missing run')
        record = json.loads(done.read_text(encoding='utf-8'))
        if record['exit'] != 0:
            raise SystemExit(f'{family} seed {seed}: failed run (exit {record["exit"]})')
        out[seed] = json.loads((run / 'ambient-gate.json').read_text(encoding='utf-8'))
    return out


def family_seeds(manifest_path, seeds_path, family):
    manifest = json.loads(pathlib.Path(manifest_path).read_text(encoding='utf-8'))
    seeds = json.loads(pathlib.Path(seeds_path).read_text(encoding='utf-8'))
    fam = next(f for f in manifest['families'] if f['id'] == family)
    first = fam.get('seed_offset', 0)
    return seeds[fam['seeds']][first:first + fam['n']], fam
