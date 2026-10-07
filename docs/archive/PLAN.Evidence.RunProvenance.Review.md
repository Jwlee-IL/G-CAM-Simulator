# PLAN.Evidence.RunProvenance.Review — TODO-37 implementer review

Review date: 2026-10-07. Turn 1, review only. No implementation or evidence regeneration.

## At a glance

The provenance gap is real. The reference plan needs correction before implementation. Copying the root's current `run-info.json` cannot identify completed seeds: the driver overwrites that file on every invocation, then skips successful seeds without checking their code or recipe. Hashing the actual executable closure is cheap, and necessary: all six shared engine DLLs currently differ between the CLI and probe output directories. Neither HEAD nor a source diff establishes which build ran.

Recommend versioned per-seed provenance, immutable copies of managed executable inputs, strict validation before reuse or aggregation, and separate identities for execution and subsequent analysis. Preserve existing evidence and calibration blocks byte for byte. Include the omitted derived-summary writer and decide explicitly how CSVs carry provenance. No approval-dependent command was needed or run.

## Verification of "What exists"

| Plan row | Verdict | Evidence |
|---|---|---|
| Driver | Holds, with a material omission | `run_seeds.py:main` writes the stated fields to `<out>/run-info.json`; `jobs` is the number of scheduled jobs, not worker concurrency. `run_one` writes config, stdout, stderr and `done.json` in the isolated seed directory. Its early return trusts only `done.exit == 0`. No per-seed source/build identity exists; the root record is overwritten even if every seed is skipped. Git subprocess failures are not checked. |
| Aggregators | Correct it | The producers and destinations below are verified at their write sites. None reads/copies `run-info.json`. `rtl_seeds.py` is a raw-run producer, not an aggregator. `ambient/bias_baseline.py` is missing from the plan. Several writers use caller-supplied destinations, not fixed committed paths. |
| Record generator | Holds | `calibration_record.py:data_section` unconditionally prints `not recorded; the code landed in ...`, using each record's `CodeLandedIn`. `generate` invokes it for all four records. The spec pins the selection/validation files. The generated section 6 in all four records has that line. |
| Dirty example | Holds for the dirty flag; qualify its historical interpretation | Read `%TEMP%/gcam-todo34/runs/run-info.json`: commit `5977c239cbc9682d871518921fa4352d727ddcb8`, dirty true, family `angres_matched`, start `2026-10-06T15:38:03`. This demonstrates a dirty invocation, but the overwrite/skip behavior means this record alone does not establish provenance of every seed in that root. I did not independently reconstruct that historical source tree. |

`manifest.json` also has `engine_commit` and an `engine_note`; `README.md` mentions this. They describe the historical recipe record, not the executable used by a future invocation. Do not use them as missing run provenance.

## Exact producer inventory

All paths below are relative to `samples/evidence/`, unless stated otherwise. The committed filenames are verified against existing JSON family keys, the calibration spec's reproduction recipes, README, and the turn-7/8/9 reports. An `--out` writer can write any supplied path; a historical rename/copy is not a fixed destination encoded in that script.

