"""Generate a synthetic ADC waveform (adc.txt) + event truth (truth.csv) for the
peak-detector RTL. Models a scintillator front-end seeing a Cs-137 source:
Poisson-timed gamma interactions, each a bi-exponential pulse scaled by energy
(662 keV photopeak + Compton continuum), on a noisy baseline.

Usage: python gen_stimulus.py [rate_per_s] [n_events] [outdir]
"""
import sys
import csv
import numpy as np

rate     = float(sys.argv[1]) if len(sys.argv) > 1 else 50_000.0   # events/s
n_events = int(sys.argv[2])   if len(sys.argv) > 2 else 2000
outdir   = sys.argv[3]        if len(sys.argv) > 3 else "."

FS          = 100e6            # 100 MHz sample rate
BASELINE    = 200             # ADC baseline (counts)
NOISE_SIGMA = 3.0             # baseline noise (counts RMS)
ADC_PER_KEV = 3000.0 / 662.0  # 662 keV -> 3000 counts
ADC_MAX     = 4095
COMPTON_EDGE = 477.0          # Cs-137 Compton edge (keV)

rng = np.random.default_rng(12345)

# Bi-exponential pulse shape (fast rise, slower decay), peak normalized to 1.
tau_r, tau_d, L = 2.0, 12.0, 80
n = np.arange(L)
shape = np.exp(-n / tau_d) - np.exp(-n / tau_r)
shape = shape / shape.max()
shape_peak = int(np.argmax(shape))

# Poisson arrivals -> sample indices (exponential inter-arrival), with lead-in.
mean_gap = FS / rate
gaps = rng.exponential(mean_gap, n_events).astype(int) + 1
times = np.cumsum(gaps) + 400
total = int(times[-1] + L + 200)

# Energies: 60% photopeak Gaussian (7% FWHM), 40% Compton continuum.
E = np.empty(n_events)
is_pp = rng.random(n_events) < 0.60
sigma_pp = 0.07 * 662.0 / 2.355
E[is_pp] = rng.normal(662.0, sigma_pp, is_pp.sum())
E[~is_pp] = rng.uniform(30.0, COMPTON_EDGE, (~is_pp).sum())
E = np.clip(E, 0.0, None)
amp = E * ADC_PER_KEV

# Build the waveform (overlapping pulses simply sum -> pile-up).
wave = np.full(total, float(BASELINE))
truth = []
for t, a, e in zip(times, amp, E):
    end = min(t + L, total)
    wave[t:end] += a * shape[:end - t]
    truth.append((t + shape_peak, e, a))

wave += rng.normal(0.0, NOISE_SIGMA, total)
adc = np.clip(np.round(wave), 0, ADC_MAX).astype(int)

np.savetxt(f"{outdir}/adc.txt", adc, fmt="%d")
with open(f"{outdir}/truth.csv", "w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["sample", "energy_keV", "amp_adc"])
    for pk, e, a in truth:
        w.writerow([pk, f"{e:.2f}", f"{a:.2f}"])

print(f"generated {n_events} events over {total} samples "
      f"({total / FS * 1e6:.1f} us), rate {rate:.0f}/s -> adc.txt, truth.csv")
