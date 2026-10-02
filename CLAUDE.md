@AGENTS.md

# Gcam — session context (portable memory)

This file carries the **cross-session context that isn't otherwise in the repo**, so work
survives moving to another computer. It is git-tracked and **auto-loaded by Claude Code on
any clone** (the machine-local auto-memory under `~/.claude/…/memory/` does NOT travel).
Architecture + build/run is in `@AGENTS.md` above; per-theme quantitative results are in
`docs/AGENTS.Findings.md` (themes 1–59). Keep answers to the author in **Korean**.

## What this is
**Gcam** — a personal C#/.NET 9 Monte Carlo simulator for **coded-aperture gamma-source
localization** (Cs-137 + multi-isotope), modelling a fixed, lab-mounted coded-aperture camera
of the class the author has hands-on experience with: rank-7 tungsten MURA mask (2×2 mosaic, ~10 mm) → 12×12
crystal flood map → ADC peak detection → cross-correlation decode. 59 themes of work;
`Gcam.sln`, CLI `montecarlo <sub>` (41 study sub-commands), GCAM Studio (the WPF viewer; the legacy Gcam.Wpf was removed in TODO-12 (2026-10-02); last present at `85b2ed1`), and a SystemVerilog +
cocotb RTL front-end path.

## Key decisions / context (not derivable from the code or git history)
- **Isotope scope:** core Cs-137 / Co-60 / Co-57, plus Ir-192 as the industrial reference source (theme 51; no
  cascade model) (122→1332 keV, cascade sum, low line under
  high-E Compton).
- **Reference readout (author, 2026-10-02):** the conventional design — scintillator pixels matched to the SiPM array
  1:1, with dead regions between pixels; the array read through **four 14-bit ADC channels**, position from the
  relative-signal (Anger-type) formula, crystal from the flood map. So pile-up is array-wide and moves the position too; the engine
  does not model this readout yet (TODO-19).
  The reference scintillator is **GAGG** — GAGG is the engine default by design; the other scintillators are what-if comparisons.
- **The classic problem of this camera class** is 662-keV window contamination — Co-60 downscatter
  reading as Cs. Gcam's crystal-Compton **spectral** lever (per-pixel stripping, themes 16–17, 26) recovers
  the Cs count; the **spatial** lever holds only up to Co:Cs ≈ 2:1 once the crystal uses physical GAGG cross
  sections (theme 52 — the earlier, stronger result rested on an over-absorbing crystal model).
- **Productization** (theme 22, design-only — not being built): GAGG:Ce,Mg, 16×16 @1 mm, D=55,
  15 mm crystal, rank-7, non-cyclic; weight is the SHIELD not the crystal; SiPM thermal drift is
  an **energy-window** issue, not a position issue.