| Writer and function | Actual output interface | Corresponding committed files |
|---|---|---|
| `aggregate.py:main` (lines 500–524) | `--write-results`; optional `--json PATH` | `results/aggregate.csv`, `results/aggregate_discrete.csv`, `results/values.json`. Optional full JSON is a list of metric objects, not an object envelope; no fixed committed filename for it. |
| `cascade_fit.py:main`; inputs in `load_rows` | `--out PATH`, optional | `results/cascade_fit.json`. Without `--out`, prints results only. |
| `ambient/aggregate_gate.py:main`; inputs through `gate_stats.load_runs` | `--out PATH`, optional `--csv PATH` | `results/ambient-baseline-v1-turn7-gate.json`, `results/ambient-baseline-v1-turn8-gate.json`; `results/ambient-baseline-v1-turn7-sources.csv`, `results/ambient-baseline-v1-turn8-sources.csv`. Families `gate_validation`, `gate_validation_v2`. |
| `ambient/select_thresholds.py:main`; inputs through `gate_stats.load_runs` | `--out PATH` | `ambient/gate-thresholds-v1.json` (strict), `ambient/gate-thresholds-v2.json` (inclusive). |
| `ambient/aggregate_ev.py:main`; inputs in `load` | `--out PATH` per family | `results/ambient-baseline-v1-turn8-ev01.json` (`ev01_sweep`), `-ev02.json` (`ev02_fov`), `-ev12.json` (`ev12_antimask`), `-ev15.json` (`ev15_separation`); `results/ambient-baseline-v1-turn9-ev02-cs662.json` (`ev02_fov_cs662`), `-ev15a.json` (`ev15_abs_a`), `-ev15b.json` (`ev15_abs_b`). |
| `ambient/select_csco_thresholds.py:main`; inputs in `load` | `--out PATH` | `ambient/csco-thresholds-v1.json`. |
| `ambient/aggregate_csco.py:main`; inputs in `load` | `--out PATH` | `results/csco-v1-validation.json`. Its custom serializer rounds numerical summaries to six significant digits; provenance must bypass numerical rounding. |
| `angres/aggregate_angres.py:main`, `summarise`, `select`, `floor_family`; inputs in `load` | `--out PATH`, modes `--family`, `--select`, `--floor` | `results/angres-v1-1m.json`, `angres-v1-5m.json`, `angres-v1-ambient.json`, `angres-v1-ladder.json`, `angres-v1-matched.json`, `angres-v1-near.json`, `angres-v1-wide.json`; selection `angres-v1-iterations.json` (`angres_select`, `angres_matched_select`); joint selection/validation `angres-v2-floor.json` (`angres_floor_1m`, `angres_floor_5m`, using their two selection families). |
| **Omitted:** `ambient/bias_baseline.py:main` | `--gate PATH`, optional `--sweep PATH`, `--out PATH` | `results/ambient-baseline-v1-turn8-bias.json`. Reads aggregate summaries, not run directories. Must inherit their source lineage and record its own analysis identity. |
| `rtl_seeds.py:main`, `frontend`, study functions | writes to current working directory | **No direct committed summary.** Writes `result.json`, frontend `config.json`/`cli-stdout.txt`, and study/RTL scratch artifacts. Under the driver these are `<out>/runs/<family>/<seed>/...`; `aggregate.py:parse_rtl` reads the result. |
| `run_seeds.py:main`, `run_one` | writes under `--out` | **No direct committed summary.** Root `run-info.json` and per-seed records/config/logs. CLI/probe commands produce the numerical seed files. |
| `calibration_record.py:main` | default writes generated document blocks; `--check`/`--release` do not write | Repository `docs/VV.Gcam.Calibration.md`, not an evidence summary. |

The literal "every committed evidence summary" scope is wider than these scripts:

- `results/ambient-baseline-v1-turn5-omissions.json` comes from repository `samples/ambient/audit_omissions.py:main`.
- `results/ambient-baseline-v1-turn5-uncollided-check.json` corresponds to `src/Gcam.Cli/Commands/AmbientCommands.cs:RunUncollided`, with a supplied output path.
- `results/ambient-baseline-v1-turn6-generator.json` is the retained generator result; `AmbientCommands.RunTerrestrial` writes `generator-results.json` into its supplied output folder, alongside a spectrum and SHA-256 sidecar. The turn-6 report explains its retention under the committed filename.
- `results/ambient-baseline-v1-prerequisites.json`, `ambient-baseline-v1-turn3.json`, and the unsuffixed turn-6/7/8/9 JSONs and Markdown reports are handoff/curated reports, not outputs of the listed aggregators. No reusable writer for these report filenames was found in the evidence scripts. Requests, material/catalog/source data, and threshold inputs are not all aggregation outputs either.
- `depthlik/probe/Program.cs` and `Rough.cs` write exploratory `scan_*.csv` and `rough_*.csv` next to their executable; these are outside the seed-driver pipeline.

Recommend defining TODO-37 as **future seed-driver outputs and their downstream summaries**, including bias baseline. Do not silently claim automatic coverage for historical reports or direct CLI/exploratory runs.

## RP verdicts

