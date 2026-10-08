# PLAN.Docs.TestRecords.Turn5 — specific catalog purposes

Scope: documentation only; replace the 61 named placeholder purposes after reading their class sources and render partials.

Rewrote all **61 purposes** with the behavior or law checked; clarified **17 oracle lines** and reviewed the other 44 for class-specific references. Preserved existing numerical budgets and their derivations. Byte comparison proves every metadata block (including ids, selectors and source hashes), every tolerance paragraph and all catalog content outside the target purpose/oracle paragraphs unchanged. Coverage remains 153 complete / zero incomplete entries. Catalog size is 281,251 bytes.

`check-catalog --discover` passed without building or executing tests. Regenerated `%TEMP%/gcam-todo28/turn5/VV.Tests.Results.md` and its new catalog pin from unchanged turn-4 evidence copied into the turn-5 scratch bundle. Full `check` passed, including catalog resolution, artifact replay and generated-byte verification. No source code, test, Studio block or committed milestone changed; no build or full test run was needed. M1 commands remain those in the turn-4 report.

Examples, verbatim purpose sentences:

- **KleinNishinaTests:** Checks sampled Compton scattering angles against the Klein-Nishina angular distribution at several photon energies and enforces the Compton energy-angle relation. Rotating a sampled direction must preserve unit length and the requested scattering angle.
- **ImagingServiceTests:** Checks isotope-channel localization and signed spectral stripping, including net counts, calibration uncertainty and overlapping-window covariance. It also checks retained-event replay, incremental-prefix equivalence and MLEM matrix reuse across refreshes.
- **MinMaxPyramidTests:** Checks that plot-envelope queries return the same extrema as a direct scan, including endpoints and block boundaries. It also checks clipped ranges, rejection of non-finite samples and the storage bound.

Write-command audit (`<scratch>` = `%TEMP%/gcam-todo28/turn5`):

- `New-Item -ItemType Directory -Force` created `<scratch>` and `<scratch>/runtime-temp`.
- `Set-Content -Encoding utf8` wrote `read_classes.py`, `rewrite_purposes.py` and `preserve_derivations.py` beneath scratch.
- `python <scratch>/read_classes.py 0 21` read all target class sources and wrote the initial catalog snapshot, target list and source-review notes beneath scratch.
- `python <scratch>/rewrite_purposes.py` ran twice: changed only catalog purpose/oracle prose; wrote scratch preservation proofs and copied 96 unchanged replay inputs from turn 4 into the turn-5 bundle.
- `python <scratch>/preserve_derivations.py` refined only the scratch transcription utility to retain existing oracle-side numerical budget descriptions.
- `python samples/testing/test_records.py generate --bundle <scratch>/bundle --output <scratch>/VV.Tests.Results.md` wrote the new catalog pin, generation manifest, scratch report and inventory/trace outputs. Studio was not supplied.
- Edit tool created this report. Environment assignments were process-local; subsequent catalog/check commands were read-only apart from task-local runner scratch used by discovery.

APPROVAL REQUESTS: none.
