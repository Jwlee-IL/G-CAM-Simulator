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

## Run provenance (schema version 1)

New runs bind each seed to the source snapshot, actual executable closure, recipe and output bytes. A source record
contains the full commit, scoped tracked binary-diff SHA-256 and untracked-file content hashes. Dirty trees are
allowed and displayed as dirty; these source observations do not prove that an assembly was built from that tree.
Execution identity hashes the output directory actually launched, the driver/helper code and applicable Python/RTL
scripts. The driver records Python, effective pinned .NET runtime, SDK observation, and NumPy/Icarus/vvp versions
where applicable. CLI and probe identities are separate; RTL modes have separate typed closures.

Before launching workers, the driver captures the managed output directories and a conservative repository-local
script/data closure. Each seed executes in a fresh `attempts/<id>/` directory, using its own `snapshot/` copy:
managed assemblies, local Python modules, RTL sources/testbenches and sample inputs. No child reads these inputs
from the live repository. Installed interpreters, runtime libraries and simulators remain external toolchain
dependencies whose versions are recorded; repository edits and rebuilds cannot change the staged inputs of a run.
Attempt folders are retained for inspection. The driver pins the .NET runtime explicitly with `--fx-version`.

`done.json.Provenance` holds versioned source records plus the seed/family, common recipe digest and output hashes.
The root `run-info.json` indexes source records and records invocation details; a later invocation preserves source
records used by completed seeds. Reuse validates the root binding, output bytes, execution identity and recipe.
Legacy seeds without provenance, corrupt outputs or a changed executable/recipe are refused before the invocation
index is rewritten. Use a fresh output root, or explicitly `--force` to execute again; never label old numbers with
today's build. If a prior attempt left extra unbound numerical files at the stable output paths, use a fresh root.

Every downstream aggregator/selector validates the consumed per-seed provenance before writing. Execution conflicts
within one executor type, recipe conflicts within one family, missing metadata and conflicting duplicate seeds
across roots fail closed. There is no `--allow-mixed`. Invocation time, worker count and output location do not
enter identity. Summaries record analysis-time script/helper hashes, applicable tool versions and semantic options.
The bias-baseline writer validates and inherits both input summaries, rather than consulting current root metadata.

JSON summaries embed a `Provenance` object. Future `aggregate.py --json` output is `{Provenance, Metrics}`;
future `results/values.json` is `{Provenance, Values}`. Metric payloads and CSV columns retain their existing meaning.
Each output receives `<filename>.provenance.json`, containing schema version, provenance and SHA-256 bindings for
every member of its output set. `BaseParents` locates a shared logical base for sets spanning output directories;
member paths are relative, with no machine paths. CSV provenance lives in this sidecar. All members are serialized
and validated before writing, and sidecars are written last. An interrupted write produces an invalid set, not a
valid partial ensemble; consumers must verify the binding. Pinned calibration JSONs already have byte hashes and
can read embedded provenance directly.

Existing evidence and four calibration records remain unchanged. The calibration generator prints new selection
and validation provenance separately; legacy files retain their exact "not recorded" line. Malformed new provenance
fails. New threshold/summary bytes require deliberate updates to their downstream pins when adopted as evidence.
Direct generators and curated reports are outside this pipeline; there is no backfill/provenance-only mode.

Standard-library checks:

```bash
python -B -m unittest discover -s samples/evidence/tests -p "test_provenance*.py"
python -B samples/evidence/calibration_record.py --release
```

The isolated temporary-Git test is enabled in CI with `GCAM_PROVENANCE_GIT_TESTS=1`. It initializes and commits a
disposable fixture; under the implementer's no-Git-mutation guard it needs explicit local approval. Other tests use
retained scratch fixtures under the temporary `gcam-todo37` directory and require no build or scientific packages.