| Row | Verdict | Evidence and required correction |
|---|---|---|
| RP-1 | Correct it | Actual loaded application binaries are the useful execution identity. Hash the CLI's own output directory and the probe's own directory independently, including dependency DLLs and host JSON files. Hash untracked contents, not only their names. Include root build inputs omitted by `src samples rtl`. Source metadata is diagnostic, not proof that a build matches HEAD. Scripts and RTL dependencies need content hashes and applicable tool versions too. Measurements below support the cost. |
| RP-2 | Correct it | Root metadata can drift through resume and file copying. Bind provenance to each completed seed and verify it before reuse; all loaders must validate it. A single equality test on root records would compare timestamps/jobs/families and reject legitimate split runs while missing old seeds relabeled by an overwritten root. CSVs and the optional JSON list need a format decision. `bias_baseline` needs summary-input validation instead of `run-info.json`. |
| RP-3 | Holds, with qualification | Dirty source is legitimate. Validate required fields and hashes, but never reject merely because dirty is true. A hash of an empty tracked diff does not identify untracked code. Label the recorded scope and distinguish source snapshot identity from executable identity. Dirty status currently excludes docs/root build files. |
| RP-4 | Holds | Read only pinned source summaries in `Files`; print provenance for selection and validation separately. CAL-04 needs both roles/families from the same file. Missing legacy provenance keeps exactly the existing fallback. Present-but-malformed provenance must fail, not become "not recorded". No current HEAD, git call or timestamp in generation. |
| RP-5 | Holds for preservation; optional mode remains open | No old summary needs rewriting. Existing root records lack assembly/diff hashes and cannot safely be upgraded into complete new provenance. Reattaching today's identity to old numbers is invalid. Recommend omitting `--provenance-only` in this task. |
| RP-6 | Correct it | Stdlib tests can cover shared validation without importing NumPy/SciPy-dependent aggregators. Add resume, per-seed binding, duplicate-root conflict, output formats, mutation and deterministic rendering cases. CI's existing `calibration-records` job uses Python 3.13 and runs `--release`, which is stronger than `--check`. Temporary Git mutation tests belong in implementation/CI; none was run in this review under the no-git-state-change limit. |

## Measurements and stability

Machine: Windows, Python 3.11.9, Git 2.45.0.windows.1, selected SDK 9.0.311. HEAD at measurement: `4c65effb7b22353c8f8e6433167a4e2cde69857d`. The relevant source paths were clean; the pre-existing working-tree changes were `docs/AGENTS.Todo.md` and the untracked reference plan.

Thirty SHA-256 passes per set, Python `hashlib`, reading bytes directly, with no build:

| Input set | Bytes | First pass | Median |
|---|---:|---:|---:|
| CLI, all seven `Gcam.*.dll` | 947,200 | 2.316 ms | 1.233 ms |
| CLI, all DLLs + `.deps.json` + `.runtimeconfig.json` | 950,693 | 1.862 ms | 1.394 ms |
| Probe, nine `Gcam.*.dll` | 671,744 | 125.794 ms | 0.941 ms |
| Probe, all ten DLLs + host JSON, including CommunityToolkit.Mvvm | 797,632 | 1.934 ms | 1.230 ms |

The first probe pass was colder; this is not a controlled cold-cache benchmark. All six common engine DLLs (`Configuration`, `Core`, `Decoding`, `Detector`, `Masks`, `Simulation`) have different byte hashes in the two directories. That establishes different artifacts, not the reason for the difference or their source commits. No rebuild was needed for this review.

Ten scoped binary-diff captures had median 23.10 ms, maximum 25.15 ms. The current scoped diff was empty. `dotnet --version` took 98.77 ms; `dotnet --list-runtimes` took 21.38 ms. Installed .NETCore.App versions include 9.0.9 and 9.0.13; this inventory does **not** establish the effective runtime used by a child process. Capture its resolved runtime through the actual launch context or an explicit same-host diagnostic; SDK version alone cannot identify runtime roll-forward.

Line-ending experiment used a scratch worktree view of the existing read-only Git index, not a new repository or checkout. Copied `.gitattributes` and HEAD's `src/Gcam.Core/Photon.cs`, appended one comment, then tested LF and CRLF variants with `GIT_WORK_TREE` pointing into scratch and optional locks disabled. Both captured diffs were 404 bytes, SHA-256 `4c0df5d5ed02063e1a7bbaa522a8743e3a7be8196c11c75f85344d6ba27c7d6e`. CRLF produced only a normalization warning on stderr. Repository attributes therefore normalized this tracked text example under `core.autocrlf=false`.

This is not a universal cross-platform patch-hash guarantee. Pin diff algorithm, quoting, full indexes, rename behavior, prefixes and external/textconv suppression; capture stdout as **bytes**, not decoded text with newline translation. Changes to attributes, Git versions, file modes, filters, or untracked raw bytes can still change identity. Do not advertise patch SHA-256 as a canonical executable identity. Raw script/config hashes deliberately distinguish changed bytes; the repository's LF rule should be enforced for pinned text inputs.

