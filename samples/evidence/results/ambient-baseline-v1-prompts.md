# TODO-30 implementer prompts (turns 1-5)

Prompts the planner sent to Codex for TODO-30, kept so a new implementer conversation can be started on another computer (the Codex conversation itself is local). Turn 1 = review; turns 2-5 = implementation and the AB-4a/4b/4c corrections. Start a new turn from the plan's Status, the review, the turn reports in this folder and the turn-2 work list below.

## codex-t30-prompt

```text
You are the implementer for TODO-30 in the repository <repo root> (C#/.NET 9 Monte
Carlo simulator of a coded-aperture gamma camera; branch MainRoot). This is TURN 1: REVIEW AND MEASUREMENT ONLY — no
changes to code, tests, configuration or existing documents. Your only repository write is the new file
`docs/PLAN.Physics.AmbientBackground.Review.md`.

## Read first
1. `docs/PLAN.Physics.AmbientBackground.md` — the reference plan (the specification for this review).
2. `docs/AGENTS.Planning.md` (procedure; what a review contains), `AGENTS.md` (architecture), `docs/VV.Gcam.Evidence.md`
   §1 "How MC numbers are quoted" and "Ideal conditions and background" (the table this task must eventually fill),
   EV-02, EV-07, EV-12, EV-23, EV-25, and `docs/VV.Gcam.PRS.md` PR-SENS-02.
3. Code: `src/Gcam.Configuration/SimulationConfig.cs` (BackgroundConfig), `src/Gcam.Simulation/BackgroundStudy.cs`,
   `ListModeSource.cs`, `ListModeBackground.cs`, `EventStreamStudy.cs` (BackgroundDepositSpectrum), `DoseStudy.cs`
   (ICRP 74), `ShieldStudy.cs`, `FieldOfViewStudy.cs`, `NoiseStudy.cs`, `src/Gcam.Studio.Services/SimulationService.cs`,
   `src/Gcam.Studio.Core/ViewModels/MainViewModel.cs`, and the seed driver `samples/evidence/` (README, run_seeds.py).

## What the review must do
- Verify every row of "What exists" against the code (file, member, line) and correct it where wrong.
- For A-1 / A-2 / A-5 / A-10: find **citable published data** — terrestrial gamma spectrum composition (line energies
  and relative contributions to H*(10) or fluence, continuum fraction; e.g. ICRU Report 53, UNSCEAR, Beck/de Planque
  in-situ spectrometry, or equivalent), typical indoor / outdoor H*(10) rates, the cosmic photon component at sea level,
  intrinsic activities of LYSO / LaBr3 / CeBr3 / GAGG / NaI / CsI / BGO, and at least one published measured
  background spectrum or count rate of a small scintillator at a stated dose rate for the sanity anchor. Give full
  references. If you cannot access a source, say so — never invent a number; mark it "needs source".
- Measure (in %TEMP% only, see limits): what the current BSR model means physically — e.g. for `samples/scenario.json`,
  `samples/scenario_handheld.json` and Studio's default scene, the detected background rate a 0.1 µSv/h isotropic field
  would give (a first-order estimate from fluence × crystal face × efficiency is acceptable if labelled), and the BSR that
  corresponds to at the default source activities and distances. Show how much the ideal-environment conclusions
  (EV-07 gates, EV-01/09 floors, EV-15) could move, as an order of magnitude, before implementation.
- Propose: the config / API shape (how ambient and BSR coexist), the transport route through mask and shield (what the
  existing shield models cover — front plate only?), pixel placement from the actual deposit, the Poisson time process,
  Studio input and its default (A-7 is the author's decision: give options with a recommendation), the significance
  measure and protocol for the PR-SENS-02 gate (A-8), the re-measurement list and cost for A-9 using the seed driver,
  test oracles with tolerances derived from sample sizes (never borrowed).
- Point out anything wrong or missing in the plan.

## Rules
- If the plan is wrong, or a premise does not hold in the code, say so and stop on that item — never work around it.
- This is a clean-room public repository: do not write anything about any former employer's instrument; model from
  published practice. Do not mention other private repositories.
- Build into your own output directory if you need binaries (e.g. `dotnet build -c Release --artifacts-path
  %TEMP%\gcam-ambient-review\artifacts`), so you do not lock the main tree's bin folders — another process may run UI
  tests from the main tree.

HARD LIMITS (no sandbox is enforcing them — you are trusted to keep them; the planner audits your command log):
1. Paths. Write only inside the repository working tree (never inside .git) and %TEMP%\gcam-*; `dotnet restore`/`build`
   may fill the NuGet package cache. Read only the
   repository, %TEMP%, the NuGet package cache and the .NET SDK. Do not read or write C:\Windows, C:\Program Files*,
   C:\ProgramData, the registry, other users' folders, ~/.codex, ~/.claude, ~/.ssh, or any credential store.
2. Irreversible commands need the author's approval: deleting (Remove-Item, rm, del, rmdir, rd, git clean),
   moving or renaming over an existing file, `dotnet clean`, any git command that changes state (add, commit,
   checkout, switch, reset, restore, stash, rebase, merge, push, tag, branch -d), installing or updating anything
   (dotnet tool, winget, npm -g, pip), persistent changes to environment variables / PATH / settings (setx,
   user or machine variables, profiles), stopping processes,
   network uploads. Setting a variable for one command's own process (e.g. `$env:GCAM_RENDER_SNAPSHOTS='1'; dotnet
   test …`, `GCAM_EVIDENCE_TESTS=1 dotnet test …`) is allowed: it ends with the process. Editing repository files through your edit tool is allowed (git can restore them); a new file
   that replaces an existing one is not a reason to delete the old one.
3. When you need one: do NOT run it. Finish everything that does not depend on it, then end your report with a section
   "APPROVAL REQUESTS": each exact command, why, what it destroys, how to undo it. Stop there. Approval arrives in the
   next turn, naming the exact command.
4. No desktop: do not launch Studio or any GUI, no UI tests (GCAM_UI_TESTS), no window automation.

Also: do not stop or touch any running process (a long measurement, PID 63228, is running).

## Final report
Write the review file, then make your final message a short report: what you verified and corrected, the data you
found (with references), the measurements with method and numbers, the proposals, open questions for the author, what
you could not do, and APPROVAL REQUESTS if any.
```

