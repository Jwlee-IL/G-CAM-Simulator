"""SiPM gain temperature drift → energy-window integrity, with stabilization ①+②.

Reference sensor: Hamamatsu S13360-3050CS (samples/sipm/hamamatsu_s13360_3050cs.json).
Question: as temperature swings, how much does the photopeak walk out of the fixed
LLD/ULD energy window, and how far do ① bias-compensation and ② LED-pulser lock fix it?

Key project fact reused: coded-aperture LOCALIZATION is invariant to a global gain scale
(the balanced MURA decode is scale-invariant) — so temperature drift is an ENERGY-window
problem, not a position problem. Only when the peak walks fully out of the window do the
counts (and hence the noise-limited localization) suffer.

Honest residual: ① fixes SiPM V_br(T); ② (LED) also catches SiPM/electronics aging but
BYPASSES the crystal, so GAGG's own light-yield drift (~-0.15%/°C) survives both — only a
spectral reference (K-40 / reference source, ③) removes it.

Usage:  python samples/sipm_thermal_study.py
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

T0 = 20.0                               # calibration temperature (°C)
T = np.linspace(0, 45, 300)            # ambient / board temperature (°C)
WIN = 0.10                              # ±10% LLD/ULD energy window (per-crystal)
FWHM = 0.07                             # GAGG photopeak FWHM at 662 keV
sigma = FWHM / 2.3548                   # relative Gaussian sigma of the peak

# per-°C peak-position tempco for each stabilization case (SiPM part + crystal light yield)
sipm_fixed = sipm["gainTempCoeffPctPerC_fixedBias"] / 100.0          # -1.8%/°C
cry = sm["crystalLightYieldTempCoeffPctPerC"] / 100.0               # -0.15%/°C (only ③ fixes)
cases = {
    "no compensation":       sipm_fixed + cry,                                    # bias fixed
    "① bias comp":           sm["biasCompResidualPctPerC"] / 100.0 + cry,          # SiPM tracked
    "①+② bias + LED lock":   sm["ledLockResidualPctPerC"] / 100.0 + cry,           # + optical gain lock
}
col = {"no compensation": "#c0392b", "① bias comp": "#e67e22", "①+② bias + LED lock": "#2e8b57"}

def retained(peak_shift_rel):
    """Fraction of the Gaussian photopeak still inside the fixed ±WIN window when the
    peak center has moved by peak_shift_rel (relative to the nominal 1.0 line)."""
    lo, hi = 1.0 - WIN, 1.0 + WIN
    mu = 1.0 + peak_shift_rel
    z1 = (hi - mu) / (sigma * np.sqrt(2)); z2 = (lo - mu) / (sigma * np.sqrt(2))
    return 0.5 * (np.vectorize(erf)(z1) - np.vectorize(erf)(z2))

R0 = retained(0.0)   # at calibration T, ~full window capture

fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(14, 5.6))

# (1) peak-position error (keV-equivalent, % of 662) vs temperature
for name, tc in cases.items():
    ax1.plot(T, (tc * (T - T0)) * 100.0, color=col[name], lw=2.2, label=name)
ax1.axhspan(-WIN*100, WIN*100, color="#3498db", alpha=0.10)
ax1.text(1, WIN*100*0.8, f"±{WIN*100:.0f}% energy window", color="#2471a3", fontsize=8)
ax1.axvline(T0, ls=":", color="#888"); ax1.text(T0+0.4, -16, "calib T", fontsize=8, color="#888")
ax1.set_xlabel("temperature (°C)"); ax1.set_ylabel("photopeak position error (% of 662 keV)")
ax1.set_title("(1) Peak walk vs T\n① kills the SiPM part; crystal light-yield is the floor (needs ③)")
ax1.legend(fontsize=8); ax1.grid(alpha=0.3)

# (2) window-retained effective counts (%) vs temperature  + position invariance
for name, tc in cases.items():
    ax2.plot(T, retained(tc * (T - T0)) / R0 * 100.0, color=col[name], lw=2.2, label=name)
ax2.axhline(100, ls=":", color="#888")
ax2.axhline(98, ls="--", color="#2e8b57", lw=0.8)
ax2.annotate("localization RMS: flat\n(gain-scale invariant — a position, not energy, story)",
             (23, 55), (6, 40), fontsize=8, color="#555",
             arrowprops=dict(arrowstyle="->", color="#555"))
ax2.set_xlabel("temperature (°C)"); ax2.set_ylabel("counts kept in the energy window (%)")
ax2.set_title("(2) Window integrity vs T\nno-comp falls off a cliff; ①+② hold flat (multi-isotope windows survive)")
ax2.legend(fontsize=8, loc="lower center"); ax2.grid(alpha=0.3); ax2.set_ylim(0, 105)

fig.suptitle("SiPM (Hamamatsu S13360-3050CS) gain drift is an ENERGY-WINDOW problem, not a position one. "
             "① bias-comp + ② LED lock hold the window; the crystal's own light-yield drift is the ③-only residual.", fontsize=10)
fig.tight_layout(rect=[0, 0, 1, 0.94])
out = os.path.join(here, "sipm_thermal.png")
fig.savefig(out, dpi=130); print("saved", out)

# text: how far can T drift before a ±10% window loses 10% of counts?
print(f"\nSiPM: {sipm['name']}  fixed-bias gain tempco {sipm['gainTempCoeffPctPerC_fixedBias']}%/°C")
print(f"±{WIN*100:.0f}% window, {FWHM*100:.0f}% FWHM peak, calib {T0}°C")
for name, tc in cases.items():
    # find |ΔT| where retained drops to 90% of R0
    dT = np.linspace(0, 60, 6000)
    keep = retained(tc * dT) / R0
    idx = np.argmax(keep < 0.90)
    lim = dT[idx] if keep[-1] < 0.90 else float("inf")
    print(f"  {name:22s}: tempco {tc*100:+.2f}%/°C  ->  10% count loss at ΔT = {lim if lim!=float('inf') else '>60'}{'°C' if lim!=float('inf') else ''}")
