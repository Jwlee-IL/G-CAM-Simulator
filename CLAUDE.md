@AGENTS.md

# Gcam — session context (portable memory)

This file carries the **cross-session context that isn't otherwise in the repo**, so work
survives moving to another computer. It is git-tracked and **auto-loaded by Claude Code on
any clone** (the machine-local auto-memory under `~/.claude/…/memory/` does NOT travel).
Architecture + build/run is in `@AGENTS.md` above; per-theme quantitative results are in
`docs/AGENTS.Findings.md` (themes 1–50). Keep answers to the author in **Korean**.

## What this is
**Gcam** — a personal C#/.NET 9 Monte Carlo simulator for **coded-aperture gamma-source
localization** (Cs-137 + multi-isotope), recreating a real coded-aperture instrument the
author previously built in hardware: rank-7 tungsten MURA mask (2×2 mosaic, ~10 mm) → 12×12
crystal flood map → ADC peak detection → cross-correlation decode. 50 themes of work;
`Gcam.sln`, CLI `montecarlo <sub>` (~39 sub-commands), a WPF viewer, and a SystemVerilog +
cocotb RTL front-end path.

## Key decisions / context (not derivable from the code or git history)
- **Isotope scope:** core Cs-137 / Co-60 / Co-57, plus Ir-192 as the industrial reference source (theme 51; no
  cascade model) (122→1332 keV, cascade sum, low line under
  high-E Compton).
- **The real rig's long-unsolved problem** was 662-keV window contamination — Co-60 downscatter
  reading as Cs. Gcam's crystal-Compton **spectral** lever (per-pixel stripping, themes 16–17, 26) recovers
  the Cs count; the **spatial** lever holds only up to Co:Cs ≈ 2:1 once the crystal uses physical GAGG cross
  sections (theme 52 — the earlier, stronger result rested on an over-absorbing crystal model).
- **Productization** (theme 22, design-only — not being built): GAGG:Ce,Mg, 16×16 @1 mm, D=55,
  15 mm crystal, rank-7, non-cyclic; weight is the SHIELD not the crystal; SiPM thermal drift is
  an **energy-window** issue, not a position issue.
- **Realistic front-end defaults** (themes 30–32): AD9648 14-bit/125 MSPS, `tau_rise=1` sample
  (set by Nyquist), intrinsic ~6 % @662, ~3 keV electronic noise; cusp = best ENC, trapezoid's
  flat-top pays ballistic-deficit; cusp stays a Python benchmark by choice.
- **Core principle enforced throughout:** every feature is built from REAL PHYSICS (never
  hand-add a tail/line/number); every physics claim is MC-verified; Codex (and an adversarial
  Claude agent) cross-verify at important junctures. Many effects turned out honestly **small**
  for this camera — reported as such, not inflated.

## Working style
Design-first discussion before coding; give options with a recommendation, then build one clean
pass and **show a visual** (ASCII map or a matplotlib PNG). Honest self-correction from the data.
Use Codex + an adversarial Claude reviewer for "did I really finish / is this right?" moments.
**Verify inherited "it's done" claims against the actual code before propagating them** (a docs
pass once trusted a summary's "CLI complete" and was wrong — 39 commands, not 23).

## ⚠ Machine-specific — re-establish per computer, do NOT trust these paths verbatim on a new machine
The physics/code above is portable; the toolchain paths below are for the **original machine only**.
On a new computer, find the local equivalents (and update this section, or keep them in local memory):
- **cocotb / matplotlib:** python.org **Python 3.13** (cocotb 2.0.1). Windows-Store Python's `python311.dll` is VPI-denied.
- **RTL:** Icarus (iverilog/vvp) + **OSS CAD Suite** (yosys + nextpnr-ecp5) via scoop — needs BOTH
  `bin` AND `lib` on PATH. nextpnr is Lattice-only (ECP5 as an Fmax proxy). Vivado not installed
  (`rtl/vivado_trap.tcl` ready for exact Artix-7 Fmax whenever available).
- **codex exec:** run FOREGROUND with `< /dev/null`, read-only (`-s read-only --skip-git-repo-check`),
  ONE topic per call.
- **WPF** can't be GUI-tested headless — verify it *compiles* (build `src/Gcam.Wpf`).
  GCAM Studio (`src/Gcam.Studio`) is driven through UI Automation instead — see
  `docs/AGENTS.Studio.md` ("Verifying a UI change").