## codex-t30-impl-prompt

```text
TURN 2 — IMPLEMENTATION of the TODO-30 baseline.

The planner and the author have written "Decisions after review (2026-10-03)" into docs/PLAN.Physics.AmbientBackground.md
(rows AB-1 … AB-9). That section is the specification; read it first. Your review findings were accepted: high-energy
transport (TODO-31) and a realistic housing (TODO-32) are deferred; this turn builds the baseline.

First: if you materially disagree with any AB row, or a row cannot be met as written (a premise fails in the code, a
validation oracle fails), STATE IT AND STOP on that item — never loosen a tolerance, never invent a number, never tune
the model to hit UNSCEAR. Otherwise implement.

## Work (in this order; report after each that you could not finish)
1. AB-4 spectrum generator: soil half-space source of K-40, U-238 series, Th-232 series (UNSCEAR 2000 activities
   420 / 33 / 45 Bq/kg), evaluated decay data cited in the code/doc (ENSDF / DDEP / NNDC; if a source cannot be reached,
   say so and stop on that nuclide — no values from memory), photon transport through soil and air to 1 m height
   (soil and air composition / density cited), output: incident photon fluence spectrum (lines + scattered continuum)
   and angular distribution at the detector point, per Bq/kg, versioned + hashed file under samples/ (or a data folder —
   say why). Oracle: air kerma rate per Bq/kg vs UNSCEAR 0.0417 / 0.462 / 0.604 nGy/h; report the ratio and its MC
   uncertainty; AB-3 limits stated. A headless tool / CLI sub-command reproduces the file.
2. AB-1 / AB-2 / AB-6 engine: `SimulationConfig.Ambient` sibling config (null = off) per your review §5; fluence from
   H*(10) via the existing ICRP 74 table and the spectrum's fluence weights; two bounding geometries (bare crystal all
   faces with ray–box entry; front-only through the mask slab); deposit-site pixel via the existing sinks; Poisson in
   time for list mode and per pixel for flood maps; background-only acquisitions and a fixed-time event API that
   progresses through empty intervals.
3. AB-8 compatibility: with Ambient null, every existing record / RNG sequence bit-for-bit identical (legacy BSR,
   Stop / Continue). Prove it with tests.
4. AB-5 Studio: ambient input (photon H*(10) with the preset name), default 0 / off, "ideal environment" label when 0,
   derived ambient BSR readout, background-only acquisition allowed; inputs lock with acquisition as today. Follow
   docs/AGENTS.Studio.md and the SRS / SDS conventions; add SRS rows for what you add. No desktop / UI tests (the planner
   runs them later); headless render snapshots are allowed (`$env:GCAM_RENDER_SNAPSHOTS='1'`) if a view changes.
5. Tests per review §9 (tolerances derived from each test's sample size, stated in the test).
6. AB-7 / AB-9 measurements through the seed driver (`samples/evidence/`) with versioned ambient manifests: first the
   PR-SENS-02 gate study (≥ 1000 background-only acquisitions per configuration, separate selection / validation
   seeds), then EV-07, EV-02, EV-12, EV-15, EV-01 / EV-09 under the field at 0.10 µSv/h (and the other levels in your
   review §7) for both bounds. If the full budget does not fit in this turn, do the gate study and EV-07 first, report
   timings, and list what remains — do not cut N silently.
7. Do NOT edit the VV.* documents, README, PAPER, Findings or PLAN/Todo files: put every measured number, with its
   recipe and N, into your final report and into a results file under samples/evidence/; the planner writes the docs.

## Rules
Clean-room public repository: nothing about any former employer's instrument. Build in the main tree is fine now (no UI
test runs concurrently), but do not touch PID 63228 or any process you did not start.

HARD LIMITS (no sandbox is enforcing them — you are trusted to keep them; the planner audits your command log):
1. Paths. Write only inside the repository working tree (never inside .git) and %TEMP%\gcam-*; `dotnet restore`/`build`
   may fill the NuGet package cache. Read only the
   repository, %TEMP%, the NuGet package cache and the .NET SDK. Do not read or write C:\Windows, C:\Program Files*,
   C:\ProgramData, the registry, other users' folders, ~/.codex, ~/.claude, ~/.ssh, or any credential store.
2. Irreversible commands need the author's approval: deleting (Remove-Item, rm, del, rmdir, rd, git clean),
   moving or renaming over an existing file, `dotnet clean`, any git command that changes state (add, commit,
   checkout, switch, reset, restore, stash, rebase, merge, push, tag, branch -d), installing or updating anything
   (dotnet tool, winget, npm -g, pip), persistent changes to environment variables / PATH / settings (setx,
   user or machine variables, profiles), stopping processes,
   network uploads. Setting a variable for one command's own process (e.g. `$env:GCAM_RENDER_SNAPSHOTS='1'; dotnet
   test …`, `GCAM_EVIDENCE_TESTS=1 dotnet test …`) is allowed: it ends with the process. Editing repository files through your edit tool is allowed (git can restore them); a new file
   that replaces an existing one is not a reason to delete the old one.
3. When you need one: do NOT run it. Finish everything that does not depend on it, then end your report with a section
   "APPROVAL REQUESTS": each exact command, why, what it destroys, how to undo it. Stop there. Approval arrives in the
   next turn, naming the exact command.
4. No desktop: do not launch Studio or any GUI, no UI tests (GCAM_UI_TESTS), no window automation.

## Verification and report
`dotnet build Gcam.sln -c Release` (0 errors; warnings vs the current 2), `dotnet test Gcam.sln -c Release` (counts
before → after; baseline engine 298, Studio.Core 179, services 83 + 7 skipped, UI 13 + 14 skipped). Final report: files
by group; each AB row → what was done; the spectrum and its UNSCEAR comparison; every measured number with recipe, N,
both bounds; test counts; deviations; what remains; APPROVAL REQUESTS if any. Do not commit.
```

