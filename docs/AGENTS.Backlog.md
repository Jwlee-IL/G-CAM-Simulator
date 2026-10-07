# AGENTS.Backlog — deferred and undecided work

Scope: work that is **not ready to start** — deferred by choice, waiting for a decision, or ideas worth keeping. Ready
or running tasks are in [AGENTS.Todo](AGENTS.Todo.md); finished tasks are indexed in
[docs/archive](archive/README.md#closed-tasks) and their results are in [AGENTS.Findings](AGENTS.Findings.md). When an
item here is picked up, it moves to the Todo as a new row with a plan; nothing is recorded in both places.

Reorganised 2026-10-07: the old "Open" list held mostly finished items (their results are in Findings themes 26–50)
and the "Done" summary moved to the archive index; only the genuinely open items below were kept.

## Deferred engine physics

| Item | Why it waits / notes |
|---|---|
| **High-energy transport (> 1.33 MeV)** (formerly TODO-31): extend the tungsten μ(E) table beyond its 1332 keV clamp, add pair production to crystal transport (sizeable at 2–3 MeV), lift the dose path's 2000 keV deposit cut; validate against NIST / xraylib | needed for a faithful Tl-208 2614 keV / Bi-214 1764 keV ambient component (TODO-30 review §1); the TODO-30 baseline states the exposure instead (author 2026-10-03) |
| **Realistic head housing for ambient background** (formerly TODO-32): ray transport through the head's shield walls (front plate with mask, four sides, rear) instead of the `ShieldStudy` exp(−μt) surrogate and the top-face-only crystal entry; one housing estimate in place of the bare-crystal / front-only bounds | TODO-30 review §1, §5 (author 2026-10-03); the calibration records use the bounds and the conservative-neighbour rule meanwhile |
| **Background-shape decoding, stage 2** (from TODO-33, D-55): the 662 keV window, the front-only bound and centre sources for the fitted-amplitude MLEM; wiring it into the CLI single run (needs a fixed-time entry) and, if wanted, Studio | stage 1 (open window, bare bound, edge) is validated (EV-35); the full matrix was estimated at days of compute — decide after reading stage 1 (author 2026-10-07) |
| Room / object / operator scatter | only the entrance and backing scatterers are modelled; also the open limit of the side-window stripping comparison (D-43, "before scatter") |
| Scintillator K X-ray escape peak (GAGG Gd, ~43 keV below the photopeak) | a real satellite peak; low–medium impact |
| Non-proportionality as deposit-history-dependent (Compton-split vs photoelectric histories resolve differently) | theme 46 models the intrinsic-resolution component only |
| Per-channel SiPM / preamp gain · PDE · threshold mismatch, microcell saturation, afterpulsing | partly overlaps TODO-19 (four-channel readout, light sharing); decide when TODO-19's decisions are taken |
| Intrinsic activity (LYSO Lu-176, LaBr3 La-138) | nil for GAGG; matters only if those scintillators are chosen |
| Mask K-edge: proper NIST split below 122 keV | documented approximation (70–122 keV under-attenuated); no line sits there and 10 mm W is opaque, so curve fidelity only |
| Compton-stripping response matrix for 3+ overlapping isotopes | the one-pass scalar stripping is exact for a clean pair; a per-pixel isotope × window solve would be exact for more |
| MLEM follow-ons: iteration / noise regularisation; a finite-mask ghost-suppression demonstration | the likelihood depth estimate is TODO-25; iteration counts are now chosen by a stated rule (D-46 … D-48, SR-IMG-07) |
| Deferred by choice (second order): mask tilt (pitch / yaw), mask warping, reflector material | judged second-order in themes 38–39 |

## RTL and front end

| Item | Why it waits / notes |
|---|---|
| Exact Artix-7 Fmax in Vivado | install friction; `rtl/vivado_trap.tcl` is ready; nextpnr (ECP5) gives a proxy (theme 25) |
| Shaping-time / filter sweep on the realistic waveform for the SNR vs pile-up optimum | needs a pile-up-inclusive photopeak fit; theme 30's front-end study has the resolution machinery |

## Productization (theme 22 — design-only; not being built)

| Item | Why it waits / notes |
|---|---|
| Integrate the SiPM / thermal / gain-stabilisation and shield models into the C# pipeline | they are Python design layers on the validated MC; the author is not building the product |
