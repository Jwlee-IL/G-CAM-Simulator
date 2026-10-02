# PLAN.Physics.CascadeEmission — per-decay list-mode emission with correlated cascade gammas (TODO-14)

Scope: GCAM Studio's list-mode source emits each line photon as an independent event. A Co-60 decay emits 1173 and
1332 keV together (Na-22: two back-to-back 511 keV plus 1275 keV); with one decay → one arrival time, true coincidence
summing would come out of the existing pile-up / measurement stage instead of being absent. Procedure:
[AGENTS.Planning](AGENTS.Planning.md).

Status: **reference plan** 2026-10-02 — implementer: a substitute Claude subagent (Codex out of credits).

## What exists (checked, 2026-10-02)

| Item | Where | Fact |
|---|---|---|
| Decay schemes | `Gcam.Simulation/DecayScheme.cs` | Cs-137, Co-60, Na-22; `Sample` returns the photons of one decay. **Co-60's W(θ) (A₂ ≈ 0.10) is approximated as independent isotropic** in the code although the doc comment describes it — *verify*; Na-22's 511 pair is back-to-back |
| Cascade study | `CascadeSummingStudy`, Findings theme 44 | sum peak ∝ ε², measured in the rig's near geometry |
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
