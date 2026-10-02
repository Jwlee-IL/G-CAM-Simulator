# PLAN.Physics.CsIData — CsI(Tl) transport data for what-if comparison (TODO-21)

Scope: add CsI(Tl) to the engine's crystal material table by the same method as theme 52, so the existing CsI(Tl)
front-end preset can be offered in GCAM Studio. GAGG (the reference crystal) stays the default; CsI is a what-if.
Procedure: [AGENTS.Planning](AGENTS.Planning.md).

Status: **done** 2026-10-02 (Codex, worktree `C:\gw\p21`; two plan corrections below). Findings 59. Planner re-verified: build 0 / 0,
tests 274 / 165 / 78 (+7) / 13 (+14).

The planner registered the task from the TODO row only; the implementer's review
([PLAN.Physics.CsIData.Review](PLAN.Physics.CsIData.Review.md)) served as the reference plan.

## Decisions after review (2026-10-02)

| # | Decision | Basis |
|---|---|---|
| C-1 | Generate with the repository generator (`samples/materials/gen_crystal_tables.py`) and **xraylib 4.3.0**, as theme 52; CsI tuple `("CsI", "CsI", 4.51, "CsI:Tl")`; Tl omitted as an activator (as NaI:Tl), documented as a host-material approximation | review 1–2; author approved the scoped install (`%TEMP%\gcam-csi-data\packages`, no global change) |
| C-2 | Base grid plus K-edge pairs for I (33.17 keV) and Cs (35.98 keV) by the generator's own rule; 800 keV spline limit and the existing analytic extension above it; coherent and pair production excluded as for every material | review 3–6 |
| C-3 | Full-precision JSON and a provenance / check report kept as evidence; only the CsI row copied into `CrystalMaterial` at the existing precision; existing rows untouched (regenerated only for comparison) | review 7 |
| C-4 | Checks: NIST CsI totals at 300 / 600 / 800 / 1000 / 1250 keV (like with like; ≤ 800 keV add coherent for the comparison), the existing 0.92–1.0 bound only if the measured residuals support it — stop if not; K-edge jumps measured per edge (no borrowed 2× threshold); 661.7 keV anchor; grid / array / clamping checks | review checks |
| C-5 | Studio: `FrontEndMaterials` maps `CsI(Tl) → CsI` (no exclusion); tests by name, not index; SR-CHAIN-03 / SDS / VV.Studio.Waveform updated; GAGG first and default | review table, Waveform D-3 |

## Correction (2026-10-02, after the implementer stopped)

C-4's "existing 0.92–1.0 bound" was **borrowed from the GAGG test** — exactly what the procedure forbids. At 300 keV
CsI's photo + incoherent μ/ρ is 0.9186 of NIST's total because CsI's coherent share there is larger than GAGG's; adding
xraylib coherent gives 0.181803 vs NIST 0.1818 (+0.0015 %). The table is right; the bound was wrong.

Replacement (C-4'): (a) **like with like** at the NIST nodes ≤ 800 keV — photo + incoherent + coherent against NIST's
total, tolerance derived from the NIST table's stated precision and the generator's measured agreement on CsI (state the
derivation); (b) above 800 keV, the model / NIST ratio band from **CsI's own omitted components** (coherent + pair
production at each node, from the same sources), plus the table's precision — not from another material. All other C-4
checks unchanged.

## Correction 2 (2026-10-02, after the implementer stopped again)

C-4' (b) assumed the model above 800 keV differs from NIST only by the omitted coherent and pair terms. It also carries
the **extension's own approximation error** (photoelectric power-law extrapolation from 600–800 keV, the method every
material in theme 52 uses): measured −0.353 % against NIST photo + incoherent at 1250 keV for CsI. That is a property
of the documented method, to be measured and recorded — not hidden in a wider band and not "fixed" for CsI alone.

Replacement for > 800 keV (C-4''): assert what is guaranteed — the extension reproduces its documented formula (already
checked to 1e-14) and the model stays below NIST's total (omissions and the extension only remove attenuation);
**report** the residual against NIST photo + incoherent at 1000 / 1250 keV in the evidence report and the Findings entry
as the extension's measured approximation error. The ≤ 800 keV like-with-like check stays as derived.

Also: the evidence JSON must not sit where the engine's scenario test scans `samples/**/*.json`; place it so that test
needs no change (e.g. a non-scenario extension or folder the test already excludes), never weaken the scenario test.
