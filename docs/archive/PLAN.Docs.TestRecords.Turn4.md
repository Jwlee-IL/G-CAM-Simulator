# PLAN.Docs.TestRecords.Turn4 — readable catalog and compressed evidence

Status: implemented and locally verified. No milestone committed; the planner collects M1 after the implementation commit. No test, assertion or tolerance changed. No desktop, render or numerical-evidence opt-in ran.

## Results

- Catalog: **2,728,186 → 265,322 bytes**, 3,244 lines; 153 complete / zero incomplete entries. Purpose, oracle, tolerance and gates occur in prose; metadata contains identity, relative method/trace selectors and reviewed source hashes. Relative selectors expand to exact qualified identities. Method groups contain names, never source. Only JSON fences remain. Existing unexplained-margin findings remain findings, not invented derivations.
- Standalone report: **94,706 → 22,869 bytes**. The previous 2,878,673-byte Markdown total included the pinned catalog; the new total is 291,895 bytes. Summaries show project/family and class counts; case details show failures, skips grouped by reason, and missing executions. Passed case details and complete tool/environment identification remain in JSON. Trace results aggregate by requirement; exact selectors remain in the catalog.
- Scratch: `%TEMP%/gcam-todo28/turn4/bundle`, report `VV.Tests.Results.md` beneath the same scratch root, retained `export` and byte-identical `export-repeat`. No repository `VV.Tests.Results.md` was created. The two Studio generated interiors were regenerated; stripping those interiors proves every other byte matches the turn-start snapshot.
- Inventory unchanged: xUnit 118 classes / 843 cases, 815 passed / 28 skipped; Python two classes / 39 cases, 38 passed / one skipped; RTL 29 configurations / 137 cases, 111 passed / 26 optional-vector skips; four checkers passed. Total: **1,023 selected cases, 968 passed, 55 skipped, zero failures/missing**, 11 invocation records. RTL used `py -3.13`, CPython 3.13.4 / cocotb 2.0.1.

## Retained size

Bytes, including replay inputs and outputs, excluding build products and self-test fixtures:

| Kind | Turn 3 | Turn 4 |
|---|---:|---:|
| TRX / TRX.gz | 1,209,449 | 207,681 |
| JSON / JSON.gz, including manifest pool | 2,437,705 | 201,878 |
| Markdown | 2,878,673 | 291,895 |
| Logs | 221,354 | 221,039 |
| RTL XML | 35,278 | 35,288 |
| **Total** | **6,782,459** | **957,781** |

| Family | Turn 3 | Turn 4 |
|---|---:|---:|
| .NET | 2,517,771 | 391,577 |
| unittest | 182,360 | 9,539 |
| cocotb | 383,639 | 176,659 |
| checker | 556,650 | 6,170 |
| Shared catalog/report/manifests | 3,142,039 | 373,836 |

TRX + JSON: **3,647,154 → 409,559 bytes**. Same-run uncompressed retention would be 4,195,335 bytes; gzip without pooling would be 1,483,825. Pooling saves another 526,044 bytes, including its 60,328-byte pool with eight unique manifests. These are normal-profile measurements; added evidence/artifacts change milestone size.

Exports gzip TRX and JSON at level 9 with mtime zero, no header filename and a fixed header. Working collections remain uncompressed. Source/generator manifests and subject before/after manifests share content-addressed pool entries. Logical paths, original hashes, raw-hash claims, all normalized fields and all original artifact bytes are preserved. The reader expands pooled documents before validation. Export compares every retained expanded file byte for byte with its original; two complete exports also matched physically. The checker rejects malformed gzip headers, truncated gzip and stale/missing pool references, then re-normalizes sanitized TRX as before. Logs/XML/Markdown remain plain. Compressed and historical plain bundles are both readable.

## Verification

Release build passed with zero warnings/errors. Full English Release solution test passed; its process-local TEMP/TMP pointed inside the task scratch root, as did collection. Both Python suites ran through the unittest adapter, including the ten NumPy/SciPy background-shape tests. Calibration `--check` reports four current records. `check-catalog --discover`, working-bundle/report/Studio `check`, compressed-export `check`, and final `self-test --out` passed. Self-test now proves 26 precise refusals plus the existing eight adapter/sanitizer checks, compressed replay and byte-exact field preservation. Archived replay reports zero committed milestones, explicitly not a passing milestone. The first checker collection omitted `--checker` and was refused before a record was selected; the four named checker invocations succeeded.

GitHub Actions was not run; workflow and RTL runner were not edited this turn. Other received planner/prior-turn edits remain as received.

## M1 commands

After the implementation commit, start with a clean tree, a fresh scratch bundle and an absent export destination. Run in one child PowerShell process from `<repo>`; continue only after each native command exits zero. The environment assignments affect that process only. All profiles here are normal; desktop attachment still needs the author's authorization.