## Proposed schema and validation

Use one stdlib module (proposed `samples/evidence/provenance.py`), schema version 1. Proposed logical shape, with field spelling fixed before implementation:

```json
{
  "Provenance": {
    "SchemaVersion": 1,
    "Sources": {
      "<source-record-sha256>": {
        "ExecutionId": "<sha256>",
        "Source": {
          "Commit": "<full git object id>",
          "Dirty": true,
          "Scope": ["<explicit paths>"],
          "DiffSha256": "<sha256 of pinned-command raw stdout>",
          "Untracked": {"<relative path>": "<content sha256>"},
          "SnapshotSha256": "<sha256 of canonical source metadata>"
        },
        "Executor": {
          "Kind": "cli",
          "Files": {"Gcam.Cli.dll": "<sha256>", "<dependency or script>": "<sha256>"},
          "Tools": {"DotnetRuntime": "<effective version>", "Python": "<version>"}
        },
        "BuildObservation": {"DotnetSdk": "<version>", "Git": "<version>"}
      }
    },
    "Inputs": [
      {
        "Family": "<id>",
        "Seed": 12345,
        "SourceId": "<source-record-sha256>",
        "RecipeSha256": "<sha256>",
        "Outputs": {"<relative filename>": "<sha256>"}
      }
    ],
    "Analysis": {
      "Files": {"<writer and imported helpers>": "<sha256>"},
      "Tools": {"Python": "<version>"},
      "Arguments": {"<semantic option>": "<value>"},
      "InputFiles": {"<logical input>": "<sha256>"}
    },
    "Mixed": false
  }
}
```

`done.json` binds the source record (embedded or immutable record plus verified digest), recipe, seed/family and numerical output hashes. Root `run-info.json` is an invocation index with the same versioned source records; start time/concurrency/requested families remain there, outside identity. A root cannot relabel existing `done.json` files.

Canonical JSON means sorted keys, fixed separators, UTF-8, finite values only. Hashes are lowercase SHA-256 hex; validate schema/version, types, identifiers, required executor inputs, and binding digests. Do not hash a record containing its own identifier. Use logical/repository-relative paths, not usernames, machine paths or full environment dumps. Record UTC invocation time only in the invocation log. Installed package metadata must be captured without importing package code when possible.

`ExecutionId` covers the actual executor file closure and effective execution tool versions, including Python scripts/helpers for recipes and NumPy/Icarus/vvp for the applicable RTL modes. Runtime patch/OS/architecture belong in execution context; include context that can affect numerical results. SDK/Git versions and source lineage are retained observations, but do not enter execution equality merely because a docs commit or already-generated summary changed. `SourceId` preserves those distinctions. Recipe identity covers manifest bytes, seed-list bytes, family/overrides, effective cloned request/config, and pinned/transitive data inputs; compare this within a family, keeping different families' recipes separate. The per-seed effective-config digest necessarily differs by seed: use a common family recipe digest plus a separate seed-specific config digest.

Hash assemblies from the directory **actually launched**, including third-party dependencies, `.deps.json`, `.runtimeconfig.json` and native dependencies when present. Do not substitute DLLs from their individual project directories. The CLI presently has seven DLLs; the probe additionally loads Studio layers and CommunityToolkit.Mvvm. An application-file digest is inexpensive but says nothing about whether the binary was built from the recorded source. State this explicitly.

To prevent a rebuild changing binaries between measurement and execution, copy the executable closure once to `<out>/artifacts/<digest>/` and execute that immutable copy, checking its bytes on reuse. The copy cost was not measured. For live Python/RTL recipes, either stage the required script/data closure too or enforce a no-mutation contract and verify its hashes before and after every seed. Before/after checks detect ordinary edits, but cannot guarantee detection of a transient edit reverted during execution. The plan must choose the strength of that guarantee rather than call a pre-run hash something that "cannot drift".

Capture Git failures as errors, not clean/empty facts. Recommended source scope includes `src`, executable evidence/probe/RTL sources, relevant sample inputs, `.gitattributes`, `Directory.Build.*`, `global.json`, package/lock configuration and any custom build targets. Untracked content hashes are necessary; tracked diff alone misses untracked files. Do not include ignored build outputs in source diff identity: they have their own artifact hashes. Record the chosen scope and diff command/version so the diagnostic is interpretable.

