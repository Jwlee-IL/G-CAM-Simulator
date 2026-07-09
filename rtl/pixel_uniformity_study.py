"""Per-crystal pulse-shape / gain non-uniformity through the RTL.

Each crystal in the array has its own gain (light yield / PMT gain) and decay time.
We run the peak-detector RTL per pixel (each with its own gain g_i and decay tau_i),
aggregate the detected pulse-height spectrum, and measure the photopeak FWHM. Per-pixel
gain scatter smears the aggregate photopeak; dividing each pixel's amplitudes by its
(calibrated) gain — gain equalization, as a real gamma camera does — restores it.

This is the ENERGY-domain counterpart to the C# flood/localization uniformity study
(where coded-aperture *position* was found robust). Energy is NOT robust: it needs
per-channel calibration.

Run from rtl/ with iverilog/vvp on PATH:  python pixel_uniformity_study.py
"""
import os
import csv
import subprocess
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

os.chdir(os.path.dirname(os.path.abspath(__file__)))

FS = 100e6
BASELINE = 200
NOISE = 3.0
ADC_PER_KEV = 3000.0 / 662.0
ADC_MAX = 4095
COMPTON_EDGE = 477.0

PIXELS = 25                 # 5x5 array
EVENTS_PER_PIXEL = 500
RATE = 150e3               # mild pile-up
DECAY_MEAN_NS = 55.0       # GAGG:Mg-ish
DECAY_SIGMA = 0.15         # per-pixel pulse-shape spread (fixed, always on)
GAIN_SIGMAS = [0.0, 0.03, 0.06, 0.10, 0.15]

subprocess.run(["iverilog", "-g2012", "-o", "sim.vvp", "tb_peak_detector.sv", "peak_detector.sv"], check=True)


def run_pixel(decay_ns, gain, seed):
    """Generate one pixel's event stream (its decay + gain), run the RTL, return detected
    pulse-height amplitudes (ADC counts) for photopeak-region events."""
    rng = np.random.default_rng(seed)
    tau_d = max(1.0, decay_ns / 10.0)
    tau_r = max(0.5, tau_d / 5.0)
    L = int(max(64, 8 * tau_d))
    n = np.arange(L)
    shape = np.exp(-n / tau_d) - np.exp(-n / tau_r)
    shape /= shape.max()
    pk = int(np.argmax(shape))

    gaps = rng.exponential(FS / RATE, EVENTS_PER_PIXEL).astype(int) + 1
    times = np.cumsum(gaps) + 400
    total = int(times[-1] + L + 400)

    E = np.empty(EVENTS_PER_PIXEL)
    pp = rng.random(EVENTS_PER_PIXEL) < 0.6
    E[pp] = rng.normal(662.0, 0.07 * 662 / 2.355, pp.sum())
    E[~pp] = rng.uniform(30.0, COMPTON_EDGE, (~pp).sum())
    E = np.clip(E, 0.0, None)
    amp = E * ADC_PER_KEV * gain          # per-pixel gain scales the measured amplitude

    wave = np.full(total, float(BASELINE))
    for t, a in zip(times, amp):
        end = min(t + L, total)
        wave[t:end] += a * shape[:end - t]
    wave += rng.normal(0.0, NOISE, total)
    adc = np.clip(np.round(wave), 0, ADC_MAX).astype(int)

    np.savetxt("adc.txt", adc, fmt="%d")
    subprocess.run(["vvp", "sim.vvp"], check=True, capture_output=True)
    det = np.loadtxt("peaks.txt") if os.path.getsize("peaks.txt") > 0 else np.zeros((0, 2))
    if det.ndim == 1:
        det = det.reshape(-1, 2)
    return det[:, 1] if det.size else np.zeros(0)   # amplitudes (ADC)


def fwhm_percent(energies_keV):
    near = energies_keV[(energies_keV > 560) & (energies_keV < 760)]
    if len(near) < 20:
        return float("nan")
    return 2.355 * near.std() / near.mean() * 100.0


rows = []
for gsig in GAIN_SIGMAS:
    grng = np.random.default_rng(999)
    gains = np.clip(grng.normal(1.0, gsig, PIXELS), 0.3, None)
    decays = np.clip(grng.normal(DECAY_MEAN_NS, DECAY_MEAN_NS * DECAY_SIGMA, PIXELS), 5.0, None)

    raw_keV, cal_keV = [], []
    for i in range(PIXELS):
        amps = run_pixel(decays[i], gains[i], seed=1000 + i)
        raw_keV.append(amps / ADC_PER_KEV)              # uncalibrated
        cal_keV.append(amps / gains[i] / ADC_PER_KEV)   # gain-equalized
    raw = np.concatenate(raw_keV) if raw_keV else np.zeros(0)
    cal = np.concatenate(cal_keV) if cal_keV else np.zeros(0)

    fr, fc = fwhm_percent(raw), fwhm_percent(cal)
    rows.append((gsig, fr, fc))
    print(f"gain σ={gsig*100:4.1f}%   FWHM raw={fr:5.1f}%   gain-calibrated={fc:5.1f}%")

with open("pixel_uniformity.csv", "w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["gain_sigma", "fwhm_raw", "fwhm_calibrated"])
    for g, fr, fc in rows:
        w.writerow([f"{g:.3f}", f"{fr:.3f}", f"{fc:.3f}"])

g = np.array([r[0] * 100 for r in rows])
fr = np.array([r[1] for r in rows])
fc = np.array([r[2] for r in rows])
quad = np.sqrt(fc[0] ** 2 + g ** 2)   # expected quadrature broadening
fig, ax = plt.subplots(figsize=(7.5, 5))
ax.plot(g, fr, "o-", color="#c0392b", lw=2, label="raw (uncalibrated)")
ax.plot(g, fc, "s-", color="#4C9F70", lw=2, label="per-pixel gain calibrated")
ax.plot(g, quad, ":", color="gray", label=r"$\sqrt{intrinsic^2 + \sigma_g^2}$")
ax.set_xlabel("per-crystal gain non-uniformity σ (%)")
ax.set_ylabel("aggregate photopeak FWHM (%)")
ax.set_title("Per-crystal gain non-uniformity through the RTL\nenergy resolution needs per-channel calibration (unlike position)")
ax.legend(); ax.grid(alpha=0.3)
fig.tight_layout()
fig.savefig("pixel_uniformity.png", dpi=130)
print("saved pixel_uniformity.png, pixel_uniformity.csv")
