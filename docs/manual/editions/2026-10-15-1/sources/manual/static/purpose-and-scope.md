#### What GCAM Studio is

GCAM Studio is the engineering viewer of Gcam, a Monte Carlo simulator of a coded-aperture gamma camera: photon
transport through a tungsten MURA mask onto a scintillator array, and decoding that finds the source. Studio stays the
simulator's viewer; it is not the user interface of the hand-held product concept and is not grown into one.
Sources: `README.md`; `docs/VV.Gcam.Decisions.md` D-34.

[[notice:N-SIM]]

It runs a live list-mode acquisition — Start, Stop, Continue and Reset over a preset live time, with the physical inputs
locked until a confirmed Reset — and shows the result in four workspaces: Imaging, Spectrum, Waveform and Detector. Every
count is one independently transported event, so scatter inside the crystal is part of the image. Scene, optics and
readout inputs sit in the left tabs; inputs changed rarely (seed, detection chain, detector and environment) are in the
Options dialog. Sources: `README.md` (What it demonstrates); `docs/VV.Studio.SRS.md` SR-RUN-12, SR-RUN-16, SR-RUN-26.

[[notice:N-NOT-SAVED]]

#### Who this manual is for

Readers of this repository who want to drive the viewer. The chapters of parts 1–3 are generated from automated runs
that drove one build of Studio: their steps, screen labels and figures come from that run, and a chapter whose run did
not pass on that build is replaced by a notice. Each numbered figure label pairs the control's name on
screen with what it is for ("Preset: choose a geometry"); those purposes are written by hand from the repository's
documents, each with its sources in `docs/manual/purposes.json`. Chapters 1.1 and 5.2 are written by hand from the repository's documents. This edition covers the window ([[chapter:1.2]]), sources ([[chapter:2.1]]), optics and detector settings
([[chapter:2.2]]), the acquisition controls ([[chapter:2.4]]), measurements on the images ([[chapter:3.1]]), the
spectrum ([[chapter:3.4]]) and how to read the results ([[chapter:5.2]]).

#### Prerequisites

- Windows: Studio is a WPF application (`net9.0-windows`). Source: `src/Gcam.Studio/README.md`.
- The .NET 9 SDK, then from the repository root `dotnet build Gcam.sln -c Release` and
  `dotnet run --project src/Gcam.Studio -c Release`. Source: `README.md` (build and run commands).
- Units on screen: positions and distances in mm, activity in µCi, energy in keV, live time in s. Source:
  `docs/VV.Studio.SRS.md` (inputs and outputs).
