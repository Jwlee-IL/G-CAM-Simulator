# PLAN.Physics.CascadeEmission — per-decay list-mode emission with correlated cascade gammas (TODO-14)

Scope: GCAM Studio's list-mode source emits each line photon as an independent event. A Co-60 decay emits 1173 and
1332 keV together (Na-22: two back-to-back 511 keV plus 1275 keV); with one decay → one arrival time, true coincidence
summing would come out of the existing pile-up / measurement stage instead of being absent. Procedure:
[AGENTS.Planning](AGENTS.Planning.md).

Status: **done** 2026-10-02 (substitute Claude subagent, worktree `C:\gw\w14`); Findings 61. Planner re-verified: build 0 / 0,
tests 291 / 179 / 82 (+7) / 13 (+14). The cascade slope test's budget was raised (5e6 → 2e7 decays) instead of its range —
its old seed sat 1.7σ from the bound.

## What exists (checked, 2026-10-02)

| Item | Where | Fact |
|---|---|---|
| Decay schemes | `Gcam.Simulation/DecayScheme.cs` | Cs-137, Co-60, Na-22; `Sample` returns the photons of one decay. **Co-60's W(θ) (A₂ ≈ 0.10) is approximated as independent isotropic** in the code although the doc comment describes it — *verify*; Na-22's 511 pair is back-to-back |
| Cascade study | `CascadeSummingStudy`, Findings theme 44 | sum peak ∝ ε², measured in the reference near geometry |
| List-mode | `ListModeSource` | one biased photon per history (directional biasing toward the detector, weight ≤ A/(4πz²), rejection to unweighted events), Poisson arrivals at the running rate; no decay grouping |
| Pile-up | Studio spectrum / waveform | array-wide summing within the resolving time already exists |

## Proposed design (reference — verify / improve)

| # | Proposal | Status |
|---|---|---|
| E-1 | A **decay-level history**: sample one decay (all photons, with angular correlation), transport every photon, emit the detected ones with one arrival time — as one summed event at the arg-max pixel, or as separate pixel hits with the same time so the existing pile-up stage sums them | *verify* which matches Studio's readout (one array-wide channel) |
| E-2 | **Biasing**: directional biasing aims one photon; the partner's direction follows W(θ) relative to it. Options: bias each photon in turn with correct weights, or unbiased 4π decays for cascade isotopes (cost). The estimator must stay unbiased — derive the weight algebra and measure it against 4π | *verify*: derive and measure |
| E-3 | W(θ) for Co-60 (4→2→0, 1 + a₂cos²θ + a₄cos⁴θ) instead of isotropic, with a test | *verify* the coefficients from a cited source |
| E-4 | **Magnitude first:** the sum-peak fraction at Studio's default geometry (1 m) and near (300 mm). If negligible at 1 m, say so plainly — correct physics, but the claim must match the number | proposed |
| E-5 | Tests: per-decay photon counts and energies, W(θ) histogram vs formula (derived k·σ), biased vs 4π sum-peak rate agreement, arrival-time identity of decay partners | proposed |

## Steps

1. Review + measurement (no repository code): E-1 … E-5, the bias derivation, cost, the magnitude at 300 / 1000 mm;
   write `docs/PLAN.Physics.CascadeEmission.Review.md`.
2. Decisions after review. 3. Implement (engine + Studio), tests, Findings entry.

## Decisions after review (2026-10-02)

Review: [PLAN.Physics.CascadeEmission.Review](PLAN.Physics.CascadeEmission.Review.md) (substitute implementer).
Measured sum fraction (both Co-60 gammas deposit / detected decays): **2.0 × 10⁻⁶ at 1000 mm, 2.2 × 10⁻⁵ at 300 mm** —
about 50× below random pile-up at 500 µCi; negligible at 1 m, visible only within ~100 mm. The author chose to
implement it anyway as correct physics, reported at its true size.

| # | Decision | Basis |
|---|---|---|
| K-1 | **One event per decay** (E-1 form a): total deposit of all the decay's gammas at the largest-deposit pixel over the merged sites — the plan's "separate hits summed by pile-up" option is rejected (it would make true coincidence depend on the Spectrum pile-up toggle, and Imaging never sums) | review E-1, R-7 |
| K-2 | Unbiased weighting: pick one emitted gamma at random, aim it, draw the partner from W(θ), weight `n·w_k / (n̄·H)`; source choice ∝ activity × n̄ (rate code and acceptance unchanged; bound +0.1 % Co-60, +6.5 % Na-22) | review E-2 derivation |
| K-3 | Co-60 W(θ) = 1 + (1/8)cos²θ + (1/24)cos⁴θ sampled by **inverse CDF** (one draw — never rejection on `DefaultRandom`, R-6); `DecayScheme` summary corrected | review E-3, R-6 |
| K-4 | Only Co-60 and Na-22 take the decay path; single-photon isotopes keep today's draws; Ir-192 stays independent (no scheme) | review |
| K-5 | Settle the Na-22 511 keV intensity mismatch (1.798 vs 1.806) from a cited evaluated source; one value in both places | review |
| K-6 | Tests per the review's E-5 plan with derived tolerances; re-measure the sum fraction on the implemented `ListModeSource`; stop/continue equivalence must still hold; Findings entry + theme 44 correction | review |