## codex-t30-impl2-prompt

```text
TURN 3 — continue the TODO-30 baseline implementation.

Your stop on Po-218 was correct. The planner has added **Correction AB-4a** to docs/PLAN.Physics.AmbientBackground.md
(read it): a nuclide or branch without evaluated photon data may be omitted only with a quantified upper bound computed
from the evaluated feeding of excited levels (photon yield ≤ feeding; photon energy ≤ Σ feeding × level energy), the
bound on its share of the chain's photon fluence and air kerma written next to the spectrum; if a bound exceeds 10⁻³ of
its chain's air kerma, stop and report. Feeding data are for the bound only, never substitute line intensities. Apply
the same rule to any other nuclide that hits the same gap, and list each one with its bound.

Also fix `samples/evidence/results/ambient-baseline-v1-prerequisites.json`: no machine paths in committed files
(snapshot paths relative to the TEMP work folder or omitted; keep URLs and hashes).

Then continue the turn-2 work list in order (spectrum generator and its UNSCEAR comparison; engine; compatibility
tests; Studio; tests; measurements), with every rule, the HARD LIMITS and the report format of the turn-2 prompt
unchanged. Same stop rule: a failed oracle or premise is reported, never tuned away. Do not commit.
```

## codex-t30-impl3-prompt