Before any aggregation or output write:

1. Validate every chosen seed's completion, provenance version/digests, family/seed, recipe and output hashes. Require a valid root index too, but derive source identity from the seed record. Missing root or missing seed provenance fails; `--allow-mixed` must not waive absence or corruption.
2. Compare execution identities for the runs actually consumed, and compare common recipes within each family. Exclude invocation start time, worker count, family lists and absolute output paths. CLI and probe are different executor kinds, so an ensemble spanning both needs a typed executor map rather than one identical DLL set for every family.
3. Inspect duplicate `(family, seed)` runs across roots. Existing `aggregate.find_runs` takes the first; `cascade_fit.load_rows` takes the first successful run. Refuse conflicting duplicates rather than silently selecting convenient provenance. Identical bound outputs may be deduplicated deterministically.
4. Resume only a seed with matching execution/recipe identity. Preserve its original source record. A mismatching or legacy seed requires an explicit rerun using existing `--force`, or a new output root; never overwrite metadata to make it match. Reject before changing root metadata when reuse cannot be validated.
5. Record analysis-time writer/helper hashes, tool versions and semantic options. `cascade_fit` generates an independent NumPy geometric MC expectation and bootstrap, so copying only engine provenance omits code that also produced its numbers. `--first-nulls`, `--compact`, seed-list choice and bootstrap parameters belong in analysis provenance.
6. For summary inputs (`bias_baseline`, selected thresholds), validate pinned hashes and retain input roles and their provenance. Threshold selection and later validation may have different historical source records; print both. A declared mixed execution must enumerate all identities and their family/seed membership, never choose one "representative" identity.

For JSON objects, add `Provenance` without touching numerical fields. For the optional full-summary JSON list, use a versioned object envelope `{Provenance, Metrics}` for future output. For the three committed CSV interfaces, recommend bound `.provenance.json` sidecars containing the same object and hashes of the CSV files, retaining CSV columns and reader compatibility. Bind `values.json` too if it remains the legacy metrics object; alternatively add its top-level `Provenance` with consumers updated. Adding a comment to CSV is not a portable embedding. This is an explicit correction to RP-2's literal embedding requirement.

Validate everything before writing any output. Today `aggregate.main` writes CSVs before its final quoted-key completeness check; failure can leave partially updated results. Prepare serialized outputs first and publish a complete set with the binding sidecar last. A missing/mismatching sidecar must make a new consumer refuse an incomplete set.

For calibration generation, extend `data_section` from already verified pinned JSON inputs: role/family, commit, dirty diff plus untracked/snapshot digest, execution/artifact identity, effective tool versions, analysis identity and any mixed status. No local build lookup. Keep old fallback text exactly unchanged when provenance is absent; reject invalid new provenance. Selection/validation fields in the existing spec suffice to locate inputs, including CAL-04's shared file. Any future regenerated file changes its byte pin and requires intentional pin/record review, including downstream threshold pins in requests/manifests. Do not update those pins in this review or attach current hashes to historical runs.

## Proposed changes to the plan

1. Correct the producer table: classify RTL as a raw executor; include `bias_baseline`; enumerate JSON, CSV and JSON-list formats. Narrow the guarantee to the seed-driver pipeline unless direct CLI/exploratory generators are explicitly added.
2. Make per-seed provenance and resume validation mandatory. Root metadata alone is insufficient. Bind numerical outputs and immutable source records; reject conflicting duplicate seeds across roots.
3. Hash each actual executor closure, scripts and relevant tools. Snapshot managed binaries before launching; decide how to freeze live Python/RTL inputs. Keep source commit/diff/untracked-content identity as diagnostic lineage, not a claimed source-to-build proof.
4. Define versioned canonical schema, identity equality, explicit source scope, transitive recipe/data pins, and separate analysis identity. Exclude invocation-only fields and preserve source roles. Fail on missing/malformed provenance and Git/tool-query failures.
5. Default to refusing incompatible execution identities. If mixed support is retained, record every identity and its consumed inputs; it cannot override missing provenance, output corruption or a conflicting duplicate seed.
6. Adopt bound CSV sidecars and a future JSON-list envelope. Preflight/serialize the whole output set before publishing. Preserve existing metric names, values, rounding and columns.
7. Print source/analysis provenance per selection and validation role in CAL section 6, from pinned files only. Keep four legacy records byte-identical and drop provenance-only backfill from this task.
8. Add stdlib unit tests plus fixture-based integration of the actual producer/consumer validation hooks. Run them in the existing calibration CI job before `calibration_record.py --release`; document next-run provenance and resume behavior in README during implementation.

