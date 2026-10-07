# samples/evidence — seed ensembles behind the evidence quotes

Every Monte Carlo number in [VV.Gcam.Evidence](../../docs/VV.Gcam.Evidence.md) is quoted over N outer seeds
(§1 there, "How MC numbers are quoted"). This folder holds what is needed to repeat those ensembles and the summaries
the quotes were taken from. Background and decisions: [AGENTS.Findings theme 63](../../docs/AGENTS.Findings.Part2.md#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02).

| File | What |
|---|---|
| `seeds.json` | the ordered seed lists (`O128`, `F128` for the field-of-view study, and the two RTL lists) and how they were built |
| `manifest.json` | one entry per study family: kind (`cli` / `probe` / `rtl`), command or mode, scenario, seed list, N, overrides; the scenarios' SHA-256, the patterns each study keeps fixed, the engine commit |
| `run_seeds.py` | the driver: one isolated working directory per (family, seed), a complete clone of the scenario with only `Seed` (and declared overrides) changed, stdout / stderr / exit / timing recorded |
| `aggregate.py` | parses the runs into per-metric summaries; refuses missing, failed or schema-mismatched runs |
| `cascade_fit.py` | the pooled Poisson fit of the Co-60 cascade sum-peak yield and the slope the geometry implies |
| `rtl_seeds.py` | the Python / RTL studies (`rtl/`, `samples/open_fraction_study.py`) with their own generators and fixed fixtures, seed injected, no plots |
| `calibration-records.json`, `calibration_record.py` | the calibration records of `docs/VV.Gcam.Calibration.md`: the spec pins every evidence file a record reads (SHA-256 of the git bytes); the generator rewrites the records' generated blocks, cross-checks seeds, pins and stored verdicts, and with `--check` / `--release` fails on any difference or unsigned draft (CI runs `--release`) |
| `probe/` | `Gcam.EvidenceProbe`, the headless recipes no CLI command runs (biasing vs 4π, selected scan rows, field of view, crystal gap, activity ratios, sharp-optics depth, material efficiencies, the viewer's list-mode focus) — not part of `Gcam.sln` |
| `results/aggregate.csv` | N, seed-12345 value, mean, SD, median, quartiles, min, max, first / second-half SD per metric key |
| `results/aggregate_discrete.csv` | value frequencies for keys with ≤ 8 distinct values (picks, gates) |
| `results/values.json` | per-seed values of the keys the documents quote |
| `results/cascade_fit.json` | the pooled cascade fit |

```bash
dotnet build Gcam.sln -c Release
dotnet build samples/evidence/probe/Gcam.EvidenceProbe.csproj -c Release
python samples/evidence/run_seeds.py --out <dir> --family maskfab bias        # --n 2 for a smoke test
python samples/evidence/aggregate.py --runs <dir> --family maskfab bias       # --write-results needs every family
python samples/evidence/cascade_fit.py --runs <dir> --out <dir>/cascade_fit.json
```

The `rtl` families need iverilog / vvp on PATH; `rtl_frontend` also runs the Release CLI. Run times per seed range
from about a second (`rtl_material`, `depth`) to over ten minutes (`fov`).
