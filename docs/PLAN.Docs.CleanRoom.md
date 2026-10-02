# PLAN.Docs.CleanRoom — remove former-instrument attribution from the docs and code comments (TODO-29)

Scope: the repository is a clean-room personal reimplementation (README). A planner sweep on 2026-10-02 found text that
reads as a former employer's product description, its unsolved defects, its internal countermeasures or its
implementation choices, and a requirement set that can read as a company product plan. Rewrite that text as generic,
published practice; keep the physics, numbers and conclusions. Procedure: [AGENTS.Planning](AGENTS.Planning.md).

Status: **done** 2026-10-02 by the planner (doc work). Applied C-1 … C-6 across docs, plans, samples, RTL scripts and
code comments; preset renamed "CSP + CR-RC (200 ns)" (headless renders regenerated: only the label changed). Also added,
at the author's request, an **ideal-environment notice** (no ambient background; best-case bounds) to README, Overview,
PRS, Evidence §1 ("Ideal conditions and background", with what is expected to change under background), EV-07, LIM-06
and PAPER, and reworded PR-SENS-02 to net source counts / a significance gate — measured in TODO-30. Kept on purpose:
code identifiers `Rig(...)` / `Rigs.cs` in tests (renaming breaks nothing but buys nothing), file names such as
`scenario_orig_gagg.json` (reproduce commands), git history (author). Open: the README desktop screenshot still shows
the old preset label — retake on the author's go (desktop).

## Rule

Describe the camera class, never "the rig / the real product / what the hardware used". A problem is stated as a known
problem of pixelated scintillator coded-aperture cameras (textbook or published), not as something an instrument
"never solved" or "fought with X". A parameter is a default chosen by physics or a datasheet, not "the original rig's".
Already-public generic statements stay (rank-7 MURA, 12 × 12 array, GAGG, 1:1 SiPM matching, four-channel Anger-type
readout), as decided by the author on 2026-10-02. Git history is not rewritten (author, 2026-10-02).

## Decisions (author, 2026-10-02)

| # | Item (planner sweep; *verify* every location, the list is a starting point) | Decision |
|---|---|---|
| C-1 | Product description: `samples/camera_parallax_study.py:3` ("The user's real product: 9" LCD … USB camera"), `docs/PAPER.ko.md` §9 "재현 대상(실기 — 과거 장비)" (fixture-mounted, 9-inch rear LCD, front USB camera, MPPC, per-pixel LLD/ULD), `docs/VV.Gcam.URS.md` §1 lines ~25–26 | Remove the product description. Where a premise is needed: "a fixed, lab-mounted coded-aperture setup with a video overlay" (generic) |
| C-2 | Unsolved defects / internal countermeasures: "Co-60 read as Cs-137 — never solved on the rig" (URS §1 and UN-03, Overview table, Limitations ~122, AGENTS.md ~227, CLAUDE.md); "cooling was a headache", "bias compensation was the only thermal measure the hardware used" (PAPER ~207, Findings 36 ~1090 / ~1113, `samples/thermal_motion_study.py:1–3`); "shaking wrecked the reconstruction" | Restate as known problems of the camera class (high-energy downscatter into a lower line's window; SiPM gain drift with temperature; motion blur of the coded shadow; ghosts of cyclic decoding). Drop "never solved", "the only measure used", "headache" and similar first-hand history |
| C-3 | Implementation choices attributed to the instrument: Findings 31 ~262 and `ComptonCrystalDetector.cs:9` ("the old rig's per-pixel LLD/ULD"), `CrystalDetector.cs:6`, Findings 43 ~1274 ("Gaussian — what the real hardware used"), Findings 34 ~1002 ("matches the real instrument's laser module"), Findings 35 ~1070 and `PLAN.Physics.RigReadout(.Review)` ("ceramic — the real rig"), `FrontEndParts.cs:48` preset "CSP + CR-RC (original rig)" (shown in Studio; also `PLAN.Physics.CrrcPresets.Review`, Studio docs / tests that quote the name), `SimulationConfig.cs:154,353` ("old rig used 7", "old rig was ~50–80") | Remove the attribution: "per-crystal LLD/ULD (a common pixelated readout)", "Gaussian peak fit (a common choice)", "an external rangefinder", reflector material as one of the published options (ESR, PTFE, BaSO₄, ceramic …), preset renamed "CSP + CR-RC (200 ns)" (update every test, doc and render that quotes it; regenerate renders only if headless) |
| C-4 | Requirement set reads as a company product plan: `VV.Gcam.Overview`, `VV.Gcam.URS` ("the needs start from the original instrument's problems") | Add at the top of Overview and URS: a personal design exercise by the author, not any company's product plan; requirements derived from public standards, published same-class product data and this simulator's results. URS §1 rewritten per C-2 ("problems of this camera class") |
| C-5 | Scenario naming "original rig" / "lab rig" (Evidence, PRS, Findings, tests' `Rigs.cs`, Studio strings, `scenario_orig_gagg.json` description) | Rename in prose to "reference lab geometry" (and "reference lab geometry, GAGG"); file names and code identifiers may stay where renaming would break reproduce commands — say which |
| C-6 | TODO-19 wording ("Model the original rig's readout", plan title) | Done by the planner 2026-10-02 (TODO row and plan title now say "a conventional four-channel Anger readout"); the plan body follows in TODO-19's own decisions |

## Steps
1. Sweep (grep the whole tree, not only the listed lines: rig, original, real instrument / product / hardware, old,
   실기, 원래 장비, 과거 장비, 거치형, never solved, headache …) and apply C-1 … C-5; report every hit kept and why.
2. Build + tests (renamed preset), headless renders if a visible string changed.
3. Planner audit: re-grep, read the diff, confirm no physics number or conclusion changed.

Done when: no tracked file attributes a design choice, defect or countermeasure to a former instrument or product,
and the clean-room statement is backed by the text.