## Tests proposed for implementation

Proposed command: `python -B -m unittest discover -s samples/evidence/tests -p "test_provenance*.py"`, followed by `python -B samples/evidence/calibration_record.py --release`. The job needs no Monte Carlo build, NumPy or SciPy for shared provenance tests.

Use explicit scratch fixtures to cover canonical order, malformed hashes/versions, untracked same-name content changes, staged plus unstaged edits versus HEAD, LF/CRLF and binary changes, dirty acceptance, Git failure, missing root/per-seed metadata, tampered outputs, split roots, timestamps excluded from equality, typed CLI/probe executor maps, mismatching resume and legacy resume, changed executable bytes, conflicting duplicate seeds, mixed-mode membership, JSON/list/CSV bindings, derived-summary lineage, and deterministic CAL text for legacy/clean/dirty/mixed inputs. CAL-04 must expose both selection and validation identities. Test new artifact snapshots with fake executors so a .NET build is unnecessary.

An isolated temporary Git repository can be initialized/staged/committed by the future test harness in implementation/CI, with a controlled local test identity and no global configuration. Under this turn's hard limits those state-changing Git commands were not authorized, so the review used the existing index read-only for its LF/CRLF measurement. A future NumPy/SciPy integration run may be a separate environment check; the stdlib CI test must not depend on importing those packages. Test that adding provenance leaves numerical payloads unchanged, rather than rerunning expensive evidence ensembles.

## Open questions for the author

1. **Scope:** future seed-driver pipeline, or all direct generators and curated evidence reports as well? Recommendation: seed driver and every downstream summary, including bias baseline; explicitly exclude direct generators/curated reports until separately planned.
2. **Mixed support:** ship `--allow-mixed` now? Recommendation: defer it and fail closed for incompatible execution identities. Multiple roots with compatible provenance remain supported; heterogeneous CLI/probe families are represented by executor kind, not treated as an accidental mixed build.
3. **CSV format:** accept bound sidecars instead of literal embedding? Recommendation: sidecars for CSVs, object envelopes for future JSON-list output, with a versioned format and hashes binding every member of the output set.
4. **Drift guarantee:** stage live Python/RTL dependencies, or enforce no concurrent edits plus before/after checks? Recommendation: immutable managed artifacts now; stage the actual Python/RTL closure if the requirement is an identity that cannot drift during execution. If staging is deferred, document the remaining limitation plainly.

No decision is needed to allow dirty trees or preserve old records; both are already specified. Recommend no provenance-only mode because legacy root metadata cannot supply the missing execution identity.

## Checks, limits and write audit

- `python -B samples/evidence/calibration_record.py --release`: **passed**, four records up to date, all checks passed. No writes by this command.
- No build, test installation, engine evidence run, desktop action, deletion, process termination, upload or Git state change was performed. No temporary Git repository was initialized. No external docs/web were consulted; this is a repository review.
- Did not measure actual selected child runtime, artifact-copy cost, Python-package/Icarus version query cost, end-to-end provenance (not implemented), cold-cache reproducibility, or patch equality on another operating system. No historical source reconstruction/backfill was attempted.
- Readonly shell searches with PowerShell brace/glob syntax failed in several calls; corrected searches used explicit paths/directories and `rg -g`. They wrote nothing.
- Only one repository file was written: this review, by `tools.apply_patch` (Add File). Verify LF and the final status after writing.
- The **only write-producing shell command** was a PowerShell here-string piped to `python -B -`, the LF/CRLF experiment. Its writes were `scratch.mkdir(parents=True, exist_ok=True)`, `work.mkdir(exist_ok=True)`, `target.parent.mkdir(parents=True, exist_ok=True)`, `(work/'.gitattributes').write_bytes(...)`, and `target.write_bytes(content)` twice. Resolved destinations: `%TEMP%/gcam-todo37/`, `%TEMP%/gcam-todo37/eol-worktree/.gitattributes`, `%TEMP%/gcam-todo37/eol-worktree/src/Gcam.Core/Photon.cs`, and their parent directories. Git only read the existing repository/index with `GIT_WORK_TREE` and `GIT_OPTIONAL_LOCKS=0`; it wrote no Git state.

No approval requests.
