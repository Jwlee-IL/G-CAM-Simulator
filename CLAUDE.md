@AGENTS.md

# Gcam — session context (portable memory)

This file carries the **cross-session context that isn't otherwise in the repo**, so work
survives moving to another computer. It is git-tracked and **auto-loaded by Claude Code on
any clone** (the machine-local auto-memory under `~/.claude/…/memory/` does NOT travel).
Architecture + build/run is in `@AGENTS.md` above; per-theme quantitative results are in
`docs/AGENTS.Findings.md` (themes 1–68). Keep answers to the author in **Korean**.

## What this is
**Gcam** — a personal C#/.NET 9 Monte Carlo simulator for **coded-aperture gamma-source
localization** (Cs-137 + multi-isotope), modelling a fixed, lab-mounted coded-aperture camera
of the class the author has hands-on experience with: rank-7 tungsten MURA mask (2×2 mosaic, ~10 mm) → 12×12
crystal flood map → ADC peak detection → cross-correlation decode. 66 themes of work;
`Gcam.sln`, CLI `montecarlo <sub>` (study sub-commands; `montecarlo help` lists them), GCAM Studio (the WPF viewer; the legacy Gcam.Wpf was removed in TODO-12 (2026-10-02); last present at `85b2ed1`), and a SystemVerilog +
cocotb RTL front-end path.

## Key decisions / context (not derivable from the code or git history)
- **Isotope scope:** core Cs-137 / Co-60 / Co-57, plus Ir-192 as the industrial reference source (theme 51; no
  cascade model) (122→1332 keV, cascade sum, low line under
  high-E Compton).
- **Reference readout (author, 2026-10-02):** the conventional design — scintillator pixels matched to the SiPM array
  1:1, with dead regions between pixels; the array read through **four 14-bit ADC channels**, position from the
  relative-signal (Anger-type) formula, crystal from the flood map. So pile-up is array-wide and moves the position too. The engine
  models this readout as a study (theme 68, EV-36, `ReadoutStudy`; default stays the direct crystal assignment, D-57); Studio
  integration is TODO-40.
  The reference scintillator is **GAGG** — GAGG is the engine default by design; the other scintillators are what-if comparisons.
- **The classic problem of this camera class** is 662-keV window contamination — Co-60 downscatter
  reading as Cs. Gcam's crystal-Compton **spectral** lever (per-pixel stripping, themes 16–17, 26) recovers
  the Cs count; the **spatial** lever holds only up to Co:Cs ≈ 2:1 once the crystal uses physical GAGG cross
  sections (theme 52 — the earlier, stronger result rested on an over-absorbing crystal model). Its cost is measured
  (TODO-35, theme 65, D-42…D-45): a √(Co counts) detection limit, a side-window stripping reference (gain-robust), a
  separately calibrated trust statistic for stripped images, claims up to 10 µSv/h of Co-60 at the head.
- **Productization** (theme 22, design-only — not being built): GAGG:Ce,Mg, 16×16 @1 mm, D=55,
  15 mm crystal, rank-7, non-cyclic; weight is the SHIELD not the crystal; SiPM thermal drift is
  an **energy-window** issue, not a position issue.
- **Requirement set (2026-10-01, design-only)** — `docs/VV.Gcam.{Overview,URS,PRS,Evidence,Limitations,Decisions}.md`
  (+ `VV.Studio.{SRS,SDS}` for the viewer). Author decisions with reasons are logged in
  `VV.Gcam.Decisions.md` (D-01…D-57); headlines:
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
- **Confidentiality (author, 2026-10-02):** this is a clean-room public repo. Never write specifics of the author's
  former-employer instrument (readout network, thresholds, LUT method, digitisation, pitch, reflector thickness, …)
  into any file, commit, plan, review or implementer prompt; model readout choices from published practice with
  defaults chosen by physics / measurement. Already-public generic statements (four-channel Anger-type readout, GAGG)
  stay as they are.

## Where work stands
Task state lives in **`docs/AGENTS.Todo.md`** (each row links its plan; the plan's `Status:` line is the detail) — do not
copy it here, it goes stale. Only what the Todo does not hold:
- **Ambient background (TODO-30, done 2026-10-04):** the evidence register now quotes an absolute terrestrial field next to
  every ideal value it re-measured (EV-34); results not re-measured are still ideal-environment best cases.
- Waiting for a free desktop (author's go): the desktop UI scenarios after TODO-30's Studio default and fixed spectrum
  axis (list in `samples/evidence/results/ambient-baseline-v1-turn10.md`), and the README desktop screenshot that still
  shows the old preset label "(original rig)" (TODO-29). PR-SENS-02's count gate is settled (measured gate, EV-34).
- Hand-off material in the repo: TODO-25 probe and raw results incl. the finished 10 s v3 run, **not yet analysed** (`samples/evidence/depthlik/`).
- Codex conversations, worktrees (`C:\gw\…`) and probe outputs (`%TEMP%\gcam-*`) are machine-local: start new
  implementer turns from the plan Status and the review / turn-report files. While Codex is unavailable, turns run as
  substitute Claude subagents (`docs/AGENTS.Planning.md`).
- Test records (TODO-28, done 2026-10-08): catalog `docs/VV.Tests.md`, generated `docs/VV.Tests.Results.md`, milestones in
  `samples/testing/records/<Mn>/`. A new milestone is collected by the planner on a clean tree (commands in the archived
  plan's turn 4 report; cocotb needs `--python py -3.13` on the original machine).

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
**Commit identity (author, 2026-10-05):** commits in this repo are authored as
`Jwlee-IL <74895590+Jwlee-IL@users.noreply.github.com>` — never a machine's global git identity. On a new computer set it
per repository (`git config user.name` / `user.email`, local, not `--global`) and check `git log -1 --format='%an <%ae>'`
before the first commit.
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
