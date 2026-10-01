# PLAN.Studio.ImagingOptions — detector realism and per-nuclide imaging (TODO-08)

Scope: what `Gcam.Wpf` adds to imaging that Studio lacks — detector realism defaults, gain non-uniformity, decay
cascades, ambient background, per-nuclide energy-window imaging and Compton stripping — and the steps, in two
phases. Builds on [PLAN.Studio.LiveAcquisition](PLAN.Studio.LiveAcquisition.md) (closes its review points R-2, R-5)
and [PLAN.Studio.Spectrum](PLAN.Studio.Spectrum.md).

Status: phase A handed to Codex 2026-10-01; phase B planned (starts after phase A is reviewed).

## What `Gcam.Wpf` does that Studio does not

`ConfigFromScene` / `PrepareLive` / `RenderImaging` in `src/Gcam.Wpf/MainWindow.xaml.cs`:

| Wpf behaviour | Studio today |
|---|---|
| **Entrance absorber 0.15 mm** steel-equivalent (source capsule + detector window), MC-verified: it leaves the Ba K X-rays as a modest bump and fills the photopeak's low-energy tail | absent — bare vacuum geometry |
| **Backing 2 mm** behind the crystal (SiPM, PCB, housing) → the ~184 keV backscatter peak | absent |
| **Pixel gain σ** (default 3 %, seeded fixed pattern) | absent (R-2) |
| **Reflector gap 100 µm** between crystals | absent (editing it is TODO-11; the default belongs here) |
| Decay-scheme cascades (Co-60 1173 + 1332 sum) via the detector's cascade RNG / `DecayScheme` | list-mode emits singles only (R-2) |
| **Background** at a background-to-signal ratio (uncoded pedestal + crystal response at the background energy) | rejected with `NotSupportedException` (R-5) |
| **One imaging channel per isotope**: events inside that isotope's primary-line window, decoded separately, composited; found peaks labelled by isotope | one flood of all events |
| **Compton strip**: per pixel, subtract R·(higher channel) from a lower channel; R calibrated from an H-only run of the scene | absent |

**Correction to TODO-07.** The Spectrum commit and its test describe the Ba K peak being the global maximum as
expected physics. That holds only for the bare geometry Studio uses today; with the entrance absorber `Gcam.Wpf`
always had, the Ba K X-rays are attenuated (steel at 32 keV, μ ≈ 65 cm⁻¹, gives ~38 % transmission through
0.15 mm by the narrow-beam estimate — the MC is the judge). Phase A replaces that assertion with a physics check.

## Decisions (planner, 2026-10-01; the author may override)

| # | Decision | Why |
|---|---|---|
| I-1 | **Detector realism defaults come from `Gcam.Wpf`** (entrance 0.15 mm, backing 2 mm, gain σ 3 %, reflector gap 0.1 mm), passed by Studio explicitly — a Studio `DetectorSettings`, not new defaults inside `SceneConfigBuilder.Build`, so engine tests and studies that call `Build` keep their results. | They were MC-verified for this instrument and silently lost in the move; engine results must not shift. |
| I-2 | **Events keep the true deposit; the detector response is applied once, in one shared "measurement" stage** (pixel gain × deposit, then the chain's `FrontEndModel` smear), used by both the Spectrum and the imaging windows. Gain σ and its seed are detector inputs: changing them marks the acquisition stale (a real detector's gain cannot be changed on recorded data). | One consistent measured energy per event across workspaces; no double gain. |
| I-3 | **Background uses the engine's existing model** (theme 28: BSR × source rate, `BackgroundStudy` spatial profile, crystal response at the background energy, as `EventStreamStudy` builds it) as a second Poisson process merged into the event stream, each event used once. | No new, unverified background physics; R-5 asked for a transported producer, and this one is the engine's. |
| I-4 | **One energy-window setting N** shared by Spectrum and Imaging (owned by the shell, shown in both panels). | The band the user sees on the spectrum must be the window the image uses. |
| I-5 | **Per-nuclide images are chosen with a selector** (All / each isotope in the scene), not a colour composite; found peaks are marked and labelled with the isotope. | Colour-only coding fails SR-A11Y-04; a selector keeps one colormap and the existing measurement tools. |
| I-6 | **Strip ratios R come from H-only calibration acquisitions** of the scene, run on the worker when an acquisition starts (as a real instrument is calibrated with an H-only source), at the same window N. | Same model as `Gcam.Wpf` and the CLI (themes 16–17, 26); its one-pass scalar limit for 3+ overlapping contaminants is stated, not hidden. |

## Phase A — acquisition physics (R-2, R-5, realism defaults)

1. **`DetectorSettings`** in Studio (entrance, backing, gain σ, gain seed, reflector gap) with the I-1 defaults,
   applied by the service when building the acquisition config (clone + mutate, never a hand-written config).
   Shown read-only in the left panel's geometry section except gain σ / seed, which are editable run inputs.
2. **`ListModeSource`**: pass the per-pixel sensitivity (gain pattern) and the decay-scheme cascade exactly as
   `EventStreamStudy` does; emit background events per I-3. Additive; existing engine results unchanged.
3. **Measurement stage** per I-2, used by `SpectrumService` (replace its own smear) and ready for phase B's windows.
4. **Tests (k·σ, real engine):**
   - Ba K band counts with the 0.15 mm absorber vs without: ratio consistent with the MC transmission of that
     absorber at 32–36 keV (state the expectation from an independent narrow-beam calculation and the tolerance);
     replace the TODO-07 "global maximum is Ba K" assertion with this.
   - Backscatter: a peak region near 184 keV appears with the backing and not without.
   - Gain σ = 3 %: the 662 keV in-window fraction drops vs σ = 0, and the measured photopeak FWHM widens
     consistently with σ added in quadrature (within tolerance).
   - Co-60 cascade: a sum peak at 2505 keV whose rate scales ∝ ε² when the source distance changes (as theme 41 /
     `CascadeSummingStudy`), absent for Cs-137.
   - Background: at BSR = 1 the event rate doubles within k·σ and the extra events follow the engine's spatial
     profile and energy response; at BSR = 0 the stream is unchanged bit-for-bit for the same seed.
5. **Docs:** VV.Studio.SRS / SDS / matrix / VV.Studio.Acquisition (present behaviour; history only in the SDS and
   evidence notes), DESIGN.Layout (left-panel detector rows), the Spectrum plan's Ba K note.

## Phase B — per-nuclide imaging and Compton strip (after phase A review)

1. Shared window N (I-4) moves from the Spectrum panel to the shell; both panels bind it.
2. Imaging channels: per isotope in the scene, the events whose measured energy (phase A stage) is inside that
   isotope's primary-line window; decode each; the selector (I-5) picks All (all events, as today) or one isotope;
   found peaks per isotope via `MixedFieldStudy.TopPeaks`, labelled.
3. Compton strip toggle (view setting): calibration per I-6 at acquisition start; per pixel
   `max(0, low − Σ R·high)` before decode; status says when stripping is applied and its scalar-model limit.
4. Tests: a co-located Cs-137 + Co-60 scene recovers the Cs count in the 662 keV window with strip on, consistent
   with the engine's own stripping result for the same geometry (the CLI `compton-strip` / evidence for isotope
   separation), and an off-axis pair separates per channel; window N changes re-filter without a new acquisition.
5. Docs as in phase A, plus SR rows for channels, selector and strip.

**Not here:** editable optics (TODO-09), chain selection (TODO-10), editing reflector gap / SiPM / crosstalk and
depth (TODO-11).
