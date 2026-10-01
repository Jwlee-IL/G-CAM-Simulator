# Gcam — coded-aperture gamma camera simulator

[![CI](https://github.com/Jwlee-IL/G-CAM-Simulator/actions/workflows/ci.yml/badge.svg?branch=MainRoot)](https://github.com/Jwlee-IL/G-CAM-Simulator/actions/workflows/ci.yml)
![.NET 9](https://img.shields.io/badge/.NET-9-512BD4)
![SystemVerilog + cocotb](https://img.shields.io/badge/RTL-SystemVerilog%20%2B%20cocotb-555)

A Monte Carlo re-creation, in C# / .NET 9, of a **coded-aperture gamma camera** I worked on in industry
(2017–2024): a gamma source shines through a **tungsten MURA mask**, the mask's shadow lands on a
**12×12 scintillator array**, and decoding that shadow tells you where the source is. The hardware
version found its sources by physically moving an isotope around the lab; here every geometry, decoder and
detector choice can be swept in software — and the signal chain goes down to **SystemVerilog checked
bit-for-bit against the C# model**.

![Cyclic vs non-cyclic decoding](samples/cyclic_vs_noncyclic.png)
<sub>Localization error over a grid of source positions (black = found within 3 mm), both decoders searching the
same ±18 mm grid — wider than one 19.7 mm mask period. Classical cyclic decoding assumes the shadow repeats, so a
source can land on any period replica (errors ≈ 19 mm, even inside the fully-coded field, cyan box) and sources
outside it ghost to the opposite side; finite-mask (non-cyclic) decoding breaks that ambiguity: 360/625 positions
vs 172/625. Hand-held head: rank-7 MURA, 16×16 × 1 mm pixels, mask–detector 55 mm, source plane 100 mm from the mask.
Plot: <code>samples/plot_sweep.py</code>.</sub>

### 한국어 요약
산업 현장에서 개발에 참여했던 부호화 구경(coded-aperture) 감마 카메라를 소프트웨어로 다시 만든 개인
프로젝트입니다. 광자 수송 몬테카를로 → 텅스텐 MURA 마스크 → 12×12 섬광체 → 복원(상호상관·MLEM)까지 전
과정을 구현했고, 신호 처리 회로는 SystemVerilog로 작성해 C# 모델과 **비트 단위로 같은지** cocotb로 검증합니다.
뷰어(GCAM Studio)는 계층 경계를 컴파일러가 강제하는 MVVM 구조입니다. 자세한 물리 해설은
[`docs/PAPER.ko.md`](docs/PAPER.ko.md).

## What it demonstrates

| | |
|---|---|
| **Physics engine** | Photon transport with directional biasing (unbiased vs. 4π, ~100× fewer photons), Klein–Nishina Compton in the crystal, tabulated tungsten / scintillator cross sections, cross-correlation and **MLEM** decoders. One JSON config describes a scenario; ~40 CLI studies sweep it. |
| **Tests that check physics, not snapshots** | 300+ .NET tests. Each transport stage is held to a closed form — biased-source weight = the detector's solid angle, crystal stopping 1 − exp(−μt/cosθ), mask transmission = open fraction + tungsten leak, Compton energy conservation, decoder peak on an analytic shadow — with k·σ tolerances that follow the sample size; study-level tests assert physics (MLEM is non-negative and out-resolves cross-correlation, ghosts appear outside the fully-coded field) rather than pinning output numbers. A mutation check (Poisson off-by-one, half-pixel flood origin, slant path ignored) fails each one. |
| **Hardware path** | CR-RC⁴ / trapezoidal shapers and a baseline restorer in SystemVerilog. The RTL (cocotb + Icarus) and the native C# `Waveform` model (xUnit golden values) are each held **bit-exact** to one shared integer reference, both in CI. Pipelining raised Fmax from 59 to 119 MHz (ECP5, nextpnr timing). |
| **Viewer** | GCAM Studio: WPF + CommunityToolkit.Mvvm, split into `Studio.Core` (no WPF) → `Studio.Services` (only layer touching the engine) → `Studio` (views), so the boundaries are compiler-enforced; ViewModel tests plus a UI-automation harness with independent oracles. |
| **Self-correction on record** | When the crystal model moved to physical cross sections, earlier multi-isotope results got weaker; they were re-run and revised in place ([theme 52](docs/AGENTS.Findings.md#52-crystal-attenuation-from-tabulated-cross-sections--crystalmaterial-2026-10-01)), not left standing. |

## Headline results

| Result | Condition |
|---|---|
| Non-cyclic decoding roughly **doubles the correctly localized area** (360 vs 172 of 625 positions within 3 mm) | hand-held head, ±18 mm search grid, figure above (282 vs 148 on the 12×12 lab rig) |
| **MLEM separates two sources 2 mm apart** (≈1.1° seen from the mask); cross-correlation still merges them at 3 mm | ideal, high-count (800 k photons), source plane 100 mm from the mask |
| The real instrument's long-standing problem — **Co-60 downscatter counted as Cs-137** — is reproduced; per-pixel spectral stripping removes it from the 662 keV window, while spatial separation alone holds only up to Co:Cs ≈ 2:1 | GAGG crystal, physical cross sections |
| C# ↔ RTL **bit-exact** on all three shaper paths; pipelined shaper **59 → 119 MHz** | cocotb + Icarus; ECP5 nextpnr as the Fmax proxy |

The evidence behind these numbers — conditions, limits, reproduce command and, where one exists, the test that pins
it: [`docs/VV.Gcam.Evidence.md`](docs/VV.Gcam.Evidence.md) (EV-01, EV-11, EV-15, EV-17).
The full working log of all 54 study themes: [`docs/AGENTS.Findings.md`](docs/AGENTS.Findings.md).

## A 10-minute tour for reviewers

1. [`src/Gcam.Decoding/MlemDecoder.cs`](src/Gcam.Decoding/MlemDecoder.cs) — the MLEM update, with its approximations stated up front.
2. [`tests/Gcam.Tests/MlemTests.cs`](tests/Gcam.Tests/MlemTests.cs) and [`PipelineTests.cs`](tests/Gcam.Tests/PipelineTests.cs) — what "testing physics" looks like (e.g. `Biasing_IsUnbiasedVersus4Pi`).
3. [`src/Gcam.Masks/MuraGenerator.cs`](src/Gcam.Masks/MuraGenerator.cs) — MURA construction from quadratic residues.
4. [`rtl/crrc_shaper.sv`](rtl/crrc_shaper.sv) + [`rtl/test_crrc.py`](rtl/test_crrc.py) — gateware and the cocotb bench that holds it to the shared integer reference.
5. [`src/Gcam.Studio.Core/ViewModels/MainViewModel.cs`](src/Gcam.Studio.Core/ViewModels/MainViewModel.cs) and [`docs/DESIGN.Architecture.md`](docs/DESIGN.Architecture.md) — the viewer's layering.
6. [`docs/VV.Gcam.Overview.md`](docs/VV.Gcam.Overview.md) — the requirement set below, on one page.

## Design documents (V&V)

If the simulated camera were made into a hand-held product, what would it have to do — and how much can the simulator
already show? The `VV.*` documents answer that the way a regulated product's requirement set does (structured after
IEC 62304, no compliance claimed), and they are self-contained:

| Document | What it holds |
|---|---|
| [Overview](docs/VV.Gcam.Overview.md) | the whole set on one page — **start here** |
| [URS](docs/VV.Gcam.URS.md) | 19 user needs, use conditions from standards, real industrial reference sources, benchmark imagers |
| [PRS](docs/VV.Gcam.PRS.md) | 51 product requirements, each with its evidence and grade (simulated / analytical / standard / open), traced to the needs; Korean law |
| [Evidence](docs/VV.Gcam.Evidence.md) | 33 simulation and design-model results behind those numbers, each with the command that reproduces it and, where one exists, the test that pins it |
| [Limitations](docs/VV.Gcam.Limitations.md) | 9 things the concept cannot do, every fix and its cost |
| [Decisions](docs/VV.Gcam.Decisions.md) | 38 decisions with the author's reasons, and what is still open |
| [Studio SRS](docs/VV.Studio.SRS.md) · [SDS](docs/VV.Studio.SDS.md) · [V&V](docs/VV.Studio.md) | the engineering viewer as a software item: requirements, design record, test traceability, UI-automation validation |

## Build & run

```bash
dotnet build Gcam.sln -c Release
dotnet test  Gcam.sln -c Release          # engine physics + Studio ViewModels + service layer + UI oracles

# single scenario -> flood map + reconstruction + source estimate (ASCII)
dotnet run --project src/Gcam.Cli -c Release -- samples/scenario.json

# list the study sub-commands (sweep, mlem, compton, depth, thermal, pileup, ...)
dotnet run --project src/Gcam.Cli -c Release -- help

# MVVM viewer (Windows)
dotnet run --project src/Gcam.Studio -c Release

# RTL: Icarus Verilog + cocotb 2.x
python rtl/run_cocotb.py
```

## Limitations

- **Not validated against measured data.** Correctness rests on physics invariants, tabulated cross sections
  (NIST / xraylib) and independent cross-review — not on a comparison with the real camera's recordings.
- The default geometry is small and near-field (12×12 × 1 mm pixels, source plane 100 mm from the mask);
  millimetre figures above are for that geometry.
- Many realism effects (thermal drift, pile-up, fabrication tolerance, …) turned out **small for this
  camera**, and are reported that way.

## How it was built

Built with AI coding agents (Claude Code, Codex) as development tools; what to build, the physics decisions
and the verification are mine, and every commit carries the agent's co-author line. Codex and an adversarial
reviewer agent cross-check the important claims — several fixes in the history came from those reviews.

## Where things live

| Path | What |
|---|---|
| `src/` | Core domain, masks, detector / Compton transport, decoders, simulation studies, CLI (`Gcam.Cli`, one class per study group under `Commands/`), GCAM Studio (`Gcam.Studio*`); `Gcam.Wpf` is the legacy viewer being folded into Studio (TODO-06 … TODO-12) |
| `tests/` | xUnit — engine physics and closed-form invariants (shared rigs and k·σ assertions in `Gcam.Tests/Harness`), Studio ViewModels, the service layer against the real engine, UI-automation oracles and opt-in desktop scenarios |
| `rtl/` | SystemVerilog front-end, Icarus + cocotb benches ([`rtl/README.md`](rtl/README.md)) |
| `samples/` | Scenario JSON plus the result CSVs / figures each finding cites |
| `docs/` | V&V documents (above), viewer design notes (`DESIGN.*`), Korean physics write-up (`PAPER.ko.md`), and the working notes for contributors and AI agents (`AGENTS.*`) |
| [`AGENTS.md`](AGENTS.md) | Architecture, pipeline, coordinate frame, full command reference (also the entry point for AI agents) |

## License

Proprietary — **All Rights Reserved**. This repository is published for personal reference only; no
permission is granted to use, copy, modify, or distribute it. See [`LICENSE`](LICENSE).