- **Requirement set (2026-10-01, design-only)** — `docs/VV.Gcam.{Overview,URS,PRS,Evidence,Limitations,Decisions}.md`
  (+ `VV.Studio.{SRS,SDS}` for the viewer). Author decisions with reasons are logged in
  `VV.Gcam.Decisions.md` (D-01…D-40); headlines:
  - The product is a **scintillator coded-aperture camera**: compare only with same-class imagers (iPIX),
    never with Compton / HPGe cameras (Polaris-H, GeGI). Use conditions **relaxed to iPIX level**
    (−10…+45 °C, 93 % RH, IP65, 60 cm drop, ≤ 2.5 kg).
  - **No built-in radioactive source** (cost, handling, and it would be a 방사선기기 under 원자력안전법 §60);
    gain held by compensated bias + LED + **peak tracking + window widening**.
  - **Dose-rate function required** (NSS-1 range, over-range indication) + calibration obligation; no
    imaging requirement in high fields (the user's alarming dosimeter acts first). The collimated imaging head
    reads dose only frontally (theme 54), so dose comes from a **separate small counter over data I/O** (D-37).
  - **Ghosts: non-cyclic decoding**, no mask-rotation hardware. **Narrow FOV** (±3.6° fully coded):
    software path — non-cyclic usable field ~±5°, out-of-field cue, IMU/VIO hand sweep.
  - Users: radiation-safety technician + non-specialists. Dockable **Windows tablet** UI; own record
    format; plain category "industrial"; no fixed false-positive target; **Korean law only**.
  - GCAM Studio stays the **simulator's engineering viewer** (the repo is a simulator and a portfolio demo) —
    not grown into the product UI (D-34).
- **Realistic front-end defaults** (themes 30–32): AD9648 14-bit/125 MSPS, `tau_rise=1` sample
  (set by Nyquist), intrinsic ~6 % @662, ~3 keV electronic noise; cusp = best ENC, trapezoid's
  flat-top pays ballistic-deficit; cusp stays a Python benchmark by choice.
- **Core principle enforced throughout:** every feature is built from REAL PHYSICS (never
  hand-add a tail/line/number); every physics claim is MC-verified; Codex (and an adversarial
  Claude agent) cross-verify at important junctures. Many effects turned out honestly **small**
  for this camera — reported as such, not inflated.

## Handoff — where work stood (2026-10-02 evening, moving to another computer)
- Done: the Gcam.Wpf → GCAM Studio migration (Gcam.Wpf deleted, TODO-12) and physics TODO-14/17/18/20/21/23/26
  (Findings 57–61). Open (see `docs/AGENTS.Todo.md`, each with its PLAN and review in `docs/`):
  - **TODO-27** evidence refresh — **done 2026-10-02** (Findings 63; 12 author decisions ER-1…12 in the plan; MC quotes now
    carry seed spreads; driver and aggregates in `samples/evidence/`). Open question: PR-SENS-02's "~50 counts" gate.
  - **TODO-29** clean-room wording pass — **done 2026-10-02** (`PLAN.Docs.CleanRoom.md`); README desktop screenshot still
    shows the old preset label "(original rig)" → retake on the author's go.
  - **TODO-30** absolute ambient background — reference plan (`PLAN.Physics.AmbientBackground.md`); **every MC result so
    far is ideal-environment** (no ambient background, or background relative to the source). Codex turn 1 on the
    author's go.
  - **TODO-19** Anger-type SiPM readout — review done (`PLAN.Physics.RigReadout.Review.md`); next: decisions after review.
  - **TODO-25** joint depth likelihood — reviewed (Findings 62): beats the sharpest plane at 60 s; gate "not yet"
    (10 s wrong-maximum rate, count-scaled model budget, model mismatch still open).
  - **TODO-28** test documentation + automatically generated Test Result report — follow the procedure from the
    author's other project (ask the author where it is; adopt the process only, copy no content).
- Codex conversations, worktrees (`C:\gw\…`) and probe outputs (`%TEMP%\gcam-*`) were local and do not travel: start
  new implementer turns from the review files. Procedure, guard block and the substitute-subagent rule:
  `docs/AGENTS.Planning.md`.
- **Confidentiality (author, 2026-10-02):** this is a clean-room public repo. Never write specifics of the author's
  former-employer instrument (readout network, thresholds, LUT method, digitisation, pitch, reflector thickness, …)
  into any file, commit, plan, review or implementer prompt; model readout choices from published practice with
  defaults chosen by physics / measurement. Already-public generic statements (four-channel Anger-type readout, GAGG)
  stay as they are.

## Working style
Design-first discussion before coding; give options with a recommendation, then build one clean
pass and **show a visual** (ASCII map or a matplotlib PNG). Honest self-correction from the data.
Use Codex + an adversarial Claude reviewer for "did I really finish / is this right?" moments.
**Delegation (since 2026-10-01):** Claude plans and verifies, Codex (`gpt-6.1-sol`) implements — reference plan →
Codex's measured review → "Decisions after review" → implementation in the same Codex conversation (`resume`);
desktop UI tests last. Procedure, call rules and cautions: `docs/AGENTS.Planning.md`.
**Commit messages (author, 2026-10-02):** end with the `Co-Authored-By: Claude …` line only — **never add a
`Claude-Session:` link** (or any other claude.ai session/conversation URL) to a commit, PR or file: the repo is public and
the sessions hold private context. Earlier commits keep theirs (history is not rewritten); those sessions are never shared.
**Verify inherited "it's done" claims against the actual code before propagating them** (a docs
pass once trusted a summary's "CLI complete" and was wrong — 39 commands, not 23).

## ⚠ Machine-specific — re-establish per computer, do NOT trust these paths verbatim on a new machine
The physics/code above is portable; the toolchain paths below are for the **original machine only**.
On a new computer, find the local equivalents (and update this section, or keep them in local memory):
- **cocotb / matplotlib:** python.org **Python 3.13** (cocotb 2.0.1). Windows-Store Python's `python311.dll` is VPI-denied.
- **RTL:** Icarus (iverilog/vvp) + **OSS CAD Suite** (yosys + nextpnr-ecp5) via scoop — needs BOTH
  `bin` AND `lib` on PATH. nextpnr is Lattice-only (ECP5 as an Fmax proxy). Vivado not installed
  (`rtl/vivado_trap.tcl` ready for exact Artix-7 Fmax whenever available).
- **codex exec (cross-verification):** run FOREGROUND with `< /dev/null`, read-only (`-s read-only --skip-git-repo-check`),
  ONE topic per call.
- **WPF** can't be GUI-tested headless — verify it *compiles* (build `src/Gcam.Studio`; the legacy `src/Gcam.Wpf` was
  removed in TODO-12 (2026-10-02); last present at `85b2ed1`).
  GCAM Studio (`src/Gcam.Studio`) is driven through UI Automation instead — see
  `docs/AGENTS.Studio.md` ("Verifying a UI change").