```text
TURN 4 — continue the TODO-30 baseline implementation.

Your stop on the count bound was correct. The planner has added **Correction AB-4b** to
docs/PLAN.Physics.AmbientBackground.md (read it): photons per feeding <= number of levels between the fed level and the
ground state in the adopted level scheme; energy bound unchanged; kerma bound = energy bound x max air mu_en/rho over
[smallest level spacing below the fed level, fed-level energy], both from cited tables; 10^-3 threshold and stop rule
unchanged. Apply it to Po-218 and any other nuclide with the same gap, list each with its bounds.

Then continue the turn-2 work list in order (spectrum generator and UNSCEAR comparison; engine; compatibility tests;
Studio; tests; measurements). All rules, HARD LIMITS and the report format of the turn-2 prompt are unchanged. A failed
oracle or premise is reported, never tuned away. If a further premise of the planner's corrections is wrong, stop on
that item but continue every independent item that does not depend on it (e.g. engine plumbing, compatibility tests,
Studio input can proceed with a placeholder spectrum file clearly marked "not validated" and never used for evidence).
Do not commit.
```

## codex-t30-impl4-prompt

```text
TURN 5 — continue the TODO-30 baseline.

Both of your AB-4b objections were correct. The planner has added **Correction AB-4c** and an **angular-distribution
addendum** to docs/PLAN.Physics.AmbientBackground.md (read both): the omission criterion is now on the source term
(omitted emitted photon energy per chain decay <= 1e-4 of the chain's evaluated emitted photon energy; alpha bound =
sum feeding x level energy; beta branch without feeding data = branch fraction x Q_beta), declared as a modelling
approximation, listed in the spectrum file; angular representation = energy x zenith-cosine table; acceptance = the
uncollided line fluence and zenith distribution vs the analytic uniform half-space result within MC error, the total
checked through the UNSCEAR kerma comparison.

Continue: certify (or stop on) the omissions under AB-4c; build the spectrum generator, the angular sampler and the
UNSCEAR comparison; then the gate study and the EV measurements per the turn-2 list. If a premise of AB-4c is wrong,
stop on that item and say exactly why, but continue every independent item. All rules, HARD LIMITS and the report
format of the turn-2 prompt are unchanged. Do not commit.
```
