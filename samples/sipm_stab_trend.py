"""Gain-stabilization TELEMETRY over a mission — the trend you would LOG in the field so a
count-rate dip can be explained later ("temperature spiked at min 40, stabilization lagged").

Simulates a mission temperature profile (DAQ self-heating warm-up + an ambient event) and
tracks, for each stabilization method (none / ① bias-comp / ①+② LED / ③ spectral), the
photopeak drift and the fraction of counts kept in the fixed LLD/ULD window. Writes both a
PNG and a CSV (sipm_stab_trend.csv) so the run is trackable/replayable.

Sensor: Hamamatsu S13360-3050CS (samples/sipm/hamamatsu_s13360_3050cs.json).
Usage:  python samples/sipm_stab_trend.py
"""
import os, json
import numpy as np
from math import erf
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
sipm = json.load(open(os.path.join(here, "sipm", "hamamatsu_s13360_3050cs.json"), encoding="utf-8"))
sm = sipm["_stabilizationModel"]

T0 = 25.0          # calibration temperature (°C)
WIN, FWHM = 0.10, 0.07
sigma = FWHM / 2.3548
rng = np.random.default_rng(7)

# --- mission temperature profile (minutes) ---
t = np.arange(0, 120.25, 0.25)
warmup = 8.0 * (1 - np.exp(-t / 20.0))                 # DAQ board self-heating, tau=20 min
ambient = 25.0 + 2.0 * np.sin(2 * np.pi * t / 45.0)    # slow room swing
ambient = ambient - 10.0 * (t > 70)                    # step: taken outdoors (colder) at min 70
Temp = ambient + warmup

# --- per-method peak-position drift over the mission ---
sipm_fixed = sipm["gainTempCoeffPctPerC_fixedBias"] / 100.0
cry = sm["crystalLightYieldTempCoeffPctPerC"] / 100.0
tc = {
    "none":            sipm_fixed + cry,
    "① bias comp":     sm["biasCompResidualPctPerC"] / 100.0 + cry,
    "①+② bias+LED":    sm["ledLockResidualPctPerC"] / 100.0 + cry,
}
col = {"none": "#c0392b", "① bias comp": "#e67e22", "①+② bias+LED": "#2e8b57", "③ spectral": "#8e44ad"}

drift = {k: v * (Temp - T0) for k, v in tc.items()}    # relative peak drift
# ③ spectral: no temperature term; a small random jitter resampled each update (~0.5 min here)
s3 = np.hypot((sm["spectralRefFwhmPct"]/100)/2.3548/np.sqrt(300*10), sm["spectralRefExtrapResidualPct"]/100)
drift["③ spectral"] = rng.normal(0, s3, size=t.size)

def retained(shift):
    lo, hi = 1.0 - WIN, 1.0 + WIN
    z1 = (hi - (1 + shift)) / (sigma * np.sqrt(2)); z2 = (lo - (1 + shift)) / (sigma * np.sqrt(2))
    return 0.5 * (np.vectorize(erf)(z1) - np.vectorize(erf)(z2))
R0 = float(retained(np.array([0.0]))[0])
kept = {k: retained(d) / R0 * 100.0 for k, d in drift.items()}

# --- figure ---
fig, (a1, a2, a3) = plt.subplots(3, 1, figsize=(12, 9.5), sharex=True)

a1.plot(t, Temp, color="#2c3e50", lw=2)
a1.axhline(T0, ls=":", color="#888"); a1.text(1, T0 + 0.3, "calib 25°C", fontsize=8, color="#888")
a1.annotate("DAQ warm-up (+8°C)", (18, Temp[72]), (25, 36), fontsize=8, arrowprops=dict(arrowstyle="->"))
a1.axvline(70, ls="--", color="#3498db"); a1.text(71, 20, "taken outdoors (−10°C)", fontsize=8, color="#2471a3")
a1.set_ylabel("board temp (°C)"); a1.set_title("(1) Mission temperature — log this"); a1.grid(alpha=0.3)

for k, d in drift.items():
    a2.plot(t, d * 100, color=col[k], lw=1.8, label=k)
a2.axhspan(-WIN*100, WIN*100, color="#3498db", alpha=0.08)
a2.text(1, WIN*100*0.72, "±10% window", fontsize=8, color="#2471a3")
a2.set_ylabel("photopeak drift (%)"); a2.set_title("(2) Gain drift per method — replay to explain count dips")
a2.legend(fontsize=8, ncol=4, loc="lower left"); a2.grid(alpha=0.3)

for k, y in kept.items():
    a3.plot(t, y, color=col[k], lw=1.8, label=k)
a3.set_ylabel("counts kept in window (%)"); a3.set_xlabel("mission time (min)")
a3.set_title("(3) Operational impact — window-kept counts"); a3.set_ylim(0, 105)
a3.legend(fontsize=8, ncol=4, loc="lower left"); a3.grid(alpha=0.3)

fig.suptitle("SiPM gain-stabilization telemetry (S13360-3050CS): log T, peak drift, and window-kept counts over the "
             "mission — ① holds through warm-up, ③ stays flat even outdoors; 'none' loses the window on every swing.", fontsize=10)
fig.tight_layout(rect=[0, 0, 1, 0.96])
out = os.path.join(here, "sipm_stab_trend.png")
fig.savefig(out, dpi=130); print("saved", out)

# --- CSV log (trackable / replayable) ---
import csv as _csv
with open(os.path.join(here, "sipm_stab_trend.csv"), "w", newline="") as f:
    w = _csv.writer(f)
    w.writerow(["time_min", "temp_C"] +
               [f"drift_pct_{k}" for k in drift] + [f"kept_pct_{k}" for k in kept])
    for i in range(t.size):
        w.writerow([f"{t[i]:.2f}", f"{Temp[i]:.2f}"] +
                   [f"{drift[k][i]*100:.3f}" for k in drift] + [f"{kept[k][i]:.2f}" for k in kept])
print("saved", os.path.join(here, "sipm_stab_trend.csv"))
print(f"min window-kept:  none {kept['none'].min():.0f}%   ① {kept['① bias comp'].min():.0f}%   "
      f"①+② {kept['①+② bias+LED'].min():.0f}%   ③ {kept['③ spectral'].min():.0f}%")