```powershell
$m1Root = Join-Path $env:TEMP 'gcam-todo28/M1'
$m1Runtime = Join-Path $m1Root 'runtime-temp'
$m1Bundle = Join-Path $m1Root 'bundle'
New-Item -ItemType Directory -Force -Path $m1Root, $m1Runtime | Out-Null
$env:TEMP = $m1Runtime
$env:TMP = $m1Runtime
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:PYTHONDONTWRITEBYTECODE = '1'
$env:PYTHONIOENCODING = 'utf-8'
$env:GIT_OPTIONAL_LOCKS = '0'
$env:GCAM_EVIDENCE_TESTS = '0'
$env:GCAM_RENDER_SNAPSHOTS = '0'
$env:GCAM_UI_TESTS = '0'
$env:GCAM_PROVENANCE_GIT_TESTS = '0'
$env:GCAM_CRRC_VECTORS = $null
dotnet build Gcam.sln -c Release
python -B samples/testing/test_records.py check-catalog --discover
python -B samples/testing/test_records.py collect --family dotnet --no-build --out $m1Bundle
python -B samples/testing/test_records.py collect --family unittest --include-background --out $m1Bundle
python -B samples/testing/test_records.py collect --family cocotb --out $m1Bundle --python py -3.13
python -B samples/testing/test_records.py collect --family checker --checker calibration --out $m1Bundle
python -B samples/testing/test_records.py collect --family checker --checker test-catalog --out $m1Bundle
python -B samples/testing/test_records.py collect --family checker --checker archived-replay --out $m1Bundle
python -B samples/testing/test_records.py collect --family checker --checker test-record-self-test --out $m1Bundle
python -B samples/testing/test_records.py generate --bundle $m1Bundle --output docs/VV.Tests.Results.md --studio docs/VV.Studio.md --export samples/testing/records/M1
python -B samples/testing/test_records.py check --bundle $m1Bundle --output docs/VV.Tests.Results.md --studio docs/VV.Studio.md
python -B samples/testing/test_records.py check --bundle samples/testing/records/M1
python -B samples/testing/test_records.py self-test --out (Join-Path $m1Root 'self-test')
```

## Write-command audit

Paths below use `%TEMP%/gcam-todo28/turn4` as `<scratch>`. Full command payloads and edit patches are in the turn's tool log. No delete, install, Git mutation, persistent setting or upload command ran.

- Edit tool: four updates to `samples/testing/test_records.py` (storage/catalog reader; compact renderer/export; archive defect fixtures; gzip corruption/ambiguity handling) and creation of this report.
- `New-Item -ItemType Directory -Force`: `<scratch>` and `<scratch>/runtime-temp`.
- `Set-Content -Encoding utf8`: `<scratch>/compact_catalog.py`, `refine_catalog.py`, `refresh_catalog.py`, `refine_catalog_2.py`, `refine_catalog_3.py`, `catalog_size.py`, `measure.py`, `proof.py`. These are retained transcription/proof utilities, not shipped generators.
- `python <scratch>/compact_catalog.py` four times: reworked the catalog and wrote scratch findings; the initial invocation also preserved the original catalog/Studio snapshots. `python <scratch>/refine_catalog.py`, `refine_catalog_2.py`, `refine_catalog_3.py`: edited only the scratch transcription utility. `python <scratch>/refresh_catalog.py` three times: refreshed reviewed catalog source fingerprints.
- `dotnet build Gcam.sln -c Release *> <scratch>/build.log`; `dotnet test Gcam.sln -c Release *> <scratch>/full-test.log`: ordinary build/test outputs and task-local runtime scratch.
- `python samples/testing/test_records.py self-test --out <scratch>/self-test-preflight`, `self-test-storage`, `self-test-final`: isolated seeded fixtures and self-test JSON.
- `python samples/testing/test_records.py collect --family dotnet --no-build --out <scratch>/bundle *> <scratch>/collect-dotnet.log`.
- `python samples/testing/test_records.py collect --family unittest --include-background --out <scratch>/bundle *> <scratch>/collect-python.log`.
- `python samples/testing/test_records.py collect --family cocotb --out <scratch>/bundle --python py -3.13 *> <scratch>/collect-rtl.log`.
- `python samples/testing/test_records.py collect --family checker --out <scratch>/bundle *> <scratch>/collect-checkers.log`: refused invocation; empty scratch directory/log retained. Then `collect --family checker --checker <name> --out <scratch>/bundle` for calibration, test-catalog, archived-replay and test-record-self-test: records and checker-owned scratch fixtures.
- `python samples/evidence/calibration_record.py --check *> <scratch>/calibration-check.log`: log only.
- `python samples/testing/test_records.py generate --bundle <scratch>/bundle --output <scratch>/VV.Tests.Results.md --studio docs/VV.Studio.md --export <scratch>/export`: pinned compact catalog, generated output/manifests, compressed retained export and the two authorized Studio interiors.
- `python <scratch>/measure.py`: size JSON and independent `export-repeat`; `python <scratch>/proof.py`: augmented size JSON. `python <scratch>/catalog_size.py` was read-only.

APPROVAL REQUESTS: none.
